"""Rural and industrial construction, crossings and small props."""

import math

from arena_pack_mesh import Mesh


def bridge(style, size, rng):
    mesh=Mesh()
    w,l,h=size
    if style in {'log_bridge','plank_single'}:
        if style=='plank_single':
            mesh.box((0,0,h/2),(w,l,h),'wood','wood_light')
        else:
            mesh.tube((0,-l/2,w/2),(0,l/2,w/2),w/2,'bark',10,.85,'wood_light')
        return mesh
    if style=='stepping_stones_set':
        for i in range(5):
            mesh.lump((rng.uniform(-.17,.17),(i-2)*.85,0),(.37,.35,.25),['stone_dark','stone','lichen'],rng,8,4)
        return mesh
    stone=style=='bridge_stone_arch'
    rope=style=='bridge_rope'
    metal=style=='bridge_metal_truss'
    bays=22 if stone else 18 if l>10 else 12
    def deck(y):
        q=2*y/l
        return (h*.48*(1-q*q)+h*.29 if stone else h*(.45+.18*q*q) if rope else h*.25)
    def bottom(y):
        return max(0,h*.42*(1-(2*y/l/.78)**2)**.5) if abs(y)<l*.39 else 0
    if stone:
        # One closed masonry shell, with per-section tones and no hidden internal walls.
        rings=[]
        for i in range(bays+1):
            y=(i/bays-.5)*l
            rings.append([(w/2,y,bottom(y)),(-w/2,y,bottom(y)),(-w/2,y,deck(y)),(w/2,y,deck(y))])
        mesh.loft(rings,['stone_dark','stone','stone','stone'],'stone','stone_dark')
    else:
        for i in range(bays):
            a,b=(i/bays-.5)*l,((i+1)/bays-.5)*l
            mid=(a+b)/2
            mesh.box((0,mid,deck(mid)),(w,(b-a)*.96,.105 if not metal else .14),
                     'iron' if metal else 'wood_dark',rng.choice(['iron','silver']) if metal else rng.choice(['wood','wood_light','wood_old']))
    n=6 if l>10 else 4
    for side in (-1,1):
        x=side*(w/2-.08)
        previous=None
        for i in range(n):
            y=-l*.48+l*.96*i/(n-1)
            top=deck(y)+h*(.55 if not stone else .22)
            z0=deck(y) if stone else 0 if i in (0,n-1) and not rope else deck(y)-.10
            mesh.box((x,y,(top+z0)/2),(.17,.17,top-z0), 'stone_dark' if stone else 'iron' if metal else 'wood_dark',
                     'stone_light' if stone else 'silver' if metal else 'wood_light')
            if previous:
                a,z=previous
                if rope:
                    mid=(x,(y+a)/2,(top+z)/2-.09)
                    mesh.tube((x,a,z),mid,.04,'wood_light',6)
                    mesh.tube(mid,(x,y,top),.04,'wood_light',6)
                else:
                    mesh.beam((x,a,z-.04),(x,y,top-.04),.11,'stone' if stone else 'iron' if metal else 'wood')
                    if metal:
                        mesh.beam((x,a,z),(x,y,deck(y)+.12),.13,'iron')
            previous=y,top
        if not stone:
            mesh.beam((x,-l/2,deck(-l/2)-.075),(x,l/2,deck(l/2)-.075),.16,'wood_dark' if not metal else 'shadow')
    return mesh


def gable(mesh, w, d, eave, top, tone):
    points=[(-w/2,-d/2,eave),(w/2,-d/2,eave),(0,-d/2,top)]
    mesh.face(points,'plaster')
    mesh.face([(x,d/2,z) for x,y,z in points[::-1]],'plaster')
    mesh.face(((-w/2,d/2,eave),(-w/2,-d/2,eave),(0,-d/2,top),(0,d/2,top)),tone)
    mesh.face(((0,d/2,top),(0,-d/2,top),(w/2,-d/2,eave),(w/2,d/2,eave)),tone)
    for y in (-d/2,d/2):
        mesh.beam((-w/2,y,eave),(0,y,top),.11,'wood_dark')
        mesh.beam((0,y,top),(w/2,y,eave),.11,'wood_dark')
    for i in range(1,5):
        x=w*.5*i/5
        z=top+(eave-top)*i/5+.016
        for side in (-1,1):
            mesh.beam((side*x,-d/2+.045,z),(side*x,d/2-.045,z),.055,'roof_dark' if tone=='roof' else 'shadow')
    mesh.beam((0,-d/2,top+.02),(0,d/2,top+.02),.1,'roof_dark' if tone=='roof' else 'slate')


