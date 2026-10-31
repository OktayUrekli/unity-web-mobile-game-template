using DG.Tweening;
using UnityEngine;

namespace _Core.UI
{
    /// <summary>
    /// DOTween helpers shared by the <c>_Core</c> UI classes.
    /// </summary>
    internal static class UITween
    {
        /// <summary>
        /// Tweens the alpha of <paramref name="group"/> to <paramref name="endAlpha"/>, with the group as the tween
        /// target. Callers add SetUpdate/SetLink/callbacks. DOTween.To instead of the CanvasGroup.DOFade module
        /// shortcut: the DOTween modules compile into Assembly-CSharp, which the _Core assembly cannot reference.
        /// </summary>
        internal static Tweener Fade(CanvasGroup group, float endAlpha, float duration) =>
            DOTween.To(() => group.alpha, alpha => group.alpha = alpha, endAlpha, duration)
                .SetTarget(group);
    }
}
