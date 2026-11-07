# Главное меню — восемь направлений

6 ноября 2026. Основание: [CODEX_MENU_BRIEF.md](../../CODEX_MENU_BRIEF.md). Инструмент: встроенный image_gen, восемь отдельных вызовов, без CLI/API. Все восемь изображений просмотрены и сохранены как PNG 1920×1080; размер приведён к требуемому при сохранении без кадрирования композиции. Надписей, кнопок-плиток и UI-панелей на картинках нет; физические пульты/рычаги — объекты мира. Пространство сверху по центру предназначено для будущего названия.

Это цельные концепты для выбора направления. Они не подключены к Unity, нажимаемых областей/анимаций ещё нет. Рисованные символы служат ориентирами. На этом этапе не утверждается единая внешность героя: она различается между вариантами и уточняется после выбора. Читаемость сцен оценена визуально, проверка в окне игры не проводилась.

| № | Направление / изображение | Расположение пунктов |
| --- | --- | --- |
| 1 | [Ангар](Variants/menu_concept_1_hangar.png) | Мастерская — верстак слева; Старт — ворота справа; Условия — отдельный пульт на переднем плане справа от героя. |
| 2 | [Командный мостик](Variants/menu_concept_2_bridge.png) | Мастерская — объёмный голографический инструмент слева; Старт — голографические ворота в центре; Условия — голографическая шкала угроз справа. |
| 3 | [Подземная мастерская](Variants/menu_concept_3_underground.png) | Старт — подъёмник с круглой дверью слева; Мастерская — главный верстак с дроном справа; Условия — стойка рубильников на переднем плане. |
| 4 | [Арена снаружи](Variants/menu_concept_4_arena_exterior.png) | Старт — главный вход на арену по центру справа; Мастерская — ремонтная палатка слева; Условия — отдельная стойка угроз справа на переднем плане. |
| 5 | [Орбитальная станция](Variants/menu_concept_5_orbital.png) | Мастерская — ремонтный модуль слева; Старт — центральный стыковочный шлюз; Условия — отдельный реакторный модуль справа. |
| 6 | [Созвездие](Variants/menu_concept_6_constellation.png) | Мастерская — созвездие шестерни с ключом слева; Старт — созвездие ворот в центре; Условия — созвездие шкалы угроз справа. |
| 7 | [Ремонтный двор](Variants/menu_concept_7_scrapyard.png) | Мастерская — ремонтный грузовик слева; Старт — ворота из металлолома справа; Условия — самодельная стойка рычагов в центре на переднем плане. |
| 8 | [Музейная витрина](Variants/menu_concept_8_museum.png) | Мастерская — экспонат с разборным механизмом слева; Старт — большие круглые ворота справа; Условия — кинетический экспонат со шкалой угроз впереди. |

После выбора владельцем: уточнить композицию/героя; подготовить фон, героя и каждый интерактивный предмет отдельными слоями (предметы — PNG с альфой), затем подключать реальные зоны нажатия и подписи кодом. Выбор не сделан; слои не генерировались. Код, сцены и существующие GUID этой задачей не менялись.

# Точные запросы генерации

Полный запрос для каждого изображения = общий запрос ниже + соответствующее дополнение, без других референсов и изображений.

```text
Use case: stylized-concept. Create ONE polished full-screen game MAIN MENU CONCEPT ART, landscape 16:9, intended final output 1920x1080. Industrial sci-fi survival arena shooter universe. High-end painted volumetric illustration, dark graphite metal, blue steel, teal emissive accents and restrained warm gold light, soft key light from upper left. A strong coherent environmental composition with three unmistakable separated INTERACTIVE IN-WORLD LOCATIONS: arena entrance/start, workshop, difficulty/threat apparatus. These are believable physical or spatial objects of the world, NOT buttons, NOT tiles, NOT rectangular UI panels, NOT a HUD. Each must be large and visually distinct, with enough space around it for separate click regions. Readable at 1280x720. Leave a calm low-detail empty area in the TOP CENTER for a game title to be added later by code. ABSOLUTELY NO TEXT, no letters, no numbers, no written labels or logos anywhere (including signs, screens and gauges). Only simple abstract pictorial symbols may appear on objects. No watermark, no border, no monitor or device framing. Do not bake in UI labels. 
```

## 1. Ангар

Файл: `Variants/menu_concept_1_hangar.png`.

Мастерская — верстак слева; Старт — ворота справа; Условия — отдельный пульт на переднем плане справа от героя.

Исходный файл генератора: `exec-c2da41cc-7993-4712-af3b-57bb0010a4c8.png`.

Дополнение к общему запросу (точный текст):

