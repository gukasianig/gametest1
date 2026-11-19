"""Turn the approved-for-preview F2 material into two example meshes."""

import math
import random
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import make_cargo_terminal as kit
import preview_ground_materials as lookdev


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/Arena/F2Clinker"
PREVIEW = ROOT / "docs/art/Arena/F2Clinker"
RNG = random.Random(84502)
SEED = 6091 + 5*223 + 39


def shaded_variant(source, name, tint, strength):
    material = source.copy()
    material.name = name
    tree = material.node_tree
    shader = tree.nodes.get("Principled BSDF")
    source_socket = shader.inputs["Base Color"].links[0].from_socket
    color = tree.nodes.new("ShaderNodeMixRGB")
    color.blend_type = 'MULTIPLY'
    color.inputs[0].default_value = 1
    color.inputs[2].default_value = (*tint, 1)
    tree.links.new(source_socket, color.inputs[1])
    tree.links.new(color.outputs[0], shader.inputs["Base Color"])
    tree.nodes.get("Normal Map").inputs["Strength"].default_value = strength
    return material


def make_materials():
    lookdev.CACHE = ART
    surface = lookdev.material(5, 1)
    surface.name = "F2_Clinker"
    fields = lookdev.pattern(SEED)
    _, height, roughness, _ = lookdev.volcanic(SEED, 1, fields)
    mask = np.zeros((*roughness.shape, 4), dtype=np.float32)
    mask[..., 3] = 1-np.clip(roughness, 0, 1)
    lookdev.png(ART / "F2_MetallicSmoothness.png", mask)
    for image in bpy.data.images:
        if image.source == 'FILE':
            image.pack()
    return surface, shaded_variant(surface, "F2_CutEdge", (0.64, 0.58, 0.57), 0.63), height, fields


def create_mesh(name, vertices, faces, materials, uv, material_indices):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    assert not mesh.validate(), name
    mesh.update()
    channel = mesh.uv_layers.new(name="UVMap")
    for material in materials:
        mesh.materials.append(material)
    for poly, coords, index in zip(mesh.polygons, uv, material_indices):
        poly.material_index = index
        for loop, point in zip(poly.loop_indices, coords):
            channel.data[loop].uv = point
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def tile(surface, edge, height, fields):
    grid = 28
    stride = grid+1
    broad, stains, _, _, _, _ = fields
    vertices, faces, uv, slots = [], [], [], []
    for j in range(stride):
        for i in range(stride):
            x, y = -4+8*i/grid, -4+8*j/grid
            u = min(lookdev.SIZE-1, round(i/grid*(lookdev.SIZE-1)))
            v = min(lookdev.SIZE-1, round((1-j/grid)*(lookdev.SIZE-1)))
            border = max(0, min(1, (4-max(abs(x), abs(y)))/0.68))
            ramp = border*border*(3-2*border)
            z = ramp*max(0.004, 0.019+0.044*(broad[v, u]-0.32)+0.035*stains[v, u]+0.42*height[v, u])
            vertices.append((x, y, z))
    def add(face, slot, coords=None):
        faces.append(face)
        slots.append(slot)
        uv.append(coords if coords is not None else [((vertices[i][0]+4)/8, (vertices[i][1]+4)/8) for i in face])

    for j in range(grid):
        for i in range(grid):
            index = j*stride+i
            a, b, c, d = index, index+1, index+stride+1, index+stride
            for tri in ((a, b, d), (b, c, d)) if (i+j) % 2 else ((a, b, c), (a, c, d)):
                add(tri, 0)
    # A shallow closed slab, with exactly level and matching edges.
    boundary = ([i for i in range(grid)] + [j*stride+grid for j in range(grid)]
                + [grid*stride+i for i in range(grid, 0, -1)] + [j*stride for j in range(grid, 0, -1)])
    bottom = []
    for index in boundary:
        bottom.append(len(vertices))
        x, y, _ = vertices[index]
        vertices.append((x, y, -0.22))
    centre = len(vertices)
    vertices.append((0, 0, -0.22))
    for k, a in enumerate(boundary):
        j = (k+1) % len(boundary)
        b = boundary[j]
        u0, u1 = k/grid, (k+1)/grid
        add((bottom[k], bottom[j], b, a), 1, ((u0, 0.16), (u1, 0.16), (u1, 0.1875), (u0, 0.1875)))
        add((bottom[j], bottom[k], centre), 1)
    obj = create_mesh("F2_Clinker_Tile_8m", vertices, faces, [surface, edge], uv, slots)
    for polygon in obj.data.polygons:
        polygon.use_smooth = polygon.material_index == 0
    return obj


