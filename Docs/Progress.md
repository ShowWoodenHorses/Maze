# Прогресс разработки

Сводка того, что уже сделано относительно ТЗ ([Terms_of_Reference](../Terms_of_Reference)), какие решения приняты сверх или вместо ТЗ, и что делать дальше.
Предназначен для разработчиков и ИИ-агентов, которые подключаются к проекту.

**Как вести файл:** по завершении каждого этапа обновить таблицу статуса (раздел 2), при необходимости — решения (раздел 3), и добавить запись в историю (раздел 6).

**Последнее обновление:** 2026-10-04

---

## 1. Коротко

- **Готово:** фундамент данных уровня (Core) и инструмент дизайнера уровней (Editor).
  - модель `LevelData`;
  - генератор лабиринта;
  - визуальная система (темы, наборы, веса, назначения, overrides);
  - A*;
  - решатель ключей и дверей;
  - валидатор;
  - окно **Maze → Level Designer** с полным циклом: генерация → ручное редактирование → визуал → валидация → превью → Build/Sync.
- **Готово:** каркас runtime (этап 5).
  - сборки Gameplay / Application / Presentation / Composition;
  - сцены Bootstrap и Game, `ProjectLifetimeScope`, `LevelLifetimeScope`;
  - `GameFlow` со всеми операциями §8, загрузка уровня по §9 с отменой и обработкой ошибок §109;
  - `AddressablesService` с владельцами handle'ов, каталог уровней;
  - экраны-заглушки: меню, загрузка, HUD, пауза, результат, ошибка.
  - Цикл «меню → уровень → пауза → повтор / выход в меню» работает, утечек handle'ов нет.
- **Готово:** визуал уровня в игре (этап 6).
  - загрузка только используемых префабов через владельца уровня;
  - геометрия склеена в чанки 8×8 со Static Batching;
  - шейдер `Maze/Geometry` скрывает отдельные клетки по маске (основа для тумана войны);
  - вьюхи дверей, выходов и предметов; камера показывает весь уровень.
