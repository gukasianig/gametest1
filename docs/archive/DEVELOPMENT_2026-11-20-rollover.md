# Проверки 19–20.11.2026 (перенесено 21.11)

20.11.2026, batch после правок целей/маршрутов (Unity закрыт): ArenaThemeChecks FAIL «Factory: obstacle count 612» (ожидание 10–36 устарело с переходом на Terrain), VerticalSliceChecks FAIL «Damage cap mismatch» (ожидает урон ×5, баланс 19.11 сделал +60%), HeroCombatChecks: выстрел по тестовой цели с EnemyAI прошёл, FAIL позже — «Weapon model or muzzle missing from right hand 5». Все три падения — устаревшие ожидания проверок; до NextWaypoint проверки не дошли, маршруты в Play не проверены.

20.11.2026, аллокации целей/маршрутов (AutoShoter, ArenaLayout, HeroCombatChecks): компиляция Roslyn Assembly-CSharp и Assembly-CSharp-Editor (против свежей Assembly-CSharp), 0 ошибок. VerticalSliceChecks/ArenaThemeChecks не запускались: Unity был открыт и занят (batch невозможен), а ArenaThemeChecks.Finish всегда вызывает EditorApplication.Exit. Запустить при закрытом Unity: `-executeMethod ArenaThemeChecks.RunBatch` и `VerticalSliceChecks.RunBatch` (+ HeroCombatChecks). Остаток: `foreach` по `EnemyAI.All` (IEnumerable над HashSet) упаковывает перечислитель — ~1 мелкая аллокация на вызов.

20.11.2026, вид боя: CombatLookShots.RunBatch (без -quit) — 2 кадра в Logs/combat-look-*.png; риски по кадрам: мелкие фигуры (чёрные ожоги исправлены — FxSplat, TelegraphFxChecks 8 PASS), полоски HP смещены от врагов (проверить в живом Play).

20.11.2026, палитры врагов и камера (0, 13, −4,333; сначала 15): CameraSpawnChecks.RunBatch (batchmode, без -quit) PASS, Logs/camera-spawn-check.txt. Палитры: `python tools/icons/enemy_palette_remap.py` (`--restore` — откат из Logs/enemy-palettes-backup/, Logs в git не попадает — оригиналы также в git). Не проверено: читаемость тёмных врагов на траве/в тени, вид кадра с новой высоты, хватает ли обзора для стрелков/снайперов (17 м) — на 16:9 по вертикали видно примерно ±10 м (оценка по кадру), снайпер сверху/снизу может стрелять из-за края экрана.

20.11.2026, шрифт UI (`GameUi.DefaultFont`): только компиляция Roslyn, 0 ошибок. Риск при подключении своего шрифта: другая ширина/высота строк может переполнить поля (Text без Best Fit), нужна проверка меню, HUD, карточек и Мастерской на RU и EN; шрифт без кириллицы покажет пустые квадраты.

20.11.2026, круги событий (`ArenaEvents.Flat`, прозрачные кольца): ArenaEventChecks.RunBatch (batchmode, без -quit) — 15 PASS, отчёт Logs/arena-event-results.txt. Не проверено: вид с игровой камеры (batch без графики) — яркость колец на траве, пересечение с прозрачными эффектами EnemyFx, сглаживание пунктира на мелких кругах засады (2,2 м).

19.11.2026, боссы: BossShowcaseChecks.RunBatch (без -quit) — отчёт Logs/boss-showcase.txt и лист Logs/boss-showcase.png (крупно в движении/во время атаки, игровая камера с полосой HP); HP в середине опускается ниже 50% для проверки второй фазы. Риски: баланс (HP ×5, урон ×1,9, у Громилы 2800); пятна GroundSplat с 20.11 полупрозрачные.

19.11.2026, турель-миниган: TurretMinigunChecks.RunBatch (без -quit) OK, 0 runtime errors, Logs/turret-minigun-check.txt и кадры turret-minigun.png. Сборка моделей: `blender.exe -b --factory-startup -P tools/blender/make_items.py -- Turret`, затем ItemModelInstall.RunBatch. Риск: части моделей не называть Leg*/Body/Carapace/Wing* (ModelWalkAnimator).
