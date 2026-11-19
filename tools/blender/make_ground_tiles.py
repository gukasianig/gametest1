"""Create an 8 m modular ground library and preview the tiles and their joins."""

import math
import random
import sys
from pathlib import Path

import bpy

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import make_cargo_terminal as kit


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/Arena/GroundTiles"
PREVIEW = ROOT / "docs/art/Arena/GroundTiles"
HALF = 4
COLORS = dict(
    seam="52595c", concrete="888e8b", concrete_light="979b95", concrete_dark="777d7c", cement="626a6a",
    repair="77766b", oil="515553", scratch="a1a199", asphalt="4e565d", road_patch="596168",
    shoulder="737876", white="d4d0ba", ochre="dfaf59", shadow="303940", steel="546977", steel_light="687e86",
    steel_dark="425663", metal_scuff="7e9294", bolt="a4acaa", painted="668880", rail="a4b5b4", rubber="292f35",
    shale="5d5762", shale_light="6e6874", shale_dark="4f4d58", iron="836a64", earth="82746b",
    earth_dark="6c605c", earth_light="988678", slate="585e68", gravel="76777a", pale_grit="aaa3a0",
    sand="b19775", sand_light="c2a885", sand_dark="a18968", dune="bfa181", dry_mud="9a7860",
    clay="ab866b", clay_dark="815f4e", tire="8f785d", pale_sand="d4b68e", charcoal="454046",
    ash="615458", coal="59484b", soot="3c393e", cinder="78544d", hot="b65831", lava="e97e39",
    lava_core="ffc76a", poison="617243", poison_dark="495539", poison_light="9ab65a", moss="75834e",
    label="ecede6", background="242a34", board_line="6e7a8a",
)

FAMILIES = [
    ("CONCRETE", ("Panels", "Cracked", "Drains")),
    ("ASPHALT", ("Straight", "Corner", "Cross")),
    ("QUARRY", ("Slate", "Gravel", "Sediment")),
    ("SAND", ("Ripples", "Dry Clay", "Tracks")),
    ("METAL", ("Deck", "Grating", "Hazard")),
    ("SCORCHED", ("Ash", "Lava Veins", "Toxic Stain")),
]


def polygon_disc(mesh, x, y, radius, z, color, rng, count=7, stretch=1):
    points = []
    for i in range(count):
        a = math.tau*i/count
        r = radius*rng.uniform(0.83, 1.08)
        points.append((x + stretch*r*math.cos(a), y + r*math.sin(a), z))
    mesh.face(points, color)


def line(mesh, start, end, width, z, color):
    dx, dy = end[0] - start[0], end[1] - start[1]
    mesh.rect((start[0]+end[0])/2, (start[1]+end[1])/2, math.hypot(dx, dy), width, z, color,
              math.atan2(dy, dx))


def grain(mesh, rng, colors, count, size, z=0.009):
    for i in range(count):
        x, y = rng.uniform(-3.69, 3.69), rng.uniform(-3.69, 3.69)
        r = rng.uniform(size*0.28, size)
        polygon_disc(mesh, x, y, r, z, rng.choice(colors), rng, count=5, stretch=1.05)


def tessellate(mesh, rng, colors, steps=7, z=0):
    rows = []
    for j in range(steps+1):
        row = []
        for i in range(steps+1):
            x, y = -HALF+2*HALF*i/steps, -HALF+2*HALF*j/steps
            if i not in (0, steps):
                x += rng.uniform(-0.36, 0.36)
            if j not in (0, steps):
                y += rng.uniform(-0.36, 0.36)
            row.append((x, y, z))
        rows.append(row)
    for j in range(steps):
        for i in range(steps):
            a, b, c, d = rows[j][i], rows[j][i+1], rows[j+1][i+1], rows[j+1][i]
            color = rng.choice(colors)
            mesh.face([a, b, c], color)
            mesh.face([a, c, d], color if rng.random() < 0.57 else rng.choice(colors))