def opening(mesh,x,y,z,w,h,door=False):
    mesh.box((x,y,z),(w+.16,.12,h+.15),'wood_dark')
    mesh.box((x,y-.078,z),(w,.06,h),'wood' if door else 'glass',slot=0 if door else 2)
    mesh.box((x,y-.115,z),(.056,.038,h),'wood_dark')
    if door:
        for offset in (-.3,.29):
            mesh.box((x,y-.114,z+h*offset),(w,.05,.081),'wood_light')
        mesh.box((x+w*.28,y-.15,z),(.078,.035,.078),'iron')
    else:
        mesh.box((x,y-.115,z),(w,.038,.051),'wood_dark')
        mesh.box((x,y-.124,z-h*.52),(w+.16,.18,.075),'stone_light')


def masonry(mesh,w,d,h,rng,base=0):
    mesh.box((0,0,base+h/2),(w,d,h),'stone_dark')
    rows=min(10,max(2,round(h/.34)))
    columns=min(12,max(3,round(w/.57)))
    for j in range(rows):
        for i in range(columns):
            x=(i+.5)/columns*w-w/2
            if i not in (0,columns-1):
                x+=.13*(-1)**j
            z=base+(j+.5)*h/rows
            for side in (-1,1):
                mesh.box((x,side*d/2,z),(w/columns*.89,.065,h/rows*.80),rng.choice(['stone','stone_light','gray','lichen']))


def house(style, size, rng):
    mesh=Mesh()
    w,d,h=size
    stone=style in {'house_stone','watermill','pump_station','warehouse_small','crypt_entrance'}
    industrial=style in {'warehouse_small','pump_station'}
    eave=h*(.72 if style not in {'shed_tool','warehouse_small','pump_station'} else .82)
    mesh.box((0,0,.13),(w,d,.26),'stone_dark','stone')
    if stone and not industrial:
        masonry(mesh,w*.96,d*.94,eave-.24,rng,.24)
    else:
        mesh.box((0,0,(eave+.23)/2),(w*.96,d*.94,eave-.23), 'iron' if industrial else 'wood')
        panels=10 if w>7 else 8
        for i in range(panels):
            x=(i+.5)/panels*w*.95-w*.475
            for side in (-1,1):
                mesh.box((x,side*d*.475,(eave+.24)/2),(w*.88/panels,.08,eave-.24),
                         rng.choice(['slate','iron','green'] if industrial else ['wood','wood_light','wood_old']))
    gable(mesh,w*1.035,d*1.07,eave,h,'slate' if industrial or style=='shed_tool' else 'roof')
    for x in (-w*.46,w*.46):
        for y in (-d*.47,d*.47):
            mesh.box((x,y,eave/2),(.14,.14,eave),'wood_dark' if not industrial else 'shadow')
    opening(mesh,0,-d*.49,eave*.32,w*(.39 if style in {'barn','warehouse_small'} else .23),eave*.58,True)
    if style not in {'shed_tool','crypt_entrance'}:
        for x in (-w*.29,w*.29):
            opening(mesh,x,-d*.49,eave*.58,w*.16,eave*.21)
    if style in {'house_wood_small','house_wood_medium','house_stone','pump_station'}:
        z=h*.84
        mesh.box((w*.24,d*.19,(z+h*.99)/2),(.53,.59,h*.99-z),'stone_dark' if not industrial else 'iron')
        mesh.box((w*.24,d*.19,h*.98),(.65,.69,.09),'stone_light' if not industrial else 'silver')
    if style.startswith('house_wood') or style=='barn':
        # Side-wall framing makes the building readable beyond the front elevation.
        for side in (-1,1):
            for z in (.42,eave-.20):
                mesh.box((side*w*.485,0,z),(.11,d*.95,.12),'wood_dark')
            for y in (-d*.22,d*.22):
                mesh.box((side*w*.485,y,eave*.5),(.11,.12,eave-.30),'wood_dark')
    if style=='watermill':
        wheel(mesh,w*.57,0,h*.29,h*.24,.52,'wood_dark',10,axis_x=True)
    return mesh


