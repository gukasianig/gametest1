"""Build Acid Quarry: arena geometry, prefab layout data and Blender renders."""

import json
import math
import random
import sys
from pathlib import Path

import bpy

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import make_cargo_terminal as kit


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/Arena/AcidQuarry"
PREVIEW = ROOT / "docs/art/Arena/AcidQuarry"
HALF = 110
SOUTH, NORTH = 13.0, 25.0
BRIDGES = [(-57.0, 12.0), (0.0, 11.0), (56.0, 13.0)]
STATION = (11.0, 4.2, 11.0, 8.0)
SPILL = (-9.5, 5.2, 3.35)
RNG = random.Random(631207)
COLORS = dict(
    basalt="343842", shale="454552", slate="575461", silt="61595f", iron="745c58", dust="6d6769",
    clay="8d6f64", scree="5d575a", gravel="635e64", track="777478", concrete="898481",
    joint="5a535a", ridge="a08072", dirt="766663", shadow="232936", steel="344858", steel_top="526676",
    roof="677782", light="91cbd1", window="1c303e", mustard="a87839", warning="e6ab58", stripe="c2b6a0",
    tank="657b80", copper="a76d47", pipe="75838d", rubber="32353f", dark_acid="405321", pool_edge="6a892d",
    acid="91c947", channel="a7e35b", film="69852d", foam="d7f790", spark="edffc7",
)

Surface = kit.Surface


def trench_segments():
    edge = -HALF
    for centre, width in BRIDGES:
        left = centre - width/2
        yield edge, left
        edge = centre + width/2
    yield edge, HALF


def noise(x, y):
    return (math.sin(x*0.12 + math.sin(y*0.11)) + math.cos(y*0.14 - x*0.06))*0.5


def deck_patch(mesh, xmin, xmax, ymin, ymax):
    nx, ny = math.ceil((xmax-xmin)/6.4), math.ceil((ymax-ymin)/6.4)
    rows = []
    for j in range(ny+1):
        row = []
        for i in range(nx+1):
            x, y = xmin + (xmax-xmin)*i/nx, ymin + (ymax-ymin)*j/ny
            if i not in (0, nx):
                x += RNG.uniform(-1.55, 1.55)
            if j not in (0, ny):
                y += RNG.uniform(-1.55, 1.55)
            row.append((x, y, 0))
        rows.append(row)
    for j in range(ny):
        for i in range(nx):
            a, b, c, d = rows[j][i], rows[j][i+1], rows[j+1][i+1], rows[j+1][i]
            shade = noise(*a[:2]) + RNG.uniform(-0.24, 0.24)
            colors = ["scree", "gravel", "gravel", "dust", "dirt"]
            color = colors[max(0, min(4, int((shade+1.1)*2.3)))]
            if RNG.random() < 0.08:
                mesh.face([a, b, c], color)
                mesh.face([a, c, d], RNG.choice(("gravel", color, "dust")))
            else:
                mesh.face([a, b, c, d], color)


