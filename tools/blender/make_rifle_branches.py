"""Three rifle branch models and matching transparent icons; local project-authored assets.
Run: blender -b -t 2 --factory-startup -P tools/blender/make_rifle_branches.py
"""
from pathlib import Path
import math
import shutil
import sys
import bpy
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).parent))
import kit
import weapon_kit as wk
from creature_mesh import Part

ROOT = Path(__file__).resolve().parents[2]


def icon(out, *unused):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.view_settings.view_transform = 'Standard'
    scene.world = bpy.data.worlds.new('Rifle icon world')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.75, .8, .9, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .6
    for name, rot, power, color in [('Key',(35,0,145),3,(1,.95,.85)),('Fill',(65,0,-40),1.5,(.7,.85,1))]:
        data = bpy.data.lights.new(name,'SUN')
        data.energy = power
        data.color = color
        obj = bpy.data.objects.new(name,data)
        bpy.context.collection.objects.link(obj)
        obj.rotation_euler = tuple(math.radians(x) for x in rot)
    bpy.context.view_layer.update()
    points = [o.matrix_world @ v.co for o in bpy.data.objects if o.type == 'MESH' for v in o.data.vertices]
    lo = Vector([min(p[i] for p in points) for i in range(3)])
    hi = Vector([max(p[i] for p in points) for i in range(3)])
    center = (lo+hi)*.5
    direction = Vector((.84,.42,.5)).normalized()
    data = bpy.data.cameras.new('IconCamera')
    data.type = 'ORTHO'
    camera = bpy.data.objects.new('IconCamera',data)
    bpy.context.collection.objects.link(camera)
    camera.location = center + direction*5
    camera.rotation_euler = (-direction).to_track_quat('-Z','Y').to_euler()
    bpy.context.view_layer.update()
    right = camera.matrix_world.to_3x3() @ Vector((1,0,0))
    up = camera.matrix_world.to_3x3() @ Vector((0,1,0))
    xs=[(p-center).dot(right) for p in points]
    ys=[(p-center).dot(up) for p in points]
    data.ortho_scale=max(max(xs)-min(xs),max(ys)-min(ys))*1.15
    camera.location += right*((max(xs)+min(xs))/2)+up*((max(ys)+min(ys))/2)
    scene.camera=camera
    scene.render.filepath=str(out/'icon.png')
    bpy.ops.render.render(write_still=True)


wk.render_model = icon  # finish still saves clean meshes and checks the FBX round trip.

