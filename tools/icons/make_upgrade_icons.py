"""Рисует значки карточек улучшений (PNG 256x256, прозрачный фон) в стиле игры:
графит и металл, тёмный контур, бирюзовые акценты, блик сверху-слева.
Запуск:  python tools/icons/make_upgrade_icons.py   (нужен Pillow)
Результат: Assets/Resources/UI/Upgrades/<UpgradeEffect>.png + лист-превью Logs/upgrade-icons-sheet.png
"""
import math, os, sys
from PIL import Image, ImageDraw, ImageChops, ImageFilter

BIG, OUT = 1024, 256
K = BIG / 100.0
DARK = (14, 20, 27, 255)
OUTDIR = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Resources', 'UI', 'Upgrades')

# палитра (светлый, тёмный)
def pal(c, lo=0.62, hi=1.22):
    r, g, b = c
    return (tuple(min(255, int(v * hi + 10)) for v in c), tuple(int(v * lo) for v in c))

TEAL, BLUE, RED, GOLD, ORANGE = (60, 205, 195), (85, 140, 245), (225, 55, 60), (250, 205, 70), (250, 140, 45)
STEEL, GRAPH, WHITE, GREEN, PURPLE = (150, 165, 182), (70, 80, 96), (235, 242, 248), (105, 215, 115), (165, 105, 235)
BRASS, CRIMSON, GLASS = (214, 160, 70), (170, 25, 50), (190, 228, 250)


def rot(pts, deg, cx=50, cy=50):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def draw_shape(d, shape):
    t = shape[0]
    if t == 'poly': d.polygon([(x * K, y * K) for x, y in shape[1]], fill=255)
    elif t == 'circ':
        _, cx, cy, r = shape; d.ellipse([(cx - r) * K, (cy - r) * K, (cx + r) * K, (cy + r) * K], fill=255)
    elif t == 'rr':
        _, x0, y0, x1, y1, r = shape; d.rounded_rectangle([x0 * K, y0 * K, x1 * K, y1 * K], r * K, fill=255)
    elif t == 'line':
        _, pts, w = shape
        p = [(x * K, y * K) for x, y in pts]
        d.line(p, fill=255, width=int(w * K), joint='curve')
        for x, y in p: d.ellipse([x - w * K / 2, y - w * K / 2, x + w * K / 2, y + w * K / 2], fill=255)
    elif t == 'arc':
        _, cx, cy, r, a0, a1, w = shape
        d.arc([(cx - r) * K, (cy - r) * K, (cx + r) * K, (cy + r) * K], a0, a1, fill=255, width=int(w * K))
    elif t == 'star':
        _, cx, cy, ro, ri, n, rt = shape
        pts = []
        for i in range(n * 2):
            a = math.radians(rt + i * 180 / n); rr = ro if i % 2 == 0 else ri
            pts.append(((cx + math.cos(a) * rr) * K, (cy + math.sin(a) * rr) * K))
        d.polygon(pts, fill=255)


