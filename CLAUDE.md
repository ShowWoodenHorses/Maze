# Maze — 3D Top-Down Maze Zombie Game

Полное ТЗ: [Terms_of_Reference](Terms_of_Reference) (v2.0). Когда нужны детали, ищи в нём нужный раздел по номеру. Этот файл — выжимка.

**Прогресс относительно ТЗ, принятые решения и следующие шаги — [Docs/Progress.md](Docs/Progress.md). Читать перед началом работы; обновлять (статус + история) после завершения каждого этапа.**

**Суть:** игрок выбирается из лабиринта (двери, ключи, оружие, аптечки, немного зомби). Звёзды: 1 — выход, 2 — убиты все зомби, 3 — собраны все фрагменты карты.
**Платформы:** Android, WebGL 2 (desktop browser). Цель — 30–60 FPS.

## Стек (установлено)
Unity 6000.3.8f1 (6.3 LTS) · Built-in RP · VContainer 1.19 · UniTask 2.5 · Addressables 4.1 · Input System 1.18 (только новый) · uGUI · Animator · Test Framework (EditMode/PlayMode) · MCP for Unity.
Встроенное: `Mesh.CombineMeshes`, Static Batching, `UnityEngine.Pool`, `JsonUtility` (SaveData).

## ЗАПРЕЩЕНО
- URP/HDRP, NavMesh, ECS/DOTS, LOD.
- **Любая физика:** Rigidbody, Collider, `Physics.Raycast/Overlap*`, `OnCollision*`, `OnTrigger*`. Вместо неё Grid + Occupancy + `ISpatialQueryService`.
- Singleton с mutable state, `*.Instance`, `FindObjectOfType`/`FindAnyObjectByType`, `GameObject.Find`, `Resources.FindObjectsOfTypeAll`.
- Запуск gameplay из `Awake/Start/OnEnable`. Lifecycle ведут LifetimeScope и `GameFlow`.
- Per-frame A*, per-frame Visibility, тяжёлый AI в каждом кадре, лишние аллокации, `Debug.Log` в горячих циклах.
- **Random в runtime для выбора visual prefab.** Runtime берёт только сохранённый `VariantId`.
- God Object. Prefab, определяющий gameplay-семантику (проходимость, LOS и т.п.).

## Архитектурные правила
- Слои: `Core ← Gameplay ← Application ← Presentation`. Editor зависит от Runtime, Runtime никогда не зависит от Editor.
- Gameplay не знает о Presentation. View (MonoBehaviour) — только Transform/Animator/Renderer/UI/VFX. Схема: `Data → Runtime → View`.
- Animator только отображает состояние. Состоянием управляет gameplay, не наоборот.
- `LevelData` (ScriptableObject, Addressable) — source of truth. Runtime state хранится отдельно.
- Дорогие операции запускаются событиями: Visibility — по `PlayerCellChanged`; A* — при смене target, двери, AI task или после invalidation.
- Все async-операции — UniTask с `CancellationToken`. Уровневые операции привязаны к lifetime `LevelLifetimeScope`.
- У каждого Addressables handle есть owner. При Dispose LevelScope level-specific ассеты освобождаются. Потерянных handle быть не должно.
- Navigation, Visibility и Detection — три независимые системы.
- Невидимые объекты только скрываются (View = Hidden), AI и симуляция продолжают работать.
- При критической ошибке: отменить async, освободить Addressables, уничтожить LevelScope, показать error state.

## Ключевые классы (имена из ТЗ)
- **ProjectLifetimeScope:** `GameFlow`, `SaveService`, `AudioService`, `InputService`, `AddressablesService`, `SettingsService`.
- **LevelLifetimeScope** (одновременно только один): `LevelData`, `LevelGrid`, `DoorSystem`, `NavigationSystem`, `VisibilitySystem`, `PlayerSystem`, `ZombieSystem`, `CombatSystem`, `WeaponSystem`, `PickupSystem`, `MapSystem`, `LevelProgress`, `LevelVisualSystem`, `LevelRuntime`.
- **GameFlow:** `InitializeApplication, OpenMainMenu, StartLevel, StartGameplay, PauseGameplay, ResumeGameplay, CompleteLevel, FailLevel, RetryLevel, ExitToMenu`.
- **Visual:** `VisualTheme` → `*VisualSet` (weighted variants, у стен есть категории Straight/Corner/T_Junction/End/Special) → `VisualData` (`GeometryVisualAssignments`, `ObjectVisualAssignments`, `Overrides`). Приоритет: Override > Saved > Generated > Default. `GeometryBuilder`, `EntityViewFactory`, `VisibilityController`, `VisibilityChunk`.
- **Прочее:** `ISpatialQueryService` (GetObjectsInCell/AroundCell, QuerySegment/Direction/Radius), `LevelGenerationSettings` (Width, Height — чётные, MazeSeed, VisualSeed, MazeAlgorithm, LoopDensity…), `CellVisualContext`, `ZombieController`/`ZombieStateMachine` (Idle/Patrol/Chase/Attack/Return), `SoundEvent`, `SaveData`, `WeaponDefinition → WeaponVisualDefinition`.

