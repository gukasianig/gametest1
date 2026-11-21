# Запуск, проверки и известные проблемы

21.11.2026, быстрый старт опыта и подсказки первого забега (ProgressionSettings, Progression.asset, FirstRunHints, GameSession.Begin): компиляция Roslyn Assembly-CSharp — 0 ошибок; `node tools/localization.cjs` — 773 записи, 0 ошибок; batch CombatMechanicsChecks.RunBatch — exit 0, 18 PASS, 0 error CS. VerticalSliceChecks не запускали (берёт цену через ExperienceForLevel; падал раньше на устаревшем «Damage cap»). Не проверено в Play: вид/читаемость плашки, скрытие после рывка, уровень к 10-й минуте (ожидаемо +1–2 от прежних ~32–35).

21.11.2026, очистка Assets (≈55,7 МБ): batch-импорт `-batchmode -quit` — exit 0, без error CS/Missing; CombatMechanicsChecks.RunBatch (открывает GreenArenaPlay, бой) — exit 0, все PASS, без missing script в логе. После удаления объекта «enemyspawner» и его скриптов: импорт exit 0, CombatMechanicsChecks снова exit 0, все PASS, без missing script. GreenArena.unity и ReliefTest.unity не открывали. Удалённое восстанавливается из git до коммита.

21.11.2026, проекты Мастерской после 2 забегов (WorkshopProjects.SectionUnlocked, WorkshopEquipmentMap, WorkshopTree.Refresh/Focus*): batch WorkshopProjectsChecks.RunBatch — exit 0, 5 PASS, runtime errors=0 (новый раздел: плашка при 0/2 и 1/2, скрытые узлы/карточки, фокус игнорируется, ядро или 2 забега открывают); RunEquipmentBatch — exit 0, 8 PASS. Снимок Logs/workshop-projects-locked-ru.png — плашка в зоне проектов, заголовок в 2 строки. Не запускали RunEquipmentUiBatch (там только добавлен runs = 2). Живой переход «2-й забег → Мастерская» в Play не смотрели (покрыт только Refresh в тесте).

21.11.2026, Мастерская без заглушек веток (WorkshopEquipmentMap/Panel, WorkshopPopup, WorkshopTree.Focus*): batch WorkshopProjectsChecks.RunEquipmentBatch (включает Layout и Motion) — exit 0, 8 PASS, runtime errors=0. Снимок Logs/workshop-equipment-en-1024.png: у закрытых оружий один узел, окно Дробовика только с усилением; после снимка отступ окна изменён 0,57 → 0,585 (только константа, повторно не снимали). Не проверено: живая мышь; устаревшие WorkshopLayoutChecks (ждёт 54 узла) и WorkshopTreeChecks (69 узлов, `weapon:1:Frost`) уже не совпадали с картой и теперь ещё ищут удалённые `weapon:2:Frost` — не правили, не запускали.

21.11.2026, запрет карточки D06 (PlayerProgression, UpgradeCardFx, CombatMechanicsChecks): batch CombatMechanicsChecks.RunBatch (Unity закрыт, компиляция в нём без ошибок) — PASS разделы 1–8 и новые «Banish: 2 per run… replaced… no rank granted» и «Reroll… 5 level-ups closes the screen»; после привязки раздела 9 к каталогу повторный batch — exit 0, 18 PASS («Combat mechanics checks complete»), 0 исключений в логе (броня 14%, реген 1,25/с, вампиризм 1,5, дальность ×1,35, охотник ×1,4, рывок 2,8 с, опыт 4→5, удача 80/2000, орбиты ×1,6). `node tools/localization.cjs` — 768 записей, 0 ошибок. Не проверено: клавиша `B`, клики мышью, вид кнопок/рамок и длина подписей RU/EN в Play (batch не шлёт ввод); запрет в сундуке и кооперативе не сделан намеренно (первый этап).

21.11.2026, рецепт эволюции (PlayerProgression.RefreshCards/FeedsEvolution, RunStatistics.AppendEvolution): компиляция Roslyn Assembly-CSharp — 0 ошибок; `node tools/localization.cjs` — 764 записи, 0 ошибок. В Play не проверено: вмещается ли 4-я строка на карточке (Text без Best Fit, особенно кооп-окно и EN), длина строки рецепта в Tab, перевод составных строк на EN.

