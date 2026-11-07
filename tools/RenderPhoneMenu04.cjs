// Phone preview verification and export; no Unity or project orientation settings are changed.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const modules = process.argv[2];
if (!modules) throw new Error('Provide the installed node_modules directory');
const sharp = require(path.join(modules, 'sharp'));
const { createCanvas, loadImage, GlobalFonts } = require(path.join(modules, '@napi-rs/canvas'));
const directory = path.resolve(__dirname, '../docs/art/Menu/Selected04');
const api = require(path.join(directory, 'phone-animation.js'));

async function main() {
    const data = JSON.parse(fs.readFileSync(path.join(directory, 'scene-data.json'), 'utf8'));
    data.width = 1080; data.height = 1920; data.profiles = api.profiles;
    const portrait = await sharp(path.join(directory, 'Layers/background_portrait.png')).metadata();
    data.layers.push({ id: 'background_portrait', file: 'Layers/background_portrait.png', width: portrait.width,
        height: portrait.height, crop: [0, 0, portrait.width, portrait.height] });
    fs.writeFileSync(path.join(directory, 'phone-scene-data.json'), JSON.stringify(data, null, 2) + '\n');
    fs.writeFileSync(path.join(directory, 'phone-scene-data.js'), 'globalThis.ArenaSceneData = ' + JSON.stringify(data) + ';\n');
    if (fs.existsSync('C:/Windows/Fonts/arial.ttf')) GlobalFonts.registerFromPath('C:/Windows/Fonts/arial.ttf', 'Arial');
    const images = {};
    for (const layer of data.layers) images[layer.id] = await loadImage(path.join(directory, layer.file));
    const checks = [];
    for (const [width, height, name] of [[1080, 1920, 'phone-portrait'], [1080, 2400, 'phone-tall'],
        [2340, 1080, 'phone-landscape'], [1920, 1080, 'phone-landscape-16x9']]) {
        const canvas = createCanvas(width, height);
        const scene = api.create(canvas, images, data);
        const geometry = scene.render(0);
        let maxAspectError = 0;
        for (const [id, rect] of Object.entries(geometry.rects)) {
            const source = data.layers.find(layer => layer.id === id).crop;
            const error = Math.abs(rect.w / rect.h - source[2] / source[3]);
            maxAspectError = Math.max(maxAspectError, error);
            assert(error < 1e-9, 'Non-uniform stretching: ' + name + '/' + id);
            assert(rect.x >= -1e-6 && rect.y >= -1e-6 && rect.x + rect.w <= geometry.width + 1e-6 &&
                rect.y + rect.h <= geometry.height + 1e-6, 'Object outside screen: ' + name + '/' + id);
        }
        const targetWidth = width < height ? 360 : 780;
        for (const zone of scene.zones()) {
            assert.equal(scene.pick(zone.x + zone.w / 2, zone.y + zone.h / 2), zone.id, 'Wrong touch target ' + name + '/' + zone.id);
            const cssHeight = targetWidth * height / width;
            assert(zone.w * targetWidth >= 44 && zone.h * cssHeight >= 44, 'Touch target too small');
        }
        const hero = geometry.rects.hero;
        assert.equal(scene.pick((hero.x + hero.w / 2) / geometry.width, (hero.y + hero.h * .7) / geometry.height), null,
            'Hero should not intercept an action');
        fs.writeFileSync(path.join(directory, name + '.png'), canvas.toBuffer('image/png'));
        checks.push({ name, width, height, maxAspectError, allObjectsInside: true, touchTargetsAtLeast44CssPixels: true });
    }
    const animations = [];
    for (const [width, height, name] of [[360, 640, 'animated-phone-portrait'], [780, 360, 'animated-phone-landscape']]) {
        const frames = 90, canvas = createCanvas(width, height), scene = api.create(canvas, images, data);
        const raw = Buffer.alloc(width * height * 4 * frames);
        let first, changedPixels = 0;
        for (let frame = 0; frame < frames; frame++) {
            scene.render(frame / 15);
            const rgba = Buffer.from(canvas.getContext('2d').getImageData(0, 0, width, height).data);
            rgba.copy(raw, frame * width * height * 4);
            if (frame === 0) first = Buffer.from(rgba);
            if (frame === 15) for (let p = 0; p < rgba.length; p += 4)
                if (Math.abs(rgba[p] - first[p]) + Math.abs(rgba[p + 1] - first[p + 1]) + Math.abs(rgba[p + 2] - first[p + 2]) > 12) changedPixels++;
        }
        assert(changedPixels > 100, 'Animation does not move: ' + name);
        const output = path.join(directory, name + '.gif');
        const delay = Array.from({ length: frames }, (_, index) => index % 3 === 0 ? 60 : 70);
        await sharp(raw, { raw: { width, height: height * frames, channels: 4, pageHeight: height } })
            .gif({ loop: 0, delay, colours: 192, dither: .4, effort: 3 }).toFile(output);
        const meta = await sharp(output, { animated: true }).metadata();
        assert.equal(meta.pages, frames);
        assert.equal(meta.delay.reduce((sum, value) => sum + value, 0), 6000);
        animations.push({ name, width, height, frames: meta.pages, durationMs: 6000, changedPixelsAfterOneSecond: changedPixels });
        console.log(name + ': 90 frames, 6 seconds');
    }
    const report = { checkedProject: ['ProjectSettings/ProjectSettings.asset: all four autorotation flags enabled',
        'Assets/scripts/GameUi.cs: referenceResolution=1280x720', 'docs/CODEX_MENU_BRIEF.md: original 1920x1080 brief'],
        orientationPolicyChanged: false, checks, animations, browserTouchEventsChecked: false, realDeviceChecked: false, unityIntegration: false };
    fs.writeFileSync(path.join(directory, 'PHONE_VERIFICATION.json'), JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify(report));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