for branch, colors in [
    ('Assault',('258D85','174B4D','64E6C8','287669','9DF7DE','FFE2A6','617981','ADBCB7')),
    ('Minigun',('9B6C32','513D27','E4B24F','916333','FFD780','FFF0B2','657079','B8C0BD')),
    ('Heavy',('934942','472E31','E16F50','743D33','FFA76E','FFE3B0','65757C','B9C5C3'))]:
    name='Rifle'+branch
    out=ROOT/'docs/art/Weapons/RifleBranches'/name
    out.mkdir(parents=True,exist_ok=True)
    kit.clear_scene()
    root=kit.make_root(name)
    material=wk.make_atlas(wk.palette(*colors),out,name)
    parts=[]
    def add(name,part):
        parts.append(part.build(name,(0,0,0),root,material))
    def box(name,y0,y1,z,w,h,color,x=0):
        p=Part();wk.block(p,y0,y1,z,w,h,min(w,h)*.24,color,x);add(name,p)
    def rod(name,y0,y1,z,r,color,x=0):
        p=Part();wk.rod(p,y0,y1,z,r,color,12,x,wk.VOID);add(name,p)

    box('Receiver',-.22,.19,.055,.115 if branch!='Minigun' else .17,.13,wk.shell_color(wk.MAIN,wk.LIGHT,wk.MAIN_DARK,wk.DARK))
    box('TopRail',-.19,.17,.135,.062,.024,wk.GRAPHITE)
    box('Stock',-.46,-.2,.055,.072,.085,wk.MAIN_DARK)
    box('ButtPad',-.485,-.455,.05,.09,.14,wk.RUBBER)
    box('SideAccentL',-.18,.12,.06,.012,.022,wk.ACCENT,-.065)
    box('SideAccentR',-.18,.12,.06,.012,.022,wk.ACCENT,.065)
    grip=wk.lower(add,y=-.13)
    muzzle=Vector((0,.55,.07))
    if branch=='Assault':
        box('Handguard',.18,.4,.06,.09,.09,wk.MAIN_DARK)
        for i in range(4):
            box('Vent'+str(i),.2+i*.043,.222+i*.043,.065,.098,.029,wk.GRAPHITE)
        rod('Barrel',.39,.55,.07,.018,wk.STEEL)
        rod('MuzzleBrake',.52,.58,.07,.03,wk.DARK)
        box('OpticHousing',-.08,.045,.19,.064,.07,wk.DARK)
        box('OpticGlow',.042,.05,.19,.045,.04,wk.GLOW)
        box('Magazine',.0,.08,-.105,.06,.19,wk.GRAPHITE)
        box('MagazineHeel',-.01,.1,-.205,.08,.025,wk.ACCENT)
        muzzle=Vector((0,.58,.07))
    elif branch=='Minigun':
        box('FeedBox',-.06,.14,-.10,.21,.20,wk.MAIN_DARK)
        box('FeedBoxLid',-.07,.15,-.21,.23,.028,wk.ACCENT)
        for i in range(6):
            a=i*math.tau/6
            rod('Barrel'+str(i),.17,.65,.07+math.sin(a)*.053,.017,wk.STEEL,math.cos(a)*.053)
        p=Part();wk.fins(p,.27,.58,.07,.087,.074,3,wk.DARK,wk.STEEL,12);add('BarrelBands',p)
        box('CarryHandle',-.12,.11,.25,.04,.033,wk.GRAPHITE)
        box('HandleRear',-.14,-.1,.20,.04,.1,wk.GRAPHITE)
        box('HandleFront',.09,.13,.20,.04,.1,wk.GRAPHITE)
        for i in range(3):
            box('FeedRound'+str(i),-.06+i*.043,-.04+i*.043,-.035,.035,.032,wk.BRASS,.125)
        muzzle=Vector((0,.65,.07))
    else:
        box('ReinforcedHandguard',.18,.42,.07,.13,.12,wk.MAIN_DARK)
        rod('HeavyBarrel',.39,.69,.07,.029,wk.STEEL)
        box('SquaredBrake',.64,.73,.07,.095,.085,wk.DARK)
        for i in range(3):
            box('BrakeVent'+str(i),.655+i*.022,.667+i*.022,.07,.103,.022,wk.STEEL)
        box('LongOptic',-.14,.1,.195,.066,.065,wk.GRAPHITE)
        box('OpticLens',.095,.11,.195,.055,.05,wk.GLOW)
        box('ShortMagazine',.0,.10,-.065,.085,.1,wk.DARK)
        box('BipodMountL',.31,.35,.015,.035,.105,wk.GRAPHITE,-.08)
        box('BipodMountR',.31,.35,.015,.035,.105,wk.GRAPHITE,.08)
        box('BipodL',.31,.48,-.025,.025,.025,wk.GRAPHITE,-.085)
        box('BipodR',.31,.48,-.025,.025,.025,wk.GRAPHITE,.085)
        muzzle=Vector((0,.73,.07))
    wk.finish(root,parts,out,name,1,grip,muzzle,(.6,1.35),preview_scale=1,frame=1.9)
    asset=ROOT/'Assets/Art/Weapons'/name
    asset.mkdir(parents=True,exist_ok=True)
    for file in (name+'.fbx',name+'_Palette.png'):
        shutil.copy2(out/file,asset/file)
    for directory,file in [('UI/Tree','el_'+branch.lower()+'.png'),('UI/Weapons','Rifle_'+branch+'.png')]:
        dest=ROOT/'Assets/Resources'/directory
        dest.mkdir(parents=True,exist_ok=True)
        shutil.copy2(out/'icon.png',dest/file)
    print('INSTALLED_SOURCE',name,flush=True)
