"""Ещё шесть вариантов арены (превью): пустыня, болото, станция, небесные острова, кладбище, неон-город.
Запуск: blender -b -P make_arena_variants2.py -> docs/art/Arena/Variant_<имя>_overview.png / _game.png"""
import math
import random
import runpy
from pathlib import Path

import bpy
from mathutils import Vector

lib = runpy.run_path(str(Path(__file__).parent / "make_arena_variants.py"), run_name="lib")
Scene, hx, hsh, fbm, jit, shade, ring_quads, OUT, H = (lib[k] for k in ("Scene", "hx", "hsh", "fbm", "jit", "shade", "ring_quads", "OUT", "H"))


def edge_pts(side, t):
    return (t, H) if side == 0 else (t, -H) if side == 1 else (H, t) if side == 2 else (-H, t)


def tiles(s, n, size, colfn, y=-0.4, thick=0.8, gap=0.2):
    for i in range(n):
        for j in range(n):
            cx, cz = -H + (i + 0.5) * size, -H + (j + 0.5) * size
            s.box((cx, y, cz), (size - gap, thick, size - gap), colfn(i, j))


def outer(s, rim, out, step, cfn, hfn):
    s.terrain(rim, out, step, cfn, hfn, lambda x, z: max(abs(x), abs(z)) < rim)


def ramp_h(d0, base, mult, seed, wob=26.0, scale=0.03, pw=1.15, span=100.0):
    def f(x, z):
        d = max(abs(x), abs(z))
        t = max(0.0, (d - d0) / span)
        return base + mult * t ** pw + (fbm(x * scale, z * scale, seed) - 0.5) * wob * min(1, t * 3)
    return f


# 1 ------------------------------------------------------------- Пустыня
def desert():
    s = Scene("Desert")
    rng = random.Random(5)
    tiles(s, 22, 10.0, lambda i, j: jit(hx("D9B77A"), 0.1, i, j, 2), thick=0.6, gap=0.05)
    for k in range(110):  # песчаные волны и камни
        x, z = rng.uniform(-H + 6, H - 6), rng.uniform(-H + 6, H - 6)
        if math.hypot(x, z) < 12:
            continue
        if k % 3:
            s.cone((x, 0, z), rng.uniform(2, 5), rng.uniform(0.3, 0.9), hx("E8C98F"), sides=8, tip=0.3)
        else:
            s.cone((x, 0, z), rng.uniform(0.8, 1.8), rng.uniform(0.8, 2.0), hx("9C8160"), sides=5, tip=0.5, rot=rng.random())
    for k in range(10):   # руины: колонны и арки
        a = rng.uniform(0, 6.28)
        r = rng.uniform(30, 95)
        x, z = math.cos(a) * r, math.sin(a) * r
        for m in range(rng.randint(2, 4)):
            s.cone((x + m * 4, 0, z), 1.2, rng.uniform(3, 8), hx("C9A872"), sides=8, tip=0.9)
    ring_quads(s, 4.8, 5.4, 0.05, 40, lambda i: hx("3FB6C4"))
    for side in range(4):  # песчаниковая стена и обелиски
        for i in range(-22, 23):
            x, z = edge_pts(side, i * 5.0)
            s.cone((x, 0, z), 3.4, 5 + hsh(i, side, 4) * 4, jit(hx("B98E55"), 0.12, i, side, 5), sides=4, tip=0.85, rot=0.78)
        for i in (-18, 0, 18):
            x, z = edge_pts(side, i * 5.0)
            s.cone((x * 0.98, 0, z * 0.98), 2.4, 16, hx("D9B77A"), sides=4, tip=0.3, rot=0.78)
    outer(s, H + 5, 225, 6.0, lambda q, i, j: jit(hx("E3BE80") if sum(p[1] for p in q) / 4 < 40 else hx("B98E55"), 0.1, i, j, 7),
          ramp_h(H + 6, 8, 40, 14, 30, 0.02))
    return s, (0.55, 0.75, 0.95)


