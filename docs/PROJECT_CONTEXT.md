# Контекст проекта

Короткая сводка текущего состояния (держать ≤ 10 КБ, устаревшее заменять, а не дописывать). Полная прежняя версия — `docs/archive/PROJECT_CONTEXT_2026-11-09.md`; хронология — `CHANGELOG.md` (старое в `docs/archive/`), проверки и риски — `DEVELOPMENT.md`.

## Что за игра

Аренный шутер на выживание, вид сверху, ПК. Unity 2022.3.62f3, C#, URP 14.0.12. Цикл: главное меню → выбор оружия → бой 600 с (волны, опыт, карточки улучшений, события арены, Esc-пауза) → босс «Страж арены» → результаты → награда «Детали» → Мастерская (постоянные улучшения, стихии оружия) → новый забег. Владелец приглашает предлагать идеи; крупные изменения дизайна сначала обсуждать.

Единственная сцена и сцена билда — `Assets/Scenes/GreenArenaPlay.unity` (SampleScene удалена 09.11.2026, есть в истории Git). Запуск: `OpenUnity.cmd` → Play → «Старт» → оружие.

## Текущее состояние (на 09.11.2026)

- **Арена:** открытое поле зелёной арены (ассеты Codex в `Assets/Art/ArenaGreen`, Terrain 400×400, `ArenaLayout.halfSize` 200). Выбор темы скрыт; заводские инструменты (`ArenaTerrainSetup`/`ArenaPropsEditor`) больше не работают; ~20 автопроверок переведены на GreenArenaPlay, большинство на новой сцене ещё не прогонялись.
- **Враги:** 14 типов, модели D3 с анимацией коленей/челюсти (`ModelWalkAnimator`, `EnemyCorpse`, `EnemyFx`), до 32 живых, HP +2%/волну, плотность толпы `EnemyAI.CrowdPacking` 0,75.
- **Оружие:** 13 видов (4 обычных + 9 специальных), 10 моделей из Blender (`Assets/Art/Weapons`, установка `Tools > Install Blender Weapon Models`), иконки из `docs/art/Weapons/Icons` (WeaponIconBaker не запускать). Огнемёт и ракетница — свои модели в `docs/art/Weapons/`, ожидают установки в Unity (FBX → `Assets/Art/Weapons/`, `ModelSkinSet` Kind_Flame/Kind_Rocket, снаряд `Resources/Projectiles/rocket.fbx` с сохранением GUID). `WeaponFireAnimation` — отдача/дым.
- **Механики:** 34 карточки (крит, пробивание, рикошет, дроны/турели/мины — только улучшениями), переброс 2 за забег, рывок (Пробел/Shift), ремонтный набор и магнит.
- **События арены** (`ArenaEvents`, каждые 50–80 с): метеориты, магнитная буря, ремонтная зона, сброс припасов, удержание точки, засада, токсичные лужи (`AcidPuddle`), пожар (`GroundFireZone`), «Защити сундук» (35 с, награда — 3 особых улучшения `PlayerProgression.ChestUpgrades`). Эффекты событий и сундук — только компиляция.
- **UI:** `CombatHud` (панель ХП/брони из Blender, таймер, баннер событий), меню с фоном из Blender (`MenuBackdrop`, `MenuIntro`, `MenuButtonFx`), `WeaponSelectScreen` (сетка 13 плиток), экраны «Статистика», «Испытания» (`ChallengeScreen`), «Настройки» (`GameSettings`, PlayerPrefs), «Тест-арена» (`TestArena`), кнопка «Выход».
- **Мета:** `MetaProgress` (`meta.json`, рекорды, открытия), `WorkshopTree` (ветки «ГЕРОЙ»/«ОРУЖИЕ», оружие 5×2), `WeaponElements` + `ElementStatus` (горение/яд).
- **Звук:** `GameAudio`, музыка меню/боя и три записи выстрелов в `Assets/Audio/Resources/RecordedAudio/`; подробности — `docs/AUDIO.md`.
- **Билд:** `BuildTools.BuildWindows` → `Builds/Windows`, версия +1 при каждой сборке (0.1.x). Пересобирать только по просьбе владельца.
- **Git:** ветка `event-guard-chest`. GreenArenaPlay и `Assets/Art/ArenaGreen` в Git; в рабочей копии много изменённых бинарных файлов (`docs/art`, аудио) — перед коммитом смотреть `git status`.

## Ждёт проверки (кратко; детали в DEVELOPMENT.md)

Play Mode: «Защити сундук» и эффекты событий (`ArenaEventChecks.RunBatch`), экраны «Статистика»/«Испытания», анимация меню (`CombatHudChecks`), тест-арена, настройки; остальные автопроверки на GreenArenaPlay. Глазами владельца: бой с новыми моделями оружия, анимации врагов с игровой камеры, баланс дальностей/толпы/HP, запуск exe.

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

Общение на русском. Небольшие шаги; Unity-проверки короткие и одна на завершённую систему; пройденные проверки не повторять; полный аудит — только по просьбе. Новые 3D-модели самому не генерировать (их делает Codex), кроме явного разрешения. Билд — только по просьбе. Платформа — ПК, мобильное не предлагать.

Планы: `MASTER_PLAN.md` (сводка + «Ближайшие шаги»), `GAME_ROADMAP.md` («Ближайшая очередь» + нужный раздел), `UPGRADE_ROADMAP.md`, `VISUAL_ASSETS.md`, `IDEAS.md` — читать только нужные разделы.
