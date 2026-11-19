# Контекст проекта

Сводка состояния (≤ 10 КБ). История — `CHANGELOG.md` и `docs/archive/`, проверки и риски — `DEVELOPMENT.md`.

## Что за игра

Аренный шутер на выживание, вид сверху, ПК. Unity 2022.3.62f3, C#, URP 14.0.12. Цикл: главное меню → выбор оружия → бой 600 с (волны, опыт, карточки улучшений, события арены, Esc-пауза) → босс «Страж арены» → результаты → награда «Детали» → Мастерская (постоянные улучшения, стихии оружия) → новый забег. Владелец приглашает предлагать идеи; крупные изменения дизайна сначала обсуждать.

Сцена и билд — `Assets/Scenes/GreenArenaPlay.unity`. Запуск: `OpenUnity.cmd` → Play → «Старт» → оружие.

## Текущее состояние (на 18.11.2026)

- **Арена:** открытое поле зелёной арены (ассеты Codex в `Assets/Art/ArenaGreen`, Terrain 400×400). Выбор темы скрыт; `ArenaTerrainSetup`/`ArenaPropsEditor` открывают GreenArenaPlay, но это заводские инструменты; часть автопроверок на GreenArenaPlay не прогонялась.
- **Враги:** 14 типов, модели D3 с анимацией коленей/челюсти (`ModelWalkAnimator`, `EnemyCorpse`, `EnemyFx`), поздние пределы 103 в зоне давления / 206 всего без модификаторов (пик на поле ≈105–110, 18.11; FPS в окне не мерили — Tools/Checks/Late peak perf), HP +2%/волну, плотность толпы `EnemyAI.CrowdPacking` 0,75.
- **Герой/оружие:** 13 видов; Оператор v3 — 7882 треугольника, 11 клипов; темп ног исправлен до 1–1,35× (17.11), HeroSmoothChecks/HeroCadenceChecks PASS; HERO_VISUAL_V3.md. Способности — HERO_UPGRADE_PLAN.md. WeaponIconBaker не запускать.
- **Боссы:** мини-босс «Громила» на 5-й минуте (`MiniBoss`); финальный — случайно Страж, Королева роя или Грозовой снайпер (`BossController.Kind`).
- **Бесконечный режим** (`GameSession.EndlessMode`): после первой победы, круги по 5 мин с боссами, рекорды в «Статистике».
- **Эволюция оружия** (`AutoShoter.Evolutions`, `ReadyEvolution`): все 10 оружий, карточка только из сундука при «Урон пуль» 5 + парной карточке; золотое свечение и пули.
- **Ощущение от боя** (11.11, `CombatFeel`): стоп-кадр на элите, смерть Стража в замедлении, красная рамка при низком HP, стрелки к элитам, новые звуки.
- **Механики:** 34 карточки (крит, пробивание, рикошет, дроны/турели/мины — только улучшениями), переброс 2 за забег, рывок (Пробел/Shift), ремонтный набор и магнит.
- **Предметы:** 16 моделей (`make_items.py`), анимации `ItemModelParts`.
- **События арены:** `ArenaEvents` каждые 50–80 с: метеориты, бури, лечение, припасы, удержание, засада, кислота, огонь, защита сундука (35 с, 3 особых улучшения). Подробные проверки — DEVELOPMENT.md.
- **Мастерская (18.11):** карта без линий, платформы/карточки, герой 2×2/оружие 3 колонки/проекты 3+2; обзор/zoom/мышь. 5 проектов/15 этапов (Q/E покупкой); усиление 10 оружий: 5×+10% базового урона, отдельно от веток. Meta v1 сохранён; Play/UI PASS/0 ошибок, RU/EN/3 формата. Баланс и живая мышь — приёмка.
- **Долгая игра:** ENDGAME_PLAN.md; 9 испытаний, рекорд угрозы, взрывные элиты, малый сундук и «Стальная воля». Базовые реликвии — D15; предложения — REVIEW_2026-11-15.md.
- **Кооператив** (`COOP_PLAN.md`): этапы 1–4 готовы, 5–6 отложены. `CoopMode.Unlocked` (PlayerPrefs `CoopUnlocked`) по умолчанию закрыт, F9 переключает.
- **Результаты (15.11):** RunReport/RunResultsView — сборка, HP по источникам, время боссов, RU/EN. RunResultsChecks PASS, кадры просмотрены; далее баланс полного забега.
- **HUD:** XP «С делениями», Q/E, таймер со шкалой до босса (30/10 с); целевые проверки PASS, кадры RU/EN/coop просмотрены. B08 готов в указанном объёме; A08/K05 частичные. TestArena-сообщение ждёт визуальной приёмки.
- **Языки:** RU/EN, `GameText`/`LocalizedUiText`, JSON-каталог и генератор `tools/localization.cjs`. Правила — AGENTS.md; K05: остальные экраны/разрешения ещё ждут приёмки.
- **Мета:** MetaProgress/meta.json, WorkshopTree, WeaponElements. Пистолет: Револьвер/Двойные/Липкий; автомат: Штурмовой/Роторный пулемёт/Тяжёлый, свои модели/иконки (RIFLE_BRANCHES.md, RifleBranchChecks PASS). Остальные оружия закрыты ключом; полный баланс веток не проверен.
- **Звук:** `GameAudio` (синтез + записи), см. `docs/AUDIO.md`.
- **Билд:** `BuildTools.BuildWindows` → `Builds/Windows` (0.1.x), только по просьбе владельца.
- **Git:** ветка `event-guard-chest`; много прежних изменений кода/арта. Сохранять их, перед коммитом смотреть status.

