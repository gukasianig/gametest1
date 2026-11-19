"""Варианты поля арены для выбора (только превью, в игру не подключаются).
Запуск: blender -b -P make_arena_variants.py  -> docs/art/Arena/Variant_<имя>_overview.png и _game.png"""
import math
import random
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

OUT = Path(__file__).resolve().parents[2] / "docs/art/Arena"
OUT.mkdir(parents=True, exist_ok=True)
H = 110.0


def hx(s):
    return tuple(int(s[i:i + 2], 16) / 255 for i in (0, 2, 4))


def hsh(a, b, s=0):
    n = (a * 374761393 + b * 668265263 + s * 2147483647) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def vnoise(x, z, s):
    ix, iz = math.floor(x), math.floor(z)
    fx, fz = x - ix, z - iz
    fx, fz = fx * fx * (3 - 2 * fx), fz * fz * (3 - 2 * fz)
    a, b, c, d = hsh(ix, iz, s), hsh(ix + 1, iz, s), hsh(ix, iz + 1, s), hsh(ix + 1, iz + 1, s)
    return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fz


def fbm(x, z, s, o=3):
    t = n = 0.0
    amp, f = 1.0, 1.0
    for i in range(o):
        t += vnoise(x * f, z * f, s + i * 17) * amp
        n += amp
        amp *= 0.5
        f *= 2
    return t / n


def shade(rgb, k):
    return tuple(max(0.0, min(1.0, c * k)) for c in rgb)


def jit(rgb, amt, a, b, s=0):
    """Цвет с лёгким разбросом (4 уровня — палитра остаётся маленькой)."""
    level = int(hsh(a, b, s) * 4) / 3.0
    return shade(rgb, 1.0 - amt + 2 * amt * level)


