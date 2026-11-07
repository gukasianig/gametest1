"""Build D3-05 through D3-15. Pass role IDs after -- to rebuild a subset."""

import math
from pathlib import Path
import sys
import traceback
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
import kit
from creature_mesh import Part, finish, make_atlas

OUT = Path(__file__).resolve().parents[2] / "docs/art/EnemyConcepts/Living"
PALETTE = (
    "242037", "453058", "724271", "AE497D", "CD657C", "A84A42", "DC8046", "F1B45F",
    "255456", "408B79", "549BBB", "A3C651", "D5E66D", "E5D6AF", "F6EAD3", "62465A",
)
INK, DARK, PURPLE, PINK, ROSE, RED, ORANGE, AMBER, DEEP, TEAL, BLUE, LIME, EYE, BONE, IVORY, BELLY = range(16)


class Creature:
    def __init__(self, name, number):
        kit.clear_scene()
        self.name = f"{name}_D3-{number}"
        self.out = OUT / f"D3-{number}"
        self.out.mkdir(parents=True, exist_ok=True)
        self.root = kit.make_root(self.name)
        self.material = make_atlas(PALETTE, self.out, name)
        self.parts = []

    def add(self, name, part, pivot):
        self.parts.append(part.build(name, pivot, self.root, self.material))

    def export(self, width, height, frame, yaw, max_height):
        finish(self.root, self.parts, self.out, self.name, width, height, frame, yaw, max_height)


def form(part, position, radii, color, sides=10):
    x, y, z = position
    w, d, h = radii
    part.tube([((x, y+d*t, z), w*s, h*s) for t, s in
               ((-.98, .16), (-.72, .68), (-.34, .94), (.22, 1), (.68, .72), (.97, .22))], color, sides)


def ridge(part, base, tip, width, color, sides=6):
    mid = tuple(a*.46 + b*.54 for a, b in zip(base, tip))
    part.tube([(base, width, width*.74), (mid, width*.61, width*.46),
               (tip, .004, .004)], color, sides, transport=True)


def plate(part, outline, color, trim, thickness=.028, axis=(0, 0, 1)):
    part.solid(outline, thickness, color, trim, axis)


def eyes(part, x, y, z, scale=1):
    for sign in (-1, 1):
        def contour(points, forward):
            return [(sign*(x+u*scale), y+forward-.28*u*scale, z+v*scale) for u,v in points]
        rim = [(-.062, .004), (-.044, .052), (.067, .075), (.076, -.016), (.012, -.033)]
        glow = [(-.044, .012), (-.031, .039), (.056, .059), (.057, -.004), (.010, -.017)]
        slit = [(0, .045), (.021, .048), (.023, -.015), (.007, -.017)]
        plate(part, contour(rim, 0), INK, DARK, .077*scale, (0, 1, 0))
        plate(part, contour(glow, .009*scale), EYE, LIME, .015*scale, (0, 1, 0))
        plate(part, contour(slit, .018*scale), INK, DARK, .013*scale, (0, 1, 0))


def foot(creature, name, joint, bend, pad, radius, color, toes=3):
    leg = Part()
    above = Vector(bend).lerp(Vector(joint), .25)
    below = Vector(bend).lerp(Vector(pad), .27)
    leg.tube([(joint, radius*1.33, radius*1.17), (above, radius*1.16, radius*1.08),
              (bend, radius, radius*.95), (below, radius*.87, radius*.80),
              (pad, radius*.75, radius*.53)], color, 8, transport=True)
    x, y, z = pad
    leg.tube([((x, y-radius*.75, z-.024), radius*.59, radius*.45),
              ((x, y+radius*.70, z-.024), radius*.90, radius*.45)], color, 8)
    for i in range(toes):
        offset = (i-(toes-1)/2)*radius*.62
        ridge(leg, (x+offset, y+radius*.48, z-.020),
              (x+offset*1.3, y+radius*1.48, z-radius*.49-.023), radius*.23, BONE, 5)
    creature.add(name, leg, joint)


def four_feet(creature, front, rear, color):
    for sign, side in ((-1, "L"), (1, "R")):
        for label, (x, y, h, spread, radius) in (("F", front), ("B", rear)):
            foot(creature, "Leg_"+label+side, (sign*x, y, h),
                 (sign*(x+spread), y-.07, h*.46),
                 (sign*(x+spread*.82), y+.045, radius*.55+.026), radius, color)


