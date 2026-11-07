"""Враги с РАЗНЫМИ силуэтами в общем стиле (скруглённые плоские цвета, одна палитра). Перёд — +Y, ось вверх — Z.
Части для анимации в Unity (ModelWalkAnimator): Leg* — ноги/гусеницы (шарнир сверху), Body — корпус (покачивание; у парящих — парение),
Antenna — качается, Front — метка «перёд». Запуск: blender -b -P make_enemies.py (PREVIEW_DIR — куда писать превью)."""
import math
import os
import sys
sys.path.insert(0, os.path.dirname(__file__))
import kit

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Generated"))
PREVIEW = os.environ.get("PREVIEW_DIR", OUT)


class R:
    def __init__(self, name):
        self.name = name
        kit.clear_scene()
        p = os.path.join(OUT, "Palette.png")
        kit.save_palette(p)
        self.mat = kit.make_material(p)
        self.root = kit.make_root(name)
        self.parts = []

    def part(self, pname, build, origin=(0, 0, 0)):
        b = kit.Builder(pname)
        build(b)
        self.parts.append(b.finish(origin=origin, parent=self.root))

    def leg(self, pname, x, y, h, w=0.2, c=1, foot=10, depth=None):
        d = depth or w
        def build(b):
            b.box((x, y, h / 2), (w, d, h), c, bevel=0.03)
            b.box((x, y, 0.04), (w + 0.1, d + 0.12, 0.08), foot, bevel=0.02)
        self.part(pname, build, (x, y, h))

    def done(self, front=(0, 0.8, 0.6), size=2.4):
        for o in self.parts:
            o.data.materials.append(self.mat)
        marker = kit.make_marker("Front", front, self.root)
        kit.export_fbx([self.root] + self.parts + [marker], os.path.join(OUT, self.name + ".fbx"))
        kit.render_preview(self.parts, os.path.join(PREVIEW, self.name + "_preview.png"), yaw_deg=35, elev_deg=25, size=size)
        print("DONE", self.name)


def lens(b, y, z, w=0.4, h=0.12, c=4):
    b.box((0, y, z), (w + 0.14, 0.07, h + 0.12), 10, bevel=0.02)
    b.box((0, y + 0.04, z), (w, 0.04, h), c, bevel=0.01)


# 1. FAST — длинный низкий «гепард»: клиновидное тело, спойлер, тонкие длинные ноги
r = R("Enemy_Fast")
for n, sx, sy in (("Leg_FL", -1, 1), ("Leg_FR", 1, 1), ("Leg_BL", -1, -1), ("Leg_BR", 1, -1)):
    r.leg(n, sx * 0.24, sy * 0.5, 0.42, 0.1, c=12, foot=3)
def fast_body(b):
    b.box((0, 0, 0.62), (0.56, 1.5, 0.32), 3, bevel=0.07)
    b.box((0, 0.7, 0.58), (0.44, 0.5, 0.26), 4, bevel=0.07, rot_x=-0.35)
    b.box((0, -0.15, 0.82), (0.4, 0.8, 0.1), 1, bevel=0.03)
    lens(b, 0.88, 0.66, 0.3, 0.08, 10)
    b.box((0, -0.75, 0.95), (0.9, 0.1, 0.1), 2, bevel=0.02)       # спойлер
    for sx in (-1, 1):
        b.box((sx * 0.4, -0.75, 0.82), (0.07, 0.1, 0.26), 2, bevel=0.01)
        b.box((sx * 0.29, 0.1, 0.62), (0.03, 1.0, 0.1), 4, bevel=0.005)
r.part("Body", fast_body, (0, 0, 0.62))
r.done(front=(0, 1.1, 0.62), size=2.5)

# 2. TANK — гусеничный танк: две гусеницы, башня-купол, толстая пушка
r = R("Enemy_tank")
for n, sx in (("Leg_L", -1), ("Leg_R", 1)):
    def tread(b, sx=sx):
        b.box((sx * 0.72, 0, 0.3), (0.42, 1.6, 0.6), 10, bevel=0.1)
        for i in (-0.6, -0.2, 0.2, 0.6):
            b.cylinder((sx * 0.94, i, 0.3), 0.18, 0.06, 12, axis='X', segments=10)
        b.box((sx * 0.72, 0, 0.62), (0.46, 1.5, 0.06), 12, bevel=0.02)
    r.part(n, tread, (sx * 0.72, 0, 0.3))
