"""Перекраска палитр врагов D3 в приземлённую гамму (2026-11-20).

Запуск из корня проекта:  python tools/icons/enemy_palette_remap.py
Первый запуск копирует оригиналы в Logs/enemy-palettes-backup/<Папка>/; повторные запуски
всегда читают оригинал из резервной копии, поэтому результат не «накапливается».
Откат: python tools/icons/enemy_palette_remap.py --restore
Меняются только пиксели PNG; .meta и GUID не трогаются.

Правила (по оттенку/яркости каждого из 16 цветов палитры):
- фиолетовые/розовые/пурпурные (h 250..350): тёмные -> хитин/графит с бурым оттенком,
  средние/светлые -> засохшая кровь/ржавчина (хищный красно-бурый, остаётся читаемым акцентом);
- бирюзовые/синие (h 150..230): тёмные -> болотный, яркие -> кислотно-зелёный акцент;
- почти чёрные -> графит;
- светлые кремовые (кость) -> пепельная кость, чуть темнее;
- оранжевые/красные/янтарные/жёлто-зелёные — без изменений (глаза, шипы, опасные органы).
"""
import colorsys
import glob
import os
import shutil
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PATTERN = os.path.join(ROOT, "Assets", "Art", "Enemies", "*", "*_Palette.png")
BACKUP = os.path.join(ROOT, "Logs", "enemy-palettes-backup")


def hsv(h, s, v):
    r, g, b = colorsys.hsv_to_rgb((h % 360) / 360.0, max(0.0, min(1.0, s)), max(0.0, min(1.0, v)))
    return int(round(r * 255)), int(round(g * 255)), int(round(b * 255))


def remap(rgb):
    r, g, b = (c / 255.0 for c in rgb)
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    h *= 360.0
    if v < 0.18:
        return hsv(30, 0.12, v * 0.9)                       # графит
    if s < 0.28 and v > 0.75:
        return hsv(38, s * 0.7, v * 0.86)                   # пепельная кость
    if 250 <= h <= 352:
        if v < 0.55:
            return hsv(28 + (v - 0.2) * 20, 0.28, 0.11 + v * 0.38)   # тёмный хитин / бурый
        return hsv(4, 0.62, v * 0.78)                       # засохшая кровь / ржавчина
    if 150 <= h <= 230:
        if v < 0.6:
            return hsv(95, 0.42, v * 0.75)                  # болотный
        return hsv(84, 0.72, min(1.0, v * 1.05))            # кислотно-зелёный акцент
    return tuple(rgb)                                       # тёплые акценты без изменений


def main():
    files = sorted(glob.glob(PATTERN))
    restore = "--restore" in sys.argv
    for path in files:
        folder = os.path.basename(os.path.dirname(path))
        backup = os.path.join(BACKUP, folder, os.path.basename(path))
        if restore:
            if os.path.exists(backup):
                shutil.copyfile(backup, path)
                print("restored", folder)
            continue
        if not os.path.exists(backup):
            os.makedirs(os.path.dirname(backup), exist_ok=True)
            shutil.copyfile(path, backup)
        image = Image.open(backup).convert("RGB")
        cache = {}
        pixels = [cache.setdefault(p, remap(p)) for p in image.getdata()]
        result = Image.new("RGB", image.size)
        result.putdata(pixels)
        result.save(path)
        changed = sum(1 for a, b in cache.items() if a != b)
        print("%-12s %d/%d colours changed" % (folder, changed, len(cache)))
    print(len(files), "palettes", "restored" if restore else "remapped", "| backup:", BACKUP)


if __name__ == "__main__":
    main()
