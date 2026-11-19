"""Export the ground fire kit and render the parts and three assembled sizes."""

import math
import random
import sys
from pathlib import Path

import bpy
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from make_surface_hazards import ART, PREVIEW, COLORS, kit, band, ember


FLAMES = (
    ("Large_A", 1.0, 0.33, 7, 0.1, "fork"),
    ("Large_B", 1.0, 0.33, 6, 0.6, "curl"),
    ("Mid_A", 0.7, 0.26, 6, 0.2, "fork"),
    ("Mid_B", 0.7, 0.25, 7, 0.7, "straight"),
    ("Mid_C", 0.7, 0.24, 5, 0.5, "curl"),
    ("Small_A", 0.4, 0.19, 5, 0.4, "straight"),
    ("Small_B", 0.4, 0.18, 5, 0.9, "curl"),
)


def make_materials():
    kit.SWATCHES = {name: ((i % 8 + 0.5)/8, (i // 8 + 0.5)/8)
                    for i, name in enumerate(COLORS)}
    texture = bpy.data.images.load(str(ART / "SurfaceHazards_Palette.png"))
    texture.pack()
    material = bpy.data.materials.new("SurfaceHazardsPalette")
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    shader.inputs["Roughness"].default_value = 0.92
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = texture
    node.interpolation = 'Closest'
    material.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
    glow = material.copy()
    glow.name = "HazardEmissive"
    nodes = glow.node_tree.nodes
    shader = next(node for node in nodes if node.type == 'BSDF_PRINCIPLED')
    texture_node = next(node for node in nodes if node.type == 'TEX_IMAGE')
    glow.node_tree.links.new(texture_node.outputs["Color"], shader.inputs["Emission Color"])
    shader.inputs["Emission Strength"].default_value = 0.58
    return material, glow


def make_base(material):
    surface = kit.Surface("Fire_Base")

    def outline(radius, z, variation, phase):
        points = []
        for i in range(24):
            a = i*math.tau/24
            r = radius + variation*(0.48*math.sin(3*a+phase) + 0.32*math.sin(7*a) +
                                    0.20*math.sin(a+phase))
            points.append((r*math.cos(a), r*math.sin(a), z))
        return points

    outer = outline(0.988, 0.010, 0.012, 0)
    furthest = max(math.hypot(x, y) for x, y, _ in outer)
    outer = [(x/furthest, y/furthest, z) for x, y, z in outer]
    ash = outline(0.77, 0.014, 0.17, 0.9)
    coal = outline(0.49, 0.017, 0.14, 2.4)
    band(surface, ash, outer, "soot")
    band(surface, coal, ash, "ash")
    surface.face(coal, "coal")
    return surface.object(material)


def make_rim(material):
    surface = kit.Surface("Fire_Rim_Segment")
    angles = [math.radians(-15 + i*5) for i in range(7)]
    rows = []
    for radii, heights in (
        ([0.75, 0.79, 0.75, 0.80, 0.75, 0.78, 0.75], [0.018]*7),
        ([0.89, 0.86, 0.92, 0.87, 0.90, 0.88, 0.89], [0.020]*7),
        ([1, 0.975, 1, 0.986, 1, 0.982, 1], [0.018]*7),
    ):
        rows.append([(r*math.cos(a)-1, r*math.sin(a), h)
                     for r, a, h in zip(radii, angles, heights)])
    for i in range(6):
        for inner, outer in zip(rows, rows[1:]):
            surface.face((inner[i], outer[i], outer[i+1], inner[i+1]), "soot")
    return surface.object(material)


def make_coals(material, variant):
    surface = kit.Surface(f"Fire_Coals_Patch_{variant}")
    rng = random.Random(5510 + ord(variant))
    for i in range(8):
        angle = i*2.39996 + rng.uniform(-0.24, 0.24)
        radius = 0.49*math.sqrt(i/7)
        x, y = radius*math.cos(angle), radius*math.sin(angle)
        width, depth = rng.uniform(0.06, 0.17), rng.uniform(0.025, 0.07)
        surface.rect(x, y, width, depth, 0.027,
                     ("hot_coal", "flame_dark", "ember", "hot_coal", "warm")[i % 5], angle)
    return surface.object(material)


def flame_shell(surface, height, width, sides, phase, profile, shades, tip_style,
                offset=(0, 0)):
    rings = []
    for level, (t, spread) in enumerate(profile):
        ring = []
        for i in range(sides):
            angle = phase + i*math.tau/sides + t*0.48
            radius = spread * width/2 * (1 + 0.13*math.sin(3*angle + level))
            ring.append((offset[0] + radius*math.cos(angle),
                         offset[1] + radius*math.sin(angle)*0.87,
                         t*height))
        rings.append(ring)
    surface.face(tuple(reversed(rings[0])), shades[0])
    for level, (low, high) in enumerate(zip(rings, rings[1:])):
        for i in range(sides):
            j = (i+1) % sides
            shade = shades[min(2, level + int((i + level) % 4 == 0))]
            next_shade = shades[min(2, level + int((i + 2*level) % 3 != 1))]
            surface.face((low[i], low[j], high[j]), shade)
            surface.face((low[i], high[j], high[i]), next_shade)
    twist = width * 0.18 if tip_style == "curl" else 0
    top = (offset[0]+twist*math.cos(phase), offset[1]+twist*math.sin(phase), height)
    upper = rings[-1]
    if tip_style == "fork":
        split = sides//2
        second = (offset[0]-width*0.20, offset[1]+width*0.06, height*0.85)
        for i in range(sides):
            surface.face((upper[i], upper[(i+1) % sides], top if i < split else second),
                         shades[1 + i % 2])
        surface.face((upper[split], second, top), shades[1])
        surface.face((upper[0], top, second), shades[2])
    else:
        for i in range(sides):
            surface.face((upper[i], upper[(i+1) % sides], top), shades[1 + i % 2])


def make_flame(material, variant, height, width, sides, phase, shape):
    surface = kit.Surface(f"Fire_Flame_{variant}")
    profile = [(0, 0.78), (0.31, 0.89), (0.63, 0.39)]
    if variant.startswith("Small"):
        profile = [(0, 1.00), (0.49, 0.51)]
    flame_shell(surface, height, width, sides, phase, profile,
                ("warm", "flame", "flame_dark"), shape)
    # The bright inner tongue breaks through the lower facets of the orange shell.
    offset = (0.30*width*math.cos(phase), 0.30*width*math.sin(phase))
    flame_shell(surface, height*0.64, width*0.57, 5, phase+0.2,
                [(0.015, 0.95), (0.42, 0.67)], ("core", "warm", "flame"), "straight", offset)
    return surface.object(material)


def make_parts(base, glow):
    parts = [make_base(base), make_rim(base)]
    parts += [make_coals(glow, variant) for variant in "ABC"]
    parts += [make_flame(glow, *spec) for spec in FLAMES]
    spark = kit.Surface("Fire_Spark")
    ember(spark, 0, 0, -0.007142857, 0.017857143)
    flake = kit.Surface("Fire_Ember_Flake")
    triangle = ((-0.029, -0.013, 0), (0.031, -0.008, 0.004), (-0.006, 0.021, -0.003))
    flake.face(triangle, "ash")
    parts += [spark.object(glow), flake.object(base)]
    return parts


def label(body, position, size, material, camera):
    data = bpy.data.curves.new(body, 'FONT')
    data.body, data.size = body, size
    data.align_x = 'CENTER'
    obj = bpy.data.objects.new(body, data)
    bpy.context.collection.objects.link(obj)
    obj.location = position
    obj.rotation_euler = camera.rotation_euler
    data.materials.append(material)


def instance(source, x, y, z=0):
    obj = bpy.data.objects.new(source.name + " demo", source.data)
    bpy.context.collection.objects.link(obj)
    obj.location = (x, y, z)
    return obj


def show_zone(parts, x, y, radius, seed):
    rng = random.Random(seed)
    base = instance(parts[0], x, y)
    base.scale = (radius, radius, 1)
    count = round(math.tau*radius / (math.tau/12))
    rim = parts[1].data.copy()
    for vertex in rim.vertices:
        vx, vy, _ = vertex.co
        angle = math.atan2(vy, vx+1)*12/count
        r = radius + math.hypot(vx+1, vy) - 1
        vertex.co.x, vertex.co.y = r*math.cos(angle)-radius, r*math.sin(angle)
    for i in range(count):
        a = i*math.tau/count
        obj = instance(parts[1], x+radius*math.cos(a), y+radius*math.sin(a))
        obj.data = rim
        obj.rotation_euler.z = a
    flame_count = round(1.05*math.pi*radius**2)
    for i in range(flame_count):
        d = radius*0.90*math.sqrt((i+0.5)/flame_count)
        a = i*2.39996+rng.uniform(-0.17, 0.17)
        px, py = x+d*math.cos(a), y+d*math.sin(a)
        variant = rng.choice(parts[5:7] if d/radius < 0.40 else
                             parts[7:10] if d/radius < 0.73 else parts[10:12])
        obj = instance(variant, px, py, 0.034)
        obj.scale.z = rng.uniform(0.70, 1.14)
        obj.rotation_euler = (rng.uniform(-0.07, 0.07), rng.uniform(0.10, 0.30), rng.uniform(0, math.tau))
        if i % 5 < 2:
            coal = instance(rng.choice(parts[2:5]), x+(px-x)*0.87, y+(py-y)*0.87)
            coal.rotation_euler.z = rng.uniform(0, math.tau)
        if i % 4 == 0:
            spark = instance(parts[12], px+0.18, py, rng.uniform(0.7, 1.65))
            spark.rotation_euler.y = rng.uniform(-0.5, 0.5)


def presentation(parts):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.world = bpy.data.worlds.new("Preview sky")
    scene.world.use_nodes = True
    background = next(node for node in scene.world.node_tree.nodes if node.type == 'BACKGROUND')
    background.inputs["Strength"].default_value = 0.35
    bpy.ops.object.light_add(type='SUN', location=(-5, -8, 12))
    sun = bpy.context.object
    sun.data.energy = 1.55
    sun.data.angle = math.radians(12)
    kit.aim(sun, (0, 0, 0))
    bpy.ops.object.camera_add()
    scene.camera = bpy.context.object
    scene.view_settings.view_transform = 'Standard'
    scene.camera.location = (0, -18, 25.7)
    kit.aim(scene.camera, (0, 0, 0))
    scene.camera.data.type = 'ORTHO'
    scene.camera.data.ortho_scale = 26
    scene.render.resolution_x, scene.render.resolution_y = 1760, 1290
    floor = bpy.data.materials.new("Preview slate")
    floor.diffuse_color = (0.048, 0.051, 0.059, 1)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.010))
    bpy.context.object.data.materials.append(floor)
    bpy.context.object.name = "Preview floor"
    ink = bpy.data.materials.new("Labels")
    ink.use_nodes = True
    nodes = ink.node_tree.nodes
    text_shader = nodes.new('ShaderNodeEmission')
    text_shader.inputs[0].default_value = (0.65, 0.70, 0.74, 1)
    output = next(node for node in nodes if node.type == 'OUTPUT_MATERIAL')
    ink.node_tree.links.new(text_shader.outputs[0], output.inputs[0])
    label("GROUND FIRE KIT  /  14 PARTS", (0, 10.45, 0.1), 0.57, ink, scene.camera)
    for i, obj in enumerate(parts):
        x, y = (i % 7 - 3)*3.5, 8.3 - (i//7)*2.8
        obj.location = (x, y, 0.08)
        if i >= 12:
            display = instance(obj, x, y, 0.29)
            display.scale = (11, 11, 11)
            obj.hide_render = True
        text = obj.name.removeprefix("Fire_").replace("_", " ")
        if i >= 12:
            text += "  x11"
        label(text, (x, y-1.35, 0.1), 0.28, ink, scene.camera)
    for x, radius, seed in ((-10.3, 1.5, 22), (-5.3, 3, 28), (5.5, 6, 28)):
        show_zone(parts, x, -4.7, radius, seed)
        label(f"RADIUS {radius:g} m", (x, -5.15-radius, 0.1), 0.34, ink, scene.camera)
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.filepath = str(PREVIEW / "preview_kit.png")
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(PREVIEW / "GroundFireKit.blend"))
    bpy.ops.render.render(write_still=True)