def terrain():
    deck = Surface("Quarry Floor")
    deck_patch(deck, -HALF, HALF, -HALF, SOUTH)
    deck_patch(deck, -HALF, HALF, NORTH, HALF)
    for left, right in [(-152, -HALF), (HALF, 152)]:
        deck_patch(deck, left, right, -147, SOUTH)
        deck_patch(deck, left, right, NORTH, 147)
    deck_patch(deck, -HALF, HALF, -147, -HALF)
    deck_patch(deck, -HALF, HALF, HALF, 147)

    for x in (-57, 0, 56):
        for low, high in [(-95, SOUTH), (NORTH, 105)]:
            deck.rect(x, (low+high)/2, 7.3, high-low, 0.009, "track")
            for side in (-1, 1):
                deck.rect(x+side*2.6, (low+high)/2, 0.27, high-low, 0.013, "dust")
            for y in range(math.ceil(low+3), int(high-1), 6):
                deck.rect(x, y, 0.13, 2.0, 0.016, "stripe")
    deck.rect(11, 3, 14.8, 12.7, 0.016, "concrete")
    for x in (4.0, 17.9):
        deck.rect(x, 3, 0.12, 12.3, 0.02, "warning")
    for x in range(6, 17, 2):
        deck.rect(x, -2.85, 0.65, 0.20, 0.021, "warning", -0.62)
    kit.label(deck, "PUMP ACCESS", 11, -1.85, 0.66, "stripe", 0.022)
    for i in range(720):
        x, y = RNG.uniform(-108, 108), RNG.uniform(-108, 108)
        if SOUTH-0.7 < y < NORTH+0.7 or min(abs(x-c) for c, _ in BRIDGES) < 4:
            continue
        if 2 < x < 20 and -5 < y < 11:
            continue
        deck.rect(x, y, RNG.uniform(0.17, 1.1), RNG.uniform(0.08, 0.55), 0.012,
                  RNG.choice(("joint", "dust", "dirt", "clay")), RNG.uniform(0, math.tau))
    return deck


def rail(mesh, left, right, y):
    mesh.box((left+right)/2, y, right-left, 0.23, 0.1, 0.26, "steel_top", "steel")
    mesh.box((left+right)/2, y, right-left, 0.20, 0.61, 0.78, "warning", "mustard")
    for i in range(math.ceil(left+0.45), int(right), 2):
        mesh.box(i, y, 0.19, 0.29, 0, 0.83, "stripe", "steel")
        mesh.rect(i+0.35, y, 0.34, 0.205, 0.782, "rubber")


def cliff(mesh, boundary, direction):
    n = 101
    rings = []
    for height, shift in [(0, 0), (-1.2, 0.65), (-3.25, 0.93), (-5.0, 1.35), (-7.3, 2.8)]:
        row = []
        for i in range(n):
            x = -155 + i*310/(n-1)
            jitter = 0 if shift == 0 else RNG.uniform(-0.46, 0.46)
            row.append((x, boundary+direction*(shift+jitter), height+0.45*jitter))
        rings.append(row)
    for i, (top, bottom) in enumerate(zip(rings, rings[1:])):
        tones = [("silt", "iron", "slate"), ("basalt", "slate", "shale"),
                 ("shale", "iron", "silt"), ("basalt", "shale", "slate")][i]
        for k in range(n-1):
            faces = [(top[k], bottom[k], bottom[k+1]), (top[k], bottom[k+1], top[k+1])]
            for face in faces:
                mesh.face(face if direction < 0 else list(reversed(face)), RNG.choice(tones))


def ravine():
    banks = Surface("Basalt Ravine and Bridges")
    cliff(banks, SOUTH, 1)
    cliff(banks, NORTH, -1)
    for left, right in trench_segments():
        rail(banks, left+0.09, right-0.09, SOUTH-0.08)
        rail(banks, left+0.09, right-0.09, NORTH+0.08)
        banks.rect((left+right)/2, SOUTH-0.37, right-left, 0.20, 0.025, "warning")
        banks.rect((left+right)/2, NORTH+0.37, right-left, 0.20, 0.025, "warning")
    for x, width in BRIDGES:
        banks.box(x, (NORTH+SOUTH)/2, width, NORTH-SOUTH, -0.64, 0, "steel_top", "steel")
        for edge in (-1, 1):
            x_edge = x+edge*(width/2+0.18)
            banks.box(x_edge, (NORTH+SOUTH)/2, 0.34, NORTH-SOUTH+0.36, -1.5, -0.02, "steel_top", "shadow")
            banks.box(x_edge, (NORTH+SOUTH)/2, 0.2, NORTH-SOUTH, 0.64, 0.86, "warning", "mustard")
            for y in (13.3, 16.4, 19.5, 22.6, 24.7):
                banks.box(x_edge, y, 0.25, 0.24, -0.22, 0.86, "steel_top", "steel")
        for i in range(1, 12):
            banks.rect(x, SOUTH+i, width-0.44, 0.055, 0.008, "shadow")
        for y in (SOUTH+0.36, NORTH-0.36):
            banks.rect(x, y, width-0.45, 0.18, 0.015, "warning")
    for side in (-1, 1):
        banks.box(0, side*110.25, 221, 1.5, 0, 0.79, "slate", "shale")
        for lo, hi in [(-110, SOUTH), (NORTH, 110)]:
            banks.box(side*110.25, (lo+hi)/2, 1.5, hi-lo, 0, 0.79, "slate", "shale")
        for x in range(-104, 105, 8):
            banks.rect(x, side*109.65, 2.1, 0.28, 0.792, "warning")
            if x < SOUTH-2 or x > NORTH+2:
                banks.rect(side*109.65, x, 0.28, 2.1, 0.792, "warning")
    return banks


