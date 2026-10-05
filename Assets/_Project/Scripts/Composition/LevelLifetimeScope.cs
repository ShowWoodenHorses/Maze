using Maze.Application.Services;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Gameplay.Doors;
using Maze.Gameplay.Grid;
using Maze.Gameplay.Level;
using Maze.Gameplay.Pickups;
using Maze.Gameplay.Player;
using Maze.Gameplay.Visibility;
using Maze.Gameplay.Weapons;
using Maze.Presentation.UI;
using Maze.Presentation.Visual;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Maze.Composition
{
    /// <summary>
    /// Scope of the active level (ТЗ §7), placed in the Game scene with Auto Run off. Built by
    /// <see cref="LevelSessionFactory"/> as a child of the ProjectLifetimeScope, which also registers
    /// <see cref="LevelData"/>, the level's <c>IAssetOwner</c> and <see cref="LevelLaunchOptions"/>.
    /// Disposing it disposes every level system.
    /// Registration order matters within a load stage and for ticking: gameplay before its views.
    /// </summary>
    public sealed class LevelLifetimeScope : LifetimeScope
    {
        [SerializeField] private LevelViewRoot _viewRoot;
        [SerializeField] private TopDownCamera _camera;

        protected override void Awake()
        {
            if (autoRun)
            {
                Debug.LogWarning("[Maze] LevelLifetimeScope must not auto-run: it is built by LevelSessionFactory.", this);
                autoRun = false;
            }

            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // Shared definitions live in the project scope.
            builder.Register(resolver => resolver.Resolve<SharedDefinitionsService>().Player, Lifetime.Singleton);
            builder.Register(resolver => resolver.Resolve<SharedDefinitionsService>().PlayerVisual, Lifetime.Singleton);

            // Gameplay
            builder.Register(resolver => new LevelGrid(resolver.Resolve<LevelData>().Geometry), Lifetime.Singleton);
            builder.Register<DoorSystem>(Lifetime.Singleton);
            builder.Register<LevelPassability>(Lifetime.Singleton);
            builder.Register<OccupancyMap>(Lifetime.Singleton);
            builder.Register<PlayerHealth>(Lifetime.Singleton);
            builder.Register<PlayerInventory>(Lifetime.Singleton);
            builder.Register<WeaponSystem>(Lifetime.Singleton).AsSelf().As<ILevelTickable>();
            builder.Register<PlayerSystem>(Lifetime.Singleton).AsSelf().As<ILevelLoadStep, ILevelTickable>();
            builder.Register<PickupSystem>(Lifetime.Singleton).AsSelf().As<ILevelLoadStep>();
            builder.Register<PlayerInteraction>(Lifetime.Singleton).AsSelf().As<ILevelTickable>();
            builder.Register<ExitSystem>(Lifetime.Singleton);
            builder.Register<VisibilitySystem>(Lifetime.Singleton).AsSelf().As<ILevelLoadStep>();
            builder.Register<LevelRuntime>(Lifetime.Singleton);
            builder.RegisterEntryPoint<LevelTickDriver>();

            // Presentation
            builder.RegisterComponent(_viewRoot);
            builder.RegisterComponent(_camera);
            builder.Register<EntityViewRegistry>(Lifetime.Singleton);
            builder.Register<LevelVisualSystem>(Lifetime.Singleton).AsSelf().As<ILevelLoadStep>();
            builder.Register<VisibilityController>(Lifetime.Singleton).AsSelf().As<ILevelLoadStep>();
            builder.Register<DoorViewPresenter>(Lifetime.Singleton).AsSelf().As<ILevelLoadStep>();
            builder.Register<PickupViewPresenter>(Lifetime.Singleton).AsSelf().As<ILevelLoadStep>();
            builder.Register<HudPresenter>(Lifetime.Singleton).AsSelf().As<ILevelLoadStep>();
            builder.Register<PlayerViewPresenter>(Lifetime.Singleton).AsSelf().As<ILevelLoadStep, ILevelLateTickable>();
        }
    }

    /// <summary>
    /// Drives <see cref="LevelRuntime"/> from the player loop: simulation in Update, view sync in LateUpdate.
    /// The runtime decides whether to tick (not while loading, paused or stopped).
    /// </summary>
    public sealed class LevelTickDriver : ITickable, ILateTickable
    {
        private readonly LevelRuntime _runtime;

        public LevelTickDriver(LevelRuntime runtime)
        {
            _runtime = runtime;
        }

        public void Tick() => _runtime.Tick(Time.deltaTime);

        public void LateTick() => _runtime.LateTick(Time.deltaTime);
    }
}