def boar(c):
    body = Part()
    body.tube([((0,-.58,.48),.20,.20), ((0,-.39,.52),.36,.31), ((0,-.12,.55),.42,.38),
               ((0,.17,.56),.405,.38), ((0,.39,.46),.31,.28)],
              lambda row, side: PURPLE if side > 4 else DARK, 12)
    for i, y in enumerate((.22,.03,-.16,-.35,-.50)):
        ridge(body, (0,y,.79-i*.045), (0,y-.23,1.18-i*.082), .15-i*.019, RED, 7)
    c.add("Body", body, (0,0,.48))
    head = Part()
    head.tube([((0,.21,.48),.263,.271), ((0,.36,.463),.300,.257), ((0,.45,.45),.306,.246), ((0,.66,.36),.255,.177),
               ((0,.84,.34),.196,.122), ((0,.9,.34),.147,.082)], PURPLE, 12)
    form(head, (0,.89,.35), (.169,.065,.09), ROSE, 9)
    eyes(head,.14,.702,.486,1.10)
    for s in (-1,1):
        head.tube([((s*.17,.701,.26),.07,.063), ((s*.285,.825,.239),.058,.054),
                   ((s*.351,.887,.312),.04,.040), ((s*.366,.914,.427),.005,.005)], IVORY, 8, transport=True)
        ridge(head,(s*.232,.429,.602),(s*.337,.254,.796),.088,PINK)
    c.add("Head", head, (0,.29,.45))
    four_feet(c,(.26,.27,.42,.11,.128),(.24,-.39,.48,.09,.115),DARK)


def bomber(c):
    body = Part()
    form(body,(0,0,.573),(.43,.36,.49),ORANGE,14)
    form(body,(0,.226,.51),(.369,.237,.379),lambda row, side: AMBER if side in (1,2,3,4) else LIME,16)
    for x,y,z,r in ((-.116,-.215,.998,.088),(.114,-.181,1.016,.092),(0,.039,1.04,.077)):
        form(body,(x,y,z),(r,r*.85,r*.61),AMBER,8)
    for x,y,z,r in ((-.28,.18,.79,.102),(.29,.16,.78,.108),(-.29,-.06,.90,.082),(.30,-.12,.86,.098),
                    (0,-.15,1.00,.10),(-.15,.37,.92,.085),(.17,.35,.92,.082)):
        form(body,(x,y,z),(r,r*.87,r*.77),RED,7)
    c.add("Body",body,(0,0,.52))
    head=Part()
    form(head,(0,.256,.851),(.282,.22,.171),ORANGE,12)
    form(head,(0,.431,.806),(.182,.075,.034),INK,8)
    eyes(head,.133,.444,.897,1.06)
    c.add("Head",head,(0,.17,.816))
    four_feet(c,(.28,.17,.36,.10,.105),(.25,-.21,.40,.12,.112),RED)


def healer(c):
    body=Part()
    form(body,(0,-.05,.32),(.28,.39,.24),TEAL,12)
    cross=[(-.055,-.22),(.055,-.22),(.055,-.105),(.17,-.105),(.17,.01),(.055,.01),
           (.055,.128),(-.055,.128),(-.055,.01),(-.17,.01),(-.17,-.105),(-.055,-.105)]
    plate(body,[(x,y,.550-.79*x*x-.40*y*y) for x,y in cross],IVORY,TEAL,.037)
    c.add("Body",body,(0,0,.33))
    tail=Part()
    tail.tube([((0,-.32,.33),.125,.117), ((.065,-.55,.33),.101,.083), ((.238,-.66,.42),.072,.06),
               ((.397,-.587,.52),.049,.042), ((.437,-.42,.59),.031,.028), ((.373,-.27,.61),.004,.004)],
              lambda row,side: LIME if row in (2,4) else TEAL,9,transport=True)
    c.add("Tail",tail,(0,-.32,.33))
    head=Part()
    form(head,(0,.32,.356),(.251,.235,.160),TEAL,12)
    eyes(head,.136,.542,.416,.92)
    for s in (-1,1):
        ridge(head,(s*.145,.435,.346),(s*.327,.61,.251),.024,LIME,5)
        ridge(head,(s*.168,.427,.371),(s*.358,.536,.355),.021,EYE,5)
    c.add("Head",head,(0,.20,.34))
    four_feet(c,(.17,.16,.29,.11,.077),(.17,-.26,.30,.11,.083),DEEP)


