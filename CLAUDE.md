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
- **Прочее:** `ISpatialQueryService` (GetObjectsInCell/AroundCell, QuerySegment/Direction/Radius), `LevelGenerationSettings` (Width, Height — чётные, MazeSeed, VisualSeed, MazeAlgorithm, LoopDensity…), `CellVisualContext`, `ZombieController`/`ZombieStateMachine` (Idle/Patrol/Alert/Chase/Attack/Return), `SoundEvent`, `SaveData`, `WeaponDefinition → WeaponVisualDefinition`.

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
- **Test / Play** из тулзы — `LevelPlayLauncher` (Editor): валидация → Build / Sync → `playModeStartScene` = Bootstrap → Play → при `MainMenu` вызывает `GameFlow.StartLevel` с `LevelLaunchOptions(startIndex)`. Запрос хранится в `SessionState` (переживает reload домена), стартовая сцена сбрасывается после выхода из Play. Ищет `ProjectLifetimeScope` через `FindFirstObjectByType` — допустимо только в Editor-коде.
- Сборки: **Maze → Build** (`Editor/Build/MazeBuilder`) — Addressables-контент + плеер в `Builds/`; платформа должна быть активной (иначе меню только переключает). Перед контентом ассеты, попавшие в несколько бандлов, переносятся в группу **Maze Shared Dependencies** (`LevelSync.IsolateDuplicates`, правило Addressables «Check Duplicate Bundle Dependencies»); остались дубли — сборка не идёт.
- **Ловушка сборки:** не-Addressable ассет, на который ссылаются несколько групп, копируется в каждый бандл — в сборке это **разные объекты** (в редакторе — один). Не сравнивать определения из разных Addressable-корней по ссылке без запасного сравнения по id; PlayMode-тесты (режим AssetDatabase) такое не ловят. Ориентация — только альбомная. В UI-текстах только ASCII: встроенный шрифт в WebGL не содержит «—», «…», «•», «★».
- Список префабов, нужных игре, и требования к ним — [Docs/Prefabs.md](Docs/Prefabs.md).
- **Сторонние паки** качаются в `Assets/_Project/Import` — папка в `.gitignore`. Используемое переносит в `Art/ThirdParty/<пак>/…` меню **Maze → Dev → Collect Used Third-Party Assets** (GUID сохраняются; заодно снимает коллайдеры/Rigidbody с префабов). Запускать после того, как взяли из пака что-то новое, **до коммита**; ссылок на `Import` из проекта быть не должно. Он же ограничивает текстуры паков для Android/WebGL (≤2048, ключи 256; отдельно — **Cap Third-Party Texture Sizes**). Меши/аватар персонажей — копии в `Art/Characters` (**Maze → Dev → Extract Character Meshes**), чтобы общий `Characters.fbx` Synty не попадал в сборку целиком; запускать после смены модели персонажа.
- Тулза: окно **Maze → Level Designer** (`Scripts/Editor/LevelDesigner`), правки уровня — через `LevelEditing` (Core, покрыт тестами). Руководство для людей: [Docs/LevelDesigner.md](Docs/LevelDesigner.md) — **обновлять при изменении тулзы/визуальной системы**.
- Валидатор: `LevelValidator.Validate(level)` → `ValidationReport`; проверки идентифицируются `ValidationCodes`, сообщения на английском.