## Ключевые игровые правила
- Клетка 1×1 м. Типы клеток: Floor/Wall/Door. Генератор — Randomized DFS + `LoopDensity`.
- **Размер для генератора — нечётный** (мин. 5×5), по решению пользователя вместо «чётного» из ТЗ §12 (чётный давал двойную стену сверху/справа). Валидатор чётность не проверяет.
- Обзор 11×11 вокруг клетки игрока плюс LOS. LOS и пули блокируют Wall и Closed Door.
- В клетке может быть 1 игрок или 0..N зомби, но не игрок и зомби вместе. Pickup-ы не блокируют.
- Движение игрока плавное. Во время атаки игрок стоит, cooldown блокирует только следующую атаку. Слоты оружия: Melee и Ranged. Патроны бесконечны, магазин ограничен, перезарядка автоматическая.

## Принятые соглашения (уже в коде)
- Клетка `(x, y)` ↔ мир `(x, 0, y)`; North = +Y сетки = +Z мира. За пределами сетки = Wall.
- Мутация `LevelData` (и `LevelCatalog`) только `internal` (InternalsVisibleTo `Maze.Editor`, `Maze.Tests.EditMode`); команды тулзы — `LevelAuthoring` (GenerateNew, RegenerateVisuals).
- ID объектов: `{prefix}_{N}` через `LevelData.CreateUniqueId`. Межобъектные ссылки — по ID; на Definitions — прямые SO-ссылки.
- Случайность только `DeterministicRandom` / `StableHash` (не `System.Random`, не `string.GetHashCode`).
- Визуал клетки — 2 слоя (`CellLayer`): **Floor у каждой клетки** (сплошной пол под тонкими стенами), **Wall** только у клеток-стен поверх. Префабы стен — без пола. Overrides — по (клетка, слой).
- Визуал: выбор = хеш(VisualSeed, kind, cellIndex | entityId) → взвешенно; хранится `VisualChoice` (VariantId + Rotation). Rotation — четверти по часовой сверху. Префабы стен в канонической ориентации: End→N, Straight→N+S, Corner→N+E, TJunction→N+E+S; дверь rot 0 перекрывает проход С–Ю. Special-варианты только вручную. Нет варианта → явный DefaultVariantId → иначе пусто (не случайный другой).
- Префабы визуала — `AssetReferenceGameObject` (Addressables).
- **Строгое правило ключей: 1 ключ = 1 дверь.** Ключ того же цвета, что дверь; после использования удаляется из инвентаря. Ключ только отпирает дверь — она остаётся закрытой; открыть/закрыть её игрок может отдельным действием сколько угодно раз. Связь — `Door.KeyId`, цвет — `VisualVariant.ColorTag` (запертая дверь — цветной вариант, разные пары по возможности разных цветов; ключ берёт цвет итогового визуала своей двери; дверь без ключа — без цвета).
- Зомби не открывают двери (закрытая дверь — blocker для их маршрутов). A* — 4 направления, `GridPathfinder` без аллокаций.
- Тулза: окно **Maze → Level Designer** (`Scripts/Editor/LevelDesigner`), правки уровня — через `LevelEditing` (Core, покрыт тестами). Руководство для людей: [Docs/LevelDesigner.md](Docs/LevelDesigner.md) — **обновлять при изменении тулзы/визуальной системы**.
- Валидатор: `LevelValidator.Validate(level)` → `ValidationReport`; проверки идентифицируются `ValidationCodes`, сообщения на английском.

