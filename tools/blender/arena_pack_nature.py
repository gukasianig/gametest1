"""Landforms, water shapes, rocks and plants for the green arena."""

import math

from arena_pack_mesh import Mesh


def hill_profile(style, x, y, rng, variant):
    r = math.hypot(x, y)
    jitter = .08*math.sin(7*x+variant)*math.cos(8*y-variant)+.05*math.cos(11*x-8*y)
    if style == 'crater_dry':
        return .1+.82*math.exp(-((r-.61+.06*math.sin(5*x))/.14)**2)+jitter*.55
    if style == 'sinkhole':
        return .81-.76*math.exp(-(r/.51)**6)+jitter
    if style == 'cliff_wall_straight':
        return .09+.83*min(1,max(0,(y+.16+.065*math.sin(6*x))/.22))+jitter
    if style.startswith('cliff_wall_corner'):
        depth = min(x,y) if style.endswith('in') else max(x,y)
        return .07+.86*min(1,max(0,(depth+.17)/.28))+jitter
    if style == 'cliff_ledge':
        return .08+.78*min(1,max(0,(y+.43+.1*math.cos(x*3))/.30))+jitter
    if style == 'ramp_dirt':
        return .025+.91*(y+1)/2+jitter*.13
    if style in {'bank_river_straight', 'bank_river_curve'}:
        curve = .32*(x*x-.3) if style.endswith('curve') else 0
        return .08+.83*min(1,max(0,(y+curve+.50)/1.32))+jitter*.24
    if style == 'waterfall_cliff':
        return .07+int(max(0,min(.99,(y+1)/2))*5)*.19+jitter*.58
    if style == 'canyon_segment':
        return .10+.74*min(1,max(0,(abs(x+.19*math.sin(y*2.5))-.28)/.39))+jitter
    if style in {'mesa_flat', 'plateau_step'}:
        peak = max(0,1-math.hypot(x*1.12,y*1.12)+jitter)
        if style == 'mesa_flat':
            return min(.91,peak*2.1)
        return max(0,int(peak*4.1)*.24+jitter*.45)
    if style.startswith('mountain_far'):
        spread = ((-.55,.48),(-.48,.21),(-.2,.62),(-.7,.25))[variant%4]
        return max(0, max(.96*(1-math.hypot((x-spread[0])/.66,(y+.07)/1.08)),
                          .79*(1-math.hypot((x-spread[1])/.66,(y-.13)/1.11)))+jitter)
    if style == 'ridge_rocky':
        return max(0,1-abs(y+.15*math.sin(x*7))**.71)*max(0,.84-abs(x)**1.7*.67+math.sin(x*22)*.16)+jitter
    if style.startswith('dune'):
        crest = .40+.32*math.cos(x*2.8)
        return max(0, .84-abs(y-crest+.36)**1.24-.42*abs(x)**2.5)+jitter*.45
    return max(0,1-math.hypot(x*.97,y*1.05)**1.55)**1.32+jitter


def terrain(style, size, budget, rng, variant):
    mesh = Mesh()
    n = min(29, max(8,int(math.sqrt(budget/2))-3))
    width, depth, height = size
    rows = []
    color = 'sand' if style.startswith('dune') else 'cut' if style == 'ramp_dirt' else 'grass'
    rocky = style.startswith(('ridge','cliff_wall','mountain','mesa','canyon','waterfall'))
    for j in range(n+1):
        row = []
        for i in range(n+1):
            x, y = 2*i/n-1,2*j/n-1
            if 0<i<n:
                x += rng.uniform(-.27,.27)/n
            if 0<j<n:
                y += rng.uniform(-.27,.27)/n
            h = max(.024,hill_profile(style,x,y,rng,variant))
            row.append((x*width/2,y*depth/2,h*height))
        rows.append(row)
    highest = max(p[2] for row in rows for p in row)
    rows = [[(x,y,z*height/highest) for x,y,z in row] for row in rows]
    for j in range(n):
        for i in range(n):
            a,b,c,d = rows[j][i],rows[j][i+1],rows[j+1][i+1],rows[j+1][i]
            slope = max(p[2] for p in (a,b,c,d))-min(p[2] for p in (a,b,c,d))
            palette = ('stone','stone_dark','shale','gray') if rocky and slope > height/n*.47 else (
                ('stone','stone_dark','shale','gray','grass_dark') if rocky else
                ('sand','ochre','sand','sand') if color == 'sand' else
                ('cut','soil','cut') if color == 'cut' else ('grass','grass','grass_mid','meadow','grass_dark'))
            # Broad connected regions; random per-triangle colors read as camouflage.
            field=math.sin(i/n*4.1)+.6*math.cos(j/n*4.8)
            pigment=palette[0] if field<.7 else palette[1]
            mesh.face((a,b,c),pigment)
            mesh.face((a,c,d),pigment)
    edge = rows[0][:-1]+[row[-1] for row in rows[:-1]]+rows[-1][:0:-1]+[row[0] for row in rows[:0:-1]]
    lower = [(x,y,-.5) for x,y,z in edge]
    mesh.loft([lower,edge], ['darksoil','cut','stone_dark'] if rocky else ['darksoil','soil','cut'], None, None)
    for i in range(len(lower)):
        mesh.face((lower[(i+1)%len(lower)], lower[i], (0,0,-.5)), 'darksoil')
    return mesh


