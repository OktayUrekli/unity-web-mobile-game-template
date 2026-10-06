using System;
using System.Collections.Generic;
using _Core.Platform.Core;

namespace _Core.Platform.Services.Purchase
{
    /// <summary>
    /// Provides platform-independent in-app purchases (for example Yandex Games payments or Google Play Billing).
    /// Products are set up in the platform's console; the game refers to them by their product ID.
    /// Deliver an item only on <see cref="PurchaseResult.Completed"/> or when it shows up in
    /// <see cref="GetOwnedPurchases"/>, then <see cref="Consume"/> it if it can be bought again.
    /// </summary>
    public interface IPurchaseService
    {
        /// <summary>
        /// True when purchases can be made right now (SDK ready and payments enabled for the game).
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Fetches the product catalog with prices in the player's currency; the callback receives an empty
        /// list when purchases are unavailable.
        /// </summary>
        void GetProducts(Action<IReadOnlyList<PlatformProduct>> callback);

        /// <summary>
        /// Opens the platform purchase dialog for <paramref name="productId"/>. The callback receives the result
        /// and, on <see cref="PurchaseResult.Completed"/>, the purchase to deliver; otherwise null.
        /// </summary>
        void Purchase(string productId, Action<PurchaseResult, PlatformPurchase> callback);

        /// <summary>
        /// Fetches the purchases the player owns that were not consumed: permanent items (such as "remove ads")
        /// and consumables bought but not delivered yet, for example after a crash. Call it once at start and
        /// deliver what it returns. Empty list when unavailable.
        /// </summary>
        void GetOwnedPurchases(Action<IReadOnlyList<PlatformPurchase>> callback);

        /// <summary>
        /// Marks a delivered consumable purchase as used so the product can be bought again; the callback
        /// receives true on success. Never consume a permanent item.
        /// </summary>
        void Consume(PlatformPurchase purchase, Action<bool> callback);
    }
}
