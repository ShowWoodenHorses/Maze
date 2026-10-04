using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Assets;
using Maze.Application.Flow;
using Maze.Application.Levels;
using Maze.Composition;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Gameplay.Level;
using Maze.Gameplay.Visibility;
using Maze.Presentation.Visual;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Maze.Tests.PlayMode
{
    /// <summary>
    /// Real Bootstrap scene, Addressables and LevelScope (ТЗ §110 PlayMode: Bootstrap, GameFlow, Addressables,
    /// LevelScope, Retry). Uses the first level of the project's LevelCatalog.
    /// </summary>
    public class BootstrapFlowTests
    {
        private const string BootstrapScene = "Bootstrap";
        private const string GameScene = "Game";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private IObjectResolver _container;
        private GameFlow _flow;
        private IAddressablesService _addressables;

        [UnitySetUp]
        public IEnumerator SetUp() => Async(async () =>
        {
            var loading = SceneManager.LoadSceneAsync(BootstrapScene, LoadSceneMode.Single);
            Assert.IsNotNull(loading, $"Scene '{BootstrapScene}' could not be loaded: is it in Build Settings?");
            await loading.ToUniTask();
            var scope = FindRoot<ProjectLifetimeScope>(SceneManager.GetSceneByName(BootstrapScene));
            Assert.IsNotNull(scope, "Bootstrap scene has no ProjectLifetimeScope.");
            Assert.IsNotNull(scope.Container, "ProjectLifetimeScope was not built (see the console for its exception).");

            _container = scope.Container;
            _flow = _container.Resolve<GameFlow>();
            _addressables = _container.Resolve<IAddressablesService>();
            await WaitFor(() => _flow.State == GameFlowState.MainMenu || _flow.State == GameFlowState.Error);
            Assert.AreEqual(GameFlowState.MainMenu, _flow.State, _flow.ErrorMessage);
        });

        [UnityTearDown]
        public IEnumerator TearDown() => Async(async () =>
        {
            var empty = SceneManager.CreateScene("Empty " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene == empty || !scene.isLoaded)
                    continue;
                var unloading = SceneManager.UnloadSceneAsync(scene);
                if (unloading != null)
                    await unloading.ToUniTask();
            }
        });

        [Test]
        public void Bootstrap_ReachesMainMenu_WithLevelsInCatalog()
        {
            var catalog = _container.Resolve<ILevelCatalog>();
            Assert.Greater(catalog.Levels.Count, 0, "Run Build / Sync for at least one level.");
            Assert.AreEqual(3, _addressables.ActiveHandleCount,
                "Only application-wide assets are resident in the menu: level catalog, player definition, player visual.");
        }

        [UnityTest]
        public IEnumerator StartLevel_BuildsLevelScope_ThenExit_ReleasesEverything() => Async(async () =>
        {
            var levelId = FirstLevelId();
            var handlesInMenu = _addressables.ActiveHandleCount;

            await _flow.StartLevel(levelId);

            Assert.AreEqual(GameFlowState.Playing, _flow.State, _flow.ErrorMessage);
            var gameScene = SceneManager.GetSceneByName(GameScene);
            Assert.IsTrue(gameScene.isLoaded);
            var levelScope = FindRoot<LevelLifetimeScope>(gameScene);
            Assert.IsNotNull(levelScope.Container, "LevelScope must be built.");
            Assert.AreSame(_container, levelScope.Parent.Container, "LevelScope is a child of the project scope.");

            var level = levelScope.Container.Resolve<LevelData>();
            var grid = levelScope.Container.Resolve<LevelGrid>();
            Assert.AreEqual(level.Geometry.Width, grid.Width);
            Assert.AreEqual(LevelRunState.Running, levelScope.Container.Resolve<LevelRuntime>().State);
            Assert.Greater(_addressables.ActiveHandleCount, handlesInMenu);

            await _flow.ExitToMenu();

            Assert.AreEqual(GameFlowState.MainMenu, _flow.State);
            Assert.IsFalse(SceneManager.GetSceneByName(GameScene).isLoaded, "Game scene must be unloaded.");
            Assert.AreEqual(handlesInMenu, _addressables.ActiveHandleCount, "Level Addressables must be released.");
        });

        [UnityTest]
        public IEnumerator StartLevel_BuildsVisuals_AndExit_DestroysThem() => Async(async () =>
        {
            await _flow.StartLevel(FirstLevelId());
            Assert.AreEqual(GameFlowState.Playing, _flow.State, _flow.ErrorMessage);

            var container = FindRoot<LevelLifetimeScope>(SceneManager.GetSceneByName(GameScene)).Container;
            var level = container.Resolve<LevelData>();
            var visuals = container.Resolve<LevelVisualSystem>();
            var geometry = visuals.Geometry;
            Assert.Greater(geometry.Chunks.Count, 0);
            Assert.AreEqual(0, geometry.MissingVisuals, "Every cell layer of a synced level has a prefab.");
            Assert.IsTrue(Shader.IsKeywordEnabled(GeometryShader.VisibilityKeyword));

            var expectedViews = level.AllEntities().Count(e => !(e is PlayerStartData) && !(e is ZombieSpawnData));
            Assert.AreEqual(expectedViews, container.Resolve<EntityViewRegistry>().Count);

            var meshes = geometry.Chunks.Select(c => c.Mesh).ToList();
            await _flow.ExitToMenu();

            Assert.IsTrue(meshes.All(m => m == null), "Chunk meshes are runtime assets and must be destroyed.");
            Assert.IsFalse(Shader.IsKeywordEnabled(GeometryShader.VisibilityKeyword));
        });

        [UnityTest]
        public IEnumerator StartLevel_SpawnsPlayerAtStart_WithViewAndCamera() => Async(async () =>
        {
            await _flow.StartLevel(FirstLevelId(), new LevelLaunchOptions(startIndex: 0));
            Assert.AreEqual(GameFlowState.Playing, _flow.State, _flow.ErrorMessage);

            var container = FindRoot<LevelLifetimeScope>(SceneManager.GetSceneByName(GameScene)).Container;
            var level = container.Resolve<LevelData>();
            var player = container.Resolve<Maze.Gameplay.Player.PlayerSystem>();
            Assert.IsTrue(player.IsSpawned);
            Assert.AreEqual(level.PlayerStarts[0].Position, player.Cell);
            Assert.AreEqual(player.Cell, container.Resolve<Maze.Gameplay.Grid.OccupancyMap>().PlayerCell);

            var view = container.Resolve<PlayerViewPresenter>().View;
            Assert.IsNotNull(view, "Player view instantiated.");
            Assert.AreEqual(new Vector3(player.Position.x, 0f, player.Position.y), view.transform.position);

            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            var camera = container.Resolve<TopDownCamera>().transform;
            var toPlayer = view.transform.position - camera.position;
            Assert.Greater(Vector3.Dot(camera.forward, toPlayer.normalized), 0.99f, "Camera looks at the player.");

            await _flow.ExitToMenu();
            Assert.IsTrue(view == null, "Player view destroyed with the level.");
        });

        [UnityTest]
        public IEnumerator StartLevel_AppliesVisibility_ToGeometryAndObjects() => Async(async () =>
        {
            await _flow.StartLevel(FirstLevelId(), new LevelLaunchOptions(startIndex: 0));
            Assert.AreEqual(GameFlowState.Playing, _flow.State, _flow.ErrorMessage);

            var container = FindRoot<LevelLifetimeScope>(SceneManager.GetSceneByName(GameScene)).Container;
            var visibility = container.Resolve<VisibilitySystem>();
            var player = container.Resolve<Maze.Gameplay.Player.PlayerSystem>();
            var grid = container.Resolve<LevelGrid>();
            var geometry = container.Resolve<LevelVisualSystem>().Geometry;

            Assert.IsTrue(visibility.HasResult, "Calculated when the player spawned.");
            Assert.IsTrue(visibility.IsVisible(player.Cell));
            Assert.LessOrEqual(visibility.VisibleCells.Count, 121, "At most 11x11 cells (ТЗ §53).");

            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                var cell = new GridPosition(x, y);
                Assert.AreEqual(visibility.IsVisible(cell), geometry.Mask.IsVisible(cell), $"Geometry mask at {cell}.");
            }

            foreach (var chunk in geometry.Chunks)
                Assert.AreEqual(geometry.Mask.AnyVisible(chunk.Cells), chunk.IsVisible, $"Chunk {chunk.Cells}.");
            foreach (var view in container.Resolve<EntityViewRegistry>().All)
                Assert.AreEqual(visibility.IsVisible(view.Cell), view.IsVisible, $"View '{view.EntityId}' at {view.Cell}.");

            await _flow.ExitToMenu();
        });

        [UnityTest]
        public IEnumerator Retry_AfterFail_ReloadsLevel_WithSingleLevelScope() => Async(async () =>
        {
            var levelId = FirstLevelId();
            var handlesInMenu = _addressables.ActiveHandleCount;
            await _flow.StartLevel(levelId);
            var firstRuntime = FindRoot<LevelLifetimeScope>(SceneManager.GetSceneByName(GameScene)).Container.Resolve<LevelRuntime>();

            _flow.FailLevel();
            Assert.AreEqual(GameFlowState.Failed, _flow.State);
            await _flow.RetryLevel();

            Assert.AreEqual(GameFlowState.Playing, _flow.State, _flow.ErrorMessage);
            Assert.AreEqual(LevelRunState.Disposed, firstRuntime.State, "Old LevelScope must be disposed.");
            var gameScenes = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name == GameScene) gameScenes++;
            Assert.AreEqual(1, gameScenes, "Only one LevelScope at a time (ТЗ §7).");

            await _flow.ExitToMenu();
            Assert.AreEqual(handlesInMenu, _addressables.ActiveHandleCount);
        });

        [UnityTest]
        public IEnumerator ExitToMenu_DuringLoading_LeavesNoLevelBehind() => Async(async () =>
        {
            var handlesInMenu = _addressables.ActiveHandleCount;
            var loading = _flow.StartLevel(FirstLevelId());
            await _flow.ExitToMenu();
            await loading;

            Assert.AreEqual(GameFlowState.MainMenu, _flow.State);
            Assert.IsFalse(_flow.HasLevel);
            Assert.IsFalse(SceneManager.GetSceneByName(GameScene).isLoaded);
            Assert.AreEqual(handlesInMenu, _addressables.ActiveHandleCount);
        });

        private string FirstLevelId()
        {
            var catalog = _container.Resolve<ILevelCatalog>();
            if (catalog.Levels.Count == 0)
                Assert.Ignore("Level catalog is empty: run Build / Sync for a level.");
            return catalog.Levels[0].LevelId;
        }

        private static T FindRoot<T>(Scene scene) where T : UnityEngine.Component
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent<T>(out var component))
                    return component;
            return null;
        }

        /// <summary>
        /// Runs an async test body as a coroutine. Must stay a compiler-generated iterator: when play mode is entered
        /// without a domain reload (MCP for Unity runs PlayMode tests that way) right after an EditMode run, the Test
        /// Framework keeps its EditMode helper and reads the iterator state field ("&lt;&gt;1__state") of every
        /// [UnitySetUp]/[UnityTest] enumerator. UniTask's own enumerator has no such field → NullReferenceException.
        /// </summary>
        private static IEnumerator Async(Func<UniTask> body)
        {
            yield return UniTask.ToCoroutine(body);
        }

        private static async UniTask WaitFor(Func<bool> condition)
        {
            using var timeout = new CancellationTokenSource(Timeout);
            await UniTask.WaitUntil(condition, cancellationToken: timeout.Token);
        }
    }
}

