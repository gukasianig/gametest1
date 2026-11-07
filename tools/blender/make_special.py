"""Босс «Страж арены» и мировые объекты спецоружия: Saw (пила), Mine (мина), Turret (турель), Drone (дрон).
Запуск: blender -b -P make_special.py (PREVIEW_DIR — куда писать превью)."""
import math
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


def finish(name, root, mat, parts, extra=(), size=2.0):
    for o in parts:
        o.data.materials.append(mat)
    kit.export_fbx([root] + parts + list(extra), os.path.join(OUT, name + ".fbx"))
    kit.render_preview(parts, os.path.join(PREVIEW, name + "_preview.png"), yaw_deg=35, elev_deg=25, size=size)
    print("DONE", name)


# --- Пила: диск с зубьями ---
root, mat, parts = start("Weapon_Saw")
b = kit.Builder("Saw")
b.cylinder((0, 0, 0.05), 0.62, 0.1, 12, segments=16)
b.cylinder((0, 0, 0.05), 0.2, 0.14, 4, segments=10)
for i in range(10):
    a = i * math.tau / 10
    b.box((math.cos(a) * 0.66, math.sin(a) * 0.66, 0.05), (0.26, 0.12, 0.1), 3, bevel=0.01, rot_z=a + 0.5)
parts.append(b.finish(parent=root))
finish("Weapon_Saw", root, mat, parts, size=1.7)

# --- Мина: низкий диск с шипами и красной кнопкой ---
root, mat, parts = start("Weapon_Mine")
b = kit.Builder("Mine")
b.cylinder((0, 0, 0.1), 0.5, 0.2, 1, segments=14)
b.cylinder((0, 0, 0.23), 0.34, 0.08, 12, segments=14)
b.cylinder((0, 0, 0.3), 0.14, 0.08, 2, segments=10)
for i in range(6):
    a = i * math.tau / 6
    b.box((math.cos(a) * 0.5, math.sin(a) * 0.5, 0.1), (0.14, 0.1, 0.12), 3, bevel=0.01, rot_z=a)
parts.append(b.finish(parent=root))
finish("Weapon_Mine", root, mat, parts, size=1.3)

# --- Турель: основание, поворотная голова, ствол (перёд +Y) ---
root, mat, parts = start("Weapon_Turret")
b = kit.Builder("Base")
b.box((0, 0, 0.1), (0.9, 0.9, 0.2), 12, bevel=0.04)
b.cylinder((0, 0, 0.3), 0.25, 0.3, 1, segments=10)
parts.append(b.finish(parent=root))
b = kit.Builder("Head")
b.box((0, 0, 0.62), (0.56, 0.56, 0.34), 9, bevel=0.05)
b.box((0, 0.1, 0.8), (0.36, 0.4, 0.1), 7, bevel=0.03)
b.box((0, 0.29, 0.62), (0.3, 0.04, 0.14), 4, bevel=0.01)
b.cylinder((0, 0.55, 0.62), 0.07, 0.5, 10, axis='Y')
parts.append(b.finish(origin=(0, 0, 0.62), parent=root))
finish("Weapon_Turret", root, mat, parts, size=1.6)

# --- Дрон: корпус и четыре рычага с роторами (Rotor_* вращаются в Unity) ---
root, mat, parts = start("Weapon_Drone")
b = kit.Builder("Body")
b.box((0, 0, 0.3), (0.5, 0.5, 0.24), 9, bevel=0.06)
b.box((0, 0.26, 0.3), (0.3, 0.04, 0.12), 3, bevel=0.01)
b.box((0, 0, 0.18), (0.3, 0.3, 0.06), 1, bevel=0.02)
for sx in (-1, 1):
    for sy in (-1, 1):
        b.box((sx * 0.28, sy * 0.28, 0.3), (0.4, 0.06, 0.06), 12, bevel=0.01, rot_z=math.radians(45 * sx * sy))
parts.append(b.finish(origin=(0, 0, 0.3), parent=root))
for k, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
    b = kit.Builder("Rotor_%d" % k)
    x, y = sx * 0.45, sy * 0.45
    b.cylinder((x, y, 0.38), 0.05, 0.1, 1, segments=8)
    b.box((x, y, 0.44), (0.5, 0.07, 0.02), 3, bevel=0.005)
    b.box((x, y, 0.44), (0.07, 0.5, 0.02), 3, bevel=0.005)
    parts.append(b.finish(origin=(x, y, 0.44), parent=root))
finish("Weapon_Drone", root, mat, parts, size=1.5)

# --- Босс «Страж арены»: крупный шестиногий робот, оранжевый, с пушкой и короной ---
root, mat, parts = start("Enemy_Boss")
leg_h = 0.5
for n, sx, sy in (("Leg_FL", -1, 1), ("Leg_FR", 1, 1), ("Leg_BL", -1, -1), ("Leg_BR", 1, -1)):
    b = kit.Builder(n)
    x, y = sx * 0.62, sy * 0.5
    b.box((x, y, leg_h / 2), (0.34, 0.34, leg_h), 1, bevel=0.05)
    b.box((x, y, 0.04), (0.5, 0.56, 0.08), 10, bevel=0.03)
    parts.append(b.finish(origin=(x, y, leg_h), parent=root))
zc = leg_h + 0.55
b = kit.Builder("Body")
b.box((0, 0, zc), (1.7, 1.5, 1.1), 3, bevel=0.1)
b.box((0, 0, zc + 0.62), (1.5, 1.3, 0.16), 2, bevel=0.06)
b.box((0, 0, leg_h + 0.06), (1.76, 1.56, 0.12), 12, bevel=0.04)
for sx in (-1, 1):
    b.box((sx * 0.95, 0, zc + 0.1), (0.3, 0.9, 0.7), 12, bevel=0.06)
    b.box((sx * 0.7, 0, zc + 0.85), (0.16, 0.16, 0.34), 2, bevel=0.03)   # корона-шипы
b.box((0, 0.76, zc + 0.05), (1.1, 0.12, 0.44), 10, bevel=0.04)
b.box((0, 0.82, zc + 0.05), (0.8, 0.06, 0.24), 2, bevel=0.02)
b.cylinder((0, 0.95, zc + 0.62), 0.12, 0.9, 10, axis='Y')
b.box((0, 0.5, zc + 0.62), (0.34, 0.34, 0.3), 12, bevel=0.05)
parts.append(b.finish(origin=(0, 0, zc), parent=root))
b = kit.Builder("Antenna")
b.box((0, -0.45, zc + 0.9), (0.08, 0.08, 0.5), 1, bevel=0.01)
b.box((0, -0.45, zc + 1.2), (0.18, 0.18, 0.14), 4, bevel=0.02)
parts.append(b.finish(origin=(0, -0.45, zc + 0.7), parent=root))
front = kit.make_marker("Front", (0, 1.4, zc), root)
finish("Enemy_Boss", root, mat, parts, extra=[front], size=3.6)
