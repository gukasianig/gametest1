"""Build the D3-02 cave runner in a mid-stride pose. Blender forward is +Y."""

from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).parent))
import kit
from creature_mesh import Part, finish, make_atlas

OUT = Path(__file__).resolve().parents[2] / "docs/art/EnemyConcepts/Living/D3-02"
OUT.mkdir(parents=True, exist_ok=True)

SWATCHES = (
    "193140", "185658", "268680", "35ABA1",
    "77CDBC", "94C23D", "D8E962", "C94B8C",
    "8D376C", "542641", "171525", "F5E8BE",
    "F9FCDB", "244B61", "E3629D", "597E31",
)
DARK, SHADE, SKIN, LIGHT, AQUA, LIME, GLOW, PINK, BERRY, MOUTH, INK, CLAW, TIP, BLUE, GUM, OLIVE = range(16)

kit.clear_scene()
material = make_atlas(SWATCHES, OUT, "CaveRunner")
root = kit.make_root("D3_02_CaveRunner")
parts = []


def add(part, name, pivot):
    parts.append(part.build(name, pivot, root, material))


def side_contour(points, sign):
    return [(sign * x, y, z) for x, y, z in points]


def dorsal_color(row, sector):
    if sector in (6, 7, 8):
        return LIME if row in (1, 3) and sector != 7 else LIGHT
    if sector in (2, 3):
        return PINK if sector == 2 else BERRY
    return SKIN if sector in (0, 5, 9) else SHADE


body = Part()
body.tube([
    ((0, -.49, .555), .098, .101), ((0, -.36, .582), .185, .159),
    ((0, -.18, .574), .202, .159), ((0, .05, .55), .169, .134),
    ((0, .24, .553), .157, .121), ((0, .35, .585), .131, .105),
    ((0, .45, .62), .121, .098), ((0, .55, .642), .112, .092),
], dorsal_color, 10)
for y, z, size in ((-.36, .703, .07), (-.21, .718, .083), (-.06, .684, .065), (.11, .665, .052), (.29, .669, .044)):
    body.tube([((0, y, z), size*.71, size*.75), ((0, y - size*.60, z + size), .01, .01)], LIME, 5)
add(body, "Body", (0, -.10, .56))

tail = Part()
tail.tube([
    ((0, -.445, .55), .105, .10), ((.03, -.62, .542), .094, .083),
    ((.123, -.79, .57), .080, .070), ((.246, -.914, .635), .068, .063),
    ((.392, -.977, .735), .056, .052), ((.469, -.985, .797), .052, .048),
    ((.55, -.969, .864), .047, .044), ((.614, -.935, .921), .043, .040),
    ((.661, -.877, .967), .039, .036), ((.691, -.733, 1.018), .030, .028),
    ((.641, -.599, 1.02), .022, .019), ((.542, -.524, .991), .014, .013),
    ((.442, -.496, .955), .004, .004),
], lambda row, sector: LIME if row in (2, 5, 8, 10) and sector < 6 else LIGHT if sector < 4 else BERRY if sector in (5, 6) else SHADE,
    10, transport=True)
add(tail, "Tail", (0, -.445, .55))

head = Part()
head.tube([
    ((0, .45, .637), .102, .091), ((0, .51, .657), .123, .103),
    ((0, .56, .671), .153, .121), ((0, .68, .667), .156, .106),
    ((0, .755, .649), .142, .090), ((0, .815, .623), .121, .075),
    ((0, .931, .599), .09, .058), ((0, .984, .589), .053, .032),
], lambda row, sector: (LIGHT if row < 4 else SKIN) if sector in (8, 9, 10) else SHADE if sector > 5 else DARK, 14)
head.tube([((0, .584, .584), .115, .046), ((0, .742, .556), .121, .038),
           ((0, .928, .547), .084, .025)], MOUTH, 8)
