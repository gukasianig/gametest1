"""Фон главного меню: ночная индустриальная арена в стиле Мастерской (тёмно-синее звёздное небо, туман,
силуэты на горизонте, тёплый свет из ворот). Слева спокойная тёмная зона под заголовок и кнопки.

Запуск (Blender 4.5):
  blender -b --factory-startup -P tools/blender/make_menu_background.py -- <out.png> [width height samples]
По умолчанию 1920x1080, 48 сэмплов EEVEE. Положение ворот на кадре (доли, от левого нижнего угла)
записывается рядом в <out>.json — по нему код ставит пульсирующее свечение.
"""
import bpy, bmesh, math, random, sys, os, json
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

random.seed(11)
args = sys.argv[sys.argv.index("--") + 1:]
OUT = os.path.abspath(args[0])
RES_X = int(args[1]) if len(args) > 1 else 1920
RES_Y = int(args[2]) if len(args) > 2 else 1080
SAMPLES = int(args[3]) if len(args) > 3 else 48

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x, scene.render.resolution_y = RES_X, RES_Y
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGB"
scene.eevee.taa_render_samples = SAMPLES
scene.view_settings.view_transform = "AgX"
scene.view_settings.look = "AgX - Medium High Contrast"
try:
    scene.eevee.use_volumetric_shadows = True
    scene.eevee.volumetric_tile_size = "4"
    scene.eevee.volumetric_end = 400
except Exception:
    pass

ARENA = Vector((26.0, 95.0, 0.0))
RADIUS = 34.0


