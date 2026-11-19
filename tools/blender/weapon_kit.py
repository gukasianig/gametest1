"""Общие детали для оружия героя в стиле Kenney Blaster Kit (make_flamethrower.py, make_rocket_launcher.py):
скошенные сечения, круги, раскраска граней, ориентация как у моделей Kenney и общий финал
(рукоять в начало координат, маркеры GripPoint/Muzzle, проверка, FBX, рендеры, VERIFICATION.json)."""

import json
import math
from pathlib import Path

import bpy
from mathutils import Vector

import kit
from creature_mesh import Part, render_model, save_model, validate, verify_fbx, world_vertices

ROOT = Path(__file__).resolve().parents[2]


def chamfer_ring(y, z, width, height, cut, x=0.0):
    w, h = width / 2, height / 2
    return [(x + a, y, z + b) for a, b in ((-w + cut, -h), (w - cut, -h), (w, -h + cut), (w, h - cut),
                                           (w - cut, h), (-w + cut, h), (-w, h - cut), (-w, -h + cut))]


def case(part, sections, color):
    part.rings([chamfer_ring(*section) for section in sections], color)


def circle(y, z, rx, rz, sides=12, x=0.0):
    return [(x + rx * math.cos((i + .5) * math.tau / sides), y, z + rz * math.sin((i + .5) * math.tau / sides)) for i in range(sides)]


# Грани chamfer_ring: 0 низ, 1 и 7 нижние скосы, 2 и 6 бока, 3 и 5 верхние скосы, 4 верх.
def shell_color(side, top, bevel, bottom):
    return lambda row, face: top if face == 4 else bevel if face in (3, 5) else side if face in (2, 6) else bottom


def kenney_forward_sign():
    """+1, если у моделей Kenney ствол смотрит в +Y Blender, иначе -1 (рукоять — самые нижние вершины, она сзади)."""
    kit.clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(ROOT / "Assets/Art/Kenney/BlasterKit/Models/blaster-l.fbx"))
    points = [o.matrix_world @ v.co for o in bpy.data.objects if o.type == 'MESH' for v in o.data.vertices]
    low = min(p.z for p in points)
    grip = [p.y for p in points if p.z < low + 0.04]
    center = (min(p.y for p in points) + max(p.y for p in points)) * 0.5
    return -1 if sum(grip) / len(grip) > center else 1


