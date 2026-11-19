"""Мировые предметы в стиле оружия героя (make_hero_weapons.py): крупные скошенные формы, плоская палитра 4×4 с градиентом,
светящиеся части — отдельным материалом «<Имя> Glow». Просьба владельца 11.11.2026: модели для предметов, у которых их не было.
Запуск: blender -b -P tools/blender/make_items.py [-- Crystal Mine ...]   (без списка — все)
Результат: docs/art/Items/<Имя>/ (BLEND, FBX, палитра, три ракурса, VERIFICATION.json); установка — Tools > Install Blender Item Models.
Ключи ModelSkinSet: Crystal, Repair kit, Magnet, Mine, Turret, Drone, Saw, Chest.
Части не называть Body/Carapace/Leg*/Wing* — по ним ModelWalkAnimator включает анимацию врага.
Подвижные части (их ищет код игры по имени): Turret → Head (+ маркер TurretMuzzle), Drone → RotorL/RotorR, Mine → Light, Chest → Lid."""

import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
import kit
from creature_mesh import Part, render_model, save_model, validate, verify_fbx, world_vertices
from weapon_kit import (ROOT, make_atlas, shade_uvs, shell_color, palette, block, rod, circle,
                        GRAPHITE, DARK, STEEL, LIGHT, WHITE, VOID, RUBBER, BRASS,
                        MAIN, MAIN_DARK, ACCENT, ACCENT_DARK, GLOW, HOT, EXTRA, EXTRA2)


# ---------- вертикальные сечения (ось Z вверх) ----------
def hring(z, rx, ry=None, sides=8, phase=.5, cx=0.0, cy=0.0):
    ry = rx if ry is None else ry
    return [(cx + rx * math.cos((i + phase) * math.tau / sides), cy + ry * math.sin((i + phase) * math.tau / sides), z) for i in range(sides)]


def hbox(z, wx, wy, cut, cx=0.0, cy=0.0):
    w, h = wx / 2, wy / 2
    cut = min(cut, w * .9, h * .9)
    return [(cx + a, cy + b, z) for a, b in ((-w + cut, -h), (w - cut, -h), (w, -h + cut), (w, h - cut),
                                             (w - cut, h), (-w + cut, h), (-w, h - cut), (-w, -h + cut))]


def turn(ring, angle):
    c, s = math.cos(angle), math.sin(angle)
    return [(x * c - y * s, x * s + y * c, z) for x, y, z in ring]


def vblock(part, z0, z1, wx, wy, cut, side, top=None, bottom=DARK, cx=0.0, cy=0.0, angle=0.0):
    """Вертикальный скошенный брусок: фаски сверху и снизу, свой цвет боков, верха и низа."""
    bevel = min(.012, wx * .2, wy * .2, (z1 - z0) * .3)
    rings = [hbox(z0, wx - 2 * bevel, wy - 2 * bevel, cut, cx, cy), hbox(z0 + bevel, wx, wy, cut, cx, cy),
             hbox(z1 - bevel, wx, wy, cut, cx, cy), hbox(z1, wx - 2 * bevel, wy - 2 * bevel, cut, cx, cy)]
    if angle:
        rings = [turn(r, angle) for r in rings]
    part.rings(rings, side)
    part.colors[-1] = side if top is None else top
    part.colors[-(len(rings) - 1) * 8 - 2] = bottom


def column(part, sections, color, sides=12, phase=.5, top=None):
    """Тело вращения по списку (z, радиус)."""
    part.rings([hring(z, r, None, sides, phase) for z, r in sections], color)
    if top is not None:
        part.colors[-1] = top


# ---------------- Кристалл опыта: гранёный голубой, светится целиком ----------------
def crystal(add, glow_add):
    gem = Part()
    tones = lambda row, face: ((MAIN_DARK, MAIN), (MAIN, ACCENT), (ACCENT, HOT), (HOT, GLOW))[min(row, 3)][(face + row) % 2]
    gem.rings([hring(0, .006, None, 6), hring(.13, .1, None, 6), hring(.36, .115, None, 6, .5),
               hring(.5, .075, None, 6, 1.0), hring(.6, .006, None, 6, 1.0)], tones)
    glow_add("Gem", gem)
    shard = Part()
    shard.rings([hring(.02, .004, None, 5, 0, .1, .04), hring(.1, .045, None, 5, 0, .12, .04),
                 hring(.22, .004, None, 5, .5, .13, .04)], lambda row, face: MAIN if face % 2 else ACCENT)
    glow_add("Shard", shard)
    return None