def wheel(mesh,x,y,z,r,width,color,n=10,axis_x=False):
    def point(rad,i,offset):
        a=i*math.tau/n
        u,v=rad*math.cos(a),rad*math.sin(a)
        return (x+offset,y+u,z+v) if axis_x else (x+u,y+offset,z+v)
    for i in range(n):
        j=(i+1)%n
        for side in (-1,1):
            depth=side*width/2
            f=(point(r,i,depth),point(r,j,depth),point(r*.78,j,depth),point(r*.78,i,depth))
            mesh.face(f if (side>0)==axis_x else f[::-1],color)
        for rr in (r,r*.78):
            f=(point(rr,i,-width/2),point(rr,j,-width/2),point(rr,j,width/2),point(rr,i,width/2))
            mesh.face(f if (rr==r)==axis_x else f[::-1],color)
        if i%2==0:
            mesh.beam((x,y,z),point(r*.85,i,0),r*.085,color)
    a,b=((x-width/2,y,z),(x+width/2,y,z)) if axis_x else ((x,y-width/2,z),(x,y+width/2,z))
    mesh.tube(a,b,r*.16,'iron',7)


def building(style, size, rng, variant):
    w,d,h=size
    mesh=Mesh()
    extras=[]
    if style.startswith(('house','barn','warehouse','pump','shed','watermill','crypt')):
        mesh=house(style,size,rng)
    elif style in {'windmill','silo_metal','tower_stone_ruined'}:
        radius=w*.46
        mesh.tube((0,0,0),(0,0,h*.78),radius,'iron' if 'silo' in style else 'stone',12,.67,'shadow')
        for t in (.13,.27,.49,.71):
            mesh.tube((0,0,h*t),(0,0,h*t+.14),radius*(1-.33*t/.78)+.09,
                      'silver' if 'silo' in style else 'stone_dark',12,1)
        if style=='tower_stone_ruined':
            for i in range(8):
                a=math.tau*i/8
                mesh.box((radius*.65*math.cos(a),radius*.65*math.sin(a),h*(.83+.04*math.sin(i))),(.68,.65,h*.18),'stone','stone_light')
        else:
            mesh.tube((0,0,h*.77),(0,0,h),radius*.72,'slate' if 'silo' in style else 'roof',12,.14)
        if style=='windmill':
            opening(mesh,0,-radius*.95,1.16,w*.24,2.14,True)
            rotor=Mesh()
            for i in range(4):
                a=math.pi/2*i+.37
                c,s=math.cos(a),math.sin(a)
                radius_rot=w*.48
                rotor.beam((0,0,0),(c*radius_rot,0,s*radius_rot),.15,'wood_dark')
                sail=[(c*r*radius_rot-s*t*w*.12,-.078,s*r*radius_rot+c*t*w*.12)
                      for r,t in ((.40,.06),(.89,.06),(.83,.98),(.41,.91))]
                rotor.face(sail,'cloth' if i%2 else 'cream')
                for j in range(7):
                    t=.32+j*.09
                    span=w*.11
                    rotor.beam((c*t*radius_rot,0,s*t*radius_rot),(c*t*radius_rot-s*span,0,s*t*radius_rot+c*span),.07,'wood_light')
                rotor.beam((c*radius_rot*.30-s*w*.11,0,s*radius_rot*.30+c*w*.11),
                           (c*radius_rot*.86-s*w*.11,0,s*radius_rot*.86+c*w*.11),.09,'wood')
            rotor.tube((0,-.14,0),(0,.18,0),.24,'iron',10)
            extras.append(('Rotor',rotor,(0,-d*.515,h*.65)))
    elif style in {'tower_watch_wood','gazebo'}:
        deck=h*(.68 if 'tower' in style else .08)
        for x in (-w*.39,w*.39):
            for y in (-d*.39,d*.39):
                mesh.beam((x*1.14,y*1.14,0),(x,y,h*.82),.21,'wood_dark')
        for y in (-d*.39,d*.39):
            for side in (-1,1):
                mesh.beam((side*w*.39,y,deck*.17),(-side*w*.39,y,deck*.91),.14,'wood')
        for i in range(9):
            mesh.box(((i/8-.5)*w*.77,0,deck),(w*.085,d*.82,.13),'wood','wood_light')
        if 'tower' in style:
            for i in range(12):
                mesh.box((0,-d*.48,.2+i*deck/12),(.54,.15,.10),'wood_light')
        gable(mesh,w,d,h*.80,h,'slate' if 'tower' in style else 'roof')
    elif style=='well_stone':
        n=10
        for row in range(3):
            for i in range(n):
                a=(i+.48*(row%2))*math.tau/n
                mesh.box((math.cos(a)*w*.39,math.sin(a)*d*.39,.12+row*.23),(.42,.25,.21),
                         rng.choice(['stone_dark','stone','stone_light']),angle=a+math.pi/2)
        for x in (-w*.36,w*.36):
            mesh.box((x,0,h*.58),(.10,.12,h*.74),'wood_dark')
        gable(mesh,w,d,h*.76,h,'roof')
        mesh.tube((-w*.36,0,h*.63),(w*.36,0,h*.63),.065,'wood',8)
    elif style=='campfire_pit':
        for i in range(8):
            a=i*math.tau/8
            mesh.lump((.68*math.cos(a),.68*math.sin(a),0),(.21,.19,.20),['stone_dark','stone'],rng,5,3)
        for a in (.46,2.1,3.33):
            mesh.tube((-.34*math.cos(a),-.34*math.sin(a),.11),(.34*math.cos(a),.34*math.sin(a),.31),.095,'wood_dark',8,.84,'ash')
    elif style=='tent_camp_2':
        for x in (-w*.25,w*.25):
            a,b=w*.235,d*.42
            mesh.face(((x-a,-b,0),(x+a,-b,0),(x,-b,h)),'shadow')
            mesh.face(((x-a,b,0),(x-a,-b,0),(x,-b,h),(x,b,h)),'cloth')
            mesh.face(((x,b,h),(x,-b,h),(x+a,-b,0),(x+a,b,0)),'cream')
            mesh.face(((x+a,b,0),(x-a,b,0),(x,b,h)),'cloth')
            mesh.beam((x,-b-.07,0),(x,-b-.07,h),.046,'wood')
    elif style.startswith(('ruins','cabin')):
        if style=='ruins_arch':
            for i in range(11):
                a=i*math.pi/10
                mesh.lump((math.cos(a)*1.74,0,math.sin(a)*1.65),(.44,.44,.57),['stone_dark','stone','lichen'],rng,7,4)
        elif style=='ruins_pillars_3':
            for i in range(3):
                mesh.tube((i-1,0,0),(i-1,0,h*(.55+i*.2)),.40,'stone',9,.76)
        elif style=='cabin_ruined':
            mesh.box((0,0,.10),(w,d,.20),'stone_dark','soil')
            for x,y,wall_h in [(-w*.43,-d*.37,h*.62),(w*.44,-d*.40,h*.53),(-w*.46,d*.43,h*.84),(w*.43,d*.40,h*.90)]:
                mesh.box((x,y,wall_h/2),(.22,.23,wall_h),'wood_dark')
            for side in (-1,1):
                for j in range(5):
                    length=d*(.88-.11*j)
                    mesh.box((side*w*.45,-d*.1*side,.30+j*.34),(.18,length,.31),rng.choice(['wood','wood_old','wood_dark']))
            for j in range(4):
                y=d*(.45-j*.19)
                mesh.beam((-w*.45,y,h*.51),(0,y,h*(.85-j*.075)),.16,'wood')
                mesh.beam((0,y,h*(.85-j*.075)),(w*.44,y,h*.56),.16,'wood_dark')
        else:
            for j in range(6):
                for i in range(11):
                    if rng.random() < j*.1:
                        continue
                    x=w*((i+.5)/11-.5)+.08*(j%2)
                    y=(j%3)*.07 if style!='cabin_ruined' else d*.33*math.sin(i*.55)
                    z=h*(j+.5)/6
                    mesh.box((x,y,z),(w/11*.91,d*.64,h/6*.94),rng.choice(['stone','stone_dark','lichen','stone_light']))
    elif style.startswith(('shrine','statue')):
        mesh.box((0,0,h*.12),(w,d,h*.24),'stone_dark','stone_light')
        for x in (-w*.14,w*.14):
            mesh.beam((x,0,h*.22),(x*.74,.03,h*.55),w*.20,'stone_dark')
        mesh.lump((0,0,h*.48),(w*.30,d*.26,h*.35),['stone_dark','stone','lichen'],rng,8,5)
        if style!='statue_broken':
            mesh.lump((0,0,h*.78),(w*.17,d*.16,h*.21),['stone','stone_light','lichen'],rng,8,4)
        mesh.beam((-w*.22,0,h*.73),(-w*.34,-d*.13,h*.49),w*.16,'stone')
        if style!='statue_broken':
            mesh.beam((w*.23,0,h*.73),(w*.32,-d*.17,h*.56),w*.16,'stone_light')
        if style=='statue_angel':
            for side in (-1,1):
                for j in range(4):
                    mesh.beam((0,d*.18,h*(.54+.06*j)),(side*w*(.36+.08*j),d*.34,h*(.58+.10*j)),.12,'stone_light')
        elif style=='statue_warrior':
            mesh.beam((w*.31,-d*.21,h*.29),(w*.31,-d*.21,h*.81),.11,'stone')
            mesh.tube((-w*.28,-d*.19,h*.52),(-w*.28,-d*.27,h*.52),w*.25,'stone_light',8,.93)
    return mesh,extras


