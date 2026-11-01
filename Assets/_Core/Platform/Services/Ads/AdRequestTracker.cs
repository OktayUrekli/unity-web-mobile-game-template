using System;
using _Core.Events;
using _Core.Events.Ads;
using UnityEngine;

namespace _Core.Platform.Services.Ads
{
    /// <summary>
    /// The one full-screen ad request a platform bridge has in flight, and the order of what it owes: one
    /// <see cref="AdStartedEvent"/> when the ad starts, one <see cref="AdFinishedEvent"/> for a started ad, then the
    /// caller's callback, after the request is released so that the callback may request the next ad.
    /// <para>
    /// When <see cref="AdPolicyService"/> gives up on the request (<see cref="AdRequestCompletedEvent"/> with
    /// <see cref="AdResult.TimedOut"/>) it has published the finish event of a started ad itself, so none is owed any
    /// more. The request then stays in flight until the SDK answers (new requests get <see cref="AdResult.InProgress"/>),
    /// except a request whose ad never started when the bridge asks for it to be released.
    /// </para>
    /// <para>
    /// Platform bridges (Assembly-CSharp) map their SDK callbacks onto <see cref="NotifyStarted"/>,
    /// <see cref="NotifyRewardEarned"/> and <see cref="Complete"/>. Main thread only: a bridge whose SDK calls back on
    /// other threads marshals the callback first.
    /// </para>
    /// </summary>
    internal sealed class AdRequestTracker
    {
        private readonly bool _releaseUnstartedOnTimeout;
        private readonly Action _onTimedOut;

        private Action<AdResult> _callback;
        private bool _isFinishOwed;

        /// <param name="releaseUnstartedOnTimeout">
        /// True ends a request whose ad never started as soon as AdPolicyService gives up on it. The tracker ignores
        /// later SDK answers for it only while no newer request is in flight, so a bridge whose SDK may still answer it
        /// tags its SDK callbacks with <see cref="Id"/> and checks <see cref="IsCurrent"/>. False keeps it in flight
        /// until the SDK answers, for SDKs that ignore new requests until then (CrazySDK).
        /// </param>
        /// <param name="onTimedOut">
        /// Called once when AdPolicyService gives up on the request, while <see cref="HasStarted"/> still describes it
        /// and before an unstarted request is released: undo what the SDK changed for the ad there.
        /// </param>
        public AdRequestTracker(bool releaseUnstartedOnTimeout, Action onTimedOut = null)
        {
            _releaseUnstartedOnTimeout = releaseUnstartedOnTimeout;
            _onTimedOut = onTimedOut;
        }

        /// <summary>
        /// True from <see cref="TryBegin"/> until the request completes or is released after a timeout.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Format of the request in flight, or of the last one.
        /// </summary>
        public AdType AdType { get; private set; }

        /// <summary>
        /// Changes with every request. A bridge whose SDK may answer an abandoned request after a newer one began tags
        /// its SDK callbacks with it and checks <see cref="IsCurrent"/>.
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// True once the ad of the request in flight has started; false while none is in flight.
        /// </summary>
        public bool HasStarted { get; private set; }

        /// <summary>
        /// True once AdPolicyService has given up on the request in flight (the SDK may still answer it); false while
        /// none is in flight.
        /// </summary>
        public bool IsTimedOut { get; private set; }

        /// <summary>
        /// True once the SDK reported the reward of the request in flight; false while none is in flight.
        /// </summary>
        public bool IsRewardEarned { get; private set; }

        /// <summary>
        /// True when <paramref name="id"/> is the <see cref="Id"/> of the request still in flight.
        /// </summary>
        public bool IsCurrent(int id)
        {
            return IsActive && id == Id;
        }

        /// <summary>
        /// Starts tracking a request for <paramref name="callback"/>. Returns false, and changes nothing, while another
        /// request is in flight; the bridge then answers <see cref="AdResult.InProgress"/> itself.
        /// </summary>
        public bool TryBegin(AdType adType, Action<AdResult> callback)
        {
            if (IsActive)
                return false;

            Id++;
            IsActive = true;
            AdType = adType;
            _callback = callback;

            // AdPolicyService reports a timeout with this event.
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
            return true;
        }

        /// <summary>
        /// The ad started: publishes <see cref="AdStartedEvent"/> once per request, also for an ad that starts after a
        /// timeout while its request is still in flight. Returns false when ignored (no request in flight, or its ad
        /// already started).
        /// </summary>
        public bool NotifyStarted()
        {
            if (!IsActive || HasStarted)
                return false;

            HasStarted = true;
            _isFinishOwed = true;
            EventBus.Publish(new AdStartedEvent());
            return true;
        }

        /// <summary>
        /// The SDK reported the reward of the request in flight; ignored while none is in flight.
        /// </summary>
        public void NotifyRewardEarned()
        {
            if (IsActive)
                IsRewardEarned = true;
        }

        /// <summary>
        /// Ends the request in flight with <paramref name="result"/>: releases it, publishes the
        /// <see cref="AdFinishedEvent"/> a started ad still owes, then invokes the caller's callback (after a timeout
        /// too; AdPolicyService ignores it then). Returns false, and does nothing, while no request is in flight.
        /// </summary>
        public bool Complete(AdResult result)
        {
            if (!IsActive)
                return false;

            Action<AdResult> callback = _callback;
            bool isFinishOwed = _isFinishOwed;
            Release();

            // EventBus isolates handler exceptions, so the callback below always runs.
            if (isFinishOwed)
                EventBus.Publish(new AdFinishedEvent(result == AdResult.Completed));

            try
            {
                callback?.Invoke(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            return true;
        }

        private void Release()
        {
            IsActive = false;
            HasStarted = false;
            IsTimedOut = false;
            IsRewardEarned = false;
            _isFinishOwed = false;
            _callback = null;
            EventBus.Unsubscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
        }

        private void OnAdRequestCompleted(AdRequestCompletedEvent gameEvent)
        {
            if (gameEvent.Result != AdResult.TimedOut || !IsActive || IsTimedOut)
                return;

            int id = Id;
            IsTimedOut = true;

            // AdPolicyService has published AdFinishedEvent(false) itself if the ad had started.
            _isFinishOwed = false;

            try
            {
                _onTimedOut?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            // The hook may have completed the request already.
            if (_releaseUnstartedOnTimeout && IsCurrent(id) && !HasStarted)
                Release();
        }
    }
}
