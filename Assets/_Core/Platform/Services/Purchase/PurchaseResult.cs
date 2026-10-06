namespace _Core.Platform.Services.Purchase
{
    /// <summary>
    /// Outcome of <see cref="IPurchaseService.Purchase"/>. Only <see cref="Completed"/> means the item may be delivered.
    /// </summary>
    public enum PurchaseResult
    {
        /// <summary>
        /// The player paid; deliver the item.
        /// </summary>
        Completed,

        /// <summary>
        /// The player closed the purchase dialog.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The platform reported an error (unknown product, payment declined, SDK error).
        /// </summary>
        Failed,

        /// <summary>
        /// The payment is not final yet (for example a Google Play pending payment). Deliver the item when it
        /// shows up in <see cref="IPurchaseService.GetOwnedPurchases"/>.
        /// </summary>
        Pending,

        /// <summary>
        /// Purchases are not supported or not ready on this platform.
        /// </summary>
        NotAvailable
    }
}
