"""Набор оружия героя в стиле огнемёта и ракетницы (make_flamethrower.py, make_rocket_launcher.py):
крупные скошенные формы, плоские цвета палитры, у каждого оружия свой цвет (как в экране выбора).
Запуск: blender -b -P tools/blender/make_hero_weapons.py [-- Pistol Rifle ...]   (без списка — все)
Результат: docs/art/Weapons/<Имя>/ (BLEND, FBX, палитра, три ракурса, VERIFICATION.json).
Ключи ModelSkinSet: Pistol→Weapon0, Rifle→Weapon1, Shotgun→Weapon2, HeavyRifle→Weapon3,
ArcBlaster→Kind_Arc, IceCannon→Kind_Ice, Railgun→Kind_Rail, SawLauncher→Kind_Boomerang."""

import math
from pathlib import Path
import sys

from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
import kit
from creature_mesh import Part
from weapon_kit import (ROOT, make_atlas, case, circle, finish, kenney_forward_sign, shell_color, palette, block, rod, fins, lower,
                        GRAPHITE, DARK, STEEL, LIGHT, WHITE, VOID, RUBBER, BRASS,
                        MAIN, MAIN_DARK, ACCENT, ACCENT_DARK, GLOW, HOT, EXTRA, EXTRA2)


def body_color():
    return shell_color(MAIN, MAIN_DARK, MAIN_DARK, DARK)


def stripes(part, y0, count, step, z, width, height, on, off):
    case(part, [(y0 + i * step, z, width, height, height * .3) for i in range(count)],
         lambda row, face: (on if row % 2 == 0 else off) if face in (3, 4, 5) else DARK)


# ---------------- Пистолет (Weapon0): стально-голубой, компактный ----------------
def pistol(add):
    p = Part()
    block(p, -.2, .09, .02, .085, .07, .018, shell_color(MAIN_DARK, DARK, DARK, DARK))
    add("Frame", p)
    s = Part()
    block(s, -.21, .14, .085, .09, .065, .022, shell_color(MAIN, LIGHT, MAIN, MAIN_DARK))
    add("Slide", s)
    ser = Part()
    case(ser, [(-.2 + i * .012, .085, .094, .069, .02) for i in range(7)], lambda row, face: DARK if row % 2 == 0 else MAIN)
    add("Serrations", ser)
    stripe = Part()
    block(stripe, -.08, .07, .085, .093, .014, .003, ACCENT)
    add("Stripe", stripe)
    b = Part()
    rod(b, .13, .175, .085, .022, STEEL, front=VOID)
    add("Muzzle", b)
    sights = Part()
    block(sights, .11, .135, .125, .016, .016, .004, GRAPHITE)
    block(sights, -.2, -.17, .125, .05, .018, .005, GRAPHITE)
    add("Sights", sights)
    dots = Part()
    block(dots, .124, .136, .13, .008, .008, .002, GLOW)
    add("SightDot", dots)
    grip = lower(add, y=-.12, heel=ACCENT, trigger=ACCENT)
    return grip, Vector((0, .175, .085))


