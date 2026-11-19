"""Build the optional Cargo Terminal arena with Blender 4.5 (background mode)."""

import math
import random
import struct
import sys
import zlib
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/Arena/CargoTerminal"
PREVIEW = ROOT / "docs/art/Arena/CargoTerminal"
KENNEY = ROOT / "Assets/Art/Kenney"
HALF = 110
RANDOM = random.Random(1108)
COLORS = dict(
    joint="535b59", concrete="777f7b", cold="6d7777", warm="817e74", repair="6e736d",
    seam="626b69", scuff="737b75", apron="505957", road="4f5b61", road_patch="556168",
    steel="34444a", trim="566a6f", shadow="28373c", curb="647471", gravel="65665a",
    ochre="b69a5b", chalk="a5aaa1", jade="73a8a1", pad="46595a", pad_inner="526767",
    amber="d9aa55", roof="546269", rust="906851", box="627d76", stone="7b7767",
)


def palette_material(name="CargoTerminal"):
    # A real sRGB PNG makes the exported palette independent of the Blender view transform.
    swatch = 16
    side = 8 * swatch
    values = [bytes.fromhex(color) for color in COLORS.values()]
    values.extend([values[0]] * (64 - len(values)))
    rows = []
    for y in range(side):
        row = (7 - y // swatch) * 8
        rows.append(b"\0" + b"".join(values[row + x] * swatch for x in range(8)))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data))

    path = ART / f"{name}_Palette.png"
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">2I5B", side, side, 8, 2, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(b"".join(rows))) + chunk(b"IEND", b""))
    texture = bpy.data.images.load(str(path))
    texture.pack()
    material = bpy.data.materials.new(f"{name}Palette")
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = 0.88
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = texture
    node.interpolation = "Closest"
    material.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
    return material


