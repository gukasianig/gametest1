"""Перестройка формы камней зелёной арены (08.11.2026).

Генератор Codex (arena_pack_nature.rock_asset -> Mesh.lump/loft) строит камень стопкой колец с отдельной
крышкой другого цвета: сверху видны радиальные швы и круглое пятно на макушке. Скрипт берёт ГОТОВЫЕ FBX
и каждую глыбу (связную часть) перестраивает:
  - выпуклая оболочка по случайным точкам с её же поверхности (+ исходное основание) — неправильные грани без колец;
  - вытягивание в случайном горизонтальном направлении (не круглая в плане);
  - 1–3 среза плоскостями: наклонная «макушка» и сколы сбоку;
  - цвет граней по наклону: бока — исходный цвет боков, верх — смесь камня/лишайника (мшистые — мох/листва) пятнами.
Габарит, опора (низ на z=0, начало координат) и материал сохраняются.

Запуск:  blender -b --python tools/blender/reshape_arena_rocks.py -- <папка rocks> [<папка оригиналов для копии>] [--preview <png>]
"""
import math
import os
import random
import shutil
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from arena_pack_mesh import INDEX  # noqa: E402  (порядок ячеек атласа)

PREFIXES = ('rock_small_', 'rock_medium_', 'rock_mossy_medium_', 'rock_large_', 'rock_cluster_', 'rock_pebble_set_')


def cell_of(uv):
    return int(min(7, max(0, math.floor(uv.y * 8)))) * 8 + int(min(7, max(0, math.floor(uv.x * 8))))


def name_of(index):
    for n, i in INDEX.items():
        if i == index:
            return n
    return 'stone_dark'


def surface_points(bm, faces, count, rng):
    areas = [f.calc_area() for f in faces]
    total = sum(areas)
    pts = []
    for _ in range(count):
        r = rng.uniform(0, total)
        acc = 0
        for f, a in zip(faces, areas):
            acc += a
            if acc >= r:
                break
        v = [l.vert.co for l in f.loops]
        # случайная точка в треугольнике (грань лофта может быть 4-угольной — берём первый треугольник веера)
        k = rng.randrange(1, len(v) - 1)
        a, b = rng.random(), rng.random()
        if a + b > 1:
            a, b = 1 - a, 1 - b
        pts.append(v[0] + (v[k] - v[0]) * a + (v[k + 1] - v[0]) * b)
    return pts


