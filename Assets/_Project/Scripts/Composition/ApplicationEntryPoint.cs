using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Flow;
using Maze.Core.Common;
using Maze.Presentation.Audio;
using Maze.Presentation.UI;
using VContainer.Unity;

namespace Maze.Composition
{
    /// <summary>Starts the application once the ProjectLifetimeScope is built: UI, sound, input, services, main menu.</summary>
    public sealed class ApplicationEntryPoint : IAsyncStartable
    {
        private readonly GameFlow _flow;
        private readonly ScreenRouter _screens;
        private readonly PauseController _pause;
        private readonly SettingsPresenter _settings;
        private readonly AppAudioPresenter _audio;

        public ApplicationEntryPoint(GameFlow flow, ScreenRouter screens, PauseController pause, SettingsPresenter settings,
            AppAudioPresenter audio)
        {
            _audio = audio;
            _flow = flow;
            _screens = screens;
            _pause = pause;
            _settings = settings;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            GameLog.Info(LogChannel.Bootstrap, "Bootstrap started.");
            _screens.Initialize();
            _settings.Initialize();
            _audio.Initialize();
            _pause.Initialize();

            await _flow.InitializeApplication();
            if (_flow.State != GameFlowState.Error && !cancellation.IsCancellationRequested)
                await _flow.OpenMainMenu();
        }
    }
}