def disc(mesh, x, y, radius, z, color, n=24):
    mesh.face([(x+math.cos(a)*radius, y+math.sin(a)*radius, z)
               for a in [math.tau*i/n for i in range(n)]], color)


def acid_surfaces():
    ground = Surface("Spill Rim")
    water = Surface("Acid")
    middle = (NORTH+SOUTH)/2
    water.rect(0, middle, 312, NORTH-SOUTH-3.1, -5.7, "film")
    last = None
    for x in range(-156, 157, 3):
        wave = math.sin(x*0.15)*0.52 + math.cos(x*0.31)*0.18
        borders = [-3.55, -2.15, -0.86, 0.50, 1.88, 3.55]
        row = [(x, middle+offset+wave*(1-abs(offset)/4), -5.684) for offset in borders]
        if last is not None:
            for i, color in enumerate(("pool_edge", "acid", "channel", "acid", "pool_edge")):
                water.face([last[i], row[i], row[i+1], last[i+1]], color)
        last = row
    for i in range(300):
        x, y = RNG.uniform(-154, 154), middle+RNG.uniform(-3.6, 3.6)
        water.rect(x, y, RNG.uniform(0.1, 1.75), RNG.uniform(0.035, 0.19), -5.66,
                   RNG.choice(("acid", "channel", "foam")), RNG.uniform(-0.25, 0.25))
    x, y, r = SPILL
    disc(ground, x, y, r+0.42, 0.017, "dark_acid", 40)
    disc(water, x, y, r, 0.023, "pool_edge", 40)
    disc(water, x, y, r-0.22, 0.027, "acid", 40)
    for i in range(26):
        a, distance = RNG.uniform(0, math.tau), RNG.uniform(0.2, r-0.32)
        disc(water, x+math.cos(a)*distance, y+math.sin(a)*distance, RNG.uniform(0.055, 0.21),
             0.035, RNG.choice(("channel", "foam", "pool_edge")), 7)
    for dx in (-2.25, 2.25):
        ground.rect(x+dx, y-r-0.31, 1.25, 0.14, 0.021, "warning", 0.5 if dx > 0 else -0.5)
    kit.label(ground, "ACID", x, y-r-0.83, 0.64, "warning", 0.022)
    return ground, water


def cylinder(mesh, centre, axis, length, radius, material, rim, segments=14):
    # Axis is vertical or aligned to the length of the pipe in the horizontal plane.
    direction = kit.Vector(axis).normalized()
    reference = kit.Vector((0, 1, 0)) if abs(direction.z) > 0.8 else kit.Vector((0, 0, 1))
    u = reference.cross(direction).normalized()
    v = direction.cross(u)
    centre = kit.Vector(centre)
    rings = []
    for offset in (-length/2, length/2):
        rings.append([tuple(centre+direction*offset + radius*(u*math.cos(a)+v*math.sin(a)))
                      for a in [math.tau*i/segments for i in range(segments)]])
    bottom, top = rings
    mesh.face(list(reversed(bottom)), rim)
    mesh.face(top, rim)
    for i in range(segments):
        j = (i+1) % segments
        mesh.face([bottom[i], bottom[j], top[j], top[i]], material)


