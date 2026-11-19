"""Ракетница для руки героя в стиле набора Kenney Blaster Kit: толстая оранжевая пусковая труба с полосами
опасности, торчащая из жерла боеголовка, задний раструб-сопло, тёмный корпус с рукоятью, передняя рукоять,
прицел с бирюзовой линзой и запасная ракета сбоку.
Запуск: blender -b -P tools/blender/make_rocket_launcher.py
Результат: docs/art/Weapons/RocketLauncher/ (BLEND, FBX, палитра, три ракурса, VERIFICATION.json)."""

from pathlib import Path
import sys

from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
import kit
from creature_mesh import Part
from weapon_kit import ROOT, make_atlas, case, circle, finish, kenney_forward_sign, shell_color

OUT = ROOT / "docs/art/Weapons/RocketLauncher"
OUT.mkdir(parents=True, exist_ok=True)
NAME = "RocketLauncher"
PALETTE = (
    "1E2228", "3A4049", "7E8896", "C6CED8", "F2A23A", "C56F1E", "F5C542", "C8402E",
    "EEF1F4", "08090B", "FF9A3C", "5FD8D0", "C39055", "121417", "FFE3A0", "8E2A22",
)
GRAPHITE, DARK, STEEL, LIGHT, ORANGE, DARKORANGE, YELLOW, RED, WHITE, VOID, GLOW, LENS, BRASS, RUBBER, HOT, DARKRED = range(16)

FORWARD = kenney_forward_sign()
kit.clear_scene()
root = kit.make_root(NAME)
material = make_atlas(PALETTE, OUT, NAME)
parts = []


def add(name, part):
    parts.append(part.build(name, (0, 0, 0), root, material))


TZ = .1          # ось трубы по высоте
R = .062         # радиус трубы
FRONT, BACK = .4, -.27

# Пусковая труба: оранжевая, тёмные бандажи у торцов и в середине, полосы опасности ближе к жерлу.
tube = Part()
rows = []
for y, r in ((BACK, R + .006), (BACK + .03, R + .006), (BACK + .032, R), (-.12, R), (-.118, R + .005), (-.09, R + .005),
             (-.088, R), (.2, R), (.202, R + .003), (.21, R + .003), (.23, R + .003), (.25, R + .003), (.27, R + .003),
             (.272, R), (FRONT - .04, R), (FRONT - .038, R + .008), (FRONT, R + .008)):
    rows.append(circle(y, TZ, r, r, 14))


def tube_color(row, face):
    if row in (0, 1, 4, 5, 7, 12, 14, 15):
        return DARK
    if row in (8, 9, 10, 11):  # кольца опасности
        return YELLOW if row % 2 == 1 else GRAPHITE
    return DARKORANGE if face in (9, 10, 11) else ORANGE


tube.rings(rows, tube_color)
add("LaunchTube", tube)

# Жерло: светлый обод, тёмная глубина.
mouth = Part()
mouth.rings([circle(FRONT, TZ, R + .008, R + .008, 14), circle(FRONT + .004, TZ, R + .004, R + .004, 14),
             circle(FRONT + .004, TZ, R - .01, R - .01, 14), circle(FRONT - .05, TZ, R - .01, R - .01, 14)],
            lambda row, face: (LIGHT, LIGHT, VOID)[row])
mouth.colors[-1] = VOID
add("Mouth", mouth)

# Боеголовка ракеты выглядывает из жерла: белый пояс, красный конус.
warhead = Part()
warhead.rings([circle(FRONT - .04, TZ, .038, .038, 12), circle(FRONT + .012, TZ, .038, .038, 12), circle(FRONT + .014, TZ, .04, .04, 12),
               circle(FRONT + .026, TZ, .04, .04, 12), circle(FRONT + .05, TZ, .03, .03, 12), circle(FRONT + .075, TZ, .013, .013, 12),
               circle(FRONT + .082, TZ, .004, .004, 12)],
              lambda row, face: (STEEL, WHITE, WHITE, RED, RED, DARKRED)[row])
add("Warhead", warhead)