# ---------------- Ремонтный набор: светлый кейс, красные кресты, ручка ----------------
def repair_kit(add, glow_add):
    body = Part()
    vblock(body, 0, .26, .5, .32, .045, MAIN, MAIN, DARK)
    add("Case", body)
    seam = Part()
    vblock(seam, .165, .19, .512, .332, .046, MAIN_DARK, MAIN_DARK, MAIN_DARK)
    add("Seam", seam)
    # Кресты из непересекающихся брусков (наложенные бруски давали чёрный квадрат в середине).
    cross = Part()
    vblock(cross, .255, .285, .22, .065, .012, ACCENT, ACCENT, ACCENT_DARK, cx=.06)
    for cy in (-.072, .072):
        vblock(cross, .255, .285, .065, .078, .01, ACCENT, ACCENT, ACCENT_DARK, cx=.06, cy=cy)
    for y0, y1 in ((-.175, -.158), (.158, .175)):
        block(cross, y0, y1, .1, .15, .045, .008, ACCENT)
        for dz in (-.05, .05):
            block(cross, y0, y1, .1 + dz, .045, .053, .008, ACCENT)
    add("Cross", cross)
    handle = Part()
    handle.tube([((-.12, -.07, .27), .02, .02), ((-.12, -.07, .33), .02, .02), ((-.09, -.07, .355), .02, .02), ((.03, -.07, .355), .02, .02),
                 ((.06, -.07, .33), .02, .02), ((.06, -.07, .27), .02, .02)], GRAPHITE, 8, transport=True)
    add("Handle", handle)
    latches = Part()
    for x in (-.19, .19):
        block(latches, -.172, -.155, .18, .05, .05, .01, EXTRA, x=x)
    add("Latches", latches)
    lamp = Part()
    vblock(lamp, .255, .275, .05, .05, .012, GLOW, GLOW, GLOW, cx=.19, cy=.11)
    glow_add("StatusGlow", lamp)


# ---------------- Магнит: красная подкова, стальные полюса, искры ----------------
def magnet(add, glow_add):
    path = [(-.15, 0, .44), (-.15, 0, .3), (-.15, 0, .2)]
    for i in range(1, 8):
        a = math.pi + i * math.pi / 8
        path.append((.15 * math.cos(a), 0, .2 + .15 * math.sin(a)))
    path += [(.15, 0, .2), (.15, 0, .3), (.15, 0, .44)]
    arm = Part()
    arm.tube([(p, .065, .075) for p in path], MAIN, 8, transport=True)
    add("Horseshoe", arm)
    tips = Part()
    for x in (-.15, .15):
        tips.tube([((x, 0, .435), .07, .08), ((x, 0, .53), .07, .08)], LIGHT, 8)
        tips.colors[-1] = WHITE
    add("Poles", tips)
    sparks = Part()
    for x, z, r in ((-.05, .6, .035), (.06, .64, .028), (0, .7, .022)):
        sparks.rings([hring(z - r, .004, None, 4, 0, x), hring(z, r, None, 4, 0, x), hring(z + r, .004, None, 4, 0, x)], GLOW)
    glow_add("Sparks", sparks)


# ---------------- Мина: сланцевый диск, жёлто-чёрная полоса, красный огонёк ----------------
def mine(add, glow_add):
    base = Part()
    stripes = lambda row, face: (ACCENT if face % 2 == 0 else ACCENT_DARK) if row == 1 else MAIN if row == 2 else MAIN_DARK
    base.rings([hring(0, .4, None, 16), hring(.02, .43, None, 16), hring(.075, .43, None, 16), hring(.1, .37, None, 16),
                hring(.12, .3, None, 16)], stripes)
    base.colors[-1] = MAIN
    add("Shell", base)
    cap = Part()
    column(cap, [(.115, .25), (.14, .23), (.16, .18)], MAIN_DARK, 12, top=GRAPHITE)
    add("Cap", cap)
    studs = Part()
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        studs.tube([((.3 * math.cos(a), .3 * math.sin(a), .1), .025, .025), ((.3 * math.cos(a), .3 * math.sin(a), .155), .02, .02)], GRAPHITE, 6)
    add("Studs", studs)
    light = Part()
    column(light, [(.155, .07), (.19, .062), (.215, .03)], GLOW, 10)
    glow_add("Light", light)


# ---------------- Турель: зелёная, поворотная голова со спаренным стволом ----------------
HEAD_PIVOT = Vector((0, 0, .32))