def splitter(c):
    for s, side, color in ((-1,"L",PINK),(1,"R",TEAL)):
        part=Part()
        form(part,(s*.31,-.12,.335),(.20,.246,.25),color,10)
        form(part,(s*.36,.093,.44),(.213,.228,.217),color,10)
        form(part,(s*.37,.264,.399),(.135,.071,.056),INK,8)
        plate(part,[(s*.258,.298,.455),(s*.289,.288,.518),(s*.444,.254,.537),
                    (s*.447,.272,.461),(s*.345,.294,.439)],EYE,LIME,.038,(0,1,0))
        for i in (-1,1):
            ridge(part,(s*.36+i*.058,.310,.402),(s*.36+i*.058,.315,.365),.019,IVORY,5)
        c.add("Half_"+side,part,(s*.26,0,.33))
        foot(c,"Leg_F"+side,(s*.38,.05,.33),(s*.48,.104,.177),(s*.49,.175,.055),.076,color)
        foot(c,"Leg_B"+side,(s*.29,-.21,.31),(s*.40,-.268,.162),(s*.43,-.22,.054),.072,color)
    seam=Part()
    for y,z,arch in ((.034,.30,-.051),(-.106,.427,.032),(.12,.379,.021)):
        seam.tube([((-.22,y,z),.084,.075),((-.105,y+.027,z+arch*.65),.050,.055),
                   ((0,y+.038,z+arch),.037,.041),((.103,y+.013,z+arch*.57),.051,.053),
                   ((.22,y,z),.082,.073)],LIME,8,transport=True)
    plate(seam,[(-.16,-.127,.436),(0,-.065,.41),(.17,-.116,.432),(.151,.073,.411),
                (0,.092,.349),(-.16,.067,.402)],LIME,DEEP,.041)
    c.add("Seam",seam,(0,0,.38))


def brood(c):
    body=Part()
    form(body,(0,-.13,.57),(.36,.45,.43),PINK,12)
    for y,z in ((-.42,.872),(-.23,.983),(-.015,.993),(.171,.907)):
        ridge(body,(0,y,z-.083),(0,y-.12,z+.08),.11,ORANGE,5)
    c.add("Body",body,(0,0,.57))
    head=Part()
    form(head,(0,.285,.45),(.236,.22,.177),PURPLE,10)
    eyes(head,.118,.472,.504,.91)
    for s in (-1,1):
        ridge(head,(s*.116,.42,.422),(s*.105,.55,.29),.049,BONE)
    c.add("Head",head,(0,.17,.47))
    for s,side in ((-1,"L"),(1,"R")):
        sac=Part()
        form(sac,(s*.315,.175,.401),(.202,.229,.228),DARK,10)
        for i,(dx,dz) in enumerate(((-.078,.067),(.062,.09),(-.099,-.064),(.028,-.052),(.113,-.015))):
            form(sac,(s*(.315+dx),.351-(i%2)*.023,.404+dz),(.063,.054,.068),LIME if i%2 else EYE,7)
        ridge(sac,(s*.204,.275,.514),(s*.420,.364,.548),.049,PURPLE,7)
        ridge(sac,(s*.418,.272,.325),(s*.228,.363,.305),.046,PINK,7)
        c.add("Brood_"+side,sac,(s*.28,.095,.43))
        for i,y in enumerate((.18,.015,-.145,-.31)):
            x=.48+(.13 if i in (1,2) else .05)
            foot(c,f"Leg_{side}{i+1}",(s*.235,y,.50),(s*x,y-.055,.474),
                 (s*(x+.088),y+(.183 if i==0 else -.10),.065),.071,PURPLE,toes=1)


