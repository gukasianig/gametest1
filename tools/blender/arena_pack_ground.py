"""Periodic, softly painted terrain maps and transparent ground decals."""

import math
import struct
import zlib

import numpy as np


GROUND = (
    ('grass_lush', '4c7f34', 8, 'grass'), ('grass_meadow', '6e9440', 8, 'grass'),
    ('grass_dry', 'a89a58', 8, 'grass'), ('grass_dark', '2f5a2c', 8, 'grass'),
    ('grass_clover', '5a8c3a', 8, 'clover'), ('dirt_path', '7a5c3e', 10, 'dirt'),
    ('dirt_rich', '4e3a28', 10, 'dirt'), ('dirt_clay', 'a5683f', 10, 'clay'),
    ('mud_wet', '3b2e22', 8, 'mud'), ('sand_fine', 'd1be8c', 10, 'sand'),
    ('gravel', '8c8780', 8, 'gravel'), ('rock_gray', '77757a', 12, 'rock'),
    ('rock_mossy', '5e6e54', 12, 'moss'), ('rock_dark', '43424a', 12, 'rock'),
    ('leaf_litter', '7b5a30', 8, 'leaves'), ('forest_floor', '4a4028', 8, 'forest'),
)
MIN_GROUND = (0, 3, 5, 6, 9, 11, 12, 14)
HEIGHT_KINDS = {'dirt', 'clay', 'mud', 'gravel', 'rock', 'moss'}
DECALS = ('leaves', 'pebbles', 'cracks', 'flowers', 'footprints', 'moss')


def rgb(value):
    return np.frombuffer(bytes.fromhex(value), dtype=np.uint8).astype(np.float32) / 255


def png(path, image):
    data = image if image.dtype == np.uint8 else np.clip(np.rint(image*255), 0, 255).astype(np.uint8)
    if data.ndim == 2:
        data = data[..., None]
    height, width, channels = data.shape
    # Sub-filter compresses smooth gradients without quantizing the color or alpha.
    scan = data.copy()
    scan[:, 1:] = data[:, 1:]-data[:, :-1]
    rows = b''.join(b'\1'+row.tobytes() for row in scan)
    def chunk(tag, payload):
        return struct.pack('>I', len(payload))+tag+payload+struct.pack('>I', zlib.crc32(tag+payload))
    header = struct.pack('>2I5B', width, height, 8, {1: 0, 3: 2, 4: 6}[channels], 0, 0, 0)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR', header)+chunk(b'IDAT', zlib.compress(rows, 4))+chunk(b'IEND', b''))


def noise(size, cells, seed, y_cells=None):
    ny = y_cells or cells
    grid = np.random.default_rng(seed).random((ny, cells), dtype=np.float32)
    def indexes(length):
        values = np.arange(size, dtype=np.float32)*(length/size)
        i = values.astype(np.int32)
        t = values-i.astype(np.float32)
        return i, (i+1) % length, (t*t*(3-2*t)).astype(np.float32)
    ix, jx, sx = indexes(cells)
    iy, jy, sy = indexes(ny)
    rows = grid[:, ix]*(1-sx) + grid[:, jx]*sx
    return rows[iy]*(1-sy[:, None])+rows[jy]*sy[:, None]


def smooth(value, low, high):
    t = np.clip((value-low)/(high-low), 0, 1)
    return t*t*(3-2*t)