def render(parts):
    img = Image.new('RGBA', (BIG, BIG), (0, 0, 0, 0))
    # общая тень под предметом
    shadow = Image.new('L', (BIG, BIG), 0)
    for p in parts:
        if p[3]: draw_shape(ImageDraw.Draw(shadow), p[0])
    shadow = ImageChops.offset(shadow, 10, 22).filter(ImageFilter.GaussianBlur(14))
    img.paste((0, 0, 0, 90), (0, 0), shadow.point(lambda v: v * 0.5))
    import random
    random.seed(7)
    # мягкая «лампа» сверху-слева для глянца
    lamp = Image.new('L', (BIG, BIG), 0)
    ld = ImageDraw.Draw(lamp)
    ld.ellipse([BIG * 0.02, BIG * -0.05, BIG * 0.62, BIG * 0.55], fill=255)
    lamp = lamp.filter(ImageFilter.GaussianBlur(120))
    noise = Image.effect_noise((BIG, BIG), 22).convert('L')
    for shape, top, bot, outline in parts:
        mask = Image.new('L', (BIG, BIG), 0)
        draw_shape(ImageDraw.Draw(mask), shape)
        if outline:
            dil = mask.filter(ImageFilter.MaxFilter(13))
            img.paste((8, 12, 18, 255), (0, 0), dil)
        # трёхточечный градиент: светлый верх, база, тёмный низ
        grad = Image.new('RGBA', (1, BIG))
        for y in range(BIG):
            t = y / (BIG - 1)
            if t < 0.4: u = t / 0.4; c = [top[i] * 1.12 + (top[i] - bot[i]) * 0.0 + 0 for i in range(3)]; c = [min(255, top[i] * (1.12 - 0.12 * u)) for i in range(3)]
            else: u = (t - 0.4) / 0.6; c = [top[i] + (bot[i] - top[i]) * u for i in range(3)]
            grad.putpixel((0, y), tuple(int(v) for v in c) + (255,))
        fill = grad.resize((BIG, BIG))
        # зерно красочного слоя
        grain = ImageChops.multiply(noise, mask).point(lambda v: v)
        img.paste(fill, (0, 0), mask)
        # внутренняя тень по краю (объём) — по всему контуру
        inner = ImageChops.subtract(mask, mask.filter(ImageFilter.MinFilter(21)).filter(ImageFilter.GaussianBlur(7)))
        img.paste((0, 0, 12, 255), (0, 0), inner.point(lambda v: int(v * 0.45)))
        # глянец от лампы
        gloss = ImageChops.multiply(mask, lamp)
        img.paste((255, 255, 255, 255), (0, 0), gloss.point(lambda v: int(v * 0.30)))
        # светлая кромка сверху-слева и острый блик
        rim = ImageChops.subtract(mask, ImageChops.offset(mask, 14, 14)).filter(ImageFilter.GaussianBlur(3))
        img.paste((255, 255, 255, 255), (0, 0), rim.point(lambda v: int(v * 0.75)))
        spec = ImageChops.subtract(mask.filter(ImageFilter.MinFilter(31)), ImageChops.offset(mask.filter(ImageFilter.MinFilter(31)), 12, 12)).filter(ImageFilter.GaussianBlur(4))
        img.paste((255, 255, 255, 255), (0, 0), spec.point(lambda v: int(v * 0.5)))
        # зерно
        gn = ImageChops.multiply(mask, noise.point(lambda v: abs(v - 128) * 2 if abs(v - 128) > 18 else 0))
        img.paste((255, 255, 255, 255), (0, 0), gn.point(lambda v: int(v * 0.05)))
    return img.resize((OUT, OUT), Image.LANCZOS)


def P(shape, color, outline=True):
    hi, lo = pal(color); return (shape, hi, lo, outline)

def flat(shape, color, outline=False): return (shape, color, color, outline)

# ---------- составные предметы ----------
def bullet(cx, cy, deg=0, scale=1.0, body=BRASS, tip=(205, 90, 60)):
    b = [(cx - 9 * scale, cy - 6 * scale), (cx + 9 * scale, cy - 6 * scale), (cx + 9 * scale, cy + 26 * scale), (cx - 9 * scale, cy + 26 * scale)]
    t = [(cx - 9 * scale, cy - 6 * scale), (cx + 9 * scale, cy - 6 * scale), (cx + 6 * scale, cy - 22 * scale), (cx, cy - 32 * scale), (cx - 6 * scale, cy - 22 * scale)]
    ring = [(cx - 9 * scale, cy + 20 * scale), (cx + 9 * scale, cy + 20 * scale), (cx + 9 * scale, cy + 26 * scale), (cx - 9 * scale, cy + 26 * scale)]
    return [P(('poly', rot(b, deg, cx, cy)), body), P(('poly', rot(t, deg, cx, cy)), tip), P(('poly', rot(ring, deg, cx, cy)), STEEL, False)]

def plus(cx, cy, c=GREEN):
    return [P(('circ', cx, cy, 11), c), flat(('line', [(cx - 5, cy), (cx + 5, cy)], 3.2), WHITE), flat(('line', [(cx, cy - 5), (cx, cy + 5)], 3.2), WHITE)]

def drone(cx=50, cy=50):
    return [P(('line', [(cx - 24, cy - 2), (cx + 24, cy - 2)], 6), GRAPH),
            P(('rr', cx - 40, cy - 14, cx - 12, cy - 7, 3), STEEL), P(('rr', cx + 12, cy - 14, cx + 40, cy - 7, 3), STEEL),
            P(('circ', cx, cy + 2, 17), TEAL), P(('circ', cx, cy + 2, 8), (20, 35, 45)), flat(('circ', cx - 2, cy, 3), WHITE)]

