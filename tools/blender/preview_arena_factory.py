"""Превью арены: строит модель (make_arena_factory.py) и рендерит три вида в docs/art/Arena/ (Workbench)."""
import math
import runpy
from pathlib import Path
import bpy
from mathutils import Vector

runpy.run_path(str(Path(__file__).parent / "make_arena_factory.py"))
OUT = Path(__file__).resolve().parents[2] / "docs/art/Arena"
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE'
scene.display.shading.show_shadows = True
scene.render.resolution_x, scene.render.resolution_y = 1280, 720


def shot(name, loc, target, lens=35):
    cam_data = bpy.data.cameras.new(name)
    cam_data.lens = lens
    cam_data.clip_end = 2000
    cam = bpy.data.objects.new(name, cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    scene.render.filepath = str(OUT / ("Arena_Factory_%s.png" % name))
    bpy.ops.render.render(write_still=True)

# Blender: Z вверх, мир Unity (x, y, z) = Blender (x, z, -y)
shot("overview", (0, -330, 230), (0, 0, 0), 30)
shot("corner", (80, -70, 30), (95, 95, 5), 28)
shot("game", (0, -45, 35), (0, 10, 0), 40)
