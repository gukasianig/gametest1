"""Панель героя для экрана боя (ХП, броня, рывок), построенная в Blender с нуля.

Запуск (Blender 4.5):
  blender -b --factory-startup -P tools/blender/make_hud_panel.py -- preview <out.png>
      лист из трёх вариантов стиля (A сталь, B голограмма, C броне-плита) с макетом заполнения.
  blender -b --factory-startup -P tools/blender/make_hud_panel.py -- export <A|B|C> <out_dir>
      отдельные PNG с прозрачным фоном: hud_panel.png (рамка с пазами), hud_heart.png, hud_shield.png,
      hud_bar_fill.png (белая глянцевая заливка — цвет задаёт код), hud_dash.png (значок рывка).

Раскладка панели в долях (x слева, y снизу) совпадает с CombatHud.cs: сердце 0.03-0.14 / 0.47-0.90,
полоса ХП 0.16-0.97 / 0.52-0.86, броня 0.03-0.47 / 0.10-0.42, рывок 0.50-0.97 / 0.10-0.42.
Единица = 100 px макета 1280x720: панель 4.416 x 1.188.
"""
import bpy, bmesh, math, sys, os
from mathutils import Vector

W, H = 4.416, 1.188
HEART = (0.03, 0.47, 0.14, 0.90)
HP = (0.16, 0.52, 0.97, 0.86)
ARMOR = (0.03, 0.10, 0.47, 0.42)
DASH = (0.50, 0.10, 0.97, 0.42)

VARIANTS = {
    "A": dict(name="Сталь", plate=(0.075, 0.085, 0.095), plate_metal=0.85, plate_rough=0.38,
              rim=(0.32, 0.35, 0.38), slot=(0.012, 0.016, 0.02), accent=(0.12, 0.95, 0.78), accent_power=1.4,
              bolts=True, hazard=False, glass=False, heart=(0.85, 0.06, 0.07), shield=(0.25, 0.6, 1.0)),
    "B": dict(name="Голограмма", plate=(0.01, 0.04, 0.07), plate_metal=0.2, plate_rough=0.08,
              rim=(0.1, 0.7, 0.9), slot=(0.005, 0.015, 0.025), accent=(0.2, 0.85, 1.0), accent_power=1.8,
              bolts=False, hazard=False, glass=True, heart=(1.0, 0.15, 0.3), shield=(0.3, 0.8, 1.0)),
    "C": dict(name="Броне-плита", plate=(0.14, 0.13, 0.095), plate_metal=0.7, plate_rough=0.5,
              rim=(0.62, 0.46, 0.2), slot=(0.02, 0.018, 0.014), accent=(1.0, 0.55, 0.12), accent_power=1.4,
              bolts=True, hazard=True, glass=False, heart=(0.8, 0.08, 0.05), shield=(0.85, 0.65, 0.3)),
}


# ---------- материалы ----------
def material(name, base, metal=0.0, rough=0.5, emit=None, power=0.0, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*base, 1)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    if emit is not None:
        p.inputs["Emission Color"].default_value = (*emit, 1)
        p.inputs["Emission Strength"].default_value = power
    if alpha < 1.0:
        p.inputs["Alpha"].default_value = alpha
        m.surface_render_method = "BLENDED"
    return m


# ---------- геометрия ----------
def rounded_outline(w, h, r, steps=6):
    r = min(r, w / 2 - 1e-3, h / 2 - 1e-3)
    pts = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for i in range(steps + 1):
            a = math.radians(a0 + 90 * i / steps)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def heart_outline(size, n=72):
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((x / 34 * size, (y + 2.5) / 34 * size))
    return pts


def shield_outline(size, n=24):
    w, top, mid = 0.8 * size, 0.92 * size, 0.15 * size
    pts = [(-w, top), (-w * 0.35, top + 0.08 * size), (0, top + 0.1 * size), (w * 0.35, top + 0.08 * size), (w, top)]
    for i in range(1, n + 1):  # правый бок: прямо вниз, затем дуга к острию
        t = i / n
        if t < 0.35:
            pts.append((w, top - (top - mid) * t / 0.35))
        else:
            k = (t - 0.35) / 0.65
            pts.append((w * (1 - k) ** 1.4, mid - (mid + size) * math.sin(k * math.pi / 2)))
    left = [(-x, y) for x, y in reversed(pts[5:-1])]
    return pts + left