def fence(style,size,rng):
    w,d,h=size
    mesh=Mesh()
    if style in {'fence_stone_low','barrier_concrete','barricade_sandbags'}:
        rows=3 if 'barrier' not in style else 1
        count=5 if rows>1 else 1
        for row in range(rows):
            for i in range(count):
                x=w*((i+.5)/count-.5)+(.04 if row%2 else -.04)
                color=rng.choice(['stone','stone_dark','lichen'] if 'sandbag' not in style else ['cloth','sand','ochre'])
                mesh.box((x,0,h*(row+.5)/rows),(w/count*.95,d*(1-.12*row),h/rows*.97),color)
        if rows==1:
            mesh.box((0,0,h*.85),(w*.95,d*.68,h*.24),'stone_light')
        return mesh
    metal='metal' in style
    if style.endswith('corner'):
        points=[(-w/2,0),(0,0),(0,d/2 if d>.5 else w/2)]
    else:
        points=[(-w/2,0),(w/2,0)]
    for x,y in points:
        mesh.box((x,y,h/2),(.16,.17,h),'iron' if metal else 'wood_dark','silver' if metal else 'wood_light')
    for (x,y),(u,v) in zip(points,points[1:]):
        for z in (h*.34,h*.74):
            mesh.beam((x,y,z),(u,v,z*.75 if 'broken' in style else z),.096,'iron' if metal else 'wood')
        if metal:
            for i in range(1,8):
                a=i/9
                xx=x+(u-x)*a
                mesh.beam((xx,y,.09),(min(u-.05,xx+.41),v,h*.84),.021,'silver')
                mesh.beam((min(u-.05,xx+.41),y,.09),(xx,v,h*.84),.021,'silver')
        if 'gate' in style:
            mesh.beam((x,y,h*.27),(u,v,h*.78),.088,'wood_light')
    return mesh