def turret(add, glow_add, head_add):
    base = Part()
    column(base, [(0, .36), (.05, .4), (.11, .38), (.16, .3)], MAIN_DARK, 8, top=DARK)
    add("Base", base)
    feet = Part()
    for k in range(3):
        vblock(feet, 0, .07, .14, .24, .03, GRAPHITE, DARK, DARK, cy=.36, angle=k * math.tau / 3)
    add("Feet", feet)
    neck = Part()
    column(neck, [(.15, .11), (.33, .09)], STEEL, 10, top=GRAPHITE)
    add("Neck", neck)
    head = Part()
    block(head, -.17, .15, .43, .3, .2, .05, shell_color(MAIN, MAIN, MAIN_DARK, DARK))
    block(head, -.12, .05, .43, .36, .08, .02, ACCENT)          # жёлтые щитки по бокам
    for x in (-.06, .06):
        rod(head, .14, .44, .44, .032, STEEL, 10, x=x, front=VOID)
        rod(head, .4, .47, .44, .042, GRAPHITE, 10, x=x, front=VOID)
    block(head, -.22, -.17, .41, .2, .12, .02, GRAPHITE)        # противовес сзади
    head_obj = head_add("Head", head)
    eye = Part()
    block(eye, .145, .16, .5, .14, .035, .008, GLOW)
    head_add("HeadGlow", eye, glow=True)
    kit.make_marker("TurretMuzzle", Vector((0, .47, .44)) - HEAD_PIVOT, head_obj)


# ---------------- Дрон: бирюзовый корпус, глаз, два винта ----------------
def drone(add, glow_add):
    body = Part()
    vblock(body, 0, .2, .34, .4, .07, MAIN, LIGHT, MAIN_DARK)
    add("Hull", body)
    dome = Part()
    column(dome, [(.195, .12), (.25, .095), (.28, .045)], MAIN_DARK, 10)
    add("Dome", dome)
    arms = Part()
    arms.tube([((-.42, 0, .12), .032, .026), ((.42, 0, .12), .032, .026)], GRAPHITE, 6)
    for x in (-.42, .42):
        arms.tube([((x, 0, .08), .055, .055), ((x, 0, .17), .05, .05)], STEEL, 10)
    add("Struts", arms)
    eye = Part()
    block(eye, .2, .215, .1, .15, .055, .012, GLOW)
    glow_add("Eye", eye)
    for name, x in (("RotorL", -.42), ("RotorR", .42)):
        rotor = Part()
        vblock(rotor, .175, .19, .4, .06, .02, ACCENT, ACCENT, ACCENT_DARK, cx=x)
        for cy in (-.115, .115):
            vblock(rotor, .175, .19, .06, .17, .02, ACCENT, ACCENT, ACCENT_DARK, cx=x, cy=cy)
        column(rotor, [(.188, .035), (.21, .028)], STEEL, 8, top=GRAPHITE)
        rotor.vertices[-16:] = [(vx + x, vy, vz) for vx, vy, vz in rotor.vertices[-16:]]
        add(name, rotor, pivot=(x, 0, .18))


# ---------------- Пила: стальной диск с зубьями, оранжевая середина ----------------
def saw(add, glow_add):
    teeth = 20
    outline = []
    for i in range(teeth * 2):
        a = i * math.pi / teeth + (.06 if i % 2 == 0 else 0)
        r = .5 if i % 2 == 0 else .42
        outline.append((r * math.cos(a), r * math.sin(a)))
    blade = Part()
    blade.rings([[(x, y, 0) for x, y in outline], [(x, y, .03) for x, y in outline]], STEEL)
    blade.colors[0] = LIGHT
    blade.colors[-1] = LIGHT
    add("Blade", blade)
    plate = Part()
    column(plate, [(-.004, .3), (.034, .3)], MAIN, 16, top=MAIN)
    add("Plate", plate)
    marks = Part()
    for k in range(4):
        vblock(marks, .03, .042, .16, .05, .012, MAIN_DARK, MAIN_DARK, MAIN_DARK, cy=.2, angle=k * math.pi / 2)
    add("Marks", marks)
    hub = Part()
    column(hub, [(-.012, .1), (.05, .1)], ACCENT, 12, top=ACCENT_DARK)
    add("Hub", hub)
    core = Part()
    column(core, [(.045, .045), (.065, .035)], GLOW, 10)
    glow_add("HubGlow", core)


# ---------------- Малый сундук: дерево, золото, фиолетовый замок; крышка отдельно ----------------
def chest(add, glow_add):
    body = Part()
    vblock(body, 0, .32, .8, .52, .03, MAIN, MAIN_DARK, DARK)
    add("Box", body)
    profile = [(-.27, .32)] + [(-.27 * math.cos(a), .32 + .15 * math.sin(a)) for a in (math.pi * k / 8 for k in range(1, 8))] + [(.27, .32)]
    lid = Part()
    lid.rings([[(x, y, z) for y, z in profile] for x in (-.4, -.385, .385, .4)],
              lambda row, face: ACCENT_DARK if row != 1 else MAIN)
    lid.colors[0] = ACCENT
    lid.colors[-1] = ACCENT
    for x in (-.29, .23):
        lid.rings([[(xx, y * 1.04, .32 + (z - .32) * 1.08) for y, z in profile] for xx in (x, x + .06)], ACCENT)
    add("Lid", lid, pivot=(0, .27, .32))
    bands = Part()
    for x in (-.26, .26):
        vblock(bands, 0, .325, .06, .54, .01, ACCENT, ACCENT, ACCENT_DARK, cx=x)
    for x in (-.37, .37):
        for y in (-.24, .24):
            vblock(bands, 0, .08, .08, .06, .015, ACCENT_DARK, ACCENT, ACCENT_DARK, cx=x, cy=y)
    add("Bands", bands)
    lock = Part()
    block(lock, -.29, -.262, .3, .13, .15, .025, shell_color(ACCENT_DARK, ACCENT, ACCENT, ACCENT_DARK))
    add("Lock", lock)
    gem = Part()
    block(gem, -.302, -.288, .31, .055, .055, .012, GLOW)
    glow_add("LockGlow", gem)


