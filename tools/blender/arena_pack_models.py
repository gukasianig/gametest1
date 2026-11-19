"""Manifest, export and round-trip checks for arena model families."""

import math
import random
import json
from dataclasses import dataclass

import bpy
from mathutils import Vector

import make_cargo_terminal as preview
from arena_pack_mesh import Mesh, materials
from arena_pack_nature import terrain, water_asset, plant, rock_asset
from arena_pack_structures import bridge, building, fence, road, small


@dataclass(frozen=True)
class Asset:
    category: str
    style: str
    name: str
    size: tuple
    budget: int
    variant: int = 0
    minimum: bool = False


def inventory():
    assets=[]
    def add(category, style, size, budget, minimum=False, name=None):
        assets.append(Asset(category,style,name or style,size,budget,0,minimum))
    def variants(category, style, count, size, budget, minimum=0, start=1):
        for i in range(count):
            dims=size(i) if callable(size) else size
            assets.append(Asset(category,style,f'{style}_{i+start:02d}',dims,budget,i,i<minimum))
    for name,size,budget,minimum in [
        ('hill_round_S',(20,20,4),800,False),('hill_round_M',(40,40,8),1600,True),('hill_round_L',(70,70,14),2500,True),
        ('hill_long',(80,25,10),2000,False),('ridge_rocky',(60,18,12),2500,False),('cliff_wall_straight',(40,6,14),2000,True),
        ('cliff_wall_corner_in',(14,14,14),1500,False),('cliff_wall_corner_out',(14,14,14),1500,False),
        ('cliff_ledge',(30,10,4),1200,False),('mesa_flat',(40,40,18),1800,False),('plateau_step',(36,36,9),1500,False),
        ('ramp_dirt',(8,14,3),300,False),('canyon_segment',(30,40,12),3000,False),('crater_dry',(30,30,4),1200,True),
        ('sinkhole',(14,14,5),900,False),('dune_sand_S',(25,15,3),700,False),('dune_sand_M',(50,25,6),700,False),
        ('bank_river_straight',(20,8,2),600,False),('bank_river_curve',(20,8,2),600,False),('waterfall_cliff',(14,10,10),1500,False)]:
        add('terrain',name,size,budget,minimum)
    variants('terrain','mountain_far',4,(150,60,60),800,2)
    add('water','water_plane_square',(10,10,0),2048)
    for name,size,budget,minimum in [
        ('pond_S',(8,8,.34),750,False),('pond_M',(18,18,.48),1000,True),('pond_L',(40,40,.62),1500,False),
        ('river_straight_20',(10,20,.35),800,True),('river_curve_45',(20,20,.35),1000,False),
        ('river_curve_90',(20,20,.35),1000,True),('river_fork_Y',(25,25,.36),1500,False),
        ('river_delta_end',(20,25,.40),1200,False),('oxbow_lagoon',(50,14,.46),1300,False),
        ('marsh_patch',(14,14,.44),900,False),('stream_small',(3,10,.28),300,False),
        ('ford_stones',(3,8,.33),500,False),('spring_pool',(4,4,.39),500,False)]:
        add('water',name,size,budget,minimum)
    variants('water','puddle',6,lambda i:(1.2+i*.71,.76+i*.51,.035),200,3)
    variants('water','lake_edge_set',6,(12,8,.36),500)
    for name,size,budget,minimum in [
        ('bridge_wood_S',(2,6,1),900,True),('bridge_wood_L',(5,14,1.5),2200,False),('bridge_stone_arch',(5,12,3.5),2400,True),
        ('bridge_rope',(1.5,12,1),1500,False),('bridge_metal_truss',(5,16,4),3500,False),('plank_single',(.5,3,.1),50,False),
        ('log_bridge',(.8,5,.8),200,False),('stepping_stones_set',(1,4,.3),500,False)]:
        add('bridges',name,size,budget,minimum)
    for name,size,budget,minimum in [
        ('house_wood_small',(6,6,5),2500,True),('house_wood_medium',(9,7,6),4500,False),('house_stone',(8,8,6),4000,False),
        ('cabin_ruined',(7,6,4),2500,False),('barn',(12,8,7),3500,True),('windmill',(6,6,14),3500,True),
        ('watermill',(8,8,7),3500,False),('tower_watch_wood',(4,4,10),2000,True),('tower_stone_ruined',(6,6,12),3000,False),
        ('shed_tool',(3,3,2.5),800,False),('silo_metal',(5,5,10),1500,False),('warehouse_small',(14,9,6),3000,False),
        ('pump_station',(8,6,5),2500,False),('well_stone',(1.6,1.6,2),600,True),('gazebo',(5,5,4),1500,False),
        ('tent_camp_2',(4,4,2.5),600,False),('campfire_pit',(2,2,.6),300,False),('ruins_arch',(5,1,5),900,False),
        ('ruins_pillars_3',(4,1.2,6),900,False),('shrine_small',(3,3,3),1200,False),('crypt_entrance',(5,4,3),1500,False),
        ('statue_warrior',(1.5,1.5,4),1200,False),('statue_angel',(1.5,1.5,4),1200,False),('statue_broken',(1.5,1.5,3),1200,False)]:
        add('buildings',name,size,budget,minimum)
    variants('buildings','ruins_wall_set',5,lambda i:(2+i,.5,1+i*.48),700,5)
    for name,size,budget,minimum in [
        ('fence_wood_straight',(3,.2,1.2),300,True),('fence_wood_corner',(1.6,1.6,1.2),300,True),
        ('fence_wood_gate',(3,.2,1.2),300,False),('fence_wood_broken',(3,.2,1.2),300,False),
        ('fence_stone_low',(3,.6,1),300,True),('fence_metal_chain',(3,.1,2),240,False),
        ('barrier_concrete',(2,.6,1),80,False),('barricade_sandbags',(3,1,1),300,False),
        ('road_dirt_straight',(4,10,.012),200,False),('road_dirt_curve',(4,10,.012),200,False),
        ('road_dirt_cross',(4,10,.018),200,False),('road_stone_tiles',(3,6,.041),300,False),
        ('lamp_post_old',(.3,.3,4),300,False),('cart_wood',(3,1.5,1.4),700,False),('boat_small_wood',(1.6,4,1),600,False),
        ('dock_pier',(2,8,1.2),800,False),('crates_stack_3',(2,1,2),300,False),('barrels_cluster',(1.5,1.5,1),500,False),
        ('haystack',(2.5,2.5,2.5),400,False),('scarecrow',(1,1,2.2),500,False),('beehives_3',(2,1,1),400,False),
        ('log_pile',(2,1,1.2),300,False),('wheelbarrow',(1.6,.7,.8),300,False)]:
        add('structures',name,size,budget,minimum)
    variants('structures','signpost',3,(.5,.2,2),150)
    for style,n,w,d,lo,hi,budget,minimum in [
        ('grass_clump_short',4,.55,.54,.32,.49,250,2),('grass_clump_tall',3,.72,.74,.82,1.26,400,0),
        ('grass_wild_seedy',3,.65,.63,.62,.99,350,0),('weeds_mixed',3,.67,.65,.45,.76,350,0),('fern',3,1.1,1,.63,.96,500,1),
        ('bush_leafy',5,1.7,1.5,1.02,1.78,1200,3),('bush_flowering',3,1.57,1.42,1.0,1.50,1200,0),
        ('bush_berry',2,1.49,1.35,1,1.3,1000,0),('bush_dry',3,1.38,1.29,.8,1.4,800,0),('bush_thorny_dead',2,1.63,1.45,1,1.6,700,0),
        ('flower_single_set',6,.31,.30,.31,.59,200,6),('flower_patch',4,1,1,.34,.63,600,0),
        ('reed_clump',3,.79,.75,1.05,1.57,450,1),('cattail_clump',2,.81,.78,1.0,1.5,450,0),
        ('water_lily_set',3,1.32,1.20,.05,.05,200,0),('lotus_pad_cluster',2,1.49,1.40,.05,.05,200,0),
        ('mushroom_small_set',6,.24,.23,.1,.29,200,0),('mushroom_giant',2,2.4,2.1,2,4,700,0),
        ('ivy_wall_patch',2,1.1,.28,.83,1,200,0),('vine_hanging',2,1,.46,2.8,3,300,0),('moss_patch',3,1.3,1.1,.17,.20,100,0),
        ('tree_oak',3,6.4,5.6,8.6,13.6,2800,0),('tree_birch',3,4.3,4.1,8.5,13.3,3000,1),
        ('tree_pine',3,4.7,4.2,9.2,13.9,3000,0),('tree_spruce',3,5.1,4.6,8.5,13.6,3000,0),
        ('tree_willow',3,7.1,6.6,8.3,12.4,3500,1),('tree_dead',3,5.1,4.5,8,13,1900,0),
        ('tree_small_sapling',3,1.62,1.44,2,3.9,950,0),('stump_big',3,1.4,1.3,.8,1.5,400,0),
        ('log_fallen',3,3.1,1.4,1.1,1.4,900,0),('root_cluster',3,2,2,.5,.7,500,0),
        ('cactus_set',4,.8,.54,.84,2.96,800,0),('palm_small',2,3.6,3.4,4.4,6.9,1500,0),('bamboo_clump',2,2.1,1.8,4.3,5.8,1200,0)]:
        variants('plants',style,n,lambda i,w=w,d=d,lo=lo,hi=hi,n=n:(w*(.91+i*.17/max(1,n-1)),d,lo+(hi-lo)*i/max(1,n-1)),budget,minimum)
    for style,n,w,d,h,budget,minimum in [
        ('rock_pebble_set',6,.34,.27,.095,120,0),('rock_small',6,.64,.55,.47,400,3),('rock_medium',5,1.63,1.44,1.14,1000,2),
        ('rock_large',4,4.1,3.2,2.63,1800,1),('rock_mossy_medium',4,1.77,1.49,1.25,1000,1),
        ('rock_spire',3,2,2,7.5,1500,0),('rock_arch_natural',1,8,3,5,1800,0),('rock_cluster',4,4,4,2,1800,0),
        ('rock_slab_flat',3,3,3,.5,200,0),('rock_crystal_cluster',3,2,2,1.9,500,0),('stone_menhir',3,1,1,3.5,400,0),
        ('stone_circle_8',1,10,10,3,1200,0),('cave_entrance',2,6,4,4,1500,0)]:
        variants('rocks',style,n,lambda i,w=w,d=d,h=h,n=n:(w*(.86+.26*i/max(1,n-1)),d,h*(.89+.19*i/max(1,n-1))),budget,minimum)
    for name,size,budget in [
        ('bones_small_set',(1.13,.54,.28),200),('skull_animal',(.75,.6,.55),360),('ribcage',(1.4,.64,.65),460),
        ('broken_cart_wheel',(1.32,.29,1.32),240),('rusty_sword_stuck',(.48,.16,1.4),170),('shield_old',(.75,.18,.85),260),
        ('signal_flag_pole',(.68,.23,1.96),120),('rope_coil',(.86,.73,.27),600),('fishing_net_pile',(1.3,1.2,.36),600),
        ('ladder_wood',(.52,.18,1.9),250),('pallet_stack',(1.4,1.16,.89),400),('tire_old',(.76,.29,.78),400),
        ('oil_drum_rusty',(.64,.65,.95),260),('pipe_segment_S',(.44,.44,.70),280),('pipe_segment_M',(.53,.53,1.24),280),
        ('pipe_segment_L',(.59,.59,1.92),280),('valve_wheel',(.43,.23,.45),350),('bench_wood',(1.82,.58,.83),400),
        ('table_picnic',(1.97,1.32,.93),450),('torch_post',(.25,.25,1.5),200),('milestone_stone',(.59,.4,.74),300)]:
        add('props',name,size,budget)
    variants('props','banner_pole',3,(.79,.29,1.95),200)
    return assets


