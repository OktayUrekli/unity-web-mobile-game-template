namespace _Core.Platform.Core
{
    /// <summary>
    /// A purchase made through the platform, owned by the player until it is consumed.
    /// </summary>
    public class PlatformPurchase
    {
        /// <summary>
        /// ID of the purchased product.
        /// </summary>
        public string ProductId { get; }

        /// <summary>
        /// Platform token identifying this purchase; consuming uses it.
        /// </summary>
        public string PurchaseToken { get; }

        /// <summary>
        /// Platform-signed purchase data a game server can verify; may be empty.
        /// </summary>
        public string Receipt { get; }

        public PlatformPurchase(
            string productId,
            string purchaseToken,
            string receipt)
        {
            ProductId = productId ?? string.Empty;
            PurchaseToken = purchaseToken ?? string.Empty;
            Receipt = receipt ?? string.Empty;
        }
    }
}
