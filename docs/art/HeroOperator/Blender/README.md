# Оператор из Blender

Модель сделана по [выбранному эскизу](../operator-concept.png), переработана в v2 и отполирована 17.11.2026 (docs/HERO_VISUAL_V3.md). `Operator.blend` содержит редактируемые детали брони, комбинезон, материалы, скелет и 11 действий в Action Editor. Фон и освещение находятся в отдельной коллекции `Studio | previews only`. В экспорт попадают только модель, скелет, сокеты и клипы.

- Unity FBX — `Assets/Art/Characters/OperatorBlender/Operator.fbx`. Одна скин-сетка, 7882 треугольника (v2: 5540; исходник: 4236), 16 костей и 6 материалов URP. Исходная поза имеет высоту 2 м, подошвы на `Y=0`, лицо по `+Z` в Unity. В Blender ось высоты `Z`, лицо по `−Y`.
- `OperatorRig/Hips/Spine/Chest/UpperArmR/ForearmR/HandR/WeaponSocket` — хват оружия. Локальный `+Z` сокета смотрит вдоль ствола. `LeftHandGrip` — сокет левой кисти для гранаты. `BackSocket` — на груди со стороны спины.
- `Idle`, `Run`, `RunBack`, `StrafeLeft/Right`, `TurnLeft/Right` зациклены. `Dash`, `Throw`, `Hit` и `Death` проигрываются один раз. Выпуск гранаты рассчитан на 47% клипа `Throw`. Перемещение и рывок остаются на `PlayMovement`, клипы двигают скелет относительно неподвижного корня.

## Подключение в Unity

08.11.2026 FBX подключён в сохранённых `SampleScene` и `GreenArenaPlay`. Play Mode прошёл в обеих сценах, новая Windows-сборка включает `OperatorSkin`. Ниже шаги для повторного подключения.

1. Вложить `Operator.fbx` под объект игрока вместо старой визуальной модели. Перенести локальное смещение визуального объекта. В текущей `GreenArenaPlay` это `(0, −1, 0)`, без дополнительного разворота.
2. На корневом `Animation` FBX выставить `Idle`, `Play Automatically` и `Always Animate`. Указать этот компонент в `HeroLocomotion.heroAnimation`. Параметры импорта и ссылки на шесть URP-материалов уже записаны в `.meta`.
3. Проверить хват и вид с боевой камеры после сохранения и повторного открытия сцены. Коллайдер, `Rigidbody`, `AutoShoter`, `PlayMovement` и исходный `firePoint` остаются на игроке.

Для повторной проверки использовать `OperatorFbxImport.RunBatch` для импорта или `HeroCombatChecks.RunFbxBatch` для проверки в `GreenArenaPlay` в Play Mode. Второй метод работает в batch и не сохраняет сцену. Исходный экспорт воспроизводится через `tools/blender/make_operator_fbx.py` из Blender 4.5 LTS.

16.11.2026: `HeroVisualChecks.RunBatch` проверяет импорт/направления/развороты/фазу шага и боевые зависимости в сохранённой GreenArenaPlay. PASS, 0 runtime errors; см. `docs/HERO_VISUAL_V2.md`. `--quick` у Blender-генератора снижает только samples контрольных рендеров, не геометрию/анимации FBX.

17.11.2026: HeroSmoothChecks.RunBatch PASS, 0 runtime errors. Предпросмотр исходного Run: Blender -b --python tools/blender/preview_operator_motion.py, затем node tools/operator-motion-preview.cjs. Результат — operator-run-preview.png (APNG, 16 кадров, 25 fps); это студийный цикл, не запись runtime-смешивания в игре.
