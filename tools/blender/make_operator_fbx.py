"""Create the editable Operator rig and an animated, single-mesh Unity FBX."""

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'docs/art/HeroOperator/Blender'
ASSET = ROOT / 'Assets/Art/Characters/OperatorBlender/Operator.fbx'
NAME = 'Operator'
FPS = 60
ART.mkdir(parents=True, exist_ok=True)
ASSET.parent.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.fps = FPS
source = bpy.data.collections.new('Operator | source geometry')
scene.collection.children.link(source)


def linear(channel):
    value = int(channel, 16) / 255
    return value / 12.92 if value < .04045 else ((value + .055) / 1.055) ** 2.4


def material(name, hex_color, metal, roughness, emission=0):
    color = tuple(linear(hex_color[i:i+2]) for i in (0, 2, 4)) + (1,)
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    shader = next(node for node in mat.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = color
    shader.inputs['Metallic'].default_value = metal
    shader.inputs['Roughness'].default_value = roughness
    shader.inputs['Emission Color'].default_value = color
    shader.inputs['Emission Strength'].default_value = emission
    return mat


MATS = [
    material('Graphite Suit', '282C38', .07, .62),
    material('Titanium Armor', 'BEC0CC', .35, .38),
    material('Blue Steel', '535865', .37, .43),
    material('Joint Rubber', '171B23', .02, .70),
    material('Cyan Optics', '04D6FA', .09, .30, 1.1),
    material('Brass Webbing', 'A38C52', .23, .49),
]
SUIT, ARMOR, STEEL, JOINT, LIGHT, BRASS = range(len(MATS))

# Character right is Blender -X. Front is Blender -Y and the floor is Z=0.
# Export/import converts these to Unity +X, +Z and Y=0 respectively.
BONES = {}


def bone(name, parent, head, tail):
    BONES[name] = (parent, head, tail)


bone('Hips', None, (0, .025, 1.03), (0, .018, 1.18))
bone('Spine', 'Hips', (0, .018, 1.17), (0, .014, 1.38))
bone('Chest', 'Spine', (0, .014, 1.36), (0, .008, 1.64))
bone('Head', 'Chest', (0, .002, 1.67), (0, -.008, 1.96))
for side, sign in [('L', 1), ('R', -1)]:
    shoulder = (sign * .294, .013, 1.551)
    elbow = (sign * .354, .016, 1.257)
    wrist = (sign * .376, -.014, 1.023)
    hip = (sign * .134, .028, 1.01)
    knee = (sign * .157, -.010, .562)
    ankle = (sign * .166, .016, .167)
    bone('UpperArm' + side, 'Chest', shoulder, elbow)
    bone('Forearm' + side, 'UpperArm' + side, elbow, wrist)
    bone('Hand' + side, 'Forearm' + side, wrist, (sign * .38, -.033, .903))
    bone('Thigh' + side, 'Hips', hip, knee)
    bone('Shin' + side, 'Thigh' + side, knee, ankle)
    bone('Foot' + side, 'Shin' + side, ankle, (sign * .166, -.179, .086))

rig_data = bpy.data.armatures.new('Operator Skeleton')
rig = bpy.data.objects.new('OperatorRig', rig_data)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, (parent, head, tail) in BONES.items():
    edit_bone = rig_data.edit_bones.new(name)
    edit_bone.head, edit_bone.tail = head, tail
    if parent:
        edit_bone.parent = rig_data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front = True
rig.data.display_type = 'OCTAHEDRAL'


def make_part(name, verts, faces, color_ids, weights, smooth=False):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    source.objects.link(obj)
    obj.parent = rig
    for mat in MATS:
        mesh.materials.append(mat)
    for poly, color in zip(mesh.polygons, color_ids):
        poly.material_index = color
        poly.use_smooth = smooth
    groups = {}
    for i, weights_at_vertex in enumerate(weights):
        for name, weight in weights_at_vertex.items():
            if name not in groups:
                groups[name] = obj.vertex_groups.new(name=name)
            groups[name].add([i], weight, 'REPLACE')
    obj.modifiers.new('Operator skin', 'ARMATURE').object = rig
    return obj


def loft(name, sections, joint, material_id, sides=10, profile=.76, smooth=False):
    """Tapered, faceted cross-sections, with per-ring skin weights at the joints."""
    verts, weights = [], []
    for section in sections:
        z, x, y, width, depth = section[:5]
        w = section[5] if len(section) > 5 else {joint: 1}
        for i in range(sides):
            angle = 2 * math.pi * (i + .5) / sides
            cx, sy = math.cos(angle), math.sin(angle)
            verts.append((x + math.copysign(abs(cx) ** profile, cx) * width,
                          y + math.copysign(abs(sy) ** profile, sy) * depth, z))
            weights.append(w)
    faces = [tuple(reversed(range(sides)))]
    for ring in range(len(sections) - 1):
        a, b = ring * sides, (ring + 1) * sides
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((a+i, a+j, b+j, b+i))
    faces.append(tuple(range(len(verts) - sides, len(verts))))
    return make_part(name, verts, faces, [material_id] * len(faces), weights, smooth)


def plate(name, points, joint, color, bevel=.13, rim=STEEL, shell=.025, rise=.014, face_y=None):
    """A cut armor silhouette with a chamfered rim on the front face."""
    center = sum((Vector(point) for point in points), Vector()) / len(points)
    area = sum(a[0] * b[2] - b[0] * a[2] for a, b in zip(points, points[1:] + points[:1]))
    if area < 0:
        points = list(reversed(points))
    back = [(x, y + shell, z) for x, y, z in points]
    face = []
    for point in points:
        tip = Vector(point).lerp(center, bevel)
        tip.y = face_y if face_y is not None else tip.y - rise
        face.append(tip)
    count = len(points)
    verts = back + points + face
    faces = [tuple(reversed(range(count))), tuple(range(2*count, 3*count))]
    colors = [rim, color]
    for i in range(count):
        j = (i+1) % count
        faces += [(i, j, count+j, count+i), (count+i, count+j, 2*count+j, 2*count+i)]
        colors += [rim, color]
    return make_part(name, verts, faces, colors, [{joint: 1}] * len(verts))


def cuboid(name, center, size, joint, color, angle=0):
    """Lightly chamfered equipment, with roll around the character's front axis."""
    x, y, z = center
    dx, dy, dz = (width * .5 for width in size)
    rotation = Quaternion(Vector((0, 1, 0)), math.radians(angle))
    outline = [(-.72, -1), (.72, -1), (1, -.65), (1, .65), (.72, 1), (-.72, 1), (-1, .65), (-1, -.65)]
    points = [Vector((x, y-dy, z)) + rotation @ Vector((px*dx, 0, pz*dz)) for px, pz in outline]
    return plate(name, points, joint, color, bevel=.1, rim=color, shell=size[1], rise=.003)


def ribbon(name, edge_a, edge_b, joint, color, thickness):
    """A folded band with matching cross-sections and a constant front clearance."""
    front = [v for pair in zip(edge_a,edge_b) for v in pair]
    back = [(x,y+thickness,z) for x,y,z in front]
    end = len(front)
    faces = [(0,end,end+1,1), (end-2,end-1,2*end-1,2*end-2)]
    for i in range(0,end-2,2):
        a,b,c,d = i,i+2,i+3,i+1
        faces.extend(((a,b,c,d), (d+end,c+end,b+end,a+end), (a,a+end,b+end,b), (d,c,c+end,d+end)))
    return make_part(name, front+back, faces, [color]*len(faces), [{joint:1}]*(2*end))


def earpiece(side, sign):
    """Nested bevels and a recessed dark core on the side of the helmet."""
    sides = 12
    sections = [(.126,.061,ARMOR),(.150,.061,STEEL),(.168,.052,STEEL),
                (.174,.044,JOINT),(.171,.035,JOINT),(.171,.024,SUIT)]
    verts = [(x*sign, .010 + radius*.94*math.cos(math.tau*i/sides),
              1.817 + radius*math.sin(math.tau*i/sides))
             for x,radius,_ in sections for i in range(sides)]
    faces = [tuple(reversed(range(sides)))]
    colors = [ARMOR]
    for ring, (_, _, color) in enumerate(sections[:-1]):
        a, b = ring*sides, (ring+1)*sides
        for i in range(sides):
            j = (i+1) % sides
            faces.append((a+i,a+j,b+j,b+i))
            colors.append(color)
    faces.append(tuple(range(len(verts)-sides,len(verts))))
    colors.append(SUIT)
    if sign < 0:
        faces = [tuple(reversed(face)) for face in faces]
    return make_part('19 · circular comms ' + side, verts, faces, colors, [{'Head':1}]*len(verts))


# Body glove is flexible around the spine, while separate armor is bound rigidly.
loft('01 · pressure suit', [(.92,0,.026,.167,.109), (1.01,0,.025,.207,.136),
     (1.105,0,.024,.174,.113), (1.18,0,.015,.154,.107, {'Spine': .75, 'Hips': .25}),
     (1.29,0,.009,.185,.127, {'Spine': .72, 'Chest': .28}),
     (1.43,0,.005,.257,.153, {'Chest': 1}), (1.54,0,.016,.288,.136, {'Chest': 1}),
     (1.626,0,.009,.139,.113, {'Chest': 1})], 'Hips', SUIT, 12, smooth=True)
loft('02 · neck seal', [(1.58,0,.006,.114,.092), (1.63,0,.004,.104,.083),
     (1.71,0,.002,.095,.077, {'Head': .6,'Chest': .4})], 'Chest', JOINT, 10, smooth=True)
plate('03 · forged cuirass', [(-.234,-.102,1.533),(-.171,-.155,1.575),(-.104,-.166,1.541),
      (.125,-.168,1.541),(.175,-.146,1.576),(.236,-.106,1.521),(.194,-.152,1.410),
      (.084,-.171,1.372),(-.089,-.175,1.360),(-.205,-.146,1.424)], 'Chest', ARMOR, bevel=.15, shell=.043, face_y=-.184)
for i in range(3):
    z = 1.18 + i * .054
    plate(f'04 · abdominal laminate {i+1}', [(-.131,-.122,z+.034),(.131,-.122,z+.034),
          (.105,-.132,z),(-.105,-.132,z)], 'Spine', SUIT, rise=.012)
loft('05 · equipment belt', [(1.047,0,.018,.194,.142), (1.058,0,.018,.201,.144),
     (1.106,0,.018,.192,.139)], 'Hips', BRASS, 12)
cuboid('06 · belt buckle', (0,-.136,1.078), (.093,.028,.064), 'Hips', BRASS)
cuboid('06b · buckle face', (0,-.155,1.078), (.065,.016,.044), 'Hips', STEEL)
for side, x in [('L', .165), ('R', -.165)]:
    cuboid('07 · pouch ' + side, (x,-.106,1.058), (.093,.119,.139), 'Hips', SUIT)
    cuboid('07b · pouch flap ' + side, (x,-.168,1.092), (.074,.022,.068), 'Hips', STEEL)
# Strap follows the bevel of the torso rather than passing straight through it.
ribbon('08 · diagonal webbing', [(-.176,-.215,1.352),(-.061,-.250,1.434),(.077,-.250,1.535),(.186,-.175,1.607)],
       [(-.206,-.215,1.389),(-.091,-.250,1.471),(.047,-.250,1.572),(.156,-.175,1.644)], 'Chest', BRASS, .018)
cuboid('09 · chest clasp', (.053,-.261,1.535), (.079,.028,.069), 'Chest', BRASS, 41)
cuboid('10 · clasp inset', (.053,-.278,1.535), (.048,.012,.035), 'Chest', JOINT, 41)
cuboid('11 · battery case', (-.006,.171,1.457), (.34,.157,.345), 'Chest', SUIT)
cuboid('12 · upper power rail', (-.116,.213,1.565), (.102,.100,.345), 'Chest', STEEL, -5)
cuboid('13 · forward charge strip', (-.12,.155,1.640), (.038,.014,.140), 'Chest', LIGHT, -5)
cuboid('14 · rear charge strip', (-.12,.268,1.610), (.038,.014,.200), 'Chest', LIGHT, -5)
cuboid('14b · battery vent plate', (.046,.251,1.449), (.137,.021,.190), 'Chest', STEEL)
for i in range(3):
    cuboid(f'14c · vent louver {i+1}', (.046,.267,1.397+.044*i), (.095,.014,.015), 'Chest', JOINT)

# The squared brow, narrow dipped visor and pale cheek plates define the face.
loft('15 · composite helmet', [(1.665,0,-.038,.058,.075),(1.714,0,-.025,.109,.113),
     (1.799,0,-.005,.139,.135),(1.867,0,.003,.139,.133),(1.940,0,.014,.120,.111),
     (1.988,0,.018,.082,.079),(1.994,0,.020,.061,.061)], 'Head', SUIT, 10, .68)
ribbon('16 · visor gasket', [(-.137,-.112,1.808),(-.099,-.151,1.796),(0,-.154,1.778),(.099,-.151,1.796),(.137,-.112,1.808)],
       [(-.138,-.117,1.853),(-.097,-.154,1.850),(0,-.161,1.837),(.097,-.154,1.850),(.138,-.117,1.853)], 'Head', JOINT, .026)
ribbon('17 · cyan panoramic optic', [(-.124,-.131,1.819),(-.084,-.163,1.808),(0,-.168,1.797),(.084,-.163,1.808),(.124,-.131,1.819)],
       [(-.127,-.132,1.848),(-.082,-.163,1.837),(0,-.170,1.825),(.082,-.163,1.837),(.127,-.132,1.848)], 'Head', LIGHT, .011)
plate('18 · chin guard', [(-.077,-.144,1.782),(0,-.161,1.772),(.077,-.144,1.782),
      (.062,-.143,1.704),(.029,-.144,1.675),(-.029,-.144,1.675),(-.062,-.143,1.704)],
      'Head', SUIT, .18, JOINT, .036, .014)
for side, s in [('L', 1), ('R', -1)]:
    earpiece(side, s)
    plate('20 · crown rail ' + side, [(s*.087,-.063,1.959),(s*.113,-.045,1.956),
          (s*.147,-.026,1.900),(s*.154,-.038,1.863),(s*.124,-.096,1.865),(s*.104,-.107,1.916)],
          'Head', ARMOR, .11, STEEL, .040, .014)
    plate('20b · cheek plate ' + side, [(s*.133,-.085,1.804),(s*.110,-.126,1.793),
          (s*.076,-.145,1.781),(s*.048,-.139,1.700),(s*.073,-.128,1.713),(s*.119,-.105,1.758)],
          'Head', ARMOR, .15, STEEL, .023, .018)

# Arms and legs. The shell shapes and shoulder profiles differ on the two sides.
for side, s in [('L', 1), ('R', -1)]:
    upper, fore, hand = ('UpperArm'+side, 'Forearm'+side, 'Hand'+side)
    thigh, shin, foot = ('Thigh'+side, 'Shin'+side, 'Foot'+side)
    loft('21 · upper sleeve ' + side, [(1.225,s*.35,.015,.062,.065,{fore:.55,upper:.45}),
         (1.29,s*.349,.015,.081,.085),(1.43,s*.317,.014,.102,.099),(1.55,s*.298,.013,.099,.101)],
         upper, SUIT, 10, smooth=True)
    pad = 1 if side == 'L' else 0
    loft('22 · shoulder shell ' + side, [(1.432-.016*pad,s*.334,.008,.074,.101),
         (1.491,s*.329,.004,.104+.016*pad,.138+.017*pad),
         (1.557,s*.320,.011,.107+.025*pad,.144+.025*pad),
         (1.615+.038*pad,s*.303,.011,.067+.022*pad,.093+.021*pad)], upper, ARMOR, 8, .51)
    if side == 'L':
        loft('23 · shoulder second layer', [(1.380,.342,.010,.060,.086),
             (1.400,.339,.010,.081,.108),(1.456,.334,.008,.097,.117),
             (1.484,.331,.010,.093,.105)], upper, ARMOR, 8, .58)
        crest = [(.307,-.176,1.563),(.336,-.173,1.563),(.338,-.142,1.614),(.307,-.144,1.615),
                 (.332,-.087,1.656),(.303,-.089,1.656),(.327,.023,1.656),(.297,.020,1.656)]
        make_part('24 · shoulder position light', crest, [(0,1,2,3),(3,2,4,5),(5,4,6,7)], [LIGHT]*3, [{upper:1}]*8)
    loft('25 · elbow flex seal ' + side, [(1.188,s*.36,.009,.068,.069),
         (1.245,s*.355,.016,.074,.082),(1.306,s*.349,.013,.065,.065,{upper:.72,fore:.28})], fore, JOINT, 10, smooth=True)
    loft('26 · gauntlet ' + side, [(1.042,s*.375,-.007,.067,.068),
         (1.097,s*.371,-.005,.086,.092),(1.195,s*.362,.008,.084,.085),
         (1.224,s*.358,.010,.064,.064)], fore, STEEL, 8)
    plate('27 · outer gauntlet plate ' + side, [(s*.318,-.081,1.197),(s*.407,-.090,1.219),
          (s*.449,-.074,1.153),(s*.436,-.095,1.060),(s*.374,-.105,1.043),
          (s*.333,-.096,1.083),(s*.329,-.096,1.132)], fore, ARMOR, .19, STEEL, .031, .023)
    cuboid('28 · gauntlet optic ' + side, (s*.403,-.122,1.143), (.026,.014,.101), fore, LIGHT, s*8)
    loft('29 · glove ' + side, [(.914,s*.378,-.029,.054,.061),(.939,s*.378,-.025,.070,.066),
         (.995,s*.376,-.016,.065,.063),(1.035,s*.376,-.012,.055,.053)], hand, JOINT, 8, smooth=True)
    cuboid('30 · glove knuckles ' + side, (s*.406,-.073,.972), (.058,.032,.078), hand, ARMOR, s*8)
    cuboid('31 · glove thumb ' + side, (s*.326,-.048,.971), (.043,.069,.073), hand, JOINT, s*16)

    loft('32 · thigh textile ' + side, [(.545,s*.156,-.006,.068,.078,{shin:.52,thigh:.48}),
         (.655,s*.148,-.001,.093,.092),(.795,s*.139,.011,.12,.113),
         (.963,s*.132,.023,.126,.124),(1.031,s*.132,.026,.10,.102)], thigh, SUIT, 10, smooth=True)
    plate('33 · thigh guard ' + side, [(s*.179,-.091,.933),(s*.231,-.077,.942),
          (s*.263,-.049,.835),(s*.229,-.063,.704),(s*.191,-.091,.755)], thigh, ARMOR, .20)
    loft('34 · knee flex ' + side, [(.449,s*.156,-.003,.069,.079),(.543,s*.157,-.010,.085,.089),
         (.59,s*.156,-.008,.084,.086),(.646,s*.153,-.003,.066,.071,{thigh:.68,shin:.32})], shin, JOINT, 10, smooth=True)
    plate('35 · knee impact cap ' + side, [(s*.091,-.078,.598),(s*.148,-.108,.632),
          (s*.211,-.080,.600),(s*.223,-.078,.553),(s*.190,-.111,.478),(s*.136,-.111,.478),
          (s*.085,-.078,.541)], shin, ARMOR, .24, STEEL, .028, .025)
    loft('36 · shin shell ' + side, [(.195,s*.166,.013,.063,.069),(.278,s*.164,.016,.078,.083),
         (.403,s*.159,.018,.094,.103),(.471,s*.156,.015,.082,.082)], shin, SUIT, 10)
    plate('37 · shin plate ' + side, [(s*.108,-.066,.425),(s*.166,-.086,.456),
          (s*.218,-.061,.421),(s*.208,-.063,.274),(s*.171,-.074,.229),(s*.125,-.064,.275)], shin, STEEL, .18)
    loft('38 · boot upper ' + side, [(.041,s*.166,-.055,.099,.157),(.09,s*.166,-.052,.099,.155),
         (.13,s*.166,-.04,.084,.14),(.202,s*.166,.015,.067,.089),(.225,s*.166,.016,.058,.074)], foot, SUIT, 10, .56)
    loft('39 · tread ' + side, [(0,s*.166,-.052,.093,.154),(.018,s*.166,-.057,.105,.163),
         (.054,s*.166,-.057,.106,.164),(.067,s*.166,-.055,.099,.156)], foot, JOINT, 10, .51)
    plate('40 · toe cap ' + side, [(s*.094,-.200,.107),(s*.124,-.211,.128),(s*.208,-.211,.128),
          (s*.243,-.200,.111),(s*.24,-.211,.073),(s*.097,-.211,.073)], foot, ARMOR, .19, face_y=-.225)
    loft('41 · ankle armor collar ' + side, [(.177,s*.166,.012,.077,.091),(.194,s*.166,.012,.092,.100),
         (.239,s*.166,.014,.086,.087),(.253,s*.166,.016,.071,.070)], shin, STEEL, 8, .56)
    cuboid('42 · boot instep strap ' + side, (s*.166,-.146,.126), (.151,.052,.045), foot, STEEL)
    cuboid('43 · boot side optic ' + side, (s*.235,-.081,.150), (.025,.014,.055), foot, LIGHT, s*12)


# V2: deliberate large plates and equipment that remain legible from the arena camera.
# Keep the original rig, material slots, socket positions and two-metre silhouette envelope.
plate('44 · layered sternum shield', [(-.175,-.194,1.548),(0,-.216,1.505),(.175,-.194,1.548),
      (.139,-.209,1.455),(0,-.226,1.405),(-.139,-.209,1.455)], 'Chest', STEEL, .14, ARMOR, .024, .014)
for side, s in [('L',1),('R',-1)]:
    ribbon('45 · clavicle light '+side, [(s*.025,-.221,1.546),(s*.165,-.202,1.579)],
           [(s*.025,-.222,1.557),(s*.165,-.203,1.590)], 'Chest', LIGHT, .009)
    plate('46 · raised collar '+side, [(s*.078,-.071,1.676),(s*.150,-.060,1.642),
          (s*.177,-.082,1.590),(s*.099,-.102,1.607)], 'Chest', STEEL, .14, JOINT, .033, .015)
    # Lower backpack pods replace a tall antenna-like silhouette with a compact power pack.
    cuboid('47 · power pod '+side,(s*.128,.271,1.444),(.133,.112,.244),'Chest',STEEL,s*-6)
    for i in range(3):
        cuboid('48 · pack vent '+side+str(i),(s*.128,.334,1.390+.045*i),(.074,.018,.013),'Chest',JOINT)
    cuboid('49 · pack charge '+side,(s*.128,.337,1.538),(.065,.014,.018),'Chest',LIGHT)
    # Tactical thigh equipment follows the thigh, rather than floating on the pelvis.
    cuboid('50 · thigh utility pouch '+side,(s*.243,.022,.854),(.124,.142,.187),'Thigh'+side,SUIT,s*-7)
    cuboid('51 · pouch armor flap '+side,(s*.247,-.057,.903),(.103,.023,.056),'Thigh'+side,STEEL,s*-7)
    cuboid('52 · knee side brace '+side,(s*.230,.012,.548),(.045,.121,.115),'Shin'+side,STEEL)
    cuboid('53 · boot heel guard '+side,(s*.166,.097,.135),(.122,.044,.110),'Foot'+side,STEEL)
    # Crown plates frame the face and make the helmet recognisable from above.
    plate('54 · crown shell '+side,[(s*.018,-.041,1.985),(s*.070,-.069,1.970),
          (s*.112,-.041,1.936),(s*.090,.042,1.956),(s*.030,.068,1.986)],'Head',ARMOR,.15,STEEL,.016,.007)
    ribbon('55 · optical temple '+side,[(s*.120,-.127,1.841),(s*.153,-.080,1.854)],
           [(s*.120,-.128,1.853),(s*.153,-.081,1.866)],'Head',LIGHT,.008)
plate('56 · angular visor brow',[(-.132,-.133,1.867),(-.081,-.176,1.870),(0,-.189,1.853),
      (.081,-.176,1.870),(.132,-.133,1.867),(.087,-.169,1.846),(0,-.181,1.838),(-.087,-.169,1.846)],
      'Head',STEEL,.1,JOINT,.016,.009)
cuboid('57 · mask respirator',(0,-.173,1.725),(.097,.041,.045),'Head',STEEL)
for x in [-.028,0,.028]:
    cuboid('58 · mask intake '+str(x),(x,-.199,1.726),(.010,.013,.026),'Head',JOINT)
cuboid('59 · forearm tactical display',(.378,-.133,1.155),(.083,.020,.073),'ForearmL',STEEL,8)
cuboid('60 · display face',(.378,-.146,1.155),(.058,.010,.045),'ForearmL',LIGHT,8)
# A readable, non-text shoulder chevron instead of microscopic decals.
plate('61 · shoulder insignia',[(.303,-.167,1.547),(.351,-.166,1.520),(.394,-.151,1.547),
      (.389,-.153,1.523),(.352,-.173,1.497),(.307,-.173,1.520)],'UpperArmL',BRASS,.1,STEEL,.015,.007)
loft('62 · helmet crown spine',[(1.958,0,.020,.038,.102),(1.990,0,.020,.034,.082),
     (2,0,.020,.026,.059)],'Head',STEEL,8,.70)

# V3: compact shoulder silhouette and small secondary bevels on the major hard surfaces.
# Apply in bind pose so the existing bone weights and socket layout are preserved.
bpy.ops.object.select_all(action='DESELECT')
for part in list(source.objects):
    if part.name.startswith(('22 ','23 ','24 ','61 ')):
        sign = 1 if 'UpperArmL' in part.vertex_groups else -1
        center = Vector((sign*.318,.011,1.530))
        for vertex in part.data.vertices:
            delta = vertex.co-center
            vertex.co = center+Vector((delta.x*.90,delta.y*.90,delta.z*.96))
        part.data.update()
    if any(key in part.name for key in ('forged cuirass','layered sternum','composite helmet',
           'shoulder shell','knee impact cap','power pod','thigh utility pouch','angular visor brow')):
        bpy.context.view_layer.objects.active = part
        part.select_set(True)
        bevel = part.modifiers.new('Armor edge finish','BEVEL')
        bevel.width, bevel.segments = .006, 2
        bevel.limit_method = 'ANGLE'
        bevel.angle_limit = .60
        bevel.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        part.select_set(False)


def socket(name, parent, location):
    obj = bpy.data.objects.new(name, None)
    scene.collection.objects.link(obj)
    obj.parent = rig
    obj.parent_type = 'BONE'
    obj.parent_bone = parent
    obj.empty_display_type = 'ARROWS'
    obj.empty_display_size = .085
    bpy.context.view_layer.update()
    obj.matrix_world = Matrix.Translation(Vector(location)) @ Matrix.Rotation(math.pi/2, 4, 'X')
    return obj


weapon_socket = socket('WeaponSocket', 'HandR', (-.377,-.081,.966))
left_socket = socket('LeftHandGrip', 'HandL', (.377,-.087,.962))
back_socket = socket('BackSocket', 'Chest', (0,.27,1.47))


def angles(pitch=0, yaw=0, roll=0):
    x, y, z = (math.radians(v) for v in (pitch, yaw, roll))
    return Quaternion((0,0,1), -y) @ Quaternion((1,0,0), x) @ Quaternion((0,1,0), z)


def pose_delta(name, rotation):
    pb = rig.pose.bones[name]
    rest = pb.bone.matrix_local.to_quaternion()
    pb.rotation_quaternion = rest.inverted() @ rotation @ rest


def world_orientation(pb, orientation):
    rest = pb.bone.matrix_local.to_quaternion()
    parent_delta = pb.parent.matrix.to_quaternion() @ pb.parent.bone.matrix_local.to_quaternion().inverted()
    pb.rotation_quaternion = (parent_delta @ rest).inverted() @ orientation


def point_bone(pb, direction):
    rest = pb.bone.matrix_local.to_quaternion()
    rest_direction = rest @ Vector((0,1,0))
    world_orientation(pb, rest_direction.rotation_difference(direction.normalized()) @ rest)


def two_bone(first_name, second_name, end_name, target, pole, rotation):
    first, second, end = (rig.pose.bones[name] for name in (first_name, second_name, end_name))
    start = first.head.copy()
    aim = Vector(target) - start
    axis = aim.normalized()
    a, b = first.bone.length, second.bone.length
    distance = min(max(aim.length, abs(a-b)+.005), a+b-.003)
    projected = (a*a + distance*distance - b*b) / (2*distance)
    perpendicular = (Vector(pole) - axis * axis.dot(Vector(pole))).normalized()
    hinge = start + axis * projected + perpendicular * math.sqrt(max(0, a*a-projected*projected))
    point_bone(first, hinge-start)
    bpy.context.view_layer.update()
    point_bone(second, start+axis*distance-hinge)
    bpy.context.view_layer.update()
    world_orientation(end, rotation @ end.bone.matrix_local.to_quaternion())


def track(phase, points):
    """Ease between scalar or vector pose landmarks."""
    for (start, first), (end, second) in zip(points, points[1:]):
        if phase <= end:
            t = (phase - start) / (end - start)
            t = t*t*(3-2*t)
            if isinstance(first, tuple):
                return Vector(first).lerp(Vector(second), t)
            return first + (second-first)*t
    return points[-1][1]


def contact_ease(value):
    """Near-linear stance with a soft heel/toe transfer at either end (continuous velocity)."""
    edge = .12
    if value < edge: return .5*value*value/edge/(1-edge)
    if value > 1-edge: return 1-.5*(1-value)**2/edge/(1-edge)
    return (value-.5*edge)/(1-edge)


def smootherstep(value):
    return value*value*value*(value*(value*6-15)+10)


def neutral_pose():
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = Quaternion()
        pb.location = (0,0,0)


def action_pose(name, phase):
    neutral_pose()
    wave = math.sin(2*math.pi*phase)
    locomotion = name in ('Run','RunBack','StrafeLeft','StrafeRight')
    turning = name in ('TurnLeft','TurnRight')
    lean = twist = roll = recoil = 0
    offset = Vector((0,0,-.035))
    if name == 'Idle':
        offset.z += .009 * wave
        lean = 1.5 * wave
    elif locomotion:
        offset.z += -.055 + .016*math.cos(4*math.pi*phase)
        offset.x = .013 * wave
        lean, twist, roll = 8, 4*wave, -2.5*wave
        if name == 'RunBack': lean, twist = -4, 3*wave
        elif name.startswith('Strafe'):
            lean, twist, roll = 5, 5*wave, (-6 if name=='StrafeLeft' else 6)-2*wave
    elif turning:
        sign = 1 if name=='TurnLeft' else -1
        twist, lean, roll = sign*(5+7*wave), 3, -sign*2*wave
        offset.z -= .009*(1-math.cos(4*math.pi*phase))
    elif name == 'Dash':
        momentum = track(phase, [(0,.25),(.19,1),(.68,.94),(1,.18)])
        offset.z = -.048 - .125*momentum
        lean, roll = 37*momentum, -7*momentum
    elif name == 'Throw':
        lean = track(phase, [(0,0),(.30,-12),(.47,10),(.67,17),(1,0)])
        twist = track(phase, [(0,0),(.30,27),(.47,-14),(.67,-21),(1,0)])
        offset.z -= .041 * math.sin(math.pi*phase)
    elif name == 'Hit':
        recoil = track(phase, [(0,0),(.18,1),(.47,.53),(1,0)])
        offset.y = .076*recoil
        offset.z -= .08*recoil
        lean, twist, roll = -22*recoil, -14*recoil, -11*recoil
    else:
        fall = track(phase, [(0,0),(.24,.21),(.52,.56),(.83,1),(1,1)])
        offset = Vector((-.08*fall, .27*fall, -.032-.772*fall))
        pose_delta('Hips', angles(-83*fall, -13*fall, -16*fall))
        pose_delta('Spine', angles(10*fall, 9*fall))
        pose_delta('Chest', angles(8*fall, -5*fall))
        pose_delta('Head', angles(14*fall, 16*fall))
        for side, side_sign in [('L',-1),('R',1)]:
            pose_delta('UpperArm'+side, angles(-23+15*fall, 0, side_sign*(12+43*fall)))
            pose_delta('Forearm'+side, angles(-32+20*fall))
            pose_delta('Thigh'+side, angles((-39 if side == 'L' else -10)*fall, 0, side_sign*13*fall))
            pose_delta('Shin'+side, angles((32 if side == 'L' else 11)*fall))
        rig.pose.bones['Hips'].location = rig.data.bones['Hips'].matrix_local.to_quaternion().inverted() @ offset
        return

    rig.pose.bones['Hips'].location = rig.data.bones['Hips'].matrix_local.to_quaternion().inverted() @ offset
    pose_delta('Hips', angles(lean*.32, -twist*.28, roll*.47))
    pose_delta('Spine', angles(lean*.47, twist*.53, roll*.31))
    pose_delta('Chest', angles(lean*.21, twist*.48, roll*.22))
    pose_delta('Head', angles(-lean*.78, -twist*.74, -roll*.48))
    bpy.context.view_layer.update()

    for side, sign in [('L', 1), ('R', -1)]:
        x, y, z = BONES['Foot'+side][1]
        pitch = 0
        if locomotion:
            step = (phase + (.5 if side == 'L' else 0)) % 1
            if step < .51:
                stance = contact_ease(step/.51)
                y += -.370 + .74 * stance
                pitch = -8 + 17*stance
            else:
                swing = (step-.51)/.49
                eased = smootherstep(swing)
                y += .370 - .74*eased
                z += .140 * math.sin(math.pi*swing)**2
                pitch = 9 - 17*eased
            # Keep the heel/toe above the floor during the rolling contact.
            z += .030 * (math.sqrt(pitch*pitch+1)-1)/8.055
            if name == 'RunBack':
                y = BONES['Foot'+side][1][1] - (y-BONES['Foot'+side][1][1])*.90
                pitch *= -.55
            elif name.startswith('Strafe'):
                displacement = y-BONES['Foot'+side][1][1]
                x += displacement * (-1 if name=='StrafeLeft' else 1) * .90
                y = BONES['Foot'+side][1][1] + (-.045 if side=='L' else .045)
                pitch *= .3
        elif turning:
            sign_turn = 1 if name=='TurnLeft' else -1
            step = (phase + (.5 if side=='L' else 0))%1
            x += sign*.025 + sign_turn*.035*math.sin(2*math.pi*step)
            y += sign*sign_turn*.08*math.cos(2*math.pi*step)
            z += .055*max(0,math.sin(2*math.pi*step))**2
        elif name == 'Dash':
            y += (-.265 if side == 'L' else .27)*momentum
            z += (.17 if side == 'L' else .067)*momentum
            pitch = (8 if side == 'L' else 20)*momentum
        else:
            y += -.041 if side == 'L' else .046
            x += .022 * sign
        two_bone('Thigh'+side, 'Shin'+side, 'Foot'+side, (x,y,z), (0,-1,0), angles(pitch))

    gun = Vector((-.274,-.407,1.324))
    left = Vector((.342,-.197,1.135))
    gun_roll = 0
    if name == 'Idle':
        gun.z += .009*wave
        left.z += .012*wave
    elif locomotion or turning:
        gun += Vector((-.010*wave, .043, -.041+.016*math.cos(4*math.pi*phase)))
        left += Vector((.012, -.075*wave, .028+.018*wave))
        gun_roll = 3
    elif name == 'Dash':
        gun += Vector((-.013,.074,.017))*momentum
        left += Vector((.055,.39,-.005))*momentum
        gun_roll = 12*momentum
    elif name == 'Throw':
        gun += Vector((-.035,.075,-.048))*math.sin(math.pi*phase)
        left = track(phase, [(0,(.342,-.197,1.135)),(.18,(.428,-.064,1.478)),
              (.35,(.32,.128,1.915)),(.47,(.257,-.369,1.786)),
              (.66,(.207,-.402,1.399)),(1,(.342,-.197,1.135))])
    elif name == 'Hit':
        gun += Vector((-.055,.152,.126))*recoil
        left += Vector((.141,.086,.103))*recoil
        gun_roll = 20*recoil
    left_pitch = track(phase, [(0,0),(.35,-47),(.47,-12),(.65,36),(1,0)]) if name == 'Throw' else 0
    two_bone('UpperArmR','ForearmR','HandR',gun,(-.63,0,-1),angles(-3,0,gun_roll))
    two_bone('UpperArmL','ForearmL','HandL',left,(.68,.04,-1),angles(left_pitch,0,-12))


def create_actions():
    rig.animation_data_create()
    durations = {'Idle':144, 'Run':38, 'Dash':13, 'Throw':29, 'Hit':14, 'Death':45,
                 'RunBack':34,'StrafeLeft':34,'StrafeRight':34,'TurnLeft':44,'TurnRight':44}
    for name, span in durations.items():
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        rig.animation_data.action = action
        last_rotations = {}
        frames = sorted({*range(0,span,2), span, round(span*.47)})
        for frame in frames:
            scene.frame_set(frame+1)
            action_pose(name, frame/span)
            for pb in rig.pose.bones:
                rotation = pb.rotation_quaternion.copy()
                if pb.name in last_rotations and last_rotations[pb.name].dot(rotation) < 0:
                    rotation.negate()
                    pb.rotation_quaternion = rotation
                last_rotations[pb.name] = rotation
                pb.keyframe_insert('rotation_quaternion', frame=frame+1, group=pb.name)
            rig.pose.bones['Hips'].keyframe_insert('location', frame=frame+1, group='Hips')
        looping = name in ('Idle','Run','RunBack','StrafeLeft','StrafeRight','TurnLeft','TurnRight')
        for curve in action.fcurves:
            keys = curve.keyframe_points
            for key in keys:
                key.interpolation = 'BEZIER'
                key.handle_left_type = key.handle_right_type = 'AUTO_CLAMPED'
            if looping and len(keys)>3:
                first, last = keys[0], keys[-1]
                left_dt = keys[1].co.x-first.co.x
                right_dt = last.co.x-keys[-2].co.x
                slope = (keys[1].co.y-keys[-2].co.y)/(left_dt+right_dt)
                for key, dt in ((first,left_dt),(last,right_dt)):
                    key.handle_left_type = key.handle_right_type = 'FREE'
                    key.handle_left = key.co-Vector((dt/3,slope*dt/3))
                    key.handle_right = key.co+Vector((dt/3,slope*dt/3))
    rig.animation_data.action = None
    neutral_pose()
    bpy.context.view_layer.update()
    return durations


durations = create_actions()
scene.frame_end = durations['Idle']+1


def select_rig():
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig


def camera_at(name, position, focus, size):
    data = bpy.data.cameras.new(name)
    camera = bpy.data.objects.new(name, data)
    studio.objects.link(camera)
    camera.location = position
    camera.rotation_euler = (Vector(focus)-camera.location).to_track_quat('-Z','Y').to_euler()
    data.type = 'ORTHO'
    data.ortho_scale = size
    return camera


def light(name, position, energy, color, size):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.color, data.size = energy, color, size
    obj = bpy.data.objects.new(name,data)
    studio.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector((0,0,1))-obj.location).to_track_quat('-Z','Y').to_euler()


