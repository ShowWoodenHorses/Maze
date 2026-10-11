using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Flow;
using Maze.Application.Services;
using Maze.Core.Common;
using Maze.Gameplay.Level;

namespace Maze.Application.Platform
{
    /// <summary>
    /// Saves kept by the platform for the player (Yandex Games player data, CrazyGames Data…): they follow the player
    /// between devices and survive a new build at a new address, unlike the browser's own storage.
    /// </summary>
    public interface ICloudSave
    {
        /// <summary>The save JSON; null when the player has none yet. Throws when the platform could not answer.</summary>
        UniTask<string> ReadAsync(CancellationToken cancellation);

        UniTask WriteAsync(string json, CancellationToken cancellation);
    }

    /// <summary>
    /// The site the web build runs on (its SDK): start-up, language, cloud saves and the moments it wants to know about.
    /// Ads and analytics are separate (<see cref="IAdProvider"/>, <see cref="IAnalyticsSink"/>). Knows nothing about
    /// the game: <see cref="PlatformEvents"/> tells it what happens.
    /// </summary>
    public interface IGamePlatform
    {
        string Name { get; }

        /// <summary>Starts the SDK. A failure is logged and the game goes on as on <see cref="GenericPlatform"/>.</summary>
        UniTask InitializeAsync(CancellationToken cancellation);

        /// <summary>The player's language on the site ("ru", "en"…); null when the site does not say.</summary>
        string Language { get; }

        /// <summary>Null when the site keeps no saves (then only the browser's storage is used).</summary>
        ICloudSave CloudSave { get; }

        /// <summary>The game finished loading and can be played (the main menu is shown for the first time).</summary>
        void LoadingFinished();

        /// <summary>A level is running (not paused, not in a menu or a window).</summary>
        void GameplayStarted();

        void GameplayStopped();

        /// <summary>A moment of joy worth celebrating on the site (a level with all stars).</summary>
        void HappyMoment();
    }

    /// <summary>No site: the editor, a local web build, Android. Nothing to tell, no cloud.</summary>
    public sealed class GenericPlatform : IGamePlatform
    {
        public string Name => "Generic";
        public UniTask InitializeAsync(CancellationToken cancellation) => UniTask.CompletedTask;
        public string Language => null;
        public ICloudSave CloudSave => null;
        public void LoadingFinished() { }
        public void GameplayStarted() { }
        public void GameplayStopped() { }
        public void HappyMoment() { }
    }

    /// <summary>
    /// The platform as an application service, initialized first (saves and language depend on it). A platform that
    /// fails to start never stops the game: it is then treated as having no language and no cloud, and is not told
    /// about anything.
    /// </summary>
    public sealed class PlatformService : IApplicationService
    {
        private readonly IGamePlatform _platform;
        private bool _ready;

        public PlatformService(IGamePlatform platform)
        {
            _platform = platform;
        }

        public string Name => "Platform";

        /// <summary>The platform's name ("Generic", "Yandex"…), for logs and analytics.</summary>
        public string PlatformName => _platform.Name;

        public string Language => _ready ? _platform.Language : null;

        public ICloudSave CloudSave => _ready ? _platform.CloudSave : null;

        public async UniTask InitializeAsync(CancellationToken cancellation)
        {
            try
            {
                await _platform.InitializeAsync(cancellation);
                _ready = true;
                GameLog.Info(LogChannel.Bootstrap, $"Platform '{_platform.Name}': language {Language ?? "-"}, " +
                                                   $"cloud saves {(CloudSave != null ? "yes" : "no")}.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                GameLog.Exception(LogChannel.Bootstrap, e, $"Platform '{_platform.Name}' failed to start; going on without it");
            }
        }

        public void LoadingFinished() => Tell(p => p.LoadingFinished(), nameof(LoadingFinished));
        public void GameplayStarted() => Tell(p => p.GameplayStarted(), nameof(GameplayStarted));
        public void GameplayStopped() => Tell(p => p.GameplayStopped(), nameof(GameplayStopped));
        public void HappyMoment() => Tell(p => p.HappyMoment(), nameof(HappyMoment));

        private void Tell(Action<IGamePlatform> call, string what)
        {
            if (!_ready) return;
            try
            {
                call(_platform);
            }
            catch (Exception e)
            {
                GameLog.Exception(LogChannel.Bootstrap, e, $"Platform '{_platform.Name}': {what} failed");
            }
        }
    }

    /// <summary>
    /// Tells the platform what the game does, from <see cref="GameFlow"/> states: loading finished (the first main
    /// menu), gameplay running only in <see cref="GameFlowState.Playing"/> (pause, map, windows, ads between levels —
    /// stopped), a happy moment for a level finished with all stars.
    /// </summary>
    public sealed class PlatformEvents : IDisposable
    {
        private readonly GameFlow _flow;
        private readonly PlatformService _platform;
        private bool _initialized;
        private bool _loaded;
        private bool _playing;

        public PlatformEvents(GameFlow flow, PlatformService platform)
        {
            _flow = flow;
            _platform = platform;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _flow.StateChanged += OnStateChanged;
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _flow.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameFlowState state)
        {
            if (!_loaded && state == GameFlowState.MainMenu)
            {
                _loaded = true;
                _platform.LoadingFinished();
            }

            var playing = state == GameFlowState.Playing;
            if (playing != _playing)
            {
                _playing = playing;
                if (playing) _platform.GameplayStarted();
                else _platform.GameplayStopped();
            }

            if (state == GameFlowState.Completed && _flow.LastResult is { } result &&
                result.Stars >= LevelResult.MaxStars && !_flow.IsTutorialLevel)
                _platform.HappyMoment();
        }
    }
}
