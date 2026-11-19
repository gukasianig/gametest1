"""Объёмное поле арены «Завод»: плиты пола с фасками и болтами, разметка, массивные борта, угловые башни, скалы вокруг.
Играбельная поверхность плоская (верх плит на y=0) — под физику и поиск пути; весь рельеф вне арены (|x|,|z| > 110).
Запуск: blender -b -P make_arena_factory.py  (Blender 4.x). Результат: Assets/Art/Arena/ArenaFactory.fbx + ArenaFactory_Palette.png,
превью docs/art/Arena/."""
import math
import random
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT_ASSET = ROOT / "Assets/Art/Arena"
OUT_DOC = ROOT / "docs/art/Arena"
OUT_ASSET.mkdir(parents=True, exist_ok=True)
OUT_DOC.mkdir(parents=True, exist_ok=True)

HALF = 110.0          # половина арены (как ArenaLayout.halfSize)
PLATES = 20           # плит по стороне
PLATE = 2 * HALF / PLATES

# ---------------------------------------------------------------- палитра 8x8
def ramp(a, b, n):
    return [tuple(a[i] + (b[i] - a[i]) * k / (n - 1) for i in range(3)) for k in range(n)]

PALETTE = (
    ramp((0.10, 0.14, 0.15), (0.34, 0.41, 0.40), 8) +      # 0-7   плиты
    ramp((0.16, 0.21, 0.21), (0.46, 0.52, 0.48), 8) +      # 8-15  вставки плит
    ramp((0.08, 0.11, 0.15), (0.30, 0.38, 0.46), 8) +      # 16-23 борт
    ramp((0.12, 0.10, 0.10), (0.52, 0.45, 0.40), 8) +      # 24-31 скалы
    ramp((0.28, 0.12, 0.06), (0.78, 0.42, 0.18), 8) +      # 32-39 ржавчина
    ramp((0.62, 0.45, 0.06), (0.98, 0.78, 0.18), 4) +      # 40-43 жёлтый
    ramp((0.04, 0.04, 0.05), (0.14, 0.14, 0.16), 4) +      # 44-47 чёрный
    ramp((0.08, 0.45, 0.42), (0.55, 1.00, 0.88), 4) +      # 48-51 бирюзовое свечение
    ramp((0.02, 0.03, 0.03), (0.07, 0.09, 0.09), 4) +      # 52-55 щели
    ramp((0.18, 0.18, 0.20), (0.42, 0.42, 0.45), 4) +      # 56-59 кабели/решётки
    [(0.9, 0.12, 0.10), (1.0, 0.45, 0.3), (0.9, 0.92, 0.95), (0.55, 0.58, 0.62)]  # 60-63 красный, белый
)
PLATE0, INSET0, WALL0, ROCK0, RUST0, YEL0, BLK0, GLOW0, GAP0, CABLE0 = 0, 8, 16, 24, 32, 40, 44, 48, 52, 56
RED, ORANGE, WHITE, GREY = 60, 61, 62, 63