- **Не начато:** игрок, зомби, бой, расчёт видимости, карта, сохранения.
- **Тесты:** 158 EditMode-тестов (включая массовые прогоны на 1000 seed'ов, §111) и 5 PlayMode-тестов. Все проходят.
- **Руководство для дизайнера:** [Docs/LevelDesigner.md](LevelDesigner.md).

---

## 2. Статус по разделам ТЗ

Легенда: ✅ сделано · 🟡 частично · ⬜ не начато

| § ТЗ | Тема | Статус | Что есть / чего нет |
|---|---|---|---|
| 1–2 | Концепция, платформы | 🟡 | Модули Android и WebGL Build Support установлены. Сборки под платформы ещё не проверялись |
| 3 | Технологический стек | ✅ | Unity 6000.3.8f1, Built-in RP, VContainer 1.19, UniTask 2.5, Addressables 4.1, Input System 1.18, uGUI, Test Framework. Удалены Visual Scripting, Multiplayer Center, Collab Proxy |
| 4–5 | Архитектура, слои | ✅ | Сборки `Maze.Core ← Maze.Gameplay ← Maze.Application ← Maze.Presentation`, `Maze.Composition` (видит все), `Maze.Editor`, `Maze.Tests.EditMode`, `Maze.Tests.PlayMode` |
| 6–9 | Bootstrap, ProjectLifetimeScope, LevelLifetimeScope, GameFlow, загрузка уровня | ✅ | Сцены Bootstrap и Game (аддитивная). `GameFlow` — все операции §8. Пайплайн §9: шаги `ILevelLoadStep` по стадиям `LevelLoadStage`. Сейчас есть шаг `BuildVisuals`, остальные системы добавят свои шаги на своих этапах |
| 10 | LevelData | ✅ | `Core/Level/LevelData.cs` и данные объектов. Мутация только `internal` (доступна редактору и тестам) |
| 11 | Разделение логики и визуала | ✅ | Тип клетки — логика; префаб — только вид |
| 12–14 | Генерация (DFS + LoopDensity, seed'ы) | ✅ | `Core/Generation/MazeGenerator.cs`. **Отступление:** размер нечётный (см. раздел 3) |
| 15–16 | Ручное редактирование, workflow | 🟡 | Всё, кроме **Test / Play** из Level Designer (уровень запускается из меню игры) |
| 17–46 | Визуальная система | ✅ | Создание и хранение визуала (темы, наборы, веса, VisualSeed, контекстные стены, overrides, приоритеты, Regenerate Visuals) и runtime-часть: `LevelVisualSystem`, `GeometryBuilder`, `EntityViewFactory`, загрузка только используемых префабов (§44). Визуальные состояния двери (открыта/закрыта) — с `DoorSystem` |
| 47–49, 53–57, 104–107 | Visibility / Fog of War, чанки | 🟡 | Готово отображение: `VisibilityChunk` (8×8), `CellVisibilityMask` + шейдер `Maze/Geometry` скрывают любую клетку, чанки без видимых клеток выключаются, `EntityView.SetVisible`. Нет расчёта: `VisibilitySystem` (11×11 + LOS по `PlayerCellChanged`) и `VisibilityController` — после игрока |
| 50–51 | Grid, Occupancy | 🟡 | `LevelGrid` есть; Occupancy нет |
| 52 | Navigation + A* | 🟡 | `Core/Navigation/GridPathfinder.cs` (A*, 4 направления, без аллокаций). `NavigationSystem` с учётом дверей в runtime нет |
| 58–60 | Map, фрагменты | 🟡 | Данные фрагментов и областей, редактор областей, проверки пересечений. `MapSystem` / `MapRenderer` нет |
| 61–62 | Стартовые точки, выходы | 🟡 | Данные, генерация, валидация достижимости. Случайный выбор старта и окно «Завершить уровень?» нет. **Test from Start #N** нет |
| 63–80 | Игрок, управление, оружие, бой, звук, аптечки, зомби, Animator | 🟡 | Только `WeaponDefinition`, `ZombieDefinition` (ScriptableObject с параметрами из ТЗ) и их размещение на уровне. Логики нет |
| 81 | Spatial Query | ⬜ | — |
| 82 | GeometryBuilder, Mesh Combine, Static Batching | ✅ | `GeometryBuilder`: меши префабов пола и стен склеиваются по чанкам (сабмеш на материал), затем `StaticBatchingUtility.Combine`. Ни одного GameObject на клетку. Редакторское превью `LevelPreviewBuilder` осталось для Level Designer |
| 83 | Object Pooling | ⬜ | — |
| 84 | Addressables | 🟡 | Build/Sync делает уровень (`Levels/<имя>`, группа *Maze Levels*), каталог уровней (`LevelCatalog`) и префабы темы (группа *Maze Visuals*) Addressable. Runtime: `AddressablesService` + `IAssetOwner`: у каждого handle есть владелец, owner уровня освобождается вместе с LevelScope. Визуальные префабы грузятся через owner уровня — только используемые варианты. После выгрузки уровня — `Resources.UnloadUnusedAssets` |
| 85–89 | SaveData, прогресс, звёзды, смерть, завершение | 🟡 | Есть только состояния `Completed` / `Failed` в `GameFlow` и экран результата. `SaveService` — заглушка |
| 90–92 | Валидатор | ✅ | `Core/Validation/LevelValidator.cs`: структура, визуал, геймплей, уровни Error/Warning/Info, ~50 кодов. В редакторе дополнительно проверяются существование ассетов префабов и требования к префабам пола и стен (шейдер, Read/Write, только MeshRenderer) |
| 93–97 | Диагностика распределения, управление визуалом, Regenerate Visuals, overrides | ✅ | Вкладка Visuals в Level Designer |
| 98 | Детерминизм | ✅ | Свой `DeterministicRandom` (SplitMix64) и `StableHash`; результат сохраняется в уровень. В runtime случайности нет |
| 99–102 | Финальные пайплайны | 🟡 | Редакторская часть готова; runtime — нет |
| 103 | Performance | 🟡 | Заложено: A* без аллокаций; геометрия — склеенные чанки + Static Batching; видимость меняет маленькую текстуру, а не GameObject'ы; жёсткие тени; грузятся только используемые префабы. Профилирование на устройствах не проводилось |
| 108 | Structured logging | 🟡 | `GameLog` с каналами §108 (`[Maze][Channel]`), используется в Bootstrap, GameFlow, Addressables, загрузке и выгрузке. Остальные места подключатся вместе со своими системами |
| 109 | Error handling | ✅ (для каркаса) | Любое исключение в переходе GameFlow → отмена, выгрузка уровня (Dispose LevelScope, выгрузка Game, Release Addressables) → экран ошибки. Уровень с ошибками валидации не запускается |
| 110 | Тесты | 🟡 | EditMode: Grid, Generator, seeds, визуал, Validator, A*, Key/Door solver, правки уровня, **GameFlow** (на фейках: порядок, ошибки, отмена, утечки), **LevelRuntime**, **GeometryBuilder** (чанки, клетки в вершинах, повороты, маска видимости), `EntityViewFactory`, `LevelVisualUsage`. PlayMode: **Bootstrap, GameFlow, Addressables, LevelScope, Retry**, построение и уничтожение визуала. Нет Visibility, Save, Progress, Spatial Queries и PlayMode-тестов для игрока, дверей, ключей, оружия, зомби, смерти, завершения |
| 111 | Массовый тест генератора | ✅ | 1000 seed'ов: Generate (инварианты) и Generate → Assign Visuals → Validate (0 ошибок) |

---

## 3. Решения сверх ТЗ и отступления от него

Всё ниже согласовано с пользователем. Подробности — в [CLAUDE.md](../CLAUDE.md) и [LevelDesigner.md](LevelDesigner.md).

| Тема | Решение | Почему |
|---|---|---|
| Размер уровня (§12) | Генератор требует **нечётные** Width/Height (мин. 5×5) вместо чётных. Валидатор размер не проверяет | Чётный размер давал лишнюю двойную стену сверху и справа |
| Ключи (§40) | **1 ключ = 1 дверь**, ключ того же цвета, после использования исчезает из инвентаря. Ключ только отпирает дверь; открывать и закрывать её игрок может сколько угодно раз | Правило пользователя |
| Цвет пары | `VisualVariant.ColorTag`: запертая дверь цветная, ключ берёт цвет своей двери, дверь без ключа без цвета; разные пары по возможности разных цветов. Несоответствие — ошибка валидатора | Визуальная идентификация пары (§40) |
| Зомби и двери | Зомби двери **не открывают**: закрытая дверь — препятствие для их маршрутов | Следует из §52 |
| Пол под стенами | У каждой клетки визуальный слой **Floor**, у стен ещё слой **Wall** поверх. Префабы стен делаются без пола | Тонкие стены оставляли пустоты |
| Категории стен | В дополнение к ТЗ: `Cross`, `Isolated`. `Special` — только вручную | Покрыть все 16 сочетаний соседей |
| Визуальный набор | В дополнение к ТЗ: набор **MapFragment** | Фрагменту карты нужен вид |
| Аптечка (§72) | Всегда лечит до полного HP | Решение пользователя |
| Области фрагментов (§58) | Прямоугольник (`GridRect`) | Решение пользователя |
| ID объектов | Строки `{prefix}_{N}`, генерируются автоматически, можно переименовать | Решение пользователя |
| Выбор визуала | Хеш (VisualSeed, вид, клетка или ID) → взвешенный выбор; в уровень сохраняется `VariantId` + поворот | Независимость от порядка, локальное переназначение |
| A* | 4 направления | Простота; можно расширить |
| Тулза | 2D-сетка в окне редактора (не Scene View); превью в сцене по кнопке | Решение пользователя |
| Composition root | Отдельная сборка `Maze.Composition` для LifetimeScope'ов, точки входа и фабрики сессии уровня | Скоупам нужны все слои; остальные слои не зависят от VContainer |
| Сцены (§6–7) | Bootstrap всегда загружена; сцена **Game** грузится аддитивно на каждый уровень, в ней `LevelLifetimeScope` (autoRun выключен), который строится дочерним к проектному | Простая очистка: выгрузка Game убирает всё, что создал уровень |
| Список уровней | `LevelCatalog` (ScriptableObject, Addressable), заполняется Build/Sync; порядок задаётся в инспекторе | Меню не грузит все уровни ради их названий |
| Сервисы Save / Audio / Settings | Пока заглушки, реализуются на своих этапах | Хранить пока нечего |
| Пауза | Не через `Time.timeScale`: `LevelRuntime` перестаёт тикать `ILevelTickable` | Явное управление симуляцией; UI и загрузки не замирают |
| Запуск невалидного уровня | Перед созданием LevelScope уровень проверяется `LevelValidator`; при ошибках — экран ошибки | §109: не оставлять повреждённый уровень |
| UI-заглушки | uGUI с legacy `Text` (встроенный шрифт), сцены собираются кодом (`RuntimeScenesBuilder`) | Не нужен импорт TMP Essentials; финальный UI будет позже |
| Чанки и поклеточная видимость (§54 vs §82/§105) | Чанк 8×8 — один склеенный меш; клетка записана в UV3 каждой вершины; шейдер `Maze/Geometry` схлопывает вершины скрытых клеток по маске-текстуре. Чанки без видимых клеток выключаются целиком | Согласовано с пользователем: мало объектов и draw call'ов, при этом скрытие точно по клеткам. Цена — требования к префабам пола/стен (шейдер, Read/Write) |
| Тени | Жёсткие (`LightShadows.Hard`) | §103: без тяжёлых realtime-теней |
| Сообщения валидатора | На английском; проверки идентифицируются кодами `ValidationCodes` | Единообразие с кодом |

---

## 4. Что где лежит

```
Assets/_Project/
  Scripts/Core/            Maze.Core — чистая логика, без MonoBehaviour
    Common/                DeterministicRandom, StableHash
    Grid/                  GridPosition, Direction, CellType, GridRect, LevelGeometry, LevelGrid
    Level/                 LevelData, данные объектов, PatrolData, LevelSettings, LevelGenerationSettings
    Definitions/           WeaponDefinition, ZombieDefinition
    Generation/            MazeGenerator, MazeGenerationResult
    Visual/                VisualTheme, VisualSet, VisualVariant, VisualData, CellLayer, CellVisualContext/WallShapes,
                           VisualSelector, VisualAssigner, VisualResolver, LevelVisualUsage
    Navigation/            GridPathfinder (A*), KeyDoorSolver, IGridPassability
    Validation/            LevelValidator, ValidationReport, ValidationCodes
    Authoring/             LevelAuthoring (Generate New, Regenerate Visuals), LevelEditing (все ручные правки)
    Common/GameLog         структурированные логи (§108)
    Level/LevelCatalog     список уровней для меню
  Scripts/Gameplay/        Maze.Gameplay — Level/: LevelRuntime, ILevelLoadStep + LevelLoadStage, ILevelTickable, LevelOutcome
  Scripts/Application/     Maze.Application
    Assets/                AddressablesService, IAssetOwner
    Flow/                  GameFlow, ILevelSession(Factory), PauseController
    Levels/                LevelCatalogService
    Services/              IApplicationService, InputService, заглушки Settings/Save/Audio
  Scripts/Presentation/    Maze.Presentation
    UI/                    UIRoot, ScreenRouter, экраны (меню, загрузка, HUD, пауза, результат, ошибка)
    Visual/                LevelVisualSystem, VisualPrefabLibrary, GeometryBuilder (+ PrefabMeshParts, MeshAccumulator),
                           VisibilityChunk, CellVisibilityMask, LevelGeometryView, EntityView(+Registry, Factory),
                           TopDownCamera, LevelViewRoot, GeometryShader (константы шейдера)
  Art/Shaders/             MazeGeometry.shader («Maze/Geometry»)
  Scripts/Composition/     Maze.Composition — ProjectLifetimeScope, LevelLifetimeScope (+ LevelTickDriver),
                           ApplicationEntryPoint, LevelSessionFactory
  Scripts/Editor/          Maze.Editor
    LevelDesigner/         окно, сетка, инструменты, инспектор, превью, Build/Sync, валидация с проверкой ассетов
    Dev/                   PlaceholderThemeBuilder (Maze → Dev → Create Placeholder Theme),
                           RuntimeScenesBuilder (Maze → Dev → Build Runtime Scenes), GeometryShaderEditorGuard
  Scenes/                  Bootstrap.unity (первая в Build), Game.unity
  Tests/EditMode/          Maze.Tests.EditMode — 158 тестов
  Tests/PlayMode/          Maze.Tests.PlayMode — 5 тестов
  Data/Levels/             Level_Dev.asset (тестовый уровень), LevelCatalog.asset
  Data/Themes/             PlaceholderTheme и наборы
  Art/Placeholders/        префабы-заглушки из примитивов (без коллайдеров)
Assets/AddressableAssetsData/   настройки Addressables (созданы Build/Sync)
Docs/                      LevelDesigner.md (руководство), Progress.md (этот файл)
CLAUDE.md                  правила проекта и соглашения для ИИ-агента
```

---

## 5. Что дальше

### Рекомендуемый порядок
1. ~~**Каркас runtime (§4–9)**~~ — сделано (этап 5).
2. ~~**Визуал уровня в игре (§31, §82, §105)**~~ — сделано (этап 6).
3. **Игрок и ввод (§63–66):** `InputService` на Input System (джойстик для Android, WASD и мышь для десктопа), плавное движение, `GridPosition`, Occupancy, `PlayerCellChanged`, камера следует за игроком (`TopDownCamera`).
4. **Видимость (§53–57, §104–106):** `VisibilitySystem` (11×11 + LOS) по событию `PlayerCellChanged`, `VisibilityController` — пишет в `LevelGeometryView` и `EntityView.SetVisible` (отображение уже готово).
5. **Двери, ключи, подбор (§67, §72):** `DoorSystem` с правилом ключей (раздел 3), `PickupSystem`, инвентарь.
6. **Бой и Spatial Query (§68–70, §81)**, **зомби** (§73–77): state machine, обнаружение, патруль, `NavigationSystem` поверх `GridPathfinder`.
7. **Карта, прогресс, звёзды, сохранения, смерть и завершение, UI (§58–60, §85–89).**
8. **Test from Start #N** в Level Designer — как только уровень запускается в игре.
9. **PlayMode-тесты (§110), логирование (§108), обработка ошибок (§109), оптимизация (§103).**

### Открытые мелкие пункты
- **Live Preview** в Level Designer (автоматически перестраивать превью после правок) — предложено, ждёт решения пользователя.
- `Level_Dev` и `PlaceholderTheme` — тестовые ассеты, не финальный контент. У `Level_Dev` Display Name пока «New Level».
- `Assets/Scenes/SampleScene.unity` — шаблонная сцена, не в Build Settings; её можно удалить.
- Чтобы запустить игру из редактора, откройте сцену Bootstrap и нажмите Play. Из сцены Game ничего не запустится.

---

## 6. История

### 2026-10-04 — Этап 6: визуал уровня в игре (§31, §44, §82, §105)

1. **Решение о видимости** (согласовано): чанки + маска в шейдере вместо GameObject на клетку (раздел 3).
2. **Шейдер `Maze/Geometry`:** Standard-освещение плюс схлопывание вершин скрытых клеток, тени тоже. Работает на GLES3 / WebGL 2.
3. **Presentation/Visual:**
   - `VisualPrefabLibrary` — параллельная загрузка только используемых вариантов через `IAssetOwner` уровня;
   - `GeometryBuilder` — ручная склейка мешей, клетка в UV3, сабмеш на материал, Static Batching;
   - `CellVisibilityMask`, `VisibilityChunk`, `LevelGeometryView`;
   - `EntityViewFactory` / `EntityViewRegistry` — вьюхи дверей, выходов и предметов;
   - `LevelVisualSystem` — шаг загрузки `BuildVisuals`; `TopDownCamera` показывает весь уровень.
4. **Core:** `LevelVisualUsage`. **Application:** `IAssetOwner.LoadAsync(AssetReference)`.
5. **Composition:** регистрации в `LevelLifetimeScope`; после выгрузки уровня — `Resources.UnloadUnusedAssets`.
6. **Editor:**
   - проверки префабов пола и стен (`GeometryMeshNotReadable`, `GeometryShaderUnsupported`, `GeometryUnsupportedRenderer`);
   - тема-заглушка на `Maze/Geometry` (старые неиспользуемые материалы удалены);
   - сцена Game с `LevelViewRoot` и `TopDownCamera`, жёсткие тени;
   - `RuntimeScenesBuilder.Rebuild()` без диалога;
   - `GeometryShaderEditorGuard`.
7. **Тесты:** +10 EditMode (`GeometryBuilder`, маска, повороты, `EntityViewFactory`, `LevelVisualUsage`), +1 PlayMode (визуал строится и уничтожается без утечек).
8. **Ручная проверка:** уровень виден в игре; маска скрывает отдельные клетки вместе с тенями; после выхода в меню меши, маска и keyword убраны.

### 2026-10-04 — Этап 5: каркас runtime (§4–9)

1. Удалены пакеты Multiplayer Center и Collab Proxy.
2. **Сборки слоёв:** `Maze.Gameplay`, `Maze.Application`, `Maze.Presentation`, `Maze.Composition`, `Maze.Tests.PlayMode`.
3. **Application:**
   - `AddressablesService` и `IAssetOwner`: handle регистрируется в owner сразу, поэтому не теряется при отмене или ошибке;
   - `GameFlow` — все операции §8; переходы выполняются по одному, новый отменяет текущий;
   - заглушки Settings / Save / Audio, `InputService` с действием Pause (Escape, Start, Back);
   - `LevelCatalogService`.
4. **Gameplay:** `LevelRuntime` — шаги загрузки §9 по стадиям, старт, пауза без `timeScale`, остановка, результат уровня.
5. **Composition:**
   - `ProjectLifetimeScope`, `LevelLifetimeScope`, `ApplicationEntryPoint`;
   - `LevelSessionFactory`: аддитивная сцена Game, дочерний scope, выгрузка в порядке §9.
6. **Presentation:** экраны-заглушки на uGUI и `ScreenRouter`.
7. **Editor:**
   - `RuntimeScenesBuilder` (Maze → Dev → Build Runtime Scenes) собирает Bootstrap и Game и прописывает их в Build Settings;
   - Build/Sync добавляет уровень в `LevelCatalog`.
8. **Логи и ошибки:** `GameLog` (§108); обработка критической ошибки (§109); проверка уровня валидатором перед запуском.
9. **Тесты:** 21 EditMode-тест (`GameFlow` на фейках, `LevelRuntime`), 4 PlayMode-теста (реальный Bootstrap: загрузка, выход без утечек, Retry, выход во время загрузки).

### 2026-10-04 — Подготовка проекта и инструмент дизайнера уровней

1. **Анализ ТЗ и окружения.**
   - Установлены VContainer, UniTask (через OpenUPM) и Addressables.
   - Включён Static Batching для WebGL, удалён Visual Scripting.
   - Создан [CLAUDE.md](../CLAUDE.md).
2. **Модель уровня.** Сетка (`GridPosition`, `LevelGeometry`, `LevelGrid`…), `LevelData` и данные объектов, `WeaponDefinition` / `ZombieDefinition`.
3. **Генератор.** Randomized DFS, LoopDensity, разнесённые по лабиринту старты и выходы, `DeterministicRandom`, массовый тест на 1000 seed'ов.
4. **Визуальные данные.** `VisualTheme`, `VisualSet` с весами, категории стен по соседям, `VisualSeed`, сохранённые назначения и overrides, `VisualResolver`.
5. **A*, решатель ключей и дверей, валидатор** (структура, визуал, геймплей) и массовый тест Generate → Assign Visuals → Validate.
6. **Правило ключей «1 ключ = 1 дверь».** Автоматический подбор цвета пары, проверки валидатора.
7. **Level Designer, часть 1:**
   - окно, вкладки Generation / Visuals / Validate, 2D-сетка;
   - Undo и автоматическая валидация;
   - проверка существования ассетов;
   - тема-заглушка.
8. **Level Designer, часть 2:**
   - вкладка Edit: кисти, расстановка объектов, перемещение, патрули, области фрагментов, ластик;
   - инспектор: переименование ID, связь ключ↔дверь, ручной выбор визуала;
   - Build Preview, Build/Sync (Addressables);
   - вся логика правок в `LevelEditing` с тестами.
   - Написано руководство [LevelDesigner.md](LevelDesigner.md).
9. **Улучшения по отзыву:**
   - инструмент «Рука» (кнопка, H, зажатый пробел);
   - слой пола под каждой клеткой;
   - нечётные размеры вместо чётных (нет двойной стены);
   - исправлено превью: старые копии больше не остаются отрисованными после перезагрузки сцены.
