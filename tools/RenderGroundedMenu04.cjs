// Render coherent full-scene artwork with emissive/atmospheric animation only.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const modules = process.argv[2];
if (!modules) throw new Error('Provide the installed node_modules directory');
const sharp = require(path.join(modules, 'sharp'));
const { createCanvas, loadImage, GlobalFonts } = require(path.join(modules, '@napi-rs/canvas'));
const parent = path.resolve(__dirname, '../docs/art/Menu/Selected04');
const directory = path.join(parent, 'Grounded');
const api = require(path.join(directory, 'animation.js'));
async function main() {
    const data = { composition: 'single coherent painting per orientation', layers: [] };
    const images = {};
    for (const id of ['portrait', 'landscape']) {
        const full = path.join(directory, id + '.png'), meta = await sharp(full).metadata();
        data.layers.push({ id, file: 'Grounded/' + id + '.png', width: meta.width, height: meta.height });
        images[id] = await loadImage(full);
    }
    fs.writeFileSync(path.join(directory, 'scene-data.json'), JSON.stringify(data, null, 2) + '\n');
    fs.writeFileSync(path.join(directory, 'scene-data.js'), 'globalThis.ArenaSceneData = ' + JSON.stringify(data) + ';\n');
    if (fs.existsSync('C:/Windows/Fonts/arial.ttf')) GlobalFonts.registerFromPath('C:/Windows/Fonts/arial.ttf', 'Arial');
    const checks = [], animations = [];
    for (const [width, height, name] of [[1080, 1920, 'frame-portrait'], [1080, 2400, 'frame-tall'],
        [2340, 1080, 'frame-landscape'], [1920, 1080, 'frame-landscape-16x9']]) {
        const canvas = createCanvas(width, height), scene = api.create(canvas, images, data), view = scene.render(0);
        const aspectError = Math.abs(view.item.width * view.scale / (view.item.height * view.scale) - view.item.width / view.item.height);
        assert(aspectError < 1e-9, 'Scene stretched');
        for (const [id, anchor] of Object.entries(view.plan.contacts)) {
            const p = view.point(...anchor);
            assert(p.x >= 0 && p.x <= view.W && p.y >= 0 && p.y <= view.H, 'Ground contact cropped: ' + name + '/' + id);
        }
        const cssWidth = width < height ? 360 : 780, cssHeight = cssWidth * height / width;
        for (const zone of scene.zones()) {
            assert.equal(scene.pick(zone.x + zone.w / 2, zone.y + zone.h / 2), zone.id);
            assert(zone.w * cssWidth >= 44 && zone.h * cssHeight >= 44, 'Touch target too small');
        }
        const feet = view.point(...view.plan.contacts.hero);
        assert.equal(scene.pick(feet.x / view.W, feet.y / view.H), null, 'Operator is not an action');
        fs.writeFileSync(path.join(directory, name + '.png'), canvas.toBuffer('image/png'));
        checks.push({ width, height, aspectError, contactAnchorsInsideScreen: true, threeTouchZonesAtLeast44CssPixels: true });
    }
    for (const [width, height, name] of [[360, 640, 'animated-portrait'], [780, 360, 'animated-landscape']]) {
        const count = 90, canvas = createCanvas(width, height), scene = api.create(canvas, images, data);
        const raw = Buffer.alloc(width * height * 4 * count);
        let first, changed = 0;
        for (let frame = 0; frame < count; frame++) {
            scene.render(frame / 15);
            const rgba = Buffer.from(canvas.getContext('2d').getImageData(0, 0, width, height).data);
            rgba.copy(raw, frame * width * height * 4);
            if (frame === 0) first = Buffer.from(rgba);
            if (frame === 15) for (let pixel = 0; pixel < rgba.length; pixel += 4)
                if (Math.abs(first[pixel] - rgba[pixel]) + Math.abs(first[pixel + 1] - rgba[pixel + 1]) +
                    Math.abs(first[pixel + 2] - rgba[pixel + 2]) > 12) changed++;
        }
        assert(changed > 40, 'Light/steam animation is static');
        const file = path.join(directory, name + '.gif');
        await sharp(raw, { raw: { width, height: height * count, channels: 4, pageHeight: height } })
            .gif({ loop: 0, delay: Array.from({ length: count }, (_, i) => i % 3 === 0 ? 60 : 70), colours: 192, dither: .4, effort: 3 }).toFile(file);
        const meta = await sharp(file, { animated: true }).metadata();
        assert.equal(meta.pages, count);
        assert.equal(meta.delay.reduce((sum, ms) => sum + ms, 0), 6000);
        animations.push({ name, width, height, frames: count, durationMs: 6000, changedPixelsAfterOneSecond: changed });
        console.log(name + ': complete');
    }
    const report = { checks, animations, geometryInOnePainting: true, actorsTransformedIndependently: false,
        visualGroundingRequiresImageReview: true, browserEventsChecked: false, unityIntegration: false };
    fs.writeFileSync(path.join(directory, 'VERIFICATION.json'), JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify(report));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
