using _Core.Localization;
using NUnit.Framework;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for <c>LocalizationManager.MatchLanguage</c>, which maps a platform, saved or device language
    /// code to a shipped Locale.
    /// </summary>
    public class LanguageMatchTests
    {
        private static readonly string[] Shipped = { "en", "es", "ru", "tr", "pt-BR" };

        [TestCase("ru", 2)]
        [TestCase("RU", 2)]
        [TestCase("en-US", 0)]
        [TestCase("tr_TR", 3)]
        [TestCase("pt", 4)]
        [TestCase("pt-br", 4)]
        [TestCase("de", -1)]
        [TestCase("", -1)]
        [TestCase(null, -1)]
        public void MatchLanguage_ReturnsIndexOfShippedLanguage(string requested, int expectedIndex)
        {
            Assert.AreEqual(expectedIndex, LocalizationManager.MatchLanguage(requested, Shipped));
        }

        [Test]
        public void MatchLanguage_ExactMatchWinsOverLanguageOnlyMatch()
        {
            string[] shipped = { "pt", "pt-BR" };

            Assert.AreEqual(1, LocalizationManager.MatchLanguage("pt-BR", shipped));
            Assert.AreEqual(0, LocalizationManager.MatchLanguage("pt-PT", shipped));
        }
    }
}
