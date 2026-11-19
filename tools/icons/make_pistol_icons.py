"""Значки веток пистолета для мастерской (Револьвер / Двойные / Липкий) → Assets/Resources/UI/Tree/el_*.png.
Запуск:  python tools/icons/make_pistol_icons.py   (нужен Pillow; временные значки, финальный арт можно положить поверх)
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import make_upgrade_icons as m
from make_upgrade_icons import P, flat, rot, STEEL, GRAPH, TEAL, BRASS, ORANGE, GOLD, WHITE, CRIMSON, RED, DARK

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Resources', 'UI', 'Tree')


def revolver():
    parts = [P(('rr', 58, 40, 94, 56, 4), GRAPH), P(('rr', 58, 38, 90, 46, 3), STEEL, False),
             P(('poly', [(16, 64), (34, 70), (30, 94), (12, 90)]), GRAPH),
             P(('circ', 38, 50, 33), STEEL), P(('circ', 38, 50, 24), GRAPH, False)]
    for i in range(6):
        a = math.radians(-90 + i * 60)
        parts.append(flat(('circ', 38 + math.cos(a) * 15, 50 + math.sin(a) * 15, 6.2), BRASS if i < 5 else DARK))
    parts.append(P(('circ', 38, 50, 5), TEAL, False))
    return parts


def pistol(dx, deg, mirror=False):
    def pt(pts):
        pts = [((100 - x) if mirror else x, y) for x, y in pts]
        pts = [(50 + (x - 50) * 0.62 + dx, 56 + (y - 56) * 0.62) for x, y in pts]
        return rot(pts, deg)
    return [P(('poly', pt([(26, 30), (86, 30), (86, 46), (26, 46)])), STEEL),
            P(('poly', pt([(26, 46), (46, 46), (42, 78), (26, 78)])), GRAPH),
            flat(('poly', pt([(30, 33), (82, 33), (82, 37), (30, 37)])), TEAL)]


def dual():
    return pistol(-24, 22) + pistol(24, -22, True) + [P(('star', 50, 16, 13, 5, 6, 0), GOLD, False)]


def sticky():
    return [P(('circ', 44, 58, 31), CRIMSON), P(('circ', 44, 58, 21), RED, False), P(('rr', 40, 18, 48, 30, 2), GRAPH),
            flat(('circ', 44, 14, 6), GOLD), P(('circ', 44, 58, 10), GRAPH, False), flat(('circ', 44, 58, 5), GOLD),
            P(('star', 80, 26, 17, 7, 8, 0), ORANGE), P(('star', 80, 26, 9, 4, 6, 10), GOLD, False),
            P(('circ', 78, 80, 7), RED), P(('circ', 64, 90, 5), RED)]


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for name, parts in (('el_revolver', revolver()), ('el_dual', dual()), ('el_sticky', sticky())):
        m.render(parts).save(os.path.join(OUT, name + '.png'))
        print('saved', name)
