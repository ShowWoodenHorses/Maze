# Maze — 3D Top-Down Maze Zombie Game

Полное ТЗ: [Terms_of_Reference](Terms_of_Reference) (v2.0). Когда нужны детали, ищи в нём нужный раздел по номеру. Этот файл — выжимка.

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
- Обзор 11×11 вокруг клетки игрока плюс LOS. LOS и пули блокируют Wall и Closed Door.
- В клетке может быть 1 игрок или 0..N зомби, но не игрок и зомби вместе. Pickup-ы не блокируют.
- Движение игрока плавное. Во время атаки игрок стоит, cooldown блокирует только следующую атаку. Слоты оружия: Melee и Ranged. Патроны бесконечны, магазин ограничен, перезарядка автоматическая.

## Принятые соглашения (уже в коде)
- Клетка `(x, y)` ↔ мир `(x, 0, y)`; North = +Y сетки = +Z мира. За пределами сетки = Wall.
- Мутация `LevelData` только `internal` (InternalsVisibleTo `Maze.Editor`, `Maze.Tests.EditMode`); команды тулзы — `LevelAuthoring` (GenerateNew, RegenerateVisuals).
- ID объектов: `{prefix}_{N}` через `LevelData.CreateUniqueId`. Межобъектные ссылки — по ID; на Definitions — прямые SO-ссылки.
- Случайность только `DeterministicRandom` / `StableHash` (не `System.Random`, не `string.GetHashCode`).
- Визуал: выбор = хеш(VisualSeed, kind, cellIndex | entityId) → взвешенно; хранится `VisualChoice` (VariantId + Rotation). Rotation — четверти по часовой сверху. Префабы стен в канонической ориентации: End→N, Straight→N+S, Corner→N+E, TJunction→N+E+S; дверь rot 0 перекрывает проход С–Ю. Special-варианты только вручную. Нет варианта → явный DefaultVariantId → иначе пусто (не случайный другой).
- Префабы визуала — `AssetReferenceGameObject` (Addressables).

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
