"""Процедурная деревня: ландшафт, библиотека ассетов (домики, деревья, ели, камни, кусты, забор, колодец,
бочки, ящики, стога) и их расстановка по рельефу. Всё строится кодом, без внешних файлов.

Запуск (фоном, из корня проекта):
    F:/blender-x64/blender-4.5.15-windows-x64/blender.exe -b -P tools/blender/procedural_village.py -- --seed 7
Параметры после "--":
    --seed N        зерно генерации (одинаковое зерно -> одинаковый результат)
    --size M        сторона ландшафта в метрах (по умолчанию 200)
    --res N         число квадов сетки по стороне (по умолчанию 200)
    --houses N      сколько домиков поставить (по умолчанию 14)
    --trees N       сколько деревьев (по умолчанию 260)
    --rocks N       сколько камней (по умолчанию 70)
    --out DIR       папка результата (по умолчанию docs/art/ProceduralVillage)
    --name NAME     базовое имя файлов (по умолчанию village_seed<seed>)
    --samples N     сэмплы Cycles для превью (по умолчанию 24)
    --no-render     не рендерить превью
    --no-fbx        не экспортировать FBX
Результат: <name>.blend, <name>.fbx (оси под Unity), <name>_overview.png, <name>_village.png, <name>.json
(позиции домиков, центр деревни, уровень воды). Можно запускать и из окна Blender (Scripting -> Run Script):
тогда создаётся новая сцена, текущая не трогается.
"""
import argparse
import json
import math
import os
import random
import sys
import time

import bmesh
import bpy
from mathutils import Matrix, Vector, noise


# ---------------------------------------------------------------- параметры

def script_dir():
    try:
        return os.path.dirname(os.path.abspath(__file__))
    except NameError:
        return os.getcwd()


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    default_out = os.path.normpath(os.path.join(script_dir(), "..", "..", "docs", "art", "ProceduralVillage"))
    if not os.path.isdir(os.path.dirname(default_out)):
        default_out = os.path.join(os.getcwd(), "ProceduralVillage")
    p = argparse.ArgumentParser(prog="procedural_village")
    p.add_argument("--seed", type=int, default=7)
    p.add_argument("--size", type=float, default=200.0)
    p.add_argument("--res", type=int, default=200)
    p.add_argument("--houses", type=int, default=14)
    p.add_argument("--trees", type=int, default=260)
    p.add_argument("--rocks", type=int, default=70)
    p.add_argument("--out", default=default_out)
    p.add_argument("--name", default=None)
    p.add_argument("--samples", type=int, default=24)
    p.add_argument("--no-render", action="store_true")
    p.add_argument("--no-fbx", action="store_true")
    a = p.parse_args(argv)
    if a.name is None:
        a.name = "village_seed%d" % a.seed
    return a


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


# ---------------------------------------------------------------- материалы

MATS = {}


def principled(m):
    """Principled BSDF материала; создаёт узлы, если их нет (пустой factory-старт в фоне)."""
    m.use_nodes = True
    nt = m.node_tree
    for n in nt.nodes:
        if n.type == 'BSDF_PRINCIPLED':
            return n
    out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None) or nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(b.outputs["BSDF"], out.inputs["Surface"])
    return b