def road(style,size,rng):
    w,d,_=size
    mesh=Mesh()
    if style=='road_stone_tiles':
        for i in range(5):
            for j in range(4):
                mesh.box((w*((i+.5)/5-.5),d*((j+.5)/4-.5),.021),(w/5*.955,d/4*.93,.038),
                         rng.choice(['stone','stone_light','gray','lichen']))
    else:
        for i in range(12):
            a,b=d*(i/12-.5),d*((i+1)/12-.5)
            bend=lambda y: w*.32*(1-math.cos((y/d+.5)*math.pi)) if style.endswith('curve') else 0
            for lo,hi,color in [(-.50,-.38,'grass_dark'),(-.38,-.23,'cut'),(-.23,-.16,'soil'),(-.16,.16,'ochre'),
                                 (.16,.23,'soil'),(.23,.38,'cut'),(.38,.50,'grass_dark')]:
                mesh.face(((lo*w+bend(a),a,.012),(hi*w+bend(a),a,.012),(hi*w+bend(b),b,.012),(lo*w+bend(b),b,.012)),color)
        if style.endswith('cross'):
            for side in (-1,1):
                mesh.box((side*w*.39,0,.004),(w*.22,d*.37,.012),'cut','ochre')
    return mesh


def small(style,size,rng,variant):
    w,d,h=size
    mesh=Mesh()
    if style.startswith(('signpost','lamp_post','torch_post','signal_flag','banner_pole','scarecrow')):
        mesh.box((0,0,h*.5),(.085,.085,h),'wood_dark' if 'lamp' not in style else 'iron')
        if 'signpost' in style:
            mesh.box((.15,-.05,h*.81),(w*.76,.082,h*.14),'wood_light',angle=.11*variant)
        elif 'banner' in style or 'flag' in style:
            mesh.box((w*.22,0,h*.79),(w*.45,.027,h*.29),['red','teal','yellow'][variant%3])
        elif 'scarecrow' in style:
            mesh.beam((-w*.47,0,h*.68),(w*.47,0,h*.68),.092,'wood')
            mesh.box((0,0,h*.60),(w*.41,.19,h*.27),'cloth')
            mesh.lump((0,0,h*.80),(w*.16,.14,h*.21),['straw','ochre'],rng,7,3)
        else:
            mesh.box((0,0,h*.90),(w*.83,d*.82,h*.18),'iron','silver')
            mesh.box((0,-d*.41,h*.90),(w*.59,.018,h*.103),'yellow')
    elif style in {'cart_wood','wheelbarrow','broken_cart_wheel','tire_old','valve_wheel'}:
        if style in {'broken_cart_wheel','tire_old','valve_wheel'}:
            wheel(mesh,0,0,h*.5,h*.48,d*.65,'black' if 'tire' in style else 'iron' if 'valve' in style else 'wood_dark',10)
        else:
            mesh.box((0,.10,h*.46),(w*.77,d*.70,h*.13),'wood','wood_light')
            for x in (-w*.35,w*.35):
                mesh.box((x,.10,h*.63),(.08,d*.67,h*.23),'wood_light')
                mesh.beam((x,-d*.26,h*.45),(x,-d*.61,h*.37),.071,'wood_dark')
            for side in (-1,1) if style=='cart_wood' else (0,):
                wheel(mesh,side*w*.44,.10,h*.30,h*.29,.10,'wood_dark',10,axis_x=True)
    elif style=='boat_small_wood':
        xy=[(-.39,-.30),(-.25,-.48),(.25,-.48),(.39,-.30),(.45,.24),(0,.50),(-.45,.24)]
        rim=[(w*x,d*y,h*.84) for x,y in xy]
        lower=[(w*x*.75,d*y*.77,0) for x,y in xy]
        inside=[(w*x*.85,d*y*.89,h*.66) for x,y in xy]
        mesh.loft([lower,rim,inside],['wood_dark','wood','wood_light'],'wood_dark','wood_dark')
        for y in (-d*.23,0,d*.22):
            mesh.box((0,y,h*.72),(w*.67,.19,.089),'wood_light')
    elif style in {'dock_pier','pallet_stack','bench_wood','table_picnic','ladder_wood'}:
        if style=='ladder_wood':
            for x in (-w*.40,w*.40):
                mesh.beam((x,0,0),(x,d*.32,h),.065,'wood_dark')
            for i in range(7):
                z=h*(i+.58)/7.5
                mesh.beam((-w*.4,d*.32*z/h,z),(w*.4,d*.32*z/h,z),.049,'wood_light')
        else:
            levels=3 if 'pallet' in style else 1
            for j in range(levels):
                for i in range(10 if 'dock' in style else 5):
                    n=10 if 'dock' in style else 5
                    mesh.box((0,d*((i+.5)/n-.5),h*(.89-j*.27)),(w,d/n*.96,h*.10),'wood_dark',rng.choice(['wood','wood_light','wood_old']))
            for x in (-w*.40,w*.40):
                for y in (-d*.39,d*.39):
                    mesh.box((x,y,h*.41),(.15,.16,h*.82),'wood_dark')
    elif style in {'crates_stack_3','beehives_3'}:
        for i,(x,z) in enumerate([(-.26,.25),(.27,.25),(.06,.72)]):
            if style=='crates_stack_3':
                mesh.box((x*w,0,z*h),(w*.45,d*.84,h*.45),'wood','wood_light',angle=(i-1)*.12)
                mesh.box((x*w,-d*.424,z*h),(w*.34,.026,h*.083),'wood_dark')
            else:
                x=(i-1)*w*.34
                for row in range(4):
                    mesh.box((x,0,h*(.11+row*.19)),(w*.31,d*.70,h*.17),'ochre' if row%2 else 'yellow','wood_light')
                mesh.box((x,0,h*.92),(w*.34,d*.83,h*.12),'roof','wood_light')
                mesh.box((x,-d*.355,h*.14),(w*.16,.022,h*.055),'shadow')
    elif style in {'barrels_cluster','oil_drum_rusty','log_pile'} or style.startswith('pipe_segment'):
        if style=='log_pile':
            for i in range(5):
                mesh.tube((w*(i*.16-.38),-d*.47,h*.31),(w*(i*.16-.38),d*.47,h*.31),min(d,w)*.10,'bark',7,.94,'wood_light')
        else:
            color='rust' if 'rusty' in style else 'iron' if 'pipe' in style else 'wood'
            count=3 if style=='barrels_cluster' else 1
            for i in range(count):
                x=(i-1)*w*.28 if count>1 else 0
                r=min(w*(.15 if count>1 else .45),d*.45)
                mesh.tube((x,0,0),(x,0,h),r,color,10,.94,'iron' if 'pipe' in style else 'wood_dark')
                for z in (.18,.75):
                    mesh.tube((x,0,h*z),(x,0,h*z+.07),r*1.07,'iron',10)
    elif style=='fishing_net_pile':
        for i in range(10):
            offset=(i-4.5)*.082
            for side in (-1,1):
                mesh.beam((offset-.27*side,-d*.39,.055),(offset+.27*side,d*.39,.091),.012,'cloth')
        mesh.tube((-w*.41,-d*.40,.062),(w*.41,-d*.40,.062),.026,'straw',6)
    elif style=='rope_coil':
        pts=[(math.cos(i*.39)*w*(.43-i*.006),math.sin(i*.39)*d*(.43-i*.006),.08+i*.003) for i in range(37)]
        for a,b in zip(pts,pts[1:]):
            mesh.tube(a,b,.022 if 'rope' in style else .027,'straw' if 'rope' in style else 'cloth',5)
    elif style in {'rusty_sword_stuck','shield_old'}:
        if 'sword' in style:
            mesh.beam((0,0,0),(.21,0,h*.72),.08,'rust',.028)
            mesh.beam((-.14,0,h*.72),(.39,0,h*.72),.075,'iron')
            mesh.tube((.21,0,h*.72),(.27,0,h),.048,'wood_dark',7)
        else:
            mesh.tube((0,.027,h*.5),(0,-.07,h*.5),h*.47,'wood',10,.87,'iron')
            mesh.tube((0,-.07,h*.5),(0,-.13,h*.5),h*.15,'silver',9,.61)
    elif style=='skull_animal':
        mesh.lump((0,.09,.04),(.29,.24,.35),['bone','cream','ash'],rng,8,4)
        mesh.box((0,-.22,.14),(.27,.39,.19),'bone','cream')
        for side in (-1,1):
            mesh.lump((side*.21,-.055,.17),(.077,.099,.10),['shadow','darksoil'],rng,5,3)
            mesh.tube((side*.21,.14,.26),(side*.36,.18,.45),.066,'bone',6,.15)
    elif style=='ribcage':
        mesh.tube((-w*.40,0,.12),(w*.40,0,.12),.056,'bone',6)
        for i in range(6):
            x=(i-2.5)*w*.14
            for side in (-1,1):
                p=(x,side*d*.36,.36+.09*math.sin(i*.5))
                mesh.tube((x,0,.115),p,.040,'bone',5,.80)
                mesh.tube(p,(x,side*d*.46,.14),.032,'cream',5,.32)
    elif style=='bones_small_set':
        for i in range(5):
            a=(i-2)*.24
            mesh.tube((a,-d*.23,0),(a+.11,d*.22,h*.35),.042,'bone',6,.76)
    else:
        mesh.lump((0,0,0),(w*.49,d*.49,h),['straw','ochre','cloth'] if style=='haystack' else ['stone_dark','stone','lichen'],rng,9,5)
    return mesh