studio = bpy.data.collections.new('Studio | previews only')
scene.collection.children.link(studio)
studio.hide_select = True
hero_camera = camera_at('Portrait', (-1.60,-6.8,2.24), (0,0,1.015), 2.37)
top_camera = camera_at('Overhead', (-1.0,-2.25,4.7), (0,0,1.04), 2.20)
rear_camera = camera_at('Rear', (2.9,4.4,2.62), (0,0,1.05), 2.58)
light('Key',(-3.7,-4.5,5.2), 660, (.96,.97,1), 3.4)
light('Fill',(3.7,-1.8,2.8), 240, (.91,.94,1), 3.5)
light('Edge',(.2,3.1,3.7), 515, (.83,.87,1), 2.6)
floor_mesh = bpy.data.meshes.new('Studio floor')
floor_mesh.from_pydata([(-200,-200,-.005),(200,-200,-.005),(200,200,-.005),(-200,200,-.005)], [], [(0,1,2,3)])
floor = bpy.data.objects.new('Studio floor', floor_mesh)
studio.objects.link(floor)
floor_mesh.materials.append(material('Studio slate','373944',.05,.76))
scene.world = bpy.data.worlds.new('Studio world')
scene.world.color = (.22,.22,.22)
scene.camera = hero_camera
scene.render.engine = 'CYCLES'
scene.cycles.samples = 20
if '--quick' in sys.argv: scene.cycles.samples = 8
scene.cycles.use_denoising = True
scene.render.threads_mode = 'FIXED'
scene.render.threads = 8
scene.render.resolution_x, scene.render.resolution_y = 720, 860
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'


