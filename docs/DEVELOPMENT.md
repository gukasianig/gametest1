# Запуск, проверки и известные проблемы

24.11.2026, чистка и подготовка к плейтесту (IsElite, retired-разблокировки, dev-флаг оружий, сундук с элиты, UI карточек/рывка): компиляция Roslyn Assembly-CSharp — 0 ошибок; batch CombatHudChecks — PASS (меню, панель, баннер, карточки, тост); CombatMechanicsChecks — PASS (кап урона ×5 с проклятием, элита-охотник). MetaProgressChecks — FAIL на MetaProgressChecks.cs:273 («Horde effect on alive limit/interval», жёсткие 9 и 0,75² — устаревшее ожидание баланса волн, к правкам не относится; не исправлено). ArenaTheme/HeroCombat после правки ожиданий не прогонялись. Визуально карточки «★ ЭВОЛЮЦИЯ», `[R]`/`[B]` и рывок в Play не смотрели.

22.11.2026, HUD/опыт/кап урона/тексты: batch (Unity 2022.3.62f3) — CombatHudChecks, ArenaEventChecks (14 PASS), MiniBossChecks, BossShowcaseChecks (failures=0), CombatMechanicsChecks (+новая проверка капа: со «Стеклянной пушкой» и chest_rage множитель ×5, карточка Damage не предлагается), NewCardsChecks (failures=0), LocalizationChecks (773 записи) — PASS; `node tools/localization.cjs` — 0 ошибок. Пороги опыта посчитаны по формуле, в игре не проходились. Кадры: Logs/menu-weapons.png, combat-hud-event.png, mini-boss.png, workshop-layout-1600.png. WorkshopLayoutChecks: ожидания обновлены (25 узлов, одиночные узлы оружия, пропуск блока стихий), отсутствие наложений подписей и клиппинга на 1600/1024/2560 — PASS, но проверка падает дальше (NRE на ~строке 168, старые ожидания после 21.11) — не доделана. Не смотрели вживую: тонкие полоски врагов/элит/танков в бою, анонс события вместе с полосой босса.

21.11.2026, звуки и музыка (GameAudio, UiSound, BossController, AudioChecks): компиляция Roslyn — 0 ошибок; `AudioChecks.RunPlayBatch` — exit 0, все PASS (музыка меню → забег → босс → забег, 26 эффектов без ошибок, Pickup/LevelUp/HeroDash/Chest/Ui* из файлов). Не проверено: как звучит (выбор по названиям), громкость нового GameMusic против прежнего, стык петли MP3 босса, звуки кнопок в живом меню. Риск: `AudioSourcePrep.Build` пересоздаёт `Sfx/` — новые файлы внесены в его карту.

21.11.2026, шрифт GameFont (GameUi.DefaultFont, импорт .meta, FontChecks): FontChecks.RunBatch — exit 0, Resources.Load = GameFont (не LegacyRuntime), латиница 94/94, кириллица 66/66, символы UI 22/22 получают глиф. `HasCharacter` у динамического шрифта учитывает fallback, поэтому отсутствие → ★ ● ○ и т.п. в самом Jura установлено разбором cmap (Python). Компиляция Roslyn — 0 ошибок; CombatHudChecks — exit 0, 6 PASS, кадр Logs/combat-hud-cards.png: кириллица Jura, → ● ○ ★ видны; WorkshopProjectsChecks.RunEquipmentBatch — exit 0, runtime errors=0; ни в одном логе нет исключений игры. Риск: символы берутся из шрифтов Windows — на ПК без Segoe UI Symbol возможны квадраты; в собранном билде не смотрели.

21.11.2026, обновлены CombatHudChecks и WeaponChecks (только тесты): CombatHudChecks.RunBatch — exit 0, 6 PASS (меню, панель, баннер, 3 карточки, тост); WeaponChecks.RunBatch — exit 0, 13 PASS, поворот 5,7° после попадания, вращение погашено. Первый прогон WeaponChecks после правки упал на «Hero keeps spinning 24,5» — тест сам отключал PlayMovement; исправлено в тесте.