def tank_body(b):
    b.box((0, 0, 0.75), (1.2, 1.4, 0.4), 14, bevel=0.1)
    b.box((0, 0.7, 0.72), (1.0, 0.1, 0.3), 12, bevel=0.05, rot_x=0.5)
    b.cylinder((0, -0.05, 1.12), 0.5, 0.34, 5, segments=12)
    b.cylinder((0, -0.05, 1.32), 0.38, 0.1, 12, segments=12)
    b.cylinder((0, 0.75, 1.15), 0.14, 1.0, 12, axis='Y', segments=10)
    b.cylinder((0, 1.28, 1.15), 0.2, 0.18, 2, axis='Y', segments=10)
    b.box((0.28, -0.3, 1.45), (0.1, 0.1, 0.3), 3, bevel=0.01)
r.part("Body", tank_body, (0, 0, 0.9))
r.done(front=(0, 1.3, 1.1), size=2.9)

# 3. SHOOTER — высокий двуногий стрелок с пушкой вместо руки
r = R("Enemy_Shooter")
r.leg("Leg_L", -0.2, 0, 0.62, 0.18, c=9, foot=1)
r.leg("Leg_R", 0.2, 0, 0.62, 0.18, c=9, foot=1)
def shooter_body(b):
    b.box((0, 0, 1.0), (0.62, 0.42, 0.7), 9, bevel=0.07)
    b.box((0, 0, 0.7), (0.66, 0.46, 0.1), 12, bevel=0.03)
    b.box((0, 0.02, 1.55), (0.42, 0.38, 0.34), 7, bevel=0.06)                    # голова
    lens(b, 0.2, 1.57, 0.3, 0.1, 3)
    b.box((-0.42, 0.05, 1.12), (0.2, 0.22, 0.3), 12, bevel=0.04)                 # левое плечо
    b.box((0.42, 0.05, 1.12), (0.22, 0.26, 0.34), 12, bevel=0.04)                # правое плечо-пушка
    b.cylinder((0.42, 0.55, 1.08), 0.1, 0.8, 10, axis='Y', segments=10)
    b.cylinder((0.42, 0.98, 1.08), 0.13, 0.1, 3, axis='Y', segments=10)
r.part("Body", shooter_body, (0, 0, 1.0))
def shooter_ant(b):
    b.box((-0.12, -0.1, 1.9), (0.04, 0.04, 0.3), 1, bevel=0.005)
    b.box((-0.12, -0.1, 2.08), (0.08, 0.08, 0.08), 3, bevel=0.01)
r.part("Antenna", shooter_ant, (-0.12, -0.1, 1.74))
r.done(front=(0, 0.9, 1.1), size=2.6)

# 4. CHARGER — бык: горизонтальное тело, рога, толстые короткие ноги, шипы на спине
r = R("Enemy_Charger")
for n, sx, sy in (("Leg_FL", -1, 1), ("Leg_FR", 1, 1), ("Leg_BL", -1, -1), ("Leg_BR", 1, -1)):
    r.leg(n, sx * 0.38, sy * 0.55, 0.38, 0.28, c=11, foot=10, depth=0.3)
def bull_body(b):
    b.box((0, -0.1, 0.78), (0.95, 1.5, 0.7), 2, bevel=0.1)
    b.box((0, 0.2, 1.18), (0.8, 0.7, 0.3), 3, bevel=0.1)                          # горб
    b.box((0, 0.85, 0.82), (0.7, 0.55, 0.55), 2, bevel=0.09)                      # голова
    b.box((0, 1.12, 0.66), (0.45, 0.14, 0.18), 12, bevel=0.03)                    # пятак
    lens(b, 1.13, 0.9, 0.4, 0.1, 4)
    for sx in (-1, 1):
        b.box((sx * 0.46, 0.85, 1.0), (0.14, 0.14, 0.5), 8, bevel=0.03)
        b.box((sx * 0.62, 0.95, 1.25), (0.14, 0.4, 0.14), 8, bevel=0.03, rot_x=0.3)
    for i in range(4):
        b.box((0, -0.55 + i * 0.3, 1.38), (0.12, 0.14, 0.22), 4, bevel=0.01)