# ---------------- Автомат (Weapon1): бирюзовый ----------------
def rifle(add):
    r = Part()
    block(r, -.2, .16, .05, .09, .1, .024, body_color())
    add("Receiver", r)
    stripe = Part()
    block(stripe, -.15, .02, .05, .094, .012, .003, ACCENT)
    add("Stripe", stripe)
    rail = Part()
    block(rail, -.17, .13, .108, .05, .018, .005, GRAPHITE)
    add("TopRail", rail)
    sight = Part()
    block(sight, -.05, .04, .14, .05, .05, .012, shell_color(DARK, GRAPHITE, GRAPHITE, DARK))
    sight.colors[-1] = GLOW
    add("RedDot", sight)
    hg = Part()
    block(hg, .16, .36, .055, .08, .075, .02, shell_color(GRAPHITE, DARK, DARK, DARK))
    add("Handguard", hg)
    vents = Part()
    for y in (.19, .24, .29):
        block(vents, y, y + .03, .055, .084, .022, .004, MAIN)
    add("Vents", vents)
    b = Part()
    rod(b, .36, .45, .06, .016, STEEL)
    rod(b, .43, .48, .06, .024, DARK, front=VOID)
    add("Barrel", b)
    mag = Part()
    mag.tube([((0, .07, .0), .03, .055), ((0, .085, -.07), .03, .055), ((0, .115, -.14), .03, .052)], GRAPHITE, 8, transport=True)
    add("Magazine", mag)
    base = Part()
    block(base, .085, .15, -.155, .07, .022, .006, ACCENT, 0.0)
    add("MagBase", base)
    st = Part()
    case(st, [(-.37, .02, .05, .1, .014), (-.36, .02, .06, .11, .018), (-.22, .045, .06, .08, .018), (-.2, .045, .05, .07, .014)],
         shell_color(MAIN_DARK, MAIN_DARK, MAIN_DARK, DARK))
    add("Stock", st)
    pad = Part()
    block(pad, -.39, -.365, .02, .066, .115, .018, RUBBER)
    add("ButtPad", pad)
    grip = lower(add, y=-.12)
    return grip, Vector((0, .48, .06))


# ---------------- Дробовик (Weapon2): оранжевый с деревом ----------------
def shotgun(add):
    r = Part()
    block(r, -.18, .08, .05, .095, .1, .026, body_color())
    add("Receiver", r)
    b = Part()
    rod(b, .06, .5, .075, .03, STEEL, 12)
    rod(b, .46, .505, .075, .036, DARK, 12, front=VOID)
    add("Barrel", b)
    tube = Part()
    rod(tube, .06, .44, .022, .022, GRAPHITE, 10, front=STEEL)
    add("TubeMag", tube)
    pump = Part()
    case(pump, [(.2 + i * .02, .03, .076, .064, .02) for i in range(8)], lambda row, face: ACCENT if row % 2 == 0 else ACCENT_DARK)
    add("Pump", pump)
    st = Part()
    case(st, [(-.41, -.02, .058, .12, .018), (-.4, -.02, .07, .13, .022), (-.19, .04, .07, .085, .022), (-.17, .045, .06, .075, .018)],
         shell_color(ACCENT, ACCENT, ACCENT_DARK, ACCENT_DARK))
    add("Stock", st)
    pad = Part()
    block(pad, -.43, -.405, -.02, .074, .135, .02, RUBBER)
    add("ButtPad", pad)
    bead = Part()
    block(bead, .48, .495, .11, .012, .012, .003, HOT)
    add("Bead", bead)
    shells = Part()
    for y in (-.13, -.1, -.07):
        shells.tube([((.052, y, .02), .011, .011), ((.052, y, .072), .011, .011)], EXTRA, 8)
        shells.colors[-1] = BRASS
    add("Shells", shells)
    grip = lower(add, y=-.11, heel=MAIN)
    return grip, Vector((0, .505, .075))


