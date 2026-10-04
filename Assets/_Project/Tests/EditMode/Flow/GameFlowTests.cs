using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Assets;
using Maze.Application.Flow;
using Maze.Application.Levels;
using Maze.Application.Services;
using Maze.Core.Authoring;
using Maze.Core.Level;
using Maze.Gameplay.Level;
using Maze.Tests.EditMode.Visual;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Maze.Tests.EditMode.Flow
{
    public class GameFlowTests
    {
        private const string ValidId = "valid";
        private const string OtherId = "other";
        private const string InvalidId = "invalid";
        private const string MissingAssetId = "missing_asset";

        private VisualFixture _fixture;
        private LevelData _invalidLevel;
        private LevelCatalog _catalogAsset;
        private FakeAddressables _addressables;
        private FakeSessionFactory _sessions;
        private List<string> _initialized;
        private GameFlow _flow;
        private List<GameFlowState> _states;

        [SetUp]
        public void SetUp()
        {
            _fixture = new VisualFixture();
            LevelAuthoring.GenerateNew(_fixture.Level);
            _invalidLevel = ScriptableObject.CreateInstance<LevelData>(); // no starts, no exits

            _catalogAsset = ScriptableObject.CreateInstance<LevelCatalog>();
            _catalogAsset.AddOrUpdate(ValidId, "Levels/valid", "Valid");
            _catalogAsset.AddOrUpdate(OtherId, "Levels/other", "Other");
            _catalogAsset.AddOrUpdate(InvalidId, "Levels/invalid", "Invalid");
            _catalogAsset.AddOrUpdate(MissingAssetId, "Levels/missing", "Missing");

            _addressables = new FakeAddressables();
            _addressables.Assets["Levels/valid"] = _fixture.Level;
            _addressables.Assets["Levels/other"] = _fixture.Level;
            _addressables.Assets["Levels/invalid"] = _invalidLevel;

            _sessions = new FakeSessionFactory();
            _initialized = new List<string>();
            _flow = CreateFlow(new RecordingService("A", _initialized), new RecordingService("B", _initialized));
        }

        [TearDown]
        public void TearDown()
        {
            _flow.Dispose();
            _fixture.Dispose();
            Object.DestroyImmediate(_invalidLevel);
            Object.DestroyImmediate(_catalogAsset);
        }

        private GameFlow CreateFlow(params IApplicationService[] services)
        {
            var flow = new GameFlow(services, new FakeCatalog(_catalogAsset), _addressables, _sessions);
            _states = new List<GameFlowState>();
            flow.StateChanged += _states.Add;
            return flow;
        }

        private async UniTask BootToMenu()
        {
            await _flow.InitializeApplication();
            await _flow.OpenMainMenu();
        }

        // ------------------------------------------------------------ Initialization

        [UnityTest]
        public IEnumerator Initialize_RunsServicesInRegistrationOrder_ThenMainMenu() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();

            CollectionAssert.AreEqual(new[] { "A", "B" }, _initialized);
            CollectionAssert.AreEqual(new[] { GameFlowState.Initializing, GameFlowState.MainMenu }, _states);
        });

        [UnityTest]
        public IEnumerator Initialize_ServiceFails_EntersErrorState() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            _flow.Dispose();
            _flow = CreateFlow(new RecordingService("A", _initialized, fail: true));

            await _flow.InitializeApplication();

            Assert.AreEqual(GameFlowState.Error, _flow.State);
            StringAssert.Contains("A failed", _flow.ErrorMessage);
        });

        // ------------------------------------------------------------ Start level

        [UnityTest]
        public IEnumerator StartLevel_LoadsLevelAndStartsGameplay() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            await _flow.StartLevel(ValidId);

            Assert.AreEqual(GameFlowState.Playing, _flow.State);
            Assert.AreEqual(ValidId, _flow.CurrentLevelId);
            var session = _sessions.Created[0];
            Assert.AreSame(_fixture.Level, session.Level);
            Assert.IsTrue(session.Loaded && session.Started);
            Assert.AreEqual(1, _addressables.ActiveHandleCount, "The level asset is held by the level owner.");
            CollectionAssert.AreEqual(
                new[] { GameFlowState.Initializing, GameFlowState.MainMenu, GameFlowState.Loading, GameFlowState.Playing }, _states);
        });

        [UnityTest]
        public IEnumerator ExitToMenu_UnloadsLevel_AndReleasesItsAssets() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            await _flow.StartLevel(ValidId);
            await _flow.ExitToMenu();

            Assert.AreEqual(GameFlowState.MainMenu, _flow.State);
            Assert.IsTrue(_sessions.Created[0].Unloaded);
            Assert.IsFalse(_flow.HasLevel);
            Assert.AreEqual(0, _addressables.OwnerCount);
            Assert.AreEqual(0, _addressables.ActiveHandleCount);
        });

        [UnityTest]
        public IEnumerator StartLevel_WhilePlaying_UnloadsOldLevelBeforeCreatingNew() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            await _flow.StartLevel(ValidId);
            var first = _sessions.Created[0];
            _sessions.OnCreate = () => Assert.IsTrue(first.Unloaded, "Old LevelScope must be disposed first (ТЗ §9).");

            await _flow.StartLevel(OtherId);

            Assert.AreEqual(2, _sessions.Created.Count);
            Assert.AreEqual(GameFlowState.Playing, _flow.State);
            Assert.AreEqual(OtherId, _flow.CurrentLevelId);
            Assert.AreEqual(1, _addressables.OwnerCount);
        });

        [UnityTest]
        public IEnumerator RetryLevel_ReloadsSameLevel() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            await _flow.StartLevel(ValidId);
            _flow.FailLevel();
            await _flow.RetryLevel();

            Assert.AreEqual(2, _sessions.Created.Count);
            Assert.IsTrue(_sessions.Created[0].Unloaded);
            Assert.AreEqual(GameFlowState.Playing, _flow.State);
            Assert.AreEqual(ValidId, _flow.CurrentLevelId);
        });

        // ------------------------------------------------------------ Errors (ТЗ §109)

        [UnityTest]
        public IEnumerator StartLevel_UnknownLevel_EntersError() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            await BootToMenu();
            await _flow.StartLevel("nope");

            Assert.AreEqual(GameFlowState.Error, _flow.State);
            StringAssert.Contains("nope", _flow.ErrorMessage);
            Assert.AreEqual(0, _addressables.OwnerCount);
        });

        [UnityTest]
        public IEnumerator StartLevel_MissingAsset_EntersError_WithoutLeakedHandles() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            await BootToMenu();
            await _flow.StartLevel(MissingAssetId);

            Assert.AreEqual(GameFlowState.Error, _flow.State);
            Assert.AreEqual(0, _sessions.Created.Count);
            Assert.AreEqual(0, _addressables.OwnerCount);
        });

        [UnityTest]
        public IEnumerator StartLevel_InvalidLevel_EntersError_BeforeCreatingScope() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            await BootToMenu();
            await _flow.StartLevel(InvalidId);

            Assert.AreEqual(GameFlowState.Error, _flow.State);
            StringAssert.Contains("validation error", _flow.ErrorMessage);
            Assert.AreEqual(0, _sessions.Created.Count);
            Assert.AreEqual(0, _addressables.OwnerCount);
        });

        [UnityTest]
        public IEnumerator StartLevel_LoadStepFails_UnloadsPartialLevel_AndEntersError() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            await BootToMenu();
            _sessions.LoadBehaviour = _ => throw new InvalidOperationException("visual build failed");

            await _flow.StartLevel(ValidId);

            Assert.AreEqual(GameFlowState.Error, _flow.State);
            StringAssert.Contains("visual build failed", _flow.ErrorMessage);
            Assert.IsTrue(_sessions.Created[0].Unloaded, "Partially initialized level must be destroyed.");
            Assert.IsFalse(_flow.HasLevel);
            Assert.AreEqual(0, _addressables.OwnerCount);

            await _flow.ExitToMenu();
            Assert.AreEqual(GameFlowState.MainMenu, _flow.State);
        });

        // ------------------------------------------------------------ Cancellation

        [UnityTest]
        public IEnumerator ExitToMenu_WhileLoading_CancelsLoad_AndUnloadsLevel() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            var gate = new UniTaskCompletionSource();
            _sessions.LoadBehaviour = cancellation => gate.Task.AttachExternalCancellation(cancellation);

            var loading = _flow.StartLevel(ValidId);
            Assert.AreEqual(GameFlowState.Loading, _flow.State);

            await _flow.ExitToMenu();
            await loading;

            Assert.AreEqual(GameFlowState.MainMenu, _flow.State);
            Assert.IsTrue(_sessions.Created[0].Unloaded);
            Assert.IsFalse(_sessions.Created[0].Started);
            Assert.AreEqual(0, _addressables.OwnerCount);
        });

        [UnityTest]
        public IEnumerator StartLevel_WhileLoading_CancelsFirstLoad() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            var gate = new UniTaskCompletionSource();
            _sessions.LoadBehaviour = cancellation => gate.Task.AttachExternalCancellation(cancellation);
            var first = _flow.StartLevel(ValidId);

            _sessions.LoadBehaviour = null;
            await _flow.StartLevel(OtherId);
            await first;

            Assert.AreEqual(GameFlowState.Playing, _flow.State);
            Assert.AreEqual(OtherId, _flow.CurrentLevelId);
            Assert.IsTrue(_sessions.Created[0].Unloaded);
            Assert.IsFalse(_sessions.Created[0].Started);
            Assert.IsTrue(_sessions.Created[1].Started);
            Assert.AreEqual(1, _addressables.OwnerCount);
        });

        // ------------------------------------------------------------ Gameplay states

        [UnityTest]
        public IEnumerator PauseAndResume_ArePassedToLevel() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            await _flow.StartLevel(ValidId);
            var session = _sessions.Created[0];

            _flow.PauseGameplay();
            Assert.AreEqual(GameFlowState.Paused, _flow.State);
            Assert.IsTrue(session.Paused);

            _flow.ResumeGameplay();
            Assert.AreEqual(GameFlowState.Playing, _flow.State);
            Assert.IsFalse(session.Paused);
        });

        [UnityTest]
        public IEnumerator Pause_OutsideGameplay_IsIgnored() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            _flow.PauseGameplay();
            Assert.AreEqual(GameFlowState.MainMenu, _flow.State);
        });

        [UnityTest]
        public IEnumerator LevelFinished_MovesToResultState_AndStopsGameplay() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            await _flow.StartLevel(ValidId);
            var session = _sessions.Created[0];

            session.RaiseFinished(LevelOutcome.Completed);

            Assert.AreEqual(GameFlowState.Completed, _flow.State);
            Assert.IsTrue(session.Stopped);
            Assert.IsTrue(_flow.HasLevel, "The level stays loaded behind the result screen.");
        });

        [UnityTest]
        public IEnumerator ExitReached_PausesAndAsks_NoResumes_YesCompletes() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            await _flow.StartLevel(ValidId);
            var session = _sessions.Created[0];

            session.RaiseExitReached();
            Assert.AreEqual(GameFlowState.ExitConfirmation, _flow.State);
            Assert.IsTrue(session.Paused, "Gameplay waits for the answer.");

            _flow.ConfirmExit(false);
            Assert.AreEqual(GameFlowState.Playing, _flow.State);
            Assert.IsFalse(session.Paused);

            session.RaiseExitReached();
            _flow.ConfirmExit(true);
            Assert.AreEqual(GameFlowState.Completed, _flow.State);
            Assert.IsTrue(session.Stopped);
        });

        [UnityTest]
        public IEnumerator ExitReached_WhilePaused_IsIgnored() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            await _flow.StartLevel(ValidId);
            _flow.PauseGameplay();

            _sessions.Created[0].RaiseExitReached();
            Assert.AreEqual(GameFlowState.Paused, _flow.State);
        });

        [UnityTest]
        public IEnumerator LaunchOptions_ArePassedToLevel_AndKeptOnRetry() => UniTask.ToCoroutine(async () =>
        {
            await BootToMenu();
            var options = new LevelLaunchOptions(startIndex: 1);
            await _flow.StartLevel(ValidId, options);
            Assert.AreSame(options, _sessions.LastOptions);

            _flow.FailLevel();
            await _flow.RetryLevel();
            Assert.AreSame(options, _sessions.LastOptions, "Test from Start #N restarts at the same start.");

            await _flow.StartLevel(OtherId);
            Assert.AreSame(LevelLaunchOptions.Default, _sessions.LastOptions);
        });

        // ------------------------------------------------------------ Fakes

        private sealed class RecordingService : IApplicationService
        {
            private readonly List<string> _log;
            private readonly bool _fail;

            public RecordingService(string name, List<string> log, bool fail = false)
            {
                Name = name;
                _log = log;
                _fail = fail;
            }

            public string Name { get; }

            public UniTask InitializeAsync(CancellationToken cancellation)
            {
                if (_fail) throw new InvalidOperationException(Name + " failed");
                _log.Add(Name);
                return UniTask.CompletedTask;
            }
        }

        private sealed class FakeCatalog : ILevelCatalog
        {
            private readonly LevelCatalog _catalog;
            public FakeCatalog(LevelCatalog catalog) => _catalog = catalog;
            public IReadOnlyList<LevelCatalogEntry> Levels => _catalog.Levels;
            public LevelCatalogEntry Find(string levelId) => _catalog.Find(levelId);
        }

        private sealed class FakeAddressables : IAddressablesService
        {
            public readonly Dictionary<string, Object> Assets = new Dictionary<string, Object>();
            private readonly List<FakeOwner> _owners = new List<FakeOwner>();

            public int OwnerCount => _owners.Count;

            public int ActiveHandleCount
            {
                get
                {
                    var count = 0;
                    foreach (var owner in _owners) count += owner.HandleCount;
                    return count;
                }
            }

            public IAssetOwner CreateOwner(string name)
            {
                var owner = new FakeOwner(this, name);
                _owners.Add(owner);
                return owner;
            }

            private sealed class FakeOwner : IAssetOwner
            {
                private readonly FakeAddressables _service;

                public FakeOwner(FakeAddressables service, string name)
                {
                    _service = service;
                    Name = name;
                }

                public string Name { get; }
                public int HandleCount { get; private set; }
                public bool IsDisposed { get; private set; }

                public UniTask<T> LoadAsync<T>(string address, CancellationToken cancellation) where T : Object
                {
                    HandleCount++;
                    if (!_service.Assets.TryGetValue(address, out var asset) || !(asset is T typed))
                        throw new AssetLoadException(address, null);
                    return UniTask.FromResult(typed);
                }

                public UniTask<T> LoadAsync<T>(UnityEngine.AddressableAssets.AssetReference reference,
                    CancellationToken cancellation) where T : Object =>
                    LoadAsync<T>(reference.AssetGUID, cancellation);

                public void Dispose()
                {
                    if (IsDisposed) return;
                    IsDisposed = true;
                    HandleCount = 0;
                    _service._owners.Remove(this);
                }
            }
        }

        private sealed class FakeSessionFactory : ILevelSessionFactory
        {
            public readonly List<FakeSession> Created = new List<FakeSession>();
            public Func<CancellationToken, UniTask> LoadBehaviour;
            public Action OnCreate;

            public LevelLaunchOptions LastOptions;

            public UniTask<ILevelSession> CreateAsync(LevelData level, IAssetOwner levelAssets, LevelLaunchOptions options,
                CancellationToken cancellation)
            {
                OnCreate?.Invoke();
                LastOptions = options;
                var session = new FakeSession(level, levelAssets, LoadBehaviour);
                Created.Add(session);
                return UniTask.FromResult<ILevelSession>(session);
            }
        }

        private sealed class FakeSession : ILevelSession
        {
            private readonly IAssetOwner _assets;
            private readonly Func<CancellationToken, UniTask> _load;

            public FakeSession(LevelData level, IAssetOwner assets, Func<CancellationToken, UniTask> load)
            {
                Level = level;
                _assets = assets;
                _load = load;
            }

            public LevelData Level { get; }
            public bool Loaded { get; private set; }
            public bool Started { get; private set; }
            public bool Paused { get; private set; }
            public bool Stopped { get; private set; }
            public bool Unloaded { get; private set; }

            public event Action<LevelOutcome> Finished;
            public event Action ExitReached;

            public void RaiseFinished(LevelOutcome outcome) => Finished?.Invoke(outcome);

            public void RaiseExitReached() => ExitReached?.Invoke();

            public async UniTask LoadAsync(CancellationToken cancellation)
            {
                if (_load != null) await _load(cancellation);
                Loaded = true;
            }

            public void StartGameplay() => Started = true;
            public void SetPaused(bool paused) => Paused = paused;
            public void StopGameplay() => Stopped = true;

            public UniTask UnloadAsync()
            {
                Dispose();
                return UniTask.CompletedTask;
            }

            public void Dispose()
            {
                Unloaded = true;
                _assets.Dispose();
            }
        }
    }
}
