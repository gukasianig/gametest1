"""Check delivered FBX socket geometry, not only generator metadata."""
import json
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

repo=Path(__file__).resolve().parents[2]
root=repo/'_incoming/arena-assets'
records=json.loads((root/'manifest.json').read_text())['models']
report=[]
for record in records:
    if record['name'] not in {'river_straight_20','river_curve_45','river_curve_90','river_fork_Y'}:
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/record['file']))
    obj=next(o for o in bpy.context.selected_objects if o.type=='MESH')
    obj.data.calc_loop_triangles()
    vertices=[obj.matrix_world @ v.co for v in obj.data.vertices]
    triangles=list(obj.data.loop_triangles)
    tree=BVHTree.FromPolygons(vertices,[tuple(t.vertices) for t in triangles],all_triangles=True)
    rays=0
    for port in record['connectors_blender']:
        center=Vector(port['position'])
        direction=Vector(port['outward'])
        across=Vector((-direction.y,direction.x,0))
        for offset in (-2.7,0,2.7):
            for side in (-1,1):
                point=center+across*offset+direction*.04*side+Vector((0,0,3))
                hit,normal,index,distance=tree.ray_cast(point,Vector((0,0,-1)),6)
                if side==1:
                    assert hit is None,(record['name'],'blocked outlet',offset,hit)
                else:
                    assert hit is not None and abs(hit.z-.065)<.001,(record['name'],'water gap/height',offset,hit)
                    poly=obj.data.polygons[triangles[index].polygon_index]
                    assert obj.data.materials[poly.material_index].name.startswith('ArenaGreen_Water')
                rays+=1
        # Every recorded profile vertex must lie on the actual exported socket.
        for point in port['profile']:
            hit,normal,index,distance=tree.find_nearest(Vector(point))
            assert distance<.001,(record['name'],'profile/export mismatch',point,distance)
    report.append(dict(name=record['name'],sockets=len(record['connectors_blender']),rays=rays,status='PASS'))
(repo/'Logs/green-arena-sockets.json').write_text(json.dumps(report,indent=2))
print('SOCKET FBX PASS',json.dumps(report),flush=True)