# 2 ------------------------------------------------------------- Болото
def swamp():
    s = Scene("Swamp")
    rng = random.Random(9)
    tiles(s, 44, 5.0, lambda i, j: jit(hx("4B4A2E") if hsh(i, j, 8) > 0.2 else hx("2F5C4A"), 0.16, i, j, 2), thick=0.7, gap=0.08)
    for k in range(40):   # тёмные лужи
        x, z = rng.uniform(-H + 10, H - 10), rng.uniform(-H + 10, H - 10)
        if math.hypot(x, z) < 14:
            continue
        s.cone((x, 0.01, z), rng.uniform(3, 7), 0.02, hx("1F3D36"), sides=10, tip=1.0)
    for k in range(70):   # кочки и грибы
        x, z = rng.uniform(-H + 6, H - 6), rng.uniform(-H + 6, H - 6)
        if math.hypot(x, z) < 12:
            continue
        s.cone((x, 0, z), 0.3, rng.uniform(0.8, 1.8), hx("D9D2A0"), sides=6, tip=0.8)
        s.cone((x, 1.2, z), 1.1, 0.5, hx("7CFF9B") if k % 2 else hx("C5FF5A"), sides=8, tip=0.3)
    for k in range(34):   # коряги
        x, z = rng.uniform(-H + 8, H - 8), rng.uniform(-H + 8, H - 8)
        if math.hypot(x, z) < 16:
            continue
        s.cone((x, 0, z), 0.7, rng.uniform(4, 8), hx("3A2E22"), sides=6, tip=0.4)
        s.cone((x + 0.8, 3.5, z), 0.35, 3.0, hx("3A2E22"), sides=5, tip=0.2, rot=0.3)
    ring_quads(s, 4.8, 5.4, 0.05, 40, lambda i: hx("7CFF9B"))
    for side in range(4):  # стена из деревьев
        for i in range(-30, 31):
            x, z = edge_pts(side, i * 3.6)
            h = 9 + hsh(i, side, 4) * 10
            s.cone((x, 0, z), 0.9, h, hx("3A2E22"), sides=6, tip=0.7)
            s.cone((x, h * 0.55, z), 3.6, h * 0.55, jit(hx("2A5A38"), 0.2, i, side, 3), sides=7, tip=0.05)
    outer(s, H + 5, 225, 6.0, lambda q, i, j: jit(hx("2F4A34") if sum(p[1] for p in q) / 4 < 36 else hx("4A5C3C"), 0.18, i, j, 7),
          ramp_h(H + 6, 14, 38, 24, 24))
    return s, (0.18, 0.28, 0.22)


# 3 ------------------------------------------------------------- Станция (космос)
def station():
    s = Scene("Station")
    rng = random.Random(3)
    tiles(s, 22, 10.0, lambda i, j: jit(hx("5C6672") if (i + j) % 2 else hx("4B5560"), 0.12, i, j, 2), thick=0.6, gap=0.18)
    for i in range(22):
        s.box((-H + (i + 0.5) * 10, 0.04, 0), (9.6, 0.02, 0.35), hx("39E0FF"))
        s.box((0, 0.04, -H + (i + 0.5) * 10), (0.35, 0.02, 9.6), hx("39E0FF"))
    ring_quads(s, 4.8, 5.6, 0.05, 40, lambda i: hx("FF8A2A") if i % 2 else hx("1B1F26"))
    for k in range(18):   # терминалы и контейнеры
        x, z = rng.uniform(-H + 14, H - 14), rng.uniform(-H + 14, H - 14)
        if math.hypot(x, z) < 16:
            continue
        s.box((x, 1.0, z), (rng.uniform(3, 6), 2.0, rng.uniform(3, 6)), jit(hx("8A96A3"), 0.15, k, 1, 4))
    for side in range(4):  # борт: окна в космос
        for i in range(-11, 12):
            x, z = edge_pts(side, i * 10.0)
            w = (9.6, 6, 3) if side < 2 else (3, 6, 9.6)
            s.box((x, 3, z * 1.0), w, hx("2A3038"))
            s.box((x, 3.2, z * 0.985), (7.6, 2.2, 0.3) if side < 2 else (0.3, 2.2, 7.6), hx("39E0FF"))
    for k in range(60):   # астероиды вдали
        a = rng.uniform(0, 6.28)
        r = rng.uniform(160, 330)
        s.cone((math.cos(a) * r, rng.uniform(-60, 80), math.sin(a) * r), rng.uniform(5, 18), rng.uniform(6, 20),
               jit(hx("6B635C"), 0.2, k, 2, 5), sides=6, tip=0.5, rot=rng.random())
    return s, (0.015, 0.02, 0.05)


