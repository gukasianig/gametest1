"""Переделка шести сломанных моделей Codex для зелёной арены (Blender 4.5, фон).

Запуск:
  blender -b --python remake_arena_models.py -- <папка Assets/Art/ArenaGreen> [имя_модели ...]
Модели: windmill, road_dirt_cross, plateau_step, ramp_dirt, waterfall_cliff, river_fork_Y.

Все модели красятся ячейками атласа ArenaGreen_Atlas.png (8x8): у каждой грани одна ячейка,
UV внутри неё. Экспорт чистый: поворот 0, масштаб 1, метры, Y вверх (в Blender Z вверх).
Материалы: ArenaGreen_Atlas (слот 0) и ArenaGreen_Water (слот 1) - Unity подменяет их при сборке арены.
"""
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index('--') + 1:]
OUT = args[0]
ONLY = set(args[1:])

# --- ячейки атласа (столбец, строка сверху) ---
GRASS = [(7, 2), (2, 6), (1, 6), (4, 2)]
GRASS_EDGE = [(0, 6), (2, 6), (1, 6)]
DIRT = [(1, 7), (0, 7)]
DIRT_DARK = (2, 7)
SAND = (3, 7)
ROCK = [(4, 6), (6, 6), (5, 6)]
STONE_WALL = [(7, 5), (0, 2), (2, 3)]
WOOD = [(5, 4), (1, 5)]
WOOD_DARK = [(3, 5), (5, 5)]
ROOF = [(2, 4), (3, 4)]
CLOTH = (0, 2)
WATER_OUT = (2, 0)
WATER_IN = (3, 0)


def make_material(name, color):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nodes = m.node_tree.nodes
    node = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None) or nodes.new('ShaderNodeBsdfPrincipled')
    node.inputs['Base Color'].default_value = color
    m.diffuse_color = color
    return m


ATLAS = make_material('ArenaGreen_Atlas', (0.45, 0.55, 0.35, 1.0))
WATER = make_material('ArenaGreen_Water', (0.2, 0.5, 0.6, 1.0))


class Mesh:
    """Набор граней с одной ячейкой атласа на грань."""

    def __init__(self, name, seed, location=(0.0, 0.0, 0.0)):
        self.name = name
        self.location = Vector(location)
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.rng = random.Random(seed)
        self.materials = [ATLAS]

    # ---- окраска ----
    def paint(self, face, cells, mat=0):
        c = cells if isinstance(cells[0], int) else self.rng.choice(cells)
        u = (c[0] + 0.2 + 0.6 * self.rng.random()) / 8.0
        v = 1.0 - (c[1] + 0.2 + 0.6 * self.rng.random()) / 8.0
        for loop in face.loops:
            loop[self.uv].uv = (u, v)
        face.material_index = mat

    # ---- примитивы ----
    def poly(self, pts, cells, mat=0):
        face = self.bm.faces.new([self.bm.verts.new(Vector(p)) for p in pts])
        if cells is not None:
            self.paint(face, cells, mat)
        return face

    def cube(self, center, size, cells, rot=None):
        before = set(self.bm.faces)
        m = Matrix.Translation(Vector(center)) @ (rot if rot is not None else Matrix.Identity(4)) \
            @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)
        for f in [f for f in self.bm.faces if f not in before]:
            self.paint(f, cells)

    def frustum(self, matrix, r_bottom, r_top, depth, segs, side_cells, cap_cells, cap_bottom=True):
        """Усечённый конус вокруг локальной оси Z, потом трансформ matrix."""
        before = set(self.bm.faces)
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                                    radius1=r_bottom, radius2=r_top, depth=depth)
        verts = res['verts']
        zmin = min(v.co.z for v in verts)
        low = [v for v in verts if abs(v.co.z - zmin) < 1e-6]
        low_r = sum(math.hypot(v.co.x, v.co.y) for v in low) / len(low)
        if abs(low_r - r_bottom) > 1e-4 and abs(r_bottom - r_top) > 1e-4:   # радиусы поменялись местами
            for v in verts:
                t = (v.co.z - zmin) / depth
                r = r_bottom + (r_top - r_bottom) * t
                ang = math.atan2(v.co.y, v.co.x)
                v.co.x, v.co.y = r * math.cos(ang), r * math.sin(ang)
        new = [f for f in self.bm.faces if f not in before]
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        self.bm.normal_update()
        for f in new:
            local_z = (matrix.inverted_safe().to_3x3() @ f.normal).z
            if local_z > 0.9:
                self.paint(f, cap_cells)
            elif local_z < -0.9:
                self.paint(f, cap_cells if cap_bottom else DIRT_DARK)
            else:
                self.paint(f, side_cells)

    def sphere(self, center, radius, squash, cells, seed=0):
        before = set(self.bm.faces)
        m = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((radius, radius, radius * squash, 1.0))
        bmesh.ops.create_icosphere(self.bm, subdivisions=1, radius=1.0, matrix=m)
        for f in [f for f in self.bm.faces if f not in before]:
            self.paint(f, cells)

    # ---- завершение ----
    def finish(self, closed, up_only=False):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
        bm.normal_update()
        if closed:
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        if up_only:
            for f in bm.faces:
                if f.normal.z < 0:
                    f.normal_flip()
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
        bm.normal_update()
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.materials:
            me.materials.append(m)
        obj = bpy.data.objects.new(self.name, me)
        obj.location = self.location
        bpy.context.scene.collection.objects.link(obj)
        return obj