def water_flat(mesh, size, segments=32):
    for i in range(segments):
        for j in range(segments):
            x,y = size*(i/segments-.5),size*(j/segments-.5)
            d=size/segments
            mesh.face(((x,y,0),(x+d,y,0),(x+d,y+d,0),(x,y+d,0)),'water',1)


def pond(mesh, dimensions, rng, puddle=False, marsh=False, lagoon=False):
    width, depth = dimensions[:2]
    n = 27 if not puddle else 22
    angles = [math.tau*i/n for i in range(n)]
    def rim(factor, height):
        return [(width*.5*factor*math.cos(a)*r,depth*.5*factor*(math.sin(a)*r+(math.cos(a)**2*.31 if lagoon else 0)),height)
                for a,r in zip(angles, radii)]
    radii = [1+.11*math.sin(5*a)+.065*rng.uniform(-1,1) for a in angles]
    h = .012 if puddle else .25
    outer,inner = rim(1,0),rim(.84,h*.45)
    bottom = [(x,y,-h*.51) for x,y,z in outer]
    mesh.loft([bottom,outer,inner], ['soil','cut','grass_dark','sand'] if not puddle else ['soil','cut'], None, 'darksoil')
    water = rim(.858,h*.46)
    for i in range(n):
        j=(i+1)%n
        mesh.face((water[i],water[j],(0,0,h*.46)), 'water',1)
    if marsh:
        for i in range(11):
            a=rng.uniform(0,math.tau)
            d=rng.uniform(.15,.39)
            mesh.lump((math.cos(a)*width*d,math.sin(a)*depth*d,h*.34),(width*.065,depth*.065,h*1.6),
                      ['grass_dark','meadow','reed'],rng,6,3)


def river(mesh, width, length, curve=0, end=False):
    steps=24 if curve else 20
    rows=[]
    for i in range(steps+1):
        t=i/steps
        if curve:
            radius=width*1.5 if curve==90 else width*2.2
            a=math.radians(curve)*t
            x,y=math.cos(a)*radius,math.sin(a)*radius
            nx,ny=math.cos(a),math.sin(a)
        else:
            x,y=0,length*(t-.5)
            nx,ny=1,0
        edge=width*.5*(1 if not end else .73+.36*t)
        span=(1-.06*math.sin(math.pi*t))
        rows.append([(x+nx*edge*q*span,y+ny*edge*q*span,z) for q,z in [(-1,0),(-.76,.28),(-.60,.065),
                          (-.18,.065),(.18,.065),(.60,.065),(.76,.28),(1,0)]])
    for a,b in zip(rows,rows[1:]):
        for j in range(7):
            is_water=j in (2,3,4)
            mesh.face((a[j],a[j+1],b[j+1],b[j]), 'water' if is_water else
                      ('sand' if j in (1,5) else 'grass_dark'),1 if is_water else 0)
    mesh.ports=[]
    for row,sign in ((rows[0],-1),(rows[-1],1)):
        center=tuple((row[0][k]+row[-1][k])/2 for k in range(3))
        across=(row[-1][0]-row[0][0],row[-1][1]-row[0][1])
        norm=math.hypot(*across)
        mesh.ports.append(dict(position=list(center),outward=[-sign*across[1]/norm,sign*across[0]/norm,0],
                               profile=[list(p) for p in row]))


