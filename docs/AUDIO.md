# Звук

Обновлено 9 ноября 2026. Музыка меню и боя, пистолет, автомат и дробовик используют подготовленные записи из `Assets/Audio/Resources/RecordedAudio/`. Остальные эффекты загружаются из `Resources/Sfx/` или синтезируются в `GameAudio.cs`.

## Откуда какой звук

| Звук | Источник | Файлы |
| --- | --- | --- |
| `Shot`, `AutoShot`, `Shotgun` | BigSoundBank, Joseph Sardin, CC0 | `.357 Magnum` 0,20 с, Beretta M12 0,13 с, Winchester XTR 0,35 с. Приглушённая обработка B, ФНЧ 2,6 кГц. Подготовленные уровни сохранены. |
| `Rail` | Kenney Sci-Fi Sounds | `laserLarge_000-004` |
| `Zap` (турель, дроны) | Kenney | `laserSmall` ×0,78, ФНЧ 3 кГц + щелчок `impactMetal_light`, 0,16 с (прежний `laserRetro` пищал) |
| `Deploy` (установка турели) | Kenney Impact Sounds | `impactMetal_medium_000-004` — лязг (раньше играл выстрел пистолета) |
| `Flame` (вспышка зажигания) | Kenney Sci-Fi Sounds | начало `thrusterFire_000-004`, 0,6 с с затуханием |
| петля огнемёта `FlameLoop` | Kenney | ровный участок `thrusterFire_001` с 1,65 с, ФНЧ 5 кГц, бесшовная петля 1,6 с |
| `Explosion` | Kenney Sci-Fi Sounds | `explosionCrunch_000-004` + 0,7 × `lowFrequency_explosion_000/001`, 1,6 с |
| `Kill` | Kenney | глухой `impactSoft_heavy_000-004` + 0,6 × начало `slime_000`, 0,3 с. Прежний `slime_001` длился 4,7 с и звучал после исчезновения врага |
| `Ice` | Kenney Impact Sounds | `impactGlass_medium_000-004` |
| `Hit` | Kenney Impact Sounds | мягкий `impactSoft_medium_000-004`, ФНЧ 3,2 кГц, 0,14 с (раньше `impactPunch_medium`) |
| петля пилы `SawLoop` | BigSoundBank 1136 «Table saw, cut» | ровный холостой участок 46,1–47,7 с, сшит в бесшовную петлю |
| `SawCut` | BigSoundBank 1136 | начала резов на 8,0 / 15,9 / 24,1 / 37,2 с, по 0,45 с |
| `Arc` | синтез `GameAudio.Arc` (09.11) | короткий «зап» 0,25 с: нисходящий тон 1400→240 Гц с ЧМ и тихим треском под ФНЧ; файлы сварки удалены, `BuildArc` отключён |
| `MenuMusic` / `GameMusic` | yd / MintoDog, CC0 | Factory Ambiance, 122 с с перекрёстным затуханием, и Darkness Road Remake, 128 с, только музыка. Потоковый импорт OGG. |
| `Rocket`, `Throw` (бросок пилы) | синтез `GameAudio` | — |

Файлы собирает `Assets/Editor/AudioSourcePrep.cs` (`Build`) из исходников `Assets/Audio/Source/` (в сборку не попадают — на них нет ссылок). Громкость файлов из `Sfx/` при загрузке выравнивается по пику `GameAudio.Peak` (нужен импорт «Decompress On Load», он стоит по умолчанию). Частые короткие звуки при загрузке обрезаются по `GameAudio.MaxSeconds` (смерть 0,4 с, попадание 0,2 с, выстрел 0,3 с…), чтобы длинный файл не тянулся. Вернуть синтез для звука — удалить его папку в `Resources/Sfx/`.

## Что где звучит

