using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Gameplay.Level;
using Maze.Presentation.Visual;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Maze.Composition
{
    /// <summary>
    /// Scope of the active level (ТЗ §7), placed in the Game scene with Auto Run off. Built by
    /// <see cref="LevelSessionFactory"/> as a child of the ProjectLifetimeScope, which also registers
    /// <see cref="LevelData"/> and the level's <c>IAssetOwner</c>. Disposing it disposes every level system.
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
            builder.Register(resolver => new LevelGrid(resolver.Resolve<LevelData>().Geometry), Lifetime.Singleton);
            builder.Register<LevelRuntime>(Lifetime.Singleton);
            builder.RegisterEntryPoint<LevelTickDriver>();

            // Presentation
            builder.RegisterComponent(_viewRoot);
            builder.RegisterComponent(_camera);
            builder.Register<EntityViewRegistry>(Lifetime.Singleton);
            builder.Register<LevelVisualSystem>(Lifetime.Singleton).AsSelf().As<ILevelLoadStep>();
        }
    }

    /// <summary>Drives <see cref="LevelRuntime.Tick"/> from the player loop; the runtime decides whether to tick.</summary>
    public sealed class LevelTickDriver : ITickable
    {
        private readonly LevelRuntime _runtime;

        public LevelTickDriver(LevelRuntime runtime)
        {
            _runtime = runtime;
        }

        public void Tick() => _runtime.Tick(Time.deltaTime);
    }
}
