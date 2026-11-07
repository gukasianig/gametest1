"""Build the Operator's stylized pulse pistol and render it in Blender."""

import json
import math
from pathlib import Path
import sys

from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
import kit
from creature_mesh import Part, make_atlas, render_model, save_model, validate, verify_fbx, world_vertices

OUT = Path(__file__).resolve().parents[2] / "docs/art/Weapons/OperatorPulse"
OUT.mkdir(parents=True, exist_ok=True)
NAME = "OperatorPulse_Pistol"
PALETTE = (
    "202A32", "364853", "526572", "829296", "BAC5C2", "CDD3CC", "2F4B51", "208583",
    "58DCC7", "A5F0D9", "C39055", "E4AB6D", "141D24", "23343C", "768986", "A87042",
)
GRAPHITE, CASE, STEEL, EDGE, TITANIUM, LIGHT, JADE, TEAL, GLOW, LENS, BRASS, HIGHLIGHT, VOID, RUBBER, MUTED, BRONZE = range(16)

kit.clear_scene()
root = kit.make_root(NAME)
material = make_atlas(PALETTE, OUT, "OperatorPulse")
parts = []


def add(name, part, pivot=(0, 0, 0)):
    parts.append(part.build(name, pivot, root, material))


def chamfer_ring(y, z, width, height, cut):
    x = width / 2
    h = height / 2
    return [(a, y, z+b) for a,b in ((-x+cut,-h),(x-cut,-h),(x,-h+cut),(x,h-cut),
                                     (x-cut,h),(-x+cut,h),(-x,h-cut),(-x,-h+cut))]


def case(part, sections, color):
    part.rings([chamfer_ring(*section) for section in sections], color)


def rib(part, start, end, width, thickness, color):
    part.tube([(start,width,thickness),(end,width,thickness)], color, 8, transport=True)


receiver = Part()
case(receiver, [(-.47,.188,.195,.21,.040),(-.412,.221,.290,.253,.045),
                (-.230,.228,.306,.269,.050),(.119,.226,.293,.260,.050),
                (.298,.227,.252,.215,.043),(.357,.224,.211,.185,.036)],
     lambda row,face: STEEL if face in (3,4,5) else CASE if face in (2,6) else GRAPHITE)
case(receiver,[(-.39,.108,.236,.145,.029),(-.13,.099,.273,.166,.036),
               (.22,.111,.241,.140,.030),(.32,.136,.191,.090,.020)], GRAPHITE)
add("Receiver", receiver)

for side, sign in (("L",-1),("R",1)):
    shell = Part()
    axis=(sign,0,0)

    def panel(points, color, trim, depth):
        shell.solid([(sign*x,y,z) for x,y,z in points], depth, color, trim, axis)

    panel([(.153,-.360,.155),(.162,-.257,.257),(.160,.132,.257),(.149,.236,.223),
           (.155,.171,.148),(.162,-.073,.123),(.160,-.299,.128)], TITANIUM, EDGE, .054)
    panel([(.170,-.269,.189),(.172,-.221,.220),(.165,.105,.221),(.158,.168,.198),
           (.166,.098,.171),(.172,-.225,.167)], JADE, GRAPHITE, .023)
    panel([(.177,-.220,.185),(.178,-.193,.202),(.174,.099,.205),(.168,.134,.195),
           (.174,.091,.183),(.178,-.193,.180)], GLOW, TEAL, .013)
    panel([(.167,-.306,.151),(.174,-.268,.162),(.171,-.251,.133),(.167,-.286,.129)], BRASS, BRONZE, .018)
    for y in (-.342,-.293,-.244):
        panel([(.150,y,.297),(.152,y+.019,.303),(.158,y+.040,.264),(.158,y+.023,.257)], GRAPHITE, STEEL, .014)
    add("Armor_"+side,shell)

grip = Part()
grip.tube([((0,-.265,.126),.093,.119),((0,-.290,.024),.107,.116),
           ((0,-.326,-.106),.102,.105),((0,-.356,-.229),.098,.101),
           ((0,-.370,-.283),.088,.090)], RUBBER, 10, transport=True)
for side in (-1,1):
    for row in range(5):
        z=-.044-row*.042
        y=-.287-row*.012
        rib(grip,(side*.091,y-.061,z),(side*.097,y+.057,z+.013),.011,.009,CASE)
