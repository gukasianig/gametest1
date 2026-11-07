"""Build and render the D3-01 cave imp. Run Blender in background with -P.

The source faces +Y in Blender. The origin is on the ground. Named mesh parts
retain joint pivots for later rigging. The atlas uses one solid swatch per face.
"""

from pathlib import Path
import sys

from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
import kit
from creature_mesh import Part, finish, make_atlas

OUT = Path(__file__).resolve().parents[2] / "docs/art/EnemyConcepts/Living/D3-01"
OUT.mkdir(parents=True, exist_ok=True)
FOOTPRINT = 1.23

# Colors are authored in sRGB. The 4 × 4 atlas gives each swatch 16 px of padding.
SWATCHES = (
    "28172F", "42224D", "5D2D65", "843B83",
    "B9509C", "D96AB0", "F08872", "A63B65",
    "E5F353", "A6C53B", "1A1022", "E7C49C",
    "FFF2CE", "764169", "6C273F", "F5A260",
)
INK, DEEP, SHADE, SKIN, PINK, LIGHT, EAR, EAR_DARK, EYE, IRIS, MOUTH, CLAW, TIP, BELLY, GUM, GLOW = range(16)


kit.clear_scene()
material = make_atlas(SWATCHES, OUT, "CaveImp")
root = kit.make_root("D3_01_CaveImp")
parts = []


def add(part, name, pivot):
    obj = part.build(name, pivot, root, material)
    parts.append(obj)
    return obj


body = Part()
body.tube([
    ((0, -.47, .37), .12, .12), ((0, -.31, .42), .245, .19),
    ((0, -.17, .46), .255, .205), ((0, -.05, .49), .255, .22),
    ((0, .08, .522), .295, .226), ((0, .19, .54), .31, .21),
    ((0, .36, .55), .21, .15),
], lambda row, side: (PINK if row == 3 else SKIN) if side < 5 else BELLY if side in (6, 7, 8) else SHADE, 10)
for y, z in ((-.36, .55), (-.24, .62), (-.10, .68), (.06, .71)):
    body.tube([((0, y, z), .095, .08), ((0, y - .09, z + .12), .02, .019)], PINK, 5)
add(body, "Body", (0, 0, .46))

tail = Part()
tail.tube([
    ((0, -.39, .39), .117, .11), ((-.055, -.495, .36), .111, .098),
    ((-.11, -.56, .35), .099, .086), ((-.21, -.628, .338), .086, .075),
    ((-.29, -.65, .34), .077, .065), ((-.377, -.647, .372), .066, .059),
    ((-.44, -.62, .42), .057, .053), ((-.516, -.564, .49), .049, .046),
    ((-.55, -.49, .55), .043, .04), ((-.552, -.413, .602), .029, .028),
    ((-.54, -.35, .64), .015, .015),
    ((-.49, -.29, .67), .005, .005),
], lambda row, side: PINK if row >= 8 else SKIN if side < 4 else SHADE, 9)
add(tail, "Tail", (0, -.39, .39))

head = Part()
head.tube([
    ((0, .14, .65), .17, .15), ((0, .23, .67), .248, .197),
    ((0, .31, .68), .29, .23), ((0, .40, .68), .297, .222),
    ((0, .48, .66), .285, .20), ((0, .56, .63), .238, .161), ((0, .63, .60), .18, .13),
    ((0, .70, .56), .095, .078),
], lambda row, side: (PINK if row < 4 else SKIN) if side < 6 else DEEP, 14)
head.tube([((0, .34, .49), .20, .081), ((0, .51, .46), .23, .087), ((0, .62, .452), .19, .07),
           ((0, .70, .46), .15, .053)], MOUTH, 10)
head.tube([((0, .34, .43), .17, .073), ((0, .46, .389), .19, .064), ((0, .55, .37), .198, .052),
           ((0, .65, .389), .16, .048), ((0, .73, .43), .105, .043)], BELLY, 12)
head.tube([((0, .57, .52), .166, .047), ((0, .714, .514), .10, .034)], GUM, 8)
head.tube([((0, .65, .602), .08, .055), ((0, .735, .568), .057, .034)], DEEP, 8)