# ---------- 11.11.2026, вторая партия: сундук события, сброс припасов, короны элит, снаряды врагов ----------
def orb_rings(z0, r, rows=6, sides=10, cx=0.0, cy=0.0, wobble=None):
    """Кольца шара (полюса — крошечные кольца), wobble(row, i) — множитель радиуса для «комковатых» форм."""
    rings = []
    for k in range(rows + 1):
        phi = math.pi * k / rows
        rr = max(.004, r * math.sin(phi))
        z = z0 + r - r * math.cos(phi)
        ring = hring(z, rr, None, sides, .5 + .5 * (k % 2), cx, cy)
        if wobble is not None and 0 < k < rows:
            ring = [(cx + (x - cx) * wobble(k, i), cy + (y - cy) * wobble(k, i), zz) for i, (x, y, zz) in enumerate(ring)]
        rings.append(ring)
    return rings


def guard_chest(add, glow_add):
    """Большой сундук события «Защити сундук» (2,3 × 1,55 м); крышка Lid с осью на заднем верхнем ребре."""
    plinth = Part()
    vblock(plinth, 0, .1, 2.3, 1.55, .08, GRAPHITE, DARK, DARK)
    add("Plinth", plinth)
    box = Part()
    vblock(box, .1, 1.0, 2.1, 1.35, .06, MAIN, MAIN_DARK, DARK)
    add("Box", box)
    bands = Part()
    for x in (-.85, .85):
        vblock(bands, .1, 1.01, .18, 1.42, .02, ACCENT, ACCENT, ACCENT_DARK, cx=x)
    for x0, x1 in ((-1.075, -.94), (-.76, .76), (.94, 1.075)):   # пояс между вертикальными полосами, без наложения
        vblock(bands, .45, .58, x1 - x0, 1.39, .03, ACCENT, ACCENT, ACCENT_DARK, cx=(x0 + x1) / 2)
    for x in (-1.02, 1.02):
        for y in (-.64, .64):
            vblock(bands, .1, .32, .16, .16, .03, ACCENT_DARK, ACCENT, ACCENT_DARK, cx=x, cy=y)
    add("Bands", bands)
    half = .675
    profile = [(-half, 1.0)] + [(-half * math.cos(a), 1.0 + .42 * math.sin(a)) for a in (math.pi * k / 10 for k in range(1, 10))] + [(half, 1.0)]
    lid = Part()
    lid.rings([[(x, y, z) for y, z in profile] for x in (-1.05, -1.02, 1.02, 1.05)], lambda row, face: ACCENT_DARK if row != 1 else MAIN)
    lid.colors[0] = ACCENT
    lid.colors[-1] = ACCENT
    for x in (-.94, .76):
        lid.rings([[(xx, y * 1.03, 1.0 + (z - 1.0) * 1.07) for y, z in profile] for xx in (x, x + .18)], ACCENT)
    block(lid, -.72, -.66, 1.05, .34, .4, .05, shell_color(ACCENT_DARK, ACCENT, ACCENT, ACCENT_DARK))   # замок на передней стенке
    add("Lid", lid, pivot=(0, half, 1.0))
    gem = Part()
    block(gem, -.745, -.715, 1.05, .14, .14, .03, GLOW)
    glow_add("LockGlow", gem)
    runes = Part()
    for x in (-.45, 0, .45):
        block(runes, -.69, -.675, .3, .14, .1, .02, GLOW, x=x)
    glow_add("Runes", runes)


