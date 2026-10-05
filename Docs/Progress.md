# Прогресс разработки

Сводка того, что уже сделано относительно ТЗ ([Terms_of_Reference](../Terms_of_Reference)), какие решения приняты сверх или вместо ТЗ, и что делать дальше.
Предназначен для разработчиков и ИИ-агентов, которые подключаются к проекту.

**Как вести файл:** по завершении каждого этапа обновить таблицу статуса (раздел 2), при необходимости — решения (раздел 3), и добавить запись в историю (раздел 6).

**Последнее обновление:** 2026-10-05

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
- **Готово:** игрок и ввод (этап 7).
  - полная абстракция ввода §64 (клавиатура/мышь, геймпад, экранные стик и кнопки на тач-устройствах);
  - появление на случайном (или заданном) старте, плавное движение со скольжением вдоль стен и помощью на поворотах;
  - Occupancy, минимальный `DoorSystem` (открыта/закрыта), `PlayerCellChanged`;
  - выход: окно «Завершить уровень?» — уровень можно пройти от старта до конца;
  - камера следует за игроком.
- **Готово:** видимость / туман войны (этап 8).
  - окно 11×11 вокруг клетки игрока + линия видимости (стены и закрытые двери блокируют);
  - пересчёт только по событиям: появление игрока, смена клетки, дверь в окне открылась/закрылась;
  - скрываются пол, стены и все вьюхи объектов вне видимости; объекты, созданные или сдвинутые позже, получают видимость сразу.
- **Готово:** двери, ключи и подбор (этап 9).
  - кнопка Interact: подобрать оружие в своей клетке, иначе — соседняя дверь (та, куда смотрит игрок): отпереть ключом, открыть, закрыть;
  - ключи подбираются при входе в клетку; ключ только отпирает свою дверь и тратится; дверь не закрывается на игроке или зомби;
  - аптечка при входе лечит до полного HP и исчезает, при полном HP остаётся;
  - два слота оружия (Melee/Ranged), новое оружие сразу активно, старое того же типа выпадает в клетку, переключение 1/2 (плечевые кнопки);
  - вид дверей (закрыта / открыта / замок), HUD: HP, оружие, ключи, сообщения («Locked: needs the red key»).