```text
Direction 1, HANGAR: immersive human-eye-level view into an industrial hangar with deep perspective and ceiling gantries. Small armored arena operator stands on a circular deployment platform in lower center, facing away. LEFT foreground: a generous workbench with a mechanic drone, teal tool light, gear hologram and recognizable tools, clearly the WORKSHOP. RIGHT midground: massive round segmented arena blast gate opening onto warm gold light, clearly START. LOWER RIGHT foreground, well separated from gate and hero: a sculptural difficulty console with three robust lever handles, graduated lamp rods and a red-amber threat beacon; no writing, no numbers. Optional small trophy shelf far left background. Balanced asymmetry and strong silhouettes, quiet upper center wall. Rich environment art, inviting playable hub.
```

## 2. Командный мостик

Файл: `Variants/menu_concept_2_bridge.png`.

Мастерская — объёмный голографический инструмент слева; Старт — голографические ворота в центре; Условия — голографическая шкала угроз справа.

Исходный файл генератора: `exec-3e2ad138-d84b-48f2-97a7-b0de8d57d4b2.png`.

Дополнение к общему запросу (точный текст):

```text
Direction 2, COMMAND BRIDGE: cinematic wide view from a dark spaceship command bridge, angled panoramic window and distant industrial arena ship outside. THREE LARGE FLOATING VOLUMETRIC HOLOGRAPHIC SCULPTURES on separate round projector pedestals across the LOWER TWO THIRDS, NOT holographic rectangles. LEFT: a brilliant teal mechanical gear entwined with a wrench and a small physical service drone beside its projector, WORKSHOP. CENTER slightly behind: a gold-lit model of a circular arena portal with physical ring segments, START. RIGHT: vertical red/amber/teal threat rods with adjustable lever forms and a pulsing warning orb, DIFFICULTY. Silhouette of armored operator at lower edge facing projections. Quiet dark upper window region for later title, each hologram widely separated. Sophisticated high-tech ship bridge, not a generic desktop menu.
```

## 3. Подземная мастерская

Файл: `Variants/menu_concept_3_underground.png`.

Старт — подъёмник с круглой дверью слева; Мастерская — главный верстак с дроном справа; Условия — стойка рубильников на переднем плане.

Исходный файл генератора: `exec-1a9369e0-af1e-4f53-9e87-7fd11d2face7.png`.

Дополнение к общему запросу (точный текст):

```text
Direction 3, UNDERGROUND WORKSHOP: cozy atmospheric subterranean machine shop, three-quarter isometric cutaway perspective, warm tungsten desk lamps and teal cooling equipment, concrete vaulted ceiling. RIGHT half: a richly detailed main workbench with a hovering mechanic drone handling a wrench, a softly glowing gear projection, tactile equipment, WORKSHOP. LEFT half: a rugged cylindrical freight lift with a bright gold circular arena hatch and illuminated landing ramp, START. LOWER CENTER foreground: independent waist-high industrial difficulty rack with three enormous differently shaped toggle levers, amber gauge needles without numerals, three red warning bulbs, DIFFICULTY. Pinned engineering drawings only abstract lines without glyphs in distant walls. No hero covering click targets; small operator at lower-left edge. Quiet shadowed upper center. Hand-painted tactile charm, believable workshop sanctuary.
```

## 4. Арена снаружи

Файл: `Variants/menu_concept_4_arena_exterior.png`.

Старт — главный вход на арену по центру справа; Мастерская — ремонтная палатка слева; Условия — отдельная стойка угроз справа на переднем плане.

Исходный файл генератора: `exec-8bdd69ce-b4c7-4af9-9c0d-9acdd4d71db6.png`.

Дополнение к общему запросу (точный текст):

```text
Direction 4, OUTSIDE THE ARENA: dramatic exterior arena entrance at blue-hour dusk with vast industrial circular arena architecture and a hazy graphite sky. Big gold illuminated arena gate in CENTER-RIGHT lower half, inviting entrance, START. LEFT foreground: a compact open repair tent with mechanical workbench, teal lamp and service drone, clearly WORKSHOP. RIGHT foreground: freestanding rugged threat totem with three heavy rotating rings, red/amber lamps and a large control lever, clearly DIFFICULTY, separated from entry gate by open pavement. One small armored hero stands in lower center with back to viewer looking toward arena, not obscuring objects. Warm dusty ground and cool steel; strong environmental scale. No stadium advertising, lettering, banners or numbers. Calm spacious dark sky in upper center for title.
```

## 5. Орбитальная станция

Файл: `Variants/menu_concept_5_orbital.png`.

Мастерская — ремонтный модуль слева; Старт — центральный стыковочный шлюз; Условия — отдельный реакторный модуль справа.

Исходный файл генератора: `exec-31e9e3a0-206a-42f8-b56d-b8521dbc5709.png`.

Дополнение к общему запросу (точный текст):