def make_palette_image():
    img = bpy.data.images.new("ArenaFactory_Palette", width=128, height=128, alpha=False)
    px = []
    for y in range(128):
        for x in range(128):
            c = PALETTE[(y // 16) * 8 + x // 16]
            px.extend((*c, 1.0))
    img.pixels.foreach_set(px)
    img.filepath_raw = str(OUT_ASSET / "ArenaFactory_Palette.png")
    img.file_format = 'PNG'
    img.save()
    img.pack()
    return img


def make_material(img):
    mat = bpy.data.materials.new("M_ArenaFactory")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = 'Closest'
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    return mat


def cell_uv(i):
    return ((i % 8 + 0.5) / 8, (i // 8 + 0.5) / 8)


class Mesh:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UV")

    def face(self, pts, color):
        verts = [self.bm.verts.new((p[0], -p[2], p[1])) for p in pts]   # мои (x, высота, z) -> оси Blender (Z вверх)
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return
        u = cell_uv(color)
        for loop in f.loops:
            loop[self.uv].uv = u

    def box(self, c, s, color, top=None, sides=None, bottom=False):
        """Параллелепипед по центру c и размеру s. top/sides — свои цвета граней."""
        x0, x1 = c[0] - s[0] / 2, c[0] + s[0] / 2
        y0, y1 = c[1] - s[1] / 2, c[1] + s[1] / 2
        z0, z1 = c[2] - s[2] / 2, c[2] + s[2] / 2
        side = color if sides is None else sides
        self.face([(x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0)], color if top is None else top)
        self.face([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], side)     # +z
        self.face([(x1, y0, z0), (x0, y0, z0), (x0, y1, z0), (x1, y1, z0)], side)     # -z
        self.face([(x1, y0, z1), (x1, y0, z0), (x1, y1, z0), (x1, y1, z1)], side)     # +x
        self.face([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], side)     # -x
        if bottom:
            self.face([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], side)

    def prism(self, c, radius, height, color, sides=8, top=None):
        pts0 = [(c[0] + math.cos(a) * radius, c[1] - height / 2, c[2] + math.sin(a) * radius)
                for a in (2 * math.pi * i / sides for i in range(sides))]
        pts1 = [(p[0], p[1] + height, p[2]) for p in pts0]
        self.face(list(reversed(pts1)), color if top is None else top)
        for i in range(sides):
            j = (i + 1) % sides
            self.face([pts0[j], pts0[i], pts1[i], pts1[j]], color)

    def to_object(self, material, collection):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        mesh.materials.append(material)
        collection.objects.link(obj)
        return obj


# ------------------------------------------------------------------ шум
def _hash(ix, iz, seed):
    n = (ix * 374761393 + iz * 668265263 + seed * 2147483647) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def vnoise(x, z, seed):
    ix, iz = math.floor(x), math.floor(z)
    fx, fz = x - ix, z - iz
    fx, fz = fx * fx * (3 - 2 * fx), fz * fz * (3 - 2 * fz)
    a, b = _hash(ix, iz, seed), _hash(ix + 1, iz, seed)
    c, d = _hash(ix, iz + 1, seed), _hash(ix + 1, iz + 1, seed)
    return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fz


def fbm(x, z, seed, octaves=4):
    total, amp, freq, norm = 0.0, 1.0, 1.0, 0.0
    for o in range(octaves):
        total += vnoise(x * freq, z * freq, seed + o * 17) * amp
        norm += amp
        amp *= 0.5
        freq *= 2.0
    return total / norm


# ---------------------------------------------------------------- сцена
bpy.ops.wm.read_factory_settings(use_empty=True)
collection = bpy.context.collection
palette_image = make_palette_image()
material = make_material(palette_image)
rng = random.Random(2026)

# ============================================================ ПОЛ
floor = Mesh("ArenaFloor")
# тёмная подложка: видна в швах между плитами
floor.box((0, -0.5, 0), (2 * HALF + 12, 0.45, 2 * HALF + 12), GAP0 + 1, top=GAP0 + 1)
gap_top = -0.27
for ix in range(PLATES):
    for iz in range(PLATES):
        cx = -HALF + PLATE * (ix + 0.5)
        cz = -HALF + PLATE * (iz + 0.5)
        h = rng.random()
        shade = PLATE0 + 2 + int(h * 4)
        size = PLATE - 0.22
        # плита: толстое основание + фаска (ступень) — верх ровно на y=0
        floor.box((cx, -0.15, cz), (size, 0.3, size), shade - 1, top=shade - 1)
        floor.box((cx, -0.04, cz), (size - 0.16, 0.08, size - 0.16), shade, top=shade)
        # вставка-панель, чуть приподнятая (до +0.03)
        floor.box((cx, 0.0, cz), (size - 1.5, 0.06, size - 1.5), INSET0 + 2 + int(rng.random() * 4), top=INSET0 + 2 + int(rng.random() * 4))
        # болты по углам
        for sx in (-1, 1):
            for sz in (-1, 1):
                floor.prism((cx + sx * (size / 2 - 0.45), 0.0, cz + sz * (size / 2 - 0.45)), 0.17, 0.08, CABLE0 + 2, top=CABLE0 + 3)
        # потёртости и пятна ржавчины
        for _ in range(rng.choice((0, 1, 1, 2))):
            px = cx + (rng.random() - 0.5) * (size - 3)
            pz = cz + (rng.random() - 0.5) * (size - 3)
            w, d = 0.6 + rng.random() * 2.2, 0.6 + rng.random() * 2.2
            col = rng.choice((RUST0 + 1, RUST0 + 2, GAP0 + 3, BLK0 + 2, PLATE0 + 1))
            floor.box((px, 0.03, pz), (w, 0.01, d), col, top=col)
        # решётки стока на каждой ~9-й плите
        if (ix * 7 + iz * 3) % 9 == 0:
            floor.box((cx, 0.032, cz), (2.6, 0.01, 2.6), GAP0, top=GAP0)
            for k in range(-2, 3):
                floor.box((cx + k * 0.5, 0.036, cz), (0.14, 0.012, 2.4), CABLE0 + 1, top=CABLE0 + 2)

# дорожная разметка по осям (штрихи)
for axis in (0, 1):
    t = -HALF + 6.0
    while t < HALF - 6.0:
        if abs(t) > 12.5 and abs(t + 1.5) > 12.5:
            sz = (3.0, 0.012, 0.45) if axis == 0 else (0.45, 0.012, 3.0)
            pos = (t + 1.5, 0.047, 0.0) if axis == 0 else (0.0, 0.047, t + 1.5)
            floor.box(pos, sz, YEL0 + 2, top=YEL0 + 2)
        t += 6.0

# центр: бирюзовое кольцо посадки и кольцо-предупреждение
def annulus(mesh, r0, r1, y, segments, colors, start=0.0):
    for i in range(segments):
        a0 = start + 2 * math.pi * i / segments
        a1 = start + 2 * math.pi * (i + 1) / segments
        col = colors[i % len(colors)]
        mesh.face([(math.cos(a0) * r0, y, math.sin(a0) * r0), (math.cos(a0) * r1, y, math.sin(a0) * r1),
                   (math.cos(a1) * r1, y, math.sin(a1) * r1), (math.cos(a1) * r0, y, math.sin(a1) * r0)], col)

annulus(floor, 4.8, 5.3, 0.055, 48, [GLOW0 + 2])
annulus(floor, 5.3, 5.55, 0.055, 48, [GLOW0])
annulus(floor, 9.0, 10.0, 0.05, 36, [YEL0 + 2, BLK0 + 1])
annulus(floor, 9.0, 9.1, 0.052, 72, [BLK0])
annulus(floor, 9.9, 10.0, 0.052, 72, [BLK0])
# внутренний диск посадки
floor.prism((0, 0.04, 0), 4.8, 0.01, PLATE0 + 5, sides=48, top=PLATE0 + 5)

# периметр: кромка из жёлто-чёрных блоков по краю игровой зоны
edge = HALF - 0.9
for side in range(4):
    t = -HALF + 1.5
    n = 0
    while t < HALF - 1.5:
        col = YEL0 + 2 if n % 2 == 0 else BLK0 + 1
        if side == 0:
            floor.box((t + 1.0, 0.045, edge), (2.0, 0.012, 0.5), col, top=col)
        elif side == 1:
            floor.box((t + 1.0, 0.045, -edge), (2.0, 0.012, 0.5), col, top=col)
        elif side == 2:
            floor.box((edge, 0.045, t + 1.0), (0.5, 0.012, 2.0), col, top=col)
        else:
            floor.box((-edge, 0.045, t + 1.0), (0.5, 0.012, 2.0), col, top=col)
        t += 2.0
        n += 1

# ============================================================ БОРТА И БАШНИ
walls = Mesh("ArenaWalls")
THICK = 5.0
WALL_H = 4.2


def rot(k, x, z):
    for _ in range(k % 4):
        x, z = z, -x
    return x, z


def wbox(k, along, out, y, sa, sy, so, color, top=None, sides=None):
    """Блок в координатах борта: along — вдоль борта, out — наружу от внутренней грани (z=HALF+0.5)."""
    x, z = along, HALF + 0.5 + out
    rx, rz = rot(k, x, z)
    sx, sz = (sa, so) if k % 2 == 0 else (so, sa)
    walls.box((rx, y, rz), (sx, sy, sz), color, top=top, sides=sides)


for k in range(4):
    seg = 10.0
    n = int(2 * HALF / seg)
    for i in range(n):
        a = -HALF + seg * (i + 0.5)
        shade = WALL0 + 1 + int(_hash(i, k, 5) * 3)
        wbox(k, a, THICK / 2, WALL_H / 2 - 0.3, seg - 0.12, WALL_H + 0.6, THICK, shade, top=WALL0 + 5)
        # плинтус-отбойник и жёлто-чёрная полоса
        wbox(k, a, -0.04, 0.28, seg - 0.12, 0.56, 0.12, WALL0 + 3)
        for j in range(5):
            col = YEL0 + 2 if (i + j) % 2 == 0 else BLK0 + 1
            wbox(k, a - seg / 2 + 1.0 + j * 2.0, -0.07, 0.62, 2.0, 0.22, 0.06, col)
        # горизонтальные накладки
        wbox(k, a, -0.04, 1.55, seg - 0.4, 0.16, 0.1, WALL0 + 5)
        wbox(k, a, -0.04, 2.55, seg - 0.4, 0.16, 0.1, WALL0 + 5)
        # вертикальные рёбра жёсткости
        for e in (-4.0, 0.0, 4.0):
            wbox(k, a + e, -0.1, 1.9, 0.5, 3.4, 0.2, WALL0 + 2)
        # верхняя кромка и перила
        wbox(k, a, THICK / 2 + 0.2, WALL_H + 0.15, seg - 0.1, 0.4, THICK + 0.8, WALL0 + 6, top=WALL0 + 7)
        for p in range(4):
            wbox(k, a - 3.75 + p * 2.5, 0.2, WALL_H + 0.9, 0.18, 1.0, 0.18, CABLE0 + 2)
        wbox(k, a, 0.2, WALL_H + 1.35, seg - 0.2, 0.12, 0.12, CABLE0 + 3)
        # светящиеся планки каждые 20 м
        if i % 2 == 0:
            wbox(k, a, -0.1, 3.3, 4.0, 0.14, 0.08, GLOW0 + 3)
            wbox(k, a, -0.12, 3.3, 4.2, 0.28, 0.04, GLOW0)
        # контрфорсы снаружи
        if i % 3 == 1:
            wbox(k, a, THICK + 1.3, 3.2, 3.0, 6.9, 2.6, WALL0 + 2, top=WALL0 + 6)

def tower(k):
    # башня в углу: центр вне арены по диагонали
    cx, cz = rot(k, HALF + 6.5, HALF + 6.5)
    # k-й угол: (+,+), поворот — остальные
    def tb(dy, h, size, color, top=None):
        walls.box((cx, dy + h / 2, cz), (size, h, size), color, top=top)
    tb(-0.3, 9.3, 13.0, WALL0 + 1, top=WALL0 + 4)
    tb(9.0, 0.6, 14.2, WALL0 + 6, top=WALL0 + 7)
    tb(9.6, 3.0, 10.0, WALL0 + 2, top=WALL0 + 5)
    tb(12.6, 0.5, 11.0, WALL0 + 6, top=WALL0 + 7)
    tb(13.1, 1.6, 4.0, GLOW0 + 1, top=GLOW0 + 3)
    walls.box((cx, 17.5, cz), (0.35, 7.0, 0.35), CABLE0 + 2)
    walls.box((cx, 21.2, cz), (0.7, 0.7, 0.7), RED, top=RED)
    # светящиеся пояса башни и окна
    for dy in (3.0, 6.2):
        walls.box((cx, dy, cz), (13.15, 0.25, 13.15), GLOW0 + 1)
    # жёлто-чёрные кромки у основания
    for j in range(8):
        col = YEL0 + 2 if j % 2 == 0 else BLK0 + 1
        walls.box((cx - 5.6 + j * 1.6, 0.2, cz - 6.55), (1.6, 0.4, 0.15), col)

for k in range(4):
    tower(k)

# ============================================================ СКАЛЫ ВОКРУГ
cliffs = Mesh("ArenaCliffs")
STEP = 5.0
RIM = HALF + 3.5       # у борта
OUTER = 215.0


def height(x, z):
    d = max(abs(x), abs(z))
    t = max(0.0, (d - RIM) / (OUTER - RIM))
    base = 3.6 + 38.0 * (t ** 1.35)
    ridges = (fbm(x * 0.028, z * 0.028, 11, 3) - 0.5) * 34.0 * min(1.0, t * 3)
    detail = (fbm(x * 0.09, z * 0.09, 29, 2) - 0.5) * 2.5 * min(1.0, t * 4)
    h = base + ridges + detail
    # террасы: скалы ступенями
    terrace = round(h / 3.2) * 3.2
    h = h * 0.45 + terrace * 0.55 if t > 0.12 else h
    return max(3.2, h)


n_cells = int(2 * OUTER / STEP)
verts = {}
for gi in range(n_cells + 1):
    for gj in range(n_cells + 1):
        x = -OUTER + gi * STEP
        z = -OUTER + gj * STEP
        jx = (_hash(gi, gj, 3) - 0.5) * STEP * 0.5
        jz = (_hash(gi, gj, 4) - 0.5) * STEP * 0.5
        x, z = x + jx, z + jz
        verts[(gi, gj)] = (x, height(x, z), z)

for gi in range(n_cells):
    for gj in range(n_cells):
        quad = [verts[(gi, gj)], verts[(gi, gj + 1)], verts[(gi + 1, gj + 1)], verts[(gi + 1, gj)]]
        cx = sum(p[0] for p in quad) / 4
        cz = sum(p[2] for p in quad) / 4
        if max(abs(cx), abs(cz)) < RIM - 0.5:
            continue    # внутри борта скал нет
        avg = sum(p[1] for p in quad) / 4
        hmax = max(p[1] for p in quad)
        hmin = min(p[1] for p in quad)
        slope = hmax - hmin
        n = _hash(gi, gj, 8)
        if slope > 3.2:
            col = ROCK0 + 1 + int(n * 3)               # тёмные отвесы
        elif avg > 30:
            col = ROCK0 + 5 + int(n * 3)               # светлые вершины
        elif n > 0.93:
            col = RUST0 + 2 + int(n * 20) % 3          # ржавые пятна руды
        else:
            col = ROCK0 + 2 + int(n * 4)
        # плоский шейдинг: делим квад на два треугольника по высоте, чтобы грани были «гранёными»
        if abs(quad[0][1] - quad[2][1]) < abs(quad[1][1] - quad[3][1]):
            cliffs.face([quad[0], quad[1], quad[2]], col)
            cliffs.face([quad[0], quad[2], quad[3]], col)
        else:
            cliffs.face([quad[0], quad[1], quad[3]], col)
            cliffs.face([quad[1], quad[2], quad[3]], col)

# ============================================================ СБОРКА И ЭКСПОРТ
root = bpy.data.objects.new("ArenaFactory", None)
collection.objects.link(root)
objects = []
for m in (floor, walls, cliffs):
    obj = m.to_object(material, collection)
    obj.parent = root
    objects.append(obj)

# контроль: плоский верх плит, размеры
for obj in objects:
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    print("MESH %s: tris=%d bounds x[%.1f..%.1f] y[%.2f..%.2f] z[%.1f..%.1f]" % (
        obj.name, sum(len(p.vertices) - 2 for p in obj.data.polygons), min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))

# Blender: Z вверх. Мои координаты собирались как X/Z — горизонталь, Y — высота (как в Unity),
# поэтому экспортируем без смены осей: оси Unity = оси скрипта.
bpy.ops.object.select_all(action='DESELECT')
for obj in [root] + objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=str(OUT_ASSET / "ArenaFactory.fbx"), use_selection=True,
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         bake_space_transform=True, add_leaf_bones=False, mesh_smooth_type='FACE', path_mode='STRIP')
print("EXPORTED", OUT_ASSET / "ArenaFactory.fbx")