def station():
    hall = Surface("Pump Station")
    x, y, w, d = STATION
    hall.box(x, y, w, d, 0, 0.38, "concrete", "steel_top")
    hall.box(x, y, w-0.45, d-0.45, 0.38, 3.4, "roof", "steel")
    hall.box(x, y, w-0.16, d-0.14, 3.4, 3.61, "steel_top", "shadow")
    hall.box(x, y+0.23, w-0.95, d-0.91, 3.61, 3.86, "roof", "steel_top")
    for yy in (y-d/2+0.34, y+d/2-0.34):
        hall.box(x, yy, w-0.46, 0.27, 3.61, 3.95, "mustard", "shadow")
    for xx in (x-w/2+0.3, x+w/2-0.3):
        hall.box(xx, y, 0.29, d-0.87, 3.61, 3.96, "mustard", "shadow")
    for dx in (-4.6, -2.85, 2.85, 4.6):
        hall.box(x+dx, y, 0.19, d-1.02, 3.86, 4.03, "steel_top", "steel")
    kit.label(hall, "PUMP 07", x, y-0.61, 1.12, "stripe", 3.875)
    for dx in (-3.8, 3.8):
        hall.box(x+dx, y-d/2+0.18, 1.63, 0.31, 1.39, 2.28, "steel_top", "window")
        hall.face([(x+dx-0.68, y-d/2+0.019, 1.62), (x+dx+0.68, y-d/2+0.019, 1.62),
                   (x+dx+0.68, y-d/2+0.019, 2.1), (x+dx-0.68, y-d/2+0.019, 2.1)], "light")
    hall.box(x, y-d/2+0.14, 2.9, 0.25, 0.3, 2.8, "steel_top", "shadow")
    for i in range(11):
        z = 0.49+i*0.192
        hall.box(x, y-d/2+0.03, 2.59, 0.05, z, z+0.079, "steel_top", "steel_top")
    hall.box(x, y-d/2+0.52, 4.8, 1.02, 2.76, 2.98, "mustard", "steel_top")
    for xx in (x+1.42, x+3.17):
        cylinder(hall, (xx, y+2.21, 4.6), (0, 0, 1), 1.5, 0.34, "pipe", "shadow")
        cylinder(hall, (xx, y+2.21, 5.28), (0, 0, 1), 0.21, 0.46, "copper", "steel_top")
    hall.box(x-3.5, y+2.2, 1.37, 1.45, 3.85, 4.44, "steel_top", "steel")
    for i in range(6):
        hall.rect(x-3.5, y+1.64+i*0.21, 1.15, 0.057, 4.445, "shadow")
    return hall


def rock(mesh, x, y, radius, height, z=0):
    n = RNG.choice((7, 8, 9))
    a0 = RNG.uniform(0, math.tau)
    angles = [a0+math.tau*i/n for i in range(n)]
    shape = [RNG.uniform(0.72, 1.07) for _ in angles]
    drift = RNG.uniform(-0.24, 0.24)*radius, RNG.uniform(-0.24, 0.24)*radius
    layers = []
    for taper, fraction in [(1.0, 0), (0.91, 0.26), (0.73, 0.45), (0.71, 0.58), (0.46, 0.91), (0.25, 1.0)]:
        layers.append([(x+math.cos(a)*radius*taper*k+drift[0]*fraction,
                        y+math.sin(a)*radius*taper*k+drift[1]*fraction,
                        z+height*fraction+height*RNG.uniform(-0.04, 0.04)*fraction)
                       for a, k in zip(angles, shape)])
    for layer, (low, high) in enumerate(zip(layers, layers[1:])):
        shades = [("basalt", "shale"), ("slate", "shale"), ("iron", "silt"),
                  ("basalt", "slate"), ("silt", "slate", "dust")][layer]
        for i in range(n):
            j = (i+1) % n
            mesh.face([low[i], low[j], high[j]], RNG.choice(shades))
            mesh.face([low[i], high[j], high[i]], RNG.choice(shades))
    mesh.face(layers[-1], "silt")