### Runtime-каркас
- Сцены: **Bootstrap** (всегда загружена; `ProjectLifetimeScope` + UI-экраны) и **Game** (грузится аддитивно на каждый уровень; `LevelLifetimeScope` с `autoRun = false`, камера, свет). Пересоздаются меню **Maze → Dev → Build Runtime Scenes** (`RuntimeScenesBuilder`) — правки UI вносить в билдер, а не только в сцену.
- Точка входа — `ApplicationEntryPoint` (VContainer `IAsyncStartable`). Сервисы приложения реализуют `IApplicationService`, инициализируются `GameFlow.InitializeApplication` в порядке регистрации.
- `GameFlow` (Application) не знает VContainer: уровень создаёт `ILevelSessionFactory` → `LevelSessionFactory` (Composition) грузит Game, строит `LevelLifetimeScope` дочерним к проектному и регистрирует в нём `LevelData` и `IAssetOwner` уровня. Переходы GameFlow идут по одному, новый отменяет текущий; любое исключение → полная выгрузка уровня → `GameFlowState.Error`.
- Шаги загрузки уровня (§9) — `ILevelLoadStep` со `Stage` (`LevelLoadStage`), регистрируются в `LevelLifetimeScope` из любого слоя; `LevelRuntime` выполняет их по порядку. Per-frame логика уровня — `ILevelTickable`: тикается только в `Running` (пауза = нет тиков, `Time.timeScale` не трогаем).
- Addressables: только через `IAddressablesService.CreateOwner(name)` → `IAssetOwner.LoadAsync`; handle регистрируется в owner сразу. Owner уровня освобождается после Dispose LevelScope и выгрузки Game. `AddressablesService.ActiveHandleCount` — для проверки утечек в тестах.
- Логи — `GameLog` (`[Maze][Channel]`, каналы из §108), не голый `Debug.Log`.
- Каталог уровней — `LevelCatalog` (Addressable `"LevelCatalog"`, `Data/Levels/LevelCatalog.asset`); Build/Sync добавляет уровень. `LevelId` = имя ассета уровня.
- **Ловушка:** пространство имён `Maze.Application` перекрывает `UnityEngine.Application` — в коде `Maze.*` писать `UnityEngine.Application.xxx` полностью.
- **Ловушка:** VContainer (`ScriptTemplateProcessor`) перезаписывает шаблоном любой **новый** `*LifetimeScope.cs` при создании его `.meta`. Создав такой файл снаружи Unity: refresh → записать содержимое ещё раз.
- UniTask в Unity 6: `AsyncOperation` ожидать через `.ToUniTask()` (прямой `await` не компилируется).

### Визуал уровня в игре
- `LevelVisualSystem` (Presentation, шаг `BuildVisuals`): `VisualPrefabLibrary` грузит через `IAssetOwner` уровня **только используемые** варианты (`LevelVisualUsage`) → `GeometryBuilder` → `EntityViewFactory`. Варианты берутся только через `VisualResolver` (сохранённые id), никакого выбора в runtime.
- Геометрия (пол, стены) — без GameObject на клетку: меши префабов склеиваются вручную (`MeshAccumulator`) в `VisibilityChunk` 8×8, сабмеш на материал, затем `StaticBatchingUtility.Combine`. В **UV3** каждой вершины — её клетка.
- Поклеточное скрытие (§54) — шейдер **`Maze/Geometry`** (`Art/Shaders/MazeGeometry.shader`): при глобальном keyword `MAZE_VISIBILITY` вершины клеток, скрытых в `_MazeVisibility` (R8-текстура W×H, `CellVisibilityMask`), схлопываются; тени тоже. API: `LevelGeometryView.SetCellVisible/SetAllVisible` + `ApplyVisibility()` (заливает маску, выключает чанки без видимых клеток). Без keyword всё видно (редактор; `GeometryShaderEditorGuard` сбрасывает keyword после Play).
- Требования к префабам пола/стен: материалы на `Maze/Geometry`, меши Read/Write, только MeshRenderer — проверяет `EditorLevelValidator`.
- Объекты (двери, выходы, пикапы) — `EntityView` в `EntityViewRegistry` по id; зомби и игрока создают их системы. Runtime-меши/текстуры — уничтожать явно (`UnityObjects.Destroy`), после выгрузки уровня `Resources.UnloadUnusedAssets`.
- Камера уровня — `TopDownCamera` (сцена Game): `Frame(bounds)` при загрузке, затем `Follow` за игроком. Камера **не вращается по Y** — вверх экрана всегда North; ввод на это опирается.