def river_fork(mesh):
    # Three open 10 m sockets. Water and bank heights match river().
    rings=[]
    for width,z in ((3,.065),(3.8,.28),(5,0)):
        ring=[]
        for i in range(3):
            a=-math.pi/2+i*math.tau/3
            c,s=math.cos(a),math.sin(a)
            ring.extend([(15*c+width*s,15*s-width*c,z),(15*c-width*s,15*s+width*c,z),
                         (2*width*math.cos(a+math.pi/3),2*width*math.sin(a+math.pi/3),z)])
        rings.append(ring)
    mesh.face(rings[0],'water',1)
    for inner,outer,color in ((rings[0],rings[1],'sand'),(rings[1],rings[2],'grass_dark')):
        for i in range(9):
            if i%3==0:  # Open connector: never put a bank across a river mouth.
                continue
            j=(i+1)%9
            mesh.face((inner[i],outer[i],outer[j],inner[j]),color)
    mesh.ports=[]
    for i in range(3):
        a=-math.pi/2+i*math.tau/3
        c,s=math.cos(a),math.sin(a)
        profile=[[15*c-s*x,15*s+c*x,z] for x,z in [(-5,0),(-3.8,.28),(-3,.065),
                  (-.9,.065),(.9,.065),(3,.065),(3.8,.28),(5,0)]]
        mesh.ports.append(dict(position=[15*c,15*s,0],outward=[c,s,0],profile=profile))


def lake_bank(mesh, variant):
    rows=[]
    for i in range(21):
        t=i/20
        x=12*(t-.5)
        bank=math.sin(math.pi*t)*((variant-2.5)*.48+.48*math.sin(t*math.tau+variant))
        rows.append([(x,y,z) for y,z in [(-4,0),(bank-.38,0),(bank,0),(bank+.36,.25),(bank+1.22,.36),(4,.30)]])
    for a,b in zip(rows,rows[1:]):
        for j,color in enumerate(('water','water','sand','ochre','grass_dark')):
            mesh.face((a[j],b[j],b[j+1],a[j+1]),color,1 if j<2 else 0)


def water_asset(style, size, rng, variant):
    mesh=Mesh()
    if style == 'water_plane_square':
        water_flat(mesh, size[0])
    elif style.startswith(('pond','puddle','marsh','oxbow','spring')):
        pond(mesh, size, rng, style.startswith('puddle'), style.startswith('marsh'), style.startswith('oxbow'))
        if style=='spring_pool':
            for i in range(9):
                a=math.tau*i/9
                mesh.lump((1.68*math.cos(a),1.68*math.sin(a),0),(.32,.27,.30),['stone_dark','stone','lichen'],rng,6,3)
    elif style=='ford_stones':
        river(mesh, size[1], size[0])
        for i in range(5):
            mesh.lump((0,i*.95-1.9,.07),(.38,.39,.17),['stone_dark','stone','lichen'],rng,6,3)
    elif style.startswith('lake_edge'):
        lake_bank(mesh, variant)
    elif style=='river_fork_Y':
        river_fork(mesh)
    else:
        curve=90 if style.endswith('90') else 45 if style.endswith('45') else 0
        width = 10 if style.startswith('river') else size[0]
        river(mesh,width,size[1],curve,end=style=='river_delta_end')
    return mesh


def blade(mesh, x, y, height, width, bend, angle, color):
    c,s = math.cos(angle),math.sin(angle)
    rows=[]
    for t,w in [(0,1),(.45,.78),(.76,.48)]:
        d=bend*t*t
        rows.append(((x+c*d-s*width*w,y+s*d+c*width*w,height*t),
                     (x+c*d,y+s*d,height*t+width*.32),
                     (x+c*d+s*width*w,y+s*d-c*width*w,height*t)))
    tip=(x+c*bend,y+s*bend,height)
    for a,b in zip(rows,rows[1:]):
        mesh.face((a[0],a[1],b[1],b[0]),color)
        mesh.face((a[1],a[2],b[2],b[1]),'grass_mid')
    mesh.face((rows[-1][0],rows[-1][1],tip),color)
    mesh.face((rows[-1][1],rows[-1][2],tip),'grass_tip')


