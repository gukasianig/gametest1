"""Render a short, looping source-animation preview without modifying the exported assets."""
from pathlib import Path
import bpy

root = Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'docs/art/HeroOperator/Blender/Operator.blend'))
scene = bpy.context.scene
rig = next(obj for obj in bpy.data.objects if obj.type == 'ARMATURE')
rig.animation_data.action = bpy.data.actions['Run']
scene.camera = bpy.data.objects['Portrait']
scene.render.engine = 'CYCLES'
scene.cycles.samples = 4
scene.cycles.use_denoising = True
scene.render.threads_mode = 'FIXED'
scene.render.threads = 8
scene.render.resolution_x, scene.render.resolution_y = 480, 572
scene.render.resolution_percentage = 100
folder = root/'Logs/operator-v3-run'
folder.mkdir(parents=True, exist_ok=True)
for i in range(16):
    at = 1+38*i/16
    scene.frame_set(int(at), subframe=at-int(at))
    scene.render.filepath = str(folder/f'frame-{i:02}.png')
    bpy.ops.render.render(write_still=True)
print('PREVIEW_FRAMES_COMPLETE', flush=True)
