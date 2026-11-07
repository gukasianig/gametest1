"""Build D3-04, the frilled cave spitter, facing +Y in Blender."""

import math
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).parent))
import kit
from creature_mesh import Part, finish, make_atlas

OUT = Path(__file__).resolve().parents[2] / "docs/art/EnemyConcepts/Living/D3-04"
OUT.mkdir(parents=True, exist_ok=True)
PALETTE = (
    "162C30", "245457", "327F78", "4B9D87", "79B799", "AFC84A", "D7E67A", "6D8838",
    "943B72", "BE5586", "DC8196", "502745", "211A27", "D7BE95", "F1E4BD", "AF744E",
)
DARK, SHADE, SKIN, LIGHT, JADE, LIME, EYE, OLIVE, PLUM, PINK, ROSE, GUM, MOUTH, CLAW, TIP, GOLD = range(16)

kit.clear_scene()
material = make_atlas(PALETTE, OUT, "CaveSpitter")
root = kit.make_root("D3_04_CaveSpitter")
parts = []


def add(part, name, pivot):
    parts.append(part.build(name, pivot, root, material))


def mirror(points, sign):
    return [(sign*x, y, z) for x, y, z in points]


body = Part()
body.tube([
    ((0, -.18, .29), .135, .125), ((0, -.14, .43), .225, .173),
    ((0, -.045, .61), .207, .172), ((0, .015, .76), .172, .151),
    ((0, .09, .91), .125, .105), ((0, .166, 1.00), .105, .100),
], lambda row, side: OLIVE if side in (1, 2, 3) else LIGHT if side in (7, 8) else SKIN, 12, transport=True)
add(body, "Body", (0, -.13, .45))

tail = Part()
tail.tube([
    ((0, -.23, .408), .130, .119), ((-.04, -.39, .324), .104, .093),
    ((-.19, -.55, .242), .078, .076), ((-.38, -.59, .203), .062, .057),
    ((-.53, -.48, .203), .046, .044), ((-.59, -.32, .241), .032, .030),
    ((-.55, -.187, .312), .021, .020), ((-.43, -.124, .385), .012, .012),
    ((-.34, -.156, .425), .003, .003),
], lambda row, side: LIME if row in (2, 5) else LIGHT if side > 4 else SKIN, 9, transport=True)
add(tail, "Tail", (0, -.23, .408))

# Closed, scalloped membrane with raised ribs. Its outline fans out above the
# shoulders, so the silhouette is distinct even from the steep arena camera.
frill = Part()
count = 16
rim = []
for i in range(count):
    angle = math.radians(-26 + 232*i/(count-1))
    radius = .47 if i % 2 == 0 else .53
    rim.append((math.cos(angle)*radius, .112 - .34*abs(math.cos(angle)), .955 + math.sin(angle)*radius*.76))
rim += [(-.18, -.04, .714), (0, .04, .678), (.18, -.04, .714)]
center = (0, .116, .955)
inner = [tuple(center[j] + (point[j]-center[j])*.82 for j in range(3)) for point in rim]
back = [(x, y-.032, z) for x, y, z in rim]
frill.rings([back, rim, inner], lambda row, side: PLUM if row == 0 else LIME)
cap = frill.faces.pop()
frill.colors.pop()
hub = len(frill.vertices)
frill.vertices.append(center)
for i, vertex in enumerate(cap):
    frill.faces.append((vertex, cap[(i+1) % len(cap)], hub))
    frill.colors.append(PINK if i % 4 < 2 else PLUM)
for x, y, z in rim[1:count:2]:
    frill.tube([((x*.22, .121, .955+(z-.955)*.20), .023, .016),
                ((x*.64, .136+(y-.116)*.64, .955+(z-.955)*.64), .017, .013),
                ((x*.97, y+.006, .955+(z-.955)*.97), .004, .004)], LIME, 5)
add(frill, "Frill", (0, .08, .95))

throat = Part()
throat.tube([
    ((0, .105, .675), .117, .082), ((0, .179, .691), .171, .137),
    ((0, .273, .726), .185, .169), ((0, .361, .771), .158, .148),
    ((0, .404, .82), .111, .104),
], lambda row, side: ROSE if side in (7, 8, 9) else PINK if side > 4 else PLUM, 12)
add(throat, "Throat", (0, .145, .762))

head = Part()
head.tube([
    ((0, .114, .976), .117, .114), ((0, .239, 1.014), .184, .135),
    ((0, .339, .997), .170, .114), ((0, .446, .955), .136, .082),
    ((0, .522, .95), .079, .051),
], lambda row, side: LIGHT if side > 6 else SKIN if side > 3 else SHADE, 12)
head.tube([((0, .272, .929), .121, .043), ((0, .404, .908), .113, .046),
           ((0, .52, .916), .070, .027)], MOUTH, 8)
head.tube([((0, .274, .896), .108, .040), ((0, .386, .866), .107, .028),
           ((0, .509, .89), .070, .025)], JADE, 8)

for sign in (-1, 1):
    side = (sign, 0, 0)
    head.solid(mirror([(.155, .239, 1.050), (.171, .310, 1.078), (.164, .382, 1.031),
                       (.146, .362, .981), (.170, .270, .985)], sign), .048, DARK, SHADE, side)
    head.solid(mirror([(.174, .258, 1.035), (.184, .313, 1.057), (.173, .363, 1.028),
                       (.163, .349, .997), (.181, .280, .997)], sign), .019, EYE, OLIVE, side)
    head.solid(mirror([(.187, .309, 1.049), (.187, .329, 1.046), (.183, .326, 1.004),
                       (.186, .309, 1.004)], sign), .012, MOUTH, DARK, side)
    for x, y in ((.091, .464), (.107, .412)):
        head.tube([((sign*x, y, .935), .015, .014), ((sign*x, y+.01, .896), .004, .004)], TIP, 5)
add(head, "Head", (0, .14, .96))

for sign, side in ((-1, "L"), (1, "R")):
    hip = (sign*.144, -.12, .431)
    knee = (sign*.283, .01, .262)
    ankle = (sign*.310, -.196, .078)
    x = sign*.325
    leg = Part()
    leg.tube([(hip, .100, .109), (knee, .092, .085), (ankle, .054, .055),
              ((x, -.068, .050), .062, .042)],
             lambda row, sector: LIGHT if sector in (1, 2, 3) else SKIN, 9)
    leg.tube([((x, -.165, .038), .052, .037), ((x, -.052, .040), .069, .039),
              ((x, .035, .036), .061, .035)], SHADE, 8)
    for i in (-1, 0, 1):
        leg.tube([((x+i*.040, .014, .038), .024, .028), ((x+i*.063, .132, .023), .018, .017),
                  ((x+i*.074, .173, .011), .003, .003)], CLAW, 6)
    add(leg, "Leg_" + side, hip)

    shoulder = (sign*.137, .056, .736)
    arm = Part()
    arm.tube([(shoulder, .065, .070), ((sign*.274, .099, .628), .054, .054),
              ((sign*.336, .246, .595), .043, .044), ((sign*.366, .311, .575), .045, .031)], SKIN, 8)
    for i in (-1, 0, 1):
        arm.tube([((sign*.365+i*.027, .306, .575), .017, .018),
                  ((sign*.381+i*.033, .370, .563), .014, .014),
                  ((sign*.388+i*.037, .394, .529), .003, .003)], TIP, 5)
    add(arm, "Arm_" + side, shoulder)

finish(root, parts, OUT, "CaveSpitter_D3-04", 1.16, .59, 1.80, showcase_yaw=34)
