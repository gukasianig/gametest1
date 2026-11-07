"""Робот Weak: коренастый сервисный робот, квадратный корпус, красные панели, широкая линза. Перед модели — +Y.
Части разделены для процедурной анимации в Unity (ModelWalkAnimator): Leg_* (ноги, шарнир вверху), Body, Antenna, Front (метка «перёд»)."""
import os
import sys
sys.path.insert(0, os.path.dirname(__file__))
import kit

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Generated"))
PREVIEW = os.environ.get("PREVIEW_DIR", OUT)
os.makedirs(OUT, exist_ok=True)

kit.clear_scene()
palette_path = os.path.join(OUT, "Palette.png")
kit.save_palette(palette_path)
mat = kit.make_material(palette_path)
root = kit.make_root("Enemy_Weak")
parts = []


def add(builder, origin):
    obj = builder.finish(origin=origin, parent=root)
    obj.data.materials.append(mat)
    parts.append(obj)
    return obj


# Ноги: шарнир в верхней точке ноги
for name, sx, sy in (("Leg_FL", -1, 1), ("Leg_FR", 1, 1), ("Leg_BL", -1, -1), ("Leg_BR", 1, -1)):
    b = kit.Builder(name)
    x, y = sx * 0.3, sy * 0.24
    b.box((x, y, 0.17), (0.2, 0.2, 0.34), 1, bevel=0.03)
    b.box((x, y, 0.03), (0.28, 0.3, 0.06), 10, bevel=0.02)
    add(b, (x, y, 0.34))

# Корпус
b = kit.Builder("Body")
b.box((0, 0, 0.62), (0.92, 0.82, 0.62), 0, bevel=0.07)
b.box((0, 0, 0.99), (0.84, 0.74, 0.12), 2, bevel=0.04)
b.box((0, 0, 0.3), (0.96, 0.86, 0.08), 12, bevel=0.03)
for sx in (-1, 1):
    b.box((sx * 0.47, 0, 0.62), (0.06, 0.5, 0.38), 2, bevel=0.02)
b.box((0, 0.42, 0.66), (0.6, 0.08, 0.26), 10, bevel=0.03)
b.box((0, 0.465, 0.66), (0.44, 0.04, 0.14), 4, bevel=0.015)
add(b, (0, 0, 0.62))

# Антенна: шарнир у основания
b = kit.Builder("Antenna")
b.box((0.26, -0.22, 1.12), (0.05, 0.05, 0.18), 1, bevel=0.01)
b.box((0.26, -0.22, 1.24), (0.09, 0.09, 0.08), 3, bevel=0.015)
add(b, (0.26, -0.22, 1.05))

front = kit.make_marker("Front", (0, 0.8, 0.6), root)

kit.export_fbx([root] + parts + [front], os.path.join(OUT, "Enemy_Weak.fbx"))
print("EXPORTED")
kit.render_preview(parts, os.path.join(PREVIEW, "Enemy_Weak_preview.png"), yaw_deg=35, elev_deg=25, size=1.9)
print("PREVIEW done")