def concrete(mesh, rng, variant):
    mesh.rect(0, 0, 8, 8, 0, "seam")
    for x in (-2, 2):
        for y in (-2, 2):
            mesh.rect(x, y, 3.952, 3.952, 0.006, rng.choice(("concrete", "concrete_light", "concrete_dark")))
            mesh.rect(x+1.82, y-0.19, 0.06, 1.76, 0.009, "cement")
    grain(mesh, rng, ("concrete_dark", "scratch", "concrete"), 36, 0.11, z=0.013)
    if variant == 1:
        for points in [[(-3.5, 2.7), (-2.82, 1.22), (-1.71, 0.64), (-1.25, -0.43), (-0.28, -0.78), (0.98, -2.19)],
                       [(-1.71, 0.64), (-2.09, -0.23), (-2.88, -0.54)],
                       [(0.21, 3.54), (0.92, 2.03), (1.85, 1.73), (2.24, 0.78), (3.35, 0.38)]]:
            for a, b in zip(points, points[1:]):
                line(mesh, a, b, 0.045, 0.016, "cement")
        polygon_disc(mesh, 1.43, -1.64, 0.93, 0.015, "repair", rng, stretch=1.3)
        polygon_disc(mesh, 1.36, -1.66, 0.46, 0.017, "oil", rng, stretch=1.3)
    if variant == 2:
        for x, y in [(-1.9, -1.45), (1.9, 1.45)]:
            mesh.rect(x, y, 2.54, 1.34, 0.016, "cement")
            mesh.rect(x, y, 2.34, 1.12, 0.018, "rubber")
            for k in range(-6, 7):
                mesh.rect(x+k*0.166, y, 0.072, 0.99, 0.021, "steel_light")
            for side in (-1, 1):
                mesh.rect(x, y+side*0.78, 2.54, 0.10, 0.014, "ochre")


PORTS = (("S", "N"), ("S", "E"), ("S", "N", "W", "E"))


def asphalt(mesh, rng, variant):
    mesh.rect(0, 0, 8, 8, 0, "shoulder")
    mesh.rect(0, 0, 4.8, 4.8, 0.006, "asphalt")
    ports = PORTS[variant]
    for side, angle in [("S", 0), ("E", math.pi/2), ("N", math.pi), ("W", 3*math.pi/2)]:
        c, s = math.cos(angle), math.sin(angle)
        pos = lambda x, y: (x*c-y*s, x*s+y*c)
        if side in ports:
            mesh.rect(*pos(0, -3.2), 4.8, 1.6, 0.006, "asphalt", angle)
            mesh.rect(*pos(0, -3.14), 0.107, 0.95, 0.016, "white", angle)
            for x in (-2.28, 2.28):
                mesh.rect(*pos(x, -3.2), 0.112, 1.6, 0.015, "ochre", angle)
        else:
            mesh.rect(*pos(0, -2.28), 4.8, 0.112, 0.015, "ochre", angle)
    if variant == 0:
        for y in (-1.35, 0, 1.35):
            mesh.rect(0, y, 0.107, 0.76, 0.016, "white")
        for x in (-2.28, 2.28):
            mesh.rect(x, 0, 0.112, 4.8, 0.016, "ochre")
    if variant == 1:
        p = [(0, -2.39), (0, -1.26), (0.3, -0.69), (0.76, -0.26), (1.29, 0), (2.39, 0)]
        for i in (0, 2, 4):
            line(mesh, p[i], p[i+1], 0.103, 0.016, "white")
    if variant == 2:
        for i in (-1, 0, 1):
            mesh.rect(i*0.40, 0, 0.082, 0.66, 0.015, "road_patch", math.pi/4)
    for i in range(7):
        x, y = rng.uniform(-1.85, 1.85), rng.uniform(-1.85, 1.85)
        mesh.rect(x, y, rng.uniform(0.17, 0.72), rng.uniform(0.048, 0.24), 0.010, "road_patch",
                  rng.uniform(0, math.pi))