# ---------------- Тяжёлая винтовка (Weapon3): фиолетовая, с оптикой ----------------
def heavy_rifle(add):
    r = Part()
    block(r, -.2, .14, .05, .09, .105, .026, body_color())
    add("Receiver", r)
    stripe = Part()
    block(stripe, -.16, .0, .05, .094, .012, .003, ACCENT)
    add("Stripe", stripe)
    shroud = Part()
    fins(shroud, .14, .34, .062, .036, .03, 6, GRAPHITE, DARK, 10)
    add("Shroud", shroud)
    b = Part()
    rod(b, .34, .6, .062, .018, STEEL)
    add("Barrel", b)
    brake = Part()
    block(brake, .58, .66, .062, .06, .045, .012, shell_color(DARK, GRAPHITE, GRAPHITE, DARK))
    brake.colors[-1] = VOID
    for y in (.6, .625):
        block(brake, y, y + .014, .062, .064, .022, .004, VOID)
    add("MuzzleBrake", brake)
    scope = Part()
    rod(scope, -.11, .11, .16, .026, GRAPHITE, 12)
    rod(scope, -.15, -.105, .16, .034, DARK, 12)
    rod(scope, .105, .15, .16, .036, DARK, 12, front=EXTRA)
    add("Scope", scope)
    rear_lens = Part()
    rod(rear_lens, -.155, -.15, .16, .026, EXTRA, 12)
    add("ScopeLens", rear_lens)
    mounts = Part()
    for y in (-.06, .04):
        block(mounts, y, y + .022, .12, .03, .045, .006, STEEL)
    add("ScopeMounts", mounts)
    mag = Part()
    block(mag, .02, .1, -.035, .06, .12, .015, shell_color(GRAPHITE, GRAPHITE, GRAPHITE, ACCENT))
    add("Magazine", mag)
    st = Part()
    case(st, [(-.45, .0, .05, .12, .016), (-.44, .0, .064, .13, .02), (-.3, .03, .064, .1, .02), (-.21, .05, .06, .08, .018), (-.2, .05, .05, .07, .014)],
         shell_color(MAIN_DARK, MAIN, MAIN_DARK, DARK))
    add("Stock", st)
    pad = Part()
    block(pad, -.47, -.445, .0, .07, .135, .02, RUBBER)
    add("ButtPad", pad)
    bipod = Part()
    for x in (-.022, .022):
        bipod.tube([((x, .3, .02), .007, .007), ((x, .5, .02), .007, .007)], STEEL, 6)
    add("Bipod", bipod)
    grip = lower(add, y=-.12, heel=MAIN)
    return grip, Vector((0, .66, .062))


# ---------------- Электродуга (Kind_Arc): светло-голубая, медные катушки ----------------
def arc_blaster(add):
    r = Part()
    block(r, -.18, .06, .05, .095, .1, .026, body_color())
    add("Body", r)
    core = Part()
    rod(core, .02, .3, .065, .024, GLOW, 12)
    add("Core", core)
    coils = Part()
    fins(coils, .06, .26, .065, .048, .03, 7, ACCENT, ACCENT_DARK, 12)
    add("Coils", coils)
    caps = Part()
    for x in (-.024, .024):
        caps.rings([circle(-.15, .122, .012, .012, 8, x), circle(-.14, .122, .015, .015, 8, x), circle(.0, .122, .015, .015, 8, x),
                    circle(.01, .122, .012, .012, 8, x)], lambda row, face: (STEEL, GLOW, STEEL)[row])
    add("Capacitors", caps)
    plate = Part()
    rod(plate, .28, .305, .065, .052, MAIN_DARK, 12)
    add("EmitterPlate", plate)
    prongs = Part()
    tips = Part()
    for a in (90, 210, 330):
        c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
        prongs.tube([((c * .038, .3, .065 + s * .038), .009, .009), ((c * .042, .35, .065 + s * .042), .009, .009),
                     ((c * .026, .39, .065 + s * .026), .008, .008)], ACCENT, 6, transport=True)
        tips.tube([((c * .026, .388, .065 + s * .026), .008, .008), ((c * .022, .4, .065 + s * .022), .005, .005)], GLOW, 6)
    add("Prongs", prongs)
    add("ProngTips", tips)
    ball = Part()
    ball.rings([circle(.335, .065, .006, .006, 10), circle(.345, .065, .016, .016, 10), circle(.36, .065, .02, .02, 10),
                circle(.375, .065, .016, .016, 10), circle(.385, .065, .006, .006, 10)], lambda row, face: (GLOW, HOT, HOT, GLOW)[row])
    add("Spark", ball)
    stripe = Part()
    stripes(stripe, -.15, 7, .022, .102, .05, .022, EXTRA, GRAPHITE)
    add("HazardRail", stripe)
    grip = lower(add, y=-.11, heel=ACCENT)
    return grip, Vector((0, .385, .065))