# 4 ------------------------------------------------------------- Небесные острова
def sky():
    s = Scene("Sky")
    rng = random.Random(11)
    tiles(s, 22, 10.0, lambda i, j: jit(hx("CFC8B6"), 0.08, i, j, 2), thick=0.8, gap=0.1)
    for i in range(22):   # травяной ковёр по краю
        for side in range(4):
            x, z = edge_pts(side, -H + (i + 0.5) * 10)
            ex = (z * 0.0)
            s.box((x * 0.96, 0.02, z * 0.96), (9.6 if side < 2 else 4, 0.06, 4 if side < 2 else 9.6), jit(hx("6FB04A"), 0.1, i, side, 3))
    ring_quads(s, 4.8, 5.6, 0.05, 40, lambda i: hx("FFD84A"))
    for k in range(14):   # цветы-кристаллы
        x, z = rng.uniform(-H + 12, H - 12), rng.uniform(-H + 12, H - 12)
        if math.hypot(x, z) < 14:
            continue
        s.cone((x, 0, z), 0.5, rng.uniform(1.5, 3), hx("FF7AB6"), sides=5, tip=0.15)
    for k in range(10):   # колонны-ворота
        a = 2 * math.pi * k / 10
        s.cone((math.cos(a) * (H - 6), 0, math.sin(a) * (H - 6)), 1.6, 11, hx("F0EAD8"), sides=8, tip=0.85)
    # остров снизу: перевёрнутая гора
    for ring in range(8):
        r = (H + 8) * (1 - ring / 9)
        s.cone((0, -7.0 - ring * 6.0, 0), r * 1.45, 6.0, jit(hx("8A7A66"), 0.15, ring, 1, 6), sides=4, tip=0.78, rot=0.78)
    for k in range(26):   # плавающие острова
        a = rng.uniform(0, 6.28)
        r = rng.uniform(170, 330)
        x, y, z = math.cos(a) * r, rng.uniform(-30, 60), math.sin(a) * r
        w = rng.uniform(14, 34)
        s.cone((x, y, z), w, 5.0, hx("6FB04A"), sides=7, tip=1.0)
        s.cone((x, y - 18, z), w, 18, hx("8A7A66"), sides=7, tip=0.1, rot=3.14)
        s.cone((x, y + 5, z), 1.2, 7, hx("3E7A3A"), sides=6, tip=0.05)
    for k in range(40):   # облака
        a = rng.uniform(0, 6.28)
        r = rng.uniform(140, 340)
        s.cone((math.cos(a) * r, rng.uniform(-90, -60), math.sin(a) * r), rng.uniform(20, 50), 12, hx("FFFFFF"), sides=8, tip=0.6)
    return s, (0.45, 0.72, 0.95)


# 5 ------------------------------------------------------------- Кладбище
def graveyard():
    s = Scene("Graveyard")
    rng = random.Random(17)
    tiles(s, 44, 5.0, lambda i, j: jit(hx("3A3A46") if hsh(i, j, 3) > 0.25 else hx("2A3F34"), 0.18, i, j, 2), thick=0.7, gap=0.06)
    for k in range(120):   # надгробия
        x, z = rng.uniform(-H + 8, H - 8), rng.uniform(-H + 8, H - 8)
        if math.hypot(x, z) < 14:
            continue
        s.box((x, 0.9, z), (1.3, 1.8, 0.35), jit(hx("8D8D99"), 0.15, k, 2, 5))
        s.cone((x, 1.8, z), 0.65, 0.3, hx("8D8D99"), sides=8, tip=0.9)
    for k in range(34):   # мёртвые деревья
        x, z = rng.uniform(-H + 8, H - 8), rng.uniform(-H + 8, H - 8)
        if math.hypot(x, z) < 16:
            continue
        s.cone((x, 0, z), 0.6, rng.uniform(5, 9), hx("261F26"), sides=5, tip=0.3)
        s.cone((x + 0.5, 4.5, z), 0.3, 3.5, hx("261F26"), sides=4, tip=0.1, rot=0.5)
    for k in range(36):   # фонари
        x, z = rng.uniform(-H + 8, H - 8), rng.uniform(-H + 8, H - 8)
        s.cone((x, 0, z), 0.12, 2.6, hx("1A1A20"), sides=4, tip=1.0)
        s.cone((x, 2.6, z), 0.4, 0.6, hx("9DFF6A"), sides=6, tip=0.8)
    ring_quads(s, 4.8, 5.6, 0.05, 40, lambda i: hx("B39CFF"))
    for side in range(4):  # кованая ограда
        for i in range(-30, 31):
            x, z = edge_pts(side, i * 3.6)
            s.cone((x, 0, z), 0.3, 6.5, hx("15151A"), sides=4, tip=0.2)
            if i % 5 == 0:
                s.cone((x, 0, z), 1.2, 10, hx("2A2A34"), sides=4, tip=0.7, rot=0.78)
    outer(s, H + 5, 225, 6.0, lambda q, i, j: jit(hx("2C2D3C") if sum(p[1] for p in q) / 4 < 38 else hx("46476A"), 0.2, i, j, 7),
          ramp_h(H + 6, 12, 48, 30, 28))
    return s, (0.09, 0.08, 0.16)