def sniper(c):
    body=Part()
    body.tube([((0,-.47,.487),.072,.083),((0,-.335,.504),.155,.146),((0,-.13,.523),.162,.142),
               ((0,.10,.569),.104,.112),((0,.26,.589),.114,.112)],TEAL,10)
    for s in (-1,1):
        plate(body,[(s*.06,.02,.632),(s*.242,-.081,.713),(s*.47,-.278,.762),(s*.54,-.395,.785),
                    (s*.39,-.450,.746),(s*.276,-.429,.682),(s*.12,-.28,.627)],AMBER,PURPLE,.035)
        ridge(body,(s*.12,-.035,.668),(s*.507,-.377,.782),.030,IVORY,5)
    c.add("Body",body,(0,0,.55))
    head=Part()
    form(head,(0,.274,.618),(.154,.169,.131),PURPLE,10)
    eyes(head,.079,.418,.658,.80)
    head.tube([((0,.387,.611),.070,.063),((0,.546,.591),.045,.041),
               ((0,.82,.562),.028,.026),((0,1.083,.53),.006,.006)],BONE,8,transport=True)
    for s in (-1,1):
        ridge(head,(s*.073,.25,.695),(s*.22,.058,.85),.034,PINK,5)
    c.add("Head",head,(0,.17,.59))
    four_feet(c,(.115,.16,.565,.24,.067),(.12,-.25,.491,.22,.072),PURPLE)


def mortar(c):
    body=Part()
    form(body,(0,-.087,.36),(.369,.377,.303),BLUE,12)
    for x,y,z in ((-.25,-.29,.56),(.26,-.24,.59),(-.32,-.084,.47),(.30,.016,.5),(0,-.33,.641)):
        form(body,(x,y,z),(.072,.071,.064),ORANGE,7)
    c.add("Body",body,(0,0,.36))
    head=Part()
    form(head,(0,.27,.319),(.315,.251,.179),BLUE,12)
    form(head,(0,.455,.276),(.216,.095,.062),BELLY,9)
    eyes(head,.17,.465,.377,1.03)
    c.add("Head",head,(0,.16,.32))
    cup=Part()
    contours=[]
    for row,(y,z,r) in enumerate(((-.14,.497,.111),(-.20,.622,.157),(-.27,.775,.205),(-.33,.913,.265),
                                 (-.319,.907,.208),(-.242,.735,.132),(-.201,.644,.075))):
        ring=[]
        for k in range(18):
            theta=k*math.tau/18
            flare=1+.068*math.cos(theta*6) if row in (2,3,4) else 1
            lift=.026*math.cos(theta*6) if row in (3,4) else 0
            ring.append((r*flare*math.cos(theta), y+r*flare*.85*math.sin(theta), z+r*.39*math.sin(theta)+lift))
        contours.append(ring)
    cup.rings(contours,lambda row, side: (PURPLE,PINK,ROSE,ORANGE,RED,AMBER)[row])
    cup.colors[-1]=EYE
    c.add("SporeSac",cup,(0,-.14,.497))
    four_feet(c,(.252,.16,.29,.098,.106),(.25,-.262,.35,.13,.124),BLUE)


def bat(c):
    body=Part()
    form(body,(0,-.09,.55),(.19,.29,.18),PURPLE,10)
    ridge(body,(0,-.269,.524),(0,-.44,.435),.074,PINK,6)
    c.add("Body",body,(0,0,.55))
    head=Part()
    form(head,(0,.183,.593),(.163,.157,.153),PURPLE,10)
    form(head,(0,.304,.536),(.092,.057,.055),INK,8)
    eyes(head,.081,.337,.624,.72)
    for s in (-1,1):
        ridge(head,(s*.098,.132,.672),(s*.194,.073,.891),.071,PINK)
        ridge(head,(s*.051,.34,.557),(s*.046,.363,.494),.019,IVORY,5)
        ridge(body,(s*.064,-.235,.465),(s*.123,-.324,.335),.042,DARK,6)
    c.add("Head",head,(0,.095,.58))
    for s,side in ((-1,"L"),(1,"R")):
        wing=Part()
        boundary=[(.093,.024,.58),(.27,.110,.689),(.46,.18,.753),(.70,.114,.771),(.923,-.142,.715),
                  (.672,-.212,.635),(.737,-.480,.536),(.498,-.356,.539),(.543,-.552,.464),
                  (.299,-.401,.480),(.248,-.500,.442),(.102,-.301,.491)]
        plate(wing,[(s*x,y,z) for x,y,z in boundary],PINK,PURPLE,.035)
        for x,y,z in (boundary[4],boundary[6],boundary[8],boundary[10]):
            ridge(wing,(s*.455,.157,.752),(s*x,y,z+.014),.035,DARK,6)
        ridge(wing,(s*.107,.028,.59),(s*.476,.167,.762),.058,PURPLE)
        c.add("Wing_"+side,wing,(s*.105,.025,.58))