# ---------------- Ледяная пушка (Kind_Ice): голубая, иней, кристалл ----------------
def ice_cannon(add):
    r = Part()
    block(r, -.18, .1, .05, .095, .1, .026, shell_color(MAIN, ACCENT, MAIN_DARK, DARK))
    add("Body", r)
    tank = Part()
    rows = []
    for y, rr in ((-.16, .012), (-.15, .03), (-.135, .04), (-.12, .04), (-.115, .036), (-.03, .036), (-.025, .04), (.0, .04),
                  (.015, .03), (.025, .012)):
        rows.append(circle(y, .15, rr, rr, 12))
    tank.rings(rows, lambda row, face: GLOW if row == 4 else STEEL if row in (0, 1, 7, 8) else EXTRA)
    add("CryoTank", tank)
    clamps = Part()
    for y in (-.1, -.05):
        block(clamps, y, y + .014, .115, .03, .04, .006, GRAPHITE)
    add("TankClamps", clamps)
    b = Part()
    rod(b, .1, .36, .06, .026, EXTRA, 12)
    add("Barrel", b)
    cool = Part()
    fins(cool, .12, .3, .06, .044, .03, 6, ACCENT, ACCENT_DARK, 12)
    add("FrostFins", cool)
    crystal = Part()
    crystal.rings([circle(.35, .06, .03, .03, 6), circle(.39, .06, .046, .046, 6), circle(.43, .06, .036, .036, 6),
                   circle(.48, .06, .004, .004, 6)], lambda row, face: (GLOW, HOT if face % 2 else GLOW, HOT)[row])
    add("Crystal", crystal)
    shards = Part()
    for a in (30, 150, 270):
        c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
        shards.tube([((c * .03, .37, .06 + s * .03), .012, .012), ((c * .06, .42, .06 + s * .06), .004, .004)], GLOW, 6)
    add("Shards", shards)
    grip = lower(add, y=-.11, heel=MAIN)
    return grip, Vector((0, .48, .06))


# ---------------- Рельсовая пушка (Kind_Rail): графит, бирюзовый канал между рельсами ----------------
def railgun(add):
    r = Part()
    block(r, -.2, .1, .05, .095, .11, .028, body_color())
    add("Body", r)
    stripe = Part()
    block(stripe, -.16, .06, .05, .099, .012, .003, ACCENT)
    add("Stripe", stripe)
    rails = Part()
    block(rails, .06, .62, .098, .05, .024, .006, shell_color(STEEL, LIGHT, STEEL, GRAPHITE))
    block(rails, .06, .62, .022, .05, .024, .006, shell_color(STEEL, GRAPHITE, STEEL, LIGHT))
    add("Rails", rails)
    channel = Part()
    block(channel, .08, .6, .06, .02, .05, .004, GLOW)
    add("Channel", channel)
    rings = Part()
    for y in (.15, .3, .45):
        case(rings, [(y, .06, .07, .1, .02), (y + .006, .06, .08, .115, .024), (y + .03, .06, .08, .115, .024), (y + .036, .06, .07, .1, .02)],
             lambda row, face: ACCENT if face == 4 and row == 1 else MAIN_DARK)
    add("CapacitorRings", rings)
    tip = Part()
    block(tip, .6, .64, .06, .07, .1, .02, shell_color(DARK, GRAPHITE, GRAPHITE, DARK))
    tip.colors[-1] = VOID
    add("Tip", tip)
    battery = Part()
    block(battery, -.18, -.02, .13, .07, .05, .014, shell_color(DARK, GRAPHITE, GRAPHITE, DARK))
    add("Battery", battery)
    cells = Part()
    case(cells, [(-.16 + i * .02, .157, .04, .008, .002) for i in range(7)], lambda row, face: GLOW if row % 2 == 0 else DARK)
    add("BatteryCells", cells)
    st = Part()
    case(st, [(-.37, .03, .05, .09, .014), (-.36, .03, .06, .1, .018), (-.21, .05, .06, .08, .018), (-.2, .05, .05, .07, .014)],
         shell_color(MAIN_DARK, MAIN, MAIN_DARK, DARK))
    add("Stock", st)
    pad = Part()
    block(pad, -.39, -.365, .03, .066, .105, .018, RUBBER)
    add("ButtPad", pad)
    grip = lower(add, y=-.12, heel=ACCENT)
    return grip, Vector((0, .64, .06))


