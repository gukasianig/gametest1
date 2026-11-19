"""Модели снарядов: пуля, ракета, граната, ледяной осколок (Blender 4.5, фон).

Запуск:
  blender -b --python make_projectiles.py -- <папка вывода, например Assets/Resources/Projectiles>
Нос снаряда смотрит по +Y в Blender; центр модели в начале координат. Цвета - ячейки атласа ArenaGreen_Atlas.png (8x8),
по одной ячейке на грань. Материалы: ArenaGreen_Atlas и ProjGlowOrange (светящееся пламя ракеты).
Эксперимент: удаляется папкой Assets/Resources/Projectiles, код возвращается к примитивам.
"""
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Matrix, Vector

OUT = sys.argv[sys.argv.index('--') + 1]

STEEL = [(4, 6), (6, 6)]
STEEL_LIGHT = [(5, 6)]
STEEL_BLUE = [(6, 4), (4, 4)]
BRASS = [(1, 4)]
COPPER = [(2, 5)]
RED = [(2, 4), (3, 4)]
RED_BRIGHT = [(4, 3)]
DARK = [(6, 5), (0, 5)]
OLIVE = [(3, 6), (0, 6)]
ICE = [(1, 1), (3, 1)]
ICE_DEEP = [(2, 1)]
GLOW = (0, 0)   # слот материала 1; ячейка не важна


def make_material(name, color):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nodes = m.node_tree.nodes
    node = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None) or nodes.new('ShaderNodeBsdfPrincipled')
    node.inputs['Base Color'].default_value = color
    m.diffuse_color = color
    return m


ATLAS = make_material('ArenaGreen_Atlas', (0.5, 0.5, 0.5, 1.0))
GLOW_ORANGE = make_material('ProjGlowOrange', (1.0, 0.55, 0.1, 1.0))


class Model:
    def __init__(self, name, seed):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.rng = random.Random(seed)

    def paint(self, face, cells, mat=0):
        c = cells if isinstance(cells[0], int) else self.rng.choice(cells)
        u = (c[0] + 0.2 + 0.6 * self.rng.random()) / 8.0
        v = 1.0 - (c[1] + 0.2 + 0.6 * self.rng.random()) / 8.0
        for loop in face.loops:
            loop[self.uv].uv = (u, v)
        face.material_index = mat

    def cube(self, center, size, cells, rot=None, mat=0):
        before = set(self.bm.faces)
        m = Matrix.Translation(Vector(center)) @ (rot if rot is not None else Matrix.Identity(4)) @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)
        for f in [f for f in self.bm.faces if f not in before]:
            self.paint(f, cells, mat)

    def along_y(self, y0, y1, r0, r1, segs, side, cap, mat=0, cap_bottom=True):
        """Усечённый конус вдоль оси Y от y0 (радиус r0) до y1 (радиус r1)."""
        before = set(self.bm.faces)
        depth = y1 - y0
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r0, radius2=max(r1, 1e-4), depth=depth)
        verts = res['verts']
        zmin = min(v.co.z for v in verts)
        low_r = sum(math.hypot(v.co.x, v.co.y) for v in verts if abs(v.co.z - zmin) < 1e-6) / segs
        if abs(low_r - r0) > 1e-4 and abs(r0 - r1) > 1e-4:   # радиусы вышли наоборот
            for v in verts:
                t = (v.co.z - zmin) / depth
                r = r0 + (max(r1, 1e-4) - r0) * t
                a = math.atan2(v.co.y, v.co.x)
                v.co.x, v.co.y = r * math.cos(a), r * math.sin(a)
        new = [f for f in self.bm.faces if f not in before]
        rot = Matrix.Rotation(math.radians(-90), 4, 'X')   # локальная +Z -> +Y
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((0, (y0 + y1) / 2, 0)) @ rot, verts=verts)
        self.bm.normal_update()
        for f in new:
            if f.normal.y > 0.9:
                self.paint(f, cap, mat)
            elif f.normal.y < -0.9:
                self.paint(f, cap if cap_bottom else DARK, mat)
            else:
                self.paint(f, side, mat)

    def sphere(self, center, radius, squash, cells, subdiv=2):
        before = set(self.bm.faces)
        m = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((radius, radius, radius * squash, 1.0))
        bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=1.0, matrix=m)
        for f in [f for f in self.bm.faces if f not in before]:
            self.paint(f, cells)

    def finish(self):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
        bm.normal_update()
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        me.materials.append(ATLAS)
        me.materials.append(GLOW_ORANGE)
        obj = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def bullet():
    m = Model('bullet', 1)
    m.along_y(-0.28, 0.02, 0.085, 0.085, 10, BRASS, BRASS)               # гильза-корпус
    m.along_y(-0.30, -0.28, 0.09, 0.09, 10, COPPER, COPPER)               # донце
    m.along_y(0.02, 0.30, 0.085, 0.0, 10, STEEL_LIGHT, STEEL_LIGHT)       # остроконечная головка
    return m.finish()


def rocket():
    m = Model('rocket', 2)
    m.along_y(-0.35, 0.28, 0.13, 0.13, 10, STEEL_LIGHT, STEEL_LIGHT)     # корпус
    m.along_y(0.28, 0.62, 0.13, 0.0, 10, RED, RED)                        # боевая часть
    m.along_y(-0.16, -0.10, 0.135, 0.135, 10, BRASS, BRASS)               # жёлтая полоса
    m.along_y(-0.45, -0.35, 0.09, 0.13, 10, DARK, DARK)                   # сопло
    for k in range(4):
        rot = Matrix.Rotation(math.radians(90 * k), 4, 'Y')
        m.cube(rot @ Vector((0.17, -0.30, 0.0)), (0.17, 0.26, 0.025), RED, rot)   # стабилизаторы
    m.along_y(-0.78, -0.45, 0.0, 0.075, 8, (0, 0), (0, 0), mat=1)         # пламя: светящийся конус
    return m.finish()


def grenade():
    m = Model('grenade', 3)
    m.sphere((0, -0.01, 0), 0.085, 1.15, OLIVE, 2)                       # корпус
    for k in (-1, 1):                                                     # пояса «ананаса»
        m.along_y(0.025 * k - 0.006, 0.025 * k + 0.006, 0.088, 0.088, 12, DARK, DARK)
    m.along_y(0.085, 0.125, 0.032, 0.03, 10, STEEL, STEEL)               # горловина
    m.cube((0.04, 0.115, 0), (0.014, 0.075, 0.03), STEEL_LIGHT)          # рычаг
    m.cube((0.0, 0.14, 0), (0.07, 0.014, 0.02), STEEL_LIGHT)             # предохранительная скоба
    m.cube((-0.045, 0.125, 0), (0.014, 0.03, 0.014), BRASS)              # чека
    return m.finish()


def ice_shard():
    m = Model('ice_shard', 4)
    m.along_y(-0.08, 0.12, 0.11, 0.11, 6, ICE, ICE)                      # шестигранный кристалл
    m.along_y(0.12, 0.34, 0.11, 0.0, 6, ICE, ICE)                        # острие
    m.along_y(-0.30, -0.08, 0.0, 0.11, 6, ICE_DEEP, ICE_DEEP)            # хвост конусом
    return m.finish()


def export(obj, path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
                             global_scale=1.0, axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='STRIP')


for name, builder in (('bullet', bullet), ('rocket', rocket), ('grenade', grenade), ('ice_shard', ice_shard)):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    obj = builder()
    export(obj, os.path.join(OUT, name + '.fbx'))
    print('PROJECTILE', name, 'tris', len(obj.data.polygons), 'dims', tuple(round(v, 3) for v in obj.dimensions))