def prism(name, outline, depth, z, mat, bevel=0.02, segments=3, at=(0, 0)):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    verts = [bm.verts.new((x + at[0], y + at[1], z)) for x, y in outline]
    face = bm.faces.new(verts)
    res = bmesh.ops.extrude_face_region(bm, geom=[face])
    for v in [e for e in res["geom"] if isinstance(e, bmesh.types.BMVert)]:
        v.co.z += depth
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(mat)
    if bevel > 0:
        mod = obj.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(40)
    for poly in mesh.polygons:
        poly.use_smooth = True
    obj.modifiers.new("ws", "WEIGHTED_NORMAL")
    return obj


def cylinder(name, x, y, r, depth, z, mat, verts=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=(x, y, z + depth / 2))
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(mat)
    mod = o.modifiers.new("bevel", "BEVEL")
    mod.width = min(r * 0.3, depth * 0.45)
    mod.segments = 3
    bpy.ops.object.shade_smooth()
    return o


def text(name, body, x, y, z, size, mat, align="CENTER"):
    curve = bpy.data.curves.new(name, "FONT")
    curve.body = body
    curve.size = size
    curve.align_x = align
    curve.align_y = "CENTER"
    font = "C:/Windows/Fonts/arialbd.ttf"
    if os.path.exists(font):
        curve.font = bpy.data.fonts.load(font, check_existing=True)
    o = bpy.data.objects.new(name, curve)
    o.location = (x, y, z)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    return o


def rect_of(area, origin):
    x0, y0, x1, y1 = area
    cx = origin[0] - W / 2 + (x0 + x1) / 2 * W
    cy = origin[1] - H / 2 + (y0 + y1) / 2 * H
    return cx, cy, (x1 - x0) * W, (y1 - y0) * H