def supply_crate(add, glow_add):
    """Ящик сброса припасов: оливковый, жёлто-чёрные полосы, стальные уголки, маяк Beacon сверху."""
    crate = Part()
    vblock(crate, 0, .8, 1.15, 1.15, .06, MAIN, MAIN_DARK, DARK)
    add("Crate", crate)
    stripes = Part()
    stripe = lambda row, face: (ACCENT if face % 2 == 0 else ACCENT_DARK) if row == 1 else ACCENT_DARK
    stripes.rings([hbox(.5, 1.17, 1.17, .06), hbox(.51, 1.18, 1.18, .06), hbox(.62, 1.18, 1.18, .06), hbox(.63, 1.17, 1.17, .06)], stripe)
    add("Stripes", stripes)
    frame = Part()
    for x in (-.55, .55):
        for y in (-.55, .55):
            vblock(frame, 0, .82, .1, .1, .02, STEEL, LIGHT, GRAPHITE, cx=x, cy=y)
    vblock(frame, .78, .84, 1.2, .12, .02, STEEL, LIGHT, GRAPHITE)
    vblock(frame, .78, .84, .12, 1.2, .02, STEEL, LIGHT, GRAPHITE)
    add("Frame", frame)
    lamp = Part()
    column(lamp, [(.84, .13), (.9, .12)], GRAPHITE, 10, top=DARK)
    add("BeaconBase", lamp)
    beacon = Part()
    column(beacon, [(.9, .09), (1.02, .08), (1.08, .04)], GLOW, 10)
    glow_add("Beacon", beacon)


def crown(add, glow_add):
    """Корона элиты: кольцо с пятью зубцами и камнями (над головой врага, видна сверху)."""
    # Обруч из 15 сегментов (кольцо, а не сплошной диск).
    ring = Part()
    for k in range(15):
        vblock(ring, 0, .13, .095, .045, .012, MAIN, ACCENT, MAIN_DARK, cy=.2, angle=k * math.tau / 15 + math.pi / 2)
    add("Band", ring)
    spikes = Part()
    for k in range(5):
        a = k * math.tau / 5
        x, y = .19 * math.cos(a), .19 * math.sin(a)
        spikes.rings([hring(.13, .06, None, 4, 0, x, y), hring(.24, .035, None, 4, 0, x, y), hring(.32, .006, None, 4, 0, x, y)],
                     lambda row, face: MAIN if face % 2 else ACCENT)
    add("Spikes", spikes)
    gems = Part()
    for k in range(5):
        a = (k + .5) * math.tau / 5
        gems.rings(orb_rings(.035, .03, 3, 6, .228 * math.cos(a), .228 * math.sin(a)), GLOW)
    glow_add("Gems", gems)


def along_y(y, r, sides=10, z=0.0, rz=None):
    return circle(y, z, max(.004, r), max(.004, r if rz is None else rz), sides)


def acid_shot(add, glow_add):
    """Кислотный сгусток: капля вдоль +Y (голова вперёд), сзади две капельки."""
    blob = Part()
    profile = [(-.26, .004), (-.18, .06), (-.06, .13), (.06, .15), (.16, .12), (.23, .07), (.26, .004)]
    blob.rings([along_y(y, r, 10) for y, r in profile], lambda row, face: MAIN if (face + row) % 2 else ACCENT)
    glow_add("Blob", blob)
    drops = Part()
    for y, z, r in ((-.33, .03, .045), (-.42, -.02, .03)):
        drops.rings([[(px, py, pz + z) for px, py, pz in along_y(y + dy, rr, 8)] for dy, rr in ((-r, .004), (0, r), (r, .004))], ACCENT)
    glow_add("Drops", drops)
    core = Part()
    core.rings([along_y(y, r, 8) for y, r in ((-.02, .004), (.06, .07), (.14, .004))], MAIN_DARK)
    add("Core", core)


def bone_quill(add, glow_add):
    """Костяная игла: шестигранный конус остриём вперёд (+Y), сзади тёмное оперение."""
    shaft = Part()
    shaft.rings([along_y(-.4, .045, 6), along_y(-.2, .055, 6), along_y(.2, .035, 6), along_y(.45, .004, 6)],
                lambda row, face: MAIN if face % 2 else MAIN_DARK)
    add("Shaft", shaft)
    rings = Part()
    for y in (-.12, .05):
        rings.rings([along_y(y, .056, 6), along_y(y + .03, .056, 6)], ACCENT_DARK)
    add("Rings", rings)
    fletch = Part()
    for angle in (0, math.pi / 2):
        c, s = math.cos(angle), math.sin(angle)
        fin = [(-.13 * c, -.46, -.13 * s), (.13 * c, -.46, .13 * s), (.03 * c, -.3, .03 * s), (-.03 * c, -.3, -.03 * s)]
        fletch.rings([[(x + .01 * s, y, z - .01 * c) for x, y, z in fin], [(x - .01 * s, y, z + .01 * c) for x, y, z in fin]], ACCENT)
    add("Fletching", fletch)


