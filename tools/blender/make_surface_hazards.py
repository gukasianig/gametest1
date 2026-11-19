"""Build two independent ground hazard models. No scene or gameplay edits."""

import math
import random
import sys
from pathlib import Path

import bpy

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import make_cargo_terminal as kit


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/Arena/SurfaceHazards"
PREVIEW = ROOT / "docs/art/Arena/SurfaceHazards"
COLORS = dict(
    residue="2d3825", wet_edge="495f26", lime_edge="729734", liquid="9acd48",
    liquid_light="bce65a", liquid_dark="84b23d", foam="e0f38e", bubble="edf6ac",
    soot="342927", ash="4c3a36", coal="603c33", hot_coal="a54626", ember="ec5724",
    flame_dark="d94318", flame="f97622", warm="ffab3d", core="ffdf77", spark="ffeab0",
)
RNG = random.Random(506)


def ring(radius, height, phase=0, n=40):
    points = []
    for i in range(n):
        angle = i*math.tau/n
        ripple = 1 + 0.06*math.sin(angle*3+phase) + 0.035*math.cos(angle*7-phase)
        points.append((radius*ripple*math.cos(angle), radius*ripple*math.sin(angle), height))
    return points


def band(surface, inner, outer, color):
    for i in range(len(inner)):
        j = (i+1) % len(inner)
        surface.face((inner[i], outer[i], outer[j], inner[j]), color)


def disc(surface, x, y, radius, z, color, count=12):
    surface.face([(x+radius*math.cos(a), y+radius*math.sin(a), z)
                  for a in [i*math.tau/count for i in range(count)]], color)


def liquid_ripple(surface, x, y, radius):
    inner = [(x+a, y+b, z) for a, b, z in ring(radius*0.72, 0.033, n=16)]
    outer = [(x+a, y+b, z) for a, b, z in ring(radius, 0.033, n=16)]
    band(surface, inner, outer, "foam")


def puddle(base, glow):
    rim = kit.Surface("Acid_Stain")
    fluid = kit.Surface("Acid_Liquid")
    rim.face(ring(3.18, 0.010), "residue")
    rim.face(ring(3.04, 0.013), "wet_edge")
    outer = ring(2.94, 0.020)
    inner = ring(2.66, 0.023)
    band(fluid, inner, outer, "lime_edge")
    fluid.face(inner, "liquid")
    for x, y, r in [(-1.2, 0.5, 0.77), (0.72, -0.83, 0.60), (1.33, 0.89, 0.46), (-0.15, 1.45, 0.37)]:
        patch = [(x+a*1.4, y+b*0.7, z) for a, b, z in ring(r, 0.028, 1.0, 16)]
        fluid.face(patch, "liquid_light" if x < 0 else "liquid_dark")
    for x, y, r in [(-1.63, -0.19, 0.31), (0.1, -1.04, 0.47), (0.99, 1.22, 0.33), (1.62, -0.31, 0.23)]:
        liquid_ripple(fluid, x, y, r)
    for i in range(32):
        a, d = RNG.uniform(0, math.tau), RNG.uniform(0.15, 2.51)
        x, y = d*math.cos(a), d*math.sin(a)
        disc(fluid, x, y, RNG.uniform(0.045, 0.137), 0.037, RNG.choice(("bubble", "foam", "lime_edge")), 8)
    return [rim.object(base), fluid.object(glow)]