def buffer(c):
    body=Part()
    body.tube([((0,-.21,.335),.165,.154),((0,-.18,.52),.22,.183),((0,-.04,.68),.183,.153),
               ((0,.14,.82),.114,.092),((0,.18,1.04),.09,.093),((0,.228,1.22),.112,.085)],TEAL,10,transport=True)
    c.add("Body",body,(0,-.10,.46))
    tail=Part()
    tail.tube([((0,-.301,.462),.122,.11),((-.124,-.537,.338),.089,.075),((-.326,-.608,.328),.063,.05),
               ((-.454,-.497,.394),.044,.04),((-.427,-.347,.46),.004,.004)],PINK,8,transport=True)
    c.add("Tail",tail,(0,-.301,.462))
    head=Part()
    form(head,(0,.252,1.216),(.142,.174,.136),TEAL,10)
    form(head,(0,.408,1.217),(.093,.065,.055),INK,8)
    eyes(head,.074,.393,1.264,.81)
    c.add("Head",head,(0,.225,1.16))
    fins=Part()
    for s in (-1,1):
        for index in range(4):
            h=.81+index*.104
            reach=(.33,.461,.501,.417)[index]
            plate(fins,[(s*.074,.147,h-.031),(s*.138,.098,h+.064),
                        (s*(reach*.76),-.067,h+.126),(s*reach,-.155,h+.162),
                        (s*(reach*.85),-.139,h+.075),(s*.202,.066,h-.021)],
                  ORANGE if index%2 else PINK,LIME,.033,(0,1,0))
    c.add("SignalFins",fins,(0,.14,1.04))
    for s,side in ((-1,"L"),(1,"R")):
        foot(c,"Leg_"+side,(s*.144,-.126,.467),(s*.264,-.015,.226),(s*.297,-.04,.064),.091,DEEP)
        foot(c,"Arm_"+side,(s*.125,.016,.735),(s*.241,.164,.659),(s*.283,.243,.663),.039,TEAL,toes=2)


def armored(c):
    body=Part()
    form(body,(0,-.12,.448),(.420,.507,.328),DARK,14)
    # Individual curved bands with shared edges through their thickness. Adjacent
    # bands overlap, and their height offsets keep the surfaces from coinciding.
    shell_sections = ((-.55,.655,.277),(-.397,.774,.376),(-.221,.856,.437),
                      (-.034,.883,.458),(.156,.852,.418),(.337,.735,.330))
    for j, (back, front) in enumerate(zip(shell_sections, shell_sections[1:])):
        band=[]
        for y,z,w in ((back[0]-.020,back[1]-.008,back[2]),
                      (front[0]-.051,front[1]+.025,front[2]),
                      (front[0]+.030,front[1]+.014,front[2]*.97)):
            arch=[(w*math.cos(k*math.pi/12),y,z-.326*abs(math.cos(k*math.pi/12))**1.60) for k in range(13)]
            band.append(arch+[(x*.90,y,h-.066) for x,y,h in reversed(arch)])
        body.rings(band,lambda row, side: ORANGE if side in (0,11) else DARK if side >= 12 else PINK if j%2 else PURPLE)
    c.add("Carapace",body,(0,0,.46))
    head=Part()
    form(head,(0,.323,.367),(.225,.264,.176),DARK,10)
    eyes(head,.113,.543,.439,.92)
    c.add("Head",head,(0,.221,.39))
    for s,side in ((-1,"L"),(1,"R")):
        shield=Part()
        shield.tube([((s*.333,.199,.398),.151,.203),((s*.34,.337,.378),.168,.231),
                     ((s*.291,.505,.32),.167,.207),((s*.23,.598,.261),.124,.146)],BELLY,9)
        for j in range(3):
            h=.22+j*.127
            plate(shield,[(s*.123,.54,h),(s*.265,.625,h+.044),(s*.432,.452,h+.119),
                          (s*.421,.465,h+.049),(s*.267,.636,h-.025)],BONE,ORANGE,.073,(0,1,0))
        c.add("Shield_"+side,shield,(s*.31,.19,.395))
    four_feet(c,(.279,.21,.29,.088,.094),(.278,-.383,.36,.102,.107),DARK)


