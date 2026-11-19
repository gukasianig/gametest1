"""Split the finished D3 creatures into more animatable parts without changing their shape.

Run: blender -b -P tools/blender/rig_cave_parts.py -- [01 02 ...] [--check DIR]

- Every Leg_* is cut at knee height into the upper part (keeps the name and the hip
  pivot) and a child Shin_* with its pivot at the knee. The parts overlap a little,
  so bending does not open a gap.
- Creatures listed in JAW get a Jaw child under Head: the lower slice of the head with
  a hinge at its back edge. The cut is capped with the mouth colour.
The source .blend files are already normalized, so the cut keeps exact sizes.
Outputs: updated .blend and .fbx in docs/art/EnemyConcepts/Living/D3-xx, the usual three
renders and rig-check.png (the same model with knees bent and jaw open).
"""

import math
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).parent))
from creature_mesh import render_model, save_model, validate, verify_fbx, world_vertices

LIVING = Path(__file__).resolve().parents[2] / "docs/art/EnemyConcepts/Living"

# id: (file name, framing for renders (target height, frame, yaw), jaw (fraction of the head height, mouth swatch) or None)
MODELS = {
    "01": ("CaveImp_D3-01", (.46, 1.78, 26), (.30, 10)),
    "02": ("CaveRunner_D3-02", (.36, 1.79, 58), (.36, 9)),
    "03": ("CaveBrute_D3-03", (.62, 2.28, 24), (.30, 10)),
    "04": ("CaveSpitter_D3-04", (.59, 1.80, 34), (.34, 12)),
    "05": ("CaveCharger_D3-05", (.56, 2.09, 34), (.34, 5)),
    "06": ("CaveBomber_D3-06", (.59, 1.77, 32), None),
    "07": ("CaveHealer_D3-07", (.29, 1.73, 32), None),
    "08": ("CaveSplitter_D3-08", (.40, 1.77, 24), None),
    "09": ("CaveBroodmother_D3-09", (.49, 2.03, 29), (.32, 5)),
    "10": ("CaveSniper_D3-10", (.44, 2.07, 48), None),
    "11": ("CaveMortar_D3-11", (.51, 1.89, 32), (.36, 5)),
    "12": ("CaveBlinkbat_D3-12", (.23, 1.92, 26), None),
    "13": ("CaveAmplifier_D3-13", (.66, 1.94, 26), None),
    "14": ("CaveArmored_D3-14", (.44, 1.94, 28), (.34, 5)),
    "15": ("CaveGuardian_D3-15", (.79, 3.68, 41), (.40, 0)),
}
KNEE = .45          # cut height as a fraction of the leg height
OVERLAP = .018      # metres each side of the cut