def turret(cx=50, cy=50):
    return [P(('line', [(cx, cy + 6), (cx - 24, cy + 36)], 6), GRAPH), P(('line', [(cx, cy + 6), (cx + 24, cy + 36)], 6), GRAPH),
            P(('rr', cx - 6, cy - 4, cx + 40, cy + 8, 3), STEEL), P(('circ', cx, cy, 17), TEAL), P(('circ', cx, cy, 7), (20, 35, 45))]

def boot(cx=50, cy=50, c=TEAL):
    return [P(('poly', [(cx - 28, cy - 28), (cx - 4, cy - 28), (cx - 2, cy + 2), (cx + 22, cy + 6), (cx + 34, cy + 18), (cx + 34, cy + 28), (cx - 28, cy + 28)]), c),
            P(('rr', cx - 30, cy + 20, cx + 36, cy + 33, 4), GRAPH), flat(('line', [(cx - 24, cy - 14), (cx - 8, cy - 14)], 3), WHITE)]

def heart(cx=50, cy=50, s=1.0, c=RED):
    return [P(('circ', cx - 15 * s, cy - 9 * s, 17 * s), c), P(('circ', cx + 15 * s, cy - 9 * s, 17 * s), c),
            P(('poly', [(cx - 31 * s, cy - 2 * s), (cx + 31 * s, cy - 2 * s), (cx, cy + 34 * s)]), c)]

def arrow_head(x, y, deg, s=1.0, c=TEAL):
    return P(('poly', rot([(x, y - 9 * s), (x + 14 * s, y), (x, y + 9 * s)], deg, x, y)), c)