def mat(name, color, metal=0.0, rough=0.6, emit=None, power=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    if emit is not None:
        p.inputs["Emission Color"].default_value = (*emit, 1)
        p.inputs["Emission Strength"].default_value = power
    return m


CONCRETE = mat("concrete", (0.05, 0.06, 0.075), 0.2, 0.75)
STEEL = mat("steel", (0.06, 0.07, 0.08), 0.85, 0.45)
DARK = mat("dark", (0.015, 0.02, 0.028), 0.1, 0.9)
WARM = mat("warm", (1.0, 0.55, 0.2), 0.0, 0.5, emit=(1.0, 0.5, 0.15), power=18.0)
WARM_DIM = mat("warm_dim", (1.0, 0.55, 0.2), 0.0, 0.5, emit=(1.0, 0.45, 0.12), power=14.0)
RED = mat("red", (1, 0.1, 0.05), 0, 0.5, emit=(1.0, 0.08, 0.04), power=30.0)
TEAL = mat("teal", (0.2, 0.9, 0.8), 0, 0.5, emit=(0.15, 0.85, 0.75), power=8.0)


def box(name, loc, size, material, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=(0, 0, rot_z))
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    o.data.materials.append(material)
    return o


def cyl(name, loc, r, depth, material, verts=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(material)
    return o


# ---------- небо: градиент, звёзды, лёгкая туманность ----------
world = bpy.data.worlds.new("night")
scene.world = world
world.use_nodes = True
nt = world.node_tree
nodes, links = nt.nodes, nt.links
bg = nodes["Background"]
coord = nodes.new("ShaderNodeTexCoord")
sep = nodes.new("ShaderNodeSeparateXYZ")
links.new(coord.outputs["Generated"], sep.inputs[0])
grad = nodes.new("ShaderNodeValToRGB")
grad.color_ramp.elements[0].position = 0.0
grad.color_ramp.elements[0].color = (0.05, 0.1, 0.13, 1)
grad.color_ramp.elements[1].position = 0.45
grad.color_ramp.elements[1].color = (0.004, 0.009, 0.018, 1)
links.new(sep.outputs["Z"], grad.inputs[0])
stars = nodes.new("ShaderNodeTexVoronoi")
stars.inputs["Scale"].default_value = 260
links.new(coord.outputs["Generated"], stars.inputs["Vector"])
star_ramp = nodes.new("ShaderNodeValToRGB")
star_ramp.color_ramp.elements[0].position = 0.0
star_ramp.color_ramp.elements[0].color = (1, 1, 1, 1)
star_ramp.color_ramp.elements[1].position = 0.06
star_ramp.color_ramp.elements[1].color = (0, 0, 0, 1)
links.new(stars.outputs["Distance"], star_ramp.inputs[0])
twinkle = nodes.new("ShaderNodeTexNoise")
twinkle.inputs["Scale"].default_value = 60
links.new(coord.outputs["Generated"], twinkle.inputs["Vector"])
star_mul = nodes.new("ShaderNodeMath")
star_mul.operation = "MULTIPLY"
links.new(star_ramp.outputs["Color"], star_mul.inputs[0])
links.new(twinkle.outputs["Fac"], star_mul.inputs[1])
star_gain = nodes.new("ShaderNodeMath")
star_gain.operation = "MULTIPLY"
star_gain.inputs[1].default_value = 3.0
links.new(star_mul.outputs[0], star_gain.inputs[0])
nebula = nodes.new("ShaderNodeTexNoise")
nebula.inputs["Scale"].default_value = 3.0
nebula.inputs["Detail"].default_value = 8
links.new(coord.outputs["Generated"], nebula.inputs["Vector"])
neb_ramp = nodes.new("ShaderNodeValToRGB")
neb_ramp.color_ramp.elements[0].position = 0.55
neb_ramp.color_ramp.elements[0].color = (0, 0, 0, 1)
neb_ramp.color_ramp.elements[1].position = 0.8
neb_ramp.color_ramp.elements[1].color = (0.02, 0.07, 0.12, 1)
links.new(nebula.outputs["Fac"], neb_ramp.inputs[0])
add1 = nodes.new("ShaderNodeMix")
add1.data_type = "RGBA"
add1.blend_type = "ADD"
add1.inputs["Factor"].default_value = 1.0
links.new(grad.outputs["Color"], add1.inputs[6])
links.new(neb_ramp.outputs["Color"], add1.inputs[7])
add2 = nodes.new("ShaderNodeMix")
add2.data_type = "RGBA"
add2.blend_type = "ADD"
add2.inputs["Factor"].default_value = 1.0
links.new(add1.outputs[2], add2.inputs[6])
star_rgb = nodes.new("ShaderNodeCombineColor")
for i in range(3):
    links.new(star_gain.outputs[0], star_rgb.inputs[i])
# Звёзды только выше горизонта.
above = nodes.new("ShaderNodeMath")
above.operation = "GREATER_THAN"
above.inputs[1].default_value = 0.04
links.new(sep.outputs["Z"], above.inputs[0])
star_mask = nodes.new("ShaderNodeMix")
star_mask.data_type = "RGBA"
star_mask.blend_type = "MULTIPLY"
star_mask.inputs["Factor"].default_value = 1.0
links.new(star_rgb.outputs[0], star_mask.inputs[6])
links.new(above.outputs[0], star_mask.inputs[7])
links.new(star_mask.outputs[2], add2.inputs[7])
links.new(add2.outputs[2], bg.inputs["Color"])
bg.inputs["Strength"].default_value = 1.0

# ---------- туман ----------
fog_box = box("fog", (20, 90, 7), (220, 260, 14), DARK)
fog_box.data.materials.clear()
fog = bpy.data.materials.new("fog")
fog.use_nodes = True
fog.node_tree.nodes.remove(fog.node_tree.nodes["Principled BSDF"])
vol = fog.node_tree.nodes.new("ShaderNodeVolumePrincipled")
vol.inputs["Color"].default_value = (0.35, 0.5, 0.6, 1)
vol.inputs["Density"].default_value = 0.009
fog.node_tree.links.new(vol.outputs[0], fog.node_tree.nodes["Material Output"].inputs["Volume"])
fog_box.data.materials.append(fog)

# ---------- земля ----------
bpy.ops.mesh.primitive_plane_add(size=600, location=(0, 150, 0))
ground = bpy.context.active_object
ground_mat = mat("ground", (0.03, 0.035, 0.04), 0.3, 0.35)
gn = ground_mat.node_tree
noise = gn.nodes.new("ShaderNodeTexNoise")
noise.inputs["Scale"].default_value = 0.4
bump = gn.nodes.new("ShaderNodeBump")
bump.inputs["Strength"].default_value = 0.25
gn.links.new(noise.outputs["Fac"], bump.inputs["Height"])
gn.links.new(bump.outputs["Normal"], gn.nodes["Principled BSDF"].inputs["Normal"])
rough = gn.nodes.new("ShaderNodeMapRange")
rough.inputs["To Min"].default_value = 0.5
rough.inputs["To Max"].default_value = 0.9
gn.links.new(noise.outputs["Fac"], rough.inputs["Value"])
gn.links.new(rough.outputs["Result"], gn.nodes["Principled BSDF"].inputs["Roughness"])
ground.data.materials.append(ground_mat)

# ---------- арена: кольцо из пролётов с арками, два яруса, огни ----------
segments = 36
gate_angle = math.atan2(-ARENA.y, -ARENA.x) + math.radians(8)  # ворота смотрят на камеру
for i in range(segments):
    a = 2 * math.pi * i / segments
    pos = ARENA + Vector((math.cos(a), math.sin(a), 0)) * RADIUS
    tangent = a + math.pi / 2
    width = 2 * math.pi * RADIUS / segments
    diff = math.atan2(math.sin(a - gate_angle), math.cos(a - gate_angle))
    is_gate = abs(diff) < math.radians(7)
    # Опора между пролётами.
    pier = pos + Vector((math.cos(a + math.pi / segments), math.sin(a + math.pi / segments), 0)) * 0.0
    box("pier", (pos.x, pos.y, 9), (1.4, 3.2, 18), CONCRETE, tangent)
    box("buttress", (pos.x + math.cos(a) * 1.8, pos.y + math.sin(a) * 1.8, 5), (1.0, 2.2, 10), STEEL, tangent)
    mid = ARENA + Vector((math.cos(a + math.pi / segments), math.sin(a + math.pi / segments), 0)) * RADIUS
    if not is_gate:
        box("upper", (mid.x, mid.y, 15.5), (width, 2.6, 5), CONCRETE, tangent + math.pi / segments)
        box("lower_fill", (mid.x, mid.y, 2.2), (width * 0.98, 2.0, 4.4), DARK, tangent + math.pi / segments)
        # Тёплые окна-полосы на ярусах (не все горят).
        if random.random() < 0.75:
            box("window", (mid.x + math.cos(a) * 1.35, mid.y + math.sin(a) * 1.35, 15.2), (width * 0.55, 0.1, 0.45),
                WARM_DIM, tangent + math.pi / segments)
        if random.random() < 0.4:
            box("window2", (mid.x + math.cos(a) * 1.05, mid.y + math.sin(a) * 1.05, 7.5), (width * 0.3, 0.1, 0.8),
                WARM_DIM, tangent + math.pi / segments)
    box("rim", (pos.x, pos.y, 18.4), (width * 1.05, 3.6, 0.8), STEEL, tangent)

# Внутренняя стена: в проёмах темнота с тёплым отсветом, а не пересвеченное «стекло».
for i in range(segments):
    a = 2 * math.pi * (i + 0.5) / segments
    pos = ARENA + Vector((math.cos(a), math.sin(a), 0)) * (RADIUS - 5)
    box("inner_wall", (pos.x, pos.y, 8), (2 * math.pi * (RADIUS - 5) / segments * 1.05, 1.0, 16), DARK, a + math.pi / 2)
# Огни по верхнему ободу и прожекторы в небо.
for i in range(0, segments, 2):
    a = 2 * math.pi * i / segments
    pos = ARENA + Vector((math.cos(a), math.sin(a), 0)) * RADIUS
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.28, location=(pos.x, pos.y, 19.1))
    bpy.context.active_object.data.materials.append(WARM)
for k, (ang, tilt) in enumerate(((150, 12), (200, -10), (250, 8))):
    a = math.radians(ang)
    pos = ARENA + Vector((math.cos(a), math.sin(a), 0)) * (RADIUS - 2)
    beam = bpy.data.lights.new("beam%d" % k, "SPOT")
    beam.energy = 0
    beam.spot_size = math.radians(6)
    beam.spot_blend = 0.4
    beam.color = (0.75, 0.9, 1.0)
    bo = bpy.data.objects.new("beam%d" % k, beam)
    bo.location = (pos.x, pos.y, 19.5)
    bo.rotation_euler = (math.radians(tilt), math.radians(-tilt * 0.6), 0)
    scene.collection.objects.link(bo)

# Ворота: рама, свет изнутри, полоса огней.
g = ARENA + Vector((math.cos(gate_angle), math.sin(gate_angle), 0)) * (RADIUS + 1.5)
face = gate_angle + math.pi / 2
for side in (-1, 1):
    off = Vector((math.cos(face), math.sin(face), 0)) * side * 5.2
    box("gate_post", (g.x + off.x, g.y + off.y, 7), (2.2, 3.4, 14), STEEL, face)
    box("gate_light", (g.x + off.x * 0.86, g.y + off.y * 0.86, 6), (0.35, 3.5, 11), WARM, face)
box("gate_top", (g.x, g.y, 14.5), (13, 3.6, 3), STEEL, face)
box("gate_top_light", (g.x - math.cos(gate_angle) * 1.85, g.y - math.sin(gate_angle) * 1.85, 13.3), (8.5, 0.15, 0.5), WARM, face)
glow = box("gate_glow", (g.x - math.cos(gate_angle) * 2.5, g.y - math.sin(gate_angle) * 2.5, 6.5), (8.6, 0.2, 13), WARM, face)
light = bpy.data.lights.new("gate", "AREA")
light.energy = 60000
light.size = 9
light.color = (1.0, 0.55, 0.25)
lo = bpy.data.objects.new("gate_light_src", light)
lo.location = (g.x - math.cos(gate_angle) * 4, g.y - math.sin(gate_angle) * 4, 6)
lo.rotation_euler = (math.radians(90), 0, gate_angle - math.pi / 2)
scene.collection.objects.link(lo)
inner = bpy.data.lights.new("inner", "POINT")
inner.energy = 50000
inner.color = (1.0, 0.6, 0.3)
inner.shadow_soft_size = 10
io = bpy.data.objects.new("inner", inner)
io.location = (ARENA.x, ARENA.y, 14)
scene.collection.objects.link(io)

# Дорога к воротам с огнями по краям.
road_dir = Vector((-math.cos(gate_angle), -math.sin(gate_angle), 0))
for k in range(16):
    p = g + road_dir * (6 + k * 7)
    for side in (-1, 1):
        off = Vector((math.cos(face), math.sin(face), 0)) * side * 5.5
        cyl("bollard", (p.x + off.x, p.y + off.y, 0.5), 0.25, 1.0, STEEL, 8)
        cyl("bollard_light", (p.x + off.x, p.y + off.y, 1.1), 0.35, 0.25, WARM, 8)

# ---------- башни с красными огнями и провода ----------
towers = [Vector((ARENA.x + 46, ARENA.y + 10, 0)), Vector((ARENA.x - 30, ARENA.y + 34, 0)), Vector((ARENA.x + 12, ARENA.y - 52, 0))]
heights = [42, 34, 26]
for t, hgt in zip(towers, heights):
    for dx, dy in ((-1.2, -1.2), (1.2, -1.2), (-1.2, 1.2), (1.2, 1.2)):
        box("leg", (t.x + dx, t.y + dy, hgt / 2), (0.35, 0.35, hgt), STEEL)
    for z in range(4, int(hgt), 5):
        box("ring", (t.x, t.y, z), (2.9, 2.9, 0.3), STEEL)
    box("cab", (t.x, t.y, hgt - 2), (3.6, 3.6, 3.0), CONCRETE)
    box("cab_win", (t.x, t.y - 1.81, hgt - 2), (2.6, 0.05, 0.6), TEAL)
    cyl("mast", (t.x, t.y, hgt + 2.5), 0.12, 5, STEEL, 6)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.45, location=(t.x, t.y, hgt + 5.2))
    bpy.context.active_object.data.materials.append(RED)