def apply_action(name, frame):
    rig.animation_data.action = bpy.data.actions[name]
    scene.frame_set(frame)
    bpy.context.view_layer.update()


def presentation_pose():
    """Use a relaxed stance for the same silhouette comparison as the reference."""
    rig.animation_data.action = None
    neutral_pose()
    hips = rig.pose.bones['Hips']
    hips.location = hips.bone.matrix_local.to_quaternion().inverted() @ Vector((0,0,-.026))
    bpy.context.view_layer.update()
    for side, sign in [('L',1),('R',-1)]:
        two_bone('Thigh'+side,'Shin'+side,'Foot'+side,(sign*.237,-.024,.167),
                 (0,-1,0),angles(0,sign*-6,0))
        two_bone('UpperArm'+side,'Forearm'+side,'Hand'+side,(sign*.432,-.027,1.030),
                 (sign*.7,1,-.3),angles(0,0,sign*10))
    bpy.context.view_layer.update()


apply_action('Idle', 1)
select_rig()
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_distance = 4.2
            area.spaces.active.region_3d.view_location = (0,0,1.05)
            area.spaces.active.region_3d.view_rotation = hero_camera.rotation_euler.to_quaternion()
            area.spaces.active.shading.color_type = 'MATERIAL'
scene['Delivery'] = 'Operator: 2 m. Blender -Y forward. Unity +Z forward. 60 fps. Throw releases at 47 percent.'
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(ART / 'Operator.blend'))