def scenery():
    mesh = Surface("Quarry Walls and Equipment")
    solids = []
    x, y, w, d = STATION
    solids.append((x, y, w, d, 3.5))
    for x, y in [(-17, 8.4), (24, 7), (-34, 31), (34, 35), (28, -19), (-29, -25), (15, 52), (-15, 53)]:
        # Reuse the same two tank modules on foundations sized to the colliders.
        mesh.box(x, y, 5.0, 3.1, 0, 0.19, "concrete", "shadow")
        for dx in (-1.15, 1.15):
            cylinder(mesh, (x+dx, y, 1.34), (0, 0, 1), 2.31, 0.94, "tank", "steel_top")
            cylinder(mesh, (x+dx, y, 2.23), (0, 0, 1), 0.16, 0.99, "copper", "steel_top")
            cylinder(mesh, (x+dx, y, 2.53), (0, 0, 1), 0.23, 0.58, "pipe", "steel_top")
        solids.append((x, y, 5.0, 3.1, 2.55))

    crates = kit.kit_mesh("BlasterKit/Models/crate-wide.fbx")
    small_rocks = kit.kit_mesh("SurvivalKit/Models/rock-c.fbx")
    for x, y in [(-10.5, -8), (14.8, -8.5), (-20, 31), (17, 32), (-46, 47), (48, 45),
                 (-41, -7.5), (47, -4), (-81, 68), (83, -55), (42, 84), (-33, -83)]:
        kit.place_kit(mesh, crates, x, y, 0, 3.25, 0, ("mustard", "steel"), height=1.2)
        solids.append((x, y, 3.25, 3.25, 1.25))

    for x, y, r, h in [(-23, -7.3, 4.2, 3.7), (26, -8, 4.8, 4.8), (-38, 8, 6.2, 5.1), (37, 5, 5.7, 5.7),
                        (-30, 40, 6.0, 6.0), (25, 43, 6.3, 6.4), (-86, -30, 7.1, 6.4), (82, 48, 8.0, 7.8),
                        (71, 75, 7.8, 5.7), (-81, 80, 9.0, 7.6)]:
        rock(mesh, x, y, r, h)
        solids.append((x, y, r*1.89, r*1.89, max(1.4, h*0.66)))

    for side in (-1, 1):
        for t in range(-105, 106, 15):
            rock(mesh, t+RNG.uniform(-3, 3), side*RNG.uniform(117.5, 124.5), RNG.uniform(6.5, 10.3), RNG.uniform(7, 19))
            if t < 2 or t > 35:
                rock(mesh, side*RNG.uniform(117.5, 125.5), t, RNG.uniform(6.7, 10.3), RNG.uniform(7, 17))
        for t in range(-120, 123, 25):
            rock(mesh, t, side*RNG.uniform(133, 142), RNG.uniform(10, 16), RNG.uniform(13, 28))
            if t < 0 or t > 36:
                rock(mesh, side*RNG.uniform(134, 143), t, RNG.uniform(10, 16), RNG.uniform(13, 26))
        for t in range(-102, 103, 17):
            kit.place_kit(mesh, small_rocks, t, side*RNG.uniform(110.5, 114.5), 0, RNG.uniform(1.3, 3.4),
                          RNG.uniform(0, math.tau), ("iron", "shale"))
            if t < 8 or t > 30:
                kit.place_kit(mesh, small_rocks, side*RNG.uniform(111.3, 115.7), t, 0, RNG.uniform(1.5, 3.3),
                              RNG.uniform(0, math.tau), ("silt", "basalt"))
    for x in (-104, 104):
        for y, z in [(SOUTH-0.8, 0.54), (NORTH+0.8, 0.54)]:
            for dx in (0, 2.2):
                cylinder(mesh, (x+dx, y, z), (0, 0, 1), 1.16, 0.54, "copper", "pipe")
        for dx in (0, 2.2):
            cylinder(mesh, (x+dx, (SOUTH+NORTH)/2, -2.1), (0, 1, 0), 14.1, 0.44, "pipe", "steel")
    return mesh, solids