head.tube([((0, .597, .574), .100, .043), ((0, .728, .501), .103, .034),
           ((0, .870, .474), .074, .026), ((0, .943, .502), .055, .018)], PINK, 10)

for sign in (-1, 1):
    # Side-facing eyes and swept brow are enclosed at the back of the socket.
    outward = (sign, 0, 0)
    head.solid(side_contour([(.143, .622, .709), (.169, .688, .731), (.156, .775, .700),
                              (.149, .782, .647), (.162, .675, .658)], sign), .057, DARK, INK, outward)
    head.solid(side_contour([(.160, .65, .708), (.178, .704, .723), (.168, .766, .700),
                              (.159, .761, .666), (.169, .679, .673)], sign), .027, GLOW, OLIVE, outward)
    head.solid(side_contour([(.179, .691, .713), (.181, .714, .712), (.172, .725, .676),
                              (.173, .701, .682)], sign), .014, INK, DARK, outward)
    head.tube([((sign*.10, .69, .727), .048, .033), ((sign*.152, .623, .786), .03, .020),
               ((sign*.176, .51, .836), .005, .005)], LIME, 6)
    for x, y, z, length in ((.096, .921, .573, .035), (.111, .843, .576, .061), (.116, .765, .585, .048)):
        head.tube([((sign*x, y, z), .017, .014), ((sign*(x-.006), y+.014, z-length), .003, .003)], TIP, 5)
    for x, y, z in ((.061, .892, .479), (.092, .787, .489)):
        head.tube([((sign*x, y, z), .013, .012), ((sign*x, y+.014, z+.032), .003, .003)], CLAW, 5)
add(head, "Head", (0, .45, .64))


def add_limb(name, sections, claw_length, color, planted=False):
    part = Part()
    part.tube(sections, color, 10)
    x, y, z = sections[-1][0]
    if planted:
        # Wider foot pad supports the pose rather than resting on a single edge.
        part.tube([((x, y-.055, .044), .040, .037), ((x, y+.016, .034), .055, .034),
                   ((x, y+.064, .039), .050, .035)], SHADE, 8)
    for i, offset in enumerate((-.038, .0, .038)):
        length = claw_length * (1.0 if i == 1 else .83)
        part.tube([
            ((x + offset*.55, y + (.038 if planted else -.018), z), .022, .023),
            ((x + offset, y + length*.72, z-.006), .018, .016),
            ((x + offset*1.18, y + length, z-.024), .003, .004),
        ], CLAW, 6)
    add(part, name, sections[0][0])


add_limb("Leg_BL", [
    ((-.135, -.27, .55), .111, .124), ((-.226, -.13, .445), .089, .083),
    ((-.31, -.30, .278), .063, .061), ((-.310, -.435, .163), .050, .052),
    ((-.301, -.525, .077), .040, .048), ((-.342, -.574, .047), .041, .034),
], .118, lambda row, side: LIGHT if side < 4 else SHADE, planted=True)

add_limb("Leg_BR", [
    ((.132, -.245, .555), .113, .12), ((.249, -.399, .481), .094, .079),
    ((.327, -.497, .356), .059, .061), ((.38, -.356, .232), .044, .044),
    ((.399, -.218, .205), .041, .034),
], .108, lambda row, side: SKIN if side < 5 else SHADE)

add_limb("Leg_FL", [
    ((-.111, .276, .55), .063, .064), ((-.171, .332, .428), .050, .048),
    ((-.225, .502, .362), .038, .034), ((-.254, .621, .342), .038, .026),
], .083, lambda row, side: LIGHT if side < 3 else SKIN)

add_limb("Leg_FR", [
    ((.115, .268, .551), .063, .062), ((.215, .224, .419), .051, .049),
    ((.267, .170, .334), .040, .036), ((.291, .259, .295), .036, .025),
], .083, lambda row, side: SKIN if side < 5 else SHADE)

finish(root, parts, OUT, "CaveRunner_D3-02", 1.42, .36, 1.79, showcase_yaw=58)