def poly_flip_to(mesh, face, direction):
    face.normal_update()
    if face.normal.dot(Vector(direction)) < 0:
        face.normal_flip()


def heightfield_solid(m, xs, ys, pos, color_fn, zbot=-0.5, skirt=None):
    """Закрытое тело: верх по сетке xs*ys (pos(x,y) -> (x,y,z)), стенки до zbot, дно."""
    skirt = skirt or [DIRT_DARK]
    nx, ny = len(xs), len(ys)
    P = [[Vector(pos(xs[i], ys[j])) for j in range(ny)] for i in range(nx)]
    for i in range(nx - 1):
        for j in range(ny - 1):
            pts = [P[i][j], P[i + 1][j], P[i + 1][j + 1], P[i][j + 1]]
            face = m.poly(pts, None)
            face.normal_update()
            c = sum(pts, Vector()) / 4.0
            n = face.normal if face.normal.z >= 0 else -face.normal
            m.paint(face, color_fn(c, n))

    def wall(a, b):
        m.poly([a, b, Vector((b.x, b.y, zbot)), Vector((a.x, a.y, zbot))], skirt)

    for j in range(ny - 1):
        wall(P[0][j], P[0][j + 1])
        wall(P[nx - 1][j], P[nx - 1][j + 1])
    for i in range(nx - 1):
        wall(P[i][0], P[i + 1][0])
        wall(P[i][ny - 1], P[i + 1][ny - 1])
    perim = [P[i][0] for i in range(nx)] + [P[nx - 1][j] for j in range(1, ny)] + \
            [P[i][ny - 1] for i in range(nx - 2, -1, -1)] + [P[0][j] for j in range(ny - 2, 0, -1)]
    m.poly([Vector((p.x, p.y, zbot)) for p in perim], skirt)


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


