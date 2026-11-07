"""Palette meshes, size checks, export and studio renders for D3 creatures."""

import json
import math

import bmesh
import bpy
from mathutils import Vector

import kit


def make_atlas(swatches, output_dir, name):
    image = bpy.data.images.new(name + "_Palette", width=128, height=128, alpha=False)
    colors = [tuple(int(value[i:i + 2], 16) / 255 for i in (0, 2, 4)) for value in swatches]
    image.pixels.foreach_set([
        channel
        for y in range(128)
        for x in range(128)
        for channel in (*colors[(y // 32) * 4 + x // 32], 1.0)
    ])
    image.filepath_raw = str(output_dir / (name + "_Palette.png"))
    image.file_format = 'PNG'
    image.save()
    image.pack()
    material = bpy.data.materials.new(name + " Palette")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    shader = nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = .89
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = 'Closest'
    material.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
    nodes.active = texture
    return material


class Part:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.colors = []

    def shape(self, vertices, faces, colors):
        start = len(self.vertices)
        self.vertices.extend(vertices)
        self.faces.extend(tuple(start + i for i in face) for face in faces)
        self.colors.extend([colors] * len(faces) if isinstance(colors, int) else colors)

    def rings(self, rings, color):
        sides = len(rings[0])
        faces = [tuple(range(sides - 1, -1, -1))]
        colors = [color(0, 0) if callable(color) else color]
        for row in range(len(rings) - 1):
            for i in range(sides):
                j = (i + 1) % sides
                faces.append((row * sides + i, row * sides + j, (row + 1) * sides + j, (row + 1) * sides + i))
                colors.append(color(row, i) if callable(color) else color)
        faces.append(tuple(range((len(rings) - 1) * sides, len(rings) * sides)))
        colors.append(color(len(rings) - 2, 0) if callable(color) else color)
        self.shape([vertex for ring in rings for vertex in ring], faces, colors)

    def tube(self, sections, color, sides=8, transport=False):
        centers = [Vector(section[0]) for section in sections]
        rings = []
        previous_tangent = None
        u = None
        for i, (center, width, depth) in enumerate(sections):
            tangent = (centers[min(i + 1, len(centers) - 1)] - centers[max(i - 1, 0)]).normalized()
            if transport and previous_tangent is not None:
                axis = previous_tangent.rotation_difference(tangent) @ u
            else:
                axis = Vector((1, 0, 0))
                if abs(tangent.dot(axis)) > .95:
                    axis = Vector((0, 1, 0))
            u = (axis - tangent * tangent.dot(axis)).normalized()
            v = tangent.cross(u)
            previous_tangent = tangent
            ring = []
            for k in range(sides):
                angle = (k + .5) * math.tau / sides
                ring.append(Vector(center) + u * (math.cos(angle) * width) + v * (math.sin(angle) * depth))
            rings.append(ring)
        self.rings(rings, color)

    def solid(self, contour, thickness, front, edge, axis=(0, 1, 0)):
        # The contour can slope in depth. Its reverse surface closes the plate.
        offset = Vector(axis) * thickness
        self.rings([[Vector(point) - offset for point in contour], contour], edge)
        self.colors[-1] = front

    def build(self, name, pivot, parent, material):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([Vector(v) - Vector(pivot) for v in self.vertices], [], self.faces)
        mesh.update()
        uv = mesh.uv_layers.new(name="PaletteUV")
        for poly, color in zip(mesh.polygons, self.colors):
            for loop in poly.loop_indices:
                uv.data[loop].uv = kit.cell_uv(color)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.parent = parent
        obj.location = pivot
        mesh.materials.append(material)
        return obj


def world_vertices(parts):
    bpy.context.view_layer.update()
    return [obj.matrix_world @ vert.co for obj in parts for vert in obj.data.vertices]


def normalize(parts, footprint, max_height):
    vertices = world_vertices(parts)
    min_v = Vector(min(v[i] for v in vertices) for i in range(3))
    max_v = Vector(max(v[i] for v in vertices) for i in range(3))
    scale = footprint / max((max_v - min_v).x, (max_v - min_v).y)
    center = (min_v + max_v) / 2
    for obj in parts:
        obj.location = (obj.location - Vector((center.x, center.y, min_v.z))) * scale
        for vert in obj.data.vertices:
            vert.co *= scale
        obj.data.update()

    vertices = world_vertices(parts)
    bounds = [round(max(v[i] for v in vertices) - min(v[i] for v in vertices), 4) for i in range(3)]
    triangles = sum(len(face.vertices) - 2 for obj in parts for face in obj.data.polygons)
    assert .25 < bounds[2] < max_height and 0 < triangles < 3200, (bounds, triangles)
    assert abs(max(bounds[:2]) - footprint) < 0.001
    assert abs(min(v.z for v in vertices)) < 0.0001
    return bounds, triangles, round(min(v.z for v in vertices), 6)


def save_model(root, parts, output_dir, name):
    # Save clean, editable source and export the same meshes, including their pivots.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [root, *parts]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(output_dir / (name + ".blend")))
    bpy.ops.export_scene.fbx(filepath=str(output_dir / (name + ".fbx")), use_selection=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             add_leaf_bones=False, mesh_smooth_type='FACE', path_mode='COPY', embed_textures=True)


def add_light(name, position, power, color, size):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = power
    data.color = color
    data.shape = 'DISK'
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector((0, 0, .40)) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def render_model(output_dir, target_height, frame_size, showcase_yaw):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 16
    scene.cycles.use_denoising = True
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 6
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'Standard'
    scene.world = bpy.data.worlds.new("Preview World")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.12, .095, .16, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .32

    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.003))
    floor = bpy.context.object
    floor_mat = bpy.data.materials.new("Slate backdrop")
    floor_mat.use_nodes = True
    floor_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.061, .045, .089, 1)
    floor_mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .95
    floor.data.materials.append(floor_mat)
    add_light("Warm key", (2.5, 4, 5), 540, (1, .86, .80), 3.2)
    add_light("Lavender fill", (-4, 1, 2.2), 360, (.68, .72, 1), 3.1)
    add_light("Rim", (1.6, -3.3, 3.4), 590, (.91, .61, .88), 2.4)

    camera_data = bpy.data.cameras.new("Preview Camera")
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = frame_size
    camera = bpy.data.objects.new("Preview Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    target = Vector((0, 0, target_height))
    for name, yaw, elevation in (("three-quarter", showcase_yaw, 29), ("side", 90, 10), ("top", 26, 79)):
        yaw, elevation = math.radians(yaw), math.radians(elevation)
        camera.location = target + Vector((math.sin(yaw) * math.cos(elevation), math.cos(yaw) * math.cos(elevation), math.sin(elevation))) * 4
        camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(output_dir / (name + ".png"))
        bpy.ops.render.render(write_still=True)


def finish(root, parts, output_dir, name, footprint, target_height, frame_size, showcase_yaw=26, max_height=1.4):
    bounds, triangles, feet_z = normalize(parts, footprint, max_height)
    geometry = validate(parts)
    save_model(root, parts, output_dir, name)
    verify_fbx(output_dir / (name + ".fbx"), len(parts), triangles, bounds)
    render_model(output_dir, target_height, frame_size, showcase_yaw)
    report = {"meshParts": len(parts), "triangles": triangles, "materialSlotsPerPart": 1,
              "boundsMetersBlenderXYZ": bounds, "feetZ": feet_z, "blenderForward": "+Y",
              "fbxRoundTrip": "PASS", **geometry}
    (output_dir / "VERIFICATION.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(name + " PASS", json.dumps(report), flush=True)


def validate(parts):
    for obj in parts:
        mesh = obj.data
        if mesh.validate():
            raise ValueError(obj.name + " required mesh repair")
        mesh.calc_loop_triangles()
        if any(triangle.area <= 1e-10 for triangle in mesh.loop_triangles):
            raise ValueError(obj.name + " has a degenerate triangle")
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bad_edges = sum(not edge.is_manifold for edge in bm.edges)
        bm.free()
        if bad_edges:
            raise ValueError(f"{obj.name} has {bad_edges} open or non-manifold edges")
    return {"closedMeshParts": len(parts), "degenerateTriangles": 0}


def verify_fbx(path, part_count, triangles, bounds):
    original = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_image_search=False)
    imported = set(bpy.data.objects) - original
    meshes = [obj for obj in imported if obj.type == 'MESH']
    try:
        imported_triangles = sum(len(face.vertices) - 2 for obj in meshes for face in obj.data.polygons)
        if len(meshes) != part_count or imported_triangles != triangles:
            raise ValueError("FBX round-trip changed the mesh parts or triangle count")
        points = world_vertices(meshes)
        extents = [max(v[i] for v in points) - min(v[i] for v in points) for i in range(3)]
        if any(abs(expected - actual) > .002 for expected, actual in zip(sorted(bounds), sorted(extents))):
            raise ValueError(f"FBX round-trip changed the dimensions: {extents}")
        materials = {mat for obj in meshes for mat in obj.data.materials if mat is not None}
        has_palette = any(node.type == 'TEX_IMAGE' and node.image and tuple(node.image.size) == (128, 128)
                          for mat in materials if mat.use_nodes for node in mat.node_tree.nodes)
        if not has_palette:
            raise ValueError("FBX round-trip did not preserve the palette image")
    finally:
        for obj in imported:
            bpy.data.objects.remove(obj, do_unlink=True)