def verify(path, counts):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objects = {obj.name: obj for obj in bpy.context.scene.objects}
    assert set(objects) == set(counts), list(objects)
    for name, obj in objects.items():
        mesh = obj.data
        mesh.calc_loop_triangles()
        expected, limit = counts[name]
        assert len(mesh.loop_triangles) == expected <= limit, (name, expected, limit)
        assert all(triangle.area > 1e-8 for triangle in mesh.loop_triangles), name
        assert obj.parent is None and mesh.uv_layers.active and len(mesh.materials) == 1, name
        assert all(not face.use_smooth for face in mesh.polygons), name
        assert all(abs(s-1) < 1e-5 for s in obj.scale), (name, obj.scale)
        points = [obj.matrix_world.to_3x3() @ v.co for v in mesh.vertices]
        if name.startswith("Fire_Flame_"):
            height = dict(Large=1, Mid=0.7, Small=0.4)[name.split("_")[2]]
            assert abs(max(p.z for p in points) - height) < 1e-5 and abs(min(p.z for p in points)) < 1e-5, name
        if name == "Fire_Base":
            assert abs(max(math.hypot(p.x, p.y) for p in points)-1) < 1e-5
    print("GROUND FIRE KIT PASS:", counts, flush=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    parts = make_parts(*make_materials())
    limits = [120, 24, 80, 80, 80, 70, 70, 60, 60, 60, 40, 40, 8, 4]
    counts = {}
    for i, (obj, limit) in enumerate(zip(parts, limits)):
        obj.location = ((i % 7 - 3)*2.4, -(i//7)*2.6, 0)
        obj.select_set(True)
        obj.data.calc_loop_triangles()
        counts[obj.name] = len(obj.data.loop_triangles), limit
    path = ART / "GroundFireKit.fbx"
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS',
                             bake_space_transform=True,
                             use_triangles=True, mesh_smooth_type='FACE', bake_anim=False,
                             add_leaf_bones=False)
    presentation(parts)
    verify(path, counts)


if __name__ == "__main__":
    main()