| Звук | Когда | Вызов |
| --- | --- | --- |
| `Shot` / `AutoShot` / `Shotgun` | обычный выстрел, автомат и дробовик соответственно. Эффект определяется базовым оружием, дополнительные пули его не меняют. | `AutoShoter.Shoot` |
| `Rail`, `Arc`, `Rocket`, `Ice` | выстрел спецоружия | `SpecialWeapons.PlayFireSound` |
| `Flame` + петля `SfxLoop.Flame` | `Flame` — только когда пламя загорается заново; петля держится, пока пламя видно (огнемёт бьёт каждые 0,15 с) | `PlayFireSound`, `SpecialWeapons.Update` → `GameAudio.Hold` |
| `Deploy` | установка турели (если есть место под новую) | `SpecialWeapons.PlayFireSound` (Turret) |
| `Throw` | бросок пилы (металлическое «шшинг») | `SpecialWeapons.PlayFireSound` (Boomerang) |
| петля `SfxLoop.Saw` | пока пила летит: громче рядом с героем, выше на быстрой пиле и при возврате | `WeaponSaw.Update` → `GameAudio.Hold` |
| `SawCut` | пила режет врага; петля на миг проседает по высоте (`GameAudio.Load`) | `WeaponSaw.Update` |
| `Zap` | выстрел турели и дронов (тихо) | `WeaponTurret`, `WeaponDrone` |
| `Explosion` | граната, взрыв ракеты и мины, подрывник | `Grenade.Explode`, `SpecialWeapons.Blast`, `EnemyAbility.Explode` |
| `Hit`, `Kill` | попадание по врагу (0,4; от тиков огнемёта ×0,35, горение без звука — `Health.HitSoundScale`), смерть врага (0,5) | `Health` |
| музыка | меню / забег | `StartMenu` → `GameAudio.SetMusic` |

У многих эффектов есть несколько вариантов без повтора подряд. У выстрелов B по одной записи на оружие с небольшим разбросом высоты. Интервал `GameAudio.MinGap` ограничивает частоту эффектов. Музыка загружается из файлов, фоновый синтез остаётся запасным.

## Как заменить звук файлом

Для выбранных музыки и выстрелов файлы в `Resources/RecordedAudio/` имеют приоритет и сохраняют записанные уровни. Сборщик `AudioSourcePrep.Build` работает с отдельной папкой `Sfx/`.

Для других звуков положить `.ogg`/`.wav` в `Assets/Audio/Resources/Sfx/<Имя>/`, где `<Имя>` — название из `enum Sfx` (`Arc`, `SawCut`, `Explosion`…), `SawLoop` для петли пилы, `MenuMusic`/`GameMusic` для музыки. Все файлы папки станут вариантами; синтез для этого звука отключится. Петлю пилы делать бесшовной. Учтите: `AudioSourcePrep.Build` пересоздаёт папку `Resources/Sfx/` целиком — свои файлы класть после сборки или добавлять в `Build`.

## Прослушка

- В игре: главное меню → «Тест-арена», слева выбрать оружие (Пила, Электродуга…), справа создать врагов.
- Без Unity: файлы в `Assets/Audio/Resources/Sfx/`; синтезированные звуки — `Logs/audio-preview/*.wav` (пишет `AudioChecks.RunBatch`).

## Пробы готовой музыки и записей — 9 ноября 2026

Исходники в `_incoming/audio-recordings-2026-11-09/`, короткие WAV с подстроенными уровнями — в `previews/`. В Unity не подключены. Музыка обрезана для сравнения. Обе пробы выстрелов собраны из одних и тех же записей с разной фильтрацией и длиной хвоста.