### Runtime-каркас
- Сцены: **Bootstrap** (всегда загружена; `ProjectLifetimeScope` + UI-экраны) и **Game** (грузится аддитивно на каждый уровень; `LevelLifetimeScope` с `autoRun = false`, камера, свет). Пересоздаются меню **Maze → Dev → Build Runtime Scenes** (`RuntimeScenesBuilder`) — правки UI вносить в билдер, а не только в сцену.
- Точка входа — `ApplicationEntryPoint` (VContainer `IAsyncStartable`). Сервисы приложения реализуют `IApplicationService`, инициализируются `GameFlow.InitializeApplication` в порядке регистрации.
- `GameFlow` (Application) не знает VContainer: уровень создаёт `ILevelSessionFactory` → `LevelSessionFactory` (Composition) грузит Game, строит `LevelLifetimeScope` дочерним к проектному и регистрирует в нём `LevelData` и `IAssetOwner` уровня. Переходы GameFlow идут по одному, новый отменяет текущий; любое исключение → полная выгрузка уровня → `GameFlowState.Error`.
- Шаги загрузки уровня (§9) — `ILevelLoadStep` со `Stage` (`LevelLoadStage`), регистрируются в `LevelLifetimeScope` из любого слоя; `LevelRuntime` выполняет их по порядку. Последний — `LevelWarmup` (стадия `Warmup`): все вьюхи уровня (и скрытые, с неактивными детьми) и объекты от `IViewWarmup` один раз рисуются служебной камерой на весь уровень, затем `GC.Collect`. **Во время игры ничего не создавать впервые:** новые виды объектов (эффекты, свет, декор) создавать/пулить при загрузке и отдавать в прогрев через `IViewWarmup`. Per-frame логика уровня — `ILevelTickable`: тикается только в `Running` (пауза = нет тиков, `Time.timeScale` не трогаем).
- Addressables: только через `IAddressablesService.CreateOwner(name)` → `IAssetOwner.LoadAsync`; handle регистрируется в owner сразу. Owner уровня освобождается после Dispose LevelScope и выгрузки Game. `AddressablesService.ActiveHandleCount` — для проверки утечек в тестах.
- Логи — `GameLog` (`[Maze][Channel]`, каналы из §108), не голый `Debug.Log`. `Info` в release-сборке пишется без стектрейса.
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
- Камера уровня — `TopDownCamera` (сцена Game): наклон 88° (почти сверху), `Frame(bounds)` при загрузке, затем `Follow` за игроком на расстоянии, при котором ±4 клетки помещаются при любом aspect (решение пользователя: ближе; окно видимости ±5 выходит за край экрана). Камера **не вращается по Y** — вверх экрана всегда North; ввод на это опирается.

### Свет
- **Без realtime-теней и ламп Unity.** Настроение — `VisualTheme.Lighting` (`ThemeLighting`): ambient, слабая «луна» без теней, фонарь игрока, пятно-тень, пресеты источников. Применяет `LevelLighting`; пятна — `BlobShadows.Attach(view)` (дочерний квад, `Maze/BlobShadow`).
- Доп. свет — в шейдерах через `Art/Shaders/MazeLighting.cginc` (`MazeExtraLight` → albedo × свет в emission): фонарь (глобалы `_MazeLantern*`) + свет источников (`_MazeLightMap`, `LevelLightMap`). Вне уровня глобалы нулевые — доп. света нет. На полу и стенах свет источников гаснет с высотой (`MazeExtraLightGeometry`, `ThemeLighting.LightFade*/LightTopShare`) — верх стен почти не освещён; персонажи (`Maze/Lit`) — без этого.
- Источники — `LevelData.Lights` (`LightSourceData`, не в `AllEntities`), считаются при загрузке `LightField` (Core/Lighting, 4×4 текселя на клетку, стены и закрытые двери перекрывают, при смене двери — частичный пересчёт). Авторасстановка — `LightPlacer` (у стен, по `VisualSeed` и `LightDensity`), вызывается из Regenerate Visuals; правки — `LevelEditing.*Light`.
- Шейдеры: пол/стены — `Maze/Geometry`, «живые» объекты (персонажи, оружие, двери, ключи, предметы) — **`Maze/Lit`** (свойства как у Standard). После добавления моделей — **Maze → Dev → Convert Object Materials to Maze Lit**.