def make_atlas(swatches, output_dir, name):
    """Палитра 4×4 ячейки по 32 px, как creature_mesh.make_atlas, но каждая ячейка с мягким градиентом:
    низ темнее (×0,8), верх светлее (×1,12). shade_uvs раскладывает высоту модели по этому градиенту."""
    image = bpy.data.images.new(name + "_Palette", width=128, height=128, alpha=False)
    colors = [tuple(int(value[i:i + 2], 16) / 255 for i in (0, 2, 4)) for value in swatches]
    pixels = []
    for y in range(128):
        k = .8 + .32 * ((y % 32) / 31)
        for x in range(128):
            c = colors[(y // 32) * 4 + x // 32]
            pixels.extend((min(1, c[0] * k), min(1, c[1] * k), min(1, c[2] * k), 1.0))
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(output_dir / (name + "_Palette.png"))
    image.file_format = 'PNG'
    image.save()
    image.pack()
    material = bpy.data.materials.new(name + " Palette")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    shader = nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = .7
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = 'Linear'
    material.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
    nodes.active = texture
    return material


def shade_uvs(parts):
    """Вертикальное положение UV внутри своей ячейки = высота вершины в модели (верх светлее)."""
    zs = [v.co.z for obj in parts for v in obj.data.vertices]
    low, high = min(zs), max(zs)
    span = max(1e-6, high - low)
    for obj in parts:
        mesh = obj.data
        uv = mesh.uv_layers.active
        for poly in mesh.polygons:
            for loop in poly.loop_indices:
                row = min(3, int(uv.data[loop].uv[1] * 4))
                t = (mesh.vertices[mesh.loops[loop].vertex_index].co.z - low) / span
                uv.data[loop].uv[1] = (row + .12 + .76 * t) / 4


BODY_NAMES = ("Body", "Receiver", "Frame")


def add_bolts(root, parts, color=2):
    """По два болта с каждого бока основного корпуса (первая часть с именем Body/Receiver/Frame)."""
    body = next((obj for obj in parts if obj.name.split(".")[0] in BODY_NAMES), None)
    if body is None:
        return
    pts = [body.matrix_local @ v.co for v in body.data.vertices]
    x1 = max(p.x for p in pts)
    y0, y1 = min(p.y for p in pts), max(p.y for p in pts)
    z0, z1 = min(p.z for p in pts), max(p.z for p in pts)
    bolts = Part()
    for side in (-1, 1):
        for t in (.22, .78):
            y = y0 + (y1 - y0) * t
            z = z0 + (z1 - z0) * .66
            bolts.tube([((side * (x1 - .004), y, z), .009, .009), ((side * (x1 + .005), y, z), .009, .009)], color, 8)
    parts.append(bolts.build("Bolts", (0, 0, 0), root, body.data.materials[0]))


def finish(root, parts, out, name, forward, grip_center, muzzle_point, length_range, preview_scale=2.4, frame=1.9):
    """Рукоять в начало координат, разворот как у Kenney, маркеры, проверки, FBX, кадры и отчёт."""
    for obj in parts:
        for vertex in obj.data.vertices:
            vertex.co = vertex.co + obj.location - grip_center
            if forward < 0:
                vertex.co = Vector((-vertex.co.x, -vertex.co.y, vertex.co.z))
        obj.data.update()
        obj.location = Vector((0, 0, 0))
    add_bolts(root, parts)
    shade_uvs(parts)
    muzzle_local = muzzle_point - grip_center
    if forward < 0:
        muzzle_local = Vector((-muzzle_local.x, -muzzle_local.y, muzzle_local.z))
    grip_marker = kit.make_marker("GripPoint", (0, 0, 0), root)
    muzzle = kit.make_marker("Muzzle", muzzle_local, root)

    points = world_vertices(parts)
    minimum = Vector(min(v[i] for v in points) for i in range(3))
    maximum = Vector(max(v[i] for v in points) for i in range(3))
    bounds = [round(v, 4) for v in maximum - minimum]
    triangles = sum(len(face.vertices) - 2 for obj in parts for face in obj.data.polygons)
    assert length_range[0] < bounds[1] < length_range[1] and triangles < 6000, (bounds, triangles)
    validation = validate(parts)
    save_model(root, [*parts, grip_marker, muzzle], out, name)
    verify_fbx(out / (name + ".fbx"), len(parts), triangles, bounds)

    center = (minimum + maximum) * .5
    root.scale = (preview_scale,) * 3
    root.location = -Vector((center.x, center.y, minimum.z)) * preview_scale
    render_model(out, bounds[2] * preview_scale * .51, frame, 54 if forward > 0 else 234)
    report = {"meshParts": len(parts), "triangles": triangles, "boundsMetersBlenderXYZ": bounds,
              "blenderForward": "+Y (как Kenney)" if forward > 0 else "-Y (как Kenney)", "sourceMarkers": ["GripPoint", "Muzzle"],
              "fbxRoundTrip": "PASS", **validation}
    (out / "VERIFICATION.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(name + " PASS", json.dumps(report, ensure_ascii=False), flush=True)


# --- Общие детали для набора оружия героя (make_hero_weapons.py) ---


# Первые 8 цветов палитры общие, последние 8 — свои у каждого оружия.
COMMON = ("1E2228", "3A4049", "7E8896", "C6CED8", "EEF1F4", "08090B", "121417", "C39055")
GRAPHITE, DARK, STEEL, LIGHT, WHITE, VOID, RUBBER, BRASS = range(8)
MAIN, MAIN_DARK, ACCENT, ACCENT_DARK, GLOW, HOT, EXTRA, EXTRA2 = range(8, 16)


def palette(*own):
    assert len(own) == 8
    return COMMON + tuple(own)


def block(part, y0, y1, z, width, height, cut, color, x=0.0):
    """Скошенный брусок вдоль Y с фасками на торцах."""
    bevel = min(.012, width * .2, height * .2, (y1 - y0) * .3)
    cut = min(cut, width * .3, height * .3)
    inner = min(cut, (width - 2 * bevel) * .3, (height - 2 * bevel) * .3)
    case(part, [(y0, z, width - 2 * bevel, height - 2 * bevel, inner, x), (y0 + bevel, z, width, height, cut, x),
                (y1 - bevel, z, width, height, cut, x), (y1, z, width - 2 * bevel, height - 2 * bevel, inner, x)], color)


def rod(part, y0, y1, z, r, color, sides=14, x=0.0, front=None):
    """Цилиндр вдоль Y; front — цвет переднего торца."""
    part.rings([circle(y0, z, r, r, sides, x), circle(y1, z, r, r, sides, x)], color)
    if front is not None:
        part.colors[-1] = front


def fins(part, y0, y1, z, outer, inner, count, color_out, color_in, sides=16, x=0.0):
    """Рёбра/витки вокруг оси Y: чередование большого и малого радиуса."""
    rows = []
    step = (y1 - y0) / count
    for i in range(count):
        y = y0 + i * step
        rows += [circle(y, z, outer, outer, sides, x), circle(y + step * .5, z, outer, outer, sides, x),
                 circle(y + step * .52, z, inner, inner, sides, x), circle(y + step, z, inner, inner, sides, x)]
    part.rings(rows, lambda row, face: color_out if row % 4 in (0, 1) else color_in)


def lower(add, y=-.12, z=0.0, grip=GRAPHITE, heel=MAIN, guard=DARK, trigger=ACCENT, length=.15):
    """Рукоять с башмаком, скоба и спуск. Возвращает центр хвата (для finish)."""
    g = Part()
    g.tube([((0, y, z), .044, .048), ((0, y - .012, z - .06), .048, .05), ((0, y - .028, z - .12), .046, .048),
            ((0, y - .036, z - length), .042, .044)], grip, 8, transport=True)
    add("Grip", g)
    ribs = Part()
    for side in (-1, 1):
        for k in range(4):
            zz = z - .035 - k * .027
            yy = y - .01 - k * .0065
            ribs.tube([((side * .045, yy - .016, zz - .003), .0045, .0045), ((side * .047, yy + .016, zz + .003), .0045, .0045)], DARK, 6)
    add("GripRibs", ribs)
    h = Part()
    yb = y - .036
    block(h, yb - .038, yb + .036, z - length - .01, .08, .036, .01, shell_color(heel, heel, heel, DARK))
    add("GripHeel", h)
    t = Part()
    t.tube([((0, y + .03, z - .008), .015, .017), ((0, y + .07, z - .055), .017, .017), ((0, y + .115, z - .058), .015, .015),
            ((0, y + .14, z - .01), .013, .013)], guard, 6, transport=True)
    t.tube([((0, y + .08, z - .005), .01, .012), ((0, y + .085, z - .034), .01, .01)], trigger, 6, transport=True)
    add("Guard", t)
    return Vector((0, y - .02, z - .09))