# Join a disposable copy for the FBX, leaving the .blend's panels editable.
rig.animation_data.action = None
neutral_pose()
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
panels = list(source.objects)
for panel in panels:
    panel.select_set(True)
bpy.context.view_layer.objects.active = panels[0]
bpy.ops.object.join()
skin = bpy.context.view_layer.objects.active
skin.name = 'OperatorSkin'
skin.data.name = 'OperatorSkin'
skin.data.calc_loop_triangles()
triangle_count = len(skin.data.loop_triangles)
points = [v.co for v in skin.data.vertices]
assert min(v.z for v in points) == 0 and max(v.z for v in points) == 2
assert all(vertex.groups and abs(sum(w.weight for w in vertex.groups)-1)<1e-5 for vertex in skin.data.vertices)
for obj in (rig, weapon_socket, left_socket, back_socket):
    obj.select_set(True)
for obj in (weapon_socket, left_socket, back_socket):
    # FBX nulls get the opposite local Z on Unity's handedness conversion.
    obj.matrix_basis = obj.matrix_basis @ Matrix.Rotation(math.pi, 4, 'Y')
bpy.context.view_layer.objects.active = rig
# Unity preserves horizontal X and maps Blender +Y to +Z for this FBX axis setup.
rig.rotation_euler.z = math.pi
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=str(ASSET), use_selection=True, object_types={'ARMATURE','MESH','EMPTY'},
    axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', add_leaf_bones=False,
    use_triangles=True, mesh_smooth_type='FACE', bake_anim=True, bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True, bake_anim_step=1,
    bake_anim_simplify_factor=0, path_mode='STRIP')
rig.rotation_euler.z = 0
bpy.context.view_layer.update()

manifest = {
    'triangles': triangle_count, 'vertices': len(points), 'bones': len(rig.pose.bones),
    'materials': len(MATS), 'height_m': 2, 'sole_z_blender': 0, 'forward_blender': '-Y',
    'sockets': {o.name: o.parent_bone for o in (weapon_socket,left_socket,back_socket)},
    'clips_seconds': {name: frames/FPS for name,frames in durations.items()},
}
print('OPERATOR_SOURCE', json.dumps(manifest), flush=True)
(ART / 'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n', encoding='utf-8')

for filename, camera, animation, frame in (
    ('operator-front.png', hero_camera, None, 1),
    ('operator-overhead.png', top_camera, None, 1),
    ('operator-back.png', rear_camera, None, 1),
    ('operator-dash.png', hero_camera, 'Dash', 5),
    ('operator-throw.png', hero_camera, 'Throw', 14),
):
    if animation is None:
        presentation_pose()
    else:
        apply_action(animation, frame)
    scene.camera = camera
    scene.render.filepath = str(ART / filename)
    bpy.ops.render.render(write_still=True)