def hostile_orb(add, glow_add):
    """Заряд стрелка: светящееся ядро в тёмной оболочке из шести шипов."""
    core = Part()
    core.rings(orb_rings(-.15, .15, 6, 10), lambda row, face: GLOW if (row + face) % 2 else HOT)
    glow_add("Core", core)
    shell = Part()
    for axis in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
        ax = Vector(axis)
        side = Vector((0, 0, 1)) if abs(ax.z) < .5 else Vector((1, 0, 0))
        u = ax.cross(side).normalized()
        v = ax.cross(u).normalized()
        base = [tuple(ax * .12 + (u * math.cos(k * math.tau / 4) + v * math.sin(k * math.tau / 4)) * .07) for k in range(4)]
        mid = [tuple(ax * .2 + (u * math.cos(k * math.tau / 4) + v * math.sin(k * math.tau / 4)) * .035) for k in range(4)]
        tip = [tuple(ax * .27 + (u * math.cos(k * math.tau / 4) + v * math.sin(k * math.tau / 4)) * .004) for k in range(4)]
        shell.rings([base, mid, tip], lambda row, face: MAIN_DARK if row == 0 else MAIN)
    add("Spikes", shell)


def mortar_spore(add, glow_add):
    """Спора миномётчика: комковатый оранжевый шар с жёлто-зелёными светящимися пятнами."""
    lumps = lambda k, i: 1 + .12 * math.sin(i * 2.1 + k * 1.7)
    pod = Part()
    pod.rings(orb_rings(-.24, .24, 7, 12, wobble=lumps), lambda row, face: MAIN if (row + face) % 3 else MAIN_DARK)
    add("Pod", pod)
    spots = Part()
    for x, y, z, r in ((.2, .06, .05, .06), (-.1, .19, -.06, .05), (-.05, -.16, .14, .045), (.06, -.12, -.17, .04)):
        spots.rings(orb_rings(z - r, r, 3, 6, x, y), GLOW)
    glow_add("Spots", spots)


# ---------- 12.11.2026: финальные боссы (случайный выбор в конце забега) ----------
# Ноги Leg_FL…Leg_BR, тело Body, голова Head, жвалы Jaw, брюшко Brood*, штыри SignalFins — их двигает ModelWalkAnimator в игре.
def leg(add, name, hip, knee, foot, color, claw, width=.12):
    p = Part()
    p.tube([(hip, width, width), (knee, width * .85, width * .85), (foot, width * .3, width * .3)], color, 8, transport=True)
    p.tube([((foot[0], foot[1], foot[2] + .12), width * .35, width * .35), ((foot[0], foot[1], max(.004, foot[2] - .02)), .02, .02)], claw, 6)
    add(name, p, pivot=hip)


def swarm_queen(add, glow_add):
    """Королева роя: фиолетовый панцирь, светящееся брюшко с яйцами, жвалы, шесть ног (~3,4 м)."""
    body = Part()
    body.rings([along_y(y, r, 12, 1.05, r * .8) for y, r in ((-.55, .2), (-.35, .5), (0, .62), (.35, .55), (.6, .25))],
               lambda row, face: MAIN if face % 3 else MAIN_DARK)
    for y in (-.3, 0, .3):   # спинные шипы
        body.rings([hring(1.5, .1, None, 5, 0, 0, y), hring(1.78, .05, None, 5, 0, 0, y), hring(1.95, .006, None, 5, 0, 0, y)], ACCENT)
    body_obj = add("Body", body, pivot=(0, 0, 1.05))
    sac = Part()
    profile = ((-.45, .3), (-.7, .75), (-1.1, 1.0), (-1.5, .95), (-1.85, .7), (-2.05, .35), (-2.12, .02))
    sac.rings([[(x, y, z + .08 * (i % 2)) for x, y, z in along_y(y, r, 14, 1.15, r * .85)] for i, (y, r) in enumerate(profile)],
              lambda row, face: ACCENT_DARK if row % 2 else MAIN)
    sac_obj = add("Brood", sac, pivot=(0, -.6, 1.15))
    eggs = Part()
    for x, y, z, r in ((0, -1.1, 1.98, .16), (.86, -1.3, 1.55, .13), (-.86, -1.35, 1.55, .14), (.6, -1.8, 1.7, .11), (-.6, -1.75, 1.72, .1)):
        eggs.rings(orb_rings(z - r, r, 4, 8, x, y), GLOW)
    glow_add("BroodEggs", eggs, pivot=(0, -.6, 1.15), parent=sac_obj)
    head = Part()
    head.rings([along_y(y, r, 10, 1.05, r * .85) for y, r in ((.5, .2), (.65, .4), (.9, .42), (1.1, .32), (1.22, .1))],
               lambda row, face: MAIN_DARK if row == 3 and face in (2, 3, 7, 8) else MAIN)
    head_obj = add("Head", head, pivot=(0, .6, 1.05))
    eyes = Part()
    for x in (-.2, .2):
        eyes.rings(orb_rings(1.12, .08, 3, 6, x, 1.12), GLOW)
    glow_add("Eyes", eyes, pivot=(0, .6, 1.05), parent=head_obj)
    jaw = Part()
    for side in (-1, 1):
        jaw.tube([((side * .18, 1.15, .95), .06, .06), ((side * .32, 1.45, .9), .05, .05), ((side * .12, 1.7, .85), .012, .012)], ACCENT, 6, transport=True)
    add("Jaw", jaw, pivot=(0, 1.15, .95))
    for side, s in ((-1, "L"), (1, "R")):
        for tag, y, dy in (("F", .35, .45), ("M", 0, 0), ("B", -.35, -.45)):
            leg(add, "Leg_" + tag + s, (side * .45, y, 1.0), (side * 1.25, y + dy * .6, 1.6), (side * 1.75, y + dy, 0), MAIN_DARK, ACCENT_DARK, .11)


