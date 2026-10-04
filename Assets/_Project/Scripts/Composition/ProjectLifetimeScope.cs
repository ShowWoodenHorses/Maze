using Maze.Application.Assets;
using Maze.Application.Flow;
using Maze.Application.Levels;
using Maze.Application.Services;
using Maze.Gameplay.Player;
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

            // IApplicationService initialization order = registration order.
            builder.Register<SettingsService>(Lifetime.Singleton).AsSelf().As<IApplicationService>();
            builder.Register<SaveService>(Lifetime.Singleton).AsSelf().As<IApplicationService>();
            builder.Register<AudioService>(Lifetime.Singleton).AsSelf().As<IApplicationService>();
            builder.Register<InputService>(Lifetime.Singleton).As<IInputService, IPlayerInput, IApplicationService>();
            builder.Register<SharedDefinitionsService>(Lifetime.Singleton).AsSelf().As<IApplicationService>();
            builder.Register<LevelCatalogService>(Lifetime.Singleton).As<ILevelCatalog, IApplicationService>();

            builder.Register<ILevelSessionFactory>(_ => new LevelSessionFactory(this, _levelSceneName), Lifetime.Singleton);
            builder.Register<GameFlow>(Lifetime.Singleton);
            builder.Register<PauseController>(Lifetime.Singleton);

            builder.RegisterInstance(_ui);
            builder.Register<ScreenRouter>(Lifetime.Singleton);

            builder.RegisterEntryPoint<ApplicationEntryPoint>();
        }
    }
}