```text
Direction 5, ORBITAL STATION: sweeping semi-isometric exterior cutaway of a compact orbital hub floating above a luminous blue planet horizon. THREE LARGE DISTINCT STATION MODULES linked by thin illuminated walkways, concentrated in lower two-thirds with clear gaps: LEFT open-sided rounded workshop pod with robotic repair arms and teal tool station, WORKSHOP. CENTER slightly lower, largest: arena deployment docking ring/launch gateway with warm gold inner light and an empty platform, START. RIGHT elevated: threat-control reactor pod with three vertical red-amber energy rods, a physical ring dial and warning beacons, DIFFICULTY. A tiny armored operator at the central walkway communicates scale. Planet curvature and stars behind, but quiet dark upper-center space for title. Beautiful miniature sci-fi hub, not UI cards, not a flat network chart.
```

## 6. Созвездие

Файл: `Variants/menu_concept_6_constellation.png`.

Мастерская — созвездие шестерни с ключом слева; Старт — созвездие ворот в центре; Условия — созвездие шкалы угроз справа.

Исходный файл генератора: `exec-633a8677-98ce-4968-8b3c-97e7abebf9c8.png`.

Дополнение к общему запросу (точный текст):

```text
Direction 6, CONSTELLATIONS: elegant minimalist night-sky fantasy-tech composition inspired by a sci-fi constellation upgrade tree. Three monumental luminous CONSTELLATION SCULPTURES across the middle/lower half, formed from sparse glowing stars, fine flowing light trails and a few floating metal fragments, NO enclosing frames or panels. LEFT constellation forms a gear and wrench with a tiny teal mechanic drone orbiting it, WORKSHOP. CENTER constellation forms an imposing golden arena gateway/portal ring with three-dimensional metallic fragments and an inviting dark passage, START. RIGHT constellation forms a stylized three-spire threat gauge with red-amber star nodes and a lever silhouette, DIFFICULTY. Faint industrial horizon along lowest edge, small armored hero on a rock at bottom center looking up. Midnight-blue palette, teal and warm gold, generous negative space, no text or regular grid. Top-center dark calm sky reserved for title. Emotional painted cosmic scene, not a flat UI diagram.
```

## 7. Ремонтный двор

Файл: `Variants/menu_concept_7_scrapyard.png`.

Мастерская — ремонтный грузовик слева; Старт — ворота из металлолома справа; Условия — самодельная стойка рычагов в центре на переднем плане.

Исходный файл генератора: `exec-6590ede4-47b0-4e1c-a933-a5f5d4fb511f.png`.

Дополнение к общему запросу (точный текст):

```text
Direction 7, SCRAPYARD REPAIR YARD: vibrant cinematic asymmetrical junkyard hub, a scrappy handmade industrial sci-fi world with blue steel, oil-stained graphite and warm dusty gold sunlight. LEFT: an open-sided mobile repair truck with teal underlighting, salvaged tools, an articulated mechanic drone and giant wrench-shaped neon OBJECT SIGN (symbol only, no text), WORKSHOP. RIGHT midground: a dramatic arena gate assembled from thick salvaged turbine rings and armor plates with warm glowing interior, START. CENTER foreground: DIY difficulty control sculpture built from a red motorcycle handle, three giant mechanical levers, mismatched amber warning lights and a vertical row of threat indicator bulbs, DIFFICULTY. Hero leans beside a compact supply crate at far lower-left edge, not blocking targets. Rich piles of machinery toward edges, clear open central walking space, quiet top-center sky. No writing on vehicles, cans or signs.
```

## 8. Музейная витрина

Файл: `Variants/menu_concept_8_museum.png`.

Мастерская — экспонат с разборным механизмом слева; Старт — большие круглые ворота справа; Условия — кинетический экспонат со шкалой угроз впереди.

Исходный файл генератора: `exec-2fc12edd-4e36-4a7f-aa33-75798a94d95e.png`.

Дополнение к общему запросу (точный текст):

```text
Direction 8, MUSEUM DISPLAY: luxurious dark industrial sci-fi trophy gallery with curved glass domes and pools of soft top-left light. A full-size armored arena operator and signature gun displayed on a circular central plinth slightly toward the back; no text labels. Three prominent interactive EXHIBITS in the lower two-thirds, each a distinct world object: LEFT a curved transparent glass canopy over an exploded-view repair mechanism with robot arms, teal tools and a floating gear, WORKSHOP. RIGHT a huge sculptural gold-lit circular arch built from arena gate segments with an inviting traversable inner opening, START. LOWER CENTER a kinetic threat-calibration exhibit: three rotating metal spires, amber-red energy bulbs and a tactile lever on a round base, DIFFICULTY. Beautiful reflections and subtle gold edge lighting, dark graphite architecture, tall quiet upper-center wall area for later game title. No rectangular vitrines, no exhibit plaques, no written signage, no button-shaped objects. Refined volumetric painted game art.
```