def storm_sniper(add, glow_add):
    """Грозовой снайпер: треногий робот с длинной рельсовой винтовкой, катушками и громоотводами (~3 м)."""
    body = Part()
    vblock(body, 1.3, 2.0, .95, 1.15, .18, MAIN, LIGHT, MAIN_DARK)
    vblock(body, 1.25, 1.42, 1.02, 1.2, .2, MAIN_DARK, MAIN_DARK, DARK)
    for x in (-.5, .5):
        block(body, -.3, .45, 1.7, .12, .28, .04, ACCENT_DARK, x=x * 1.02)
    rod(body, -.5, 2.0, 1.72, .075, STEEL, 12, x=.62, front=VOID)          # ствол винтовки
    block(body, -.6, .2, 1.72, .26, .3, .06, shell_color(GRAPHITE, DARK, DARK, DARK), x=.62)
    body_obj = add("Body", body, pivot=(0, 0, 1.65))
    coils = Part()
    for y in (.5, .8, 1.1, 1.4):
        coils.rings([circle(y, 1.72, .12, .12, 10, .62), circle(y + .08, 1.72, .12, .12, 10, .62)], GLOW)
    glow_add("Coils", coils, pivot=(0, 0, 1.65), parent=body_obj)
    head = Part()
    head.rings(orb_rings(1.95, .3, 5, 10, 0, .35), lambda row, face: MAIN if row < 3 else MAIN_DARK)
    head_obj = add("Head", head, pivot=(0, .35, 2.0))
    visor = Part()
    block(visor, .6, .68, 2.25, .36, .1, .02, GLOW)
    glow_add("Visor", visor, pivot=(0, .35, 2.0), parent=head_obj)
    fins = Part()
    for x in (-.28, .28):
        fins.tube([((x, -.45, 1.95), .05, .05), ((x * 1.2, -.6, 2.9), .03, .03)], STEEL, 6)
    fins_obj = add("SignalFins", fins, pivot=(0, -.45, 1.95))
    tips = Part()
    for x in (-.28, .28):
        tips.rings(orb_rings(2.86, .08, 3, 6, x * 1.2, -.6), GLOW)
    glow_add("FinTips", tips, pivot=(0, -.45, 1.95), parent=fins_obj)
    leg(add, "Leg_FL", (-.35, .3, 1.35), (-.95, .85, 1.05), (-1.15, 1.1, 0), GRAPHITE, ACCENT, .13)
    leg(add, "Leg_FR", (.35, .3, 1.35), (.95, .85, 1.05), (1.15, 1.1, 0), GRAPHITE, ACCENT, .13)
    leg(add, "Leg_B", (0, -.45, 1.35), (0, -1.05, 1.05), (0, -1.35, 0), GRAPHITE, ACCENT, .13)