r.part("Body", bull_body, (0, 0, 0.78))
r.done(front=(0, 1.3, 0.85), size=2.7)

# 5. BOMBER — шар-бомба с фитилём и циферблатом, на двух коротких ножках
r = R("Enemy_Bomber")
r.leg("Leg_L", -0.25, 0, 0.3, 0.2, c=12, foot=10)
r.leg("Leg_R", 0.25, 0, 0.3, 0.2, c=12, foot=10)
def bomb_body(b):
    b.sphere((0, 0, 0.9), 0.62, 3)
    b.box((0, 0, 0.9), (1.3, 0.2, 0.18), 10, bevel=0.03)                         # пояс
    b.cylinder((0, 0, 1.55), 0.16, 0.2, 12, segments=8)
    b.box((0.06, 0, 1.72), (0.05, 0.05, 0.22), 11, bevel=0.01)
    b.sphere((0.12, 0, 1.9), 0.1, 2)
    b.box((0, 0.58, 0.95), (0.46, 0.12, 0.2), 10, bevel=0.03)
    b.box((0, 0.64, 0.95), (0.34, 0.05, 0.1), 4, bevel=0.01)
    for sx in (-1, 1):
        b.box((sx * 0.58, 0.05, 0.9), (0.16, 0.34, 0.34), 12, bevel=0.04)
r.part("Body", bomb_body, (0, 0, 0.9))
r.done(front=(0, 0.8, 0.95), size=2.2)

# 6. HEALER — парящий белый шар-медик с зелёным крестом и кольцом (без ног)
r = R("Enemy_Healer")
def healer_body(b):
    b.sphere((0, 0, 1.0), 0.55, 8)
    for i in range(8):
        a = i * math.tau / 8
        b.box((math.cos(a) * 0.82, 0, 1.0 + math.sin(a) * 0.82), (0.26, 0.12, 0.26), 6, bevel=0.03, rot_z=0)
    b.box((0, 0.5, 1.0), (0.5, 0.1, 0.16), 6, bevel=0.02)
    b.box((0, 0.5, 1.0), (0.16, 0.1, 0.5), 6, bevel=0.02)
    b.cylinder((0, 0, 0.38), 0.18, 0.18, 12, segments=8)
    b.sphere((0, 0, 0.22), 0.14, 6)
    for sx in (-1, 1):
        b.sphere((sx * 0.9, -0.05, 0.55), 0.14, 7)
r.part("Body", healer_body, (0, 0, 1.0))
def healer_ant(b):
    b.box((0, -0.2, 1.6), (0.05, 0.05, 0.3), 12, bevel=0.005)
    b.sphere((0, -0.2, 1.8), 0.09, 6)
r.part("Antenna", healer_ant, (0, -0.2, 1.5))
r.done(front=(0, 0.8, 1.0), size=2.2)

# 7. SPLITTER — двойной «слизень»: два срощенных лаймовых шара, три маленьких шарика, короткие лапки
r = R("Enemy_Splitter")
r.leg("Leg_L", -0.3, 0, 0.22, 0.16, c=14, foot=14)
r.leg("Leg_R", 0.3, 0, 0.22, 0.16, c=14, foot=14)
def split_body(b):
    b.sphere((-0.34, 0, 0.7), 0.5, 15, scale=(1, 1, 0.9))
    b.sphere((0.34, 0, 0.7), 0.5, 6, scale=(1, 1, 0.9))
    b.box((0, 0, 0.7), (0.08, 0.9, 0.9), 10, bevel=0.01)                          # шов
    b.sphere((-0.2, 0.3, 1.2), 0.17, 15)
    b.sphere((0.25, 0.1, 1.22), 0.13, 6)
    b.sphere((0.0, -0.25, 1.25), 0.1, 15)
    for sx in (-1, 1):
        b.box((sx * 0.34, 0.5, 0.78), (0.3, 0.08, 0.2), 10, bevel=0.02)
        b.box((sx * 0.34, 0.54, 0.78), (0.2, 0.04, 0.1), 4, bevel=0.01)
