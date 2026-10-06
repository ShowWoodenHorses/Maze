# Список префабов

Какие префабы нужны игре, сколько видов каждого и какие у них требования. Сейчас все они — заглушки из примитивов (`Art/Placeholders`), созданные меню **Maze → Dev → Create Placeholder …**. Финальные префабы подключаются в тот же слот: вариант в Visual Set темы или поле в definition-ассете.

## Сводка

| Что | Где подключается | Минимум | Сейчас (заглушки) | Рекомендуется |
|---|---|---|---|---|
| Пол | Theme → Floor Set | 1 | 3 + 1 Special | 3–4 вариации + Special по желанию |
| Стены | Theme → Wall Set | **6** (по одной на категорию) | 7 | 6 категорий, у Straight 2–3 вариации |
| Двери | Theme → Door Set | 1 + 1 на каждый цвет ключа | 5 (обычная + 4 цвета) | 1–2 обычных + 4 цветных |
| Ключи | Theme → Key Set | 1 на каждый цвет | 4 (red, blue, green, yellow) | 4 |
| Выход | Theme → Exit Set | 1 | 1 | 1–2 |
| Аптечка | Theme → Medkit Set | 1 | 1 | 1 |
| Оружие на полу | Theme → Weapon Set | 1 на каждое оружие | 1 общий | 3 (нож, бита, пистолет) |
| Зомби | Theme → Zombie Set | 1 на тип | 2 общих | 3 типа × 1–3 вариации |
| Фрагмент карты | Theme → MapFragment Set | 1 | 1 | 1 |
| Игрок | `Data/Player/PlayerVisual` | 1 | 1 | 1 |
| Пуля | `Data/Combat/CombatVisual` → Bullet | 0–1 | 1 | 1 |
| Попадание | `CombatVisual` → Impact | 0–1 | 1 | 1 |
| Взмах ближнего боя | `CombatVisual` → Melee Swing | 0–1 | 1 | 1 |

**Итого минимум ≈ 29 префабов:** пол 1, стены 6, двери 5, ключи 4, выход 1, аптечка 1, оружие 3, зомби 3, фрагмент 1, игрок 1, эффекты 3. С вариациями получается 40–50.

## Подробно

### Пол (Floor)
- Одна клетка 1×1 м, пивот в центре клетки на уровне пола (y = 0).
- Пол есть **под каждой клеткой**, в том числе под стенами.

### Стены (Wall)
Категория выбирается по соседям-стенам, поворот подставляется автоматически. Префаб делается в **канонической ориентации** (сверху, North = +Z):

| Категория | Соседи-стены в префабе |
|---|---|
| Isolated | нет |
| End | N |
| Straight | N + S |
| Corner | N + E |
| TJunction | N + E + S |
| Cross | N + E + S + W |
| Special | любые, ставится только вручную |

- **Без пола** внутри: пол рисуется отдельным слоем.
- Высота у заглушек 1.5 м. Камера смотрит почти сверху, поэтому важнее верхняя грань.

### Требования к полу и стенам (проверяет валидатор)
- Материалы на шейдере **`Maze/Geometry`**: на нём работает туман войны по клеткам.
- Меши с включённым **Read/Write**.
- Только MeshFilter + MeshRenderer, без скриптов, коллайдеров и анимаций.

### Двери (Door)
- Поворот 0 перекрывает проход Север–Юг.
- **Обычная дверь — без ColorTag. Запертая — с ColorTag**, совпадающим с цветом ключа (`red`, `blue`, …).
- Состояния показывает компонент **`DoorVisual`** на корне: дочерние объекты `Closed`, `Open`, `Lock` (замок у запертой) и/или Animator с bool `Open` и `Locked`. Без компонента открытая дверь просто прячется.

### Ключи (Key)
- По одному на цвет, `ColorTag` как у двери. Сколько цветов, столько возможно одновременно разных пар на уровне.

### Оружие (Weapon)
- В варианте заполнить поле **Definition** ссылкой на `WeaponDefinition` (Knife, Bat, Pistol). Тогда пистолет на полу всегда будет выглядеть как пистолет. Сейчас вариант один общий.

### Зомби (Zombie)
- В варианте заполнить **Definition** ссылкой на `ZombieDefinition` (Walker, Listener, Hunter), чтобы тип был виден.
- Лицом по **+Z**, пивот у ног.
- Сейчас — `Art/Zombie/Zombie_01..03` (Synty, Humanoid; одна модель, разные материалы): Walker, Listener, Hunter. Собираются меню **Maze → Dev → Build Zombie Animations**: клипы `Art/Animations/Zombie_*` → Humanoid, общий контроллер `Art/Zombie/Zombie.controller` на все префабы папки, масштаб 0.75, варианты Zombie Set темы (`zombie_walker/listener/hunter` с Definition) и переназначение зомби в сохранённых уровнях. Новый вид — префаб в `Art/Zombie` + строка в `ZombieAnimationsBuilder.Looks`.
- Animator (необязательно, любой параметр можно опустить; имена — `ZombieAnimatorParameters`): float `Speed`, bool `Chasing` (бег вместо ходьбы), bool `Alert` (рык перед погоней), `WalkPlayback`/`RunPlayback` (множители, ставит презентер) и `WalkGroundSpeed`/`RunGroundSpeed` (данные билдера: скорость ступни клипа), float `IdleVariant` (0/1), триггер `Attack` + int `AttackIndex` + float `AttackSpeed`, триггер `Hit` (клипа нет), bool `Dead`.

### Игрок
- Лицом по **+Z**, пивот у ног.
- Сейчас — модель `Art/Player/SM_Chr_Hunter_Male_01` (Synty, Humanoid) с клипами Mixamo `Art/Animations/Player_*`. Собирается меню **Maze → Dev → Build Player Animations**: клипы → Humanoid, контроллер `Art/Player/Player.controller`, маска `PlayerUpperBody`, масштаб модели 0.75, назначение в `PlayerVisual`. Шаг бега подгоняется под скорость, но не быстрее 1.4× (`LocomotionAnimation`).
- Animator (необязательно, любой параметр можно опустить; имена — `PlayerAnimatorParameters`): float `Speed`, int `Weapon` (0 нет, 1 ближнее, 2 дальнее), триггеры `Attack` + int `AttackIndex` + float `AttackSpeed`, `Shoot` + bool `Automatic`, bool `Reloading` + float `ReloadSpeed`, триггеры `Hit`, `Use`, bool `Dead`; слой `UpperBody` выключается при смерти.

### Эффекты боя
- **Bullet:** летит вдоль +Z.
- **Impact / Melee Swing:** короткие эффекты, живут `EffectDuration` (0.15 с), берутся из пула. Любой из трёх можно оставить пустым.

### Общее для всех префабов
- **Никакой физики:** ни Collider, ни Rigidbody.
- Никаких геймплейных скриптов: префаб только показывает состояние.
- Префабы подключаются через Addressables: Build / Sync в Level Designer делает их Addressable сам.
- Модели персонажей и анимации (Mixamo) — см. раздел «Анимации игрока и зомби» в [Progress.md](Progress.md).