def fragment(vertices, faces, uv, x, y, z, width, depth, height, angle):
    n = RNG.choice((7, 8, 9))
    shape = [RNG.uniform(0.76, 1.10) for i in range(n)]
    ring_angles = [math.tau*i/n for i in range(n)]
    drift_x, drift_y = RNG.uniform(-0.31, 0.31), RNG.uniform(-0.23, 0.23)
    c, s = math.cos(angle), math.sin(angle)
    start = len(vertices)
    for level, scale in [(0, 0.72), (0.14, 0.97), (0.49, 0.92), (0.77, 0.65), (1.0, 0.40)]:
        for i, a in enumerate(ring_angles):
            radius = shape[i]*scale*RNG.uniform(0.86, 1.12)
            u = width*(radius*math.cos(a)+drift_x*level)
            v = depth*(radius*math.sin(a)+drift_y*level)
            h = level if level in (0, 1) else level+RNG.uniform(-0.08, 0.09)
            vertices.append((x+u*c-v*s, y+u*s+v*c, z+h*height))
    top_center = len(vertices)
    vertices.append((x+width*drift_x*0.82, y+depth*drift_y*0.82, z+height*0.85))
    faces_local = [tuple(start+i for i in range(n-1, -1, -1))]
    for row in range(4):
        for i in range(n):
            a, b = start+row*n+i, start+row*n+(i+1) % n
            faces_local.extend(((a, b, b+n), (a, b+n, a+n)))
    for i in range(n):
        faces_local.append((start+4*n+i, start+4*n+(i+1) % n, top_center))
    ox, oy = RNG.random(), RNG.random()
    for face in faces_local:
        faces.append(face)
        pts = [Vector(vertices[i]) for i in face]
        normal = (pts[1]-pts[0]).cross(pts[2]-pts[0])
        axis = max(range(3), key=lambda i: abs(normal[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][axis]
        uv.append([(ox+p[a]*0.205, oy+p[b]*0.205) for p in pts])


def rocks(surface):
    vertices, faces, uv = [], [], []
    specs = [(-0.61, 0.27, 0, 0.98, 0.75, 1.29, 0.18),
             (0.59, 0.36, 0, 0.78, 0.69, 1.06, -0.49),
             (0.04, -0.64, 0, 0.84, 0.58, 0.71, 0.23),
             (-1.04, -0.56, 0, 0.51, 0.40, 0.51, -0.47),
             (0.39, 0.73, 0.18, 0.43, 0.53, 0.92, 0.4),
             (-0.14, 0.37, 0.39, 0.63, 0.52, 0.76, -0.19)]
    for _ in range(17):
        a, d = RNG.uniform(0, math.tau), RNG.uniform(0.94, 1.92)
        w = RNG.uniform(0.09, 0.27)
        specs.append((d*math.cos(a), 0.74*d*math.sin(a), 0, w, w*RNG.uniform(0.64, 0.96),
                      w*RNG.uniform(0.56, 1.12), RNG.uniform(0, math.tau)))
    for spec in specs:
        fragment(vertices, faces, uv, *spec)
    return create_mesh("F2_Clinker_RockCluster", vertices, faces, [surface], uv, [0]*len(faces))


def export(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = ART / f"{obj.name}.fbx"
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'}, axis_forward='-Z',
                             axis_up='Y', use_triangles=True, add_leaf_bones=False)
    obj.data.calc_loop_triangles()
    return path, len(obj.data.loop_triangles), len(obj.data.materials)


def floor():
    bpy.ops.mesh.primitive_plane_add(size=130, location=(0, 0, -0.222))
    obj = bpy.context.object
    obj.name = "Preview stage (not exported)"
    obj.data.materials.append(lookdev.flat_material("Preview graphite", (0.033, 0.039, 0.052)))


def preview(tile_obj, rocks_obj):
    floor()
    scene = kit.preview_setup()
    sun = next(obj for obj in scene.objects if obj.type == 'LIGHT')
    sun.location = (-18, -12, 13)
    sun.data.color = (1, 0.89, 0.78)
    sun.data.energy = 3.25
    sun.data.angle = math.radians(17)
    kit.aim(sun, (0, 0, 0))
    scene.world.node_tree.nodes.get("Background").inputs["Strength"].default_value = 0.55
    tile_obj.location.x = -2.7
    rocks_obj.location = (3.45, -0.9, -0.22)
    scene.camera.location = (6.1, -11.7, 9.2)
    kit.aim(scene.camera, (-0.65, 0, 0.1))
    scene.camera.data.type = 'ORTHO'
    scene.camera.data.ortho_scale = 15.0
    scene.render.resolution_x, scene.render.resolution_y = (1480, 950)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(PREVIEW / "F2_Clinker.blend"))
    kit.render(scene, "preview", (6.1, -11.7, 9.2), (-0.65, 0, 0.1), (1480, 950), ortho=15.0)

    tile_obj.location = (-4, -4, 0)
    for x, y in [(-4, 4), (4, -4), (4, 4)]:
        obj = bpy.data.objects.new("Tile assembly", tile_obj.data)
        bpy.context.collection.objects.link(obj)
        obj.location = (x, y, 0)
    rocks_obj.location = (2.8, 3.8, 0.035)
    obj = bpy.data.objects.new("Scaled rock cluster", rocks_obj.data)
    bpy.context.collection.objects.link(obj)
    obj.location = (-3.85, 3.4, 0.033)
    obj.rotation_euler.z = 1.85
    obj.scale = (0.64, 0.64, 0.64)
    kit.render(scene, "assembly", (4, -8, 22), (0, 0, 0), (1420, 1000), ortho=29.0)


def verify(asset):
    path, expected, material_slots = asset
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    obj, = bpy.context.scene.objects
    assert obj.type == 'MESH' and obj.data.uv_layers.active, path
    assert len(obj.data.materials) == material_slots, path
    obj.data.calc_loop_triangles()
    assert len(obj.data.loop_triangles) == expected, (path, len(obj.data.loop_triangles), expected)
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    minimum = [min(p[i] for p in points) for i in range(3)]
    maximum = [max(p[i] for p in points) for i in range(3)]
    if material_slots == 2:
        assert all(abs(minimum[i]+4) < 0.0001 and abs(maximum[i]-4) < 0.0001 for i in (0, 1))
        assert -0.221 < minimum[2] < -0.219 and 0.02 < maximum[2] < 0.095
        border = [p for p in points if abs(p.x) > 3.9999 or abs(p.y) > 3.9999]
        assert all(abs(p.z) < 0.0001 or abs(p.z+0.22) < 0.0001 for p in border)
    else:
        assert abs(minimum[2]) < 0.0001 and 1 < maximum[2] < 1.5
    dimensions = " × ".join(f"{b-a:.3f}" for a, b in zip(minimum, maximum))
    print(f"F2 PASS: {path.name}, {expected} triangles, {dimensions} m", flush=True)


def main():
    ART.mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.view.language = 'en_US'
    bpy.context.preferences.view.use_translate_new_dataname = False
    kit.PREVIEW = PREVIEW
    surface, edge, height, fields = make_materials()
    stone = shaded_variant(surface, "F2_FracturedStone", (0.68, 0.72, 0.76), 0.56)
    tile_obj = tile(surface, edge, height, fields)
    rocks_obj = rocks(stone)
    assets = [export(tile_obj), export(rocks_obj)]
    preview(tile_obj, rocks_obj)
    for asset in assets:
        verify(asset)


if __name__ == "__main__":
    main()