def cable(a, b, sag):
    curve = bpy.data.curves.new("cable", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = 0.06
    sp = curve.splines.new("POLY")
    pts = 16
    sp.points.add(pts - 1)
    for i in range(pts):
        t = i / (pts - 1)
        p = a.lerp(b, t)
        p.z -= sag * 4 * t * (1 - t)
        sp.points[i].co = (p.x, p.y, p.z, 1)
    o = bpy.data.objects.new("cable", curve)
    o.data.materials.append(STEEL)
    scene.collection.objects.link(o)


cable(Vector((towers[0].x, towers[0].y, heights[0] - 4)), Vector((ARENA.x + 30, ARENA.y + 10, 18.5)), 5)
cable(Vector((towers[1].x, towers[1].y, heights[1] - 4)), Vector((ARENA.x - 22, ARENA.y + 26, 18.5)), 4)
cable(Vector((towers[0].x, towers[0].y, heights[0] - 6)), Vector((towers[2].x, towers[2].y, heights[2] - 3)), 9)

# ---------- дальний план: эстакада, горы ----------
for i in range(14):
    x = -120 + i * 16
    box("aqueduct_pier", (x, 230, 7), (2.5, 3, 14), DARK)
    box("aqueduct_deck", (x + 8, 230, 14.5), (16, 3.5, 1.5), DARK)
# Горы: два плавных слоя (ближний темнее), без острых зубцов.
for layer, (dist, base, amp, shade) in enumerate(((420, 10, 26, (0.02, 0.035, 0.05)), (330, 6, 16, (0.012, 0.02, 0.03)))):
    mtn = bpy.data.meshes.new("mountains%d" % layer)
    bm = bmesh.new()
    prev = None
    for i in range(160):
        x = -420 + i * 5.5
        h = base + amp * (0.5 + 0.5 * math.sin(i * 0.11 + layer * 2) * math.sin(i * 0.047 + 1.3)) + 2.5 * math.sin(i * 0.6)
        v0 = bm.verts.new((x, dist, -1))
        v1 = bm.verts.new((x, dist, h))
        if prev:
            bm.faces.new((prev[0], v0, v1, prev[1]))
        prev = (v0, v1)
    bm.to_mesh(mtn)
    bm.free()
    mo = bpy.data.objects.new("mountains%d" % layer, mtn)
    mo.data.materials.append(mat("mtn%d" % layer, shade, 0.0, 0.9))
    scene.collection.objects.link(mo)

# ---------- передний план: камни по краям ----------
for i in range(14):
    side = -1 if i < 9 else 1
    x = (-34 - random.random() * 16) if side < 0 else (30 + random.random() * 10)
    y = 6 + random.random() * 22
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.8 + random.random() * 1.6, location=(x, y + 10, 0))
    rock = bpy.context.active_object
    rock.scale = (1 + random.random(), 1 + random.random() * 0.6, 0.5 + random.random() * 0.5)
    rock.rotation_euler = (random.random(), random.random(), random.random())
    rock.data.materials.append(DARK)
    d = rock.modifiers.new("d", "DISPLACE")
    tex = bpy.data.textures.new("rock%d" % i, "CLOUDS")
    tex.noise_scale = 0.8
    d.texture = tex
    d.strength = 0.8

