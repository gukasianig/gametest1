"""Preview 32 procedural arena finishes. Only preview images are project deliverables."""

import math
import struct
import sys
import zlib
from pathlib import Path

import bpy
import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import make_cargo_terminal as kit


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs/art/Arena/GroundLookdev"
CACHE = ROOT / "Logs/ground-lookdev"
SIZE = 640
STEP = 8 / SIZE
U, V = np.meshgrid((np.arange(SIZE, dtype=np.float32)+0.5)/SIZE,
                   1-(np.arange(SIZE, dtype=np.float32)+0.5)/SIZE)
X, Y = (U-0.5)*8, (V-0.5)*8

GROUPS = [
    ("A  CONCRETE", ("Cast Slabs", "Fractured", "Patched", "Washed")),
    ("B  ASPHALT", ("Service Road", "Worn Road", "Junction", "Tarmac")),
    ("C  STONE", ("Slate Beds", "Gravel", "Ironstone", "Cut Quarry")),
    ("D  DESERT", ("Wind Ripples", "Dry Basin", "Tire Ruts", "Red Silt")),
    ("E  METAL", ("Steel Deck", "Drain Grate", "Rust Plate", "Hazard Deck")),
    ("F  VOLCANIC", ("Cold Basalt", "Clinker", "Hot Fissures", "Ash Field")),
    ("G  CHEMICAL", ("Oxidized", "Acid Spatter", "Salt Crust", "Old Spill")),
    ("H  FROST", ("Packed Snow", "Glacial Ice", "Frosted Slab", "Dirty Thaw")),
]


def rgb(color):
    return np.array(list(bytes.fromhex(color)), dtype=np.float32)/255


def smooth(value, low, high):
    t = np.clip((value-low)/(high-low), 0, 1)
    return t*t*(3-2*t)


def mix(a, b, factor):
    return a+(b-a)*factor[..., None]


def paint(color, tint, amount):
    return mix(color, rgb(tint), np.clip(amount, 0, 1))


def noise(seed, cells, u=U, v=V):
    nx, ny = (cells, cells) if isinstance(cells, int) else cells
    lattice = np.random.default_rng(seed).random((ny, nx), dtype=np.float32)
    pu, pv = u*nx, v*ny
    ix, iy = np.floor(pu).astype(np.int32), np.floor(pv).astype(np.int32)
    sx, sy = pu-ix, pv-iy
    sx, sy = sx*sx*(3-2*sx), sy*sy*(3-2*sy)
    x0, y0 = ix % nx, iy % ny
    x1, y1 = (ix+1) % nx, (iy+1) % ny
    a = lattice[y0, x0]*(1-sx) + lattice[y0, x1]*sx
    b = lattice[y1, x0]*(1-sx) + lattice[y1, x1]*sx
    return (a*(1-sy)+b*sy).astype(np.float32)