### Игрок, ввод, тики
- Тики уровня: `ILevelTickable` (симуляция, только в Running) → `ILevelLateTickable` (синхронизация вьюх, также в Paused). Порядок шагов внутри одной стадии и тиков = порядок регистрации в `LevelLifetimeScope` (геймплей раньше своих вьюх).
- Ввод: `IPlayerInput` (Gameplay) реализует `InputService` (Application) — карта действий в коде: Move, Look, Attack, Interact, SwitchMelee, SwitchRanged, OpenMap, Pause. Кнопки геймплея **опрашиваются из тиков** `WasPressed(PlayerAction)` (нажатия на паузе не применяются позже); Pause — событие `IInputService.PauseRequested`. Тач-контролы (`OnScreenButton`, свой стик `TouchStick` с мёртвой зоной и кривой `StickResponse`, плавающий/фиксированный) эмулируют геймпад — новые экранные кнопки биндить на `<Gamepad>/...`. Они на отдельном канвасе `TouchControls` (под основным UI, показывает HUD): единица = 0.1 мм по `Screen.dpi` × размер из настроек, safe area (`SafeAreaFitter`), зеркальная раскладка для левшей. Настройки управления — `ControlsSettings` в `SaveData.Settings` (там же `ShowFps` — счётчик `FpsCounter` в правом нижнем углу, на основном канвасе поверх всех экранов); экран Settings (меню и пауза) → `SettingsPresenter`: `SettingsService.PreviewControls` применяет сразу, `SaveControls` пишет при закрытии. Прицел (там же, для всех устройств): `AimMode` (Free/Eight/Four — `Aim.Snap` направления атаки и `Facing` при остановке) и автонаведение (`PlayerCombat.AimDirection`: цель в конусе ±30°, без стен); Gameplay читает через `IAimSettings`.
- Позиции игрока — `Vector2` в единицах сетки (x = East, y = North), клетка = `PlayerMovement.CellOf`. Движение `PlayerMovement` (без физики): квадратный футпринт `BodyHalfSize` против непроходимых клеток, по осям (скольжение), подшаги ≤ 0.2, corner assist. Центр не входит в клетку с зомби (`OccupancyMap`).
- Проходимость: `LevelPassability` (Floor; Door — если открыта в `DoorSystem`).
- `PlayerSystem.CellChanged(from, to)` = PlayerCellChanged из ТЗ; `Spawned` — после появления. Старт: `LevelLaunchOptions` (StartIndex для Test from Start #N, Seed), иначе случайный через `DeterministicRandom`.
- Общие определения (`PlayerDefinition` "Player/Definition", `PlayerVisualDefinition` "Player/Visual") грузит `SharedDefinitionsService` при старте приложения; в LevelScope прокинуты из проектного. Build/Sync держит их Addressable (группа Maze Shared).
- Выход (§62): `ExitSystem.ExitReached` → `GameFlowState.ExitConfirmation` (геймплей на паузе) → `GameFlow.ConfirmExit(bool)`.

### Двери, ключи, предметы
- `DoorSystem`: open/closed + locked (дверь с `KeyId`, не открытая изначально). `Unlock` только отпирает; открыть запертую нельзя (исключение). События `DoorChanged`, `DoorUnlocked`.
- `PlayerInteraction` (тик после `PlayerSystem`) — Interact: оружие в клетке игрока → иначе соседняя дверь (по `Facing`, своя клетка последней): запертая + ключ в `PlayerInventory` → `Unlock` (ключ тратится, дверь закрыта); иначе открыть/закрыть; закрыть нельзя при зомби в клетке двери или пересечении тела игрока с ней. Событие `Interacted(InteractionResult, door)` для UI.
- `PickupSystem` (шаг `InitializeSystems`, подписан на `Spawned`/`CellChanged`): ключи — при входе; аптечка — при входе, если HP не полный (лечит до полного, исчезает); оружие — `TryTakeWeapon` (через Interact). `WeaponSystem`: слоты Melee/Ranged, новое сразу активно, старое того же слота сбрасывается и выпадает в клетку (`PickupSystem.Added`); `WeaponRuntime` хранит исходный `WeaponPickupData` (id и визуал). Стартового оружия нет. `PlayerHealth` — HP.
- Presentation: `DoorViewPresenter` (компонент `DoorVisual` на префабе двери: Closed/Open/Lock и/или Animator bool `Open`/`Locked`; без него — прячет рендереры открытой двери; активность корня не трогает — её ведёт видимость), `PickupViewPresenter` (`EntityViewRegistry.Remove` взятых, вьюха выпавшего оружия по его сохранённому визуалу), `HudPresenter` (шаг `InitializeUI`, пишет в `UIRoot.Hud` из проектного скоупа).
- Дев-контент: `Level_Items` (копия Level_Dev с дверями, ключом, аптечкой, оружием; PlayMode-тест предметов), двери-заглушки (Maze → Dev → Rebuild Placeholder Doors).
- **Оружие:** 10 видов (`Data/Weapons`), всё собирает **Maze → Dev → Build Weapons** (`WeaponsBuilder`): определения, `WeaponVisualCatalog` ("Weapons/Visual", грузит `SharedDefinitionsService`), метки `WeaponModel` (Muzzle/Tip; у огнестрела `Grip_L` — левая ладонь из позы выстрела), пикапы «лежит боком» в Weapon Set темы, `CharacterWeaponRig` на игроке. В руках — `PlayerWeaponPresenter` (шаг `SpawnPlayer` после `PlayerViewPresenter`): модели оружия уровня грузятся при загрузке; ближнее — жёстко в `WeaponSocket_Melee`; огнестрел каждый `LateTick` позирует `CharacterWeaponRig.PoseGun` (после Animator, без Humanoid IK): рукоять в правой ладони, ствол — куда его направляет клип (правая → левая ладонь); корпус «стрелковой» стойки доворачивается к взгляду до `_bodyYaw` (20°, ноги как в клипе); левая рука — `TwoBoneIK` на `Grip_L` (вне досягаемости — оружие чуть поворачивается к плечу). Состояния аниматора с тегом `NoHandIK` (перезарядка, урон, Use) — без IK левой руки, `NoWeaponPose` (смерть) — без позы; всё плавно за 0.15 с.

### Бой
- `PlayerCombat` (тик **до** `PlayerSystem`): Attack → направление = стик в этом кадре или `Facing`; `PlayerSystem.HoldStill(dir)` — без движения в этот тик. Cooldown (`WeaponDefinition.Cooldown`) и перезарядка — на `WeaponRuntime`; перезарядка автоматическая при пустом магазине, идёт только у активного оружия. Ближний бой — все живые `IDamageable` в секторе (`MeleeRange` до края цели, `MeleeArc`) при `IsClear`; дальний — `BulletSystem.Spawn`. Удержание: ближний и Automatic повторяют, Single — только нажатие.
- `BulletSystem` (тик после взаимодействия): пул `Bullet`, каждый тик `QuerySegment(prev, next)` → цель (`ApplyDamage`) / стена-закрытая дверь / ничего; события `Spawned`, `Ended`, `TargetHit`.
- `ISpatialQueryService` (`SpatialQueryService`, Gameplay/Spatial): объекты регистрируются `Add/Remove` (зомби — на их этапе), позиции читаются вживую; стены — `GridRaycast` (Core) с `LevelOpacity`.
- Звуки §71: `SoundEventBus.Emit(type, position, radius)` — Melee/Ranged (`WeaponDefinition.SoundRadius`), Step (`PlayerFootsteps`), Door, Pickup. Не блокируются стенами, без накопления.
- Вид: `CombatVisualDefinition` (Addressable "Combat/Visual", грузит `SharedDefinitionsService`) → `CombatViewPresenter` (ObjectPool, эффекты только в видимых клетках): трассер из `Muzzle` сходится с линией пули (геймплейная пуля летит из центра игрока), вспышка у дула и гильза ставятся в `LateTick` после позы оружия (регистрация после `PlayerWeaponPresenter`), искры по нормали стены, «звёздочка» на попадании (пуля и ближний), дуга `SwingArc` по `MeleeArc`/`MeleeRange`. Визуал ближнего боя (дуга, «звёздочки», реакция зомби — вспышка, `Hit`, смерть) ждёт момента удара в клипе: `PlayerViewPresenter.LastMeleeContactDelay` = `AttackContact{i}` (дефолты параметров контроллера, замеряет Build Player Animations) × cooldown, направление дуги — `AttackSweep{i}` (`LastMeleeSweep`); урон по геймплею — сразу (решение пользователя). Свои эффекты (стиль мультяшный, без крови и следов пуль) — **Maze → Dev → Build Combat Effects** (`CombatEffectsBuilder`, `Art/Effects`, шейдеры `Maze/Particle`, `Maze/SwingArc`); `CombatEffect` на корне префаба — длительность и перезапуск частиц. Вспышка модели при уроне — `HitFlash` (MaterialPropertyBlock `_HitFlash` в `Maze/Lit`), красная пульсация краёв экрана — `HudScreen.PulseDamage`. `LevelWarmup` проматывает системы частиц на 0.02 с, чтобы их нарисовать. Шейдеры эффектов — с `ColorMask RGB` (не портят альфу кадра).
- Зоны зрения и шум (только вид, `VisualTheme.Awareness` / `ThemeAwareness`): `VisionZonesView` — у Walker конус, у Hunter круг, веер лучей `VisionZoneShape` (`GridRaycast` + `LevelOpacity`, как геймплейный LOS), меш на зомби пересобирается при движении/повороте/смене двери не чаще 10 раз/с; жёлтая в покое, красная в Alert, нет в погоне/атаке и у невидимых игроку зомби; рисуется целиком поверх тумана. `NoiseWavesView` — волны `Maze/Ring` до радиуса звука из `SoundEventBus` поверх всего; шаги и автоматический огонь — одно кольцо при начале ходьбы/очереди (растёт от игрока и гаснет, снова — после тишины `RingRearm`). Материалы — **Maze → Dev → Build Awareness Visuals**.
- Следы: `FootprintsView` (Presentation) по `VisualTheme.Footprints` (`ThemeFootprints`) — отпечатки игрока и зомби из геймплейных позиций (у зомби и вне видимости), логика `FootprintField` (шаг, лево/право, затухание по расстоянию до ходока и возрасту, кольцевой буфер), один динамический меш под туманом (очередь 2005, видимость = `FogMask`/`IsRevealed`), пыль — одна система частиц с `Emit`. Ассеты — **Maze → Dev → Build Footprints**. Animator игрока — параметры `PlayerAnimatorParameters` (каждый необязателен); модель Synty + клипы Mixamo `Player_*` собирает **Maze → Dev → Build Player Animations** (Humanoid, root motion запечён, слой `UpperBody` для действий, правки контроллера — в билдер).

### Зомби
- `ZombieSystem` (шаг `SpawnZombies`, тик после пуль): `ZombieRuntime` (`IDamageable`, в `SpatialQueryService` и `OccupancyMap`) + `ZombieController` на каждого. Убитый: убирается из occupancy и spatial, `KilledCount++`, событие `Died` (вьюха удаляется).
- `ZombieController`: обнаружение раз в 0.1 с (`ZombieDetection`: VisionOnly — `VisionRange`/`VisionAngle` от `Facing` + `IsClear`; HearingOnly — `Hear(SoundEvent)`, дистанция ≤ min(радиус звука, `HearingRadius`); VisionAndHearing — видит во все стороны в `DetectionRadius`, стены и закрытые двери загораживают (`IsClear`), и слышит звуки по `HearingRadius`, как HearingOnly). Цель — последняя замеченная клетка; Chase к ней, дошёл без нового обнаружения → Return (ближайшая точка патруля → Patrol дальше, иначе спавн → Idle). Игрок в соседней по стороне клетке → Attack (первый удар через пол-интервала). Урон не вызывает реакции (§76).
- Движение по `NavigationSystem.TryFindPath` (A* только при смене цели или `Version` дверей); в клетку игрока не входят (`CanZombieEnter`), друг через друга — да; закрытая перед носом дверь → новый поиск.
- Скорость: Patrol/Return — `MoveSpeed` (walk), Chase — `ChaseSpeed` (run).
- **Alert** (сверх §74): заметив игрока из Idle/Patrol/Return — стоит и рычит `ZombieController.AlertDuration` (0.5 с), потом Chase; игрок рядом — сразу Attack; после атаки погоня возобновляется без рыка.
- Вид: `ZombieViewPresenter` (шаг `SpawnZombies` после системы) — `EntityViewRegistry.Add/Move/Remove`, параметры `ZombieAnimatorParameters` (наличие кэшируется — `animator.parameters` аллоцирует); убитый остаётся `CorpseTime` (анимация смерти), потом удаляется. Префабы `Art/Zombie/*` + общий контроллер собирает **Maze → Dev → Build Zombie Animations** (он же заполняет Zombie Set темы и переназначает визуал зомби в уровнях). Темп шага клипов — `LocomotionAnimation.Playback` (≤1.4×). Билдеры персонажей пересобирают контроллер на месте — не удалять ассет (ломает ссылки префабов).
- Смерть игрока: `PlayerDeathRule` (резолвится в `LevelSessionFactory`, как `ExitSystem`) → `LevelRuntime.Finish(Failed)`; `LevelRuntime.Tick` прерывает кадр, когда уровень завершён.

### Карта, звёзды, сохранения
- `MapSystem` (Gameplay/Map): собранные фрагменты; подбирает `PickupSystem` при входе в клетку. `LevelProgress` → `LevelResult` (звёзды §87: выход + все зомби + все фрагменты; нет зомби/фрагментов — звезда сразу; без завершения 0).
- Карта (§60): `GameFlowState.Map` = пауза (`GameFlow.OpenMap/CloseMap`); ввод — событие `IInputService.MapRequested` (M/Tab/Select), `PauseController` переключает карту, Escape её закрывает; кнопка «Map» в HUD. `MapPresenter` (Presentation/Map, шаг `InitializeUI`) владеет `Texture2D` (пиксель на клетку, перерисовка только при сборе), `MapRenderer` рисует из `LevelGrid` только собранные области; маркеры — цвет запертых дверей (`VisualColorTags`), выходы, старты.
- Завершение (§89): `GameFlow.EndLevel` → `ILevelSession.GetResult` → `IProgressService.RecordCompletion` (только Completed) → окно результата (`GameFlow.LastResult`). Смерть ничего не сохраняет.
- Сейв (§85): `SaveService` (`ISaveStorage` → `PlayerPrefsSaveStorage`, ключ `Maze.SaveData`, JSON `JsonUtility`; битый → новый), `SettingsService` хранит настройки в сейве и сохраняет при изменении (регистрируется после Save), `ProgressService` (первый уровень каталога открыт, завершение открывает следующий, лучшие звёзды, `TotalKills`). Закрытые уровни в редакторе/dev-сборке запускаются; «Debug: reset progress» в меню.
- PlayMode-тесты, завершающие уровень, пишут настоящий `PlayerPrefs` — сохранять и восстанавливать ключ сейва (см. `MapFragments_Map_AndCompletion_SavesStars`).

### Видимость
- Расчёт — `FieldOfView` (Core/Visibility): окно 11×11 вокруг клетки игрока; лучи из центра и 4 углов (с отступом) клетки игрока в те же точки каждой клетки окна; **видима каждая клетка, через которую прошёл луч**, и непрозрачная, в которую он упёрся. Непрозрачная клетка видна ещё если примыкает **стороной** к видимой прозрачной или закрывает угол видимого пола (диагональ + обе клетки между ними — видимые стены); диагональное касание через скрытый пол не считается. `IsRevealed` (для пола/стен) = видимые + одиночная скрытая клетка между двумя видимыми **прозрачными** по W+E или S+N; объекты — только `IsVisible`. Луч — `GridLineOfSight.Trace` (визитор клеток; угловой стык двух непрозрачных клеток блокирует), прозрачность — `IGridOpacity` (struct, generic, без аллокаций); в игре `LevelOpacity` (Wall, закрытая Door, вне сетки).
- Туман войны (только визуал): `FogOfWarView` (Presentation) — плоскость на уровне пола с материалом `VisualTheme.FogMaterial` (шейдер `Maze/Fog`, все параметры в материале), маска `_MazeFog` (R8 (W+2)×(H+2), билинейно) по `RevealedCells`; `FogMask` плавно меняет только меняющиеся клетки (первое открытие мгновенно). Туман рисуется без проверки глубины (край затягивает и стены на границе). С туманом геометрию уходящих из видимости клеток скрывает `FogOfWarView`, когда туман их полностью затянул (`JustCovered`), а не `VisibilityController`. Нет материала — нет тумана.
- `VisibilitySystem` (Gameplay) пересчитывает только по `PlayerSystem.Spawned`, `CellChanged` и `DoorChanged` (дверь в окне) → `Changed`. `VisibilityController` (Presentation) пишет `RevealedCells` в маску геометрии и `IsVisible` в `EntityView.SetVisible`. Новые вьюхи — через `EntityViewRegistry.Add`, движущиеся меняют клетку только `EntityViewRegistry.Move` — тогда видимость применяется сразу. AI видимость не использует (§57).

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
- **Сборки (APK, WebGL и т.п.) запускать только после подтверждения пользователя**, кроме случаев, когда он сам попросил сборку.
- **После завершения этапа из плана** в конце ответа предложить название коммита (коммит по-прежнему не делать).
- С Unity работаем через MCP for Unity. После правок скриптов проверять консоль (`read_console`).
- Общение на русском.