21.11.2026, клавиши 1/2/3/R окна карточек (PlayerProgression): компиляция Roslyn Assembly-CSharp и Assembly-CSharp-Editor — 0 ошибок; `node tools/localization.cjs` — 760 записей, 0 ошибок. Batch (Unity закрыт): VerticalSliceChecks — FAIL «Damage cap mismatch», CombatMechanicsChecks — FAIL «Crit chance 0,28»; оба известные устаревшие ожидания, новых падений нет. Нажатия клавиш в Play не проверены (batch не шлёт ввод). Риски: подпись «[1]   ЭВОЛЮЦИЯ ОРУЖИЯ» в узкой метке (0,68–0,96 ширины карточки) — не смотрели на EN/RU; по grep 1/2/3 больше нигде не читаются, `R` — только отладочный `OrbitManager` (enableDebugUpgrades, на паузе не срабатывает).

20.11.2026, пул пуль и FxPiece (bullet, AutoShoter.SpawnBullet/TintBullet, EnemyFx): компиляция Roslyn Assembly-CSharp и Assembly-CSharp-Editor (против свежей Assembly-CSharp), 0 ошибок. 21.11 batch (Unity закрыт): NewCardsChecks.RunBatch — OK, 30 PASS, 0 runtime errors (осколки разделения через пул: 3 по 35%, заряженный выстрел, стихии/цепь). CombatMechanicsChecks.RunBatch — FAIL до раздела пуль на устаревших ожиданиях: «Catalog size» 35 (исправлено на 50, строка 106), затем «Crit chance 0,28» (ожидает 0,2; баланс 19.11) — дальше не чинили, пробивание/рикошет не дошли. Обычный бой в Play не смотрели. По коду: проверки пробивания/рикошета создают пули напрямую (вне пула, путь Start→Launch, Destroy), осколки разделения идут через пул и активны для `FindObjectsOfType` (урон 35 выставляется после сброса). Риски: события физики отпущенной пули (выключение в OnTriggerEnter, повторная выдача только со следующего кадра); `IgnoreCollision` с прошлым владельцем может сохраняться (оба союзники); при >96 частиц старые гаснут раньше срока; `Ramp()` в Puff/Blobs всё ещё выделяет массив на вызов.

20.11.2026, H08 слияние кристаллов (ExperiencePickup + 7 вызовов с `merge: false`): только компиляция Roslyn Assembly-CSharp/Editor, 0 ошибок; в игре не проверено. Риски: вид фиолетового/золотого оттенка на модели XP Crystal (сдвиг тона по материалам FBX — не смотрели), заметность ×1,6 среди врагов, кристаллы у неподвижного игрока в радиусе притяжения не сливаются (летят).

20.11.2026, batch после правок целей/маршрутов (Unity закрыт): ArenaThemeChecks FAIL «Factory: obstacle count 612» (ожидание 10–36 устарело с переходом на Terrain), VerticalSliceChecks FAIL «Damage cap mismatch» (ожидает урон ×5, баланс 19.11 сделал +60%), HeroCombatChecks: выстрел по тестовой цели с EnemyAI прошёл, FAIL позже — «Weapon model or muzzle missing from right hand 5». Все три падения — устаревшие ожидания проверок; до NextWaypoint проверки не дошли, маршруты в Play не проверены.

20.11.2026, аллокации целей/маршрутов (AutoShoter, ArenaLayout, HeroCombatChecks): компиляция Roslyn Assembly-CSharp и Assembly-CSharp-Editor (против свежей Assembly-CSharp), 0 ошибок. VerticalSliceChecks/ArenaThemeChecks не запускались: Unity был открыт и занят (batch невозможен), а ArenaThemeChecks.Finish всегда вызывает EditorApplication.Exit. Запустить при закрытом Unity: `-executeMethod ArenaThemeChecks.RunBatch` и `VerticalSliceChecks.RunBatch` (+ HeroCombatChecks). Остаток: `foreach` по `EnemyAI.All` (IEnumerable над HashSet) упаковывает перечислитель — ~1 мелкая аллокация на вызов.

20.11.2026, вид боя: CombatLookShots.RunBatch (без -quit) — 2 кадра в Logs/combat-look-*.png; риски по кадрам: мелкие фигуры (чёрные ожоги исправлены — FxSplat, TelegraphFxChecks 8 PASS), полоски HP смещены от врагов (проверить в живом Play).

20.11.2026, палитры врагов и камера (0, 13, −4,333; сначала 15): CameraSpawnChecks.RunBatch (batchmode, без -quit) PASS, Logs/camera-spawn-check.txt. Палитры: `python tools/icons/enemy_palette_remap.py` (`--restore` — откат из Logs/enemy-palettes-backup/, Logs в git не попадает — оригиналы также в git). Не проверено: читаемость тёмных врагов на траве/в тени, вид кадра с новой высоты, хватает ли обзора для стрелков/снайперов (17 м) — на 16:9 по вертикали видно примерно ±10 м (оценка по кадру), снайпер сверху/снизу может стрелять из-за края экрана.

