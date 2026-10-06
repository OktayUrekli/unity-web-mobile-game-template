using System.Collections.Generic;
using _Core.Platform.Core;
using _Core.Platform.Services.Game.Null;
using _Core.Platform.Services.Purchase;
using _Core.Platform.Services.Purchase.Null;
using NUnit.Framework;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for Null services that game code relies on: every callback fires, so "unsupported" never
    /// turns into "hangs".
    /// </summary>
    public class NullServiceTests
    {
        [Test]
        public void NullPurchaseService_GetProducts_AnswersWithEmptyList()
        {
            IReadOnlyList<PlatformProduct> products = null;
            int calls = 0;

            new NullPurchaseService().GetProducts(result => { products = result; calls++; });

            Assert.AreEqual(1, calls);
            Assert.IsNotNull(products);
            Assert.IsEmpty(products);
        }

        [Test]
        public void NullPurchaseService_Purchase_AnswersNotAvailableWithoutPurchase()
        {
            var service = new NullPurchaseService();
            PurchaseResult? result = null;
            PlatformPurchase purchase = new PlatformPurchase("x", "y", "z");

            service.Purchase("remove_ads", (r, p) => { result = r; purchase = p; });

            Assert.IsFalse(service.IsAvailable);
            Assert.AreEqual(PurchaseResult.NotAvailable, result);
            Assert.IsNull(purchase);
        }

        [Test]
        public void NullPurchaseService_GetOwnedPurchases_AnswersWithEmptyList()
        {
            IReadOnlyList<PlatformPurchase> purchases = null;

            new NullPurchaseService().GetOwnedPurchases(result => purchases = result);

            Assert.IsNotNull(purchases);
            Assert.IsEmpty(purchases);
        }

        [Test]
        public void NullPurchaseService_Consume_AnswersFalse()
        {
            bool? consumed = null;

            new NullPurchaseService().Consume(new PlatformPurchase("coins_100", "token", null), result => consumed = result);

            Assert.AreEqual(false, consumed);
        }

        [Test]
        public void NullGameService_ReportsNoMuteNoPauseAndNoLanguage()
        {
            var service = new NullGameService();

            Assert.IsFalse(service.IsAudioMutedByPlatform);
            Assert.IsFalse(service.IsPausedByPlatform);
            Assert.IsNull(service.Language);
        }
    }
}