def quarry(mesh, rng, variant):
    colors = [("shale", "shale", "shale_light", "shale_dark", "slate"),
              ("gravel", "gravel", "earth", "earth_dark", "slate"),
              ("earth", "iron", "earth", "earth_light", "earth_dark")][variant]
    tessellate(mesh, rng, colors)
    if variant == 0:
        for i in range(13):
            x, y = rng.uniform(-3.23, 3.23), rng.uniform(-3.23, 3.23)
            polygon_disc(mesh, x, y, rng.uniform(0.24, 0.54), 0.012, "shale_light", rng, count=5, stretch=1.18)
        grain(mesh, rng, ("shale_dark", "iron", "gravel"), 26, 0.16)
    if variant == 1:
        grain(mesh, rng, ("slate", "pale_grit", "earth_dark", "shale_light"), 72, 0.23)
    if variant == 2:
        for j in range(4):
            points = [(i*0.64-3.52, -2.64+j*1.64 + 0.26*math.sin(i*0.72+j*0.62)) for i in range(12)]
            for a, b in zip(points, points[1:]):
                line(mesh, a, b, 0.088, 0.012, "clay_dark" if j % 2 else "earth_light")
        grain(mesh, rng, ("clay", "earth_dark"), 24, 0.12)


def sand(mesh, rng, variant):
    colors = [("sand", "sand", "sand_light", "dune", "sand_dark"),
              ("dry_mud", "clay", "dry_mud", "sand_dark", "clay_dark"),
              ("sand_dark", "sand", "sand", "dune", "sand_light")][variant]
    tessellate(mesh, rng, colors)
    if variant == 0:
        for j in range(12):
            y = -3.35+j*0.6
            points = [(x, y+0.15*math.sin(x*1.34+j*0.66)) for x in (-3.55, -2.55, -1.55, -0.55, 0.45, 1.45, 2.45, 3.55)]
            for a, b in zip(points, points[1:]):
                line(mesh, a, b, 0.054, 0.010, "pale_sand" if j % 3 == 1 else "sand_dark")
    if variant == 1:
        for i in range(10):
            x, y = rng.uniform(-3.18, 3.18), rng.uniform(-3.18, 3.18)
            polygon_disc(mesh, x, y, rng.uniform(0.22, 0.52), 0.01, "clay_dark", rng, count=5, stretch=1.15)
            polygon_disc(mesh, x, y, 0.21, 0.013, "clay", rng, count=5)
    if variant == 2:
        for x in (-1.4, 1.4):
            mesh.rect(x, 0, 0.76, 8, 0.008, "tire")
            for i in range(-8, 9):
                mesh.rect(x, i*0.44, 0.46, 0.085, 0.013, "clay_dark", -0.43)
    grain(mesh, rng, ("sand_light", "sand_dark", "clay"), 23, 0.10, z=0.016)


def metal(mesh, rng, variant):
    mesh.rect(0, 0, 8, 8, 0, "shadow")
    for x in (-2, 2):
        for y in (-2, 2):
            mesh.rect(x, y, 3.93, 3.93, 0.006, "steel" if (x*y > 0) else "steel_dark")
            mesh.rect(x, y, 3.48, 3.42, 0.009, "steel_light")
            for dx in (-1.79, 1.79):
                for dy in (-1.77, 1.77):
                    polygon_disc(mesh, x+dx, y+dy, 0.063, 0.014, "bolt", rng, count=6)
    if variant == 0:
        for x in (-2, 2):
            for y in (-2, 2):
                mesh.rect(x, y, 1.72, 2.75, 0.012, "painted")
                mesh.rect(x, y, 0.08, 2.52, 0.014, "metal_scuff")
    if variant == 1:
        mesh.rect(0, 0, 3.45, 7.46, 0.012, "shadow")
        for j in range(-15, 16):
            mesh.rect(0, j*0.231, 3.24, 0.074, 0.019, "rail")
        for x in (-1.76, 1.76):
            mesh.rect(x, 0, 0.11, 7.49, 0.019, "steel_dark")
    if variant == 2:
        mesh.rect(0, 0, 6.43, 5.9, 0.013, "steel")
        for y in (-2.67, 2.67):
            mesh.rect(0, y, 6.18, 0.55, 0.016, "rubber")
            for i in range(-6, 7):
                mesh.rect(i*0.45, y, 0.50, 0.21, 0.019, "ochre", math.pi/4)
        for x in (-2.9, 2.9):
            mesh.rect(x, 0, 0.07, 4.6, 0.018, "ochre")


