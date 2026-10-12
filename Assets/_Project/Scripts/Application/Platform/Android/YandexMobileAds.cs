using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Core.Common;
using YandexMobileAds;
using YandexMobileAds.Base;

namespace Maze.Application.Platform
{
    /// <summary>
    /// Ads of the Android build: Yandex Mobile Ads (Yandex Advertising Network), units from <see cref="RuStoreKeys"/>.
    /// One rewarded and one interstitial ad are kept loaded ahead (a new one after each show; a failed load is retried
    /// later, longer and longer). The player's rewarded button waits a few seconds for an ad still loading; an
    /// interstitial that is not ready is skipped. The ad's own activity covers the game: Unity is paused meanwhile and
    /// the SDK's callbacks arrive when it resumes. <c>started</c> is the ad really on screen.
    /// </summary>
    public sealed class YandexMobileAdsProvider : IAdProvider, IDisposable
    {
        /// <summary>How long the player's rewarded button waits for an ad that is still loading.</summary>
        public static readonly TimeSpan RewardedWait = TimeSpan.FromSeconds(4);

        /// <summary>Seconds before loading again after the 1st, 2nd… failure in a row (no fill, no network).</summary>
        private static readonly int[] RetryDelays = { 10, 30, 60, 120 };

        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly Slot _rewarded = new Slot(AdKind.Rewarded, RuStoreKeys.Rewarded);
        private readonly Slot _interstitial = new Slot(AdKind.Interstitial, RuStoreKeys.Interstitial);
        private RewardedAdLoader _rewardedLoader;
        private InterstitialAdLoader _interstitialLoader;

        public string Name => "YandexMobileAds";

        public bool InterstitialBetweenLevelsOnly => false;

        public UniTask InitializeAsync(CancellationToken cancellation)
        {
            if (RuStoreKeys.DemoAds)
                GameLog.Warning(LogChannel.Analytics, "Yandex Mobile Ads: demo ad units (RuStoreKeys) — test ads, no income.");
            _rewardedLoader = new RewardedAdLoader();
            _interstitialLoader = new InterstitialAdLoader();
            Load(_rewarded);
            Load(_interstitial);
            return UniTask.CompletedTask;
        }

        public async UniTask<AdResult> ShowAsync(AdKind kind, string placement, Action started, CancellationToken cancellation)
        {
            var slot = kind == AdKind.Rewarded ? _rewarded : _interstitial;
            if (!slot.Ready)
            {
                Load(slot);
                // The player asked for the reward: worth a short wait. An interstitial is never waited for.
                if (kind == AdKind.Rewarded && slot.Loading)
                    await UniTask.WaitUntil(() => !slot.Loading, cancellationToken: cancellation)
                        .TimeoutWithoutException(RewardedWait, DelayType.Realtime);
                if (!slot.Ready) return AdResult.Unavailable;
            }

            var interstitial = slot.Interstitial;
            var rewardedAd = slot.Rewarded;
            slot.Interstitial = null;
            slot.Rewarded = null;
            var completion = new UniTaskCompletionSource<AdResult>();
            var rewarded = false;
            try
            {
                if (rewardedAd != null)
                {
                    rewardedAd.OnAdShown += (_, _) => started?.Invoke();
                    rewardedAd.OnRewarded += (_, _) => rewarded = true;
                    rewardedAd.OnAdDismissed += (_, _) => CompleteAfterCallbacks(completion, () => rewarded ? AdResult.Rewarded : AdResult.Closed);
                    rewardedAd.OnAdFailedToShow += (_, e) => Fail(completion, placement, e?.Message);
                    rewardedAd.Show();
                }
                else
                {
                    interstitial.OnAdShown += (_, _) => started?.Invoke();
                    interstitial.OnAdDismissed += (_, _) => CompleteAfterCallbacks(completion, () => AdResult.Closed);
                    interstitial.OnAdFailedToShow += (_, e) => Fail(completion, placement, e?.Message);
                    interstitial.Show();
                }

                return await completion.Task.AttachExternalCancellation(cancellation);
            }
            finally
            {
                rewardedAd?.Destroy();
                interstitial?.Destroy();
                Load(slot); // the next one, ready for the next request
            }
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _rewarded.Clear();
            _interstitial.Clear();
        }

        /// <summary>
        /// The reward callback may come right after the dismissal (both are queued for the main thread when Unity
        /// resumes): the result is read a frame later.
        /// </summary>
        private static void CompleteAfterCallbacks(UniTaskCompletionSource<AdResult> completion, Func<AdResult> result)
        {
            UniTask.DelayFrame(2, PlayerLoopTiming.Update).ContinueWith(() => completion.TrySetResult(result())).Forget();
        }

        private static void Fail(UniTaskCompletionSource<AdResult> completion, string placement, string message)
        {
            GameLog.Warning(LogChannel.Analytics, $"Yandex Mobile Ads: '{placement}' failed to show: {message}");
            completion.TrySetResult(AdResult.Failed);
        }

        private void Load(Slot slot)
        {
            if (slot.Ready || slot.Loading || _lifetime.IsCancellationRequested) return;
            if (_rewardedLoader == null || _interstitialLoader == null) return; // not initialized
            slot.Loading = true;
            var request = new AdRequest(slot.Unit);
            if (slot.Kind == AdKind.Rewarded)
                _rewardedLoader.LoadAd(request, ad => OnLoaded(slot, ad, null), error => OnFailed(slot, error));
            else
                _interstitialLoader.LoadAd(request, ad => OnLoaded(slot, null, ad), error => OnFailed(slot, error));
        }

        private void OnLoaded(Slot slot, RewardedAd rewarded, Interstitial interstitial)
        {
            slot.Loading = false;
            slot.Failures = 0;
            if (_lifetime.IsCancellationRequested)
            {
                rewarded?.Destroy();
                interstitial?.Destroy();
                return;
            }

            slot.Rewarded = rewarded;
            slot.Interstitial = interstitial;
        }

        private void OnFailed(Slot slot, AdFailedToLoadEventArgs error)
        {
            slot.Loading = false;
            var delay = RetryDelays[Math.Min(slot.Failures, RetryDelays.Length - 1)];
            slot.Failures++;
            GameLog.Info(LogChannel.Analytics, $"Yandex Mobile Ads: no {slot.Kind} ad ({error?.Message}); again in {delay} s.");
            RetryAsync(slot, delay).Forget();
        }

        private async UniTaskVoid RetryAsync(Slot slot, int seconds)
        {
            var cancelled = await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime,
                cancellationToken: _lifetime.Token).SuppressCancellationThrow();
            if (!cancelled) Load(slot);
        }

        /// <summary>One loaded-ahead ad of a kind.</summary>
        private sealed class Slot
        {
            public readonly AdKind Kind;
            public readonly string Unit;
            public RewardedAd Rewarded;
            public Interstitial Interstitial;
            public bool Loading;
            public int Failures;

            public Slot(AdKind kind, string unit)
            {
                Kind = kind;
                Unit = unit;
            }

            public bool Ready => Rewarded != null || Interstitial != null;

            public void Clear()
            {
                Rewarded?.Destroy();
                Interstitial?.Destroy();
                Rewarded = null;
                Interstitial = null;
            }
        }
    }
}