21.11.2026, меню 6А/6Б (StartMenu, WeaponSelectScreen, GameSession пауза/результаты, UiFocus/UiFocusFrame/SelectRelay, MenuButtonFx, WorkshopPopup): компиляция Roslyn — 0 ошибок. Batch: WorkshopProjectsChecks RunBatch и RunEquipmentBatch — exit 0, 8 PASS; CoopChecks — сначала FAIL «Coop button did not select 2 players» (замок кооператива), после открытия замка в проверке — exit 0, 10 PASS (меню, выбор оружия по очереди, бой). Устаревшие и не связанные с правкой: CombatHudChecks — 3 PASS (меню, панель, баннер), затем FAIL «No upgrade cards found» (ищет русские `<size=27>`/«ранг» на карточках, карточки и язык по умолчанию сменились); WeaponChecks — 11 PASS, затем FAIL «Hero does not face the target 163°» (с героем v3 выстрел по анимации PoseAiming, корень не обязан смотреть на цель) — не правили. Не проверено: клавиатура/геймпад вживую (batch не шлёт ввод), вид рамки фокуса, F10 в собранном билде.

21.11.2026, VerticalSliceChecks (только ожидания теста + `BossController.StartingHealth`): batch RunBatch — exit 0, все разделы PASS, включая soak 300 с (пик 113 живых, 58 спасений бота, Fast/Tank). По пути 5 прогонов: падали потолок урона, очередь уровней (радиус без гранат), остановка волн, случайный цикл атак, затем 1 раз нестабильно предупреждение о боссе — каждое исправлено в тесте.

21.11.2026, фокус по ЛКМ (AutoShoter, hints.move, CombatMechanicsChecks): компиляция Roslyn — 0 ошибок; localization.cjs — 773 записи, 0 ошибок; CombatMechanicsChecks первый прогон — FAIL «Elite hunter damage 42 (expected 28)» (случайный крит от удачи, не от фокуса), после фикса проверки — exit 0, 18 PASS. Сам фокус batch не проверяет (нет мыши): в Play посмотреть выбор цели, кольцо на склонах, Dual/спецоружие, клики по карточкам не дают фокус после закрытия окна.

21.11.2026, телеметрия playtest_runs.csv (RunStatistics.AppendPlaytestRow, GameSession.Finish): компиляция Roslyn Assembly-CSharp — 0 ошибок. Файл в batch не пишется намеренно, поэтому реальная запись не проверена: первый живой забег — открыть `%USERPROFILE%/AppData/LocalLow/<компания>/<продукт>/playtest_runs.csv`, сверить колонки. Риск: Wave — счётчик `WaveManager.wave` (не время), в бесконечном режиме Threat/EndlessRound не проверены.

21.11.2026, ритм боссов (BossController.recoveryBonus/HeavyRecovery, BossQueen рой 3×6/8 + 2 с, BossSniper, MiniBoss.DoubleSlamRecovery, EnemyFx.ScorchColor/FxSplat, ArenaEvents метеорит): компиляция Roslyn — 0 ошибок; batch BossVariantChecks — exit 0, все ПРОЙДЕНО (рой 18 врагов, кислота, снайпер); MiniBossChecks — exit 0, все ПРОЙДЕНО; BossShowcaseChecks — failures=0, runtime errors=0 (снайпер 7 атак за 18 с, Громила 4). Не проверено: ощущение ритма и FPS в живом бою, цвет ожогов на траве (кадры не смотрели); у Королевы «model bottom vs ground max 5,21 m» — вероятно прыжок, не связано с правкой.

21.11.2026, быстрый старт опыта и подсказки первого забега (ProgressionSettings, Progression.asset, FirstRunHints, GameSession.Begin): компиляция Roslyn Assembly-CSharp — 0 ошибок; `node tools/localization.cjs` — 773 записи, 0 ошибок; batch CombatMechanicsChecks.RunBatch — exit 0, 18 PASS, 0 error CS. VerticalSliceChecks не запускали (берёт цену через ExperienceForLevel; падал раньше на устаревшем «Damage cap»). Не проверено в Play: вид/читаемость плашки, скрытие после рывка, уровень к 10-й минуте (ожидаемо +1–2 от прежних ~32–35).

21.11.2026, очистка Assets (≈55,7 МБ): batch-импорт `-batchmode -quit` — exit 0, без error CS/Missing; CombatMechanicsChecks.RunBatch (открывает GreenArenaPlay, бой) — exit 0, все PASS, без missing script в логе. После удаления объекта «enemyspawner» и его скриптов: импорт exit 0, CombatMechanicsChecks снова exit 0, все PASS, без missing script. GreenArena.unity и ReliefTest.unity не открывали. Удалённое восстанавливается из git до коммита.