20.11.2026, шрифт UI (`GameUi.DefaultFont`): только компиляция Roslyn, 0 ошибок. Риск при подключении своего шрифта: другая ширина/высота строк может переполнить поля (Text без Best Fit), нужна проверка меню, HUD, карточек и Мастерской на RU и EN; шрифт без кириллицы покажет пустые квадраты.

20.11.2026, круги событий (`ArenaEvents.Flat`, прозрачные кольца): ArenaEventChecks.RunBatch (batchmode, без -quit) — 15 PASS, отчёт Logs/arena-event-results.txt. Не проверено: вид с игровой камеры (batch без графики) — яркость колец на траве, пересечение с прозрачными эффектами EnemyFx, сглаживание пунктира на мелких кругах засады (2,2 м).

19.11.2026, боссы: BossShowcaseChecks.RunBatch (без -quit) — отчёт Logs/boss-showcase.txt и лист Logs/boss-showcase.png (крупно в движении/во время атаки, игровая камера с полосой HP); HP в середине опускается ниже 50% для проверки второй фазы. Риски: баланс (HP ×5, урон ×1,9, у Громилы 2800); пятна GroundSplat с 20.11 полупрозрачные.

19.11.2026, турель-миниган: TurretMinigunChecks.RunBatch (без -quit) OK, 0 runtime errors, Logs/turret-minigun-check.txt и кадры turret-minigun.png. Сборка моделей: `blender.exe -b --factory-startup -P tools/blender/make_items.py -- Turret`, затем ItemModelInstall.RunBatch. Риск: части моделей не называть Leg*/Body/Carapace/Wing* (ModelWalkAnimator).

19.11.2026, анимации карточек/предметов/итогов: CardMotionChecks.RunBatch (без -quit) OK, 0 runtime errors, Logs/card-motion-check.txt + лист кадров Logs/card-motion-sheet.png. В batch время кадров неравномерно — кадры раздачи сняты с выставленным `age` карточек. Не проверено: наведение мышью, кооперативное окно, частота эффектов при сотнях кристаллов (искра ограничена 0,05 с), вид предметов вблизи.

19.11.2026, 18 новых карточек: NewCardsChecks.RunBatch (без -quit) OK — 26 PASS, 0 runtime errors, Logs/new-cards-check.txt. Проверено на манекенах (волны выключены): значения всех рангов, тексты/иконки 18/18, выпадение 15 видов за 300 окон, уклонение, снаряды 50→30, последний шанс 30/100, стихии (статус, замедление 0,66, цепь, тики), 3 осколка, импульс и откат, аура, щит рывка 0,86 с, взрывной рывок, трофей 2 Детали, азарт 0 аптечек/24 магнита, заряженный выстрел. Не проверены в бою: «Критический пробой» и «Горящая дробь» (только флаги), вид эффектов, баланс и полный забег. Иконки временные.

19.11.2026, Мастерская (всплывающее окно): WorkshopProjectsChecks.RunEquipmentUiBatch PASS/0 runtime errors (отчёт пишется в Logs/workshop-projects-results.txt). Риски: наведение/закрепление/Esc мышью в batch не проверены; при крупном zoom окно может перекрыть соседние карточки; WorkshopLayoutChecks устарел (ждёт 54 узла) и падает независимо от правки.

18.11.2026, поздний пик: LatePeakPerfChecks batch (волна 25, игрок стоит, timeScale 4, 2 прогона). Лимиты 103/206 подтверждены; пик врагов на поле 105–110 (≈лимит давления + элиты), 206 не достигается — враги копятся у неподвижного игрока. Время кадра в batch без окна недостоверно (0,8–1,2 мс, счётчик кадров не игровой) — FPS нужно мерить в окне: Tools/Checks/Late peak perf в открытом редакторе или билде. Не геймплейная проверка.

19.11.2026, LatePeakPerfChecks в окне редактора (после починки сброса timeScale картами): пик врагов 105 при лимитах 103/206; 807 кадров, среднее 12,4 мс (≈81 FPS), p95 14,3, p99 16,5, худший 22,6 мс. Игрок стоит, не стреляет, эффекты выстрелов/смертей не учтены — оценка нижняя для боя; проверить FPS в живом бою/билде и пик при беге игрока.

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
