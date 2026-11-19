"""Mesh and hand-painted atlas helpers for the local arena pack."""

import math

import bpy
import numpy as np
from mathutils import Vector

from arena_pack_ground import noise, png, rgb, smooth


COLORS = dict(
    soil='715642', cut='856b50', darksoil='53483b', sand='b5a17a', ochre='b49161', grass='69884a',
    grass_tip='91a960', grass_mid='799554', grass_dark='4e6c41', meadow='879d51', moss='748750',
    lichen='a0a17a', stone='85817a', stone_light='aaa497', stone_dark='686a63', shale='777987',
    gray='5d625f', wood='866044', wood_light='ad7f52', wood_dark='5e483b', wood_old='777064',
    bark='6e5140', bark_dark='514438', birch='c4baa0', bark_mark='655d53', straw='ba9c5d',
    roof='a56950', roof_dark='794f44', slate='616d72', copper='987855', iron='626e72',
    silver='9aa4a0', shadow='3c4543', glass='80a9a6', plaster='c1af8c', cloth='a8a179',
    red='ad6159', cream='d5c6a1', yellow='d3b160', pink='d38c9d', white='dfd8b7',
    purple='9584aa', berry='ac5559', leaf='597744', leaf_tip='88a553', needle='54776a',
    pine='4c6c50', olive='768a4e', teal='6d9b91', turquoise='82b8ad', blue='779eb4',
    foam='bdcdb0', rust='97664d', coal='4d4945', ash='867d6f', bone='c7ba94',
    black='3d423d', green='53705a', water='5b9993', depth='457d77', reed='a3a25f',
)
INDEX = {name: i for i, name in enumerate(COLORS)}


def materials(root):
    block = 128
    atlas = np.zeros((block*8, block*8, 3), np.float32)
    yy,xx=np.mgrid[0:block,0:block]/block
    for i, (name, color) in enumerate(COLORS.items()):
        broad = noise(block, 4, 38+i*23)
        mids = noise(block, 11, 629+i*29)
        streaks = noise(block, 7, 195+i*19, 31)
        strength = 0.10*(broad-0.5)+0.045*(mids-0.5)
        if name.startswith(('wood','bark')) or name=='birch':
            grain=xx*7+.16*np.sin(yy*9)+.10*np.sin(yy*22+xx*5)
            strength+=.055*np.sin(grain*math.tau)-.10*np.exp(-(np.sin(grain*math.pi)/.13)**2)
            knot=np.sqrt(((xx-.38)*1.8)**2+((yy-.55)*.65)**2)
            strength+=.04*np.cos(knot*95)*np.exp(-knot*12)
        elif name in {'roof','roof_dark','slate'}:
            row=np.floor(yy*5)
            seam=np.minimum(np.mod(xx*4+row*.5,1),1-np.mod(xx*4+row*.5,1))
            strength+=.06*np.mod(yy*5,1)-.14*(np.mod(yy*5,1)<.045)-.07*(seam<.025)
        elif name.startswith(('stone','shale')):
            strength+=.045*np.sin(xx*12+yy*7)+.035*(streaks-.5)
        elif name in {'leaf','leaf_tip','grass_mid','pine','needle','meadow'}:
            strength+=.055*np.cos(xx*13+np.sin(yy*10))+.035*np.sin(yy*17)
        row, col = 7-i//8, i%8
        atlas[row*block:(row+1)*block, col*block:(col+1)*block] = rgb(color)*(1+strength[..., None])
    path = root/'ArenaGreen_Atlas.png'
    png(path, atlas)
    image = bpy.data.images.load(str(path), check_existing=True)
    image.pack()
    result = []
    for name, rough, metallic in [('ArenaGreen_Atlas', 0.84, 0), ('ArenaGreen_Water', 0.25, 0),
                                  ('ArenaGreen_Glass', 0.34, 0.08)]:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        tree = mat.node_tree
        shader = next(node for node in tree.nodes if node.type == 'BSDF_PRINCIPLED')
        tex = tree.nodes.new('ShaderNodeTexImage')
        tex.image = image
        tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
        shader.inputs['Roughness'].default_value = rough
        shader.inputs['Metallic'].default_value = metallic
        result.append(mat)
    return result