def reshape_part(src_faces, side_color, top_color, mossy, rng, pebble):
    verts = {v for f in src_faces for v in f.verts}
    lo = Vector((min(v.co.x for v in verts), min(v.co.y for v in verts), min(v.co.z for v in verts)))
    hi = Vector((max(v.co.x for v in verts), max(v.co.y for v in verts), max(v.co.z for v in verts)))
    size = hi - lo
    centre = (lo + hi) / 2
    h = max(size.z, 1e-4)
    count = 10 if pebble else 16 if max(size.x, size.y) < 0.8 else 26
    pts = surface_points(None, [f for f in src_faces if f.normal.z > -0.5], count, rng)
    # основание: исходные нижние вершины (опора остаётся на месте)
    base = [v.co.copy() for v in verts if v.co.z < lo.z + 0.03 * h]
    pts += base
    # шум по радиусу и вытягивание в случайном горизонтальном направлении
    ang = rng.uniform(0, math.tau)
    d = Vector((math.cos(ang), math.sin(ang), 0))
    p = Vector((-d.y, d.x, 0))
    stretch, squeeze = rng.uniform(1.15, 1.45), rng.uniform(0.82, 0.95)
    out = []
    for q in pts:
        rel = q - Vector((centre.x, centre.y, 0))
        a, b = rel.dot(d), rel.dot(p)
        if q.z > lo.z + 0.03 * h:
            j = rng.uniform(0.9, 1.1)
            a, b = a * j, b * j
        out.append(Vector((centre.x, centre.y, 0)) + d * a * stretch + p * b * squeeze + Vector((0, 0, q.z)))
    bm = bmesh.new()
    for q in out:
        bm.verts.new(q)
    res = bmesh.ops.convex_hull(bm, input=list(bm.verts))
    drop = {g for g in res['geom_interior'] + res['geom_unused'] if isinstance(g, bmesh.types.BMVert)}
    bmesh.ops.delete(bm, geom=list(drop), context='VERTS')
    # срезы: наклонная макушка и 0–2 скола сбоку
    cuts = []
    tilt = math.radians(rng.uniform(14, 34))
    ta = rng.uniform(0, math.tau)
    n_top = Vector((math.sin(tilt) * math.cos(ta), math.sin(tilt) * math.sin(ta), math.cos(tilt)))
    cuts.append((Vector((centre.x, centre.y, lo.z + h * rng.uniform(0.78, 0.9))), n_top))
    for _ in range(0 if pebble else rng.randint(0, 2)):
        sa = rng.uniform(0, math.tau)
        n = Vector((math.cos(sa), math.sin(sa), rng.uniform(-0.1, 0.35))).normalized()
        reach = 0.5 * max(size.x * stretch, size.y) * rng.uniform(0.62, 0.8)
        cuts.append((Vector((centre.x, centre.y, lo.z + h * 0.5)) + Vector((n.x, n.y, 0)) * reach, n))
    for co, no in cuts:
        geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, clear_outer=True)
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)   # срез — плоская грань
    # срез макушки ниже исходной высоты: вернуть высоту глыбы (наклонная грань своей верхней кромкой выходит к прежнему верху)
    top = max(v.co.z for v in bm.verts)
    if top - lo.z > 1e-4:
        k = h / (top - lo.z)
        for v in bm.verts:
            v.co.z = lo.z + (v.co.z - lo.z) * k
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    # цвета граней
    colors = []
    for f in bm.faces:
        nz = f.normal.z
        if nz < -0.5:
            colors.append(side_color)
        elif nz > 0.62:
            if mossy:
                colors.append('moss' if rng.random() < 0.7 else 'leaf')
            else:
                colors.append(rng.choice(['stone', 'stone', 'lichen', 'stone_light']))
        elif nz > 0.3 and mossy and rng.random() < 0.45:
            colors.append('moss')
        else:
            colors.append(rng.choice([side_color, side_color, 'stone'] if not mossy else [side_color, side_color, 'stone']))
    return bm, colors


def write_uv(mesh, colors):
    uv = mesh.uv_layers.active or mesh.uv_layers.new(name='UVMap')
    for poly, color in zip(mesh.polygons, colors):
        cell = INDEX[color]
        axis = max(range(3), key=lambda k: abs(poly.normal[k]))
        a, b = [(1, 2), (0, 2), (0, 1)][axis]
        coords = [mesh.vertices[i].co for i in poly.vertices]
        lo = [min(p[k] for p in coords) for k in (a, b)]
        hi = [max(p[k] for p in coords) for k in (a, b)]
        span = max(hi[0] - lo[0], hi[1] - lo[1], 1e-6)
        sx, sy = .05 * ((poly.index * 7) % 3), .05 * ((poly.index * 13) % 3)
        for loop, p in zip(poly.loop_indices, coords):
            uv.data[loop].uv = ((cell % 8 + .08 + sx + .72 * (p[a] - lo[0]) / span) / 8,
                                (cell // 8 + .08 + sy + .72 * (p[b] - lo[1]) / span) / 8)


def process(src_path, path, seed):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src_path)
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(objs) == 1, (path, [o.name for o in objs])
    obj = objs[0]
    # всё в мировые координаты (Blender-импорт уже Z-вверх), трансформы применить
    for o in bpy.context.scene.objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o is not obj:
            bpy.data.objects.remove(o)
    mesh = obj.data
    name = os.path.splitext(os.path.basename(path))[0]
    rng = random.Random(seed)
    src = bmesh.new()
    src.from_mesh(mesh)
    src.faces.ensure_lookup_table()
    uv_layer = src.loops.layers.uv.active
    # цвет боков = самый частый цвет боковых граней исходника (проверка соответствия атласу)
    tally = {}
    for f in src.faces:
        if abs(f.normal.z) < 0.5:
            c = cell_of(sum((l[uv_layer].uv for l in f.loops), Vector((0, 0))) / len(f.loops))
            tally[c] = tally.get(c, 0) + f.calc_area()
    side = name_of(max(tally, key=tally.get))
    if side not in ('stone_dark', 'stone', 'gray', 'shale', 'stone_light', 'lichen'):
        print('WARN side color', name, side); side = 'stone_dark'
    # связные части
    parts, seen = [], set()
    for f in src.faces:
        if f.index in seen:
            continue
        stack, part = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            part.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        parts.append(part)
    before = (Vector([min(v.co[i] for v in src.verts) for i in range(3)]), Vector([max(v.co[i] for v in src.verts) for i in range(3)]))
    out = bmesh.new()
    all_colors = []
    for part in parts:
        bm, colors = reshape_part(part, side, None, 'mossy' in name, rng, 'pebble' in name)
        tmp = bpy.data.meshes.new('tmp')
        bm.to_mesh(tmp)
        bm.free()
        out.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
        all_colors += colors
    src.free()
    out.to_mesh(mesh)
    out.free()
    mesh.update()
    for p in mesh.polygons:
        p.use_smooth = False
        p.material_index = 0
    write_uv(mesh, all_colors)
    assert not mesh.validate(), name
    after = (Vector([min(v.co[i] for v in mesh.vertices) for i in range(3)]), Vector([max(v.co[i] for v in mesh.vertices) for i in range(3)]))
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, use_triangles=True,
                             mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='STRIP')
    # повторный импорт: проверка
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    pts = [o.matrix_world @ v.co for o in bpy.context.scene.objects if o.type == 'MESH' for v in o.data.vertices]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    tris = sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type == 'MESH')
    ok = abs(lo.z) < 0.01 and (hi - lo).z > 0.5 * (before[1] - before[0]).z
    print('RESHAPE %-26s parts=%d side=%s tris=%d before=%.2fx%.2fx%.2f after=%.2fx%.2fx%.2f %s' % (
        name, len(parts), side, tris, *(before[1] - before[0]), *(hi - lo), 'PASS' if ok else 'FAIL'))
    return ok