for sign in (-1, 1):
    def mirror(points):
        return [(sign * x, y, z) for x, y, z in points]

    # Layered angular sockets and large forward-facing lime irises.
    head.solid(mirror([(.034, .684, .636), (.085, .683, .716), (.247, .57, .748), (.282, .496, .662), (.173, .621, .605), (.069, .671, .61)]), .089, INK, DEEP)
    head.solid(mirror([(.062, .699, .646), (.104, .690, .682), (.233, .592, .717), (.227, .610, .661), (.172, .646, .626), (.093, .694, .623)]), .025, EYE, IRIS)
    head.solid(mirror([(.138, .682, .677), (.161, .671, .682), (.164, .660, .637), (.144, .678, .634)]), .019, MOUTH, INK)
    head.solid(mirror([(.087, .69, .71), (.25, .571, .795), (.282, .497, .746), (.238, .603, .713), (.123, .68, .677)]), .102, SKIN, PINK)
    for x, y, z, length in ((.043, .698, .511, .040), (.104, .677, .521, .070), (.147, .616, .523, .050)):
        head.tube([((sign*x, y, z), .026, .020), ((sign*(x-.007), y+.018, z-length), .004, .004)], TIP, 5)
    for x, y, z in ((.065, .709, .422), (.121, .650, .399)):
        head.tube([((sign*x, y, z), .019, .016), ((sign*x, y+.012, z+.045), .004, .004)], CLAW, 5)
    head.tube([((sign*.242, .454, .57), .076, .075), ((sign*.351, .41, .545), .009, .013)], PINK, 5)
add(head, "Head", (0, .21, .61))

for sign, side in ((-1, "L"), (1, "R")):
    ear = Part()
    contour = [(.129, .24, .719), (.126, .209, .825), (.139, .17, .93), (.18, .125, 1.065),
               (.24, .06, 1.19), (.348, -.005, 1.294), (.46, -.046, 1.36),
               (.484, -.028, 1.255), (.486, .006, 1.15), (.462, .064, 1.048),
               (.41, .122, .946), (.353, .173, .854), (.27, .218, .787)]
    # Connected rim and bowl prevent gaps between the former layered plates.
    core = Vector((.315, .132, 1.039))
    ear_rings = []
    for factor, depth in ((.94, -.076), (1.00, -.021), (.76, .011), (.47, -.024)):
        ring = []
        for point in contour:
            x, y, z = core.lerp(Vector(point), factor)
            ring.append((sign*x, y + depth, z))
        ear_rings.append(ring)
    ear.rings(ear_rings, lambda row, side: (PINK, LIGHT, EAR)[row])
    ear.colors[0] = SHADE
    ear.colors[-1] = GLOW
    add(ear, "Ear_" + side, (sign*.17, .22, .76))

    for front in (True, False):
        # Wide front reach and folded rear hocks suggest a creature poised to spring.
        if front:
            joint, elbow, wrist, toe = ((sign*.24, .15, .57), (sign*.438, .24, .32),
                                       (sign*.466, .426, .13), (sign*.526, .532, .070))
        else:
            joint, elbow, wrist, toe = ((sign*.18, -.31, .45), (sign*.365, -.22, .31),
                                       (sign*.402, -.48, .11), (sign*.434, -.344, .062))
        leg = Part()
        upper = Vector(joint).lerp(Vector(elbow), .48)
        lower = Vector(elbow).lerp(Vector(wrist), .55)
        leg.tube([(joint, .119, .122), (upper, .118, .11), (elbow, .095, .095),
                  (lower, .081, .083), (wrist, .073, .07), (toe, .092, .045)],
                 lambda row, face: PINK if face in (0, 1) and row < 2 else SKIN if face < 5 else SHADE, 9)
        for i, spread in enumerate((-.078, 0, .078)):
            x, y, z = toe
            dy = .155 if i == 1 else .116
            leg.tube([
                ((x + spread*.54, y - .035, z+.021), .045, .043),
                ((x + spread, y + .079, .046), .044, .039),
            ], SHADE, 7)
            leg.tube([
                ((x + spread*.93, y + .053, .057), .037, .036),
                ((x + spread*1.26, y + dy, .048), .029, .025),
                ((x + spread*1.46, y + dy + .028, .032), .019, .017),
                ((x + spread*1.62, y + dy + .052, .009), .006, .008),
            ], lambda row, face: TIP if face in (2, 3, 4, 5) else CLAW, 7)
        add(leg, "Leg_" + ("F" if front else "B") + side, joint)

finish(root, parts, OUT, "CaveImp_D3-01", FOOTPRINT, .46, 1.78)