class Scene:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UV")
        self.colors = {}

    def cell(self, rgb):
        key = tuple(round(c * 24) / 24 for c in rgb)
        if key not in self.colors:
            self.colors[key] = len(self.colors)
        i = self.colors[key]
        if i >= 256:
            i = 255
        return ((i % 16 + 0.5) / 16, (i // 16 + 0.5) / 16)

    def face(self, pts, rgb):
        verts = [self.bm.verts.new((p[0], -p[2], p[1])) for p in pts]
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return
        u = self.cell(rgb)
        for loop in f.loops:
            loop[self.uv].uv = u

    def box(self, c, s, rgb, top=None):
        x0, x1, y0, y1, z0, z1 = c[0] - s[0] / 2, c[0] + s[0] / 2, c[1] - s[1] / 2, c[1] + s[1] / 2, c[2] - s[2] / 2, c[2] + s[2] / 2
        self.face([(x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0)], top or rgb)
        sd = shade(rgb, 0.8)
        self.face([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], sd)
        self.face([(x1, y0, z0), (x0, y0, z0), (x0, y1, z0), (x1, y1, z0)], shade(rgb, 0.7))
        self.face([(x1, y0, z1), (x1, y0, z0), (x1, y1, z0), (x1, y1, z1)], shade(rgb, 0.9))
        self.face([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], shade(rgb, 0.75))

    def cone(self, c, r, h, rgb, sides=6, tip=0.0, rot=0.0):
        base = [(c[0] + math.cos(rot + 2 * math.pi * i / sides) * r, c[1], c[2] + math.sin(rot + 2 * math.pi * i / sides) * r) for i in range(sides)]
        top = [(c[0] + math.cos(rot + 2 * math.pi * i / sides) * r * tip, c[1] + h, c[2] + math.sin(rot + 2 * math.pi * i / sides) * r * tip) for i in range(sides)]
        for i in range(sides):
            j = (i + 1) % sides
            k = 0.65 + 0.35 * (i % 3) / 2
            if tip > 0.001:
                self.face([base[j], base[i], top[i], top[j]], shade(rgb, k))
            else:
                self.face([base[j], base[i], top[i]], shade(rgb, k))
        self.face(list(reversed(top)), shade(rgb, 1.1))

    def terrain(self, rim, outer, step, colorfn, hfn, hole):
        n = int(2 * outer / step)
        v = {}
        for i in range(n + 1):
            for j in range(n + 1):
                x = -outer + i * step + (hsh(i, j, 3) - 0.5) * step * 0.5
                z = -outer + j * step + (hsh(i, j, 4) - 0.5) * step * 0.5
                v[(i, j)] = (x, hfn(x, z), z)
        for i in range(n):
            for j in range(n):
                q = [v[(i, j)], v[(i, j + 1)], v[(i + 1, j + 1)], v[(i + 1, j)]]
                cx, cz = sum(p[0] for p in q) / 4, sum(p[2] for p in q) / 4
                if hole(cx, cz):
                    continue
                col = colorfn(q, i, j)
                self.face([q[0], q[1], q[2]], col)
                self.face([q[0], q[2], q[3]], col)

    def build(self, mat_name):
        pal = bpy.data.images.new(self.name + "_pal", 256, 256, alpha=False)
        px = [0.0] * (256 * 256 * 4)
        arr = list(self.colors.items())
        for idx, (rgb, i) in enumerate(arr):
            cx, cy = (i % 16) * 16, (i // 16) * 16
            for y in range(cy, cy + 16):
                for x in range(cx, cx + 16):
                    o = (y * 256 + x) * 4
                    px[o], px[o + 1], px[o + 2], px[o + 3] = rgb[0], rgb[1], rgb[2], 1.0
        pal.pixels.foreach_set(px)
        pal.pack()
        mat = bpy.data.materials.new(mat_name)
        mat.use_nodes = True
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = pal
        tex.interpolation = 'Closest'
        mat.node_tree.links.new(tex.outputs["Color"], mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        return obj


def ring_quads(s, r0, r1, y, seg, colorfn):
    for i in range(seg):
        a0, a1 = 2 * math.pi * i / seg, 2 * math.pi * (i + 1) / seg
        s.face([(math.cos(a0) * r0, y, math.sin(a0) * r0), (math.cos(a0) * r1, y, math.sin(a0) * r1),
                (math.cos(a1) * r1, y, math.sin(a1) * r1), (math.cos(a1) * r0, y, math.sin(a1) * r0)], colorfn(i))


# ====================================================================== 1. Колизей
def colosseum():
    s = Scene("Colosseum")
    R = 112.0
    # песчаное поле: кольца и сектора разных оттенков, звезда-компас из тёмного камня
    sand = hx("C9A66B")
    for ri in range(22):
        r0, r1 = ri * 5.2, (ri + 1) * 5.2
        for i in range(72):
            a0, a1 = 2 * math.pi * i / 72, 2 * math.pi * (i + 1) / 72
            col = jit(sand, 0.12, ri, i, 1)
            if ri in (6, 7) or (ri == 14):
                col = jit(hx("8A6E49"), 0.1, ri, i, 2)
            s.face([(math.cos(a0) * r0, 0.0, math.sin(a0) * r0), (math.cos(a0) * r1, 0.0, math.sin(a0) * r1),
                    (math.cos(a1) * r1, 0.0, math.sin(a1) * r1), (math.cos(a1) * r0, 0.0, math.sin(a1) * r0)], col)
    # звезда: 8 лучей из каменных плит
    for k in range(8):
        a = math.pi / 4 * k
        for d in range(2, 20):
            r = d * 5.0 + 2.5
            s.box((math.cos(a) * r, 0.02, math.sin(a) * r), (4.0, 0.04, 4.0), jit(hx("6F6A60"), 0.1, k, d, 3))
    ring_quads(s, 12.0, 13.0, 0.05, 48, lambda i: hx("5A5248") if i % 2 else hx("D9C58F"))
    # стена-трибуны: ярусы из каменных секций с арками
    stone = hx("A99A82")
    segs = 72
    for i in range(segs):
        a = 2 * math.pi * (i + 0.5) / segs
        c, sn = math.cos(a), math.sin(a)
        w = 2 * math.pi * R / segs
        for tier, (r, hgt, y0) in enumerate([(R + 3, 5.0, 0), (R + 8, 7.0, 5.0), (R + 13, 7.0, 12.0)]):
            col = jit(stone, 0.12, i, tier, 4)
            s.cone((c * r, y0, sn * r), w * 0.62, hgt, col, sides=4, tip=0.92, rot=a + math.pi / 4)
        # арка: тёмная вставка
        s.cone((c * (R + 0.6), 0.0, sn * (R + 0.6)), w * 0.28, 3.6, hx("2A2622"), sides=4, tip=0.9, rot=a + math.pi / 4)
        if i % 3 == 0:    # зубцы
            s.cone((c * (R + 14), 19.0, sn * (R + 14)), w * 0.36, 1.6, hx("C2B39A"), sides=4, tip=0.9, rot=a + math.pi / 4)
    # колонны по кругу
    for i in range(24):
        a = 2 * math.pi * i / 24
        px, pz = math.cos(a) * (R - 4), math.sin(a) * (R - 4)
        s.cone((px, 0, pz), 1.9, 9.0, hx("D8CDB6"), sides=10, tip=0.85)
        s.cone((px, 9.0, pz), 2.6, 0.8, hx("B8A98F"), sides=10, tip=1.0)
        s.cone((px, 0, pz), 2.6, 0.8, hx("B8A98F"), sides=10, tip=1.0)
    # окрестности: травяные холмы
    def hfn(x, z):
        d = math.hypot(x, z)
        t = max(0.0, (d - (R + 18)) / 90.0)
        return 16.0 + 34.0 * t ** 1.2 + (fbm(x * 0.02, z * 0.02, 5) - 0.5) * 24 * min(1, t * 3)

    def cfn(q, i, j):
        h = sum(p[1] for p in q) / 4
        if h > 40:
            return jit(hx("8E8A80"), 0.1, i, j, 6)
        return jit(hx("5E8A3E") if hsh(i, j, 9) > 0.25 else hx("7A9A48"), 0.12, i, j, 7)
    s.terrain(R + 17, 230, 7.0, cfn, hfn, lambda x, z: math.hypot(x, z) < R + 17)
    return s


# ====================================================================== 2. Кристальная пещера
def crystal():
    s = Scene("Crystal")
    rng = random.Random(7)
    base = hx("2B2538")
    for i in range(44):
        for j in range(44):
            cx, cz = -H + (i + 0.5) * 5.0, -H + (j + 0.5) * 5.0
            hgt = (hsh(i, j, 2) - 0.5) * 0.12
            col = jit(base, 0.22, i, j, 3)
            s.box((cx, -0.4 + hgt / 2, cz), (4.8, 0.8 + hgt, 4.8), col)
    # фиолетовые светящиеся жилы
    for k in range(60):
        x, z = rng.uniform(-H + 6, H - 6), rng.uniform(-H + 6, H - 6)
        a = rng.uniform(0, math.pi)
        for step in range(rng.randint(5, 12)):
            a += rng.uniform(-0.6, 0.6)
            x += math.cos(a) * 2.2
            z += math.sin(a) * 2.2
            if abs(x) > H - 3 or abs(z) > H - 3:
                break
            s.box((x, 0.03, z), (2.4, 0.02, 0.28), hx("B35CFF") if step % 2 else hx("8A3FE0"))
    # кристаллические кластеры по полю
    cols = [hx("57F0FF"), hx("FF6BD6"), hx("9B7CFF")]
    for k in range(70):
        x, z = rng.uniform(-H + 8, H - 8), rng.uniform(-H + 8, H - 8)
        if math.hypot(x, z) < 14:
            continue
        for m in range(rng.randint(2, 5)):
            s.cone((x + rng.uniform(-1.4, 1.4), 0, z + rng.uniform(-1.4, 1.4)), rng.uniform(0.35, 0.8), rng.uniform(1.4, 4.2),
                   rng.choice(cols), sides=5, tip=0.12, rot=rng.random())
    # центральная площадка
    ring_quads(s, 4.8, 5.4, 0.05, 40, lambda i: hx("57F0FF"))
    # периметр: сталагмитовые колонны + кольцо кристаллов
    for side in range(4):
        for i in range(-22, 23):
            t = i * 5.0
            x, z = (t, H) if side == 0 else (t, -H) if side == 1 else (H, t) if side == 2 else (-H, t)
            hgt = 6 + hsh(i, side, 9) * 14
            s.cone((x, 0, z), 3.6 + hsh(i, side, 5) * 2, hgt, jit(hx("3A3350"), 0.2, i, side, 6), sides=6, tip=0.25)
            if i % 4 == 0:
                s.cone((x * 0.97, 0, z * 0.97), 1.0, hgt * 0.6, hx("57F0FF"), sides=5, tip=0.1)
    def hfn(x, z):
        d = max(abs(x), abs(z))
        t = max(0.0, (d - (H + 6)) / 100.0)
        return 10 + 55 * t ** 1.1 + (fbm(x * 0.03, z * 0.03, 12) - 0.5) * 30 * min(1, t * 3)

    def cfn(q, i, j):
        h = sum(p[1] for p in q) / 4
        k = hsh(i, j, 8)
        if k > 0.94:
            return hx("B35CFF")
        return jit(hx("463C5E") if h < 45 else hx("6A5E8A"), 0.18, i, j, 7)
    s.terrain(H + 5, 225, 6.0, cfn, hfn, lambda x, z: max(abs(x), abs(z)) < H + 5)
    return s


# ====================================================================== 3. Ледяное поле
def ice():
    s = Scene("Ice")
    rng = random.Random(21)
    for i in range(22):
        for j in range(22):
            cx, cz = -H + (i + 0.5) * 10.0, -H + (j + 0.5) * 10.0
            s.box((cx, -0.3, cz), (9.8, 0.6, 9.8), jit(hx("9FD3F0"), 0.12, i, j, 2), top=jit(hx("BFE6FA"), 0.1, i, j, 3))
    # трещины
    for k in range(40):
        x, z = rng.uniform(-H + 8, H - 8), rng.uniform(-H + 8, H - 8)
        a = rng.uniform(0, math.pi)
        for step in range(rng.randint(6, 16)):
            a += rng.uniform(-0.5, 0.5)
            x += math.cos(a) * 1.9
            z += math.sin(a) * 1.9
            if abs(x) > H - 3 or abs(z) > H - 3:
                break
            s.box((x, 0.02, z), (2.0, 0.015, 0.16), hx("3A6EA5"))
    # сугробы
    for k in range(90):
        x, z = rng.uniform(-H + 6, H - 6), rng.uniform(-H + 6, H - 6)
        if math.hypot(x, z) < 12:
            continue
        s.cone((x, 0, z), rng.uniform(1.5, 4.0), rng.uniform(0.5, 1.4), hx("F4FBFF"), sides=7, tip=0.35)
    ring_quads(s, 4.8, 5.4, 0.05, 40, lambda i: hx("2F7BD8"))
    # ледяные шипы по периметру
    for side in range(4):
        for i in range(-30, 31):
            t = i * 3.6
            x, z = (t, H) if side == 0 else (t, -H) if side == 1 else (H, t) if side == 2 else (-H, t)
            hgt = 4 + hsh(i, side, 4) * 12
            s.cone((x, 0, z), 2.3 + hsh(i, side, 6) * 1.2, hgt, jit(hx("7FC4F0"), 0.15, i, side, 5), sides=4, tip=0.08, rot=hsh(i, side, 7))
    def hfn(x, z):
        d = max(abs(x), abs(z))
        t = max(0.0, (d - (H + 6)) / 100.0)
        return 8 + 52 * t ** 1.15 + (fbm(x * 0.03, z * 0.03, 22) - 0.5) * 28 * min(1, t * 3)

    def cfn(q, i, j):
        h = sum(p[1] for p in q) / 4
        sl = max(p[1] for p in q) - min(p[1] for p in q)
        if sl > 7:
            return jit(hx("6FA6D6"), 0.14, i, j, 6)
        return jit(hx("EAF6FF") if h > 26 else hx("BFDDF2"), 0.08, i, j, 7)
    s.terrain(H + 5, 225, 6.0, cfn, hfn, lambda x, z: max(abs(x), abs(z)) < H + 5)
    return s


# ====================================================================== 4. Вулкан
def volcano():
    s = Scene("Volcano")
    rng = random.Random(33)
    for i in range(44):
        for j in range(44):
            cx, cz = -H + (i + 0.5) * 5.0, -H + (j + 0.5) * 5.0
            hgt = (hsh(i, j, 2) - 0.5) * 0.1
            s.box((cx, -0.4 + hgt / 2, cz), (4.55, 0.8 + hgt, 4.55), jit(hx("3B3835"), 0.2, i, j, 3))
    # лава в швах: оранжевая подложка видна между плитами
    s.box((0, -0.85, 0), (2 * H + 12, 0.3, 2 * H + 12), hx("FF7A18"))
    # раскалённые трещины
    for k in range(70):
        x, z = rng.uniform(-H + 6, H - 6), rng.uniform(-H + 6, H - 6)
        a = rng.uniform(0, math.pi)
        for step in range(rng.randint(4, 10)):
            a += rng.uniform(-0.7, 0.7)
            x += math.cos(a) * 2.0
            z += math.sin(a) * 2.0
            if abs(x) > H - 3 or abs(z) > H - 3:
                break
            s.box((x, 0.03, z), (2.2, 0.02, 0.3), hx("FFC23A") if step % 2 else hx("FF8A1E"))
    ring_quads(s, 4.8, 5.4, 0.05, 40, lambda i: hx("FFC23A"))
    # борт: чёрные базальтовые колонны
    for side in range(4):
        for i in range(-30, 31):
            t = i * 3.6
            x, z = (t, H) if side == 0 else (t, -H) if side == 1 else (H, t) if side == 2 else (-H, t)
            hgt = 5 + hsh(i, side, 4) * 9
            s.cone((x, 0, z), 2.2, hgt, jit(hx("2B2927"), 0.25, i, side, 5), sides=6, tip=0.9)
            s.cone((x, hgt, z), 2.0, 0.3, hx("FF8A1E"), sides=6, tip=0.95)
    # лавовое озеро вокруг: яркое кольцо перед скалами
    def hfn(x, z):
        d = max(abs(x), abs(z))
        t = max(0.0, (d - (H + 22)) / 90.0)
        if d < H + 20:
            return 2.0 + 3.0 * (d - (H + 4)) / 16.0 if d > H + 4 else -0.2
        return 6 + 60 * t ** 1.1 + (fbm(x * 0.03, z * 0.03, 32) - 0.5) * 26 * min(1, t * 3)

    def cfn(q, i, j):
        h = sum(p[1] for p in q) / 4
        d = max(abs(sum(p[0] for p in q) / 4), abs(sum(p[2] for p in q) / 4))
        if d < H + 14:
            return jit(hx("FF7A18"), 0.12, i, j, 6)         # лава
        if hsh(i, j, 8) > 0.92:
            return hx("FF8A1E")
        return jit(hx("4A3B36") if h < 40 else hx("2E2826"), 0.2, i, j, 7)
    s.terrain(H + 3, 225, 6.0, cfn, hfn, lambda x, z: max(abs(x), abs(z)) < H + 3)
    return s


def render(obj, name, shots):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.display.shading.show_shadows = True
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    for tag, (loc, target, lens) in shots.items():
        cam_data = bpy.data.cameras.new(tag)
        cam_data.lens = lens
        cam_data.clip_end = 3000
        cam = bpy.data.objects.new(tag, cam_data)
        bpy.context.collection.objects.link(cam)
        cam.location = loc
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        scene.camera = cam
        scene.render.filepath = str(OUT / ("Variant_%s_%s.png" % (name, tag)))
        bpy.ops.render.render(write_still=True)


SHOTS = {
    "overview": ((0, -330, 230), (0, 0, 0), 30),
    "game": ((0, -48, 36), (0, 12, 0), 40),
}

if __name__ == "__main__":
  for fn in (colosseum, crystal, ice, volcano):
      bpy.ops.wm.read_factory_settings(use_empty=True)
      sc = fn()
      obj = sc.build("M_" + sc.name)
      render(obj, sc.name, SHOTS)
      print("DONE", sc.name, len(obj.data.polygons))