# Заднее сопло: графитовый раструб с тёплым отсветом внутри.
exhaust = Part()
exhaust.rings([circle(BACK, TZ, R + .002, R + .002, 14), circle(BACK - .03, TZ, R + .012, R + .012, 14), circle(BACK - .05, TZ, R + .02, R + .02, 14),
               circle(BACK - .05, TZ, R + .006, R + .006, 14), circle(BACK - .02, TZ, R - .02, R - .02, 14), circle(BACK - .012, TZ, R - .03, R - .03, 14)],
              lambda row, face: (GRAPHITE, GRAPHITE, STEEL, VOID, VOID)[row])
exhaust.colors[-1] = GLOW
add("Exhaust", exhaust)

# Корпус под трубой с рукоятью и спуском.
body = Part()
case(body, [(-.17, .03, .07, .05, .014), (-.155, .025, .092, .07, .02), (.1, .025, .092, .07, .02), (.118, .03, .072, .05, .014)],
     shell_color(GRAPHITE, DARK, DARK, DARK))
add("Body", body)
plate = Part()
case(plate, [(-.12, .024, .098, .04, .01), (.06, .024, .098, .04, .01)], shell_color(ORANGE, ORANGE, ORANGE, DARK))
add("SidePlate", plate)

grip = Part()
grip.tube([((0, -.1, .0), .044, .048), ((0, -.112, -.06), .048, .05), ((0, -.128, -.12), .046, .048), ((0, -.136, -.15), .042, .044)],
          GRAPHITE, 8, transport=True)
add("Grip", grip)
heel = Part()
case(heel, [(-.17, -.165, .072, .034, .01), (-.155, -.172, .084, .04, .012), (-.1, -.17, .084, .04, .012), (-.09, -.164, .068, .03, .009)],
     shell_color(ORANGE, ORANGE, ORANGE, DARK))
add("GripHeel", heel)
guard = Part()
guard.tube([((0, -.075, -.008), .015, .017), ((0, -.035, -.055), .017, .017), ((0, .01, -.058), .015, .015), ((0, .035, -.01), .013, .013)],
           DARK, 6, transport=True)
guard.tube([((0, -.045, -.005), .01, .012), ((0, -.04, -.034), .01, .01)], RED, 6, transport=True)
add("Guard", guard)

# Передняя рукоять.
fore = Part()
fore.tube([((0, .1, .01), .036, .04), ((0, .108, -.05), .038, .04), ((0, .114, -.1), .034, .036)], GRAPHITE, 8, transport=True)
case(fore, [(.088, -.112, .05, .026, .008), (.098, -.118, .056, .03, .01), (.13, -.118, .056, .03, .01), (.14, -.112, .046, .022, .007)],
     shell_color(ORANGE, ORANGE, ORANGE, DARK))
add("ForeGrip", fore)

# Прицел слева сверху: корпус, кронштейн, бирюзовые линзы.
sight = Part()
case(sight, [(-.06, TZ + .085, .036, .03, .008, -.034), (-.05, TZ + .085, .044, .04, .011, -.034),
             (.06, TZ + .085, .044, .04, .011, -.034), (.07, TZ + .085, .036, .03, .008, -.034)],
     shell_color(DARK, GRAPHITE, GRAPHITE, DARK))
sight.colors[-1] = LENS
sight.colors[0] = LENS
add("Sight", sight)
mount = Part()
case(mount, [(-.02, TZ + .062, .02, .03, .005, -.03), (.03, TZ + .062, .02, .03, .005, -.03)], STEEL)
add("SightMount", mount)

# Запасная ракета на правом боку трубы, на двух хомутах.
spare = Part()
sx, sz = .082, TZ - .02
spare.rings([circle(-.16, sz, .006, .006, 10, sx), circle(-.15, sz, .022, .022, 10, sx), circle(.05, sz, .022, .022, 10, sx),
             circle(.052, sz, .024, .024, 10, sx), circle(.066, sz, .024, .024, 10, sx), circle(.09, sz, .014, .014, 10, sx),
             circle(.104, sz, .003, .003, 10, sx)],
            lambda row, face: (STEEL, LIGHT, WHITE, RED, RED, DARKRED)[row])
add("SpareRocket", spare)
clamps = Part()
for y in (-.08, .0):
    case(clamps, [(y, sz, .055, .056, .01, sx - .016), (y + .014, sz, .055, .056, .01, sx - .016)], STEEL)
add("SpareClamps", clamps)

finish(root, parts, OUT, NAME, FORWARD, Vector((0, -.12, -.09)), Vector((0, FRONT + .004, TZ)), (.6, .9), preview_scale=2.2, frame=2.0)