def flame(surface, x, y, width, height, lean, phase, shades):
    sides = 6
    dx, dy = lean
    levels = []
    for t, scale in [(0, 0.92), (0.19, 1), (0.44, 0.60), (0.69, 0.34), (0.87, 0.15)]:
        levels.append([(x+math.cos(a)*width*scale+dx*t*t, y+math.sin(a)*width*scale+dy*t*t,
                        0.037+height*t) for a in [phase+i*math.tau/sides for i in range(sides)]])
    for n, (lower, upper) in enumerate(zip(levels, levels[1:])):
        for i in range(sides):
            j = (i+1) % sides
            shade = shades[min(2, (n+1)//2+(i % 3 == 0))]
            surface.face((lower[i], lower[j], upper[j], upper[i]), shade)
    point = (x+dx, y+dy, 0.037+height)
    for i in range(sides):
        surface.face((levels[-1][i], levels[-1][(i+1) % sides], point), shades[-1])


def ember(surface, x, y, z, radius):
    centre = [(x+radius, y, z), (x, y+radius, z), (x-radius, y, z), (x, y-radius, z)]
    for i in range(4):
        j = (i+1) % 4
        surface.face((centre[i], centre[j], (x, y, z+1.8*radius)), "spark")
        surface.face((centre[j], centre[i], (x, y, z-radius)), "warm")


def fire(base, glow):
    ground = kit.Surface("Fire_Scorch")
    coals = kit.Surface("Fire_Coals")
    tongues = kit.Surface("Fire_Flames")
    sparks = kit.Surface("Fire_Sparks")
    ground.face(ring(3.01, 0.010, 1.2), "soot")
    ground.face(ring(2.74, 0.014, 1.2), "ash")
    ground.face(ring(2.36, 0.017, 1.2), "coal")
    for i in range(42):
        a, d = RNG.uniform(0, math.tau), RNG.uniform(0.27, 2.57)
        color = RNG.choice(("ember", "warm", "hot_coal", "flame_dark"))
        coals.rect(d*math.cos(a), d*math.sin(a), RNG.uniform(0.055, 0.42),
                   RNG.uniform(0.026, 0.105), 0.027, color, RNG.uniform(0, math.tau))
    for x, y, h in [(-1.65, -0.52, 0.64), (-1.42, 0.76, 0.94), (-0.74, -1.43, 0.73), (-0.55, 0.25, 0.77),
                     (-0.3, 1.54, 0.68), (0.04, -0.55, 1.05), (0.49, 0.58, 0.92), (0.8, -1.36, 0.7),
                     (1.58, -0.32, 0.87), (1.22, 0.90, 0.75), (0.57, 1.61, 0.58), (1.99, 0.38, 0.49)]:
        width = 0.29*h+0.045
        phase = RNG.uniform(0, math.tau)
        dx = RNG.uniform(0.18, 0.44)
        dy = RNG.uniform(-0.15, 0.22)
        flame(tongues, x, y, width, h, (dx, dy), phase, ("warm", "flame", "flame_dark"))
        flame(tongues, x-0.13, y-0.15, width*0.57, h*0.69, (-dx*0.64, dy*0.9), phase,
              ("core", "warm", "flame"))
        ember(sparks, x+dx*1.5, y+dy+0.17, h*1.17+0.03, RNG.uniform(0.035, 0.063))
    return [ground.object(base), coals.object(glow), tongues.object(glow), sparks.object(glow)]


def emissive(material):
    glow = material.copy()
    glow.name = "HazardEmissive"
    nodes = glow.node_tree.nodes
    texture = next(node for node in nodes if node.type == 'TEX_IMAGE')
    shader = nodes.get("Principled BSDF")
    glow.node_tree.links.new(texture.outputs["Color"], shader.inputs["Emission Color"])
    shader.inputs["Emission Strength"].default_value = 0.58
    return glow


def export(name, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
        obj.data.calc_loop_triangles()
    bpy.context.view_layer.objects.active = objects[0]
    triangles = sum(len(obj.data.loop_triangles) for obj in objects)
    path = ART / f"{name}.fbx"
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', use_triangles=True, add_leaf_bones=False)
    return path, triangles, len(objects)


def preview(puddle_objects, fire_objects):
    for obj in puddle_objects:
        obj.location.x = -3.7
    for obj in fire_objects:
        obj.location.x = 3.7
    # Only the .blend and preview contain this display floor.
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.012))
    ground = bpy.context.object
    ground.name = "Preview floor (not exported)"
    floor = bpy.data.materials.new("Slate floor")
    floor.diffuse_color = (0.115, 0.13, 0.155, 1)
    ground.data.materials.append(floor)
    scene = kit.preview_setup()
    scene.view_settings.view_transform = 'AgX'
    scene.camera.location = (0, -11, 15)
    kit.aim(scene.camera, (0, 0, 0))
    scene.camera.data.type = 'ORTHO'
    scene.camera.data.ortho_scale = 16
    scene.render.resolution_x, scene.render.resolution_y = (1320, 720)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(PREVIEW / "SurfaceHazards.blend"))
    kit.render(scene, "preview", (0, -11, 15), (0, 0, 0), (1320, 720), ortho=16)


def verify(asset):
    path, expected, pieces = asset
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    assert len(meshes) == pieces
    points = []
    count = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        count += len(obj.data.loop_triangles)
        assert obj.data.uv_layers.active and len(obj.data.materials) == 1, obj.name
        points.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    assert count == expected, (count, expected)
    low, high = min(point.z for point in points), max(point.z for point in points)
    assert 0 <= low < 0.03 and high < 1.5, (low, high)
    print(f"SURFACE HAZARD PASS: {path.name}, {count} triangles, Z {low:.3f}..{high:.3f} m", flush=True)


def main():
    ART.mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    kit.ART, kit.PREVIEW, kit.COLORS = ART, PREVIEW, COLORS
    kit.SWATCHES = {name: ((i % 8 + 0.5)/8, (i // 8 + 0.5)/8) for i, name in enumerate(COLORS)}
    base = kit.palette_material("SurfaceHazards")
    glow = emissive(base)
    acid_objects = puddle(base, glow)
    fire_objects = fire(base, glow)
    assets = [export("AcidPuddle", acid_objects), export("GroundFire", fire_objects)]
    preview(acid_objects, fire_objects)
    for asset in assets:
        verify(asset)


if __name__ == "__main__":
    main()
