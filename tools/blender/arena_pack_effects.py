"""Particle sprites and small latitude/longitude HDR sky environments."""

import math

import bpy
import numpy as np

from arena_pack_ground import noise, png, rgb, smooth


FX = ('dust_puff', 'leaf_particle', 'pollen', 'firefly_glow', 'water_splash', 'water_ripple_ring',
      'mist_soft', 'smoke_soft', 'light_shaft', 'butterfly_sheet', 'bird_silhouette_sheet', 'cloud_shadow')


def ell(x, y, w, h):
    return 1-smooth((x/w)**2+(y/h)**2, 0.78, 1)


def sprite(kind, idx):
    x, y = np.meshgrid(np.linspace(-1, 1, 512, dtype=np.float32), np.linspace(1, -1, 512, dtype=np.float32))
    r = np.hypot(x, y)
    n = noise(512, 10, 83+idx)
    color = 'e8d9bd'
    if kind == 'dust_puff':
        a = (1-smooth(r+(n-.5)*.33, .11, .83))*0.9
    elif kind == 'leaf_particle':
        a = ell(x, y+0.12*np.sin(3*x), .78, .35*(0.93+x*.21))
        color = '84a657'
    elif kind == 'pollen':
        a = (1-smooth(r, .08, .58))*(.6+.4*n)
        color = 'f8df88'
    elif kind == 'firefly_glow':
        a = np.exp(-10*r*r)*(1-smooth(r, .55, .92))
        color = 'd9ed90'
    elif kind == 'water_splash':
        a = np.zeros_like(x)
        for i in range(9):
            q = (i-4)/5
            a = np.maximum(a, ell(x-q*.62, y-0.3+abs(q)*.43, .065+abs(q)*.04, .12+0.12*(1-abs(q))))
        a = np.maximum(a, ell(x, y+.38, .78, .12)*.58)
        color = 'c8efee'
    elif kind == 'water_ripple_ring':
        a = (1-smooth(np.abs(r-.66), .012, .047))*.87+(1-smooth(np.abs(r-.49), .012, .034))*.37
        color = 'c8f7ee'
    elif kind in {'mist_soft', 'smoke_soft', 'cloud_shadow'}:
        a = (1-smooth(np.hypot(x*.91, y)+(n-.5)*.39, .08, .88))*({'mist_soft': .48, 'smoke_soft': .79, 'cloud_shadow': .47}[kind])
        color = {'mist_soft': 'dae7dc', 'smoke_soft': '77786f', 'cloud_shadow': '293941'}[kind]
    elif kind == 'light_shaft':
        width = .14+.31*(1-y)*.5
        a = (1-smooth(np.abs(x)/width, .13, .94))*(1-smooth(np.abs(y), .40, .94))*.76
        color = 'fff2bf'
    else:
        a = np.zeros_like(x)
        for i in range(4):
            col, row = i % 2, i//2
            region = (slice(row*256, (row+1)*256), slice(col*256, (col+1)*256))
            qx = (x[region]-(col-.5))*2
            qy = (y[region]-(.5-row))*2
            spread = (.42, .70, .93, .70)[i]
            if kind == 'butterfly_sheet':
                wings = np.maximum(ell(np.abs(qx)-.32*spread, qy-.12-np.abs(qx)*.36, .34*spread, .33),
                                   ell(np.abs(qx)-.22*spread, qy+.16+np.abs(qx)*.22, .24*spread, .23))
                a[region] = np.maximum(wings, ell(qx, qy, .049, .33))
                color = 'd7a74a'
            else:
                wing_y = (.28, .05, -.20, .05)[i]
                wing = ell(np.abs(qx)-.30*spread, qy-wing_y*np.abs(qx)*1.65-.08, .38*spread, .11+.10*(1-np.abs(qx)))
                a[region] = np.maximum(wing, ell(qx, qy+.02, .11, .24))
                color = '35463e'
    rgba = np.zeros((512, 512, 4), np.float32)
    rgba[..., :3] = rgb(color)
    if kind == 'leaf_particle':
        vein = (1-smooth(np.abs(y+.12*np.sin(3*x)), .012, .034))*.28
        rgba[..., :3] += vein[..., None]*np.array((.29, .24, .05), np.float32)
    rgba[..., 3] = np.clip(a, 0, 1)*(1-smooth(np.maximum(np.abs(x),np.abs(y)),.91,1.0))
    assert rgba[..., 3].max() > 0.35 and rgba[0, :, 3].max() == 0 and rgba[:, 0, 3].max() == 0, kind
    return rgba