r.part("Body", split_body, (0, 0, 0.7))
r.done(front=(0, 0.7, 0.8), size=2.0)

# 8. SUMMONER — парящая башня-мантия: ступенчатая юбка, капюшон, кристалл и орбы-руки
r = R("Enemy_Summoner")
def summ_body(b):
    b.box((0, 0, 0.4), (1.0, 1.0, 0.3), 14, bevel=0.07)
    b.box((0, 0, 0.75), (0.8, 0.8, 0.4), 5, bevel=0.07)
    b.box((0, 0, 1.2), (0.6, 0.6, 0.5), 14, bevel=0.07)
    b.box((0, 0, 1.7), (0.5, 0.5, 0.46), 5, bevel=0.09)                           # капюшон
    b.box((0, 0.2, 1.72), (0.34, 0.12, 0.2), 10, bevel=0.03)
    b.box((0, 0.26, 1.72), (0.2, 0.05, 0.08), 13, bevel=0.01)
    b.box((0, 0, 2.4), (0.24, 0.24, 0.4), 13, bevel=0.03, rot_z=0.78)
    b.box((0, 0, 2.4), (0.34, 0.34, 0.14), 4, bevel=0.01, rot_z=0.0)
    for sx in (-1, 1):
        b.sphere((sx * 0.8, 0.3, 1.1), 0.16, 13)
        b.box((sx * 0.55, 0.1, 1.15), (0.3, 0.1, 0.1), 14, bevel=0.02)
r.part("Body", summ_body, (0, 0, 0.4))
r.done(front=(0, 0.6, 1.2), size=3.0)

# 9. SNIPER — тонкий треножник с длинной винтовкой и красным глазом
r = R("Enemy_Sniper")
r.leg("Leg_FL", -0.3, 0.2, 0.8, 0.1, c=1, foot=10)
r.leg("Leg_FR", 0.3, 0.2, 0.8, 0.1, c=1, foot=10)
r.leg("Leg_BL", 0.0, -0.4, 0.8, 0.1, c=1, foot=10)
def sniper_body(b):
    b.box((0, 0, 1.0), (0.5, 0.5, 0.4), 12, bevel=0.07)
    b.box((0, 0, 1.3), (0.4, 0.4, 0.18), 1, bevel=0.05)
    b.sphere((0, 0.26, 1.02), 0.16, 2)
    b.box((0.3, 0.1, 1.1), (0.14, 1.3, 0.14), 10, bevel=0.03)                     # винтовка
    b.cylinder((0.3, 0.9, 1.1), 0.06, 0.6, 1, axis='Y', segments=8)
    b.box((0.3, 0.15, 1.28), (0.12, 0.5, 0.12), 12, bevel=0.02)                   # прицел
    b.box((0.3, -0.5, 1.02), (0.12, 0.3, 0.3), 1, bevel=0.03)
r.part("Body", sniper_body, (0, 0, 1.0))
r.done(front=(0, 1.2, 1.1), size=2.5)

# 10. MORTAR — приземистая платформа на колёсах с большой наклонной трубой
r = R("Enemy_Mortar")
for n, sx, sy in (("Leg_FL", -1, 1), ("Leg_FR", 1, 1), ("Leg_BL", -1, -1), ("Leg_BR", 1, -1)):
    def wheel(b, sx=sx, sy=sy):
        b.cylinder((sx * 0.62, sy * 0.5, 0.3), 0.3, 0.2, 10, axis='X', segments=10)
        b.cylinder((sx * 0.62, sy * 0.5, 0.3), 0.12, 0.24, 12, axis='X', segments=8)
    r.part(n, wheel, (sx * 0.62, sy * 0.5, 0.3))
def mortar_body(b):
    b.box((0, 0, 0.55), (1.1, 1.3, 0.34), 11, bevel=0.08)
    b.box((0, 0, 0.78), (0.9, 1.0, 0.1), 12, bevel=0.03)
    b.cylinder((0, -0.1, 0.9), 0.35, 0.12, 12, segments=12)
    b.box((0, -0.35, 1.45), (0.5, 0.5, 1.3), 3, bevel=0.08, rot_x=0.55)           # труба наклонена вперёд? (вверх-назад)
    b.box((0, -0.65, 2.0), (0.62, 0.62, 0.16), 12, bevel=0.04, rot_x=0.55)
    b.box((0, 0.5, 0.82), (0.5, 0.3, 0.2), 4, bevel=0.03)
    b.sphere((0.4, 0.5, 0.95), 0.18, 10)
