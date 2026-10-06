namespace _Core.Platform.Core
{
    /// <summary>
    /// A product from the platform's in-app purchase catalog.
    /// </summary>
    public class PlatformProduct
    {
        /// <summary>
        /// Product ID as set up in the platform's console.
        /// </summary>
        public string Id { get; }

        public string Title { get; }
        public string Description { get; }

        /// <summary>
        /// Price formatted by the platform for display, currency included (for example "29,99 TL" or "50 YAN").
        /// </summary>
        public string Price { get; }

        /// <summary>
        /// Currency code of <see cref="Price"/> (for example "TRY", "YAN"); may be empty.
        /// </summary>
        public string CurrencyCode { get; }

        public PlatformProduct(
            string id,
            string title,
            string description,
            string price,
            string currencyCode)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Price = price ?? string.Empty;
            CurrencyCode = currencyCode ?? string.Empty;
        }
    }
}