# ---------------- Пила-бумеранг (Kind_Boomerang): оранжевая, диск между лапами ----------------
def saw_launcher(add):
    r = Part()
    block(r, -.18, .12, .05, .095, .1, .026, body_color())
    add("Body", r)
    stripe = Part()
    stripes(stripe, -.15, 9, .022, .102, .05, .022, EXTRA, GRAPHITE)
    add("HazardRail", stripe)
    arms = Part()
    block(arms, .1, .33, .088, .05, .02, .006, shell_color(GRAPHITE, STEEL, STEEL, DARK))
    block(arms, .1, .33, .03, .05, .02, .006, shell_color(GRAPHITE, DARK, DARK, STEEL))
    add("Arms", arms)
    center = Vector((0, .32, .059))
    teeth = 16
    outline = []
    for i in range(teeth * 2):
        a = i * math.pi / teeth
        rr = .1 if i % 2 == 0 else .082
        a += .08 if i % 2 == 0 else 0
        outline.append((center.x + rr * math.cos(a), center.y + rr * math.sin(a)))
    blade = Part()
    blade.rings([[(x, y, center.z - .007) for x, y in outline], [(x, y, center.z + .007) for x, y in outline]], STEEL)
    blade.colors[0] = LIGHT
    blade.colors[-1] = LIGHT
    add("Blade", blade)
    hub = Part()
    hub.tube([((center.x, center.y, center.z - .03), .03, .03), ((center.x, center.y, center.z + .03), .03, .03)], ACCENT, 10)
    hub.colors[-1] = HOT
    add("Hub", hub)
    grip = lower(add, y=-.11, heel=ACCENT)
    return grip, Vector((0, .42, .059))


WEAPONS = {
    "Pistol": (pistol, palette("8FB8CC", "5D8296", "F5C542", "C8902E", "5FD8D0", "DFF8F4", "C8402E", "8E2A22")),
    "Rifle": (rifle, palette("2FB8A8", "1F7F78", "F5C542", "C8902E", "7CF0E0", "E0FFF8", "C8402E", "8E2A22")),
    "Shotgun": (shotgun, palette("F28A2E", "C56F1E", "8E5A32", "5E3A20", "FF9A3C", "FFE3A0", "C8402E", "F5C542")),
    "HeavyRifle": (heavy_rifle, palette("9A86D8", "6B5AA8", "F5C542", "C8902E", "B8A8FF", "F0E8FF", "5FD8D0", "C8402E")),
    "ArcBlaster": (arc_blaster, palette("A6C8FF", "6E8FD0", "C87A3A", "8E5226", "7FD4FF", "E8F6FF", "F5C542", "3A2A6A")),
    "IceCannon": (ice_cannon, palette("9FD8F0", "5FA6CC", "EEF8FF", "B8D8E8", "6FE0FF", "E8FCFF", "2E6FA8", "F5C542")),
    "Railgun": (railgun, palette("5A6B80", "3A4656", "8FF2FF", "3FB8C8", "9FF6FF", "F0FFFF", "F5C542", "C8402E")),
    "SawLauncher": (saw_launcher, palette("E89A3A", "B86E22", "C8402E", "8E2A22", "FFD27A", "FFF0C0", "F5C542", "5A6B80")),
}

FORWARD = kenney_forward_sign()
wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(WEAPONS)
for name in wanted:
    maker, colors = WEAPONS[name]
    kit.clear_scene()
    out = ROOT / "docs/art/Weapons" / name
    out.mkdir(parents=True, exist_ok=True)
    root = kit.make_root(name)
    material = make_atlas(colors, out, name)
    parts = []
    grip, muzzle = maker(lambda part_name, part: parts.append(part.build(part_name, (0, 0, 0), root, material)))
    length = max(muzzle.y + .05, .3) - (-.4)
    finish(root, parts, out, name, FORWARD, grip, muzzle, (.25, 1.25), preview_scale=min(3.2, 1.55 / length), frame=1.9)
