# Журнал изменений (новые сверху; держать ≤ ~20 КБ, старое переносить в `docs/archive/`)

## 2026-11-23 — имя босса в сжатой `BossHealthBar`
- `BossHealthBar.cs`: плашка 42→50 px (`Height`), имя (шрифт до 22, мин. 16) в верхних ~56% высоты, полоса HP — нижние 30%; «ЯРОСТЬ» справа в той же строке (имя ≤70% ширины), не перекрывает имя. Мировые полоски боссов, фазы, HP и урон не тронуты. `ArenaEvents` берёт `BossHealthBar.Height`, анонс сдвигается сам.
- Проверки (batch, 23.11): `MiniBossChecks`, `BossShowcaseChecks` — 0 падений, 0 ошибок; кадры `Logs/mini-boss.png`, `Logs/boss-showcase.png` (Страж, Королева, Снайпер, Громила) — имя и цифры HP читаются, «ЯРОСТЬ» без наложения. `CombatHudChecks`, `WorkshopProjectsChecks.RunEquipmentBatch` PASS.
- Не подтверждено: `WorkshopLayoutChecks` падает NullReference на `Node weapon:N:Poison` (WorkshopLayoutChecks.cs:168, устаревшее ожидание, 3 кадра раскладки при этом сохранены); отдельного кадра «рой Королевы крупно» и «toast поверх FirstRunHints» нет — по коду `FirstRunHints` гасит плашку при `ToastVisible`.

## 2026-11-22 — HUD врагов/боссов/событий, кривая опыта, кап урона, тексты испытаний
- `EnemyHealthDisplay`: обычные враги — тонкая полоска без цифр только после урона; элиты — с цифрами сразу; `Enemy_tank`/`Enemy_Armored` — с цифрами после урона; у `BossController`/`MiniBoss` мировая полоска не создаётся (числа урона остались).
- `ArenaEvents`: центральный баннер → компактный анонс под таймером (~2 с, ниже полосы босса, если она видна); плашка справа не менялась. `FirstRunHints` прячется на время тоста (`PlayerProgression.ToastVisible`). `BossHealthBar`: высота 58→42, отступ 110→105 (`AnyVisible`/`Height`).
- `WeaponSelectScreen`: подписи шкал Урон/Темп/Дальность не показывались (высота строки < шрифта + отступы Label → обрезка) — исправлено; тексты RU/EN уже были в каталоге. Тот же дефект устранён в анонсе событий и в имени/«ЯРОСТЬ» полосы босса. `WorkshopTree.AddNode`: у веток Пистолета/Автомата подписи узлов скрыты (иконка и `0/3`).
- `Progression.asset` и дефолт `ProgressionSettings`: `earlyLevelExperience` = 6, 10, 16, 28, 46, 74, 115 (уровни 1→8; с 8→9 по формуле 180, 9→10 216).
- `PlayerProgression.Available()`: «Урон пуль» не выпадает на пределе ×5 — учитывается полный множитель (`RawDamageMultiplier`: карточки + мета + «Стеклянная пушка» + мастерство), как в `ApplyStats`.
- Тексты: «Страж» → «финальный босс» в `runmodifiers.269/270/271`, `challengescreen.091` (RU/EN), `GameTextSources.cs` перегенерирован. Остальные «Страж» (бой, Мастерская, реликвия) не трогали.

## 2026-11-21 — Звуки и музыка из «Загрузок»
- `GameMusic.ogg` заменён на `Battle_1.ogg` (GUID прежний, старый трек в `_incoming/audio-replaced-2026-11-21/`); новый `RecordedAudio/BossMusic.mp3` + `GameAudio.BossFight` из `BossController.Start/OnDestroy` — музыка босса, пока он жив. Лицензия обоих треков не записана — спросить владельца.
- Kenney CC0 в `Resources/Sfx/` (и исходники в `Audio/Source/Kenney/`, карта в `AudioSourcePrep.BuildFromKenney`): Pickup, LevelUp, HeroDash, Chest вместо синтеза; новые `Sfx.UiHover/UiClick/UiBack` (в конце enum) и компонент `UiSound.cs` (наведение/стрелки/нажатие) на `GameUi.Button`, плитках оружия и узлах Мастерской; Esc назад в меню — `UiBack`.
- `AudioChecks.RunPlayBatch` дополнен: музыка босса и возврат, новые звуки взяты из файлов.