def stone_cells(size, cells, seed):
    # Evaluate periodic Voronoi on a 512 grid and upsample, to keep 2K generation inexpensive.
    n = min(512, size)
    coords = np.arange(n, dtype=np.float32)*(cells/n)
    u, v = np.meshgrid(coords, coords)
    u += 0.33*(noise(n, cells*2, seed+1)-0.5)
    v += 0.33*(noise(n, cells*2, seed+2)-0.5)
    ix, iy = np.floor(u).astype(np.int32), np.floor(v).astype(np.int32)
    offsets = np.random.default_rng(seed).random((cells, cells, 3), dtype=np.float32)
    near = np.full(u.shape, 100, np.float32)
    next_near = near.copy()
    tone = np.zeros(u.shape, np.float32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            tx, ty = ix+dx, iy+dy
            site = offsets[ty % cells, tx % cells]
            d = (u-tx-site[..., 0])**2+(v-ty-site[..., 1])**2
            wins = d < near
            next_near = np.where(wins, near, np.minimum(d, next_near)).astype(np.float32)
            near = np.minimum(d, near).astype(np.float32)
            tone = np.where(wins, site[..., 2], tone)
    def enlarge(field):
        c = np.arange(size, dtype=np.float32)*(n/size)
        i = c.astype(np.int32)
        j = (i+1) % n
        t = c-i.astype(np.float32)
        rows = field[:, i]*(1-t)+field[:, j]*t
        return rows[i]*(1-t[:, None])+rows[j]*t[:, None]
    return enlarge(np.sqrt(near)), enlarge(np.sqrt(next_near)-np.sqrt(near)), enlarge(tone)


def brush_stamp(size, cx, cy, length, width, angle, pointy=0.0):
    reach = math.ceil(max(length, width))+1
    gx = np.arange(-reach, reach+1, dtype=np.float32)
    x, y = gx[None, :], gx[:, None]
    c, s = math.cos(angle), math.sin(angle)
    along = (c*x+s*y)/length
    across = (-s*x+c*y)/(width*np.maximum(0.42, 1-pointy*along))
    radius = along*along+across*across
    return np.ix_((int(cy)+gx.astype(int)) % size, (int(cx)+gx.astype(int)) % size), (1-smooth(radius, 0.33, 1.0))


def strokes(layer, rng, amount, lengths, widths, tones, pointy=0.4):
    size = layer.shape[0]
    for _ in range(amount):
        x, y = rng.integers(size, size=2)
        region, alpha = brush_stamp(size, x, y, rng.uniform(*lengths), rng.uniform(*widths), rng.uniform(0, math.tau), pointy)
        layer[region] += rng.uniform(*tones)*alpha


def seam_check(image, name):
    # A shifted image puts the wrap seam in its centre. Compare its jump with nearby pixel gradients.
    channels = image if image.ndim == 2 else image[..., :3]
    for axis in (0, 1):
        rotated = np.moveaxis(channels, axis, 0)
        reference = np.mean(np.abs(rotated[1:33]-rotated[:32]))+np.mean(np.abs(rotated[-32:]-rotated[-33:-1]))
        delta = np.mean(np.abs(rotated[-1]-rotated[0]))
        assert delta < 1.20*reference+0.004, (name, axis, float(delta), float(reference))


def surface(folder, index, size=2048):
    name, target, meters, kind = GROUND[index]
    seed = 8308+index*193
    rng = np.random.default_rng(seed)
    soft = noise(size, 17, seed)
    middle = noise(size, 67, seed+1)
    fine = noise(size, 310, seed+2)
    tone = 0.095*(soft-0.5)+0.10*(middle-0.5)+0.058*(fine-0.5)
    pigment = np.zeros_like(tone)
    height = 0.0008*(fine-0.5)+0.0016*(middle-0.5)
    rough = np.full_like(tone, 0.86)+0.085*(middle-0.5)
    if kind in {'grass', 'clover'}:
        strokes(pigment, rng, 11000, (2.4, 6.1), (0.85, 1.85), (-0.10, 0.18), 0.58)
        tone += pigment*0.55
        height += pigment*0.009
        if kind == 'clover':
            flower = np.zeros_like(tone)
            strokes(flower, rng, 1500, (1.1, 2.5), (1.1, 2.5), (0.05, 0.30), 0.05)
            tone += flower*0.38
    if kind in {'dirt', 'gravel', 'clay', 'rock', 'moss'}:
        count = {'dirt': 108, 'gravel': 112, 'clay': 37, 'rock': 25, 'moss': 25}[kind]
        nearest, gap, shade = stone_cells(size, count, seed+7)
        cracks = 1-smooth(gap, 0.012, 0.105 if kind == 'gravel' else 0.066)
        visibility = 0.55 if kind == 'dirt' else 1.0
        tone += visibility*(0.083*(shade-0.5)-0.048*cracks)
        height -= cracks*(0.0017 if kind in {'dirt', 'gravel'} else 0.0034)
        height += (0.5-nearest)*(0.0022 if kind != 'rock' else 0.0034)
        if kind == 'moss':
            pigment = smooth(noise(size, 19, seed+22), 0.51, 0.69)
            tone += 0.026*pigment
    if kind == 'sand':
        v = np.arange(size, dtype=np.float32)[:, None]/size
        wave = np.sin(v*math.tau*27 + noise(size, 11, seed+11)*4.4)
        tone += wave*0.025
        height += wave*0.00062
    if kind == 'mud':
        pools = smooth(soft*0.67+middle*0.33, 0.44, 0.75)
        tone -= pools*0.032
        rough = 0.55-0.19*pools
        height -= pools*0.0027
    if kind in {'leaves', 'forest'}:
        strokes(pigment, rng, 6600, (2.9, 7.9), (1.1, 3.5) if kind == 'leaves' else (0.6, 1.35), (-0.17, 0.24), 0.68)
        tone += 0.64*pigment
        height += pigment*0.011
    base = rgb(target)
    tone = np.clip(tone, -0.112, 0.112)
    color = base[None, None, :]*(1+(tone-tone.mean())[..., None])
    hue = {'moss': (-0.041, 0.018, -0.035), 'leaves': (0.053, 0.019, -0.024),
           'grass': (0.029, 0.004, -0.022), 'clover': (-0.012, 0.011, 0.002),
           'forest': (0.025, 0.027, -0.009)}.get(kind)
    if hue:
        color += (np.clip(pigment, -0.2, 0.9)-np.clip(pigment, -0.2, 0.9).mean())[..., None]*np.asarray(hue, np.float32)
    coefficients = np.array((.2126, .7152, .0722), dtype=np.float32)
    target_luma = float(base @ coefficients)
    luma = color @ coefficients
    margin = .50/(255*target_luma)
    clipped = np.clip(luma, target_luma*(.90+margin), target_luma*(1.10-margin))
    color *= (clipped/np.maximum(luma, 1e-6))[..., None]
    dx = -(np.roll(height, -1, axis=1)-np.roll(height, 1, axis=1))/(2*meters/size)
    dy = (np.roll(height, -1, axis=0)-np.roll(height, 1, axis=0))/(2*meters/size)
    n = np.sqrt(dx*dx+dy*dy+1)
    normal = np.stack((dx/n, dy/n, 1/n), axis=-1)*0.5+0.5
    maps = {'color': color, 'normal': normal, 'rough': rough}
    if kind in HEIGHT_KINDS:
        maps['height'] = np.clip(0.50+height*88, 0, 1)
    for channel, image in maps.items():
        seam_check(image, f'{name}_{channel}')
        png(folder/name/f'{name}_{channel}.png', image)
    mean = color.mean(axis=(0, 1), dtype=np.float64)
    assert np.max(np.abs(mean-base)) < 0.005, (name, mean, base)
    thumb = np.roll(color, (size//2, size//2), axis=(0, 1))[::8, ::8].copy()
    return thumb, {'name': name, 'meters': meters, 'size': size, 'maps': list(maps), 'target_srgb': '#'+target}


def composite(layer, region, alpha, color):
    src = layer[region]
    old = src[..., 3]
    total = alpha+old*(1-alpha)
    src[..., :3] = (np.asarray(color)*alpha[..., None]+src[..., :3]*(old*(1-alpha))[..., None])/np.maximum(1e-6, total[..., None])
    src[..., 3] = total
    layer[region] = src


def decal(folder, kind, variant):
    size = 512
    rng = np.random.default_rng(61912+DECALS.index(kind)*571+variant*83)
    layer = np.zeros((size, size, 4), dtype=np.float32)
    colors = {'leaves': ('936535', 'ad813e', '8e4b2e', '806b3d'), 'pebbles': ('8b8474', '736e63', 'a6a096'),
              'cracks': ('594630', '493b2c'), 'flowers': ('e6d9a5', 'c789a6', 'c9b4c9', 'd5a643'),
              'footprints': ('524332', '6d563f'), 'moss': ('486531', '70864a', '65753e')}[kind]
    def dab(x, y, length, width, angle, color, opacity=1, taper=0.15):
        region, alpha = brush_stamp(size, x, y, length, width, angle, taper)
        composite(layer, region, opacity*alpha, rgb(color))
    if kind == 'footprints':
        for i in range(4):
            x, y = 256+(-1)**i*35+variant*7, 111+95*i
            angle = 1.49+0.19*math.sin(i+variant)
            dab(x, y, 31, 13, angle, colors[0], 0.75)
            dab(x, y-32, 10, 9, angle, colors[0], 0.7)
            for offset in (-10, 1, 12):
                dab(x, y+offset, 2.4, 10.0, 0.08, colors[1], 0.80)
    elif kind == 'cracks':
        for ray in range(5):
            angle = ray*math.tau/5 + rng.uniform(-0.25, 0.25)
            x, y = 256+rng.uniform(-18, 18), 256+rng.uniform(-18, 18)
            for i in range(9):
                angle += rng.uniform(-0.43, 0.43)
                d = rng.uniform(8, 18)
                nx, ny = x+math.cos(angle)*d, y+math.sin(angle)*d
                dab((x+nx)/2, (y+ny)/2, d*0.82, max(1, 5-i*0.47), angle, colors[ray % 2], 0.89)
                x, y = nx, ny
    else:
        total = {'leaves': 57, 'pebbles': 64, 'flowers': 32, 'moss': 210}[kind]
        for _ in range(total):
            angle, distance = rng.uniform(0, math.tau), 160*math.sqrt(rng.random())
            x, y = 256+distance*math.cos(angle), 256+0.76*distance*math.sin(angle)
            color = rng.choice(colors)
            length = rng.uniform(6, 18) if kind != 'moss' else rng.uniform(5, 21)
            direction = rng.uniform(0, math.tau)
            if kind == 'flowers':
                for petal in range(5):
                    a = direction+petal*math.tau/5
                    dab(x+length*0.46*math.cos(a), y+length*0.46*math.sin(a), length*0.59, length*0.37, a, color, 0.9)
                dab(x, y, length*0.25, length*0.25, 0, 'c68f42')
            else:
                dab(x, y, length, length*rng.uniform(0.33, 0.90), direction, color,
                    rng.uniform(0.65, 1.0), 0.60 if kind == 'leaves' else 0.14)
                if kind == 'leaves':
                    dab(x, y, length*0.78, 0.82, direction, 'be9958', 0.61)
    assert layer[..., 3].max() > 0.7 and layer[0, :, 3].max() == 0 and layer[:, 0, 3].max() == 0
    png(folder/f'decal_{kind}_{variant:02d}.png', layer)


def ground(root, previews, minimum):
    folder = root/'ground'
    selected = MIN_GROUND if minimum else range(len(GROUND))
    rows, records = [], []
    for index in selected:
        thumb, record = surface(folder, index)
        rows.append(thumb)
        records.append(record)
        print(f'GROUND PASS {record["name"]}: {", ".join(record["maps"])}', flush=True)
    for name, data in [('macro_variation', 0.40+0.20*noise(1024, 3, 433)),
                       ('wetness_mask', smooth(noise(1024, 7, 445)*0.72+noise(1024, 21, 871)*0.28, 0.29, 0.76))]:
        seam_check(data, name)
        png(folder/f'{name}.png', data)
    if not minimum:
        for kind in DECALS:
            for variant in range(1, 4):
                decal(folder/'decals', kind, variant)
    def sheet(filename, tiles):
        png(previews/filename,np.concatenate([np.concatenate(tiles[i:i+4], axis=1) for i in range(0, len(tiles), 4)], axis=0))
    if not minimum:
        sheet('ground_wrap.png', rows)
    sheet('ground_minimum_wrap.png', rows if minimum else [rows[i] for i in MIN_GROUND])
    return records