def scorched(mesh, rng, variant):
    colors = [("charcoal", "charcoal", "ash", "coal", "soot"),
              ("charcoal", "soot", "coal", "charcoal", "cinder"),
              ("ash", "charcoal", "earth_dark", "poison_dark", "ash")][variant]
    tessellate(mesh, rng, colors)
    grain(mesh, rng, ("cinder", "ash", "coal"), 44, 0.14)
    if variant == 1:
        paths = [[(-3.49, 1.28), (-2.47, 0.86), (-1.76, -0.28), (-0.33, -0.02), (1.45, -1.40), (3.31, -1.94)],
                 [(-1.76, -0.28), (-1.19, -1.29), (-1.32, -2.71)],
                 [(-0.33, -0.02), (0.38, 1.15), (1.72, 1.56), (2.17, 2.74)]]
        for path in paths:
            for a, b in zip(path, path[1:]):
                line(mesh, a, b, 0.27, 0.012, "hot")
                line(mesh, a, b, 0.12, 0.016, "lava")
                line(mesh, a, b, 0.037, 0.018, "lava_core")
            for x, y in path[1:-1]:
                polygon_disc(mesh, x, y, 0.108, 0.014, "hot", rng, count=8)
    if variant == 2:
        for x, y, radius in [(-1.38, -0.52, 1.13), (1.44, 1.07, 0.83), (1.74, -1.94, 0.57)]:
            polygon_disc(mesh, x, y, radius, 0.012, "poison_dark", rng, count=12, stretch=1.22)
            polygon_disc(mesh, x, y, radius*0.77, 0.015, "poison", rng, count=12, stretch=1.22)
            polygon_disc(mesh, x, y, radius*0.44, 0.018, "moss", rng, count=10, stretch=1.08)
            polygon_disc(mesh, x+0.11, y-0.12, radius*0.13, 0.020, "poison_light", rng)


BUILDERS = (concrete, asphalt, quarry, sand, metal, scorched)


def export(tile):
    bpy.ops.object.select_all(action='DESELECT')
    tile.select_set(True)
    bpy.context.view_layer.objects.active = tile
    path = ART / (tile.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', use_triangles=True, add_leaf_bones=False)
    tile.data.calc_loop_triangles()
    return path, len(tile.data.loop_triangles)


def verify(assets):
    for path, triangle_count in assets:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(path))
        tile, = bpy.context.scene.objects
        assert tile.type == 'MESH' and tile.data.uv_layers.active and len(tile.data.materials) == 1, path.name
        tile.data.calc_loop_triangles()
        assert len(tile.data.loop_triangles) == triangle_count, path.name
        points = [tile.matrix_world @ vertex.co for vertex in tile.data.vertices]
        for axis in (0, 1):
            assert abs(min(p[axis] for p in points) + HALF) < 1e-4, path.name
            assert abs(max(p[axis] for p in points) - HALF) < 1e-4, path.name
        assert all(any(abs(p.x-x) < 1e-4 and abs(p.y-y) < 1e-4 and abs(p.z) < 1e-4 for p in points)
                   for x in (-HALF, HALF) for y in (-HALF, HALF)), path.name
        assert min(p.z for p in points) > -1e-4 and max(p.z for p in points) < 0.022, path.name
    print(f"GROUND TILE PASS: {len(assets)} meshes, {sum(count for _, count in assets)} triangles total", flush=True)