# ---------- иконки ----------
def make():
    I = {}
    I['CritChance'] = [P(('circ', 50, 50, 41), STEEL), P(('circ', 50, 50, 31), WHITE, False), P(('circ', 50, 50, 21), RED, False),
                       P(('circ', 50, 50, 10), WHITE, False), flat(('star', 50, 50, 9, 3.4, 4, -45), RED),
                       P(('line', [(50, 3), (50, 16)], 5), TEAL), P(('line', [(50, 84), (50, 97)], 5), TEAL),
                       P(('line', [(3, 50), (16, 50)], 5), TEAL), P(('line', [(84, 50), (97, 50)], 5), TEAL)]
    I['CritPower'] = [P(('star', 50, 50, 47, 25, 9, -90), ORANGE), P(('star', 50, 50, 34, 19, 9, -70), GOLD, False)] + bullet(50, 52, 0, 0.85)
    I['Pierce'] = [P(('rr', 16, 22, 32, 78, 3), GRAPH), P(('rr', 66, 22, 82, 78, 3), GRAPH)] + \
                  [P(('poly', [(8, 43), (62, 43), (62, 57), (8, 57)]), BRASS), P(('poly', [(62, 43), (74, 43), (92, 50), (74, 57), (62, 57)]), (205, 90, 60)),
                   flat(('circ', 24, 50, 4), DARK), P(('line', [(4, 36), (20, 36)], 3), TEAL, False), P(('line', [(4, 64), (20, 64)], 3), TEAL, False)]
    I['Ricochet'] = [P(('rr', 74, 12, 90, 88, 3), GRAPH), P(('line', [(10, 84), (68, 42)], 6), TEAL), P(('line', [(68, 42), (30, 16)], 6), TEAL),
                     P(('star', 70, 42, 17, 7, 8, 0), GOLD), arrow_head(27, 15, 200, 1, BRASS), P(('circ', 24, 12, 7), BRASS)]
    I['DroneCount'] = drone(46, 48) + plus(80, 78)
    I['DroneDamage'] = drone(50, 34) + [P(('poly', [(46, 54), (54, 54), (66, 90), (34, 90)]), GOLD, False), P(('star', 50, 90, 12, 5, 8, 0), ORANGE)]
    I['TurretCount'] = turret(40, 44) + plus(80, 78)
    I['TurretDamage'] = turret(38, 46) + [P(('star', 86, 46, 15, 6, 8, 0), ORANGE), P(('star', 86, 46, 8, 3, 6, 10), GOLD, False)]
    I['Armor'] = [P(('poly', [(50, 6), (88, 20), (86, 58), (50, 94), (14, 58), (12, 20)]), STEEL),
                  P(('poly', [(50, 18), (76, 28), (74, 54), (50, 80), (26, 54), (24, 28)]), TEAL, False),
                  flat(('line', [(50, 24), (50, 74)], 3), WHITE), flat(('circ', 24, 22, 3), DARK), flat(('circ', 76, 22, 3), DARK)]
    I['Regeneration'] = heart(50, 52, 0.85) + [P(('arc', 50, 50, 42, 200, 320, 7), TEAL), arrow_head(90, 44, 90, 1.0, TEAL),
                                              P(('arc', 50, 50, 42, 20, 140, 7), TEAL), arrow_head(10, 56, 270, 1.0, TEAL)]
    I['Vampirism'] = [P(('poly', [(50, 6), (22, 52), (78, 52)]), CRIMSON), P(('circ', 50, 62, 29), CRIMSON),
                      flat(('poly', [(34, 68), (46, 68), (40, 86)]), WHITE, True), flat(('poly', [(54, 68), (66, 68), (60, 86)]), WHITE, True),
                      flat(('line', [(34, 66), (66, 66)], 4), DARK)]
    I['DashCooldown'] = boot(40, 58) + [P(('circ', 74, 28, 19), WHITE), flat(('line', [(74, 28), (74, 16)], 3.5), DARK), flat(('line', [(74, 28), (84, 33)], 3.5), RED)]
    I['DashDistance'] = boot(42, 56) + [P(('line', [(4, 40), (22, 40)], 4), TEAL, False), P(('line', [(2, 56), (18, 56)], 4), TEAL, False),
                                       P(('line', [(6, 72), (22, 72)], 4), TEAL, False), P(('poly', [(72, 20), (92, 36), (72, 52), (72, 42), (58, 42), (58, 30), (72, 30)]), GOLD)]
    I['ExtraPellets'] = bullet(26, 58, -22, 0.7) + bullet(74, 58, 22, 0.7) + bullet(50, 54, 0, 0.8)
    I['ExplosiveBullets'] = [P(('star', 50, 24, 26, 12, 9, -90), ORANGE), P(('star', 50, 24, 15, 7, 7, -80), GOLD, False)] + bullet(50, 66, 0, 0.8)
    I['EliteDamage'] = [P(('poly', [(14, 72), (14, 34), (32, 52), (50, 22), (68, 52), (86, 34), (86, 72)]), GOLD), P(('rr', 14, 70, 86, 82, 3), BRASS),
                        flat(('circ', 50, 54, 4), RED), P(('arc', 50, 52, 40, 0, 360, 5), RED, False), P(('line', [(50, 4), (50, 20)], 5), RED), P(('line', [(50, 84), (50, 98)], 5), RED)]
    I['WeaponRange'] = [P(('rr', 8, 52, 46, 66, 3), GRAPH), P(('poly', [(14, 62), (28, 62), (24, 84), (14, 84)]), STEEL),
                        P(('rr', 40, 54, 56, 64, 3), STEEL)] + \
                       [flat(('circ', 66 + i * 11, 59, 2.6), TEAL) for i in range(3)] + [arrow_head(92, 59, 0, 1.0, TEAL),
                       P(('arc', 8, 59, 60, -35, 35, 4), BLUE, False), P(('arc', 8, 59, 76, -28, 28, 4), BLUE, False)]
    I['OrbitSpeed'] = [P(('circ', 50, 50, 14), GRAPH), P(('arc', 50, 50, 36, 0, 360, 4), STEEL, False), P(('arc', 50, 50, 36, 200, 320, 9), TEAL, False),
                       P(('circ', 76, 33, 10), TEAL), P(('line', [(4, 28), (20, 28)], 3), TEAL, False), P(('line', [(2, 40), (14, 40)], 3), TEAL, False)]
    I['OrbitCrit'] = [P(('circ', 50, 50, 13), GRAPH), P(('arc', 50, 50, 36, 0, 360, 4), STEEL, False), P(('circ', 76, 33, 10), TEAL),
                      P(('star', 80, 22, 16, 6, 4, -45), RED)]
    I['PickupSpeed'] = [P(('line', [(28, 20), (28, 52)], 15), RED), P(('line', [(72, 20), (72, 52)], 15), RED), P(('arc', 50, 52, 22, 0, 180, 15), RED),
                        flat(('rr', 20.5, 14, 35.5, 30, 2), WHITE, True), flat(('rr', 64.5, 14, 79.5, 30, 2), WHITE, True),
                        P(('line', [(2, 86), (30, 86)], 4), TEAL, False), P(('line', [(10, 94), (34, 94)], 4), TEAL, False), P(('line', [(70, 86), (98, 86)], 4), TEAL, False)]
    I['Luck'] = [P(('circ', 36, 36, 17), GREEN), P(('circ', 64, 36, 17), GREEN), P(('circ', 36, 64, 17), GREEN), P(('circ', 64, 64, 17), GREEN),
                 P(('circ', 50, 50, 10), (150, 235, 140), False), P(('line', [(58, 66), (74, 94)], 6), (70, 160, 80))]
    I['ExperienceGain'] = [P(('poly', [(50, 6), (80, 36), (50, 94), (20, 36)]), TEAL), flat(('poly', [(50, 6), (80, 36), (50, 36)]), (170, 245, 240)),
                           flat(('poly', [(50, 6), (20, 36), (50, 36)]), (110, 225, 218)), P(('poly', [(72, 70), (88, 86), (78, 86), (78, 96), (66, 96), (66, 86), (56, 86)]), GREEN)]
    I['CursedGlass'] = [P(('poly', [(34, 30), (66, 30), (68, 90), (32, 90)]), GLASS), P(('poly', [(34, 30), (66, 30), (60, 14), (50, 6), (40, 14)]), (150, 205, 240)),
                        flat(('line', [(44, 36), (56, 52), (42, 62), (58, 80)], 3.4), CRIMSON), flat(('line', [(56, 52), (66, 56)], 2.6), CRIMSON),
                        P(('poly', [(76, 20), (88, 14), (82, 28)]), GLASS), P(('poly', [(16, 60), (26, 52), (24, 68)]), GLASS)]
    I['CursedHaste'] = [P(('poly', [(50, 6), (22, 52), (78, 52)]), CRIMSON), P(('circ', 50, 62, 29), CRIMSON),
                        P(('poly', [(58, 28), (34, 62), (48, 62), (40, 94), (68, 54), (53, 54)]), GOLD)]
    I['CursedGreed'] = [P(('star', 50, 50, 46, 38, 12, 0), PURPLE), P(('circ', 50, 50, 34), GOLD), P(('circ', 50, 50, 25), BRASS, False),
                        P(('poly', [(50, 28), (66, 50), (50, 72), (34, 50)]), CRIMSON, False)]
    I['MineCount'] = [P(('rr', 18, 68, 82, 84, 4), GRAPH), P(('circ', 50, 62, 30), STEEL), P(('rr', 18, 62, 82, 74, 2), STEEL),
                      P(('circ', 50, 40, 9), RED), flat(('circ', 47, 37, 3), (255, 190, 190)), P(('line', [(16, 50), (8, 44)], 5), GRAPH), P(('line', [(84, 50), (92, 44)], 5), GRAPH)] + plus(82, 22)
    I['ChestXp'] = [P(('rr', 12, 38, 88, 86, 5), (150, 90, 45)), P(('rr', 12, 26, 88, 50, 10), (180, 112, 55)), P(('rr', 10, 46, 90, 54, 2), GRAPH),
                    P(('rr', 42, 44, 58, 66, 3), GOLD), flat(('circ', 50, 54, 3), DARK), P(('star', 50, 22, 18, 8, 5, -90), TEAL)]
    I['SteelWill'] = heart(50, 52, 0.95, STEEL) + [flat(('line', [(50, 24), (50, 78)], 4), WHITE), flat(('line', [(30, 46), (70, 46)], 4), WHITE)]
    I['Evolution'] = [P(('circ', 50, 50, 42), GRAPH), P(('poly', [(50, 16), (80, 46), (68, 46), (50, 30), (32, 46), (20, 46)]), GOLD),
                      P(('poly', [(50, 44), (80, 74), (68, 74), (50, 58), (32, 74), (20, 74)]), GOLD)]
    return I


def main():
    os.makedirs(OUTDIR, exist_ok=True)
    icons = make()
    names = sorted(icons)
    for n in names:
        render(icons[n]).save(os.path.join(OUTDIR, n + '.png'))
    cols = 9; rows = math.ceil(len(names) / cols)
    sheet = Image.new('RGBA', (cols * 160, rows * 160), (36, 44, 54, 255))
    for i, n in enumerate(names):
        ic = Image.open(os.path.join(OUTDIR, n + '.png')).resize((144, 144), Image.LANCZOS)
        sheet.alpha_composite(ic, ((i % cols) * 160 + 8, (i // cols) * 160 + 8))
    os.makedirs('Logs', exist_ok=True)
    sheet.save('Logs/upgrade-icons-sheet.png')
    print(len(names), 'icons')

if __name__ == '__main__':
    main()
