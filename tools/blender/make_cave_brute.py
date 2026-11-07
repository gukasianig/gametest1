"""Build D3-03, a heavy cave mutant with a grounded knuckle-walking stance."""

from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).parent))
import kit
from creature_mesh import Part, finish, make_atlas

OUT = Path(__file__).resolve().parents[2] / "docs/art/EnemyConcepts/Living/D3-03"
OUT.mkdir(parents=True, exist_ok=True)

SWATCHES = (
    "291728", "40213F", "63375D", "815078",
    "A76191", "943760", "AB4172", "55223F",
    "AFC75B", "DFE78E", "261426", "CAAB8C",
    "F0DFC2", "72556F", "9E4564", "301F36",
)
INK, DEEP, SHADE, SKIN, LIGHT, MANE, CREST, BERRY, IRIS, EYE, MOUTH, CLAW, TIP, BELLY, GUM, DARK = range(16)

kit.clear_scene()
material = make_atlas(SWATCHES, OUT, "CaveBrute")
root = kit.make_root("D3_03_CaveBrute")
parts = []


def add(part, name, pivot):
    parts.append(part.build(name, pivot, root, material))


def brow_contour(points, sign):
    # Follow the front slope of the muzzle so the eyes stay clear of the skull.
    return [(sign*x, y + .123 - x*.20, z) for x, y, z in points]


body = Part()
body.tube([
    ((0, -.61, .61), .23, .25), ((0, -.46, .68), .43, .36),
    ((0, -.27, .76), .52, .44), ((0, -.04, .84), .62, .46),
    ((0, .20, .82), .60, .42), ((0, .41, .77), .45, .34),
    ((0, .56, .76), .30, .23),
], lambda row, sector: (LIGHT if row < 4 else SKIN) if sector in (8, 9, 10) else BELLY if sector in (2, 3, 4) else SHADE, 13)

# A crown of thick fur breaks up the shoulders and the top silhouette.
for x, y, z, width, rise in (
    (0, .31, 1.151, .148, .142), (-.22, .24, 1.123, .126, .122), (.22, .24, 1.123, .126, .122),
    (0, .05, 1.25, .15, .13), (-.25, -.01, 1.207, .128, .10), (.25, -.01, 1.207, .128, .10),
    (0, -.21, 1.174, .131, .125), (-.20, -.24, 1.148, .104, .095), (.20, -.24, 1.148, .104, .095),
    (0, -.43, 1.00, .11, .10),
):
    body.tube([((x, y+.06, z-.025), width, width*.68), ((x, y-.02, z+rise*.31), width*.79, width*.52),
               ((x*1.09, y-.17, z+rise), .012, .012)],
              lambda row, sector: CREST if sector in (2, 3, 4) else MANE, 6)
add(body, "Body", (0, 0, .81))

head = Part()
head.tube([
    ((0, .39, .92), .195, .195), ((0, .53, .94), .273, .233),
    ((0, .65, .93), .268, .213), ((0, .76, .867), .220, .16),
    ((0, .867, .797), .162, .112), ((0, .91, .789), .122, .079),
], lambda row, sector: SKIN if sector > 6 else DEEP if row < 3 else BELLY, 12)
head.tube([((0, .60, .735), .197, .105), ((0, .75, .693), .194, .093),
           ((0, .886, .711), .149, .066)], MOUTH, 10)
head.tube([((0, .60, .635), .165, .077), ((0, .755, .592), .188, .067),
           ((0, .882, .632), .138, .056), ((0, .927, .662), .091, .039)], BELLY, 10)
head.tube([((0, .839, .813), .127, .072), ((0, .93, .806), .112, .053)], DARK, 8)
head.tube([((0, .759, .752), .168, .045), ((0, .904, .746), .117, .032)], GUM, 8)

for sign in (-1, 1):
    head.solid(brow_contour([(.025, .811, .892), (.08, .784, .958), (.217, .694, .981),
                             (.251, .648, .889), (.148, .764, .850)], sign), .141, INK, DEEP)
    head.solid(brow_contour([(.066, .816, .895), (.108, .807, .928), (.201, .727, .947),
                       (.195, .738, .886), (.122, .791, .875)], sign), .028, EYE, IRIS)
    head.solid(brow_contour([(.110, .819, .916), (.140, .804, .923), (.140, .799, .889),
                             (.115, .811, .884)], sign), .018, INK, DARK)
    head.solid(brow_contour([(.035, .826, .938), (.192, .724, 1.016), (.254, .653, .962),
                             (.206, .742, .938), (.105, .809, .907)], sign), .138, SKIN, LIGHT)
    head.tube([((sign*.212, .627, .899), .087, .086), ((sign*.287, .595, .925), .060, .054),
               ((sign*.318, .571, .962), .024, .026)], SKIN, 8)
    for x, y, length in ((.047, .895, .045), (.109, .863, .082), (.148, .799, .069)):
        head.tube([((sign*x, y, .746), .024, .020), ((sign*(x-.008), y+.020, .746-length), .004, .004)], TIP, 5)
    for x, y, z in ((.057, .890, .658), (.118, .844, .627)):
        head.tube([((sign*x, y, z), .019, .016), ((sign*x, y+.014, z+.058), .004, .004)], CLAW, 5)
add(head, "Head", (0, .41, .87))

for sign, side in ((-1, "L"), (1, "R")):
    shoulder = (sign*.414, .151, .940)
    arm = Part()
    arm.tube([
        (shoulder, .235, .246), ((sign*.622, .231, .917), .269, .272),
        ((sign*.758, .296, .724), .229, .224), ((sign*.797, .327, .518), .173, .170),
        ((sign*.790, .442, .342), .199, .198), ((sign*.765, .552, .177), .170, .155),
    ], lambda row, sector: MANE if row < 2 and sector in (1, 2, 3) else SKIN if sector < 6 else SHADE, 11)
    # Low, broad closed palm with three knuckles reaching the support plane.
    x = sign*.753
    arm.tube([((x, .428, .125), .132, .112), ((x, .561, .123), .205, .120),
              ((x, .721, .120), .204, .116), ((x, .779, .101), .151, .092)], BELLY, 10)
    for offset in (-.119, 0, .119):
        arm.tube([((x+offset, .685, .142), .065, .082), ((x+offset, .782, .118), .064, .099),
                  ((x+offset, .833, .104), .049, .077)], SKIN, 8)
    arm.tube([((x-sign*.161, .555, .139), .076, .068), ((x-sign*.218, .666, .103), .066, .068),
              ((x-sign*.205, .758, .086), .039, .041)], SHADE, 8)
    add(arm, "Leg_F" + side, shoulder)

    hip = (sign*.294, -.352, .622)
    leg = Part()
    leg.tube([(hip, .192, .188), ((sign*.418, -.235, .477), .169, .158),
              ((sign*.441, -.463, .296), .129, .122), ((sign*.455, -.504, .142), .104, .101)],
             lambda row, sector: SKIN if sector < 5 else SHADE, 10)
    x = sign*.449
    leg.tube([((x, -.578, .099), .089, .083), ((x, -.495, .101), .135, .097),
              ((x, -.310, .089), .150, .085), ((x, -.226, .070), .122, .062)], BELLY, 10)
    for offset in (-.077, 0, .077):
        leg.tube([((x+offset, -.270, .065), .037, .049),
                  ((x+offset, -.190, .041), .029, .031)], SHADE, 7)
    add(leg, "Leg_B" + side, hip)

finish(root, parts, OUT, "CaveBrute_D3-03", 1.74, .62, 2.28, showcase_yaw=24)
