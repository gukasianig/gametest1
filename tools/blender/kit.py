"""Общие функции для генерации low-poly моделей в стиле Kenney (плоские цвета, общая палитра-текстура).
Запуск: blender -b -P <скрипт>.py  (Blender 4.x). Модель собирается из скруглённых боксов; цвета берутся из палитры PALETTE,
каждой грани назначается UV в центр ячейки палитры -> один материал на все модели."""
import bpy
import bmesh
import math
import os
from mathutils import Vector

PALETTE = [
    (0.72, 0.70, 0.86),  # 0 светлая сталь с сиреневым
    (0.24, 0.24, 0.32),  # 1 тёмная сталь
    (0.90, 0.26, 0.20),  # 2 красный
    (1.00, 0.60, 0.15),  # 3 оранжевый
    (1.00, 0.85, 0.30),  # 4 жёлтый
    (0.55, 0.36, 0.85),  # 5 фиолетовый
    (0.30, 0.80, 0.50),  # 6 зелёный
    (0.20, 0.80, 0.80),  # 7 бирюзовый
    (0.95, 0.95, 1.00),  # 8 белый
    (0.30, 0.45, 0.90),  # 9 синий
    (0.10, 0.10, 0.13),  # 10 чёрный
    (0.55, 0.36, 0.22),  # 11 коричневый
    (0.50, 0.50, 0.58),  # 12 средняя сталь
    (0.85, 0.40, 0.55),  # 13 розовый
    (0.35, 0.28, 0.55),  # 14 тёмно-фиолетовый
    (0.65, 0.95, 0.40),  # 15 лайм
]
CELLS = 4
CELL_PX = 32


def save_palette(path):
    size = CELLS * CELL_PX
    img = bpy.data.images.new("Palette", size, size, alpha=False)
    px = []
    for y in range(size):
        for x in range(size):
            i = (y // CELL_PX) * CELLS + (x // CELL_PX)
            c = PALETTE[i] if i < len(PALETTE) else (1, 0, 1)
            px.extend([c[0], c[1], c[2], 1.0])
    img.pixels = px
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    return img


def cell_uv(index):
    # Блендер: v растёт вверх; ячейка index считается сверху вниз при записи пикселей снизу вверх.
    row = index // CELLS
    col = index % CELLS
    u = (col + 0.5) / CELLS
    v = (row + 0.5) / CELLS
    return (u, v)


class Builder:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.ci = self.bm.faces.layers.int.new("ci")

    def _paint(self, faces, color):
        for f in faces:
            f[self.ci] = color

    def box(self, center, size, color, bevel=0.03, rot_z=0.0, rot_x=0.0):
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res["verts"]
        bmesh.ops.scale(self.bm, vec=Vector(size), verts=verts)
        if rot_x:
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=__import__("mathutils").Matrix.Rotation(rot_x, 3, 'X'))
        if rot_z:
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=__import__("mathutils").Matrix.Rotation(rot_z, 3, 'Z'))
        bmesh.ops.translate(self.bm, vec=Vector(center), verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self._paint(faces, color)  # красим до скоса: новые грани скоса наследуют цвет соседей
        if bevel > 0:
            edges = list({e for f in faces for e in f.edges})
            r = bmesh.ops.bevel(self.bm, geom=edges, offset=min(bevel, min(size) * 0.45), segments=1, affect='EDGES')
            self._paint([g for g in r["faces"] if isinstance(g, bmesh.types.BMFace)], color)

    def cylinder(self, center, radius, depth, color, segments=10, axis='Z', bevel=0.0):
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=depth)
        verts = res["verts"]
        mathutils = __import__("mathutils")
        if axis == 'X':
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.pi / 2, 3, 'Y'))
        elif axis == 'Y':
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.pi / 2, 3, 'X'))
        bmesh.ops.translate(self.bm, vec=Vector(center), verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self._paint(faces, color)

    def sphere(self, center, radius, color, scale=(1, 1, 1), subdiv=2):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=10, v_segments=6, radius=radius)
        verts = res["verts"]
        bmesh.ops.scale(self.bm, vec=Vector(scale), verts=verts)
        bmesh.ops.translate(self.bm, vec=Vector(center), verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self._paint(faces, color)

    def finish(self, origin=(0, 0, 0), parent=None):
        """origin — точка вращения (шарнир) части; parent — корневой Empty."""
        bmesh.ops.translate(self.bm, vec=-Vector(origin), verts=self.bm.verts)
        uv = self.bm.loops.layers.uv.new("UVMap")
        for f in self.bm.faces:
            u, v = cell_uv(f[self.ci])
            for loop in f.loops:
                loop[uv].uv = (u, v)
        self.bm.normal_update()
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for p in mesh.polygons:
            p.use_smooth = False
        obj = bpy.data.objects.new(self.name, mesh)
        obj.location = origin
        bpy.context.collection.objects.link(obj)
        if parent is not None:
            obj.parent = parent
        return obj


def make_root(name):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    return root


def make_marker(name, location, parent):
    m = bpy.data.objects.new(name, None)
    m.location = location
    m.empty_display_size = 0.1
    bpy.context.collection.objects.link(m)
    m.parent = parent
    return m


def make_material(palette_path):
    mat = bpy.data.materials.new("M_Palette")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    img = bpy.data.images.load(palette_path)
    img.colorspace_settings.name = 'sRGB'
    tex.image = img
    tex.interpolation = 'Closest'
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8
    return mat


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export_fbx(objects, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', add_leaf_bones=False, mesh_smooth_type='FACE',
                             path_mode='STRIP')


def render_preview(objects, path, yaw_deg=35, elev_deg=25, size=1.8):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.filepath = path
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = size
    cam = bpy.data.objects.new("cam", cam_data)
    bpy.context.collection.objects.link(cam)
    d = 6.0
    yaw = math.radians(yaw_deg)
    el = math.radians(elev_deg)
    # камера смотрит на модель спереди (+Y — «перед» модели) под углом
    cam.location = (math.sin(yaw) * math.cos(el) * d, math.cos(yaw) * math.cos(el) * d, math.sin(el) * d + 0.5)
    direction = Vector((0, 0, 0.5)) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    bpy.ops.render.render(write_still=True)