case(grip,[(-.473,-.271,.175,.070,.021),(-.424,-.279,.207,.087,.021),
           (-.319,-.273,.205,.090,.021),(-.277,-.258,.164,.063,.019)],STEEL)
add("Grip",grip,(0,-.325,-.102))

guard=Part()
guard.tube([((0,.115,.116),.044,.037),((0,.133,.034),.050,.039),
            ((0,.082,-.075),.051,.030),((0,-.063,-.117),.047,.032),
            ((0,-.237,-.095),.041,.031)],CASE,8,transport=True)
guard.tube([((0,-.102,.102),.021,.029),((0,-.084,.015),.023,.023),
            ((0,-.127,-.043),.017,.014)],BRASS,8,transport=True)
add("Guard",guard)

top=Part()
case(top,[(-.407,.330,.157,.066,.017),(-.342,.357,.178,.076,.019),
          (.201,.353,.174,.067,.018),(.288,.320,.145,.056,.016)],
     lambda row,face: LIGHT if face==4 else TITANIUM if face in (3,5) else EDGE)
case(top,[(-.371,.389,.097,.057,.012),(-.323,.400,.108,.064,.012),(-.290,.393,.081,.050,.009)],GRAPHITE)
case(top,[(.124,.389,.060,.043,.010),(.157,.406,.075,.058,.012),(.194,.393,.062,.049,.011)],CASE)
case(top,[(.149,.432,.037,.018,.004),(.181,.426,.042,.017,.004)],GLOW)
case(top,[(-.267,.395,.068,.021,.009),(-.221,.398,.077,.021,.009),
          (.037,.394,.067,.020,.009),(.089,.389,.047,.016,.006)],GRAPHITE)
case(top,[(-.215,.407,.033,.012,.004),(-.182,.408,.046,.013,.004),
          (-.018,.405,.041,.012,.004),(.029,.401,.028,.011,.004)],TEAL)
add("TopSpine",top)

emitter=Part()
def circle(y,z,rx,rz):
    return [(rx*math.cos(i*math.tau/12),y,z+rz*math.sin(i*math.tau/12)) for i in range(12)]

# One closed shell folds into a dark recessed emitter cup.
emitter.rings([circle(.225,.228,.102,.100),circle(.312,.228,.129,.120),
               circle(.374,.228,.127,.121),circle(.425,.228,.102,.099),
               circle(.429,.228,.071,.069),circle(.394,.228,.055,.051),
               circle(.362,.228,.052,.048)],
              lambda row,side: (CASE,TITANIUM,EDGE,BRASS,JADE,VOID)[row])
emitter.colors[-1]=TEAL
emitter.rings([circle(.362,.228,.033,.032),circle(.365,.228,.035,.034)],LENS)
add("Emitter",emitter,(0,.32,.228))

unit_scale=.40
grip_center=Vector((0,-.324,-.105))
for obj in parts:
    obj.location = (obj.location-grip_center)*unit_scale
    for vertex in obj.data.vertices:
        vertex.co *= unit_scale
    obj.data.update()
grip_marker=kit.make_marker("GripPoint", (0,0,0),root)
muzzle=kit.make_marker("Muzzle", (Vector((0,.431,.228))-grip_center)*unit_scale,root)

points=world_vertices(parts)
minimum=Vector(min(v[i] for v in points) for i in range(3))
maximum=Vector(max(v[i] for v in points) for i in range(3))
bounds=[round(v,4) for v in maximum-minimum]
triangles=sum(len(face.vertices)-2 for obj in parts for face in obj.data.polygons)
assert .30 < bounds[1] < .45 and triangles < 3000, (bounds,triangles)
validation=validate(parts)
save_model(root, [*parts,grip_marker,muzzle],OUT,NAME)
verify_fbx(OUT/(NAME+".fbx"),len(parts),triangles,bounds)

# Enlarge the preview scene around the model only after saving the actual asset.
preview_scale=3.4
center=(minimum+maximum)*.5
root.scale=(preview_scale,)*3
root.location=-Vector((center.x,center.y,minimum.z))*preview_scale
render_model(OUT,bounds[2]*preview_scale*.51,1.62,54)
report={"meshParts":len(parts),"triangles":triangles,"boundsMetersBlenderXYZ":bounds,
        "blenderForward":"+Y","sourceMarkers":["GripPoint","Muzzle"],"fbxRoundTrip":"PASS",**validation}
(OUT/"VERIFICATION.json").write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8")
print(NAME+" PASS",json.dumps(report),flush=True)