# =====================================================================
# Мельница
# =====================================================================
def windmill():
    body = Mesh('windmill', 11)
    rot15 = Matrix.Rotation(math.radians(15), 4, 'Z')   # грань (а не вершина) смотрит вперёд на -Y
    r_at = lambda z: 3.0 + (2.1 - 3.0) * (z - 0.5) / 7.5
    apothem = lambda z: r_at(z) * math.cos(math.radians(15))

    # фундамент, башня, пояса
    body.frustum(Matrix.Translation((0, 0, 0.25)) @ rot15, 3.35, 3.25, 0.5, 12, ROCK, ROCK)
    body.frustum(Matrix.Translation((0, 0, 4.25)) @ rot15, 3.0, 2.1, 7.5, 12, STONE_WALL, STONE_WALL)
    for zc in (3.2, 6.3):
        r = r_at(zc)
        body.frustum(Matrix.Translation((0, 0, zc)) @ rot15, r + 0.1, r + 0.1, 0.22, 12, ROCK, ROCK)

    # дверь с рамой и ступенькой
    zd = 0.5 + 1.2
    ad = apothem(zd)
    body.cube((0, -ad, zd + 0.05), (1.6, 0.5, 2.6), WOOD_DARK)
    body.cube((0, -ad - 0.05, zd - 0.05), (1.15, 0.5, 2.3), WOOD)
    body.cube((0, -ad - 0.55, 0.62), (2.0, 0.9, 0.25), ROCK)

    # окна
    def window(angle_deg, z):
        a = apothem(z)
        ang = math.radians(angle_deg)
        rot = Matrix.Rotation(ang, 4, 'Z')
        c = (a * math.cos(ang), a * math.sin(ang), z)
        body.cube(c, (0.45, 0.8, 1.2), WOOD_DARK, rot)
        body.cube(c, (0.55, 0.55, 0.9), (6, 5), rot)
    for k in (3, 7, 11):
        window(k * 30, 4.6)
    for k in (1, 5, 9):
        window(k * 30, 6.7)

    # поворотная шапка и крыша
    body.frustum(Matrix.Translation((0, 0, 8.2)), 2.7, 2.7, 0.4, 12, WOOD_DARK, WOOD_DARK)
    body.frustum(Matrix.Translation((0, 0, 8.65)), 2.4, 2.4, 0.5, 12, WOOD_DARK, WOOD_DARK)
    body.frustum(Matrix.Translation((0, 0, 9.9)) @ rot15, 2.75, 0.12, 2.0, 12, ROOF, ROOF)   # низ - свес
    body.frustum(Matrix.Translation((0, 0, 11.2)), 0.12, 0.04, 0.6, 6, WOOD_DARK, WOOD_DARK)

    # вал и ступица (неподвижная часть)
    rotx = Matrix.Rotation(math.radians(90), 4, 'X')   # локальная +Z -> -Y
    body.frustum(Matrix.Translation((0, -2.2, 9.1)) @ rotx, 0.3, 0.3, 2.2, 8, WOOD, WOOD_DARK)
    body.frustum(Matrix.Translation((0, -3.05, 9.1)) @ rotx, 0.62, 0.45, 0.6, 8, WOOD_DARK, WOOD_DARK)
    body_obj = body.finish(closed=False)

    # лопасти: объект с началом в центре ступицы, чтобы их можно было вращать
    blades = Mesh('Blades', 12, location=(0, -3.45, 9.1))
    blades.frustum(rotx, 0.55, 0.5, 0.5, 8, WOOD_DARK, WOOD_DARK)
    blades.frustum(Matrix.Translation((0, -0.3, 0)) @ rotx, 0.3, 0.12, 0.2, 8, WOOD, WOOD)
    for k in range(4):
        rot = Matrix.Rotation(math.radians(90 * k), 4, 'Y')

        def part(c, s, cells):
            blades.cube(rot @ Vector(c), s, cells, rot)
        part((2.75, 0.0, 0.0), (4.5, 0.2, 0.26), WOOD)                 # лонжерон от ступицы (0.5) до 5.0
        part((3.0, 0.03, 0.82), (3.6, 0.03, 1.3), CLOTH)               # парус
        part((3.0, -0.03, 0.18), (3.6, 0.07, 0.09), WOOD)              # рама вдоль
        part((3.0, -0.03, 1.47), (3.6, 0.07, 0.09), WOOD)
        for x in (1.2, 4.8):
            part((x, -0.03, 0.82), (0.09, 0.07, 1.38), WOOD)           # рама поперёк
        for x in (2.1, 3.0, 3.9):
            part((x, -0.03, 0.82), (0.06, 0.06, 1.3), WOOD)            # планки паруса
    blades_obj = blades.finish(closed=False)
    return [body_obj, blades_obj]


# =====================================================================
# Перекрёсток грунтовой дороги 10 x 10 (ширина полос 4, как у road_dirt_straight)
# =====================================================================
def road_dirt_cross():
    m = Mesh('road_dirt_cross', 21)
    lines = [-5.0, -2.0, -1.52, -0.92, -0.64, 0.64, 0.92, 1.52, 2.0, 5.0]
    for i in range(len(lines) - 1):
        for j in range(len(lines) - 1):
            xc = (lines[i] + lines[i + 1]) / 2.0
            yc = (lines[j] + lines[j + 1]) / 2.0
            d = min(abs(xc), abs(yc))
            if d >= 2.0:
                continue
            if d < 0.64:
                cells = (4, 7)
            elif d < 0.92:
                cells = (0, 7)
            elif d < 1.52:
                cells = (1, 7)
            else:
                cells = GRASS_EDGE
            x0, x1, y0, y1 = lines[i], lines[i + 1], lines[j], lines[j + 1]
            m.poly([(x0, y0, 0.01), (x1, y0, 0.01), (x1, y1, 0.01), (x0, y1, 0.01)], cells)
    return [m.finish(closed=False, up_only=True)]