21.11.2026, проекты Мастерской после 2 забегов (WorkshopProjects.SectionUnlocked, WorkshopEquipmentMap, WorkshopTree.Refresh/Focus*): batch WorkshopProjectsChecks.RunBatch — exit 0, 5 PASS, runtime errors=0 (новый раздел: плашка при 0/2 и 1/2, скрытые узлы/карточки, фокус игнорируется, ядро или 2 забега открывают); RunEquipmentBatch — exit 0, 8 PASS. Снимок Logs/workshop-projects-locked-ru.png — плашка в зоне проектов, заголовок в 2 строки. Не запускали RunEquipmentUiBatch (там только добавлен runs = 2). Живой переход «2-й забег → Мастерская» в Play не смотрели (покрыт только Refresh в тесте).

21.11.2026, Мастерская без заглушек веток (WorkshopEquipmentMap/Panel, WorkshopPopup, WorkshopTree.Focus*): batch WorkshopProjectsChecks.RunEquipmentBatch (включает Layout и Motion) — exit 0, 8 PASS, runtime errors=0. Снимок Logs/workshop-equipment-en-1024.png: у закрытых оружий один узел, окно Дробовика только с усилением; после снимка отступ окна изменён 0,57 → 0,585 (только константа, повторно не снимали). Не проверено: живая мышь; устаревшие WorkshopLayoutChecks (ждёт 54 узла) и WorkshopTreeChecks (69 узлов, `weapon:1:Frost`) уже не совпадали с картой и теперь ещё ищут удалённые `weapon:2:Frost` — не правили, не запускали.

21.11.2026, запрет карточки D06 (PlayerProgression, UpgradeCardFx, CombatMechanicsChecks): batch CombatMechanicsChecks.RunBatch (Unity закрыт, компиляция в нём без ошибок) — PASS разделы 1–8 и новые «Banish: 2 per run… replaced… no rank granted» и «Reroll… 5 level-ups closes the screen»; после привязки раздела 9 к каталогу повторный batch — exit 0, 18 PASS («Combat mechanics checks complete»), 0 исключений в логе (броня 14%, реген 1,25/с, вампиризм 1,5, дальность ×1,35, охотник ×1,4, рывок 2,8 с, опыт 4→5, удача 80/2000, орбиты ×1,6). `node tools/localization.cjs` — 768 записей, 0 ошибок. Не проверено: клавиша `B`, клики мышью, вид кнопок/рамок и длина подписей RU/EN в Play (batch не шлёт ввод); запрет в сундуке и кооперативе не сделан намеренно (первый этап).

21.11.2026, рецепт эволюции (PlayerProgression.RefreshCards/FeedsEvolution, RunStatistics.AppendEvolution): компиляция Roslyn Assembly-CSharp — 0 ошибок; `node tools/localization.cjs` — 764 записи, 0 ошибок. В Play не проверено: вмещается ли 4-я строка на карточке (Text без Best Fit, особенно кооп-окно и EN), длина строки рецепта в Tab, перевод составных строк на EN.

21.11.2026, клавиши 1/2/3/R окна карточек (PlayerProgression): компиляция Roslyn Assembly-CSharp и Assembly-CSharp-Editor — 0 ошибок; `node tools/localization.cjs` — 760 записей, 0 ошибок. Batch (Unity закрыт): VerticalSliceChecks — FAIL «Damage cap mismatch», CombatMechanicsChecks — FAIL «Crit chance 0,28»; оба известные устаревшие ожидания, новых падений нет. Нажатия клавиш в Play не проверены (batch не шлёт ввод). Риски: подпись «[1]   ЭВОЛЮЦИЯ ОРУЖИЯ» в узкой метке (0,68–0,96 ширины карточки) — не смотрели на EN/RU; по grep 1/2/3 больше нигде не читаются, `R` — только отладочный `OrbitManager` (enableDebugUpgrades, на паузе не срабатывает).

