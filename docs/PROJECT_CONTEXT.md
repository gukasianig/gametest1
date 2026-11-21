# Контекст проекта

Сводка состояния (≤ 10 КБ). История — `CHANGELOG.md` и `docs/archive/`, проверки и риски — `DEVELOPMENT.md`.

## Что за игра

Аренный шутер на выживание, вид сверху, ПК. Unity 2022.3.62f3, C#, URP 14.0.12. Цикл: меню → оружие → бой 600 с (волны, опыт, карточки, события, Esc-пауза) → босс → результаты → «Детали» → Мастерская → новый забег. Владелец ждёт идей; крупные изменения дизайна сначала обсуждать.

Сцена и билд — `Assets/Scenes/GreenArenaPlay.unity`. Запуск: `OpenUnity.cmd` → Play → «Старт» → оружие.

## Текущее состояние (на 18.11.2026)

- **Арена:** открытое поле зелёной арены (ассеты Codex в `Assets/Art/ArenaGreen`, Terrain 400×400). Выбор темы скрыт; часть автопроверок на GreenArenaPlay не прогонялась.
- **Враги:** 14 типов, модели D3 (`ModelWalkAnimator`, `EnemyCorpse`, `EnemyFx`), пределы 103 в зоне давления / 206 всего (пик ≈105–110), HP +2%/волну, `EnemyAI.CrowdPacking` 0,75.
- **Герой/оружие:** 13 видов; Оператор v3 — 7882 треугольника, 11 клипов; темп ног исправлен до 1–1,35× (17.11), HeroSmoothChecks/HeroCadenceChecks PASS; HERO_VISUAL_V3.md. Способности — HERO_UPGRADE_PLAN.md. WeaponIconBaker не запускать.
- **Боссы (19.11):** мини-босс Громила на 5-й минуте (`MiniBoss`, 5 атак); финальный — Страж, Королева роя или Грозовой снайпер (`BossController` + BossQueen/BossSniper, по 5–6 атак, вторая фаза <50%, поворот к герою, позы атак). Полоса HP — `BossHealthBar`. BossShowcaseChecks OK; баланс ждёт забега.
- **Бесконечный режим** (`GameSession.EndlessMode`): после первой победы, круги по 5 мин с боссами, рекорды в «Статистике».
- **Эволюция оружия** (`AutoShoter.Evolutions`, `ReadyEvolution`): все 10 оружий, карточка только из сундука при «Урон пуль» 5 + парной карточке; золотое свечение и пули. Рецепт виден (21.11): «★ ЭВОЛЮЦИЯ» на нужных карточках, прогресс в Tab; в Play не смотрели.
- **Ощущение от боя** (11.11, `CombatFeel`): стоп-кадр на элите, смерть Стража в замедлении, красная рамка при низком HP, стрелки к элитам, новые звуки.
- **Механики:** 52 карточки (19.11 +18: стихийные пули, защита, импульсы, аура, заряд, особые, 3 связки — `HeroCardEffects`, NewCardsChecks PASS; иконки временные (промпты в tools/icons)), переброс 2 и запрет 2 за забег (1/2/3/R/B; запрет D06 — только обычное окно, в Play не смотрели), рывок, ремонтный набор и магнит.
- **Предметы:** 16 моделей (`make_items.py`; турель-миниган 19.11), анимации `ItemModelParts`, подскок/кольца/эффекты подбора. Карточки компактные (`UpgradeCardFx`, CardMotionChecks OK), итоги — `UiReveal`.
- **События арены:** `ArenaEvents` каждые 50–80 с: метеориты, бури, лечение, припасы, удержание, засада, кислота, огонь, защита сундука (35 с, 3 особых улучшения); круги — прозрачные кольца-маркеры (20.11).
- **Мастерская (18–19.11):** дерево на весь экран, всплывающие подробности (`WorkshopPopup`); 5 проектов/15 этапов (Q/E покупкой; раздел — после 2 забегов), усиление 10 оружий 5×+10%; ветки только у Пистолета/Автомата. UI PASS; живая мышь и баланс — приёмка владельцем.
- **Долгая игра:** ENDGAME_PLAN.md; 9 испытаний, рекорд угрозы, взрывные элиты, малый сундук, «Стальная воля».
- **Кооператив** (`COOP_PLAN.md`): этапы 1–4 готовы, 5–6 отложены. `CoopMode.Unlocked` (PlayerPrefs `CoopUnlocked`) по умолчанию закрыт, F9 переключает.
- **Баланс забега (19.11):** обычные карточки слабее/редкие сильнее, веса 60/32/8, анти-повтор, удача на редкость/дроп/сундук/крит, аптечки 1,2% (элита 35%), опыт: уровни 1–3 по 6/10/16, дальше 12+10·n^1,45 (21.11). Подсказки первого забега — `FirstRunHints` (B07 частично). Ждёт забега владельца.
- **Результаты (15.11):** RunReport/RunResultsView — сборка, HP по источникам, время боссов, RU/EN; RunResultsChecks PASS.
- **HUD:** XP «С делениями», Q/E, таймер со шкалой до босса; полоса HP босса — BossHealthBar (19.11). A08/K05 частичные.
- **Языки:** RU/EN, `GameText`/`LocalizedUiText`, JSON-каталог и генератор `tools/localization.cjs`. Правила — AGENTS.md; K05: остальные экраны/разрешения ещё ждут приёмки.
- **Мета:** MetaProgress/meta.json, WorkshopTree, WeaponElements. Пистолет: Револьвер/Двойные/Липкий; автомат: Штурмовой/Роторный пулемёт/Тяжёлый, свои модели/иконки (RIFLE_BRANCHES.md, RifleBranchChecks PASS). Остальные оружия закрыты ключом; полный баланс веток не проверен.
- **Звук:** `GameAudio` (синтез + записи), см. `docs/AUDIO.md`.
- **Билд:** `BuildTools.BuildWindows` → `Builds/Windows` (сейчас 0.1.6), только по просьбе владельца.
- **Git:** ветка `event-guard-chest`; много прежних изменений кода/арта. Сохранять их, перед коммитом смотреть status.

## Ждёт проверки (кратко; детали в DEVELOPMENT.md)

Цели AutoShoter, буферы NextWaypoint, H08, пул пуль/FxPiece (`Bullet.Spawn/Release/Launch`, `EnemyFx.MaxPieces` 96) — 20.11, NewCardsChecks OK; ArenaTheme/VerticalSlice/HeroCombat падают на устаревших ожиданиях; CombatMechanicsChecks — полностью PASS с 21.11 (ожидания привязаны к каталогу).

Иконки E: 37 PNG + 18 временных новых карточек; приёмка — A08.

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