SKIES = [('morning', '669cce', 'd7d2ac', .29, 1.45, 4.8),
         ('noon', '5295d0', 'bfdbde', .90, 2.04, 5.9),
         ('sunset', '64699f', 'f39c69', .13, 4.25, 4.0)]


def sky(folder, index):
    name, zenith, horizon, elevation, azimuth, energy = SKIES[index]
    w, h = 2048, 1024
    phi = (np.arange(w, dtype=np.float32)+0.5)/w*math.tau
    theta = (np.arange(h, dtype=np.float32)+0.5)/h*math.pi-math.pi/2
    height = np.sin(theta)[:, None]
    above = np.clip(height, 0, 1)
    # Blender receives linear radiance. The JPEG is the matching tone-mapped preview.
    high, low = rgb(zenith)**2.2, rgb(horizon)**2.2
    color = low[None, None, :]+(high-low)[None, None, :]*above[..., None]**0.47
    color = np.broadcast_to(color, (h, w, 3)).copy()
    cloud = noise(w, 15, 431+index*48, 25)[::2, :]*.67+noise(w, 43, 87+index, 59)[::2, :]*.33
    mask = smooth(cloud, .56, .78)*smooth(height, .01, .10)*(1-smooth(height, .56, .97))*.66
    color += mask[..., None]*(np.array((.57, .53, .48), np.float32)-color)
    below = (1-smooth(height, -.34, 0))[..., None]
    color = color*(1-below)+np.array((.10, .13, .086), np.float32)*below
    dot = np.cos(theta)[:, None]*math.cos(elevation)*np.cos(phi-azimuth)[None, :]+height*math.sin(elevation)
    disc = smooth(dot, .99952, .99990)*energy + np.exp(np.clip((dot-1)*79, -60, 0))*0.64
    color += disc[..., None]*np.array((1, .83, .53) if index != 1 else (1, .92, .77), np.float32)
    pixels = np.ones((h, w, 4), np.float32)
    pixels[..., :3] = color
    image = bpy.data.images.new(f'Arena sky {name}', width=w, height=h, alpha=False, float_buffer=True)
    image.colorspace_settings.name = 'Linear Rec.709'
    image.pixels.foreach_set(pixels.ravel())
    target = folder/f'sky_{name}.hdr'
    image.filepath_raw = str(target)
    image.file_format = 'HDR'
    image.save()
    bpy.data.images.remove(image)
    reopened = bpy.data.images.load(str(target))
    radiance = np.empty(w*h*4, np.float32)
    reopened.pixels.foreach_get(radiance)
    assert tuple(reopened.size) == (w, h) and radiance.max() > 1.1, name
    bpy.data.images.remove(reopened)
    image = bpy.data.images.new(f'Sky preview {name}', width=w, height=h, alpha=False)
    pixels[..., :3] = np.clip(color,0,1)**(1/2.2)
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = str(folder/f'sky_{name}.jpg')
    image.file_format = 'JPEG'
    image.save()
    bpy.data.images.remove(image)
    return name


def effects(root, previews, minimum):
    folder = root/'effects'
    folder.mkdir(parents=True, exist_ok=True)
    sources = FX[:2] if minimum else FX
    thumbs = []
    for i, kind in enumerate(sources):
        image = sprite(kind, i)
        png(folder/f'fx_{kind}.png', image)
        a = image[..., 3:4]
        thumbs.append((image[..., :3]*a+0.19*(1-a))[::4, ::4].copy())
    png(previews/'effects_minimum.png', np.concatenate(thumbs[:2], axis=1))
    if not minimum:
        png(previews/'effects.png', np.concatenate([np.concatenate(thumbs[i:i+4], axis=1) for i in range(0,len(thumbs),4)], axis=0))
    skies = root/'sky'
    skies.mkdir(exist_ok=True)
    completed = [sky(skies, index) for index in (range(1) if minimum else range(3))]
    return {'sprites': list(sources), 'skies': completed}