def geometry(spec, rng):
    style,size=spec.style,spec.size
    category=spec.category
    extras=[]
    if category=='terrain':
        return terrain(style,size,spec.budget,rng,spec.variant),extras
    if category=='water':
        mesh=water_asset(style,size,rng,spec.variant)
        if style.startswith('river'):
            # Preserve circular bends and identical metre-scale socket profiles.
            center=[(min(p[k] for p in mesh.vertices)+max(p[k] for p in mesh.vertices))/2 for k in (0,1)]
            mesh.vertices=[(x-center[0],y-center[1],z) for x,y,z in mesh.vertices]
            for port in mesh.ports:
                for point in [port['position'],*port['profile']]:
                    point[0]-=center[0]
                    point[1]-=center[1]
        else:
            mesh.normalize(size,-.07 if style.startswith(('pond','spring','marsh','oxbow')) else 0)
        return mesh,extras
    if category=='bridges':
        mesh=bridge(style,size,rng)
    elif category=='buildings':
        mesh,extras=building(style,size,rng,spec.variant)
    elif category=='plants':
        mesh=plant(style,size,rng,spec.variant)
        if style=='log_fallen':
            size=((3,5.5,8)[spec.variant],*size[1:])
    elif category=='rocks':
        mesh=rock_asset(style,size,rng,spec.variant)
    elif category=='structures' and style.startswith(('fence','barrier','barricade')):
        mesh=fence(style,size,rng)
    elif category=='structures' and style.startswith('road'):
        mesh=road(style,size,rng)
    else:
        mesh=small(style,size,rng,spec.variant)
    mesh.normalize(size if not style.startswith('road_dirt') else (*size[:2],0), base=.006 if style.startswith('road_dirt') else 0)
    return mesh,extras


