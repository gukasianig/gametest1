"""Огнемёт для руки героя в стиле набора Kenney Blaster Kit (крупные скошенные формы, плоские цвета палитры).
Корпус, рукоять, топливный бак под стволом, кожух с рёбрами охлаждения, латунный раструб, запальник и шланг.
Запуск: blender -b -P tools/blender/make_flamethrower.py
Результат: docs/art/Weapons/Flamethrower/ (BLEND, FBX, палитра, три ракурса, VERIFICATION.json).
Ориентация как у моделей Kenney в Blender (по ним же определяется знак «вперёд»), чтобы запись ModelSkinSet
Kind_Flame осталась с тем же поворотом (0, 180, 0)."""

import math
from pathlib import Path
import sys

from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
import bpy
import kit
from creature_mesh import Part
from weapon_kit import ROOT, make_atlas, case, circle, finish, kenney_forward_sign, shell_color

OUT = ROOT / "docs/art/Weapons/Flamethrower"
OUT.mkdir(parents=True, exist_ok=True)
NAME = "Flamethrower"
PALETTE = (
    "1E2228", "3A4049", "7E8896", "C6CED8", "C8402E", "8E2A22", "F28A2E", "F5C542",
    "C39055", "8E6232", "121417", "08090B", "FF9A3C", "FFE3A0", "5FB8FF", "EEF1F4",
)
GRAPHITE, DARK, STEEL, LIGHT, RED, DARKRED, ORANGE, YELLOW, BRASS, BRONZE, RUBBER, VOID, GLOW, HOT, PILOT, WHITE = range(16)


FORWARD = kenney_forward_sign()
kit.clear_scene()
root = kit.make_root(NAME)
material = make_atlas(PALETTE, OUT, NAME)
parts = []


def add(name, part, pivot=(0, 0, 0)):
    parts.append(part.build(name, pivot, root, material))


# Корпус: крупный, скошенный, красный, с тёмным низом.
body = Part()
case(body, [(-.215, .045, .088, .092, .026), (-.19, .05, .118, .128, .032), (.04, .052, .124, .134, .034),
            (.115, .05, .116, .118, .03), (.14, .048, .094, .094, .024)],
     shell_color(RED, DARKRED, DARKRED, DARK))
add("Body", body)

# Затыльник и задняя накладка.
rear = Part()
case(rear, [(-.255, .04, .074, .108, .022), (-.235, .042, .086, .122, .026), (-.212, .042, .086, .122, .026), (-.205, .042, .07, .1, .02)],
     shell_color(GRAPHITE, DARK, DARK, GRAPHITE))
add("Rear", rear)

# Полосы опасности на верхней планке.
stripe = Part()
rows = [(-.15 + i * .022, .126, .056, .026, .008) for i in range(9)]
case(stripe, rows, lambda row, face: (YELLOW if row % 2 == 0 else GRAPHITE) if face in (3, 4, 5) else DARK)
add("HazardRail", stripe)

# Рукоять, низ с оранжевым башмаком.
grip = Part()
grip.tube([((0, -.13, .0), .046, .05), ((0, -.142, -.06), .05, .052), ((0, -.158, -.12), .048, .05), ((0, -.166, -.15), .044, .046)],
          GRAPHITE, 8, transport=True)
add("Grip", grip)
heel = Part()
case(heel, [(-.2, -.165, .076, .036, .01), (-.185, -.172, .088, .042, .012), (-.13, -.17, .088, .042, .012), (-.12, -.164, .072, .032, .009)],
     shell_color(ORANGE, ORANGE, ORANGE, DARK))
add("GripHeel", heel)

# Спуск и скоба.
guard = Part()
guard.tube([((0, -.1, -.008), .016, .018), ((0, -.055, -.06), .018, .018), ((0, -.01, -.064), .016, .016), ((0, .02, -.03), .014, .014)],
           DARK, 6, transport=True)
guard.tube([((0, -.07, -.005), .01, .012), ((0, -.064, -.036), .01, .01)], BRASS, 6, transport=True)
add("Guard", guard)