# =====================================================================
# Терраса: три уровня по 3 м, 36 x 36, склоны между ними
# =====================================================================
def plateau_step():
    m = Mesh('plateau_step', 31)
    SEG = 64
    n = 5.0

    def super_r(theta, half):
        c, s = abs(math.cos(theta)), abs(math.sin(theta))
        return half / ((c ** n + s ** n) ** (1.0 / n))

    def wob(theta, phase):
        return 0.30 * math.sin(3 * theta + phase) + 0.18 * math.sin(5 * theta + 1.7 * phase) + 0.08 * math.sin(9 * theta + 2.3 * phase)

    rings = [  # половина стороны, высота, фаза неровности, вид полосы до следующего кольца
        (18.0, -0.5, 0.0, 'wall'),
        (18.0, 0.0, 0.0, 'slope'),
        (15.0, 3.0, 1.1, 'flat'),
        (12.0, 3.0, 2.3, 'slope'),
        (9.0, 6.0, 3.1, 'flat'),
        (7.0, 6.0, 4.0, 'slope'),
        (4.5, 9.0, 5.2, 'top'),
    ]
    rng = random.Random(5)
    ring_pts = []
    for half, z, phase, _ in rings:
        pts = []
        for k in range(SEG):
            th = 2 * math.pi * k / SEG
            r = super_r(th, half) + (wob(th, phase) if z > 0.5 else 0.0)
            zz = z + (rng.uniform(-0.06, 0.06) if (z > 0.5 and rings and half < 17) else 0.0)
            pts.append(Vector((r * math.cos(th), r * math.sin(th), zz)))
        ring_pts.append(pts)
    for i in range(len(rings) - 1):
        kind = rings[i][3]
        cells = {'wall': [DIRT_DARK], 'slope': [(1, 7), (1, 7), (1, 7), (0, 7)], 'flat': GRASS}[kind] if kind != 'top' else GRASS
        for k in range(SEG):
            a, b = ring_pts[i][k], ring_pts[i][(k + 1) % SEG]
            c, d = ring_pts[i + 1][(k + 1) % SEG], ring_pts[i + 1][k]
            m.poly([a, b, c, d], cells)
    top = ring_pts[-1]
    centre = Vector((0.0, 0.0, 9.0))
    for k in range(SEG):
        m.poly([top[k], top[(k + 1) % SEG], centre], [(4, 2), (7, 2)])
    bottom = ring_pts[0]
    m.poly(list(reversed(bottom)), DIRT_DARK)
    return [m.finish(closed=True)]


# =====================================================================
# Пологий въезд 8 x 14, подъём 0 -> 3 м по длине (низ на y=-7, верх на y=+7)
# =====================================================================
def ramp_dirt():
    m = Mesh('ramp_dirt', 41)
    xs = [-4.0, -3.4, -3.0, -1.5, 0.0, 1.5, 3.0, 3.4, 4.0]
    ys = [-7.0 + j for j in range(15)]

    def hgt(y):
        t = (y + 7.0) / 14.0
        return 3.0 * (3 * t * t - 2 * t * t * t)

    def pos(x, y):
        h = hgt(y)
        ax = abs(x)
        if ax <= 3.0:
            z = h
        elif ax <= 3.4:
            z = h - 0.07
        else:
            z = h - min(h, 0.9)
        return (x, y, z)

    def color(c, n):
        ax = abs(c.x)
        if ax < 3.0:
            return [(1, 7), (4, 7), (4, 7), (1, 7)][min(3, int((c.x + 3.0) / 1.5))] if m.rng.random() > 0.2 else (0, 7)
        if ax < 3.4:
            return (2, 7)
        return GRASS_EDGE
    heightfield_solid(m, xs, ys, pos, color, zbot=-0.5, skirt=[(0, 7), DIRT_DARK])
    # деревянные бортики вдоль съезда
    for side in (-1, 1):
        for j in range(14):
            y0, y1 = ys[j], ys[j + 1]
            z0, z1 = hgt(y0), hgt(y1)
            ang = math.atan2(z1 - z0, y1 - y0)
            length = math.hypot(y1 - y0, z1 - z0) + 0.04
            m.cube((side * 3.2, (y0 + y1) / 2, (z0 + z1) / 2 + 0.1), (0.3, length, 0.26), WOOD,
                   Matrix.Rotation(ang, 4, 'X'))
    return [m.finish(closed=True)]