def export(root, spec, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
        obj.data.calc_loop_triangles()
    bpy.context.view_layer.objects.active=objects[0]
    file=root/spec.category/f'{spec.name}.fbx'
    file.parent.mkdir(exist_ok=True)
    count=sum(len(o.data.loop_triangles) for o in objects)
    assert count<=spec.budget, (spec.name,count,spec.budget)
    bpy.ops.export_scene.fbx(filepath=str(file),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
                             use_triangles=True,add_leaf_bones=False, path_mode='RELATIVE')
    bpy.context.view_layer.update()
    points=[o.matrix_world @ v.co for o in objects for v in o.data.vertices]
    bounds=[[min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)]]
    slots=sorted({m.name for o in objects for m in o.data.materials})
    assert len(slots)<=2, (spec.name,slots)
    return {'file':file.relative_to(root).as_posix(),'category':spec.category,'name':spec.name,'triangles':count,
            'budget':spec.budget,'blender_bounds':bounds,'material_slots':slots,'meshes':len(objects),'minimum':spec.minimum}


def verify(root, records):
    for record in records:
        bpy.ops.object.select_all(action='DESELECT')
        bpy.ops.import_scene.fbx(filepath=str(root/record['file']))
        imported=list(bpy.context.selected_objects)
        meshes=[obj for obj in imported if obj.type=='MESH']
        assert len(meshes)==record['meshes'],record['file']
        count=0
        bpy.context.view_layer.update()
        points=[]
        for obj in meshes:
            obj.data.calc_loop_triangles()
            count+=len(obj.data.loop_triangles)
            assert obj.data.uv_layers.active and len(obj.data.materials)<=2,record['file']
            points.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
            if record['name'].startswith('river'):
                for polygon in obj.data.polygons:
                    if obj.data.materials[polygon.material_index].name.startswith('ArenaGreen_Water'):
                        normal=obj.matrix_world.to_3x3() @ polygon.normal
                        assert normal.z>.99, (record['name'],'water normal',tuple(normal))
        assert count==record['triangles'],(record['file'],count,record['triangles'])
        lo,hi=record['blender_bounds']
        assert all(abs(min(p[i] for p in points)-lo[i])<.002 and abs(max(p[i] for p in points)-hi[i])<.002 for i in range(3)),record['file']
        for obj in imported:
            bpy.data.objects.remove(obj,do_unlink=True)
    print(f'MODELS PASS {len(records)} FBX: round trip, mesh count, UV, material count, triangles and bounds',flush=True)