## Ждёт проверки (кратко; детали в DEVELOPMENT.md)

Пистолет/Двойные: PistolBranchChecks PASS/0 ошибок, кадры хватов/результатов просмотрены. Полный баланс веток и забега ещё ждёт приёмки; числа/проверки — CHANGELOG/DEVELOPMENT.

Иконки E: 37 PNG, UpgradeArtworkChecks PASS/кадр 1280×720 просмотрен; GUID сохранены. Исходники — `docs/art/Upgrades/StyleE`; приёмка/другие размеры — A08.

## Карта кода (`Assets/scripts/`)

| Механика | Файлы |
| --- | --- |
| Движение и камера | `PlayMovement`, `TopDownCamera` |
| Оружие игрока | `AutoShoter`, `WeaponData`, `WeaponTargets`, `SpecialWeapons`, `bullet`, `Grenade`, `OrbitManager`/`OrbitWeapon`, `WeaponFireAnimation`; `playershoting` (ручная, не основной путь) |
| Меню и забег | `StartMenu`, `WeaponSelectScreen`, `ChallengeScreen`, `WorkshopTree`, `MetaProgress`, `WeaponElements`, `ElementStatus`, `GameSession`, `GameUi`, `CombatHud`, `GameSettings`, `TestArena`, `RunStatistics` (Tab) |
| Враги | `EnemyAI`, `EnemyAbility`, `EnemyProjectile` (+`MortarShell`), `ModelWalkAnimator`, `EnemyCorpse`, `EnemyFx`, `Health`, `PlayerHealth`, `EnemyHealthDisplay`, `HealthBarFx` |
| Босс | `BossController` |
| Волны и спавн | `WaveManager`, `WaveSettings`, `OffscreenSpawn`; старый `enemyspawner` не используется |
| Арена и события | `ArenaLayout`, `ArenaEvents`, `GroundFireZone`, `GridDrawer` |
| Опыт и улучшения | `PlayerProgression`, `ExperiencePickup`, `ProgressionSettings`, `UpgradeCatalog`, `UpgradeIcon`, `UpgradeCardFx`, `UiArtwork`, `DropPickup` |
| Модели | `ModelSkinSet`/`ModelSkins`; Editor-проверки и сборщики — `Assets/Editor/` |
| Контракты | `Interfaces/IDamagable`, `IProjectile`, `Damage` |

## Подводные камни Unity

- Inspector-данные сцены/префаба переопределяют значения по умолчанию в C#; проверять их перед правкой логики.
- Открытая в редакторе сцена держится в памяти: после внешней правки перезагрузить её; Assets → Refresh не помогает.
- Batch-проверки не запускать при открытом Unity владельца; компиляцию вне редактора можно проверить Roslyn по rsp из `Library/Bee`.
- Нельзя выключать весь объект орбитального оружия внутри его корутины.
- Сохранять `.meta` и GUID; массово не переименовывать (`playershoting`, `Bilboard`, `AutoShoter`).
- У `weapons` в сцене `range = 0` — fallback на дальность стрелка, не удалять без миграции.

## Предпочтения владельца

Общение на русском, небольшие шаги, одна короткая Unity-проверка на систему; полный аудит и билд — по просьбе. Новые 3D-модели самому не генерировать без разрешения. Платформа — ПК.

Планы: `MASTER_PLAN.md`, `GAME_ROADMAP.md`, `UPGRADE_ROADMAP.md`, `COOP_PLAN.md`, `VISUAL_ASSETS.md`, `IDEAS.md` — читать только нужные разделы.