# =====================================================================
# Водопад-каскад: три уступа, вода на ступенях, чаша внизу (14 x 10, высота 9)
# =====================================================================
KEYS = [(-5.0, 0.30), (-4.4, -0.25), (-3.3, -0.25), (-3.15, 1.4), (-3.0, 3.0), (-2.0, 3.08), (-0.9, 3.05),
        (-0.75, 4.5), (-0.6, 6.0), (0.5, 6.05), (1.6, 6.0), (1.75, 7.6), (1.9, 9.0), (3.5, 9.12), (5.0, 9.1)]


def wf_base(y):
    for (y0, z0), (y1, z1) in zip(KEYS[:-1], KEYS[1:]):
        if y0 <= y <= y1:
            return z0 + (z1 - z0) * (y - y0) / (y1 - y0)
    return KEYS[-1][1] if y > 0 else KEYS[0][1]


def wf_noise(a, b):
    return math.sin(a * 2.7 + b * 1.3) * 0.6 + math.sin(a * 5.1 - b * 2.2 + 1.0) * 0.4


def wf_z(x, y):
    base = wf_base(y)
    ax = abs(x)
    z = base
    if base < 0.6:   # чаша: борта по краям
        z += 0.9 * (smooth(3.4, 5.6, ax) ** 1.5)
    else:
        tr = max(0.0, 1.0 - (x / 2.2) ** 2)
        z -= 0.32 * tr * smooth(0.6, 1.6, base)             # жёлоб в центре
        z += 0.12 * wf_noise(x, y)
    e = smooth(4.5, 7.0, ax)
    z *= (1.0 - 0.7 * e ** 1.5) if z > 0 else 1.0
    return z


def waterfall_cliff():
    m = Mesh('waterfall_cliff', 51)
    xs = [float(i) for i in range(-7, 8)]
    ys = [k[0] for k in KEYS]

    def pos(x, y):
        yy = y
        if y in (-3.15, -0.75, 1.75) and abs(x) < 6.5:   # рваный край уступов
            yy = y + 0.2 * wf_noise(x * 1.7, y)
        return (x, yy, wf_z(x, y))

    def color(c, n):
        if n.z < 0.6:
            return [(4, 6), (4, 6), (6, 6)]
        if c.z < 0.15:
            return [SAND, (4, 7)]
        if abs(c.x) < 2.7:
            return ROCK[:2]
        return GRASS_EDGE if c.z > 2.0 else ROCK
    heightfield_solid(m, xs, ys, pos, color, zbot=-0.5, skirt=[(4, 6), (6, 6)])
    for cx, cy, r in ((-3.0, -3.9, 0.7), (3.1, -4.1, 0.6), (-4.4, -4.4, 0.5), (2.3, -3.5, 0.45), (4.4, -3.7, 0.8)):
        m.sphere((cx, cy, 0.05), r, 0.7, ROCK)
    rock = m.finish(closed=True)

    # вода: отдельный объект с материалом ArenaGreen_Water
    w = Mesh('waterfall_cliff_Water', 52)
    w.materials = [WATER]

    def wz(y):
        return wf_z(0.0, y) + 0.12

    def strip(ya, yb, steps=3):
        for s in range(steps):
            y0 = ya + (yb - ya) * s / steps
            y1 = ya + (yb - ya) * (s + 1) / steps
            f = w.poly([(-1.7, y0, wz(y0)), (1.7, y0, wz(y0)), (1.7, y1, wz(y1)), (-1.7, y1, wz(y1))], WATER_OUT, 0)
            poly_flip_to(w, f, (0, 0, 1))
            if s == steps // 2:
                inner = w.poly([(-0.7, y0, wz(y0) + 0.005), (0.7, y0, wz(y0) + 0.005), (0.7, y1, wz(y1) + 0.005), (-0.7, y1, wz(y1) + 0.005)], WATER_IN, 0)
                poly_flip_to(w, inner, (0, 0, 1))

    def sheet(y_lip, z_lip, y_foot, z_foot):
        top = (y_lip, z_lip + 0.1)
        bot = (y_foot - 0.4, z_foot + 0.12)
        mid = ((top[0] + bot[0]) / 2 - 0.2, (top[1] + bot[1]) / 2)
        rows = [top, mid, bot]
        for (ya, za), (yb, zb) in zip(rows[:-1], rows[1:]):
            f = w.poly([(-1.5, ya, za), (1.5, ya, za), (1.5, yb, zb), (-1.5, yb, zb)], [WATER_IN, WATER_OUT], 0)
            poly_flip_to(w, f, (0, -1, 0.3))
    strip(5.0, 1.9)                 # верхний ручей
    sheet(1.9, 9.0 - 0.12, 1.6, 6.0)
    strip(1.2, -0.6)
    sheet(-0.6, 6.0 - 0.1, -0.9, 3.05)
    strip(-1.3, -3.0)
    sheet(-3.0, 3.0 - 0.1, -3.3, 0.0)
    pool = w.poly([(-5.2, -4.95, 0.1), (5.2, -4.95, 0.1), (5.2, -3.0, 0.1), (-5.2, -3.0, 0.1)], [WATER_OUT, WATER_OUT, WATER_IN], 0)
    poly_flip_to(w, pool, (0, 0, 1))
    water = w.finish(closed=False)
    return [rock, water]