def verify_connectors(records):
    checked=[]
    reference=[(-5,0),(-3.8,.28),(-3,.065),(-.9,.065),(.9,.065),(3,.065),(3.8,.28),(5,0)]
    for record in records:
        if record['name'] not in {'river_straight_20','river_curve_45','river_curve_90','river_fork_Y'}:
            continue
        for port in record['connectors_blender']:
            p=Vector(port['position'])
            normal=Vector(port['outward'])
            across=Vector((-normal.y,normal.x,0))
            actual=sorted((round((Vector(v)-p).dot(across),5),round(v[2],5)) for v in port['profile'])
            assert actual==reference,(record['name'],actual)
        checked.append(record['name'])
    print('CONNECTOR PASS: common 10m bank / 6m water profile '+', '.join(checked),flush=True)


def showcase(root, previews, objects, mats, minimum, records):
    for values in objects.values():
        for obj in values:
            obj.hide_render=True
    scene=preview.preview_setup()
    sun=next(obj for obj in scene.objects if obj.type=='LIGHT')
    sun.location=(-22,-27,36)
    sun.data.energy=2.55
    sun.data.color=(1,.93,.80)
    preview.aim(sun,(0,0,0))
    scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.54
    preview.PREVIEW=previews
    floor=Mesh()
    floor.box((0,0,-.23),(70,55,.4),'grass_dark','grass')
    floor.object('Diorama ground',mats)
    def instance(name,x,y,scale=1,angle=0,z=0):
        source=objects.get(name)
        if not source:
            return
        c,s=math.cos(angle),math.sin(angle)
        for obj in source:
            copy=bpy.data.objects.new(name,obj.data)
            bpy.context.collection.objects.link(copy)
            u,v,w=obj.location
            copy.location=(x+scale*(u*c-v*s),y+scale*(u*s+v*c),z+scale*w)
            copy.rotation_euler.z=angle
            copy.scale=(scale,)*3
    instance('hill_round_M',-15,16,.62,z=-.4)
    instance('hill_round_L',12,25,.55,z=-.6)
    instance('cliff_wall_straight',18,18,.58,angle=.14,z=-.42)
    instance('pond_M',13,-3,1,z=-.045)
    # Continuous channel reaching beyond both display edges, without a cut-off pool.
    for yy in (-20,0,20):
        instance('river_straight_20',-12,yy,1,z=0)
    instance('bridge_stone_arch',-12,-2.3,.84,angle=math.pi/2,z=-.50)
    instance('bridge_wood_S',-12,4.5,1.22,angle=math.pi/2,z=-.03)
    for x in (-17.4,-6.6):
        approach=Mesh()
        approach.box((x,-2.3,.18),(1.7,4.2,.36),'stone','stone')
        approach.object('Bridge approach',mats)
    instance('house_wood_small',-2.5,5.5,1,.12)
    instance('barn',-10.7,14.5,.83,-.12)
    instance('windmill',10.4,12,.88,-.13)
    instance('tower_watch_wood',-21.4,5.7,.81,-.2)
    instance('well_stone',2.5,3.1,1.25)
    for x,y in [(-4,-4),(-1,-4),(2,-4)]:
        instance('fence_wood_straight',x,y)
    rng=random.Random(117)
    for x,y in [(-21,12),(-16,12),(-19,0),(-4,17),(16,9),(22,6),(23,-5),(6,14),(-4,10),(-24,7),(-7,-10)]:
        instance(rng.choice(('tree_willow_01','tree_birch_01')),x,y,rng.uniform(.61,1.11),rng.uniform(0,6))
    for i in range(45):
        x,y=rng.uniform(-24,24),rng.uniform(-12,14)
        if (x-13)**2+(y+3)**2<75 or abs(x+12)<5 or -6<x<4 and y>1:
            continue
        instance(rng.choice(('grass_clump_short_01','grass_clump_short_02','bush_leafy_01','rock_small_01','fern_01')),
                 x,y,rng.uniform(.72,1.37),rng.uniform(0,6))
    instance('rock_large_01',19.5,-10,1.1,.45)
    instance('rock_mossy_medium_01',9,-11,1.15,.7)
    instance('rock_medium_01',5.7,-6,.93,.64)
    instance('reed_clump_01',18,-9,1.5,.45)
    instance('reed_clump_01',7,-9,1.0,1.8)
    instance('puddle_03',-1,-7,1.1,.87)
    instance('pump_station',-4,13,.77,.04)
    instance('boat_small_wood',17,0,1.0,.17,.20)
    instance('dock_pier',16,-9,1.0,.12)
    instance('ruins_wall_set_04',-18,-7,.92,.21)
    preview.render(scene,'models_minimum' if minimum else 'models_showcase',(32,-49,40),(-1,1,1.8),(1520,960),ortho=58)
    if minimum:
        return
    for obj in list(scene.objects):
        if obj.type=='MESH':
            obj.hide_render=True
    instance('house_wood_small',0,0)
    instance('tree_birch_01',-7,2,.85)
    instance('tree_willow_01',8,3,.75)
    instance('rock_large_01',5,-3)
    preview.render(scene,'models_detail',(22,-32,20),(0,0,3),(1520,960),ortho=25)
    for obj in list(scene.objects):
        if obj.type=='MESH':
            obj.hide_render=True
    indexed={r['name']:r for r in records}
    instance('river_fork_Y',0,0)
    for port in indexed['river_fork_Y']['connectors_blender']:
        p,d=Vector(port['position']),Vector(port['outward'])
        center=p+d*10
        instance('river_straight_20',center.x,center.y,angle=math.atan2(d.y,d.x)-math.pi/2)
    preview.render(scene,'river_junction',(0,-36,65),(0,0,0),(1200,1000),ortho=70)
    for obj in list(scene.objects):
        if obj.type=='MESH':
            obj.hide_render=True
    # Place successive sockets together using the delivered connector metadata.
    instance('river_straight_20',0,0)
    endpoint=Vector((0,10,0))
    direction=Vector((0,1,0))
    for name in ('river_curve_45','river_straight_20','river_curve_90'):
        ports=indexed[name]['connectors_blender']
        first=ports[0]
        angle=math.atan2(-direction.y,-direction.x)-math.atan2(first['outward'][1],first['outward'][0])
        c,s=math.cos(angle),math.sin(angle)
        def rotate(v):
            return Vector((v[0]*c-v[1]*s,v[0]*s+v[1]*c,v[2]))
        origin=endpoint-rotate(first['position'])
        instance(name,origin.x,origin.y,angle=angle)
        endpoint=origin+rotate(ports[1]['position'])
        direction=rotate(ports[1]['outward'])
    preview.render(scene,'river_bends',(5,-22,80),(-6,19,0),(1200,1000),ortho=82)


def build(root, previews, minimum):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.view.language='en_US'
    bpy.context.preferences.view.use_translate_new_dataname=False
    mats=materials(root)
    records=[]
    objects={}
    failures=[]
    for i,spec in enumerate(inventory()):
        if minimum and not spec.minimum:
            continue
        mesh,extras=geometry(spec,random.Random(39319+i*137))
        assert mesh.faces,spec.name
        parts=[mesh.object(spec.name,mats)]
        for name,extra,origin in extras:
            part=extra.object(name,mats)
            part.location=origin
            part.parent=parts[0]
            parts.append(part)
        try:
            record=export(root,spec,parts)
            if hasattr(mesh,'ports'):
                record['connectors_blender']=mesh.ports
            records.append(record)
        except AssertionError as error:
            failures.append(str(error))
        objects[spec.name]=parts
        for obj in parts:
            obj.hide_render=True
    if failures:
        raise RuntimeError('Model checks failed:\n'+'\n'.join(failures))
    verify_connectors(records)
    showcase(root,previews,objects,mats,minimum,records)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    verify(root,records)
    return records