ITEMS = {
    "Crystal": (crystal, palette("4FD8F0", "2A9CC0", "9FF0FF", "1F6F96", "8FF6FF", "E8FFFF", "5FA6CC", "3A4656")),
    "RepairKit": (repair_kit, palette("E8EEF2", "AEB8C2", "D8402E", "9A2A20", "6FF0A8", "E8FFF0", "F5C542", "5A6B80")),
    "Magnet": (magnet, palette("D8402E", "9A2A20", "5A6B80", "3A4656", "7FD4FF", "E8F6FF", "F5C542", "C8902E")),
    "Mine": (mine, palette("5A6B80", "3A4656", "F5C542", "1E2228", "FF4A2E", "FFD0B0", "E89A3A", "C8402E")),
    "Turret": (turret, palette("6E9A5A", "4A6E3E", "F5C542", "C8902E", "7CF0E0", "E0FFF8", "C8402E", "8E2A22")),
    "Drone": (drone, palette("3FC8B8", "2A8C80", "EEF1F4", "7E8896", "8FF6FF", "F0FFFF", "F5C542", "C8402E")),
    "SawBlade": (saw, palette("E89A3A", "B86E22", "C8402E", "8E2A22", "FFD27A", "FFF0C0", "F5C542", "5A6B80")),
    "SmallChest": (chest, palette("8E5A32", "5E3A20", "F5C542", "C8902E", "C77DFF", "F0E0FF", "6B4A9A", "3A2A6A")),
    "GuardChest": (guard_chest, palette("8E5A32", "5E3A20", "F5C542", "C8902E", "C77DFF", "F0E0FF", "6B4A9A", "3A2A6A")),
    "SupplyCrate": (supply_crate, palette("6E7A44", "4E5830", "F5C542", "1E2228", "FFD24A", "FFF4C0", "C8402E", "5A6B80")),
    "EliteCrown": (crown, palette("F5C542", "C8902E", "FFE38A", "8E6A20", "7FE8FF", "FFFFFF", "C8402E", "5A6B80")),
    "EliteCrownExplosive": (crown, palette("E8602E", "A8401E", "FFB070", "6E2A14", "FFC040", "FFF0C0", "F5C542", "3A2A2A")),
    "AcidShot": (acid_shot, palette("6FE84A", "2E7A1E", "B8FF7A", "1E4A14", "9FFF6A", "E8FFD0", "5A6B80", "3A4656")),
    "BoneQuill": (bone_quill, palette("F4E6BE", "C8B48A", "735090", "4A2E5E", "FFE07A", "FFF8E0", "5A6B80", "3A4656")),
    "HostileOrb": (hostile_orb, palette("C8502E", "6E2A18", "FF9A3C", "3A1A10", "FF8A2E", "FFE0A0", "F5C542", "3A4656")),
    "SwarmQueen": (swarm_queen, palette("6B3FA0", "4A2A70", "8FE84A", "4E8A2A", "B8FF6A", "F0FFD0", "E8C060", "2A1A3A")),
    "StormSniper": (storm_sniper, palette("4A6E9A", "2E4A6E", "F5C542", "C8902E", "8FE8FF", "F0FFFF", "C8402E", "1E2A3A")),
    "MortarSpore": (mortar_spore, palette("F08A4A", "B85A2A", "FFB880", "6E3418", "D8FF5A", "F4FFC0", "5A6B80", "3A4656")),
}


def build(name, maker, colors):
    kit.clear_scene()
    out = ROOT / "docs/art/Items" / name
    out.mkdir(parents=True, exist_ok=True)
    root = kit.make_root(name)
    material = make_atlas(colors, out, name)
    glow = material.copy()
    glow.name = name + " Glow"
    parts, markers = [], []

    def add(part_name, part, pivot=(0, 0, 0), parent=None, mat=None):
        obj = part.build(part_name, pivot, parent or root, mat or material)
        # Деталь-ребёнок другой детали (светящийся глаз на голове): положение — относительно оси родителя.
        if parent is not None:
            obj.location = Vector(pivot) - parent.location
        parts.append(obj)
        return obj

    def glow_add(part_name, part, pivot=(0, 0, 0), parent=None):
        return add(part_name, part, pivot=pivot, parent=parent, mat=glow)

    head_holder = {}

    def head_add(part_name, part, glow=False):
        # Голова турели вращается вокруг своей оси: её детали — дети «Head» с вершинами относительно оси.
        head = head_holder.get("Head")
        obj = part.build(part_name, tuple(HEAD_PIVOT), head or root, glow_material if glow else material)
        if head is not None:
            obj.location = Vector((0, 0, 0))
        else:
            head_holder["Head"] = obj
        parts.append(obj)
        return obj

    glow_material = glow
    if maker is turret:
        maker(add, glow_add, head_add)
    else:
        maker(add, glow_add)

    shade_uvs(parts)
    points = world_vertices(parts)
    minimum = Vector(min(v[i] for v in points) for i in range(3))
    maximum = Vector(max(v[i] for v in points) for i in range(3))
    bounds = [round(v, 4) for v in maximum - minimum]
    triangles = sum(len(face.vertices) - 2 for obj in parts for face in obj.data.polygons)
    assert triangles < 9000, (name, triangles)
    validation = validate(parts)
    extra = [obj for obj in bpy.data.objects if obj.type == 'EMPTY' and obj is not root]
    save_model(root, [*parts, *extra], out, name)
    verify_fbx(out / (name + ".fbx"), len(parts), triangles, bounds)

    size = max(bounds)
    scale = 1.25 / max(size, .05)
    center = (minimum + maximum) * .5
    root.scale = (scale,) * 3
    root.location = -Vector((center.x, center.y, minimum.z)) * scale
    render_model(out, bounds[2] * scale * .5, 1.9, 35)
    report = {"meshParts": len(parts), "triangles": triangles, "boundsMetersBlenderXYZ": bounds,
              "markers": [obj.name for obj in extra], "fbxRoundTrip": "PASS", **validation}
    (out / "VERIFICATION.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(name + " PASS", json.dumps(report, ensure_ascii=False), flush=True)


wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(ITEMS)
for item in wanted:
    maker, colors = ITEMS[item]
    build(item, maker, colors)