| Проба | Запись и условия по странице источника |
| --- | --- |
| Меню A | Эрик Сати, Gymnopédie №1, Робин Альчиаторе. Запись передана в общественное достояние исполнителем. [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Erik_Satie_-_gymnopedies_-_la_1_ere._lent_et_douloureux.ogg) |
| Меню B | Фридерик Шопен, Прелюдия №15, Георгий Лацабидзе. Запись передана в общественное достояние правообладателем. [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Chopin_Prelude_Op_28_N_15_Giorgi_Latsabidze_performs.ogg) |
| Бой A | Rock Theme 3, Umplix, CC0. [OpenGameArt](https://opengameart.org/content/rock-theme-3) |
| Бой B | Антонио Вивальди, «Лето», III. Presto, The Modena Chamber Orchestra. Страница записи указывает Public Domain Mark 1.0. [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:The_Modena_Chamber_Orchestra_-_Vivaldi%27s_Summer,_RV_315_-_III._Presto.ogg) |
| Выстрелы A/B | Joseph Sardin, BigSoundBank, CC0. [Револьвер .357 Magnum](https://bigsoundbank.com/shot-in-357-magnum-9-mm-s0438.html), [Beretta M12](https://bigsoundbank.com/shot-beretta-m12-9-mm-s0437.html), [Winchester Magnum XTR](https://bigsoundbank.com/shot-of-winchester-magnum-xtr-s0397.html) |

## Подборка для Оператора — 9 ноября 2026

Владелец выбрал выстрелы B и попросил другую музыку. Исходники в `_incoming/audio-recordings-2026-11-09/operator-music/`, четыре пробы по 24 с в `previews/`. В боевых пробах первые 12 с звучит музыка, затем добавляется `shots-B-soft.wav` с пропуском последнего выстрела дробовика. С 22,798 с в обеих смешанных пробах звучит только музыка. `battle-B-darkness-road-music-only.wav` содержит тот же фрагмент B целиком без слоя выстрелов. У всех четырёх треков CC0 по страницам авторов.

| Проба | Трек и источник |
| --- | --- |
| Меню A | Factory ambiance, yd. [OpenGameArt](https://opengameart.org/content/factory-ambiance) |
| Меню B | Space Graveyard, TinyWorlds. [OpenGameArt](https://opengameart.org/content/space-graveyard-ambient-track) |
| Бой A | Psycho Punch, KiluaBoy. [OpenGameArt](https://opengameart.org/content/psycho-punch-loop-fps-shoot-em-up) |
| Бой B | Darkness Road (Remake, 165 BPM), MintoDog. [OpenGameArt](https://opengameart.org/content/darkness-roadremake) |

## Лицензии подключённых файлов

Все — CC0 1.0 (общественное достояние): можно в коммерческой игре, указание автора не требуется.

| Источник | Лицензия | Где скачано | Оригиналы |
| --- | --- | --- | --- |
| Kenney Sci-Fi Sounds 1.0 (kenney.nl) | CC0, `License.txt` в архиве | https://kenney.nl/assets/sci-fi-sounds — ZIP скачал владелец 08.11.2026 | `_incoming/audio-2026-11-08/kenney_sci-fi-sounds.zip` |
| Kenney Impact Sounds 1.0 | CC0, `License.txt` в архиве | https://kenney.nl/assets/impact-sounds — ZIP скачал владелец 08.11.2026 | `_incoming/audio-2026-11-08/kenney_impact-sounds.zip` |
| BigSoundBank 1136 «Table saw, cut» | CC0 (страница звука) | https://bigsoundbank.com/table-saw-cut-s1136.html | `_incoming/audio-2026-11-08/bigsoundbank/1136.ogg` |
| BigSoundBank 0952 «Short welds» | CC0 | https://bigsoundbank.com/short-welds-s0952.html | `…/bigsoundbank/0952.ogg` |
| BigSoundBank 0950 «Long welding #1», 0134 «Electric shock» | CC0 | https://bigsoundbank.com/long-welding-1-s0950.html, https://bigsoundbank.com/electric-shock-s0134.html | скачаны, пока не используются |

Kenney просит (не обязательно) упомянуть kenney.nl в титрах.

## Другие найденные источники (не скачаны)

| Пакет | Что внутри | Лицензия |
| --- | --- | --- |
| [Kenney Digital Audio](https://kenney.nl/assets/digital-audio) | 60 цифровых «пью», фазеры | CC0 |
| [BigSoundBank 1135 «Table saw, empty»](https://bigsoundbank.com/table-saw-empty-s1135.html) | пила вхолостую, 24 с (сайт отдал 403 автоматике; можно скачать вручную) | CC0 |
| [Lentikula: Sci-Fi Weapon Shots](https://lentikula.itch.io/sci-fi-weapon-shots-sfx-freecc0) | 15 выстрелов, WAV 48 кГц | CC0, цена от 0 |
| [Freesound: circular-saw](https://freesound.org/browse/tags/circular-saw/) | записи пилы | разная, нужен вход |
| [Sonniss GameAudioGDC](https://rekkerd.org/sonniss-releases-gdc-2026-game-audio-bundle/) | гигабайты профессиональных записей | royalty-free без указания автора, не CC0 (проверить на sonniss.com) |
| [directory.audio: Circular Saw](https://directory.audio/collection/388-circular-saw-sounds) | пила: работа, разгон, остановка | у каждого файла своя |

Не подходят: ElevenLabs (атрибуция, ИИ-генерация), CC BY-пакеты (towball), платные пакеты.
