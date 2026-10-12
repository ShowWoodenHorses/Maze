using System.Collections.Generic;
using Maze.Application.Platform;
using NUnit.Framework;

namespace Maze.Tests.EditMode.Progress
{
    public sealed class RuStoreTests
    {
        [Test]
        public void AppMetricaJson_TypesAndEscaping()
        {
            var parameters = new Dictionary<string, object>
            {
                ["level"] = "Level_03",
                ["stars"] = 3,
                ["time"] = 12.5f,
                ["record"] = true,
                ["quote"] = "a\"b\\c\nd",
            };

            Assert.AreEqual("{\"level\":\"Level_03\",\"stars\":3,\"time\":12.5,\"record\":true,\"quote\":\"a\\\"b\\\\c\\nd\"}",
                AppMetricaSink.ToJson(parameters));
        }

        [Test]
        public void AppMetricaJson_FloatsAreInvariant()
        {
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");
                Assert.AreEqual("{\"fps\":59.25}", AppMetricaSink.ToJson(new Dictionary<string, object> { ["fps"] = 59.25f }));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        [Test]
        public void Platform_ShowsSupportAndPrivacy_NoCloudOrLanguage()
        {
            var platform = new RuStorePlatform();

            Assert.AreEqual(Contacts.SupportEmail, platform.Traits.SupportEmail);
            Assert.AreEqual(Contacts.PrivacyPolicyUrl, platform.Traits.PrivacyPolicyUrl);
            StringAssert.StartsWith("https://", platform.Traits.PrivacyPolicyUrl);
            Assert.IsNull(platform.CloudSave);
            Assert.IsNull(platform.Language);
            Assert.IsFalse(platform.SupportsSignIn);
        }

        [Test]
        public void AdUnits_WithoutKeys_AreYandexDemoUnits()
        {
            if (!RuStoreKeys.DemoAds) Assert.Ignore("Real ad units are set.");
            Assert.AreEqual(RuStoreKeys.DemoRewardedAdUnit, RuStoreKeys.Rewarded);
            Assert.AreEqual(RuStoreKeys.DemoInterstitialAdUnit, RuStoreKeys.Interstitial);
        }
    }
}