def preview(folder, png):
    """Лист превью: камни в ряд, вид сверху-сбоку, плоское затенение с цветом из атласа отключено (форма)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    names = ['rock_small_03', 'rock_medium_02', 'rock_medium_05', 'rock_mossy_medium_02', 'rock_large_01', 'rock_large_04', 'rock_cluster_02', 'rock_pebble_set_03']
    x = 0.0
    for n in names:
        bpy.ops.import_scene.fbx(filepath=os.path.join(folder, n + '.fbx'))
        objs = [o for o in bpy.context.selected_objects if o.type == 'MESH']
        for o in bpy.context.selected_objects:
            if o.parent is None:
                o.location.x += x
        w = max((o.dimensions.x for o in objs), default=1)
        x += max(w, 1.0) * 1.3 + 1.0
    scn = bpy.context.scene
    scn.render.engine = 'BLENDER_WORKBENCH'
    scn.display.shading.light = 'STUDIO'
    scn.display.shading.color_type = 'MATERIAL'
    scn.display.shading.show_cavity = True
    scn.render.resolution_x, scn.render.resolution_y = 1800, 700
    for label, rot, loc in (('top', (math.radians(20), 0, 0), (x / 2, -8, 26)), ('side', (math.radians(70), 0, 0), (x / 2, -26, 9))):
        cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
        scn.collection.objects.link(cam)
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = x + 2
        cam.location = loc
        cam.rotation_euler = rot
        scn.camera = cam
        scn.render.filepath = png.replace('.png', '-' + label + '.png')
        bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    if '--preview' in argv:
        i = argv.index('--preview')
        preview(argv[0], argv[i + 1])
        return
    folder = argv[0]
    backup = argv[1] if len(argv) > 1 else None
    files = sorted(f for f in os.listdir(folder) if f.endswith('.fbx') and f.startswith(PREFIXES))
    fails = 0
    for i, f in enumerate(files):
        path = os.path.join(folder, f)
        if backup:
            os.makedirs(backup, exist_ok=True)
            keep = os.path.join(backup, f)
            if not os.path.exists(keep):
                shutil.copy2(path, keep)
        src = keep if backup else path   # всегда из оригинала Codex: повторный запуск не «перестраивает перестроенное»
        fails += 0 if process(src, path, 9100 + i * 17) else 1
    print('RESHAPE DONE files=%d fails=%d' % (len(files), fails))


main()