def guardian(c):
    body=Part()
    body.tube([((0,-.70,.66),.28,.249),((0,-.48,.75),.50,.381),((0,-.18,.82),.583,.445),
               ((0,.19,.795),.562,.404),((0,.425,.697),.411,.301),((0,.63,.635),.318,.241)],
              lambda row,side: ORANGE if side in (8,9) else PURPLE if side > 5 else DARK,12)
    for i,(y,z,w) in enumerate(((-.52,1.05,.104),(-.33,1.205,.132),(-.112,1.24,.155),
                                (.109,1.19,.138),(.305,1.08,.116))):
        ridge(body,(0,y,z-.063),(0,y-.147,z+.16),w,AMBER if i%2 else ORANGE)
        for s in (-1,1):
            ridge(body,(s*.27,y,z-.141),(s*.356,y-.145,z+.025),w*.70,PINK)
    c.add("Body",body,(0,0,.75))
    tail=Part()
    tail.tube([((0,-.601,.67),.246,.232),((.077,-.893,.562),.212,.19),((.279,-1.152,.484),.158,.147),
               ((.538,-1.249,.481),.118,.106),((.781,-1.178,.537),.084,.071),((.943,-1.012,.622),.055,.049),
               ((.955,-.834,.706),.032,.032),((.837,-.729,.749),.007,.006)],
              lambda row,side: ORANGE if row in (1,3,5) and side < 5 else PURPLE,10,transport=True)
    c.add("Tail",tail,(0,-.60,.67))
    head=Part()
    head.tube([((0,.455,.672),.248,.239),((0,.648,.676),.373,.257),((0,.843,.601),.379,.224),
               ((0,1.028,.532),.308,.164),((0,1.196,.484),.247,.111)],PURPLE,12)
    head.tube([((0,.712,.463),.269,.111),((0,.955,.368),.247,.078),((0,1.178,.397),.176,.043)],INK,10)
    head.tube([((0,.675,.385),.239,.082),((0,.921,.283),.239,.066),((0,1.214,.340),.174,.051)],BELLY,10)
    eyes(head,.196,.982,.657,1.34)
    for s in (-1,1):
        head.tube([((s*.231,.64,.792),.125,.10),((s*.358,.59,.94),.092,.081),
                   ((s*.459,.479,1.071),.062,.057),((s*.506,.328,1.163),.007,.006)],IVORY,8,transport=True)
        for x,y,z in ((.192,1.152,.456),(.249,1.058,.484),(.275,.936,.489)):
            ridge(head,(s*x,y,z),(s*(x-.015),y+.028,z-.10),.034,BONE,5)
        for x,y,z in ((.127,1.182,.340),(.183,1.071,.318)):
            ridge(head,(s*x,y,z),(s*(x-.010),y+.018,z+.066),.026,IVORY,5)
    c.add("Head",head,(0,.42,.65))
    four_feet(c,(.383,.317,.59,.23,.173),(.374,-.41,.626,.18,.165),DARK)


# Size is measured along the largest ground axis. Cameras frame each model
# independently. The boss has a separate height allowance.
ROSTER = {
    "05": ("CaveCharger", boar, 1.58, .56, 2.09, 34, 1.40),
    "06": ("CaveBomber", bomber, 1.05, .59, 1.77, 32, 1.45),
    "07": ("CaveHealer", healer, 1.28, .29, 1.73, 32, 1.40),
    "08": ("CaveSplitter", splitter, 1.31, .40, 1.77, 24, 1.40),
    "09": ("CaveBroodmother", brood, 1.52, .49, 2.03, 29, 1.40),
    "10": ("CaveSniper", sniper, 1.61, .44, 2.07, 48, 1.40),
    "11": ("CaveMortar", mortar, 1.25, .51, 1.89, 32, 1.40),
    "12": ("CaveBlinkbat", bat, 1.46, .23, 1.92, 26, 1.40),
    "13": ("CaveAmplifier", buffer, 1.02, .66, 1.94, 26, 1.58),
    "14": ("CaveArmored", armored, 1.40, .44, 1.94, 28, 1.40),
    "15": ("CaveGuardian", guardian, 2.86, .79, 3.68, 41, 2.20),
}


def main():
    selected = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(ROSTER)
    failures = []
    for number in selected:
        try:
            name, build, *framing = ROSTER[number]
            creature = Creature(name, number)
            build(creature)
            creature.export(*framing)
        except Exception:
            traceback.print_exc()
            failures.append(number)
    if failures:
        raise RuntimeError("D3 exports failed: " + ", ".join(failures))


if __name__ == '__main__':
    main()
