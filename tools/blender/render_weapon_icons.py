"""Иконки оружия для экрана выбора и Мастерской из FBX, собранных make_hero_weapons.py / make_flamethrower.py /
make_rocket_launcher.py: 384×384, прозрачный фон, ствол вправо, вид три четверти сверху (как WeaponIconBaker).
Запуск: blender -b -P tools/blender/render_weapon_icons.py
Результат: docs/art/Weapons/Icons/<ключ ModelSkinSet>.png — копируются в Assets/Resources/UI/Weapons/ (meta сохранить)."""

import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs/art/Weapons/Icons"
OUT.mkdir(parents=True, exist_ok=True)
KEYS = {
    "Pistol": "Weapon0", "Rifle": "Weapon1", "Shotgun": "Weapon2", "HeavyRifle": "Weapon3",
    "SawLauncher": "Kind_Boomerang", "RocketLauncher": "Kind_Rocket", "ArcBlaster": "Kind_Arc",
    "Flamethrower": "Kind_Flame", "IceCannon": "Kind_Ice", "Railgun": "Kind_Rail",
}


def light(name, kind, rotation, energy, color):
    data = bpy.data.lights.new(name, kind)
    data.energy = energy
    data.color = color
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.rotation_euler = [math.radians(a) for a in rotation]


for name, key in KEYS.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT / "docs/art/Weapons" / name / (name + ".fbx")))
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    points = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    center = sum(points, Vector()) / len(points)
    lo = Vector([min(p[i] for p in points) for i in range(3)])
    hi = Vector([max(p[i] for p in points) for i in range(3)])
    center = (lo + hi) / 2

    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = 384
    scene.render.film_transparent = True
    scene.view_settings.view_transform = 'Standard'
    scene.world = bpy.data.worlds.new("Icon World")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.75, .78, .85, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .55
    light("Key", 'SUN', (40, 0, 150), 3.2, (1, .95, .9))
    light("Fill", 'SUN', (60, 0, -40), 1.2, (.8, .85, 1))

    # Ствол (+Y) вправо и немного к зрителю, взгляд сверху.
    yaw, elevation = math.radians(28), math.radians(24)
    direction = Vector((math.cos(yaw) * math.cos(elevation), math.sin(yaw) * math.cos(elevation), math.sin(elevation)))
    cam_data = bpy.data.cameras.new("Icon Camera")
    cam_data.type = 'ORTHO'
    camera = bpy.data.objects.new("Icon Camera", cam_data)
    bpy.context.collection.objects.link(camera)
    camera.location = center + direction * 5
    camera.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.view_layer.update()
    right = camera.matrix_world.to_3x3() @ Vector((1, 0, 0))
    up = camera.matrix_world.to_3x3() @ Vector((0, 1, 0))
    xs = [(p - center).dot(right) for p in points]
    ys = [(p - center).dot(up) for p in points]
    cam_data.ortho_scale = max(max(xs) - min(xs), max(ys) - min(ys)) * 1.12
    camera.location += right * ((max(xs) + min(xs)) / 2) + up * ((max(ys) + min(ys)) / 2)
    scene.camera = camera
    scene.render.filepath = str(OUT / (key + ".png"))
    bpy.ops.render.render(write_still=True)
    print("ICON", name, key, flush=True)