def cells(seed, count, warp=0):
    rng = np.random.default_rng(seed)
    sites = rng.random((count, count, 3), dtype=np.float32)
    p = U*count+(noise(seed+1, count*2)-0.5)*warp
    q = V*count+(noise(seed+2, count*2)-0.5)*warp
    cx, cy = np.floor(p).astype(np.int32), np.floor(q).astype(np.int32)
    first = np.full(U.shape, 99, dtype=np.float32)
    second = first.copy()
    tint = np.zeros(U.shape, dtype=np.float32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            tx, ty = cx+dx, cy+dy
            site = sites[ty % count, tx % count]
            d = (p-tx-(0.12+0.76*site[:, :, 0]))**2+(q-ty-(0.12+0.76*site[:, :, 1]))**2
            nearer = d < first
            second = np.where(nearer, first, np.minimum(second, d))
            first = np.minimum(first, d)
            tint = np.where(nearer, site[:, :, 2], tint)
    return np.sqrt(first), np.sqrt(second)-np.sqrt(first), tint


def pattern(seed):
    broad = noise(seed, 4)
    mottled = noise(seed+1, 10)
    granular = noise(seed+2, 44)
    pores = noise(seed+3, 142)
    fine = noise(seed+4, 310)
    fractal = 0.36*broad+0.27*mottled+0.19*granular+0.12*pores+0.06*fine
    return broad, mottled, granular, pores, fine, fractal


def seam_distance(value, size):
    frac = np.mod(value, size)
    return np.minimum(frac, size-frac)


def concrete(seed, j, p):
    broad, stains, grain, pores, fine, fractal = p
    slab = np.minimum(seam_distance(X, 4), seam_distance(Y, 4))
    groove = 1-smooth(slab, 0.017, 0.061)
    base = mix(rgb(("555858", "49474a", "626059", "535653")[j]),
               rgb(("a9a69c", "a8a396", "b1a998", "adb4ac")[j]), smooth(fractal, 0.17, 0.86))
    base = paint(base, "3a3c38", groove*0.62)
    mineral = smooth(pores*0.7+grain*0.3, 0.73, 0.93)
    base = paint(base, "ccc4b0", mineral*0.25)
    base = paint(base, "484744", smooth(stains*0.55+broad*0.45, 0.60, 0.81)*0.32)
    height = (pores-0.5)*0.009 + (fine-0.5)*0.004 - groove*0.020
    if j == 1:
        _, gap, _ = cells(seed, 6, 0.65)
        crack = (1-smooth(gap, 0.007, 0.035))*smooth(stains, 0.34, 0.60)
        base = paint(base, "2a2c2d", crack*0.9)
        height -= crack*0.017
    if j == 2:
        patch = 1-smooth(np.maximum(np.abs(X+1.1)/1.16, np.abs(Y-0.68)/0.78)+0.13*grain, 0.92, 1.0)
        base = paint(base, "504f4d", patch*0.64)
        height -= patch*0.004
    if j == 3:
        base = paint(base, "535d48", smooth(stains*broad, 0.35, 0.57)*0.50)
    return base, height, np.clip(0.83+pores*0.14-(j == 3)*stains*0.33, 0, 1), 0


def asphalt(seed, j, p):
    broad, stains, grain, pores, fine, fractal = p
    bitumen = mix(rgb("32383b"), rgb("717473"), smooth(fractal, 0.18, 0.88))
    gravel = mix(rgb("52514c"), rgb("8e8e82"), smooth(fractal*0.62+pores*0.38, 0.2, 0.81))
    vertical = np.abs(X) < 2.38
    horizontal = np.abs(Y) < 2.38
    drive = (vertical | horizontal) if j == 2 else vertical
    if j == 3:
        drive = np.ones(U.shape, dtype=bool)
    color = np.where(drive[..., None], bitumen, gravel)
    color = paint(color, "96958a", smooth(pores*0.67+fine*0.33, 0.69, 0.88)*0.40)
    height = (pores-0.5)*0.012+(fine-0.5)*0.008
    if j == 3:
        repair = (np.abs(Y-1.05+0.17*np.sin(X*2.4)) < 0.05+0.045*stains)
        color = paint(color, "282d30", repair*0.85)
        return color, height-repair*0.008, 0.88+stains*0.09, 0
    dash = np.mod(Y, 1.6) < 0.83
    centre = (np.abs(X) < 0.055) & dash
    stripe = np.abs(np.abs(X)-2.19) < 0.047
    if j == 2:
        centre = (centre & ~horizontal) | ((np.abs(Y) < 0.055) & (np.mod(X, 1.6) < 0.83) & ~vertical)
        stripe = (stripe & ~horizontal) | ((np.abs(np.abs(Y)-2.19) < 0.047) & ~vertical)
    wear = smooth(grain*0.42+pores*0.58, 0.25+(j == 1)*0.18, 0.64)
    color = paint(color, "cbbfa2", centre*wear*0.97)
    color = paint(color, "d2a34f", stripe*wear*0.97)
    if j == 1:
        _, gap, _ = cells(seed+17, 7, 0.78)
        crack = (1-smooth(gap, 0.010, 0.041))*smooth(stains, 0.28, 0.57)
        color = paint(color, "24282a", crack*0.9)
        height -= 0.017*crack
    return color, height, 0.88+stains*0.09, 0


def stone(seed, j, p):
    broad, stains, grain, pores, fine, fractal = p
    d, gap, tint = cells(seed, (11, 59, 17, 9)[j], (0.75, 0.25, 0.65, 0.68)[j])
    crevice = 1-smooth(gap, 0.015, 0.16 if j != 1 else 0.36)
    shadow = ("2c3239", "383831", "44322b", "43413d")[j]
    light = ("9b9eab", "a9a79a", "b0805e", "a7a096")[j]
    stone_tone = np.clip(fractal*0.69 + tint*0.26 + (1-d)*0.17, 0, 1)
    color = mix(rgb(shadow), rgb(light), stone_tone)
    color = paint(color, shadow, crevice*(0.68 if j == 1 else 0.56))
    if j in (0, 3):
        strata = np.sin(Y*6.9+X*1.8+stains*4.1)
        color = paint(color, "a7977d", smooth(strata, 0.62, 0.96)*(0.15 if j == 0 else 0.36))
    color = paint(color, "c7b8a1", smooth(grain*0.42+pores*0.58, 0.67, 0.91)*0.23)
    height = (1-d)*0.015-crevice*(0.022 if j != 1 else 0.013)+(pores-0.5)*0.008
    return color, height, 0.83+fine*0.15, 0


def desert(seed, j, p):
    broad, stains, grain, pores, fine, fractal = p
    shade = ("86684c", "6b4e3c", "877352", "815542")[j]
    light = ("d8bd8b", "c79e78", "d5ba87", "d89870")[j]
    dune = np.sin(V*math.tau*(15 if j != 3 else 9)+1.7*np.sin(U*math.tau)+stains*3.9)
    tone = np.clip(fractal*0.88+(dune*0.5+0.5)*0.20, 0, 1)
    color = mix(rgb(shade), rgb(light), tone)
    height = dune*(0.006 if j == 0 else 0.0028)+(pores-0.5)*0.010+(fine-0.5)*0.0035
    if j == 1:
        _, gap, tint = cells(seed+1, 16, 0.7)
        crack = (1-smooth(gap, 0.015, 0.085))*(0.46+tint*0.54)
        color = paint(color, "524035", crack*0.84)
        height -= 0.017*crack
    if j == 2:
        rut = 1-smooth(np.abs(np.abs(X)-1.36), 0.18, 0.38)
        tread = np.mod(Y+X*0.36, 0.43) < 0.095
        color = paint(color, "695945", rut*(0.27+tread*0.30))
        height -= rut*(0.011+tread*0.011)
    color = paint(color, "e2c69f", smooth(pores*0.58+fine*0.42, 0.68, 0.89)*0.19)
    return color, height, 0.90+fine*0.09, 0


def steel(seed, j, p):
    broad, stains, grain, pores, fine, fractal = p
    seam = 1-smooth(np.minimum(seam_distance(X, 2), seam_distance(Y, 2)), 0.022, 0.064)
    brush = noise(seed+18, (13, 310))
    tone = np.clip(fractal*0.83+brush*0.23, 0, 1)
    color = mix(rgb("333e48"), rgb("98a5a5"), tone)
    color = paint(color, "273039", seam*0.91)
    bx, by = np.abs(seam_distance(X, 2)-0.13), np.abs(seam_distance(Y, 2)-0.13)
    rivets = 1-smooth(np.hypot(bx, by), 0.031, 0.055)
    color = paint(color, "acb5aa", rivets*0.78)
    brush_mask = smooth(brush, 0.76, 0.95)*smooth(grain, 0.49, 0.73)
    color = paint(color, "c4c8bb", brush_mask*0.42)
    height = (brush-0.5)*0.007 - seam*0.022 + rivets*0.008
    rust = smooth(stains*0.37+grain*0.34+pores*0.21+seam*0.08, 0.55-(j == 2)*0.15, 0.77)
    color = paint(color, "82472c", rust*0.86)
    if j == 1:
        grate = np.abs(X) < 1.76
        grooves = grate & (seam_distance(Y, 0.21) < 0.042) & (seam_distance(X, 0.77) > 0.045)
        color = paint(color, "20272c", grooves*0.94)
        height -= grooves*0.026
    if j == 3:
        marking = np.abs(np.abs(Y)-2.45) < 0.27
        stripe = np.mod(X+Y, 0.71) < 0.36
        color = paint(color, "2c2d28", marking*0.76)
        color = paint(color, "d4a340", marking*stripe*smooth(pores, 0.17, 0.71)*0.96)
    return color, height, np.clip(0.45+rust*0.42+pores*0.17, 0, 1), 0.72*(1-rust)


def volcanic(seed, j, p):
    broad, stains, grain, pores, fine, fractal = p
    d, gap, tint = cells(seed, (12, 31, 8, 24)[j], 0.78)
    edge = 1-smooth(gap, 0.008, 0.15)
    charcoal = ("292a2b", "352b28", "2b2527", "383639")[j]
    light = ("777373", "927367", "82615a", "96908b")[j]
    color = mix(rgb(charcoal), rgb(light), np.clip(0.70*fractal+0.23*tint+0.15*(1-d), 0, 1))
    color = paint(color, charcoal, edge*(0.70 if j != 3 else 0.28))
    height = (1-d)*0.016-edge*(0.025 if j != 3 else 0.008)+(pores-0.5)*0.014
    if j == 2:
        heat = smooth(stains, 0.30, 0.66)*(1-smooth(gap, 0.013, 0.102))
        color = paint(color, "ad4726", heat*0.93)
        color = paint(color, "fc983e", heat*(1-smooth(gap, 0.007, 0.040))*0.98)
        color = paint(color, "ffd377", heat*(1-smooth(gap, 0.004, 0.012))*0.80)
    return color, height, 0.83+fine*0.14, 0


def chemical(seed, j, p):
    broad, stains, grain, pores, fine, fractal = p
    color = mix(rgb(("454841", "41453a", "4b504b", "4b4e43")[j]),
                rgb(("a09876", "9aa08a", "9fa68c", "9b9479")[j]), fractal)
    stain = smooth(broad*0.61+stains*0.29+grain*0.10, (0.46, 0.53, 0.48, 0.39)[j], 0.69)
    salt = smooth(grain*0.45+pores*0.55, 0.58, 0.89)*stain
    color = paint(color, ("6c693a", "607a32", "6b7753", "4e562e")[j], stain*0.92)
    color = paint(color, ("b2995b", "a2bc57", "d1d1ac", "a6a163")[j], salt*(1.0 if j in (1, 2) else 0.57))
    if j == 1:
        d, _, tint = cells(seed+9, 23)
        splatter = (1-smooth(d, 0.025, 0.14))*smooth(tint, 0.38, 0.67)
        color = paint(color, "a8c355", splatter*0.93)
    if j == 3:
        line = 1-smooth(np.abs(stain-0.36), 0.025, 0.14)
        color = paint(color, "877d43", line*0.53)
    return color, (pores-0.5)*0.009+salt*0.014, np.clip(0.89+fine*0.10-stain*(0.08 if j == 2 else 0.39), 0, 1), 0


def frost(seed, j, p):
    broad, stains, grain, pores, fine, fractal = p
    color = mix(rgb(("737f86", "40596a", "5b666d", "666766")[j]),
                rgb(("dce0dc", "bad3d8", "cbd3d0", "d1d6cf")[j]), fractal)
    crystal = smooth(pores*0.68+fine*0.32, 0.65, 0.92)
    color = paint(color, "e0e7e5", crystal*0.52)
    height = (pores-0.5)*0.008+stains*0.009
    if j == 1:
        _, gap, tint = cells(seed, 11, 0.78)
        vein = (1-smooth(gap, 0.014, 0.073))*smooth(tint, 0.10, 0.64)
        color = paint(color, "d8e2de", vein*0.91)
        height -= vein*0.005
    if j == 2:
        slab = np.minimum(seam_distance(X, 4), seam_distance(Y, 4))
        joint = (1-smooth(slab, 0.018, 0.086))*smooth(stains, 0.17, 0.69)
        color = paint(color, "505959", joint*0.85)
        height -= joint*0.014
    if j == 3:
        slush = smooth(broad*0.74+stains*0.26, 0.44, 0.67)
        color = paint(color, "6b6255", slush*0.76)
    return color, height, np.clip((0.77, 0.42, 0.68, 0.67)[j]+fine*0.16, 0, 1), 0


BUILDERS = (concrete, asphalt, stone, desert, steel, volcanic, chemical, frost)


def png(path, pixels):
    data = np.clip(np.rint(pixels*255), 0, 255).astype(np.uint8)
    height, width, channels = data.shape
    def chunk(tag, value):
        return struct.pack(">I", len(value))+tag+value+struct.pack(">I", zlib.crc32(tag+value))
    rows = b"".join(b"\0"+row.tobytes() for row in data)
    header = struct.pack(">2I5B", width, height, 8, {1: 0, 3: 2, 4: 6}[channels], 0, 0, 0)
    path.write_bytes(b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR", header)+chunk(b"IDAT", zlib.compress(rows, 5))+chunk(b"IEND", b""))


def image_node(tree, path, x, y, data=False):
    node = tree.nodes.new("ShaderNodeTexImage")
    node.image = bpy.data.images.load(str(path))
    if data:
        node.image.colorspace_settings.name = "Non-Color"
    node.location = (x, y)
    return node.outputs["Color"]


def material(group, variant):
    code = f"{chr(65+group)}{variant+1}"
    seed = 6091+group*223+variant*39
    color, height, roughness, metallic = BUILDERS[group](seed, variant, pattern(seed))
    dx = -(np.roll(height, -1, axis=1)-np.roll(height, 1, axis=1))/(2*STEP)
    dy = (np.roll(height, -1, axis=0)-np.roll(height, 1, axis=0))/(2*STEP)
    n = np.sqrt(dx*dx+dy*dy+1)
    normals = np.stack((dx/n, dy/n, 1/n), axis=-1)*0.5+0.5
    paths = [CACHE / f"{code}_{suffix}.png" for suffix in ("Color", "Normal", "Roughness")]
    for path, value in zip(paths, (color, normals, roughness[..., None])):
        png(path, value)
    mat = bpy.data.materials.new(code+" "+GROUPS[group][1][variant])
    mat.use_nodes = True
    tree = mat.node_tree
    bsdf = tree.nodes.get("Principled BSDF")
    tree.links.new(image_node(tree, paths[0], -660, 260), bsdf.inputs["Base Color"])
    normal = tree.nodes.new("ShaderNodeNormalMap")
    normal.location = (-330, -35)
    tree.links.new(image_node(tree, paths[1], -660, -35, True), normal.inputs["Color"])
    tree.links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])
    tree.links.new(image_node(tree, paths[2], -660, -340, True), bsdf.inputs["Roughness"])
    bsdf.inputs["Metallic"].default_value = float(np.mean(metallic))
    return mat