## 2026-11-21 — Шрифт интерфейса GameFont (Jura SemiBold)
- `Assets/Resources/Fonts/GameFont.ttf` (Jura SemiBold, новый файл, импортирован — `.meta` guid fc6db9bd…) подхватывается `GameUi.DefaultFont` через `Resources.Load("Fonts/GameFont")`.
- В Jura есть вся латиница и кириллица, но нет → ← ↑ ↓ ★ ● ○ ► ■ ✓ (разбор cmap). В импорте добавлены fallback-шрифты Windows: Segoe UI, Segoe UI Symbol, Arial (`fontNames` в .meta).
- Новая `Assets/Editor/FontChecks.cs` (`FontChecks.RunBatch`, меню Tools/Checks/Game font): шрифт из Resources, растеризация латиницы, кириллицы и символов UI.

## 2026-11-21 — CombatHudChecks и WeaponChecks под текущую игру
- `CombatHudChecks`: карточки ищутся по разметке `</b></size>\n<size=` (компактные карточки 19.11, а не `<size=27>`), маркеры «новое/ранг/ПРОКЛЯТИЕ/УЛУЧШЕНИЕ» проверяются и в EN (new/rank/CURSE/UPGRADE).
- `WeaponChecks`, раздел поворота: с героем v3 выстрел даёт поза прицеливания (PoseAiming), `FaceNow` не вызывается — тело доворачивает `PlayMovement`. Проверка включает `PlayMovement` на этот раздел (ввод в batch нулевой) и ждёт поворот до 0,8 с после попадания; без этого тело крутилось с заданной тестом скоростью и угол совпадал случайно.

## 2026-11-21 — Меню к публичному билду: Тест-арена по F10, Esc назад, фокус клавиатуры/геймпада
- `StartMenu`: «Тест-арена» видна только в редакторе (`#if UNITY_EDITOR`), в билде скрыта, F10 на главном экране показывает/прячет (на сессию). Esc / кнопка геймпада 1 (B) — назад из выбора оружия (в коопе игрок 2 → игрок 1), Мастерской (первый Esc закрывает закреплённое окно — `WorkshopTree.EscapeHandledFrame/PopupPinned`), Испытаний, Настроек, Статистики; на главном ничего. Возврат домой — `ShowHome()` с фокусом на «Старт».
- Новые `UiFocus` (Set/Clear), `UiFocusFrame` (рамка при выборе стрелками, не мышью; добавляется в `GameUi.Button`), `SelectRelay`. `MenuButtonFx` подсвечивается и от фокуса клавиатуры.
- Начальный фокус: «Старт»; плитка выбранного оружия (`WeaponSelectScreen.FocusSelected`, и при смене игрока в коопе); «Продолжить» в паузе; «Заново» в результатах через 0,6 с (Пробел рывка в момент смерти не перезапускает). Фокус снимается при входе в бой и выходе из паузы (Пробел = Submit).
- Плитки оружия: `Navigation.Automatic`, фокус стрелками сразу показывает подробности (SelectRelay), Enter/Пробел/Submit = второй клик (в бой или покупка). Мышь — как раньше (первый клик выбирает).
- Проверки: `CoopChecks` открывает замок кооператива на время прогона и возвращает прежнее значение (с 11.11 кооп закрыт ключом, проверка падала).

## 2026-11-21 — VerticalSliceChecks снова проходит
- Потолки A07 считаются как `min(cap из ProgressionSettings, 1 + maxRanks·amount)` (урон 24×0,12 = ×3,88; учтён `WeaponTuning`), вместо 5/3/2/12.
- HP босса — новый `BossController.StartingHealth(kind)` (используется и при создании) вместо 2600; финальный босс в проверке — Страж (`NextBoss`), «Веер» запускается напрямую, если не выпал случайно за 15 с.
- Очередь 3 уровней — на карточке «Гранаты» (радиус теперь требует гранаты, а Bonus считается по каталогу); гранаты/радиус ждут ×(1+amount) из каталога; для радиуса в каталоге оставлены обе карточки.
- Предупреждение о боссе ждётся до 2 с (двух кадров не всегда хватало), проверка остановки волн убрана: с 12.11 волны не останавливаются, а тест сам выключает их в начале.