def blossom(mesh,x,y,z,r,color,petals=5):
    for i in range(petals):
        a=i*math.tau/petals
        c,s=math.cos(a),math.sin(a)
        mesh.face(((x,y,z),(x+c*r*.73-s*r*.30,y+s*r*.73+c*r*.30,z+.016),
                   (x+c*r,y+s*r,z+.027),(x+c*r*.73+s*r*.30,y+s*r*.73-c*r*.30,z+.016))[::-1],color)
    mesh.tube((x,y,z),(x,y,z+.024),r*.27,'yellow',5)


def tree(style, mesh, rng, variant):
    bark='birch' if 'birch' in style else 'bark_dark' if 'dead' in style else 'bark'
    top=(rng.uniform(-.24,.24),rng.uniform(-.17,.17),3.85)
    mesh.tube((0,0,0),top,.26,bark,9,.44)
    if 'birch' in style:
        for i in range(12):
            z=.25+i*.26
            a=rng.uniform(0,math.tau)
            radius=.26-.12*z/3.85
            mesh.box((top[0]*z/3.85+math.cos(a)*radius,top[1]*z/3.85+math.sin(a)*radius,z),(.14,.019,.045),'bark_mark',angle=a+math.pi/2)
    if 'pine' in style or 'spruce' in style:
        for level in range(5):
            z=.89+level*.57
            r=1.42-level*.23
            mesh.foliage((0,0,z),(r,r*.86,1.32),['pine','needle','leaf','grass_dark'],rng,10,5,.11)
        return
    for i in range(7):
        a=i*math.tau/7+variant*.6
        z=1.03+i*.27
        r=rng.uniform(.75,1.6)
        x,y=math.cos(a)*r,math.sin(a)*r
        end=(x,y,z+rng.uniform(.72,1.30))
        mesh.tube((top[0]*z/3.85,top[1]*z/3.85,z),end,.108,bark,7,.24)
        if 'dead' in style:
            for direction in (-1,1):
                xx,yy=math.cos(a+direction*.52)*.72,math.sin(a+direction*.52)*.72
                mesh.tube((x*.64,y*.64,z+.55),(x+xx,y+yy,end[2]+.24),.041,bark,5,.12)
        else:
            w=.63 if 'birch' in style else .97
            leaves=['leaf','grass_mid','leaf_tip','meadow'] if 'willow' not in style else ['grass_dark','leaf','pine','grass_mid']
            mesh.foliage((x,y,end[2]-.21),(w,w*.84,1.14 if 'birch' in style else .90),leaves,rng,9,5)
            if 'willow' in style:
                for j in range(3):
                    b=a+j*1.62
                    xx,yy=x+math.cos(b)*.47,y+math.sin(b)*.46
                    mesh.foliage((xx,yy,end[2]-1.57),(.27,.30,1.82),['grass_dark','leaf','grass_mid'],rng,6,4,.11)
    if 'dead' not in style:
        mesh.foliage((top[0],top[1],3.18),(.97,.93,1.41),['leaf','grass','meadow'],rng,10,5)