# ---------- свет: лунный контровой и слабое заполнение ----------
moon = bpy.data.objects.new("moon", bpy.data.lights.new("moon", "SUN"))
moon.data.energy = 0.7
moon.data.color = (0.55, 0.7, 1.0)
moon.rotation_euler = (math.radians(70), 0, math.radians(200))
scene.collection.objects.link(moon)

# ---------- камера ----------
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.lens = 26
cam.location = (-12, -34, 3.0)
target = Vector((ARENA.x - 22, ARENA.y, 19))
direction = target - cam.location
cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
scene.collection.objects.link(cam)
scene.camera = cam

# ---------- композитинг: мягкое свечение огней ----------
scene.use_nodes = True
tree = scene.node_tree
for n in list(tree.nodes):
    tree.nodes.remove(n)
rl = tree.nodes.new("CompositorNodeRLayers")
comp = tree.nodes.new("CompositorNodeComposite")
try:
    glare = tree.nodes.new("CompositorNodeGlare")
    glare.glare_type = "BLOOM"
    try:
        glare.threshold = 0.8
        glare.size = 8
    except Exception:
        glare.inputs["Threshold"].default_value = 0.8
    tree.links.new(rl.outputs["Image"], glare.inputs["Image"])
    tree.links.new(glare.outputs["Image"], comp.inputs["Image"])
except Exception as e:
    print("glare skipped:", e)
    tree.links.new(rl.outputs["Image"], comp.inputs["Image"])

scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)

# Положение ворот на кадре — для пульсирующего свечения в Unity.
bpy.context.view_layer.update()
gate_view = world_to_camera_view(scene, cam, Vector((g.x, g.y, 7)))
with open(os.path.splitext(OUT)[0] + ".json", "w", encoding="utf-8") as fh:
    json.dump({"gate": [round(gate_view.x, 4), round(gate_view.y, 4)]}, fh)
print("gate on screen:", gate_view)