class Mesh:
    def __init__(self):
        self.vertices, self.faces, self.paint = [], [], []
        self.index = {}
        self.keys = set()

    def face(self, points, color, slot=0):
        indices = []
        for point in points:
            p = tuple(round(float(q), 6) for q in point)
            if p not in self.index:
                self.index[p] = len(self.vertices)
                self.vertices.append(p)
            indices.append(self.index[p])
        key = tuple(sorted(indices))
        if len(set(indices)) == len(indices) and key not in self.keys:
            self.keys.add(key)
            self.faces.append(indices)
            self.paint.append((color, slot))

    def box(self, center, size, color, cap=None, angle=0, slot=0):
        x, y, z = center
        a, b, h = (value/2 for value in size)
        c, s = math.cos(angle), math.sin(angle)
        bottom = [(x+dx*c-dy*s, y+dx*s+dy*c, z-h) for dx, dy in [(-a,-b),(a,-b),(a,b),(-a,b)]]
        top = [(p, q, r+2*h) for p, q, r in bottom]
        self.loft([bottom, top], [color], cap or color, color, slot=slot)

    def loft(self, rings, sides, top, bottom, slot=0, triangulate=False):
        if bottom:
            self.face(rings[0][::-1], bottom, slot)
        for k, (lower, upper) in enumerate(zip(rings, rings[1:])):
            for i in range(len(lower)):
                j = (i+1) % len(lower)
                color = sides[(k*2+i) % len(sides)]
                if triangulate:
                    self.face((lower[i], lower[j], upper[j]), color, slot)
                    self.face((lower[i], upper[j], upper[i]), sides[(k*2+i+1) % len(sides)], slot)
                else:
                    self.face((lower[i], lower[j], upper[j], upper[i]), color, slot)
        if top:
            self.face(rings[-1], top, slot)

    def beam(self, a, b, width, color, depth=None):
        va, vb = Vector(a), Vector(b)
        vec = vb-va
        axis = vec.normalized()
        ref = Vector((0,0,1)) if abs(axis.z) < .88 else Vector((0,1,0))
        u = ref.cross(axis).normalized()*width/2
        v = axis.cross(u).normalized()*(depth or width)/2
        offsets = [-u-v, u-v, u+v, -u+v]
        self.loft([[p+q for q in offsets] for p in (va, vb)], [color], color, color)

    def tube(self, a, b, radius, color, sides=8, taper=1, top=None):
        va, vb = Vector(a), Vector(b)
        axis = (vb-va).normalized()
        ref = Vector((0,0,1)) if abs(axis.z) < .88 else Vector((0,1,0))
        u = ref.cross(axis).normalized()
        v = axis.cross(u)
        rings = []
        for p, r in ((va, radius), (vb, radius*taper)):
            rings.append([p+(u*math.cos(t)+v*math.sin(t))*r for t in [i*math.tau/sides for i in range(sides)]])
        self.loft(rings, [color], top or color, color)

    def lump(self, center, size, colors, rng, n=8, levels=4, lean=0):
        x, y, z = center
        w, d, height = size
        phase = rng.uniform(0, math.tau)
        angles = [phase+i*math.tau/n for i in range(n)]
        rad = [rng.uniform(.77, 1.11) for _ in angles]
        rings = []
        for j in range(levels):
            t = j/(levels-1)
            k = (.74 if j == 0 else (0.96*(1-t)**.55+0.23))
            rings.append([(x+w*(math.cos(a)*k*r+lean*t), y+d*math.sin(a)*k*r,
                           z+height*t*(1+rng.uniform(-.12,.12))) for a,r in zip(angles,rad)])
        self.loft(rings, [colors[0]], colors[-1], colors[0], triangulate=True)

    def normalize(self, dimensions, base=0, ground=None):
        lo = [min(p[i] for p in self.vertices) for i in range(3)]
        hi = [max(p[i] for p in self.vertices) for i in range(3)]
        scale = [size/max(0.0001, b-a) if size else 1 for a,b,size in zip(lo,hi,dimensions)]
        origin = [(lo[0]+hi[0])*0.5, (lo[1]+hi[1])*0.5, lo[2] if ground is None else ground]
        self.vertices = [((x-origin[0])*scale[0], (y-origin[1])*scale[1], base+(z-origin[2])*scale[2])
                         for x,y,z in self.vertices]

    def foliage(self, center, size, colors, rng, n=9, levels=5, lean=0):
        x,y,z=center
        w,d,h=size
        phase=rng.uniform(0,math.tau)
        rings=[]
        for j in range(levels):
            t=j/(levels-1)
            radius=.10+.9*math.sin(math.pi*t)**.72
            rings.append([(x+w*radius*math.cos(phase+i*math.tau/n)+lean*t,
                           y+d*radius*math.sin(phase+i*math.tau/n),z+h*t)
                          for i in range(n)])
        tone=colors[min(1,len(colors)-1)]
        self.face(rings[0][::-1],tone)
        for j,(a,b) in enumerate(zip(rings,rings[1:])):
            for i in range(n):
                k=(i+1)%n
                self.face((a[i],a[k],b[k],b[i]),tone)
        self.face(rings[-1],tone)

    def object(self, name, mats):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.vertices, [], self.faces)
        assert not mesh.validate(), name
        mesh.update()
        uv = mesh.uv_layers.new(name='UVMap')
        slots = sorted({slot for _, slot in self.paint})
        x0,y0 = [min(p[k] for p in self.vertices) for k in (0,1)]
        x1,y1 = [max(p[k] for p in self.vertices) for k in (0,1)]
        for slot in slots:
            mesh.materials.append(mats[slot])
        for poly, (color, slot) in zip(mesh.polygons, self.paint):
            assert poly.area > 1e-12, (name, poly.index, self.faces[poly.index])
            poly.material_index = slots.index(slot)
            if name.startswith(('tree_','bush_')) and color in {'leaf','leaf_tip','grass_mid','meadow','pine','needle','grass_dark','grass','moss'}:
                poly.use_smooth=True
            atlas = INDEX[color]
            axis = max(range(3), key=lambda k: abs(poly.normal[k]))
            a,b = [(1,2),(0,2),(0,1)][axis]
            coords = [mesh.vertices[i].co for i in poly.vertices]
            lo = [min(p[k] for p in coords) for k in (a,b)]
            hi = [max(p[k] for p in coords) for k in (a,b)]
            span = max(hi[0]-lo[0],hi[1]-lo[1],1e-6)
            sx = .05*((poly.index*7) % 3)
            sy = .05*((poly.index*13) % 3)
            for loop, p in zip(poly.loop_indices, coords):
                if slot == 1:
                    uv.data[loop].uv = ((atlas%8+.08+.84*(p.x-x0)/max(x1-x0,.001))/8,
                                        (atlas//8+.08+.84*(p.y-y0)/max(y1-y0,.001))/8)
                else:
                    uv.data[loop].uv = ((atlas%8+.08+sx+.72*(p[a]-lo[0])/span)/8,
                                        (atlas//8+.08+sy+.72*(p[b]-lo[1])/span)/8)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        return obj