def flat_material(name, color, emission=0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Emission Color"].default_value = (*color, 1)
    shader.inputs["Emission Strength"].default_value = emission
    return material


def swatch(material, x, y, size):
    bpy.ops.mesh.primitive_plane_add(size=size, location=(x, y, 0))
    obj = bpy.context.object
    obj.name = material.name
    obj.data.materials.append(material)


def draw_board(scene, materials, detail=False):
    previous = set(scene.objects)
    background = kit.Surface("Backdrop")
    text = kit.Surface("Numbers and names")
    if detail:
        background.rect(0, 0, 60, 42, -0.018, "dark")
        kit.label(text, "ARENA GROUND  /  SURFACE DETAILS", 0, 12.1, 1.13, "text", -0.010)
        featured = [(0, 1), (1, 1), (2, 1), (3, 0), (4, 2), (5, 2), (6, 1), (7, 1)]
        for index, (group, variant) in enumerate(featured):
            x, y = (index % 4-1.5)*10.45, 5.7-index//4*11.38
            mat = materials[group][variant]
            swatch(mat, x, y, 9.3)
            kit.label(text, mat.name.upper(), x, y-5.33, 0.60, "text", -0.010)
        filename, resolution, scale = "details", (1840, 1190), 43
    else:
        background.rect(0, 0, 94, 55, -0.018, "dark")
        kit.label(text, "ARENA GROUND  /  MATERIAL STUDIES", 0, 22.45, 1.57, "text", -0.010)
        kit.label(text, "32 SURFACES  /  8 FAMILIES", 0, 20.67, 0.67, "text", -0.010)
        for group, (heading, names) in enumerate(GROUPS):
            x = (group-3.5)*10.1
            kit.label(text, heading, x, 18.52, 0.77, "text", -0.010)
            for variant, name in enumerate(names):
                y = 12.91-variant*10.12
                swatch(materials[group][variant], x, y, 8.9)
                code = f"{chr(65+group)}{variant+1}"
                kit.label(text, f"{code}  {name.upper()}", x, y-5.09, 0.53, "text", -0.010)
        filename, resolution, scale = "catalog", (2240, 1430), 83.1
    background.object(flat_material("Slate display", (0.016, 0.022, 0.031)))
    text.object(flat_material("Text ink", (0.70, 0.75, 0.74), 0.52))
    kit.render(scene, filename, (0, -0.003, 50), (0, 0, 0), resolution, ortho=scale)
    for obj in set(scene.objects)-previous:
        bpy.data.objects.remove(obj, do_unlink=True)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    CACHE.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    kit.PREVIEW = OUT
    kit.SWATCHES = {"dark": (0.5, 0.5), "text": (0.5, 0.5)}
    materials = [[material(group, variant) for variant in range(4)] for group in range(8)]
    scene = kit.preview_setup()
    sun = next(obj for obj in scene.objects if obj.type == 'LIGHT')
    sun.data.energy = 2.55
    sun.location = (-35, -23, 33)
    kit.aim(sun, (0, 0, 0))
    sun.data.color = (1.0, 0.94, 0.85)
    scene.world.node_tree.nodes.get("Background").inputs["Strength"].default_value = 0.48
    draw_board(scene, materials)
    draw_board(scene, materials, detail=True)
    print("LOOKDEV PASS: 32 surfaces, 96 source maps at 640 x 640, two preview sheets", flush=True)


if __name__ == "__main__":
    main()
