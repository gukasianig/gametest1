# Архив DEVELOPMENT — перенос 18.11.2026

## Проверки

**Компиляция/импорт** (Unity не должен быть открыт другим экземпляром): `Unity.exe -batchmode -nographics -quit -projectPath "F:/test game" -logFile Logs/setup-check.log`, запуск через `Start-Process -PassThru`, затем `.WaitForExit()` и `.ExitCode` (`-Wait` может ждать дерево процессов лицензирования). Компиляция не доказывает поведение.

**Editor-проверки** (`Assets/Editor/`, не входят в сборку; без маркера ничего не запускают; отчёты в `Logs/`, не хранить в docs):

| Проверка | Маркер в `Logs/` | Что проверяет |
| --- | --- | --- |
| VerticalSliceChecks | `vertical-slice.request` (`interactive`) или batch | улучшения, пауза, арена, босс, победа, повтор, меню, 300 с бота |
| CameraSpawnChecks | `camera-spawn-check.request` | угол камеры, спавн вне кадра, учёт урона |
| GroundWaveChecks | `ground-wave-check.request` | пол, сбор опыта, ранние волны |
| EnemyRosterChecks | `enemy-roster.request` | трое базовых врагов, допуски, дебют |
| SpecialEnemyChecks | `special-enemies.request` | пять новых ролей врагов |
| CrowdMovementChecks | `crowd-movement.request` | обход толпы |
| ProjectileOwnerChecks | `projectile-owner.request` | пули игнорируют владельца |
| WeaponChecks | batch: `-executeMethod WeaponChecks.RunBatch` (без `-quit`; маркер `weapon-checks.request`) | 13 оружий, страницы меню, поведение девяти специальных оружий, очистка при смене |
| NewWeaponBuilder | batch: `-executeMethod NewWeaponBuilder.RunBatch` (без `-quit`) | не проверка: добавляет специальные оружия в `AutoShoter.weapons` сцены; повторяем безопасно |
| EnemyDirectorChecks | batch: `-executeMethod EnemyDirectorChecks.RunBatch` (без `-quit`; маркер `enemy-director.request`) | Призыватель/Снайпер, 10 типов, расписание режиссёра, элита, орда |
| NewEnemyBuilder | batch: `-executeMethod NewEnemyBuilder.RunBatch` (без `-quit`) | не проверка: создаёт префабы Summoner/Sniper и регистрирует их в сцене; повторяем безопасно |
| MetaProgressChecks | batch: `-executeMethod MetaProgressChecks.RunBatch` (без `-quit`; маркер `meta-progress.request`) | мета-прогрессия: награды, рекорды, покупки, сохранение/восстановление, кнопка Мастерской, бонусы в забеге, выплата |
| EnemyAnimationChecks | batch: `-executeMethod EnemyAnimationChecks.RunBatch` (без `-quit`) или меню Checks | колени, позы атак, снаряды, взрыв, смерти, разделение; кадры `docs/art/EnemyAnim/` |
| UiArtworkPreview | `ui-art-preview.request` (weapons/upgrades/health/reset) | показ карточек/HP, не визуальная приёмка |

**Batch VerticalSliceChecks** (Unity закрыт, БЕЗ `-quit`; метод сам завершает процесс): `-executeMethod VerticalSliceChecks.RunBatch` — все проверки, 7–10 мин; `-executeMethod VerticalSliceChecks.RunSoakBatch` — только 300 с бота с диагностикой (кол-во убийств, позиция, враги в радиусе, «замершие»), ~6 мин. Результат — `Logs/vertical-slice-results.txt`. Не запускать параллельно открытому проекту и внутри пользовательского забега (тест временно меняет данные и перезагружает сцену).

## Известные риски и открытое

1. **A09 (разобрано 5 ноября 2026):** баг зависания не найден. Бот только уклоняется (радиус 9 м) и бегает по патрулю; когда его скорость (с улучшениями движения) выше скорости врагов, они не догоняют и остаются в 20–34 м вне дальности оружия (`inRange20 0`, `frozen 0` — движутся, но не достают). Убийств нет, предел живых 20 достигнут, спавн ждёт — волна стоит (в двух полных прогонах стоп на волнах 26 и 19; изолированный прогон со случайно иным выбором улучшений дошёл до волны 31 и 158 убийств). Дизайн-вопрос (не баг): предел живых считает и далёких врагов, поэтому игрок, убегающий от медленных врагов, останавливает рост давления. Варианты: не считать врагов дальше N м, возвращать далёких ближе к игроку, сильнее подгонять скорость. Решено 5 ноября 2026: в лимит считаются только враги в 30 м от игрока (`pressureRadius`), общий предел ×2; повторный `RunSoakBatch` PASS (волна 37, 176 убийств, пик 40, без застоя). Soak-бот при HP<40 получает +40 HP (счётчик rescues) — теперь давление реальное, дальнейшая оценка сложности — за человеком.
2. Дальность 0 у `weapons` сцены (защитный fallback — не удалять).
3. `Bullet.prefab` `lifeTime = 0`: AutoShoter задаёт время из WeaponData; ручной `PlayerShooting` — только урон (ручная стрельба не основной путь, отдельно не проверялась).
4. Профилирование больших волн не проводилось (враги ищутся реестром, но `AutoShoter.FindClosestEnemy` ходит по тегу каждый кадр).
5. Не проверены: длительное удержание диагоналей человеком, края пола/нестандартные камеры, все четыре оружия по отдельности, поздний баланс, standalone-сборка.
6. Заголовок улучшений пересекается с таймером (дефект UI, отмечен 1 ноября).

## Безопасная работа с Unity и Git

Выходить из Play перед правкой сцены; не завершать пользовательский редактор с несохранёнными изменениями; не менять файлы проекта во время batch-прогона (скрипты/сцена). При проблемах кэша: закрыть редактор, сделать проверенную копию вне Temp (например `.local-backups/` с локальным git-исключением), никогда не перемещать Assets/Packages/ProjectSettings. Коммитить ассеты вместе с `.meta`.

## Как поддерживать записи

После существенной работы: PROJECT_CONTEXT — актуальная картина (заменять устаревшее), CHANGELOG — датированная запись, этот файл — проверки и риски; пункты GAME_ROADMAP (и UP-пункты) отмечать по факту и объёму проверки. Без полных журналов, учётных данных и случайных сведений о ПК. Для ответа без новых фактов записи не нужны.

Старые записи — `docs/archive/DEVELOPMENT_2026-11-15-pistol-rollover.md`.

Старые записи — `docs/archive/DEVELOPMENT_2026-11-15-rifle-rollover.md`.

Проверки автомата 15.11 — `docs/archive/DEVELOPMENT_2026-11-17-xp-rollover.md`.

Старая проверка — `docs/archive/DEVELOPMENT_2026-11-17-clock-rollover.md`.

Старый прогон — `docs/archive/DEVELOPMENT_2026-11-18-workshop-rollover.md`.
