"""Снаряд ракетницы в стиле ракетницы (make_rocket_launcher.py): красный нос с белым поясом как у боеголовки в жерле,
светлый корпус с оранжевой полосой, четыре оранжевых стабилизатора, графитовое сопло и светящийся факел.
Нос по +Y Blender (так ждёт ProjectileModels: NoseFix 180°), центр в начале координат, длина ~1 (игра сама масштабирует до 1,6 м).
Факел — отдельная часть с материалом RocketGlow (ProjectileModelSetup ставит на него M_ProjGlowOrange).
Запуск: blender -b -P tools/blender/make_rocket_shot.py
Результат: docs/art/Weapons/RocketLauncher/Shot/ (rocket.blend, rocket.fbx, RocketShot_Palette.png, кадры, VERIFICATION.json)."""

import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
import kit
from creature_mesh import Part, make_atlas, render_model, save_model, validate, verify_fbx, world_vertices
from weapon_kit import ROOT, circle

OUT = ROOT / "docs/art/Weapons/RocketLauncher/Shot"
OUT.mkdir(parents=True, exist_ok=True)
NAME = "rocket"
# Те же цвета, что у ракетницы.
PALETTE = (
    "1E2228", "3A4049", "7E8896", "C6CED8", "F2A23A", "C56F1E", "F5C542", "C8402E",
    "EEF1F4", "08090B", "FF9A3C", "5FD8D0", "C39055", "121417", "FFE3A0", "8E2A22",
)
GRAPHITE, DARK, STEEL, LIGHT, ORANGE, DARKORANGE, YELLOW, RED, WHITE, VOID, GLOW, LENS, BRASS, RUBBER, HOT, DARKRED = range(16)

kit.clear_scene()
root = kit.make_root("RocketShot")
material = make_atlas(PALETTE, OUT, "RocketShot")
glow_material = bpy.data.materials.new("RocketGlow")
glow_material.use_nodes = True
shader = glow_material.node_tree.nodes.get("Principled BSDF")
shader.inputs["Base Color"].default_value = (1, .6, .15, 1)
shader.inputs["Emission Color"].default_value = (1, .6, .15, 1)
shader.inputs["Emission Strength"].default_value = 4
parts = []


def add(name, part, mat=None):
    parts.append(part.build(name, (0, 0, 0), root, mat or material))


R = .085
S = 14

# Корпус: сопло сзади (y<0), нос спереди (+Y).
body = Part()
body.rings([circle(-.42, 0, .05, .05, S), circle(-.4, 0, R, R, S), circle(-.3, 0, R, R, S), circle(-.29, 0, R + .004, R + .004, S),
            circle(-.25, 0, R + .004, R + .004, S), circle(-.24, 0, R, R, S), circle(.08, 0, R, R, S), circle(.09, 0, R + .006, R + .006, S),
            circle(.15, 0, R + .006, R + .006, S), circle(.16, 0, R, R, S), circle(.2, 0, R, R, S), circle(.3, 0, .07, .07, S),
            circle(.4, 0, .038, .038, S), circle(.45, 0, .012, .012, S)],
           lambda row, face: (DARK, LIGHT, DARK, DARK, DARK, LIGHT, WHITE, ORANGE, WHITE, WHITE,
                              RED, RED, DARKRED)[row])
add("Body", body)

# Сопло: графитовый раструб с тёмной глубиной.
nozzle = Part()
nozzle.rings([circle(-.4, 0, .052, .052, S), circle(-.47, 0, .066, .066, S), circle(-.47, 0, .05, .05, S), circle(-.43, 0, .03, .03, S)],
             lambda row, face: (GRAPHITE, STEEL, VOID)[row])
nozzle.colors[-1] = GLOW
add("Nozzle", nozzle)

# Четыре стабилизатора по диагоналям.
fins = Part()
for k in range(4):
    a = math.radians(45 + 90 * k)
    radial = Vector((math.cos(a), 0, math.sin(a)))
    side = Vector((-math.sin(a), 0, math.cos(a)))
    t = .016
    contour = [radial * (R - .01) + Vector((0, y, 0)) + side * (t / 2) for y in (-.4, -.18)]
    contour += [radial * .19 + Vector((0, -.3, 0)) + side * (t / 2), radial * .19 + Vector((0, -.42, 0)) + side * (t / 2)]
    fins.solid([tuple(p) for p in contour], t, ORANGE, DARKORANGE, tuple(side))
add("Fins", fins)

# Факел: светящийся конус за соплом (материал RocketGlow).
flame = Part()
flame.rings([circle(-.44, 0, .036, .036, 10), circle(-.52, 0, .044, .044, 10), circle(-.62, 0, .026, .026, 10), circle(-.68, 0, .006, .006, 10)], GLOW)
add("Flame", flame, glow_material)

points = world_vertices(parts)
minimum = Vector(min(v[i] for v in points) for i in range(3))
maximum = Vector(max(v[i] for v in points) for i in range(3))
center = (minimum + maximum) * .5
for obj in parts:
    for vertex in obj.data.vertices:
        vertex.co -= center
    obj.data.update()
bounds = [round(v, 4) for v in maximum - minimum]
triangles = sum(len(face.vertices) - 2 for obj in parts for face in obj.data.polygons)
assert 1.0 < bounds[1] < 1.3 and triangles < 2000, (bounds, triangles)
validation = validate(parts)
save_model(root, parts, OUT, NAME)
verify_fbx(OUT / (NAME + ".fbx"), len(parts), triangles, bounds)

preview_scale = 1.1
root.scale = (preview_scale,) * 3
root.location = Vector((0, 0, (maximum.z - minimum.z) * .5 * preview_scale))
render_model(OUT, bounds[2] * preview_scale * .5, 1.6, 54)
report = {"meshParts": len(parts), "triangles": triangles, "boundsBlenderXYZ": bounds, "noseAxis": "+Y",
          "glowPart": "Flame (RocketGlow)", "fbxRoundTrip": "PASS", **validation}
(OUT / "VERIFICATION.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("RocketShot PASS", json.dumps(report, ensure_ascii=False), flush=True)