# Топливный бак под стволом: оранжевый, со стальными бандажами и куполами на торцах.
tank = Part()
tank_rows = []
for y, r in ((-.06, .018), (-.052, .042), (-.04, .056), (-.025, .062), (-.012, .062), (.0, .058), (.012, .062),
             (.07, .062), (.082, .058), (.094, .062), (.15, .062), (.165, .056), (.177, .042), (.185, .018)):
    tank_rows.append(circle(y, -.065, r, r, 14))
band = {0, 1, 2, 3, 5, 6, 8, 9, 11, 12}
tank.rings(tank_rows, lambda row, face: STEEL if row in band and row not in (3, 9) else (YELLOW if face in (3, 4) else ORANGE) if row in (3, 9) else ORANGE)
add("FuelTank", tank)

# Манометр на боку бака.
gauge = Part()
gauge.tube([((.058, .045, -.03), .022, .022), ((.074, .045, -.03), .024, .024), ((.078, .045, -.03), .02, .02)], STEEL, 10)
gauge.colors[-1] = WHITE
needle = Part()
needle.tube([((.079, .043, -.035), .002, .002), ((.081, .052, -.022), .002, .002)], RED, 4)
add("Gauge", gauge)
add("GaugeNeedle", needle)

# Ствол-кожух с рёбрами охлаждения.
shroud = Part()
fin_rows = []
y = .135
for i in range(7):
    fin_rows.append(circle(y, .055, .046, .046, 12))
    fin_rows.append(circle(y + .012, .055, .046, .046, 12))
    fin_rows.append(circle(y + .014, .055, .038, .038, 12))
    fin_rows.append(circle(y + .024, .055, .038, .038, 12))
    y += .026
shroud.rings(fin_rows, lambda row, face: STEEL if row % 4 in (0, 1) else GRAPHITE)
add("Shroud", shroud)

# Латунный раструб с тёмной горловиной и раскалённой серединой.
nozzle = Part()
y0 = y
nozzle.rings([circle(y0, .055, .036, .036, 14), circle(y0 + .05, .055, .046, .046, 14), circle(y0 + .085, .055, .06, .06, 14),
              circle(y0 + .095, .055, .062, .062, 14), circle(y0 + .095, .055, .048, .048, 14), circle(y0 + .07, .055, .03, .03, 14),
              circle(y0 + .06, .055, .022, .022, 14)],
             lambda row, face: (BRASS, BRASS, BRONZE, LIGHT, VOID, VOID)[row])
nozzle.colors[-1] = GLOW
add("Nozzle", nozzle)
core = Part()
core.rings([circle(y0 + .061, .055, .014, .014, 10), circle(y0 + .066, .055, .012, .012, 10)], HOT)
add("NozzleCore", core)

# Запальник под раструбом: стальная трубка с синим огоньком.
pilot = Part()
pilot.tube([((0, .18, .006), .011, .011), ((0, y0 + .07, .006), .011, .011), ((0, y0 + .078, .006), .009, .009)], STEEL, 8)
pilot.colors[-1] = PILOT
add("PilotLight", pilot)
pilot_glow = Part()
pilot_glow.tube([((0, y0 + .079, .006), .007, .007), ((0, y0 + .091, .006), .004, .004)], PILOT, 6)
add("PilotFlame", pilot_glow)

# Шланг от бака к корпусу по правому боку.
hose = Part()
hose.tube([((.04, -.045, -.07), .012, .012), ((.07, -.07, -.04), .012, .012), ((.078, -.07, .01), .012, .012),
           ((.068, -.03, .045), .012, .012), ((.06, .0, .05), .012, .012)], RUBBER, 6, transport=True)
add("Hose", hose)
fitting = Part()
fitting.tube([((.054, .0, .05), .016, .016), ((.066, .0, .05), .016, .016)], BRASS, 8)
add("HoseFitting", fitting)

# Передняя рукоять-скоба сверху (за неё держат вторую руку) — тёмная дуга над кожухом.
handle = Part()
handle.tube([((0, .02, .118), .012, .014), ((0, .045, .158), .012, .014), ((0, .11, .158), .012, .014), ((0, .135, .1), .012, .014)],
            GRAPHITE, 6, transport=True)
add("CarryHandle", handle)

finish(root, parts, OUT, NAME, FORWARD, Vector((0, -.15, -.09)), Vector((0, y0 + .095, .055)), (.5, .8))