## 2026-11-21 — Фокус цели по ЛКМ
- `AutoShoter.ManualFocusTarget`: игрок 1, пока зажата ЛКМ (игра идёт, курсор не над UI), — луч камеры на плоскость высоты героя, из `WeaponTargets.Collect` берётся живой враг в дальности оружия, ближайший к курсору в радиусе `FocusCursorRadius` 6 м; он заменяет автовыбор (у Dual — правый ствол). Нет такого врага — обычный автоприцел. `FocusTarget` — для проверок.
- Кольцо фокуса: один LineRenderer на стрелка (жёлтый, 0,07 м, радиус тела +0,35), включается только при фокусе, прячется на паузе/OnDisable.
- Подсказка `hints.move` дополнена «Зажми ЛКМ для фокуса цели» / «Hold LMB to focus a target».
- `CombatMechanicsChecks`: удар по элите делается при удаче 0 — удача даёт +10% крита за ранг, и проверка изредка падала (42 вместо 28).

## 2026-11-21 — Телеметрия забегов в CSV
- `RunStatistics.EndRun` → `AppendPlaytestRow`: одна строка в `persistentDataPath/playtest_runs.csv` (заголовок при создании) только в первом EndRun забега, не в TestMode и не в batch. Колонки: Date,Result,Weapon,Branch,Evolved,DurationSec,Level,Wave,Kills,DamageDealt,AvgDPS,DamageTaken,DeathCause,BossTimeSec,Threat,EndlessRound; InvariantCulture, экранирование CSV, UTF-8 без BOM, всё в try/catch (ошибка — только LogWarning).
- Weapon — `WeaponData.weaponName`, Branch — `WeaponElement`(+ранг), DeathCause — `HeroDamageCause`; в коопе пишется первый герой. `GameSession.Finish`: причина смерти теперь записывается до `EndRun` (иначе строка её не видела).

## 2026-11-21 — Ритм боёв с боссами: передышки, рой порциями, мягкие ожоги
- `BossController`: `recoveryBonus` (атака выставляет, цикл сбрасывает) прибавляется к паузе после атаки, не умножаясь на ×0,6 второй фазы; `HeavyRecovery` 1,2 с — тройной таран Стража, тройной луч и луч-маятник Снайпера; `LastRecovery` для проверок.
- `BossQueen.QueenBrood`: все метки сразу, затем 3 порции через 0,9 с (по 6, в ярости по 8; было 18/26 разом), порция k — каждая третья метка по кругу; после роя +2,0 с (`BroodRecovery`). Предел 60 миньонов прежний.
- `MiniBoss`: после двойного удара +1,2 с (`DoubleSlamRecovery`).
- `EnemyFx.ScorchColor` (0,3, 0,22, 0,14) вместо почти чёрных ожогов в `Explosion` и метеоритах `ArenaEvents`; `FxSplat` угасает плавно (smoothstep на последних 45% жизни) вместо линейного обрыва.

## 2026-11-21 — Быстрый старт опыта и подсказки первого забега (B07, частично)
- `ProgressionSettings.earlyLevelExperience` = {6, 10, 16} (также в `Progression.asset`): цена уровней 1→2, 2→3, 3→4; с 4-го — прежняя формула 12+10·(n−1)^1,45. Первые 3 уровня: 32 опыта вместо 74.
- Новый `FirstRunHints` (вызов из `GameSession.Begin`): только не TestMode и `MetaProgress.Data.runs == 0`; плашка снизу по часам забега 0–6 / 10–16 / 20–26 с, вторая скрывается после первого рывка; без паузы, без лучей (CanvasGroup, без GraphicRaycaster), на экране карточек/паузы скрыта.
- Тексты `hints.move/dash/goals` (RU/EN), localization.cjs — 773 записи, 0 ошибок. Фразу «Зажми ЛКМ для фокуса цели» не добавили: фокуса по ЛКМ в коде нет (мышь в бою не читается).

