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
- **Не начато:** весь игровой runtime.
  - Bootstrap и VContainer-скоупы, GameFlow, загрузка уровня;
  - игрок, зомби, бой, видимость, карта, UI, сохранения;
  - сборка геометрии для игры.
- **Тесты:** 127 EditMode-тестов, все проходят, включая массовые прогоны на 1000 seed'ов (§111). PlayMode-тестов пока нет.
- **Руководство для дизайнера:** [Docs/LevelDesigner.md](LevelDesigner.md).

---

## 2. Статус по разделам ТЗ

Легенда: ✅ сделано · 🟡 частично · ⬜ не начато

| § ТЗ | Тема | Статус | Что есть / чего нет |
|---|---|---|---|
| 1–2 | Концепция, платформы | 🟡 | Модуль Android установлен. Модуль **WebGL Build Support** должен поставить пользователь через Unity Hub — проверить, что он стоит |
| 3 | Технологический стек | ✅ | Unity 6000.3.8f1, Built-in RP, VContainer 1.19, UniTask 2.5, Addressables 4.1, Input System 1.18, uGUI, Test Framework. Visual Scripting удалён |
| 4–5 | Архитектура, слои | 🟡 | Есть сборки `Maze.Core`, `Maze.Editor`, `Maze.Tests.EditMode`. Сборок Gameplay / Application / Presentation / Composition ещё нет |
| 6–9 | Bootstrap, ProjectLifetimeScope, LevelLifetimeScope, GameFlow, загрузка уровня | ⬜ | — |
| 10 | LevelData | ✅ | `Core/Level/LevelData.cs` и данные объектов. Мутация только `internal` (доступна редактору и тестам) |
| 11 | Разделение логики и визуала | ✅ | Тип клетки — логика; префаб — только вид |
| 12–14 | Генерация (DFS + LoopDensity, seed'ы) | ✅ | `Core/Generation/MazeGenerator.cs`. **Отступление:** размер нечётный (см. раздел 3) |
| 15–16 | Ручное редактирование, workflow | 🟡 | Всё, кроме **Test / Play** (нет игрового runtime) |
| 17–46 | Визуальная система | 🟡 | Создание и хранение визуала готово: темы, наборы, веса, VisualSeed, контекстные стены, overrides, приоритеты, Regenerate Visuals. Нет runtime-части: `LevelVisualSystem`, `GeometryBuilder`, `EntityViewFactory` |
| 47–49, 53–57, 104–107 | Visibility / Fog of War, чанки | ⬜ | Есть `LevelGrid` и LOS-правила в документации; систем нет |
| 50–51 | Grid, Occupancy | 🟡 | `LevelGrid` есть; Occupancy нет |
| 52 | Navigation + A* | 🟡 | `Core/Navigation/GridPathfinder.cs` (A*, 4 направления, без аллокаций). `NavigationSystem` с учётом дверей в runtime нет |
| 58–60 | Map, фрагменты | 🟡 | Данные фрагментов и областей, редактор областей, проверки пересечений. `MapSystem` / `MapRenderer` нет |
| 61–62 | Стартовые точки, выходы | 🟡 | Данные, генерация, валидация достижимости. Случайный выбор старта и окно «Завершить уровень?» нет. **Test from Start #N** нет |
| 63–80 | Игрок, управление, оружие, бой, звук, аптечки, зомби, Animator | 🟡 | Только `WeaponDefinition`, `ZombieDefinition` (ScriptableObject с параметрами из ТЗ) и их размещение на уровне. Логики нет |
| 81 | Spatial Query | ⬜ | — |
| 82 | GeometryBuilder, Mesh Combine, Static Batching | ⬜ | Есть только редакторское превью `LevelPreviewBuilder` (отдельные префабы, не для игры). Static Batching для WebGL включён в настройках |
| 83 | Object Pooling | ⬜ | — |
| 84 | Addressables | 🟡 | Build/Sync делает уровень (`Levels/<имя>`, группа *Maze Levels*) и префабы темы (группа *Maze Visuals*) Addressable. Runtime-загрузки и владения handle нет |
| 85–89 | SaveData, прогресс, звёзды, смерть, завершение | ⬜ | — |
| 90–92 | Валидатор | ✅ | `Core/Validation/LevelValidator.cs`: структура, визуал, геймплей, уровни Error/Warning/Info, ~50 кодов. В редакторе дополнительно проверяется существование ассетов префабов |
| 93–97 | Диагностика распределения, управление визуалом, Regenerate Visuals, overrides | ✅ | Вкладка Visuals в Level Designer |
| 98 | Детерминизм | ✅ | Свой `DeterministicRandom` (SplitMix64) и `StableHash`; результат сохраняется в уровень. В runtime случайности нет |
| 99–102 | Финальные пайплайны | 🟡 | Редакторская часть готова; runtime — нет |
| 103 | Performance | ⬜ | Заложено: A* без аллокаций, правки уровня локальные. Остального нет |
| 108 | Structured logging | ⬜ | — |
| 109 | Error handling | ⬜ | — |
| 110 | Тесты | 🟡 | EditMode: Grid, Generator, seeds, взвешенный и детерминированный выбор визуала, назначения, overrides, Validator, A*, Key/Door solver, правки уровня. Нет Visibility, Save, Progress, Spatial Queries и всех PlayMode |
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
| Composition root | Планируется отдельная сборка `Maze.Composition` для LifetimeScope'ов | Скоупам нужны все слои |
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
                           VisualSelector, VisualAssigner, VisualResolver
    Navigation/            GridPathfinder (A*), KeyDoorSolver, IGridPassability
    Validation/            LevelValidator, ValidationReport, ValidationCodes
    Authoring/             LevelAuthoring (Generate New, Regenerate Visuals), LevelEditing (все ручные правки)
  Scripts/Editor/          Maze.Editor
    LevelDesigner/         окно, сетка, инструменты, инспектор, превью, Build/Sync, валидация с проверкой ассетов
    Dev/                   PlaceholderThemeBuilder (Maze → Dev → Create Placeholder Theme)
  Tests/EditMode/          Maze.Tests.EditMode — 127 тестов
  Data/Levels/             Level_Dev.asset (тестовый уровень)
  Data/Themes/             PlaceholderTheme и наборы
  Art/Placeholders/        префабы-заглушки из примитивов (без коллайдеров)
Assets/AddressableAssetsData/   настройки Addressables (созданы Build/Sync)
Docs/                      LevelDesigner.md (руководство), Progress.md (этот файл)
CLAUDE.md                  правила проекта и соглашения для ИИ-агента
```

---

## 5. Что дальше

### Рекомендуемый порядок
1. **Каркас runtime (§4–9):**
   - сборки `Maze.Gameplay`, `Maze.Application`, `Maze.Presentation`, `Maze.Composition`;
   - Bootstrap-сцена, `ProjectLifetimeScope` (VContainer), `GameFlow`;
   - `AddressablesService` с владением handle'ами;
   - `LevelLifetimeScope` и загрузка уровня по §9 с отменой через UniTask.
2. **Визуал уровня в игре (§31, §82, §105):**
   - `LevelVisualSystem`, `GeometryBuilder`;
   - склейка мешей по чанкам видимости, Static Batching;
   - `EntityViewFactory`.
3. **Игрок и ввод (§63–66):** `InputService` на Input System (джойстик для Android, WASD и мышь для десктопа), плавное движение, `GridPosition`, Occupancy.
4. **Видимость (§53–57, §104–106):** `VisibilitySystem` (11×11 + LOS) по событию `PlayerCellChanged`, `VisibilityController`.
5. **Двери, ключи, подбор (§67, §72):** `DoorSystem` с правилом ключей (раздел 3), `PickupSystem`, инвентарь.
6. **Бой и Spatial Query (§68–70, §81)**, **зомби** (§73–77): state machine, обнаружение, патруль, `NavigationSystem` поверх `GridPathfinder`.
7. **Карта, прогресс, звёзды, сохранения, смерть и завершение, UI (§58–60, §85–89).**
8. **Test from Start #N** в Level Designer — как только уровень запускается в игре.
9. **PlayMode-тесты (§110), логирование (§108), обработка ошибок (§109), оптимизация (§103).**

### Открытые мелкие пункты
- **Live Preview** в Level Designer (автоматически перестраивать превью после правок) — предложено, ждёт решения пользователя.
- Не удалены пакеты Multiplayer Center и Collab Proxy (пользователь разрешил удалить только Visual Scripting).
- `Level_Dev` и `PlaceholderTheme` — тестовые ассеты, не финальный контент.
- Проверить, что установлен модуль WebGL Build Support.

---

## 6. История

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