# =====================================================================
# Развилка реки «Y» 25 x 25: ствол снизу, две ветки сверху, профиль как у river_straight_20
# =====================================================================
def river_fork_Y():
    m = Mesh('river_fork_Y', 61)
    segs = [((0.0, -12.5), (0.0, -3.0))]

    def bez(p0, p1, p2, p3, t):
        a, b, c, d = (1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t * t, t ** 3
        return (a * p0[0] + b * p1[0] + c * p2[0] + d * p3[0], a * p0[1] + b * p1[1] + c * p2[1] + d * p3[1])
    for sx in (-1.0, 1.0):
        pts = [bez((0, -3), (0, 2.0), (sx * 7.5, 2.0), (sx * 7.5, 8.0), i / 24.0) for i in range(25)]
        pts.append((sx * 7.5, 12.5))
        segs += list(zip(pts[:-1], pts[1:]))

    def dist(x, y):
        best = 1e9
        for (ax, ay), (bx, by) in segs:
            dx, dy = bx - ax, by - ay
            t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)))
            best = min(best, math.hypot(x - (ax + t * dx), y - (ay + t * dy)))
        return best

    def profile(d):
        if d <= 2.8:
            return 0.0
        if d <= 3.8:
            return 0.35 * (d - 2.8)
        if d <= 5.0:
            return 0.35 * (5.0 - d) / 1.2
        return 0.0
    N = 64
    cell = 25.0 / N
    grid = [[None] * (N + 1) for _ in range(N + 1)]
    D = [[0.0] * (N + 1) for _ in range(N + 1)]
    for i in range(N + 1):
        for j in range(N + 1):
            x, y = -12.5 + i * cell, -12.5 + j * cell
            d = dist(x, y)
            D[i][j] = d
            grid[i][j] = Vector((x, y, profile(d)))
    water = Mesh('river_fork_Y_Water', 62)
    water.materials = [WATER]
    for i in range(N):
        for j in range(N):
            ds = [D[i][j], D[i + 1][j], D[i + 1][j + 1], D[i][j + 1]]
            dc = sum(ds) / 4.0
            pts = [grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]]
            if min(ds) < 5.0:
                cells = SAND if dc < 3.8 else GRASS_EDGE
                m.poly(pts, cells)
            if min(ds) < 3.3:
                zw = 0.085
                f = water.poly([(p.x, p.y, zw) for p in pts], WATER_IN if dc < 0.9 else WATER_OUT, 0)
    ground = m.finish(closed=False, up_only=True)
    wobj = water.finish(closed=False, up_only=True)
    return [ground, wobj]


MODELS = {
    'windmill': ('buildings/windmill.fbx', windmill),
    'road_dirt_cross': ('structures/road_dirt_cross.fbx', road_dirt_cross),
    'plateau_step': ('terrain/plateau_step.fbx', plateau_step),
    'ramp_dirt': ('terrain/ramp_dirt.fbx', ramp_dirt),
    'waterfall_cliff': ('terrain/waterfall_cliff.fbx', waterfall_cliff),
    'river_fork_Y': ('water/river_fork_Y.fbx', river_fork_Y),
}


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)


def export(objs, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='STRIP')


for name, (rel, builder) in MODELS.items():
    if ONLY and name not in ONLY:
        continue
    clear_scene()
    objs = builder()
    tris = sum(len(o.data.polygons) for o in objs)
    dims = [tuple(round(v, 2) for v in o.dimensions) for o in objs]
    export(objs, os.path.join(OUT, rel))
    print('REMADE', name, 'tris', tris, 'dims', dims)
