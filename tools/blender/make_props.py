"""Кристалл опыта, дропы (аптечка, магнит) и крупные здания арены. Размеры зданий — в метрах под габариты кубов-укрытий.
Запуск: blender -b -P make_props.py (PREVIEW_DIR — куда писать превью)."""
import os
import sys
sys.path.insert(0, os.path.dirname(__file__))
import kit

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Generated"))
PREVIEW = os.environ.get("PREVIEW_DIR", OUT)


def start(name):
    kit.clear_scene()
    p = os.path.join(OUT, "Palette.png")
    kit.save_palette(p)
    return kit.make_root(name), kit.make_material(p), []


def finish(name, root, mat, parts, size, yaw=35):
    for o in parts:
        o.data.materials.append(mat)
    kit.export_fbx([root] + parts, os.path.join(OUT, name + ".fbx"))
    kit.render_preview(parts, os.path.join(PREVIEW, name + "_preview.png"), yaw_deg=yaw, elev_deg=25, size=size)
    print("DONE", name)


def one(name, size, build):
    root, mat, parts = start(name)
    b = kit.Builder(name)
    build(b)
    parts.append(b.finish(parent=root))
    finish(name, root, mat, parts, size)


# --- Кристалл опыта: ступенчатый бирюзовый "бриллиант" ---
def crystal(b):
    for z, r, c in ((0.12, 0.12, 7), (0.26, 0.22, 7), (0.42, 0.3, 8), (0.58, 0.22, 7), (0.72, 0.12, 8)):
        b.cylinder((0, 0, z), r, 0.15, c, segments=6)


one("Pickup_Crystal", 1.1, crystal)


# --- Аптечка ---
def kit_box(b):
    b.box((0, 0, 0.35), (0.7, 0.7, 0.7), 8, bevel=0.06)
    b.box((0, 0, 0.72), (0.3, 0.1, 0.06), 12, bevel=0.01)
    for (cx, cy, sx, sy, sz) in ((0.36, 0, 0.04, 0.45, 0.15), (0.36, 0, 0.04, 0.15, 0.45), (-0.36, 0, 0.04, 0.45, 0.15), (-0.36, 0, 0.04, 0.15, 0.45)):
        b.box((cx, cy, 0.35), (sx, sy, sz), 2, bevel=0.01)
    for (cx, cy, sx, sy, sz) in ((0, 0.36, 0.45, 0.04, 0.15), (0, 0.36, 0.15, 0.04, 0.45), (0, -0.36, 0.45, 0.04, 0.15), (0, -0.36, 0.15, 0.04, 0.45)):
        b.box((cx, cy, 0.35), (sx, sy, sz), 2, bevel=0.01)


one("Pickup_Repair", 1.1, kit_box)


# --- Магнит: подкова с красными концами ---
def magnet(b):
    b.box((0, 0, 0.62), (0.76, 0.22, 0.24), 9, bevel=0.05)
    for sx in (-1, 1):
        b.box((sx * 0.26, 0, 0.36), (0.24, 0.22, 0.4), 9, bevel=0.05)
        b.box((sx * 0.26, 0, 0.1), (0.24, 0.22, 0.2), 2, bevel=0.04)
        b.box((sx * 0.26, 0, 0.17), (0.26, 0.24, 0.03), 8, bevel=0.005)


one("Pickup_Magnet", 1.1, magnet)


# --- Здания: габариты (x, y-глубина, высота) ---
def generator(b):   # 8 x 5 x 4
    b.box((0, 0, 1.6), (8, 5, 3.2), 7, bevel=0.15)
    b.box((0, 0, 0.2), (8.2, 5.2, 0.4), 12, bevel=0.05)
    b.box((0, 0, 3.3), (7.6, 4.6, 0.3), 12, bevel=0.1)
    for i in (-2, -1, 0, 1, 2):
        b.box((i * 1.4, -1, 3.7), (0.5, 3, 0.7), 1, bevel=0.05)
    for i in (-2.5, 2.5):
        b.box((i, 2.52, 1.4), (1.8, 0.1, 1.8), 10, bevel=0.04)
        b.box((i, 2.58, 1.4), (1.4, 0.05, 1.4), 4, bevel=0.03)


def tower(b):       # 8 x 8 x 6
    b.box((0, 0, 1.2), (8, 8, 2.4), 12, bevel=0.15)
    b.box((0, 0, 3.4), (5.2, 5.2, 2.0), 9, bevel=0.12)
    b.box((0, 0, 4.9), (6.4, 6.4, 0.5), 1, bevel=0.1)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((sx * 2.8, sy * 2.8, 5.45), (0.4, 0.4, 0.6), 2, bevel=0.05)
    for k in range(4):
        b.box((0, 2.62 if k % 2 == 0 else -2.62, 3.4), (4, 0.1, 0.8), 4, bevel=0.03)
    b.box((0, 0, 5.4), (1.2, 1.2, 1.2), 5, bevel=0.1)


def radar(b):       # 6 x 6 x 3
    b.box((0, 0, 0.9), (6, 6, 1.8), 11, bevel=0.15)
    b.box((0, 0, 1.95), (5.2, 5.2, 0.3), 12, bevel=0.08)
    b.cylinder((0, 0, 2.5), 0.4, 0.9, 1, segments=10)
    b.cylinder((0, 0, 2.95), 1.9, 0.12, 8, segments=14)
    b.cylinder((0, 0, 3.0), 0.3, 0.2, 3, segments=10)
    b.box((1.8, 2.6, 1.0), (1.0, 0.1, 1.0), 4, bevel=0.03)


def station(b):     # 9 x 6 x 4
    b.box((0, 0, 1.5), (9, 6, 3), 9, bevel=0.15)
    b.box((0, 0, 0.2), (9.2, 6.2, 0.4), 12, bevel=0.05)
    for i in (-3, -1.5, 0, 1.5, 3):
        b.box((i, 0, 3.4), (0.6, 5, 0.8), 12, bevel=0.05)
    for i in (-2.5, 2.5):
        b.cylinder((i, 3.06, 1.6), 0.8, 0.12, 10, axis='Y', segments=12)
        b.cylinder((i, 3.1, 1.6), 0.4, 0.12, 4, axis='Y', segments=8)


def relay(b):       # 5 x 5 x 4
    b.box((0, 0, 1.0), (5, 5, 2.0), 9, bevel=0.12)
    b.box((0, 0, 2.3), (3.4, 3.4, 0.6), 12, bevel=0.08)
    b.box((0, 0, 3.2), (1.4, 1.4, 1.2), 1, bevel=0.06)
    b.sphere((0, 0, 3.9), 0.5, 2)
    for sx in (-1, 1):
        b.box((sx * 2.52, 0, 1.0), (0.1, 3, 1.2), 10, bevel=0.04)
        b.box((sx * 2.58, 0, 1.0), (0.05, 2.4, 0.7), 4, bevel=0.03)


one("Building_Generator", 12, generator)
one("Building_Tower", 12, tower)
one("Building_Radar", 10, radar)
one("Building_Station", 12, station)
one("Building_Relay", 8, relay)