### Игрок, ввод, тики
- Тики уровня: `ILevelTickable` (симуляция, только в Running) → `ILevelLateTickable` (синхронизация вьюх, также в Paused). Порядок шагов внутри одной стадии и тиков = порядок регистрации в `LevelLifetimeScope` (геймплей раньше своих вьюх).
- Ввод: `IPlayerInput` (Gameplay) реализует `InputService` (Application) — карта действий в коде: Move, Look, Attack, Interact, SwitchMelee, SwitchRanged, OpenMap, Pause. Тач-контролы HUD (`OnScreenStick/Button`) эмулируют геймпад — новые экранные кнопки биндить на `<Gamepad>/...`.
- Позиции игрока — `Vector2` в единицах сетки (x = East, y = North), клетка = `PlayerMovement.CellOf`. Движение `PlayerMovement` (без физики): квадратный футпринт `BodyHalfSize` против непроходимых клеток, по осям (скольжение), подшаги ≤ 0.2, corner assist. Центр не входит в клетку с зомби (`OccupancyMap`).
- Проходимость: `LevelPassability` (Floor; Door — если открыта в `DoorSystem`). `DoorSystem` пока только open/closed из `IsInitiallyOpen`.
- `PlayerSystem.CellChanged(from, to)` = PlayerCellChanged из ТЗ; `Spawned` — после появления. Старт: `LevelLaunchOptions` (StartIndex для Test from Start #N, Seed), иначе случайный через `DeterministicRandom`.
- Общие определения (`PlayerDefinition` "Player/Definition", `PlayerVisualDefinition` "Player/Visual") грузит `SharedDefinitionsService` при старте приложения; в LevelScope прокинуты из проектного. Build/Sync держит их Addressable (группа Maze Shared).
- Выход (§62): `ExitSystem.ExitReached` → `GameFlowState.ExitConfirmation` (геймплей на паузе) → `GameFlow.ConfirmExit(bool)`.

### Видимость
- Расчёт — `FieldOfView` (Core/Visibility): окно 11×11 вокруг клетки игрока + permissive LOS (клетка видна, если свободна хоть одна линия между точками клетки игрока и целевой клетки; центр + 4 угла с отступом). Непрозрачная клетка видна, если до неё дошла линия или она касается (8 направлений) видимой прозрачной. Луч — `GridLineOfSight.IsClear` (угловой стык двух непрозрачных клеток блокирует), прозрачность — `IGridOpacity` (struct, generic, без аллокаций); в игре `LevelOpacity` (Wall, закрытая Door, вне сетки).
- `VisibilitySystem` (Gameplay) пересчитывает только по `PlayerSystem.Spawned`, `CellChanged` и `DoorChanged` (дверь в окне) → `Changed`. `VisibilityController` (Presentation) пишет в маску геометрии и `EntityView.SetVisible`. Новые вьюхи — через `EntityViewRegistry.Add`, движущиеся меняют клетку только `EntityViewRegistry.Move` — тогда видимость применяется сразу. AI видимость не использует (§57).

### Тесты
- **Ловушка:** `[UnitySetUp]`/`[UnityTest]` в PlayMode не должны возвращать `UniTask.ToCoroutine(...)` напрямую — только через итератор-обёртку (`yield return UniTask.ToCoroutine(...)`, см. `BootstrapFlowTests.Async`). MCP гоняет PlayMode без перезагрузки домена, и после EditMode-прогона Test Framework читает поле состояния итератора → NRE во всех SetUp/TearDown.

## Структура папок (целевая)
```
Assets/_Project/
  Scripts/
    Core/          Maze.Core          — Grid, LevelData, A*, генератор, валидатор, SpatialQuery (чистая логика)
    Gameplay/      Maze.Gameplay      — системы уровня, Player/Zombie/Combat/Weapon/Door runtime
    Application/   Maze.Application   — GameFlow, Save/Audio/Input/Addressables/Settings сервисы
    Presentation/  Maze.Presentation  — Views, UI, LevelVisualSystem, VisibilityController
    Composition/   Maze.Composition   — ProjectLifetimeScope, LevelLifetimeScope (composition root)
    Editor/        Maze.Editor        — Level Designer Tool (Editor-only)
  Tests/EditMode/  Maze.Tests.EditMode
  Tests/PlayMode/  Maze.Tests.PlayMode
  Data/            Levels/, Themes/, VisualSets/, Weapons/, Zombies/
  Prefabs/  Art/  Scenes/ (Bootstrap — первая в Build)  Settings/
```

## Рабочие договорённости
- **Git-коммиты не делать.** Их всегда делает пользователь.
- С Unity работаем через MCP for Unity. После правок скриптов проверять консоль (`read_console`).
- Общение на русском.