def emit(base):
    acid = base.copy()
    acid.name = "AcidQuarryGlow"
    shader = acid.node_tree.nodes.get("Principled BSDF")
    texture = next(node for node in acid.node_tree.nodes if node.type == 'TEX_IMAGE')
    acid.node_tree.links.new(texture.outputs["Color"], shader.inputs["Emission Color"])
    shader.inputs["Emission Strength"].default_value = 0.46
    return acid


def write_layout(solids):
    for left, right in trench_segments():
        solids.append(((left+right)/2, (SOUTH+NORTH)/2, right-left, NORTH-SOUTH, 0.58))
    def vec(x, y, z):
        return dict(x=x, y=y, z=z)
    data = dict(blocks=[dict(center=vec(x, h/2, y), size=vec(w, h, d)) for x, y, w, d, h in solids],
                spillCenter=vec(SPILL[0], 0, SPILL[1]), spillRadius=SPILL[2])
    logs = ROOT / "Logs"
    logs.mkdir(exist_ok=True)
    (logs / "acid-quarry-layout.json").write_text(json.dumps(data), encoding="utf-8")


def main():
    ART.mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    kit.ART, kit.PREVIEW, kit.COLORS = ART, PREVIEW, COLORS
    kit.SWATCHES = {key: ((i % 8 + 0.5)/8, (i // 8 + 0.5)/8) for i, key in enumerate(COLORS)}
    material = kit.palette_material("AcidQuarry")
    props, solids = scenery()
    rim, acid = acid_surfaces()
    objects = [surface.object(material) for surface in (terrain(), ravine(), station(), rim, props)]
    objects.append(acid.object(emit(material)))
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
        obj.data.calc_loop_triangles()
    bpy.context.view_layer.objects.active = objects[0]
    exported = {obj.name: len(obj.data.loop_triangles) for obj in objects}
    assert sum(exported.values()) < 40000, exported
    bpy.ops.export_scene.fbx(filepath=str(ART / "AcidQuarry.fbx"), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', add_leaf_bones=False, use_triangles=True)
    write_layout(solids)

    scene = kit.preview_setup()
    scene.world.node_tree.nodes.get("Background").inputs["Strength"].default_value = 0.52
    sun = next(obj for obj in scene.objects if obj.type == 'LIGHT')
    sun.data.energy = 2.45
    sun.location = (-22, -36, 47)
    kit.aim(sun, (0, 0, 0))
    scene.camera.location = (26, -33, 29)
    kit.aim(scene.camera, (1, 10, 0))
    scene.render.resolution_x, scene.render.resolution_y = (1440, 860)
    scene.camera.data.angle_y = math.radians(48)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(PREVIEW / "AcidQuarry.blend"))
    kit.render(scene, "station", (24, -27, 26), (1, 9, -0.2), (1440, 860), ortho=48)
    kit.render(scene, "overview", (115, -161, 270), (0, 0, -2), (1500, 1080), ortho=391)
    kit.render(scene, "start", (0, -6, 18), (0, 0, 0), (1280, 720))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ART / "AcidQuarry.fbx"))
    imported = {}
    for obj in bpy.context.scene.objects:
        assert obj.type == 'MESH', obj.name
        obj.data.calc_loop_triangles()
        assert obj.data.uv_layers.active and len(obj.data.materials) == 1, obj.name
        imported[obj.name] = len(obj.data.loop_triangles)
    assert imported == exported, (exported, imported)
    print(f"ACID REIMPORT PASS: {len(imported)} meshes, {sum(imported.values())} triangles", flush=True)


if __name__ == "__main__":
    main()