SWATCHES = {name: ((i % 8 + 0.5) / 8, (i // 8 + 0.5) / 8) for i, name in enumerate(COLORS)}


class Surface:
    def __init__(self, name):
        self.name = name
        self.vertices = []
        self.indices = {}
        self.faces = []
        self.colors = []
        self.face_keys = set()

    def face(self, points, color):
        indices = []
        for point in points:
            if point not in self.indices:
                self.indices[point] = len(self.vertices)
                self.vertices.append(point)
            indices.append(self.indices[point])
        key = tuple(sorted(indices))
        if len(set(indices)) != len(indices) or key in self.face_keys:
            return
        self.face_keys.add(key)
        self.faces.append(indices)
        self.colors.append(color)

    def rect(self, x, y, width, depth, z, color, angle=0):
        c, s = math.cos(angle), math.sin(angle)
        self.face([(x + u*c - v*s, y + u*s + v*c, z)
                   for u, v in [(-width/2, -depth/2), (width/2, -depth/2),
                                (width/2, depth/2), (-width/2, depth/2)]], color)

    def box(self, x, y, width, depth, bottom, top, color, side):
        self.rect(x, y, width, depth, top, color)
        points = [(x-width/2, y-depth/2), (x+width/2, y-depth/2),
                  (x+width/2, y+depth/2), (x-width/2, y+depth/2)]
        for (ax, ay), (bx, by) in zip(points, points[1:] + points[:1]):
            self.face([(ax, ay, bottom), (bx, by, bottom), (bx, by, top), (ax, ay, top)], side)

    def octagon(self, x, y, radius, z, color):
        self.face([(x + radius*math.cos(a), y + radius*math.sin(a), z)
                   for a in [math.tau*(i+0.5)/8 for i in range(8)]], color)

    def object(self, material):
        mesh = bpy.data.meshes.new(self.name)
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        uv = mesh.uv_layers.new(name="UVMap")
        for poly, color in zip(mesh.polygons, self.colors):
            for loop in poly.loop_indices:
                uv.data[loop].uv = SWATCHES[color]
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        mesh.materials.append(material)
        return obj


def label(surface, body, x, y, size, color, z=0.018):
    font = bpy.data.curves.new("Stencil", 'FONT')
    font.body = body
    font.align_x = 'CENTER'
    font.align_y = 'CENTER'
    font.size = size
    font.resolution_u = 3
    obj = bpy.data.objects.new("Stencil", font)
    bpy.context.collection.objects.link(obj)
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    mesh.calc_loop_triangles()
    for triangle in mesh.loop_triangles:
        surface.face([(x + mesh.vertices[i].co.x, y + mesh.vertices[i].co.y, z)
                      for i in triangle.vertices], color)
    bpy.data.meshes.remove(mesh)
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.curves.remove(font)


def make_floor():
    base = Surface("Deck")
    paint = Surface("Wayfinding")
    base.rect(0, 0, 350, 350, -0.16, "gravel")
    base.rect(0, 0, 2*HALF, 2*HALF, -0.025, "joint")
    cuts = [-110, -100, -90, -80, -70, -60, -50, -40, -30, -20, -12, -4, 4,
            12, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110]
    for left, right in zip(cuts, cuts[1:]):
        for low, high in zip(cuts, cuts[1:]):
            x, y = (left+right)/2, (low+high)/2
            width, depth = right-left, high-low
            road = abs(x) < 4 or abs(y) < 4
            near_edge = max(abs(x), abs(y)) >= 100
            if road:
                base.rect(x, y, width, depth, 0, "road")
                if max(abs(x), abs(y)) > 10 and RANDOM.random() < 0.45:
                    paint.rect(x, y, width*0.65, depth*0.27, 0.005, "road_patch")
                continue

            shades = ["concrete", "concrete", "cold", "warm", "repair"]
            if x*y > 0:
                shades.remove("warm")
            base.rect(x, y, width-0.055, depth-0.055, 0, "apron" if near_edge else RANDOM.choice(shades))
            for _ in range(4):
                px = x + RANDOM.uniform(-0.34, 0.34)*width
                py = y + RANDOM.uniform(-0.34, 0.34)*depth
                paint.rect(px, py, RANDOM.uniform(0.14, 0.65), RANDOM.uniform(0.05, 0.24),
                           0.006, "curb" if near_edge else "scuff", RANDOM.uniform(0, math.pi))
            if abs(x) == 8 and abs(y) > 10:
                paint.rect(x - math.copysign(2.5, x), y, 0.57, 1.75, 0.007, "joint")
                for step in range(-3, 4):
                    paint.rect(x - math.copysign(2.5, x), y + step*0.22, 0.43, 0.065, 0.011, "trim")

    for side in (-1, 1):
        for step in range(13, 104, 7):
            t = side*step
            paint.rect(t, 0, 2.8, 0.11, 0.012, "chalk")
            paint.rect(0, t, 0.11, 2.8, 0.012, "chalk")
        for offset in (-3.55, 3.55):
            paint.rect(side*58, offset, 96, 0.12, 0.011, "ochre")
            paint.rect(offset, side*58, 0.12, 96, 0.011, "ochre")
        for i in range(-10, 11):
            paint.rect(i*10, side*108.3, 3.1, 0.38, 0.016, "ochre")
            paint.rect(side*108.3, i*10, 0.38, 3.1, 0.016, "ochre")

    paint.octagon(0, 0, 6.8, 0.015, "pad")
    paint.octagon(0, 0, 5.95, 0.019, "jade")
    paint.octagon(0, 0, 5.77, 0.023, "pad_inner")
    for i in range(8):
        angle = math.tau*i/8
        paint.rect(6.05*math.cos(angle), 6.05*math.sin(angle), 0.42, 0.14, 0.025, "chalk", angle)
    label(paint, "01", 0, -4.05, 1.05, "chalk", 0.026)
    label(paint, "CARGO TERMINAL", 0, -4.84, 0.43, "chalk", 0.026)

    for number, (x, y) in enumerate([(-10, -7.6), (10, -7.6), (-10, 8), (10, 8)], 1):
        color = "jade" if x*y > 0 else "ochre"
        label(paint, f"{number:02}", x, y, 1.6, color)
        label(paint, "LOADING", x, y-1.15, 0.45, "chalk")
        paint.rect(x, y+1.23, 3.6, 0.09, 0.016, color)

    # Ground paint below the cargo covers already placed by ArenaLayout.
    for x, y, width, depth in [(-15, 13, 9, 6), (16, -14, 6, 9), (31, 22, 9, 6),
                                (-30, -23, 6, 9), (62, -58, 9, 6), (-66, 60, 6, 9),
                                (24, -88, 9, 6), (-28, 90, 6, 9)]:
        paint.rect(x, y, width, depth, 0.010, "apron")
        for side in (-1, 1):
            paint.rect(x, y + side*(depth/2-0.15), width-0.3, 0.10, 0.014, "ochre")
            paint.rect(x + side*(width/2-0.15), y, 0.10, depth-0.3, 0.014, "ochre")
            for offset in (-0.9, -0.5, -0.1, 0.3, 0.7):
                paint.rect(x+offset, y+side*(depth/2-0.48), 0.24, 0.10, 0.014, "ochre", math.pi/4)
    return base, paint


def make_boundary():
    wall = Surface("Perimeter")
    for side in (-1, 1):
        wall.box(side*111, 0, 3, 225, -0.14, 2.15, "steel", "shadow")
        wall.box(0, side*111, 219, 3, -0.14, 2.15, "steel", "shadow")
        wall.box(side*110.65, 0, 2.15, 219, 2.15, 2.58, "trim", "steel")
        wall.box(0, side*110.65, 219, 2.15, 2.15, 2.58, "trim", "steel")
        for t in range(-105, 106, 10):
            wall.box(side*111.4, t, 3.2, 1.4, 0, 3, "roof", "steel")
            wall.box(t, side*111.4, 1.4, 3.2, 0, 3, "roof", "steel")
            wall.rect(side*110.6, t+2, 0.18, 2.2, 2.582, "amber")
            wall.rect(t+2, side*110.6, 2.2, 0.18, 2.582, "amber")
        for t in (-80, -40, 0, 40, 80):
            wall.box(side*115, t, 5, 7, -0.15, 0.35, "apron", "shadow")
            wall.box(t, side*115, 7, 5, -0.15, 0.35, "apron", "shadow")
    return wall


def kit_mesh(path):
    bpy.ops.import_scene.fbx(filepath=str(KENNEY / path))
    imported = list(bpy.context.selected_objects)
    bpy.context.view_layer.update()
    faces = []
    for obj in imported:
        if obj.type != 'MESH':
            continue
        obj.data.calc_loop_triangles()
        for triangle in obj.data.loop_triangles:
            points = [obj.matrix_world @ obj.data.vertices[i].co for i in triangle.vertices]
            normal = (points[1] - points[0]).cross(points[2] - points[0])
            if normal.length_squared > 1e-14:
                faces.append((points, normal.normalized().z))
    for obj in imported:
        bpy.data.objects.remove(obj, do_unlink=True)
    assert faces, path
    dimensions = [(min(p[i] for face, _ in faces for p in face),
                   max(p[i] for face, _ in faces for p in face)) for i in range(3)]
    return faces, dimensions


def place_kit(surface, kit, x, y, z, width, angle, colors, height=None):
    faces, ((x0, x1), (y0, y1), (z0, z1)) = kit
    scale = width / max(x1-x0, y1-y0)
    scale_z = scale if height is None else height/(z1-z0)
    c, s = math.cos(angle), math.sin(angle)
    for points, up in faces:
        transformed = []
        for vx, vy, vz in points:
            u = (vx-(x0+x1)/2)*scale
            v = (vy-(y0+y1)/2)*scale
            transformed.append((x + u*c - v*s, y + u*s + v*c, z+(vz-z0)*scale_z))
        surface.face(transformed, colors[0] if up > 0.55 else colors[1])


def make_kit_details():
    detail = Surface("Reused Kenney modules")
    gate = kit_mesh("ModularSpaceKit/Models/gate.fbx")
    crate = kit_mesh("BlasterKit/Models/crate-wide.fbx")
    rock = kit_mesh("SurvivalKit/Models/rock-b.fbx")
    panel = kit_mesh("SurvivalKit/Models/metal-panel-screws.fbx")
    for side in (-1, 1):
        place_kit(detail, gate, 0, side*115.8, 0.35, 9, 0, ("trim", "steel"))
        place_kit(detail, gate, side*115.8, 0, 0.35, 9, math.pi/2, ("trim", "steel"))
        for t in (-80, -40, 40, 80):
            color = ("box", "steel") if t*side > 0 else ("rust", "steel")
            place_kit(detail, crate, t, side*115, 0.35, 3.6, 0.18*side, color)
            place_kit(detail, crate, side*115, t, 0.35, 4.1, math.pi/2, color)
        for t in range(-123, 124, 18):
            w = RANDOM.uniform(3.5, 9)
            angle = RANDOM.uniform(0, math.tau)
            place_kit(detail, rock, t, side*RANDOM.uniform(121, 132), -0.15, w, angle, ("stone", "gravel"))
            place_kit(detail, rock, side*RANDOM.uniform(121, 132), t, -0.15, w, angle, ("stone", "gravel"))
        for x in (-6.2, 6.2):
            for y in (10.2, 34.5, 74.5):
                place_kit(detail, panel, x, side*y, 0.003, 1.22, 0, ("curb", "joint"), height=0.018)
    return detail


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def preview_setup():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.world = bpy.data.worlds.new("Cloudy sky")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.48, 0.56, 0.67, 1)
    background.inputs["Strength"].default_value = 0.40
    bpy.ops.object.light_add(type='SUN', location=(-35, -55, 80))
    sun = bpy.context.object
    sun.data.energy = 2.0
    sun.data.color = (1.0, 0.88, 0.73)
    sun.data.angle = math.radians(13)
    aim(sun, (0, 0, 0))
    bpy.ops.object.camera_add()
    scene.camera = bpy.context.object
    scene.camera.name = "Arena preview camera"
    scene.camera.data.clip_end = 800
    scene.view_settings.view_transform = 'AgX'
    scene.render.resolution_percentage = 100
    return scene


def render(scene, name, location, target, resolution, ortho=None):
    camera = scene.camera
    camera.location = location
    aim(camera, target)
    camera.data.type = 'ORTHO' if ortho else 'PERSP'
    if ortho:
        camera.data.ortho_scale = ortho
    else:
        camera.data.angle_y = math.radians(60)
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.filepath = str(PREVIEW / f"{name}.png")
    bpy.ops.render.render(write_still=True)


def main():
    ART.mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    material = palette_material()
    objects = [surface.object(material) for surface in (*make_floor(), make_boundary(), make_kit_details())]
    bpy.ops.outliner.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
        obj.data.calc_loop_triangles()
    bpy.context.view_layer.objects.active = objects[0]
    exported = {obj.name: len(obj.data.loop_triangles) for obj in objects}
    triangles = sum(exported.values())
    assert triangles < 32000, triangles
    bpy.ops.export_scene.fbx(filepath=str(ART / "CargoTerminal.fbx"), use_selection=True,
                             object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             add_leaf_bones=False, use_triangles=True)
    scene = preview_setup()
    scene.camera.location = (0, -6, 18)
    aim(scene.camera, (0, 0, 0))
    scene.render.resolution_x, scene.render.resolution_y = (1280, 720)
    scene.camera.data.angle_y = math.radians(60)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(PREVIEW / "CargoTerminal.blend"))
    print(f"CARGO EXPORT: {len(objects)} meshes, {triangles} triangles", flush=True)
    if "--no-render" not in sys.argv:
        render(scene, "overview", (145, -165, 265), (0, 0, 0), (1450, 1100), ortho=347)
        render(scene, "centre", (0, -6, 18), (0, 0, 0), (1280, 720))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ART / "CargoTerminal.fbx"))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    assert len(meshes) == 4, len(meshes)
    for obj in meshes:
        obj.data.calc_loop_triangles()
        assert obj.data.uv_layers.active and len(obj.data.materials) == 1, obj.name
    imported = {obj.name: len(obj.data.loop_triangles) for obj in meshes}
    assert imported == exported, (exported, imported)
    print("CARGO REIMPORT: PASS", flush=True)


if __name__ == "__main__":
    main()
