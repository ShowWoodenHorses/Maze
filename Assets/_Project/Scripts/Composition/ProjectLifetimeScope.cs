using Maze.Application.Assets;
using Maze.Application.Flow;
using Maze.Application.Levels;
using Maze.Application.Save;
using Maze.Application.Services;
using Maze.Gameplay.Combat;
using Maze.Gameplay.Player;
using Maze.Presentation.Audio;
using Maze.Presentation.Localization;
using Maze.Presentation.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Maze.Composition
{
    /// <summary>
    /// Root scope in the Bootstrap scene (ТЗ §6.1): application-wide services only, no level state.
    /// The Bootstrap scene stays loaded for the whole session; levels are loaded additively.
    /// </summary>
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField] private UIRoot _ui;
        [Tooltip("Scene with the LevelLifetimeScope, loaded additively for every level. Must be in Build Settings.")]
        [SerializeField] private string _levelSceneName = "Game";

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<AddressablesService>(Lifetime.Singleton).As<IAddressablesService>();

            // IApplicationService initialization order = registration order. Settings are read from the save.
            builder.Register<PlayerPrefsSaveStorage>(Lifetime.Singleton).As<ISaveStorage>();
            builder.Register<SaveService>(Lifetime.Singleton).AsSelf().As<IApplicationService>();
            builder.Register<SettingsService>(Lifetime.Singleton).AsSelf().As<IApplicationService, IAimSettings>();
            builder.Register<LocalizationService>(Lifetime.Singleton).AsSelf().As<IApplicationService>();
            builder.Register<AudioService>(Lifetime.Singleton).AsSelf().As<IApplicationService>();
            builder.Register<UiPointer>(Lifetime.Singleton).As<IUiPointer>();
            builder.Register<InputService>(Lifetime.Singleton).As<IInputService, IPlayerInput, IApplicationService>();
            builder.Register<SharedDefinitionsService>(Lifetime.Singleton).AsSelf().As<IApplicationService>();
            builder.Register<LevelCatalogService>(Lifetime.Singleton).As<ILevelCatalog, IApplicationService>();
            builder.Register<ProgressService>(Lifetime.Singleton).As<IProgressService>();

            builder.Register<ILevelSessionFactory>(_ => new LevelSessionFactory(this, _levelSceneName), Lifetime.Singleton);
            builder.Register<GameFlow>(Lifetime.Singleton);
            builder.Register<PauseController>(Lifetime.Singleton);

            builder.RegisterInstance(_ui);
            builder.Register<ScreenRouter>(Lifetime.Singleton);
            builder.Register<SettingsPresenter>(Lifetime.Singleton);
            builder.Register<LocalizationPresenter>(Lifetime.Singleton);
            builder.Register<AppAudioPresenter>(Lifetime.Singleton);

            builder.RegisterEntryPoint<ApplicationEntryPoint>();
            builder.RegisterEntryPoint<AudioTickDriver>();
        }
    }

    /// <summary>Ticks <see cref="AudioService"/> (fades, music loops) with unscaled time, also while the game is paused.</summary>
    public sealed class AudioTickDriver : ITickable
    {
        private readonly AudioService _audio;

        public AudioTickDriver(AudioService audio)
        {
            _audio = audio;
        }

        public void Tick() => _audio.Tick(Time.unscaledDeltaTime);
    }
}