def catalog(tiles, palette):
    scene = kit.preview_setup()
    board = kit.Surface("Catalog graphics (preview only)")
    board.rect(0, 0.6, 65, 34, -0.022, "background")
    kit.label(board, "ARENA GROUND KIT", 0, 16.15, 1.22, "label", -0.012)
    kit.label(board, "18 MODULES   /   8 x 8 M", 0, 14.89, 0.53, "board_line", -0.012)
    for col, (family, names) in enumerate(FAMILIES):
        x = (col-2.5)*10.0
        kit.label(board, family, x, 13.77, 0.73, "label", -0.012)
        for row, label in enumerate(names):
            y = 8.85-row*9.62
            tiles[col][row].location = (x, y, 0)
            kit.label(board, label.upper(), x, y-4.58, 0.55, "label", -0.012)
    board_obj = board.object(palette)
    scene.camera.location = (0, -0.003, 47)
    kit.aim(scene.camera, (0, 0.35, 0))
    scene.camera.data.type = 'ORTHO'
    scene.camera.data.ortho_scale = 62.0
    scene.render.resolution_x, scene.render.resolution_y = (1720, 1000)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(PREVIEW / "ArenaGroundKit.blend"))
    kit.render(scene, "catalog", (0, 0.35-0.003, 47), (0, 0.35, 0), (1720, 1000), ortho=62.0)
    bpy.data.objects.remove(board_obj, do_unlink=True)
    return scene


def compositions(scene, tiles, palette):
    for group in tiles:
        for tile in group:
            tile.hide_render = True
    # The entries are (family, variant, quarter turns), listed south to north.
    compositions = [
        ("INDUSTRIAL", [[(0, 1, 0), (1, 0, 0), (4, 0, 0)],
                         [(1, 0, 1), (1, 2, 0), (1, 0, 1)],
                         [(0, 2, 0), (1, 0, 0), (4, 2, 1)]]),
        ("QUARRY EDGE", [[(3, 2, 1), (3, 1, 1), (2, 2, 1)],
                          [(3, 0, 0), (2, 1, 1), (2, 0, 3)],
                          [(2, 1, 0), (5, 2, 1), (5, 1, 2)]]),
    ]
    board = kit.Surface("Composition graphics (preview only)")
    board.rect(0, 0.5, 90, 46, -0.025, "background")
    for side, (name, grid) in enumerate(compositions):
        centre = (side-0.5)*29
        kit.label(board, name, centre, 13.5, 1.03, "label", -0.012)
        for row, row_tiles in enumerate(grid):
            for col, (family, variant, angle) in enumerate(row_tiles):
                original = tiles[family][variant]
                tile = bpy.data.objects.new(original.name + " (preview)", original.data)
                bpy.context.collection.objects.link(tile)
                tile.location = (centre + (col-1)*8, (row-1)*8, 0)
                tile.rotation_euler.z = angle*math.pi/2
    board.object(palette)
    kit.render(scene, "combinations", (0, -0.003, 46), (0, 0, 0), (1450, 820), ortho=58.0)


def main():
    ART.mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    kit.ART, kit.PREVIEW, kit.COLORS = ART, PREVIEW, COLORS
    kit.SWATCHES = {name: ((i % 8 + 0.5)/8, (i // 8 + 0.5)/8) for i, name in enumerate(COLORS)}
    palette = kit.palette_material("ArenaGroundKit")
    tiles, assets = [], []
    for i, ((family, variants), build) in enumerate(zip(FAMILIES, BUILDERS)):
        group = []
        for j, label in enumerate(variants):
            name = f"{i*3+j+1:02d}_{family.title()}_{label.replace(' ', '')}"
            mesh = kit.Surface(name)
            build(mesh, random.Random(8019+i*31+j), j)
            tile = mesh.object(palette)
            group.append(tile)
            assets.append(export(tile))
        tiles.append(group)
    scene = catalog(tiles, palette)
    compositions(scene, tiles, palette)
    verify(assets)


if __name__ == "__main__":
    main()