20.11.2026, пул пуль и FxPiece (bullet, AutoShoter.SpawnBullet/TintBullet, EnemyFx): компиляция Roslyn Assembly-CSharp и Assembly-CSharp-Editor (против свежей Assembly-CSharp), 0 ошибок. 21.11 batch (Unity закрыт): NewCardsChecks.RunBatch — OK, 30 PASS, 0 runtime errors (осколки разделения через пул: 3 по 35%, заряженный выстрел, стихии/цепь). CombatMechanicsChecks.RunBatch — FAIL до раздела пуль на устаревших ожиданиях: «Catalog size» 35 (исправлено на 50, строка 106), затем «Crit chance 0,28» (ожидает 0,2; баланс 19.11) — дальше не чинили, пробивание/рикошет не дошли. Обычный бой в Play не смотрели. По коду: проверки пробивания/рикошета создают пули напрямую (вне пула, путь Start→Launch, Destroy), осколки разделения идут через пул и активны для `FindObjectsOfType` (урон 35 выставляется после сброса). Риски: события физики отпущенной пули (выключение в OnTriggerEnter, повторная выдача только со следующего кадра); `IgnoreCollision` с прошлым владельцем может сохраняться (оба союзники); при >96 частиц старые гаснут раньше срока; `Ramp()` в Puff/Blobs всё ещё выделяет массив на вызов.

20.11.2026, H08 слияние кристаллов (ExperiencePickup + 7 вызовов с `merge: false`): только компиляция Roslyn Assembly-CSharp/Editor, 0 ошибок; в игре не проверено. Риски: вид фиолетового/золотого оттенка на модели XP Crystal (сдвиг тона по материалам FBX — не смотрели), заметность ×1,6 среди врагов, кристаллы у неподвижного игрока в радиусе притяжения не сливаются (летят).

Проверки 19–20.11 (цели/маршруты, вид боя, палитры, шрифт UI до GameFont, круги событий, боссы, турель) — `docs/archive/DEVELOPMENT_2026-11-20-rollover.md`.

Проверки 18–19.11 (карточки, предметы, Мастерская, поздний пик/FPS) — `docs/archive/DEVELOPMENT_2026-11-19-late-rollover.md`.

Записи 18.11 (Мастерская: платформы, анимации, раскладка, проекты) — `docs/archive/DEVELOPMENT_2026-11-18-workshop2-rollover.md`.

Записи 17.11 — `docs/archive/DEVELOPMENT_2026-11-17-late-rollover.md`.











08–11.11.2026: более ранние записи (синергия «Стальная воля», элиты «Взрывной», угроза/испытания, «Защити сундук», пули, FPS/склоны, дальность/огнемёт, круги/мортира, толпа, билд 0.1.1, сцена/мастерская, «Статистика», «Испытания», меню, GreenPack) — `docs/archive/DEVELOPMENT_2026-11-early.md`.

Проверки бесконечного режима и вариантов боссов от 12.11 — `docs/archive/DEVELOPMENT_2026-11-15-results-rollover.md`.

Проверки 12.11 — `docs/archive/DEVELOPMENT_2026-11-16-hero-overhaul-rollover.md`.

Старые проверки — `docs/archive/DEVELOPMENT_2026-11-16-visual-v2-rollover.md`.

Старые проверки — `docs/archive/DEVELOPMENT_2026-11-17-hero-smooth-rollover.md`.

## Среда

- Репозиторий `F:\test game`; Unity `C:\Program Files\Unity 2022.3.62f3\Editor\Unity.exe` (changeset 96770f904ca7); .NET SDK 8.0.427; VS Code в `C:\Users\grisha\AppData\Local\Programs\Microsoft VS Code`. Unity Hub — MSIX, пути `Program Files` не предполагать. Лицензию проверять только при ошибке запуска; идентификаторы не записывать.
- Git: remote `origin` = github.com/gukasianig/gametest1 (пуш делает владелец через GitHub Desktop: у ассистента нет учётных данных). При «dubious ownership» использовать `git -c safe.directory='F:/test game' …`; глобальный конфиг не менять.
- Python launcher не работает; Node.js доступен. `Library`, `Temp`, `Logs`, `.csproj`, `.sln` генерируются и игнорируются.

## Запуск

`OpenUnity.cmd` (открывает проект и GreenArenaPlay) или `Unity.exe -projectPath 'F:\test game' -openfile 'F:\test game\Assets\Scenes\GreenArenaPlay.unity'`. Play → «Старт» → оружие. Управление: WASD/стрелки, колёсико — зум, Tab — статистика, Esc — пауза; стрельба автоматическая.


История: `docs/archive/DEVELOPMENT_2026-11-18-motion-rollover.md`, `docs/archive/DEVELOPMENT_2026-11-19-rollover.md`.
