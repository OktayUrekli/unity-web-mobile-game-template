using System;
using System.Collections.Generic;
using _Core.Platform.Core;

namespace _Core.Platform.Services.Purchase.Null
{
    /// <summary>
    /// Safe fallback when the platform has no in-app purchases. Every callback still fires.
    /// </summary>
    public class NullPurchaseService : IPurchaseService
    {
        public bool IsAvailable => false;

        public void GetProducts(Action<IReadOnlyList<PlatformProduct>> callback)
        {
            callback?.Invoke(Array.Empty<PlatformProduct>());
        }

        public void Purchase(string productId, Action<PurchaseResult, PlatformPurchase> callback)
        {
            callback?.Invoke(PurchaseResult.NotAvailable, null);
        }

        public void GetOwnedPurchases(Action<IReadOnlyList<PlatformPurchase>> callback)
        {
            callback?.Invoke(Array.Empty<PlatformPurchase>());
        }

        public void Consume(PlatformPurchase purchase, Action<bool> callback)
        {
            callback?.Invoke(false);
        }
    }
}