def cell_uv(index):
    return Vector(((index % 4 + .5) / 4, (index // 4 + .5) / 4))


def slice_mesh(source, keep_below, z, cap_index):
    """Copy of source mesh cut by the plane Z=z (object space); caps the cut."""
    bm = bmesh.new()
    bm.from_mesh(source.data)
    uv = bm.loops.layers.uv.active
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    result = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=(0, 0, z), plane_no=(0, 0, 1),
                                    clear_outer=keep_below, clear_inner=not keep_below)
    cut_edges = [e for e in result["geom_cut"] if isinstance(e, bmesh.types.BMEdge) and e.is_valid and e.is_boundary]
    filled = bmesh.ops.holes_fill(bm, edges=cut_edges, sides=0)["faces"]
    for face in filled:
        color = None
        if cap_index is None:
            for edge in face.edges:
                for other in edge.link_faces:
                    if other is not face and other not in filled:
                        color = other.loops[0][uv].uv.copy()
                        break
                if color is not None:
                    break
        for loop in face.loops:
            loop[uv].uv = color if color is not None else cell_uv(cap_index or 0)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    loop_points = [v.co.copy() for v in bm.verts if abs(v.co.z - z) < 1e-5]
    mesh = bpy.data.meshes.new(source.data.name + ("_low" if keep_below else "_up"))
    bm.to_mesh(mesh)
    bm.free()
    for mat in source.data.materials:
        mesh.materials.append(mat)
    return mesh, loop_points


def split(obj, z_world, child_name, cap_index, pivot_rule):
    """obj keeps the part above z, a new child gets the part below, pivot set by pivot_rule(points)."""
    z = z_world - obj.matrix_world.translation.z
    upper, _ = slice_mesh(obj, False, z - OVERLAP, None)
    lower, points = slice_mesh(obj, True, z + OVERLAP, cap_index)
    if not points:
        raise ValueError(obj.name + ": the cut missed the mesh")
    pivot = pivot_rule(points, z)
    lower.transform(Matrix.Translation(-pivot))
    old = obj.data
    obj.data = upper
    bpy.data.meshes.remove(old)
    child = bpy.data.objects.new(child_name, lower)
    bpy.context.collection.objects.link(child)
    child.parent = obj
    child.matrix_parent_inverse = Matrix.Identity(4)
    child.location = pivot
    return child


def centroid(points, z):
    c = sum(points, Vector()) / len(points)
    return Vector((c.x, c.y, z))


def jaw_hinge(points, z):
    # Hinge a third of the way from the back of the cut (the back of the head stays in the neck).
    lo, hi = min(p.y for p in points), max(p.y for p in points)
    return Vector((0, lo + (hi - lo) * .35, z))


def rig(number):
    name, framing, jaw = MODELS[number]
    folder = LIVING / ("D3-" + number)
    bpy.ops.wm.open_mainfile(filepath=str(folder / (name + ".blend")))
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if any(o.name.startswith(("Shin_", "Jaw")) for o in meshes):
        raise ValueError(name + " is already rigged")
    root = next(o for o in bpy.data.objects if o.type == 'EMPTY' and o.parent is None)
    added = []
    for leg in [o for o in meshes if o.name.startswith("Leg_")]:
        pts = world_vertices([leg])
        top = leg.matrix_world.translation.z
        bottom = min(p.z for p in pts)
        knee = bottom + (top - bottom) * KNEE
        added.append(split(leg, knee, "Shin_" + leg.name[4:], None, centroid))
    if jaw is not None:
        head = bpy.data.objects["Head"]
        zs = sorted(p.z for p in world_vertices([head]))
        low, high = zs[int(len(zs) * .05)], zs[int(len(zs) * .80)]
        added.append(split(head, low + (high - low) * jaw[0], "Jaw", jaw[1], jaw_hinge))

    parts = [o for o in bpy.data.objects if o.type == 'MESH']
    validate(parts)
    triangles = sum(len(f.vertices) - 2 for o in parts for f in o.data.polygons)
    points = world_vertices(parts)
    bounds = [round(max(v[i] for v in points) - min(v[i] for v in points), 4) for i in range(3)]
    save_model(root, parts, folder, name)
    verify_fbx(folder / (name + ".fbx"), len(parts), triangles, bounds)
    render_model(folder, *framing)

    # Pose check: knees and jaw bent, side and three-quarter views in one strip.
    for o in added:
        o.rotation_euler = (math.radians(-28 if o.name == "Jaw" else 35), 0, 0)
    scene = bpy.context.scene
    camera = scene.camera
    target = Vector((0, 0, framing[0]))
    yaw, elevation = math.radians(90), math.radians(8)
    camera.location = target + Vector((math.sin(yaw) * math.cos(elevation), math.cos(yaw) * math.cos(elevation), math.sin(elevation))) * 4
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(folder / "rig-check.png")
    bpy.ops.render.render(write_still=True)
    print(f"{name} RIG PASS parts={len(parts)} added={[o.name for o in added]} tris={triangles} bounds={bounds}", flush=True)


def main():
    bpy.context.preferences.view.use_translate_new_dataname = False
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    selected = [a for a in args if a in MODELS] or list(MODELS)
    failures = []
    for number in selected:
        try:
            rig(number)
        except Exception as error:
            import traceback
            traceback.print_exc()
            failures.append(number)
    if failures:
        raise RuntimeError("rig failed: " + ", ".join(failures))


if __name__ == '__main__':
    main()