def plant(style, size, rng, variant):
    mesh=Mesh()
    if style.startswith(('tree_', 'palm_', 'bamboo_')):
        if style.startswith('palm'):
            mesh.tube((0,0,0),(.22,0,2.43),.13,'wood',8,.63)
            for i in range(11):
                a=i*math.tau/11
                mesh.beam((.22,0,2.42),(.22+math.cos(a)*.63,math.sin(a)*.63,2.84),.10,'leaf')
                mesh.foliage((.22+math.cos(a)*.82,math.sin(a)*.82,2.31),(.43,.28,.63),['leaf','grass_mid','leaf_tip'],rng,6,3)
        elif style.startswith('bamboo'):
            for i in range(8):
                x,y=rng.uniform(-.42,.42),rng.uniform(-.42,.42)
                h=rng.uniform(1.65,2.75)
                mesh.tube((x,y,0),(x+.10,y,h),.045,'reed',6,.72)
                for j in range(3):
                    z=h*(.54+j*.16)
                    mesh.foliage((x+(-1)**j*.19,y,z),(.32,.14,.20),['leaf','leaf_tip'],rng,6,3)
        else:
            tree(style,mesh,rng,variant)
    elif style.startswith(('bush_', 'moss_')):
        if 'dry' in style or 'thorny' in style:
            for i in range(11):
                a=rng.uniform(0,math.tau)
                x,y=math.cos(a)*rng.uniform(.2,.54),math.sin(a)*rng.uniform(.2,.54)
                z=rng.uniform(.49,.92)
                mesh.tube((0,0,0),(x,y,z),.035,'wood_old',5,.19)
                mesh.tube((x*.59,y*.59,z*.59),(x+.15,y-.12,z+.09),.023,'bark',5,.10)
        else:
            for i in range(11 if 'moss' not in style else 3):
                x,y=rng.uniform(-.53,.53),rng.uniform(-.47,.47)
                z=0 if 'moss' in style else rng.uniform(.05,.31)
                radius=rng.uniform(.22,.40)
                mesh.foliage((x,y,z),(radius,radius*.88,rng.uniform(.25,.55)),['grass_dark','leaf','leaf_tip','moss'],rng,
                          5 if 'moss' in style else 8,3 if 'moss' in style else 4)
                if 'flowering' in style and i%2==0:
                    blossom(mesh,x+radius*.41,y,z+.42,.12,['pink','white','yellow'][variant%3])
                if 'berry' in style and i%3==0:
                    mesh.foliage((x+radius*.53,y-.02,z+.36),(.095,.084,.19),['berry','red'],rng,6,3)
    elif style.startswith(('ivy_','vine_')):
        for i in range(3):
            x=(i-1)*.20
            points=[(x+.09*math.sin(j*.92+i),.07*math.sin(j*.53),.15*j) for j in range(8)]
            for a,b in zip(points[::2],points[2::2]):
                mesh.tube(a,b,.014,'bark',4)
            for j,(x,y,z) in enumerate(points[1:]):
                direction=(-1)**j
                mesh.face(((x,y,z),(x+direction*.19,y-.035,z-.062),(x+direction*.29,y-.01,z+.046),
                           (x+direction*.13,y+.031,z+.077)), 'leaf_tip' if j%2 else 'leaf')
    elif style.startswith(('grass_', 'reed_', 'cattail_', 'weeds_', 'fern')):
        ferns=style.startswith('fern')
        reeds=style.startswith(('reed','cattail','grass_wild'))
        count=9 if ferns else 12 if 'grass' in style else 11
        if 'short' in style:
            count=9
        for i in range(count):
            a=math.tau*i/count+rng.uniform(-.12,.12)
            x,y=math.cos(a)*rng.uniform(.02,.16),math.sin(a)*rng.uniform(.02,.16)
            h=rng.uniform(.45,.96)
            blade(mesh,x,y,h,.033 if not ferns else .065,.36 if ferns else .19,a,rng.choice(['leaf','grass','leaf_tip']))
            if reeds and i%3==0:
                mesh.tube((x,y,h*.78),(x,y,h*1.08),.024,'wood_dark' if 'cattail' in style else 'reed',5,.82)
            if style=='weeds_mixed' and i%3==0:
                blossom(mesh,x+.06,y,h,.067,'cream' if i%2 else 'yellow',4)
            if ferns and i%2==0:
                for k in range(4):
                    t=(k+1)/5
                    xx,yy=x+math.cos(a)*.36*t*t,y+math.sin(a)*.36*t*t
                    for direction in (-1,1):
                        tip=(xx+math.cos(a+direction)*.12,yy+math.sin(a+direction)*.12,h*t+.05)
                        mesh.face(((xx,yy,h*t),(xx+.035,yy,h*t+.04),tip),'leaf_tip')
    elif style.startswith(('flower_', 'water_lily', 'lotus_')):
        if style.startswith(('water_lily','lotus_')):
            for i in range(4):
                x,y=rng.uniform(-.53,.53),rng.uniform(-.53,.53)
                r=rng.uniform(.21,.39)
                ring=[(x+r*math.cos(a),y+r*math.sin(a),.013) for a in [k*math.tau/12 for k in range(12)]]
                for k in range(11):
                    mesh.face(((x,y,.016),ring[k],ring[k+1]),'olive' if k%2 else 'leaf')
                if i%2==0:
                    blossom(mesh,x,y,.021,.16,'pink' if i==0 else 'cream')
        else:
            for i in range(8 if 'patch' in style else 1):
                x,y=rng.uniform(-.3,.3),rng.uniform(-.3,.3)
                h=rng.uniform(.35,.7)
                mesh.tube((x,y,0),(x,y,h),.026,'leaf',6,.7)
                blade(mesh,x,y,h*.61,.044,.15,rng.uniform(0,math.tau),'leaf')
                blossom(mesh,x,y,h,.15,('pink','white','yellow','purple','cream','red')[variant%6])
    elif style.startswith('mushroom'):
        for i in range(3 if 'giant' in style else 1):
            x,y=(i-1)*.31,.09*math.sin(i)
            z=rng.uniform(.31,.65)
            mesh.tube((x,y,0),(x,y,z),.09,'cream',8,.71)
            mesh.lump((x,y,z-.03),(.24,.22,.19),['wood_dark','red','ochre','roof'],rng,9,4)
    elif style.startswith('cactus'):
        mesh.tube((0,0,0),(0,0,1),.17,'green',9,.89)
        for side in (-1,1):
            z=.31+.15*(side>0)
            mesh.tube((0,0,z),(side*.31,0,z+.13),.092,'olive',7,.79)
            mesh.tube((side*.31,0,z+.13),(side*.31,0,z+.49),.071,'leaf',7,.68)
    else:
        if style.startswith('stump'):
            mesh.tube((0,0,0),(0,0,1),.47,'bark',11,.76,'wood_light')
        elif style.startswith('log'):
            mesh.tube((-1,0,.28),(1,0,.39),.30,'bark',10,.78,'wood_light')
        for i in range(5):
            a=i*math.tau/5
            mesh.tube((0,0,.27),(math.cos(a)*.84,math.sin(a)*.74,0),.105,'bark_dark',6,.24)
    return mesh