## 2026-11-21 — Очистка Assets от неиспользуемых ассетов
- Аудит по GUID (rg по Assets/ProjectSettings, без .meta) и по именам/путям в .cs/tools: активный террейн — `Assets/Art/ArenaGreen/_Build/GreenArenaTerrain 11.asset` (guid 6994…8869, Terrain+Collider в GreenArenaPlay и GreenArena; ReliefTest его не использует). MossyRock17 используется ReliefTest.unity — оставлен.
- Удалено (с .meta, 47 файлов под git, ≈55,7 МБ): `GreenArenaTerrain.asset` и `GreenArenaTerrain 1–10.asset`, `Assets/New Terrain.asset`, 3 × `Assets/TerrainData_*.asset`, `Assets/TutorialInfo/` (на неё ссылался только Readme), `Assets/Readme.asset`, `Assets/scripts/playershoting.cs` (класс `PlayerShooting`, ссылок нет).
- По решению владельца удалены и `enemyspawner.cs` (`EnemySpawner`, Start был закомментирован) с `EnemySpawnData.cs`, и корневой объект «enemyspawner» в GreenArenaPlay.unity (GameObject/Transform/MonoBehaviour + запись в SceneRoots; правка YAML при закрытом Unity).
- Риск: `GreenArenaBuilder` создаёт террейн через `GenerateUniqueAssetPath`, каждый запуск снова добавит копию.

## 2026-11-21 — Проекты Мастерской после 2 забегов
- `WorkshopProjects.SectionUnlocked` (`SectionRuns` = 2): `runs >= 2`, или куплен этап любого проекта, или есть компонент `core`/`boss`/`research`.
- `WorkshopEquipmentMap`: до открытия карточки и узлы 5 проектов скрыты (`RefreshProjectsLock` из `WorkshopTree.Refresh`), в зоне проектов плашка «Projects lock»: ключ, «ПРОЕКТЫ МАСТЕРСКОЙ — откроются после 2 забегов (X/2)», подсказка и 2 деления прогресса. `FocusProject` при закрытом разделе ничего не делает, `FocusSection("projects")` ведёт камеру к плашке без выбора узла. Узлы создаются всегда (NodeCount прежний), открытие — при следующем `Refresh` (вход в Мастерскую).
- Тексты `projects.locked.title/hint` (RU/EN), localization.cjs — 770 записей, 0 ошибок.
- `WorkshopProjectsChecks`: `Checks()` проверяет закрытое состояние при 0 и 1 забеге, раннее открытие по компоненту и открытие на 2-м забеге; Layout/Equipment/EquipmentUi задают `runs = 2`.

## 2026-11-21 — Мастерская: без заглушек «временно закрыто»
- `WorkshopEquipmentMap.BuildEquipmentMap`: у оружия с `WeaponElements.IsLocked(i)` узлы веток не создаются; узел оружия по центру карточки (y 125 вместо 80). Код `WeaponElements` и ключи `element:*` в meta.json не тронуты: когда `HasBranches` вернёт true, узлы появятся сами.
- `WorkshopEquipmentPanel.RefreshEquipmentDetails`: для такого оружия скрыты «ВЕТКА ОРУЖИЯ», вкладки, описание, «Открыть/Улучшить» и «Установить» ветки; окно укорочено до блока открытия/усиления (`EquipmentShortShare` 0,585, страница растянута вниз, размеры текста прежние; `WorkshopPopup.PlacePopup`).
- `WorkshopTree.FocusSection/FocusProject`: при закреплённом окне переключают его на новый узел (`Pin`), раньше окно продолжало показывать прежнее оружие — ошибка переделки окна 18–19.11, ловилась `EquipmentChecks` («Project details did not return after weapon page»).
- `WorkshopProjectsChecks`: число узлов без веток у закрытых оружий, проверка состава каждой «Weapon group», блок веток скрыт у оружия 2 и виден у пистолета.

Старее: `docs/archive/CHANGELOG_2026-11-20-21-rollover.md`, `docs/archive/CHANGELOG_2026-11-20-rollover.md`, затем `CHANGELOG_2026-11-19-bosses-rollover.md`, `CHANGELOG_2026-11-19-cards-rollover.md`, `CHANGELOG_2026-11-19-late-rollover.md`.