def mat(name, color, rough=0.8, metal=0.0, emit=0.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new("PV_" + name)
    b = principled(m)
    b.inputs["Base Color"].default_value = (*color, 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit > 0.0:
        b.inputs["Emission Color"].default_value = (*color, 1.0)
        b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*color, 1.0)
    MATS[name] = m
    return m


def terrain_material():
    m = bpy.data.materials.new("PV_Terrain")
    b = principled(m)
    nt = m.node_tree
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Col"
    nt.links.new(vc.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.95
    m.diffuse_color = (0.3, 0.5, 0.2, 1.0)
    return m


def water_material():
    m = bpy.data.materials.new("PV_Water")
    b = principled(m)
    b.inputs["Base Color"].default_value = (0.05, 0.18, 0.22, 1.0)
    b.inputs["Roughness"].default_value = 0.04
    b.inputs["IOR"].default_value = 1.33
    m.diffuse_color = (0.05, 0.18, 0.22, 1.0)
    return m


# ---------------------------------------------------------------- сборщик сеток

class MeshBuilder:
    """Копит вершины и грани нескольких деталей и собирает из них одну сетку с несколькими материалами."""

    def __init__(self):
        self.verts = []
        self.faces = []
        self.fmat = []
        self.mats = []

    def _mi(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def add(self, verts, faces, m, matrix=None):
        base = len(self.verts)
        mi = self._mi(m)
        for v in verts:
            v = Vector(v)
            if matrix is not None:
                v = matrix @ v
            self.verts.append(v)
        for f in faces:
            self.faces.append([base + i for i in f])
            self.fmat.append(mi)

    def box(self, center, size, m, rot_z=0.0, rot_x=0.0):
        sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
        vs = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
              (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz)]
        fs = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        M = Matrix.Translation(Vector(center)) @ Matrix.Rotation(rot_z, 4, 'Z') @ Matrix.Rotation(rot_x, 4, 'X')
        self.add(vs, fs, m, M)

    def prism_gable(self, w, d, z0, rh, m):
        """Треугольный фронтон: основание w*d на высоте z0, конёк вдоль X на высоте z0+rh."""
        hw, hd = w / 2, d / 2
        vs = [(-hw, -hd, z0), (hw, -hd, z0), (hw, hd, z0), (-hw, hd, z0), (-hw, 0, z0 + rh), (hw, 0, z0 + rh)]
        fs = [(0, 3, 2, 1), (0, 1, 5, 4), (2, 3, 4, 5), (3, 0, 4), (1, 2, 5)]
        self.add(vs, fs, m)

    def cylinder(self, base, r1, r2, h, m, seg=10, rot_z=0.0):
        bx, by, bz = base
        vs, fs = [], []
        for i in range(seg):
            a = 2 * math.pi * i / seg + rot_z
            vs.append((bx + r1 * math.cos(a), by + r1 * math.sin(a), bz))
        if r2 < 1e-4:
            vs.append((bx, by, bz + h))
            apex = seg
            for i in range(seg):
                fs.append((i, (i + 1) % seg, apex))
        else:
            for i in range(seg):
                a = 2 * math.pi * i / seg + rot_z
                vs.append((bx + r2 * math.cos(a), by + r2 * math.sin(a), bz + h))
            for i in range(seg):
                j = (i + 1) % seg
                fs.append((i, j, seg + j, seg + i))
            fs.append(tuple(range(seg, 2 * seg)))
        fs.append(tuple(reversed(range(seg))))
        self.add(vs, fs, m)

    def blob(self, center, radius, m, scale=(1, 1, 1), rough=0.25, freq=1.4, off=(0, 0, 0), seg=10, rings=7):
        """Неровная сфера: радиус возмущён шумом -> крона, камень, куст, стог."""
        cx, cy, cz = center
        off = Vector(off)
        vs, fs = [], []

        def point(d):
            r = radius * (1.0 + rough * noise.noise(d * freq + off))
            return (cx + d.x * r * scale[0], cy + d.y * r * scale[1], cz + d.z * r * scale[2])

        vs.append(point(Vector((0, 0, 1))))
        for ri in range(1, rings):
            th = math.pi * ri / rings
            for si in range(seg):
                ph = 2 * math.pi * si / seg
                vs.append(point(Vector((math.sin(th) * math.cos(ph), math.sin(th) * math.sin(ph), math.cos(th)))))
        vs.append(point(Vector((0, 0, -1))))
        bottom = len(vs) - 1

        def ring(ri, si):
            return 1 + (ri - 1) * seg + (si % seg)

        for si in range(seg):
            fs.append((0, ring(1, si), ring(1, si + 1)))
        for ri in range(1, rings - 1):
            for si in range(seg):
                fs.append((ring(ri, si), ring(ri + 1, si), ring(ri + 1, si + 1), ring(ri, si + 1)))
        for si in range(seg):
            fs.append((bottom, ring(rings - 1, si + 1), ring(rings - 1, si)))
        self.add(vs, fs, m)

    def build(self, name, smooth=False):
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        for m in self.mats:
            me.materials.append(m)
        me.polygons.foreach_set("material_index", self.fmat)
        me.polygons.foreach_set("use_smooth", [smooth] * len(me.polygons))
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
        me.validate()
        return me


# ---------------------------------------------------------------- ассеты

WALL_COLORS = [(0.86, 0.82, 0.72), (0.80, 0.70, 0.52), (0.62, 0.42, 0.26), (0.88, 0.86, 0.82), (0.74, 0.58, 0.40)]
ROOF_COLORS = [(0.55, 0.16, 0.10), (0.32, 0.20, 0.14), (0.24, 0.27, 0.32), (0.62, 0.36, 0.16), (0.40, 0.12, 0.10)]


def make_house(idx, rng):
    """Домик: фундамент, стены, фахверк, фронтон, двускатная крыша, конёк, дверь, крыльцо, окна, труба.
    Дверь смотрит в локальный -Y. Пол первого этажа на z=0.3, основание на z=0."""
    b = MeshBuilder()
    stone = mat("Stone", (0.42, 0.40, 0.37), 0.9)
    wood = mat("WoodDark", (0.22, 0.13, 0.07), 0.85)
    glass = mat("WindowLit", (1.0, 0.72, 0.35), 0.3, emit=1.5)
    wall = mat("Wall%d" % (idx % len(WALL_COLORS)), WALL_COLORS[idx % len(WALL_COLORS)], 0.9)
    roof = mat("Roof%d" % (idx % len(ROOF_COLORS)), ROOF_COLORS[(idx * 3 + 1) % len(ROOF_COLORS)], 0.75)

    w = rng.uniform(4.5, 7.5)
    d = rng.uniform(4.0, 6.0)
    floors = 2 if rng.random() < 0.35 else 1
    floor_h = rng.uniform(2.5, 3.0)
    wall_h = floor_h * floors
    z_floor = 0.3
    zt = z_floor + wall_h

    b.box((0, 0, -0.15), (w + 0.35, d + 0.35, 0.9), stone)                 # фундамент
    b.box((0, 0, z_floor + wall_h / 2), (w, d, wall_h), wall)              # стены
    for sx in (-1, 1):                                                     # угловые брусья
        for sy in (-1, 1):
            b.box((sx * w / 2, sy * d / 2, z_floor + wall_h / 2), (0.24, 0.24, wall_h), wood)
    if floors == 2:                                                        # пояс между этажами
        b.box((0, 0, z_floor + floor_h), (w + 0.1, d + 0.1, 0.2), wood)

    rh = d * rng.uniform(0.35, 0.6)
    b.prism_gable(w, d, zt, rh, wall)                                      # фронтоны
    o, t = 0.45, 0.18
    half = d / 2 + o
    drop = o * rh / (d / 2)
    ang = math.atan2(rh + drop, half)
    L = math.hypot(half, rh + drop)
    zc = (zt - drop + zt + rh) / 2
    for side in (-1, 1):                                                   # скаты крыши
        a = ang if side < 0 else -ang
        n = Vector((0, side * math.sin(ang), math.cos(ang)))
        c = Vector((0, side * half / 2, zc)) + n * (t / 2)
        b.box(c, (w + 2 * o, L, t), roof, rot_x=a)
    b.box((0, 0, zt + rh + t * 0.7), (w + 2 * o + 0.1, 0.28, 0.28), wood)  # конёк

    door_x = rng.uniform(-w * 0.2, w * 0.2)
    b.box((door_x, -d / 2 - 0.04, z_floor + 0.95), (0.95, 0.14, 1.9), wood)            # дверь
    b.box((door_x, -d / 2 - 0.45, 0.18), (1.5, 0.8, 0.25), stone)                      # крыльцо

    def window(x, y, z, side_axis):
        if side_axis:
            b.box((x, y, z), (0.14, 1.0, 1.0), wood)
            b.box((x, y, z), (0.18, 0.75, 0.75), glass)
        else:
            b.box((x, y, z), (1.0, 0.14, 1.0), wood)
            b.box((x, y, z), (0.75, 0.18, 0.75), glass)

    for f in range(floors):
        zw = z_floor + f * floor_h + floor_h * 0.55
        n_front = max(1, int(w // 2.2))
        for i in range(n_front):
            x = -w / 2 + w * (i + 0.5) / n_front
            if f > 0 or abs(x - door_x) > 1.1:
                window(x, -d / 2, zw, False)
            window(x, d / 2, zw, False)
        for sx in (-1, 1):
            if rng.random() < 0.8:
                window(sx * w / 2, 0, zw, True)

    if rng.random() < 0.75:                                                # труба
        ch = rh + 1.3
        b.box((w * rng.uniform(-0.3, 0.3), d * 0.22, zt + ch / 2 - 0.2), (0.6, 0.6, ch), stone)

    me = b.build("House_%02d" % idx)
    radius = 0.5 * math.hypot(w + 2 * o, d + 2 * o) + 0.8
    return me, radius, (w, d)


def make_tree(idx, rng):
    b = MeshBuilder()
    bark = mat("Bark", (0.25, 0.16, 0.09), 0.9)
    leaves = mat("Leaves%d" % (idx % 3), [(0.16, 0.36, 0.10), (0.22, 0.42, 0.12), (0.30, 0.40, 0.10)][idx % 3], 0.8)
    h = rng.uniform(2.6, 3.8)
    b.cylinder((0, 0, -0.3), 0.28, 0.16, h + 0.3, bark, seg=7)
    for k in range(rng.randint(2, 4)):
        r = rng.uniform(1.3, 2.1)
        c = (rng.uniform(-0.7, 0.7), rng.uniform(-0.7, 0.7), h + rng.uniform(0.3, 1.4))
        b.blob(c, r, leaves, scale=(1, 1, 0.85), rough=0.3, off=(idx * 3.1, k * 7.3, 0), seg=9, rings=6)
    return b.build("Tree_%02d" % idx)


def make_pine(idx, rng):
    b = MeshBuilder()
    bark = mat("Bark", (0.25, 0.16, 0.09), 0.9)
    needles = mat("Needles", (0.08, 0.24, 0.12), 0.85)
    b.cylinder((0, 0, -0.3), 0.25, 0.12, 2.0, bark, seg=6)
    z = 1.0
    r = rng.uniform(2.0, 2.6)
    for k in range(rng.randint(3, 5)):
        hh = r * 1.3
        b.cylinder((0, 0, z), r, 0.0, hh, needles, seg=9, rot_z=k * 0.6)
        z += hh * 0.45
        r *= 0.75
    return b.build("Pine_%02d" % idx)


def make_rock(idx, rng):
    b = MeshBuilder()
    rock = mat("Rock%d" % (idx % 2), [(0.45, 0.44, 0.42), (0.38, 0.36, 0.33)][idx % 2], 0.95)
    r = rng.uniform(0.8, 1.4)
    b.blob((0, 0, r * 0.15), r, rock, scale=(1.0, rng.uniform(0.7, 1.0), rng.uniform(0.55, 0.8)),
           rough=0.45, freq=1.1, off=(idx * 5.7, 1.3, 2.1), seg=8, rings=6)
    return b.build("Rock_%02d" % idx)


def make_bush(idx, rng):
    b = MeshBuilder()
    leaves = mat("Bush", (0.18, 0.34, 0.10), 0.85)
    for k in range(rng.randint(2, 3)):
        r = rng.uniform(0.5, 0.8)
        b.blob((rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), r * 0.5), r, leaves,
               scale=(1, 1, 0.8), rough=0.3, off=(idx * 2.3, k * 4.1, 0), seg=8, rings=5)
    return b.build("Bush_%02d" % idx)


def make_fence():
    """Пролёт забора 3 м вдоль X, начало в x=0."""
    b = MeshBuilder()
    wood = mat("WoodLight", (0.45, 0.30, 0.17), 0.85)
    for x in (0.0, 1.5, 3.0):
        b.box((x, 0, 0.45), (0.14, 0.14, 1.3), wood)
    for z in (0.45, 0.85):
        b.box((1.5, 0, z), (3.1, 0.06, 0.12), wood)
    return b.build("Fence")


def make_well():
    b = MeshBuilder()
    stone = mat("Stone", (0.42, 0.40, 0.37), 0.9)
    wood = mat("WoodDark", (0.22, 0.13, 0.07), 0.85)
    roof = mat("Roof0", ROOF_COLORS[1], 0.75)
    water = mat("WellWater", (0.04, 0.12, 0.16), 0.05)
    b.cylinder((0, 0, -0.3), 1.2, 1.15, 1.2, stone, seg=14)
    b.cylinder((0, 0, 0.75), 0.95, 0.95, 0.02, water, seg=14)
    for sx in (-1, 1):
        b.box((sx * 0.95, 0, 1.6), (0.16, 0.16, 2.0), wood)
    b.box((0, 0, 2.05), (2.1, 0.12, 0.12), wood)
    for side in (-1, 1):
        b.box((0, side * 0.45, 2.75), (2.5, 1.1, 0.1), roof, rot_x=-side * 0.6)
    return b.build("Well")


def make_barrel():
    b = MeshBuilder()
    wood = mat("WoodLight", (0.45, 0.30, 0.17), 0.85)
    iron = mat("Iron", (0.18, 0.18, 0.19), 0.5, metal=0.8)
    b.cylinder((0, 0, 0), 0.38, 0.42, 0.5, wood, seg=12)
    b.cylinder((0, 0, 0.5), 0.42, 0.38, 0.5, wood, seg=12)
    for z in (0.15, 0.85):
        b.cylinder((0, 0, z - 0.03), 0.43, 0.43, 0.06, iron, seg=12)
    return b.build("Barrel")


def make_crate():
    b = MeshBuilder()
    wood = mat("WoodLight", (0.45, 0.30, 0.17), 0.85)
    dark = mat("WoodDark", (0.22, 0.13, 0.07), 0.85)
    b.box((0, 0, 0.4), (0.8, 0.8, 0.8), wood)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((sx * 0.38, sy * 0.38, 0.4), (0.08, 0.08, 0.82), dark)
    return b.build("Crate")


def make_haystack():
    b = MeshBuilder()
    hay = mat("Hay", (0.78, 0.62, 0.25), 0.95)
    b.blob((0, 0, 0.6), 1.2, hay, scale=(1, 1, 1.1), rough=0.12, freq=2.0, seg=12, rings=7)
    return b.build("Haystack")


# ---------------------------------------------------------------- рельеф

class Terrain:
    def __init__(self, size, seed, rng):
        self.size = size
        self.half = size / 2
        self.o1 = Vector((rng.uniform(-500, 500), rng.uniform(-500, 500), seed * 0.37))
        self.o2 = Vector((rng.uniform(-500, 500), rng.uniform(-500, 500), seed * 0.71))
        self.water = -2.6
        ang = rng.uniform(0, 2 * math.pi)
        dist = size * rng.uniform(0.18, 0.26)
        self.lake = (math.cos(ang) * dist, math.sin(ang) * dist, size * 0.09)
        self.sites = []   # (x, y, h, r) площадки под домики и колодец
        self.paths = []   # отрезки ((x1, y1), (x2, y2))

    def base(self, x, y):
        n = noise.fractal(Vector((x / 55.0, y / 55.0, 0)) + self.o1, 1.0, 2.0, 6, noise_basis='PERLIN_ORIGINAL')
        h = n * 3.2
        r = math.hypot(x, y) / self.half
        mtn = smoothstep(0.5, 1.0, r)
        ridge = 1.0 - abs(noise.noise(Vector((x / 40.0, y / 40.0, 0)) + self.o2))
        h += mtn * (ridge * ridge * 30.0 + 10.0)
        lx, ly, lr = self.lake
        dl = math.hypot(x - lx, y - ly)
        h -= 5.5 * (1.0 - smoothstep(lr * 0.4, lr, dl))
        return h

    def height(self, x, y):
        h = self.base(x, y)
        for sx, sy, sh, sr in self.sites:
            dd = math.hypot(x - sx, y - sy)
            if dd < sr + 6.0:
                h = lerp(h, sh, 1.0 - smoothstep(sr, sr + 6.0, dd))
        return h

    def path_dist(self, x, y):
        best = 1e9
        for (x1, y1), (x2, y2) in self.paths:
            dx, dy = x2 - x1, y2 - y1
            ll = dx * dx + dy * dy
            t = 0.0 if ll < 1e-6 else max(0.0, min(1.0, ((x - x1) * dx + (y - y1) * dy) / ll))
            best = min(best, math.hypot(x - (x1 + t * dx), y - (y1 + t * dy)))
        return best

    def site_span(self, x, y, r):
        hs = [self.base(x + r * math.cos(a), y + r * math.sin(a)) for a in [i * math.pi / 4 for i in range(8)]]
        hs.append(self.base(x, y))
        return min(hs), max(hs), sum(hs) / len(hs)

    def build_mesh(self, res, mat_terrain):
        n = res + 1
        step = self.size / res
        verts = []
        for j in range(n):
            y = -self.half + j * step
            for i in range(n):
                x = -self.half + i * step
                verts.append((x, y, self.height(x, y)))
        faces = []
        for j in range(res):
            for i in range(res):
                a = j * n + i
                faces.append((a, a + 1, a + n + 1, a + n))
        me = bpy.data.meshes.new("Terrain")
        me.from_pydata(verts, [], faces)
        me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
        me.materials.append(mat_terrain)
        me.update()

        # цвет вершин: трава / тёмная трава / песок / земля дорожек / скала / снег
        grass_a = Vector((0.20, 0.38, 0.09))
        grass_b = Vector((0.33, 0.45, 0.13))
        sand = Vector((0.62, 0.55, 0.38))
        dirt = Vector((0.36, 0.26, 0.15))
        rock = Vector((0.13, 0.12, 0.11))
        snow = Vector((0.92, 0.93, 0.95))
        vcol = []
        for v in me.vertices:
            x, y, z = v.co
            slope = 1.0 - v.normal.z
            g = 0.5 + 0.5 * noise.noise(Vector((x / 12.0, y / 12.0, 3.3)) + self.o1)
            c = grass_a.lerp(grass_b, g)
            c = c.lerp(sand, 1.0 - smoothstep(self.water + 0.2, self.water + 1.2, z))
            near_site = 0.0
            for sx, sy, sh, sr in self.sites:
                near_site = max(near_site, 1.0 - smoothstep(sr * 0.6, sr + 1.5, math.hypot(x - sx, y - sy)))
            c = c.lerp(dirt, near_site * 0.55)
            c = c.lerp(dirt, (1.0 - smoothstep(0.8, 1.8, self.path_dist(x, y))) * 0.9)
            c = c.lerp(rock, smoothstep(0.12, 0.3, slope))
            c = c.lerp(snow, smoothstep(30.0, 35.0, z + 3.0 * g) * (1.0 - smoothstep(0.35, 0.5, slope)))
            vcol.append(c)
        attr = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
        data = []
        for loop in me.loops:
            c = vcol[loop.vertex_index]
            data.extend((c.x, c.y, c.z, 1.0))
        attr.data.foreach_set("color", data)
        me.color_attributes.active_color = attr
        return me


# ---------------------------------------------------------------- сцена

def setup_scene(name):
    if bpy.app.background:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = bpy.context.scene
        scene.name = name
    else:
        scene = bpy.data.scenes.new(name)
        if bpy.context.window is not None:
            bpy.context.window.scene = scene
    return scene


def new_collection(scene, name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c


def place(coll, mesh, name, loc, rot_z=0.0, scale=1.0):
    o = bpy.data.objects.new(name, mesh)
    o.location = loc
    o.rotation_euler = (0, 0, rot_z)
    o.scale = (scale, scale, scale)
    coll.objects.link(o)
    return o


def setup_world_and_light(scene):
    world = bpy.data.worlds.new("PV_Sky")
    world.use_nodes = True
    wnt = world.node_tree
    bg = next((n for n in wnt.nodes if n.type == 'BACKGROUND'), None)
    if bg is None:
        wout = next((n for n in wnt.nodes if n.type == 'OUTPUT_WORLD'), None) or wnt.nodes.new("ShaderNodeOutputWorld")
        bg = wnt.nodes.new("ShaderNodeBackground")
        wnt.links.new(bg.outputs["Background"], wout.inputs["Surface"])
    bg.inputs["Color"].default_value = (0.50, 0.66, 0.90, 1.0)
    bg.inputs["Strength"].default_value = 0.9
    scene.world = world
    sun_data = bpy.data.lights.new("Sun", 'SUN')
    sun_data.energy = 4.0
    sun_data.angle = math.radians(2.0)
    sun_data.color = (1.0, 0.95, 0.86)
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(50), math.radians(8), math.radians(35))
    scene.collection.objects.link(sun)


def add_camera(scene, name, loc, target, lens):
    cam_data = bpy.data.cameras.new(name)
    cam_data.lens = lens
    cam_data.clip_end = 2000
    cam = bpy.data.objects.new(name, cam_data)
    cam.location = loc
    direction = Vector(target) - Vector(loc)
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.collection.objects.link(cam)
    return cam


def setup_render(scene, samples):
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'


# ---------------------------------------------------------------- генерация

def generate(args):
    t0 = time.time()
    rng = random.Random(args.seed)
    noise.seed_set(args.seed)
    MATS.clear()
    scene = setup_scene("ProceduralVillage")
    os.makedirs(args.out, exist_ok=True)

    terrain = Terrain(args.size, args.seed, rng)
    lib = new_collection(scene, "AssetLibrary")
    village = new_collection(scene, "Village")
    nature = new_collection(scene, "Nature")
    land = new_collection(scene, "Landscape")

    # --- библиотека ассетов (по одному образцу на вариант, со сдвигом за пределы карты)
    houses = [make_house(i, rng) for i in range(6)]
    trees = [make_tree(i, rng) for i in range(4)]
    pines = [make_pine(i, rng) for i in range(3)]
    rocks = [make_rock(i, rng) for i in range(5)]
    bushes = [make_bush(i, rng) for i in range(3)]
    fence, well, barrel, crate, hay = make_fence(), make_well(), make_barrel(), make_crate(), make_haystack()
    lib_meshes = [h[0] for h in houses] + trees + pines + rocks + bushes + [fence, well, barrel, crate, hay]
    for k, me in enumerate(lib_meshes):
        place(lib, me, "LIB_" + me.name, (args.size + 20 + (k % 6) * 12, (k // 6) * 12, 0))

    # --- центр деревни: ровное место, выше воды, ближе к середине
    best = None
    for _ in range(400):
        x, y = rng.uniform(-args.size * 0.15, args.size * 0.15), rng.uniform(-args.size * 0.15, args.size * 0.15)
        lo, hi, avg = terrain.site_span(x, y, 8.0)
        if lo < terrain.water + 1.0:
            continue
        score = (hi - lo) + math.hypot(x, y) * 0.02
        if best is None or score < best[0]:
            best = (score, x, y, avg)
    if best is None:
        best = (0, 0.0, 0.0, terrain.water + 2.0)
    _, cx, cy, ch = best
    terrain.sites.append((cx, cy, ch, 6.0))   # площадь у колодца без деревьев

    # --- площадки под домики
    placed = []
    attempts = 0
    while len(placed) < args.houses and attempts < 6000:
        attempts += 1
        variant = rng.randrange(len(houses))
        me, radius, _ = houses[variant]
        ring_max = 14.0 + 3.2 * args.houses ** 0.5 * 2.2
        ang = rng.uniform(0, 2 * math.pi)
        dist = rng.uniform(11.0, ring_max)
        x, y = cx + math.cos(ang) * dist, cy + math.sin(ang) * dist
        if math.hypot(x, y) > terrain.half * 0.6:
            continue
        lo, hi, avg = terrain.site_span(x, y, radius)
        if lo < terrain.water + 0.8 or hi - lo > 2.2:
            continue
        if any(math.hypot(x - px, y - py) < radius + pr + 2.5 for px, py, _, pr in terrain.sites):
            continue
        terrain.sites.append((x, y, avg, radius))
        placed.append((variant, x, y, avg, radius))

    # дорожки от дверей к колодцу (дверь в локальном -Y, разворачиваем домик к центру)
    house_records = []
    for variant, x, y, h, radius in placed:
        dx, dy = cx - x, cy - y
        rot = math.atan2(dx, -dy) + rng.uniform(-0.25, 0.25)
        d = houses[variant][2][1]
        door = (x + math.sin(rot) * (d / 2 + 0.9), y - math.cos(rot) * (d / 2 + 0.9))
        terrain.paths.append((door, (cx, cy)))
        house_records.append((variant, x, y, h, rot, radius))

    # --- ландшафт и вода
    t_me = terrain.build_mesh(args.res, terrain_material())
    place(land, t_me, "Terrain", (0, 0, 0))
    w_me = MeshBuilder()
    w_me.add([(-terrain.half, -terrain.half, 0), (terrain.half, -terrain.half, 0),
              (terrain.half, terrain.half, 0), (-terrain.half, terrain.half, 0)], [(0, 1, 2, 3)], water_material())
    place(land, w_me.build("Water"), "Water", (0, 0, terrain.water))

    # --- домики, колодец и мелочи у домов
    place(village, well, "Well", (cx, cy, ch))
    for n, (variant, x, y, h, rot, radius) in enumerate(house_records):
        place(village, houses[variant][0], "House_%02d" % n, (x, y, h), rot)
        w, d = houses[variant][2]
        c, s = math.cos(rot), math.sin(rot)

        def world(lx, ly):
            return x + lx * c - ly * s, y + lx * s + ly * c

        r = rng.random()
        if r < 0.45:   # забор за домом
            for k in (-1, 0):
                fx, fy = world(k * 3.0, d / 2 + 2.2)
                place(village, fence, "Fence_%02d_%d" % (n, k), (fx, fy, terrain.height(fx, fy)), rot)
        props = [(barrel, "Barrel"), (crate, "Crate"), (hay, "Haystack")]
        for k in range(rng.randint(1, 3)):
            pm, pn = rng.choice(props)
            side = rng.choice((-1, 1))
            lx, ly = side * (w / 2 + rng.uniform(0.8, 1.6)), rng.uniform(-d / 2, d / 2)
            px, py = world(lx, ly)
            place(village, pm, "%s_%02d_%d" % (pn, n, k), (px, py, terrain.height(px, py) - 0.05),
                  rng.uniform(0, 6.28), rng.uniform(0.85, 1.15))

    # --- природа: лес кластерами, ели выше по склонам, камни, кусты
    def free_spot(x, y, clearance):
        if math.hypot(x, y) > terrain.half * 0.97:
            return False
        if any(math.hypot(x - sx, y - sy) < sr + clearance for sx, sy, _, sr in terrain.sites):
            return False
        return terrain.path_dist(x, y) > clearance + 0.8

    count, tries = 0, 0
    while count < args.trees and tries < args.trees * 40:
        tries += 1
        x, y = rng.uniform(-terrain.half, terrain.half), rng.uniform(-terrain.half, terrain.half)
        z = terrain.height(x, y)
        if z < terrain.water + 0.4 or z > 27.0 or not free_spot(x, y, 2.5):
            continue
        forest = noise.noise(Vector((x / 28.0, y / 28.0, 9.1)) + terrain.o2)
        if forest < -0.05 and rng.random() > 0.08:
            continue
        pine = rng.random() < smoothstep(2.0, 14.0, z) * 0.85 + 0.1
        me = rng.choice(pines) if pine else rng.choice(trees)
        place(nature, me, "%s_%03d" % ("Pine" if pine else "Tree", count), (x, y, z - 0.15),
              rng.uniform(0, 6.28), rng.uniform(0.75, 1.35))
        count += 1
    n_trees = count

    count, tries = 0, 0
    while count < args.rocks and tries < args.rocks * 40:
        tries += 1
        x, y = rng.uniform(-terrain.half, terrain.half), rng.uniform(-terrain.half, terrain.half)
        z = terrain.height(x, y)
        if not free_spot(x, y, 1.5):
            continue
        steep = abs(terrain.height(x + 1, y) - z) + abs(terrain.height(x, y + 1) - z)
        if steep < 0.25 and rng.random() > 0.3:
            continue
        place(nature, rng.choice(rocks), "Rock_%03d" % count, (x, y, z - 0.25),
              rng.uniform(0, 6.28), rng.uniform(0.6, 2.4))
        count += 1
    n_rocks = count

    count = 0
    for k in range(args.trees // 2):
        x, y = rng.uniform(-terrain.half * 0.7, terrain.half * 0.7), rng.uniform(-terrain.half * 0.7, terrain.half * 0.7)
        z = terrain.height(x, y)
        if z < terrain.water + 0.3 or not free_spot(x, y, 0.6):
            continue
        place(nature, rng.choice(bushes), "Bush_%03d" % count, (x, y, z - 0.1),
              rng.uniform(0, 6.28), rng.uniform(0.7, 1.4))
        count += 1
    n_bushes = count

    # библиотеку скрываем из вида и рендера, но сохраняем в .blend для будущих работ
    scene.view_layers[0].layer_collection.children[lib.name].exclude = True

    setup_world_and_light(scene)
    setup_render(scene, args.samples)
    cam_over = add_camera(scene, "Cam_Overview", (cx + args.size * 0.42, cy - args.size * 0.55, ch + args.size * 0.38),
                          (cx, cy, ch), 30)
    cam_vil = add_camera(scene, "Cam_Village", (cx + 28.0, cy - 36.0, ch + 20.0), (cx, cy, ch + 1.0), 32)
    scene.camera = cam_over

    base = os.path.join(args.out, args.name)
    info = {
        "seed": args.seed, "size": args.size, "res": args.res,
        "water_level": terrain.water, "village_center": [cx, cy, ch],
        "houses": [{"variant": v, "pos": [x, y, h], "rot_z_deg": math.degrees(r), "radius": rad}
                   for v, x, y, h, r, rad in house_records],
        "counts": {"houses": len(house_records), "trees": n_trees, "rocks": n_rocks, "bushes": n_bushes,
                   "objects": len(scene.objects)},
        "note": "Blender Z-up; в FBX оси под Unity (-Z вперёд, Y вверх)",
    }

    bpy.ops.wm.save_as_mainfile(filepath=base + ".blend", copy=not bpy.app.background)
    if not args.no_fbx:
        bpy.ops.object.select_all(action='DESELECT')
        for coll in (land, village, nature):
            for o in coll.objects:
                o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=base + ".fbx", use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', colors_type='SRGB',
                                 object_types={'MESH'}, use_mesh_modifiers=True)
    if not args.no_render:
        for cam, suffix in ((cam_over, "overview"), (cam_vil, "village")):
            scene.camera = cam
            scene.render.filepath = "%s_%s.png" % (base, suffix)
            bpy.ops.render.render(write_still=True)
        scene.camera = cam_over

    info["seconds"] = round(time.time() - t0, 1)
    with open(base + ".json", "w", encoding="utf-8") as f:
        json.dump(info, f, ensure_ascii=False, indent=2)
    print("PROCEDURAL_VILLAGE OK", json.dumps(info["counts"]), "->", base, "(%.1f s)" % info["seconds"])
    return info


if __name__ == "__main__":
    generate(parse_args())