def rock_asset(style, size, rng, variant):
    mesh=Mesh()
    if style in {'rock_arch_natural','cave_entrance'}:
        for i in range(10):
            a=i*math.pi/9
            x,z=math.cos(a)*1.72,math.sin(a)*1.43
            mesh.lump((x,0,max(0,z-.27)),(.46,.58,.61),['gray','stone_dark','shale','lichen'],rng,9,4,.21)
        if style=='cave_entrance':
            mesh.box((0,.54,.67),(2.42,.11,1.42),'shadow')
    elif style=='stone_circle_8':
        for i in range(8):
            a=i*math.tau/8
            mesh.lump((math.cos(a)*2,math.sin(a)*2,0),(.43,.43,rng.uniform(.74,1.20)),['stone_dark','stone','lichen'],rng,8,4)
    elif 'crystal' in style:
        for i in range(8):
            x,y=rng.uniform(-.5,.5),rng.uniform(-.5,.5)
            mesh.lump((x,y,0),(.19,.18,rng.uniform(.46,1.2)),['shale','teal','turquoise','blue'],rng,6,3,.34)
    else:
        count=9 if 'pebble' in style else 6 if 'cluster' in style else 3 if 'large' in style else 1
        if 'pebble' in style:
            count=3
        palette = ['stone_dark','stone','stone_light','lichen'] if 'mossy' not in style else ['stone_dark','stone','moss','leaf']
        for i in range(count):
            x,y=(0,0) if i==0 else (rng.uniform(-.7,.7),rng.uniform(-.66,.66))
            r=rng.uniform(.38,.79)*(1 if i==0 else .67)
            mesh.lump((x,y,0),(r,r*rng.uniform(.61,1),rng.uniform(.56,1.22)*(1 if i==0 else .48)),palette,rng,
                      6 if 'pebble' in style else 10, 3 if 'pebble' in style else 5,rng.uniform(-.24,.24))
    return mesh