- **Не начато:** бой, зомби, карта, сохранения.
- **Тесты:** 202 EditMode-теста (включая массовые прогоны на 1000 seed'ов, §111, и фазз-тест движения) и 8 PlayMode-тестов. Все проходят.
- **Руководство для дизайнера:** [Docs/LevelDesigner.md](LevelDesigner.md).

---

## 2. Статус по разделам ТЗ

Легенда: ✅ сделано · 🟡 частично · ⬜ не начато

| § ТЗ | Тема | Статус | Что есть / чего нет |
|---|---|---|---|
| 1–2 | Концепция, платформы | 🟡 | Модули Android и WebGL Build Support установлены. Сборки под платформы ещё не проверялись |
| 3 | Технологический стек | ✅ | Unity 6000.3.8f1, Built-in RP, VContainer 1.19, UniTask 2.5, Addressables 4.1, Input System 1.18, uGUI, Test Framework. Удалены Visual Scripting, Multiplayer Center, Collab Proxy |
| 4–5 | Архитектура, слои | ✅ | Сборки `Maze.Core ← Maze.Gameplay ← Maze.Application ← Maze.Presentation`, `Maze.Composition` (видит все), `Maze.Editor`, `Maze.Tests.EditMode`, `Maze.Tests.PlayMode` |
| 6–9 | Bootstrap, ProjectLifetimeScope, LevelLifetimeScope, GameFlow, загрузка уровня | ✅ | Сцены Bootstrap и Game (аддитивная). `GameFlow` — все операции §8. Пайплайн §9: шаги `ILevelLoadStep` по стадиям `LevelLoadStage`. Сейчас есть шаги `BuildVisuals` и `SpawnPlayer` (игрок и его вьюха), остальные системы добавят свои на своих этапах |
| 10 | LevelData | ✅ | `Core/Level/LevelData.cs` и данные объектов. Мутация только `internal` (доступна редактору и тестам) |
| 11 | Разделение логики и визуала | ✅ | Тип клетки — логика; префаб — только вид |
| 12–14 | Генерация (DFS + LoopDensity, seed'ы) | ✅ | `Core/Generation/MazeGenerator.cs`. **Отступление:** размер нечётный (см. раздел 3) |
| 15–16 | Ручное редактирование, workflow | 🟡 | Всё, кроме кнопок **Test / Play** в Level Designer (уровень запускается из меню игры; runtime-поддержка заданного старта уже есть — `LevelLaunchOptions.StartIndex`) |
| 17–46 | Визуальная система | ✅ | Создание и хранение визуала (темы, наборы, веса, VisualSeed, контекстные стены, overrides, приоритеты, Regenerate Visuals) и runtime-часть: `LevelVisualSystem`, `GeometryBuilder`, `EntityViewFactory`, загрузка только используемых префабов (§44). Визуальные состояния двери (закрыта/открыта/заперта) — `DoorVisual` на префабе, `DoorViewPresenter` |
| 47–49, 53–57, 104–107 | Visibility / Fog of War, чанки | ✅ | Расчёт: `FieldOfView` + `GridLineOfSight` (Core, без аллокаций), `VisibilitySystem` (Gameplay; пересчёт по `Spawned`, `PlayerCellChanged`, `DoorChanged` в окне). Отображение: `VisibilityController` → маска `CellVisibilityMask` + шейдер `Maze/Geometry`, чанки 8×8 без видимых клеток выключаются, `EntityView.SetVisible` (в т.ч. для вьюх, добавленных/сдвинутых позже через `EntityViewRegistry`). AI видимость не учитывает (§57). Detection (§107) — отдельно, на этапе зомби |
| 50–51 | Grid, Occupancy | ✅ | `LevelGrid`, `OccupancyMap` (1 игрок, 0..N зомби, не вместе; пикапы не занимают), `LevelPassability` |
| 52 | Navigation + A* | 🟡 | `Core/Navigation/GridPathfinder.cs` (A*, 4 направления, без аллокаций). `NavigationSystem` с учётом дверей в runtime нет |
| 58–60 | Map, фрагменты | 🟡 | Данные фрагментов и областей, редактор областей, проверки пересечений. `MapSystem` / `MapRenderer` нет |
| 61–62 | Стартовые точки, выходы | 🟡 | Случайный старт при каждом запуске (или заданный через `LevelLaunchOptions`), `ExitSystem` + окно «Завершить уровень?» (Нет — игра продолжается, повторный вход спрашивает снова). Нет кнопки **Test from Start #N** в Level Designer |
| 63–66 | Игрок, управление, движение, взаимодействие | ✅ | `PlayerSystem` (спавн, движение, взгляд по движению, `CellChanged`), `PlayerMovement` (без физики, скольжение, corner assist, без туннелирования), `InputService` (Move/Look/Attack/Interact/SwitchMelee/SwitchRanged/OpenMap/Pause), тач-контролы HUD, `PlayerViewPresenter`, камера следует за игроком. `PlayerDefinition` / `PlayerVisualDefinition`. Inventory (ключи) и Interaction (`PlayerInteraction`) — этап 9. Атака — в этапе боя |
| 67–80 | Оружие, бой, звук, аптечки, зомби, Animator | 🟡 | §67 и §72 готовы: `WeaponSystem` (слоты, подбор, выпадение со сбросом, переключение), `PickupSystem` (ключи, аптечки, оружие на земле), `PlayerHealth`. `WeaponDefinition`, `ZombieDefinition` и их размещение на уровне. Animator игрока получает `Speed` из геймплея. Атак, стрельбы, звука и зомби нет |
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
| 110 | Тесты | 🟡 | EditMode: Grid, Generator, seeds, визуал, Validator, A*, Key/Door solver, правки уровня, **GameFlow** (на фейках: порядок, ошибки, отмена, утечки), **LevelRuntime**, **GeometryBuilder** (чанки, клетки в вершинах, повороты, маска видимости), `EntityViewFactory`, `LevelVisualUsage`, **движение игрока** (стены, скольжение, corner assist, зомби-клетки, фазз-тест 20 000 шагов без туннелирования), `PlayerSystem`, `ExitSystem`, `OccupancyMap`, `DoorSystem`, подтверждение выхода в `GameFlow`, **двери, ключи, аптечки, оружие, Interact**, **Visibility** (окно, LOS, двери, диагональные щели, боковые проходы, пересчёт по событиям). PlayMode: **Bootstrap, GameFlow, Addressables, LevelScope, Retry**, визуал, **Player spawn** (старт, вьюха, камера), **Visibility** (маска, чанки и вьюхи совпадают с `VisibilitySystem`), **двери и предметы** (вид дверей, удаление и появление вьюх предметов; на дев-уровне `Level_Items`). Нет Save, Progress, Spatial Queries и PlayMode-тестов для зомби, смерти, завершения |
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
| Столкновения игрока (§65, §78) | Квадратный футпринт (`BodyHalfSize` = 0.3) против непроходимых клеток целиком, скольжение по осям, **corner assist** (подтягивает в боковой проход, если до его оси ≤ 0.35). Клетки с зомби блокируют только центр игрока | Без физики; в коридор шириной 1 клетка легко попасть с джойстика |
| Параметры игрока | ТЗ их не задаёт: скорость 3.5 клетки/с, HP 100 (по умолчанию в `PlayerDefinition`, меняются в ассете) | — |
| Look (§64) | Действие есть в `InputService`, но взгляд пока только по движению (§65). Прицеливание мышью/правым стиком — решить на этапе боя | Не противоречить §65 |
| Подтверждение выхода (§62) | Пока окно открыто, геймплей на паузе (новое состояние `GameFlowState.ExitConfirmation`) | ТЗ не уточняет; так не получить урон, пока думаешь |
| Interact (§63, §64, §67) | Одна кнопка (E / gamepad West / экранная «Door»): сначала оружие в клетке игрока, иначе соседняя дверь — приоритет той, куда игрок смотрит; дверь, в которой стоит игрок, — в последнюю очередь | В ТЗ на Android нет отдельной кнопки подбора, есть только «door» |
| Ключи | Подбираются автоматически при входе в клетку (ТЗ не уточняет) | Как аптечки; ключ нельзя «не взять» |
| Отпирание | Interact у запертой двери с ключом только отпирает (ключ тратится), открывает следующее нажатие. Без ключа — сообщение с цветом нужного ключа | Правило пользователя «ключ только отпирает» |
| Закрытие двери | Нельзя, если в клетке двери зомби или тело игрока заходит в неё | Иначе игрок или зомби окажется «в стене» |
| Стартовое оружие | Нет: слоты пустые, оружие только с пола | ТЗ не задаёт |
| Нажатия кнопок | `IPlayerInput.WasPressed(action)` — опрос из тика уровня, а не события | Нажатия на паузе не применяются после неё |
| Вид двери | `DoorVisual` на префабе: объекты Closed / Open / Lock и/или Animator с bool `Open`, `Locked`. Без компонента — при открытой двери прячутся рендереры. Активность корня не трогается (её ведёт видимость) | §37: префаб только показывает состояние |
| Общие ассеты игрока | `PlayerDefinition` + `PlayerVisualDefinition` (раздельно: геймплей и вид, как у оружия), Addressable "Player/Definition" / "Player/Visual", грузятся при старте приложения и остаются резидентными | §84: shared assets могут оставаться resident |
| Правила LOS (§55) | Игрок может стоять в любой точке своей клетки: лучи идут из центра и 4 углов (с отступом 0.05) клетки игрока в те же точки каждой клетки окна. **Видима каждая клетка, через которую прошёл луч**, плюс непрозрачная клетка, в которую он упёрся. Стена или закрытая дверь видна также, если **примыкает стороной** к видимой прозрачной клетке, или если это **угол** видимого пола (касается его по диагонали, и обе клетки между ними — видимые стены). Стена, касающаяся видимого пола только по диагонали через скрытый пол, скрыта (дальняя стена комнаты за дверью). Через угловой стык двух стен не видно | Без дыр вдоль лучей и на углах; за закрытой дверью ничего не видно, в т.ч. дальних стен комнаты |
| Геометрия vs объекты | Пол и стены рисуются по **Revealed** = видимые клетки + одиночная скрытая клетка между двумя видимыми прозрачными (пол, открытая дверь) по горизонтали или вертикали (например, тень за колонной). Объекты — строго по видимости | Нет одиночных дыр в полу |
| Камера | Почти сверху (наклон 88°), не вращается; расстояние подбирается так, чтобы квадрат ±6.5 клетки вокруг игрока помещался при любом соотношении сторон экрана | Пожелание пользователя; портретная ориентация на Android |
| Видимость до появления игрока | Всё скрыто (идёт экран загрузки) | — |
| Сообщения валидатора | На английском; проверки идентифицируются кодами `ValidationCodes` | Единообразие с кодом |

---

## 4. Что где лежит

```
Assets/_Project/
  Scripts/Core/            Maze.Core — чистая логика, без MonoBehaviour
    Common/                DeterministicRandom, StableHash
    Grid/                  GridPosition, Direction, CellType, GridRect, LevelGeometry, LevelGrid
    Level/                 LevelData, данные объектов, PatrolData, LevelSettings, LevelGenerationSettings
    Definitions/           WeaponDefinition, ZombieDefinition, PlayerDefinition
    Generation/            MazeGenerator, MazeGenerationResult
    Visual/                VisualTheme, VisualSet, VisualVariant, VisualData, CellLayer, CellVisualContext/WallShapes,
                           VisualSelector, VisualAssigner, VisualResolver, LevelVisualUsage, PlayerVisualDefinition
    Navigation/            GridPathfinder (A*), KeyDoorSolver, IGridPassability
    Visibility/            FieldOfView (11×11 + LOS), GridLineOfSight (луч по клеткам, IGridOpacity)
    Validation/            LevelValidator, ValidationReport, ValidationCodes
    Authoring/             LevelAuthoring (Generate New, Regenerate Visuals), LevelEditing (все ручные правки)
    Common/GameLog         структурированные логи (§108)
    Level/LevelCatalog     список уровней для меню
  Scripts/Gameplay/        Maze.Gameplay
    Level/                 LevelRuntime, ILevelLoadStep + LevelLoadStage, ILevelTickable/ILevelLateTickable, LevelOutcome,
                           LevelLaunchOptions, ExitSystem
    Grid/                  OccupancyMap, LevelPassability
    Doors/                 DoorSystem (open/closed, locked/unlocked)
    Player/                PlayerSystem (+ PlayerStartSelector), PlayerMovement, IPlayerInput (+ PlayerAction), PlayerHealth,
                           PlayerInventory (ключи), PlayerInteraction (Interact)
    Weapons/               WeaponSystem (слоты Melee/Ranged), WeaponRuntime
    Pickups/               PickupSystem (ключи, аптечки, оружие на земле), Pickup
    Visibility/            VisibilitySystem, LevelOpacity
  Scripts/Application/     Maze.Application
    Assets/                AddressablesService, IAssetOwner
    Flow/                  GameFlow, ILevelSession(Factory), PauseController
    Levels/                LevelCatalogService
    Services/              IApplicationService, InputService, SharedDefinitionsService, заглушки Settings/Save/Audio
  Scripts/Presentation/    Maze.Presentation
    UI/                    UIRoot, ScreenRouter, экраны (меню, загрузка, HUD с тач-контролами, пауза, «Завершить уровень?»,
                           результат, ошибка)
    Visual/                LevelVisualSystem, VisualPrefabLibrary, GeometryBuilder (+ PrefabMeshParts, MeshAccumulator),
                           VisibilityChunk, CellVisibilityMask, LevelGeometryView, EntityView(+Registry, Factory),
                           TopDownCamera, LevelViewRoot, GeometryShader (константы шейдера), PlayerViewPresenter,
                           VisibilityController, DoorVisual, DoorViewPresenter, PickupViewPresenter
  Art/Shaders/             MazeGeometry.shader («Maze/Geometry»)
  Scripts/Composition/     Maze.Composition — ProjectLifetimeScope, LevelLifetimeScope (+ LevelTickDriver),
                           ApplicationEntryPoint, LevelSessionFactory
  Scripts/Editor/          Maze.Editor
    LevelDesigner/         окно, сетка, инструменты, инспектор, превью, Build/Sync, валидация с проверкой ассетов
    Dev/                   PlaceholderThemeBuilder (Maze → Dev → Create Placeholder Theme / Player / Weapons,
                           Rebuild Placeholder Doors),
                           RuntimeScenesBuilder (Maze → Dev → Build Runtime Scenes), GeometryShaderEditorGuard
  Scenes/                  Bootstrap.unity (первая в Build), Game.unity
  Tests/EditMode/          Maze.Tests.EditMode — 202 теста
  Tests/PlayMode/          Maze.Tests.PlayMode — 8 тестов
  Data/Levels/             Level_Dev.asset (тестовый уровень), Level_Items.asset (дев-уровень: копия Level_Dev с дверями,
                           ключом, аптечкой, оружием), LevelCatalog.asset
  Data/Weapons/            Knife, Bat (Melee), Pistol (Ranged) — заглушки
  Data/Themes/             PlaceholderTheme и наборы
  Data/Player/             PlayerDefinition, PlayerVisual (Addressable, группа Maze Shared)
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
3. ~~**Игрок и ввод (§63–66)**~~ — сделано (этап 7).
4. ~~**Видимость (§53–57, §104–106)**~~ — сделано (этап 8).
5. ~~**Двери, ключи, подбор (§67, §72)**~~ — сделано (этап 9).
6. **Бой и Spatial Query (§68–70, §81)**, **зомби** (§73–77): state machine, обнаружение, патруль, `NavigationSystem` поверх `GridPathfinder`.
7. **Карта, прогресс, звёзды, сохранения, смерть и завершение, UI (§58–60, §85–89).**
8. **Test from Start #N** в Level Designer: runtime уже принимает `LevelLaunchOptions.StartIndex`; нужна кнопка в окне и запуск Play с Bootstrap.
9. **PlayMode-тесты (§110), логирование (§108), обработка ошибок (§109), оптимизация (§103).**

### Визуализация (запланировано, по запросу пользователя)
- **Анимированный туман войны** поверх скрытых клеток (только визуал, на видимость и AI не влияет):
  - одна плоскость над уровнем (примерно на высоте стен) с прозрачным шейдером, читающим глобальную маску `_MazeVisibility`: где клетка скрыта — туман, где видна — прозрачно (для геометрии — `RevealedCells`);
  - мягкая граница: билинейная выборка маски с небольшим размытием вместо ступенек по клеткам;
  - движение: два слоя шума, медленно сдвигающиеся в разные стороны;
  - плавное рассеивание и сгущение (доли секунды) при смене видимых клеток вместо мгновенного переключения;
  - производительность (WebGL 2, Android): шум из маленькой текстуры, без многослойного fBm в шейдере; проверить overdraw на устройстве;
  - скорость, плотность, цвет, мягкость границы — параметры материала, чтобы настраивать в инспекторе.
- **Анимации игрока и зомби** (например, Mixamo), подключать на этапах, где появляются действия (атаки — бой, зомби — этап зомби), или отдельной визуальной полировкой:
  - от пользователя: модели в FBX «With Skin» с Humanoid-ригом (или прогнать через авториг Mixamo); анимации «Without Skin», FBX for Unity, 30 fps, ходьба/бег — **In Place** (движение в коде, root motion не используется); оценка в движении (MCP даёт только скриншоты);
  - игрок: idle, ходьба, бег, удар ближнего боя, выстрел, урон, смерть (опц. перезарядка); зомби: idle, патруль, преследование, атака, урон, смерть. Нет клипа — можно обойтись (например, вспышка при уроне);
  - Animator только отображает состояние геймплея (параметры как `Speed` у игрока); тайминг атак и кулдаун задаёт `WeaponDefinition`, клип подгоняется под них по скорости;
  - скорость проигрывания ходьбы масштабируется по реальной скорости, чтобы ноги не скользили;
  - типы зомби — один контроллер + Animator Override Controller;
  - проверить кости/полигоны моделей для Android и WebGL.

### Открытые мелкие пункты
- **Live Preview** в Level Designer (автоматически перестраивать превью после правок) — предложено, ждёт решения пользователя.
- `Level_Dev` и `PlaceholderTheme` — тестовые ассеты, не финальный контент. У `Level_Dev` Display Name пока «New Level».
- `Assets/Scenes/SampleScene.unity` — шаблонная сцена, не в Build Settings; её можно удалить.
- Чтобы запустить игру из редактора, откройте сцену Bootstrap и нажмите Play. Из сцены Game ничего не запустится.
- **Enter Play Mode Options:** перезагрузка домена при входе в Play включена (`m_EnterPlayModeOptions: 0`, решение пользователя 2026-10-05). MCP на время своих PlayMode-прогонов временно её выключает и обычно возвращает обратно; если после прерванного прогона в `EditorSettings.asset` снова окажется `1` — вернуть 0.

---

## 6. История

### 2026-10-05 — Этап 9: двери, ключи, подбор (§37, §40, §63, §67, §72)

1. **Ввод:** кнопки действий опрашиваются из тиков (`IPlayerInput.WasPressed(PlayerAction)`), события убраны: нажатия на паузе не срабатывают после неё. Pause остаётся событием.
2. **Gameplay:**
   - `DoorSystem`: запертые двери (есть ключ и дверь не открыта изначально), `Unlock`, событие `DoorUnlocked`, открыть запертую нельзя;
   - `PlayerHealth`, `PlayerInventory` (ключи);
   - `WeaponSystem` + `WeaponRuntime` (слоты, активное, сброс при выпадении, SwitchMelee/SwitchRanged);
   - `PickupSystem` (шаг `InitializeSystems`): ключи при входе, аптечки при входе и неполном HP, `TryTakeWeapon` с выпадением старого; события `Added`/`Removed`;
   - `PlayerInteraction`: Interact (решения в разделе 3), событие `Interacted` для UI.
3. **Presentation:** `DoorVisual` (компонент префаба двери), `DoorViewPresenter`, `PickupViewPresenter` (убирает вьюхи взятых, создаёт вьюху выпавшего оружия с его сохранённым визуалом), `HudPresenter` + строки статуса и сообщений в HUD.
4. **Editor и контент:** префабы дверей-заглушек с состояниями (Maze → Dev → Rebuild Placeholder Doors, GUID сохранены), Maze → Dev → Create Placeholder Weapons, дев-уровень `Level_Items`; сцены пересобраны (HUD).
5. **Тесты:** +7 EditMode (двери с ключами и без, запрет закрытия на игроке и зомби, ключи, аптечка, подбор и выпадение оружия, переключение), +1 PlayMode (вид дверей и вьюхи предметов на `Level_Items`).
6. **Ручная проверка на `Level_Items`:** подбор ножа и пистолета, открытие двери, «Locked» без ключа, бита вместо ножа (нож выпал), ключ подобран, дверь отперта и открыта отдельно, видимость открылась за дверью, аптечка при полном HP осталась.

### 2026-10-05 — Этап 8: видимость (§46–49, §53–57, §104–107)

1. **Core/Visibility:** `GridLineOfSight` (обход клеток по лучу, Amanatides–Woo; блокирует непрозрачная клетка между концами или угловой стык двух непрозрачных; пригодится и для пуль), `IGridOpacity` / `CellMaskOpacity`, `FieldOfView` (окно 11×11, permissive LOS по 5×5 точкам, стены вокруг видимого пола; переиспользует буферы).
2. **Gameplay/Visibility:** `LevelOpacity` (Wall и закрытая Door), `VisibilitySystem` — шаг загрузки `InitializeVisibility`, пересчёт по `PlayerSystem.Spawned`, `CellChanged`, `DoorSystem.DoorChanged` (если дверь в окне), событие `Changed`.
3. **Presentation:** `VisibilityController` (шаг `BuildVisuals` после `LevelVisualSystem`): обновляет только изменившиеся клетки маски, `ApplyVisibility`, видимость вьюх объектов; `EntityViewRegistry` получил события `Added` / `Moved` и метод `Move` для движущихся объектов (зомби).
4. **Тесты:** +16 EditMode (`FieldOfView`, `GridLineOfSight`, `VisibilitySystem`), +1 PlayMode (маска, чанки и вьюхи совпадают с расчётом).
5. **Нестабильные PlayMode-прогоны — причина найдена и устранена.** MCP запускает PlayMode без перезагрузки домена; если перед этим шёл EditMode-прогон, Test Framework оставляет свой `EditModePcHelper` и читает поле состояния итератора у енумератора `[UnitySetUp]`/`[UnityTest]`. У енумератора `UniTask.ToCoroutine` такого поля нет → NRE во всех SetUp/TearDown. Тела PlayMode-тестов теперь оборачиваются в обычный итератор (`Async(...)` в `BootstrapFlowTests`).
6. **Ручная проверка:** в игре видна только область вокруг игрока (коридор до поворота и стены вокруг), остальное скрыто; совпадает с картой видимости.
7. **Доработка по отзыву пользователя:**
   - камера почти сверху (88°), расстояние под любое соотношение сторон (`TopDownCamera.FollowDistance`);
   - дыры вдоль лучей: раньше клетка проверялась только лучами к ней самой, и дальняя клетка могла быть видна при скрытой ближней. Теперь видна каждая клетка, через которую прошёл луч (`GridLineOfSight.Trace` + `IGridCellVisitor`);
   - дыры от стен: стены видны по соседству **стороной** с видимым полом или как угол видимого пола; раньше хватало касания по диагонали, и дальние стены комнаты за закрытой дверью рисовались вокруг скрытого пола;
   - одиночная скрытая клетка между двумя видимыми прозрачными (тень за колонной) дорисовывается для геометрии (`IsRevealed`), объекты — строго по видимости;
   - тесты: тень за колонной, комната за закрытой дверью с дальней стеной, связность видимого пола на 20 сгенерированных лабиринтах.

### 2026-10-04 — Этап 7: игрок и ввод (§50–51, §61–66)

1. **Ввод:** `InputService` реализует `IPlayerInput` (Gameplay) — Move, Look, Attack, Interact, SwitchMelee, SwitchRanged, OpenMap, Pause для клавиатуры/мыши и геймпада; экранные стик и кнопки в HUD (видны только на тач-устройствах) эмулируют геймпад.
2. **Gameplay:** `OccupancyMap`, `LevelPassability`, минимальный `DoorSystem`; `PlayerSystem` — старт (`PlayerStartSelector`, `LevelLaunchOptions`), плавное движение (`PlayerMovement`: футпринт, скольжение, corner assist, подшаги), взгляд по движению, `CellChanged`; `ExitSystem`; `ILevelLateTickable` для вьюх.
3. **Application:** `SharedDefinitionsService` (определения игрока резидентны); `GameFlow` — `StartLevel(levelId, options)`, Retry с теми же опциями, состояние `ExitConfirmation` и `ConfirmExit`.
4. **Presentation:** `PlayerViewPresenter` (позиция, поворот, Animator `Speed`), `TopDownCamera.Follow`, экран «Завершить уровень?», тач-контролы.
5. **Editor:** Maze → Dev → Create Placeholder Player; Build/Sync держит общие ассеты Addressable (группа Maze Shared); сцены пересобраны.
6. **Тесты:** +21 EditMode (движение, фазз-тест, игрок, выход, Occupancy, двери, подтверждение выхода и опции запуска), +1 PlayMode (спавн игрока, вьюха, камера).
7. **Ручная проверка:** игрок появляется на старте, ходит (клавиатура и виртуальный геймпад), упирается в стены точно по границе клетки, камера следует.

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