# ---------- сборка варианта ----------
def build(key, origin=(0.0, 0.0), mock=False):
    v = VARIANTS[key]
    groups = {"panel": [], "heart": [], "shield": [], "fill": [], "dash": [], "mock": []}
    plate_mat = material(key + "_plate", v["plate"], v["plate_metal"], v["plate_rough"], alpha=0.82 if v["glass"] else 1.0)
    rim_mat = material(key + "_rim", v["rim"], 0.9, 0.3, emit=v["rim"] if v["glass"] else None, power=0.9)
    slot_mat = material(key + "_slot", v["slot"], 0.3, 0.6)
    accent_mat = material(key + "_accent", v["accent"], 0.0, 0.4, emit=v["accent"], power=v["accent_power"])
    bolt_mat = material(key + "_bolt", (0.55, 0.57, 0.6), 1.0, 0.25)
    ox, oy = origin

    # Плита: внешний обод + основная плита чуть меньше.
    groups["panel"].append(prism(key + "_rim", rounded_outline(W, H, 0.16), 0.06, 0.0, rim_mat, 0.02, at=origin))
    groups["panel"].append(prism(key + "_plate", rounded_outline(W - 0.06, H - 0.06, 0.13), 0.1, 0.02, plate_mat, 0.03, at=origin))
    # Подсвеченная полоса сверху.
    groups["panel"].append(prism(key + "_accent", rounded_outline(W * 0.78, 0.03, 0.014), 0.02, 0.12, accent_mat, 0.0,
                                 at=(ox, oy + H / 2 - 0.07)))
    if v["glass"]:
        # Светящаяся окантовка вместо болтов.
        for side in (-1, 1):
            groups["panel"].append(prism(key + "_edge%d" % side, rounded_outline(0.03, H * 0.6, 0.014), 0.02, 0.12, accent_mat, 0.0,
                                         at=(ox + side * (W / 2 - 0.07), oy)))
    if v["bolts"]:
        for sx in (-1, 1):
            for sy in (-1, 1):
                groups["panel"].append(cylinder(key + "_bolt", ox + sx * (W / 2 - 0.1), oy + sy * (H / 2 - 0.1), 0.035, 0.05, 0.12, bolt_mat, 6))
    if v["hazard"]:
        black = material(key + "_hz_k", (0.02, 0.02, 0.02), 0.2, 0.6)
        yellow = material(key + "_hz_y", (0.95, 0.7, 0.05), 0.1, 0.5)
        for i in range(6):
            outline = [(-0.04, -0.06), (0.0, -0.06), (0.06, 0.06), (0.02, 0.06)]
            groups["panel"].append(prism(key + "_hz%d" % i, outline, 0.01, 0.12, yellow if i % 2 == 0 else black, 0.0,
                                         at=(ox + W / 2 - 0.62 + i * 0.065, oy - H / 2 + 0.07)))

    # Пазы: светлый ободок и тёмное углубление (под полосу ХП, броню, рывок).
    for area, label in ((HP, "hp"), (ARMOR, "armor"), (DASH, "dash")):
        cx, cy, w, h = rect_of(area, origin)
        groups["panel"].append(prism(key + "_slotrim_" + label, rounded_outline(w + 0.04, h + 0.04, 0.09), 0.015, 0.12, rim_mat, 0.008, at=(cx, cy)))
        groups["panel"].append(prism(key + "_slot_" + label, rounded_outline(w, h, 0.08), 0.012, 0.128, slot_mat, 0.006, at=(cx, cy)))

    # Гнездо и сердце: латунное кольцо с заклёпками, тёмное дно, объёмное глянцевое сердце с линией пульса.
    cx, cy, w, h = rect_of(HEART, origin)
    size = min(w, h) * 0.5
    heart_mat = material(key + "_heart", v["heart"], 0.0, 0.16, emit=v["heart"], power=0.1)
    heart_mat.node_tree.nodes["Principled BSDF"].inputs["Coat Weight"].default_value = 0.8
    heart_dark = material(key + "_heart_dark", tuple(c * 0.35 for c in v["heart"]), 0.0, 0.4)
    pulse_mat = material(key + "_pulse", (1, 0.92, 0.85), 0.0, 0.3, emit=(1, 0.85, 0.7), power=1.2)
    groups["heart"].append(cylinder(key + "_socket", cx, cy, size * 1.14, 0.06, 0.12, rim_mat, 48))
    groups["heart"].append(cylinder(key + "_socket_in", cx, cy, size * 0.98, 0.04, 0.15, slot_mat, 48))
    for i in range(8):
        a = math.radians(22.5 + i * 45)
        groups["heart"].append(cylinder(key + "_stud%d" % i, cx + math.cos(a) * size * 1.06, cy + math.sin(a) * size * 1.06,
                                        size * 0.055, 0.03, 0.18, bolt_mat, 8))
    groups["heart"].append(prism(key + "_heart_back", heart_outline(size * 1.42), 0.05, 0.17, heart_dark, 0.02, 3, at=(cx, cy - size * 0.03)))
    groups["heart"].append(prism(key + "_heart", heart_outline(size * 1.3), 0.3, 0.2, heart_mat, 0.14, 8, at=(cx, cy)))
    # Линия пульса поверх сердца.
    pts = [(-0.55, 0.0), (-0.2, 0.0), (-0.1, 0.32), (0.02, -0.38), (0.14, 0.18), (0.22, 0.0), (0.55, 0.0)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        length = math.hypot(x1 - x0, y1 - y0) * size
        ang = math.atan2(y1 - y0, x1 - x0)
        seg = prism(key + "_pulse", rounded_outline(length + size * 0.06, size * 0.07, size * 0.03), 0.02, 0.52, pulse_mat, 0.0,
                    at=(0, 0))
        seg.rotation_euler = (0, 0, ang)
        seg.location = (cx + (x0 + x1) / 2 * size, cy + (y0 + y1) / 2 * size + size * 0.05, 0)
        groups["heart"].append(seg)

    # Щит брони: латунная окантовка, стальное поле, заклёпки, рельефная оранжевая звезда.
    cx, cy, w, h = rect_of(ARMOR, origin)
    sx = cx - w / 2 + h * 0.55
    sy = cy - h * 0.02
    s = h * 0.36
    trim_mat = material(key + "_shield_trim", v["rim"], 1.0, 0.28)
    shield_mat = material(key + "_shield", (0.42, 0.46, 0.5), 0.95, 0.3)
    shield_mat.node_tree.nodes["Principled BSDF"].inputs["Coat Weight"].default_value = 0.4
    star_mat = material(key + "_star", v["accent"], 0.6, 0.3, emit=v["accent"], power=0.5)
    groups["shield"].append(prism(key + "_shield_trim", shield_outline(s * 1.12), 0.06, 0.14, trim_mat, 0.02, 3, at=(sx, sy - s * 0.04)))
    groups["shield"].append(prism(key + "_shield", shield_outline(s), 0.14, 0.15, shield_mat, 0.05, 5, at=(sx, sy)))
    star = []
    for i in range(10):
        a = math.radians(90 + i * 36)
        r = s * (0.36 if i % 2 == 0 else 0.15)
        star.append((math.cos(a) * r, math.sin(a) * r))
    groups["shield"].append(prism(key + "_star", star, 0.05, 0.29, star_mat, 0.012, 2, at=(sx, sy + s * 0.18)))
    for px, py in ((-0.62, 0.88), (0.62, 0.88), (-0.62, 0.3), (0.62, 0.3)):
        groups["shield"].append(cylinder(key + "_rivet", sx + px * s, sy + py * s, s * 0.06, 0.03, 0.29, bolt_mat, 8))

    # Значок рывка: металлические шевроны и линии скорости.
    cx, cy, w, h = rect_of(DASH, origin)
    dx = cx - w / 2 + h * 0.5
    chevron_mat = material(key + "_chevron", v["accent"], 0.8, 0.25, emit=v["accent"], power=0.35)
    line_mat = material(key + "_speed", v["rim"], 0.9, 0.35)
    for i in range(2):
        chevron = [(-0.06, 0.1), (0.0, 0.1), (0.08, 0.0), (0.0, -0.1), (-0.06, -0.1), (0.02, 0.0)]
        groups["dash"].append(prism(key + "_dash%d" % i, chevron, 0.06, 0.15, chevron_mat, 0.015, 3, at=(dx - 0.03 + i * 0.09, cy)))
    for j, (ly, ll) in enumerate(((0.06, 0.1), (0.0, 0.14), (-0.06, 0.1))):
        groups["dash"].append(prism(key + "_speed%d" % j, rounded_outline(ll, 0.018, 0.009), 0.03, 0.15, line_mat, 0.004,
                                    at=(dx - 0.12 - ll / 2 + 0.03, cy + ly)))

    # Заливка полосы ХП (белая глянцевая; цвет задаётся в игре).
    cx, cy, w, h = rect_of(HP, origin)
    fill_mat = material(key + "_fill", (0.92, 0.92, 0.92), 0.0, 0.22)
    fill = prism(key + "_fill", rounded_outline(w - 0.06, h - 0.06, 0.06), 0.03, 0.142, fill_mat, 0.012, 3, at=(cx, cy))
    groups["fill"].append(fill)

    if mock:
        # Макет для листа выбора: полоса ХП зелёная на 75 %, цифры, подписи.
        fill.hide_render = True
        green = material(key + "_mock_green", (0.15, 0.75, 0.3), 0.0, 0.25, emit=(0.1, 0.6, 0.25), power=0.4)
        groups["mock"].append(prism(key + "_mock_hp", rounded_outline((w - 0.06) * 0.75, h - 0.06, 0.06), 0.03, 0.142, green, 0.012, 3,
                                    at=(cx - (w - 0.06) * 0.125, cy)))
        white = material(key + "_mock_txt", (1, 1, 1), 0, 0.5, emit=(1, 1, 1), power=1.0)
        groups["mock"].append(text(key + "_t_hp", "92 / 100", cx, cy, 0.2, 0.22, white))
        ax, ay, aw, ah = rect_of(ARMOR, origin)
        groups["mock"].append(text(key + "_t_ar", "Броня 20%", ax - aw / 2 + ah * 1.05, ay, 0.2, 0.17,
                                   material(key + "_mock_ar", v["shield"], 0, 0.5, emit=v["shield"], power=0.6), "LEFT"))
        dx2, dy2, dw, dh = rect_of(DASH, origin)
        groups["mock"].append(text(key + "_t_ds", "Рывок  [Пробел]", dx2 + dh * 0.35, dy2, 0.2, 0.15, white))
        groups["mock"].append(text(key + "_t_name", key + " — " + v["name"], ox - W / 2, oy + H / 2 + 0.2, 0.2, 0.2,
                                   material(key + "_mock_cap", (0.8, 0.85, 0.9), 0, 0.5, emit=(0.8, 0.85, 0.9), power=1.0), "LEFT"))
    return groups


# ---------- сцена и рендер ----------
def setup_scene(res_x, res_y):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = True
    scene.render.resolution_x, scene.render.resolution_y = res_x, res_y
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("w")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.38, 0.42, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    key_light = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
    key_light.data.energy = 900
    key_light.data.size = 6
    key_light.location = (-4, 4, 6)
    key_light.rotation_euler = (math.radians(35), math.radians(-30), 0)
    scene.collection.objects.link(key_light)
    rim = bpy.data.objects.new("rim", bpy.data.lights.new("rim", "AREA"))
    rim.data.energy = 300
    rim.data.size = 4
    rim.data.color = (0.6, 0.85, 1.0)
    rim.location = (5, -3, 3)
    rim.rotation_euler = (math.radians(-50), math.radians(50), 0)
    scene.collection.objects.link(rim)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.type = "ORTHO"
    cam.location = (0, 0, 10)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return scene, cam


def frame(cam, cx, cy, w, h):
    scene = bpy.context.scene
    cam.location = (cx, cy, 10)
    aspect = scene.render.resolution_x / scene.render.resolution_y
    cam.data.ortho_scale = max(w, h * aspect)


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def only(groups, visible):
    for name, objects in groups.items():
        for o in objects:
            o.hide_render = name not in visible


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    mode = args[0]
    if mode == "preview":
        scene, cam = setup_scene(1100, 1100)
        for i, key in enumerate("ABC"):
            build(key, (0.0, 1.9 - i * 1.9), mock=True)
        frame(cam, 0.0, 0.1, W + 0.6, 3 * 1.9 + 0.2)
        render(os.path.abspath(args[1]))
        return
    key, out = args[1], os.path.abspath(args[2])
    os.makedirs(out, exist_ok=True)
    scene, cam = setup_scene(884, 238)
    groups = build(key)
    # Рамка с пазами, сердце, щит, значок рывка: те же координаты, что у панели, чтобы совпадали при наложении.
    only(groups, {"panel"})
    frame(cam, 0, 0, W, H)
    render(os.path.join(out, "hud_panel.png"))
    bpy.context.view_layer.update()
    layout = {}
    for part, area in (("heart", HEART), ("shield", None), ("dash", None), ("fill", HP)):
        only(groups, {part})
        if area is None:
            # Квадратный кадр вокруг значка.
            objs = groups[part]
            xs = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
            cx = (min(p.x for p in xs) + max(p.x for p in xs)) / 2
            cy = (min(p.y for p in xs) + max(p.y for p in xs)) / 2
            side = max(max(p.x for p in xs) - min(p.x for p in xs), max(p.y for p in xs) - min(p.y for p in xs)) * 1.15
            scene.render.resolution_x = scene.render.resolution_y = 256
            frame(cam, cx, cy, side, side)
        elif part == "heart":
            cx, cy, w, h = rect_of(area, (0, 0))
            side = max(w, h) * 1.05
            scene.render.resolution_x = scene.render.resolution_y = 256
            frame(cam, cx, cy, side, side)
        else:
            cx, cy, w, h = rect_of(area, (0, 0))
            scene.render.resolution_x, scene.render.resolution_y = 720, 64
            side = None
            frame(cam, cx, cy, w - 0.04, (w - 0.04) * 64 / 720)
            layout[part] = [(cx + W / 2) / W, (cy + H / 2) / H, (w - 0.04) / W, (w - 0.04) * 64 / 720 / H]
        if side is not None:
            layout[part] = [(cx + W / 2) / W, (cy + H / 2) / H, side / W, side / H]
        render(os.path.join(out, "hud_%s.png" % ("bar_fill" if part == "fill" else part)))
    # Положение и размер каждой картинки в долях панели: [центр x, центр y, ширина, высота] (читает CombatHud).
    import json
    with open(os.path.join(out, "hud_layout.json"), "w", encoding="utf-8") as fh:
        json.dump({k: [round(x, 5) for x in val] for k, val in layout.items()}, fh)


main()