r.part("Body", mortar_body, (0, 0, 0.55))
r.done(front=(0, 0.8, 0.6), size=3.0)

# 11. TELEPORTER — парящий портал: вертикальное кольцо с бирюзовым диском и орбитами
r = R("Enemy_Teleporter")
def tele_body(b):
    for i in range(10):
        a = i * math.tau / 10
        b.box((math.cos(a) * 0.75, 0, 1.0 + math.sin(a) * 0.75), (0.3, 0.3, 0.3), 5 if i % 2 else 14, bevel=0.05, rot_x=0)
    b.cylinder((0, 0, 1.0), 0.62, 0.08, 7, axis='Y', segments=14)
    b.cylinder((0, 0.02, 1.0), 0.3, 0.08, 8, axis='Y', segments=12)
    b.sphere((0, 0.2, 1.0), 0.14, 13)
    b.box((0, 0, 0.18), (0.5, 0.5, 0.1), 12, bevel=0.03)
    b.box((0, 0, 0.3), (0.12, 0.12, 0.2), 5, bevel=0.01)
r.part("Body", tele_body, (0, 0, 1.0))
r.done(front=(0, 0.6, 1.0), size=2.2)

# 12. BUFFER — знаменосец: двуногий, высокий шест с жёлтым флагом и парящими орбами
r = R("Enemy_Buffer")
r.leg("Leg_L", -0.2, 0, 0.5, 0.2, c=11, foot=10)
r.leg("Leg_R", 0.2, 0, 0.5, 0.2, c=11, foot=10)
def buf_body(b):
    b.cylinder((0, 0, 0.95), 0.42, 0.9, 4, segments=10)
    b.cylinder((0, 0, 1.45), 0.3, 0.2, 3, segments=10)
    b.sphere((0, 0.05, 1.7), 0.26, 4)
    lens(b, 0.4, 0.95, 0.34, 0.1, 3)
    b.box((0, -0.3, 1.8), (0.07, 0.07, 1.4), 11, bevel=0.01)                      # шест
    b.box((0.0, -0.3, 2.3), (0.04, 0.7, 0.45), 3, bevel=0.01)                     # флаг
    b.box((0.0, -0.3, 2.3), (0.05, 0.3, 0.2), 4, bevel=0.01)
    for sx in (-1, 1):
        b.sphere((sx * 0.62, 0.0, 1.2), 0.15, 4)
r.part("Body", buf_body, (0, 0, 0.95))
r.done(front=(0, 0.6, 1.0), size=3.0)

# 13. ARMORED — шестиногий краб-бронеход: широкий плоский панцирь, щит, шипы
r = R("Enemy_Armored")
for n, sx, sy in (("Leg_FL", -1, 0.7), ("Leg_FR", 1, 0.7), ("Leg_ML", -1, 0), ("Leg_MR", 1, 0), ("Leg_BL", -1, -0.7), ("Leg_BR", 1, -0.7)):
    r.leg(n, sx * 0.62, sy * 0.5, 0.34, 0.22, c=12, foot=1)
def armor_body(b):
    b.box((0, 0, 0.55), (1.5, 1.3, 0.4), 12, bevel=0.1)
    b.box((0, 0, 0.82), (1.2, 1.0, 0.18), 1, bevel=0.07)
    b.box((0, 0.75, 0.62), (1.5, 0.2, 0.7), 0, bevel=0.07)                       # щит
    b.box((0, 0.87, 0.62), (1.0, 0.04, 0.1), 2, bevel=0.01)
    lens(b, 0.4, 0.9, 0.4, 0.08, 2)
    for sx in (-1, 1):
        for i in (-0.3, 0.3):
            b.box((sx * 0.8, i, 0.95), (0.16, 0.16, 0.3), 12, bevel=0.02)
    b.box((0, -0.4, 1.0), (0.5, 0.4, 0.2), 1, bevel=0.04)
r.part("Body", armor_body, (0, 0, 0.55))
r.done(front=(0, 1.1, 0.65), size=3.0)