# 6 ------------------------------------------------------------- Неон-город
def neon():
    s = Scene("Neon")
    rng = random.Random(23)
    tiles(s, 22, 10.0, lambda i, j: jit(hx("14161F"), 0.15, i, j, 2), thick=0.6, gap=0.12)
    for i in range(23):
        pos = -H + i * 10
        s.box((pos, 0.04, 0), (0.22, 0.02, 2 * H), hx("FF3FD0") if i % 2 else hx("3FE8FF"))
        s.box((0, 0.04, pos), (2 * H, 0.02, 0.22), hx("3FE8FF") if i % 2 else hx("FF3FD0"))
    ring_quads(s, 4.8, 5.8, 0.06, 40, lambda i: hx("FFFFFF") if i % 2 else hx("FF3FD0"))
    for k in range(22):   # пилоны-светильники
        x, z = rng.uniform(-H + 12, H - 12), rng.uniform(-H + 12, H - 12)
        if math.hypot(x, z) < 16:
            continue
        s.box((x, 2.5, z), (1.2, 5.0, 1.2), hx("20243A"))
        s.box((x, 5.2, z), (1.6, 0.5, 1.6), hx("3FE8FF") if k % 2 else hx("FF3FD0"))
    for side in range(4):   # светящийся борт
        for i in range(-22, 23):
            x, z = edge_pts(side, i * 5.0)
            dims = (4.8, 3.5, 2.5) if side < 2 else (2.5, 3.5, 4.8)
            s.box((x, 1.7, z), dims, hx("1B1F33"))
            s.box((x * 0.99, 3.6, z * 0.99), (4.8, 0.25, 0.25) if side < 2 else (0.25, 0.25, 4.8), hx("FF3FD0") if i % 2 else hx("3FE8FF"))
    for k in range(260):   # небоскрёбы вокруг
        x, z = rng.uniform(-250, 250), rng.uniform(-250, 250)
        if max(abs(x), abs(z)) < H + 45:
            continue
        w, h = rng.uniform(8, 20), rng.uniform(20, 90)
        s.box((x, h / 2, z), (w, h, w), jit(hx("1A1E34"), 0.25, k, 1, 3))
        for m in range(int(h // 8)):
            if rng.random() < 0.6:
                s.box((x, 4 + m * 8, z + w / 2 + 0.05), (w * 0.7, 1.2, 0.1), hx("FF3FD0") if m % 3 == 0 else hx("3FE8FF") if m % 3 == 1 else hx("FFE36B"))
    return s, (0.02, 0.02, 0.06)


def render(obj, name, bg):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.display.shading.show_shadows = True
    world = bpy.data.worlds.new("w")
    world.color = bg
    scene.world = world
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    for tag, (loc, target, lens) in lib["SHOTS"].items():
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


for fn in (sky, neon):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc, bg = fn()
    obj = sc.build("M_" + sc.name)
    render(obj, sc.name, bg)
    print("DONE", sc.name, len(obj.data.polygons))
