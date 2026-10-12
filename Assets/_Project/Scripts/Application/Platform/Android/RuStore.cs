using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Maze.Application.Platform
{
    /// <summary>
    /// Keys of the Android build for RuStore — the one place to set them; rebuild after a change. Empty means "not yet":
    /// AppMetrica stays off (events only go to the log), ads use Yandex's demo units (test ads, no income).
    /// </summary>
    public static class RuStoreKeys
    {
        /// <summary>AppMetrica API key (appmetrica.yandex.ru → the app's settings).</summary>
        public const string AppMetricaApiKey = "";

        /// <summary>Rewarded ad unit id (Yandex Advertising Network partner interface, "R-M-…").</summary>
        public const string RewardedAdUnit = "";

        /// <summary>Interstitial ad unit id ("R-M-…").</summary>
        public const string InterstitialAdUnit = "";

        public const string DemoRewardedAdUnit = "demo-rewarded-yandex";

        public const string DemoInterstitialAdUnit = "demo-interstitial-yandex";

        public static string Rewarded => Or(RewardedAdUnit, DemoRewardedAdUnit);

        public static string Interstitial => Or(InterstitialAdUnit, DemoInterstitialAdUnit);

        /// <summary>The ads are Yandex's demo ones (no real units yet).</summary>
        public static bool DemoAds => string.IsNullOrWhiteSpace(RewardedAdUnit) || string.IsNullOrWhiteSpace(InterstitialAdUnit);

        private static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    /// <summary>
    /// The Android app from RuStore: no site SDK — the language is the system's, saves are local (<c>PlayerPrefs</c>);
    /// the store's rules: a support contact and the privacy policy in the settings. Ads — <see cref="YandexMobileAdsProvider"/>,
    /// analytics — <see cref="AppMetricaSink"/>.
    /// </summary>
    public sealed class RuStorePlatform : IGamePlatform
    {
        private static readonly PlatformTraits RuStoreTraits = new PlatformTraits
        {
            SupportEmail = Contacts.SupportEmail,
            PrivacyPolicyUrl = Contacts.PrivacyPolicyUrl,
        };

        public string Name => "RuStore";
        public UniTask InitializeAsync(CancellationToken cancellation) => UniTask.CompletedTask;
        public string Language => null;
        public ICloudSave CloudSave => null;
        public void LoadingFinished() { }
        public void GameplayStarted() { }
        public void GameplayStopped() { }
        public void HappyMoment() { }
        public void GameProgress(int percent) { }
        public bool SupportsSignIn => false;
        public bool IsSignedIn => false;
        public UniTask<bool> SignInAsync(CancellationToken cancellation) => UniTask.FromResult(false);
        public event Action<bool> PauseRequested { add { } remove { } }
        public PlatformTraits Traits => RuStoreTraits;
        public bool MuteAudio => false;
        public event Action<bool> MuteAudioChanged { add { } remove { } }
        public event Action SignedIn { add { } remove { } }
    }
}
