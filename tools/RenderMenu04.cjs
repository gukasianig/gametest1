// Usage: node tools/RenderMenu04.cjs <bundled node_modules path>. No network or API credentials required.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const modules = process.argv[2];
if (!modules) throw new Error('Provide the installed node_modules directory containing sharp and @napi-rs/canvas');
const sharp = require(path.join(modules, 'sharp'));
const { createCanvas, loadImage, GlobalFonts } = require(path.join(modules, '@napi-rs/canvas'));
const root = path.resolve(__dirname, '..');
const directory = path.join(root, 'docs/art/Menu/Selected04');
const renderer = require(path.join(directory, 'animation.js'));
const destinations = {
    background: [0, 0, 1, 1], hero: [.32, .61, .155, .39], gate: [.46, .38, .32, .49],
    workshop: [0, .29, .36, .65], conditions: [.74, .11, .26, .83], drone: [.23, .57, .08, .09]
};
async function main() {
    const data = { width: 1920, height: 1080, duration: 6, fps: 15, layers: [] };
    const images = {};
    for (const [id, destination] of Object.entries(destinations)) {
        const file = 'Layers/' + id + '.png';
        const full = path.join(directory, file);
        const { data: pixels, info } = await sharp(full).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
        let left = info.width, top = info.height, right = 0, bottom = 0, transparent = 0;
        for (let y = 0; y < info.height; y++) for (let x = 0; x < info.width; x++) {
            const alpha = pixels[(y * info.width + x) * 4 + 3];
            if (alpha < 5) transparent++;
            if (alpha > 10) { left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y); }
        }
        assert(right > left && bottom > top, 'Empty layer ' + id);
        const alphaFraction = transparent / (info.width * info.height);
        if (id !== 'background') assert(alphaFraction > .2, 'Missing real transparency on ' + id);
        const margin = id === 'background' ? 0 : 4;
        left = Math.max(0, left - margin); top = Math.max(0, top - margin);
        right = Math.min(info.width - 1, right + margin); bottom = Math.min(info.height - 1, bottom + margin);
        const crop = id === 'background' ? [0, 0, info.width, info.height] : [left, top, right - left + 1, bottom - top + 1];
        data.layers.push({ id, file, width: info.width, height: info.height, transparentFraction: Number(alphaFraction.toFixed(4)), crop, destination });
        images[id] = await loadImage(full);
    }
    fs.writeFileSync(path.join(directory, 'scene-data.json'), JSON.stringify(data, null, 2) + '\n');
    fs.writeFileSync(path.join(directory, 'scene-data.js'), 'globalThis.ArenaSceneData = ' + JSON.stringify(data) + ';\n');
    const font = 'C:/Windows/Fonts/arial.ttf';
    if (fs.existsSync(font)) GlobalFonts.registerFromPath(font, 'Arial');
    const still = createCanvas(1920, 1080);
    const scene = renderer.create(still, images, data);
    scene.render(0);
    fs.writeFileSync(path.join(directory, 'preview-still.png'), still.toBuffer('image/png'));
    assert.equal(scene.pick(.17, .7), 'workshop');
    assert.equal(scene.pick(.62, .7), 'gate');
    assert.equal(scene.pick(.9, .6), 'conditions');
    assert.equal(scene.pick(.4, .75), null, 'Hero is not a menu action');
    const width = 960, height = 540, frames = data.duration * data.fps;
    const canvas = createCanvas(width, height);
    const animation = renderer.create(canvas, images, data);
    const pixels = Buffer.alloc(width * height * 4 * frames);
    let start, changedPixels = 0;
    for (let frame = 0; frame < frames; frame++) {
        animation.render(frame / data.fps);
        const rgba = Buffer.from(canvas.getContext('2d').getImageData(0, 0, width, height).data);
        rgba.copy(pixels, frame * width * height * 4);
        if (frame === 0) start = Buffer.from(rgba);
        if (frame === 15) for (let p = 0; p < rgba.length; p += 4)
            if (Math.abs(rgba[p] - start[p]) + Math.abs(rgba[p + 1] - start[p + 1]) + Math.abs(rgba[p + 2] - start[p + 2]) > 12) changedPixels++;
    }
    assert(changedPixels > 500, 'Preview does not actually animate');
    const output = path.join(directory, 'animated-preview.gif');
    await sharp(pixels, { raw: { width, height: height * frames, channels: 4, pageHeight: height } })
        .gif({ loop: 0, delay: Math.round(1000 / data.fps), colours: 192, dither: .4, effort: 3 }).toFile(output);
    const metadata = await sharp(output, { animated: true }).metadata();
    assert(metadata.pages > 1 && metadata.pageHeight === height, 'Expected a multi-frame animation');
    const report = { layers: data.layers.length, frames: metadata.pages, width, height, changedPixelsAfterOneSecond: changedPixels,
        bytes: fs.statSync(output).size, actualBrowserInteractionChecked: false, unityIntegration: false };
    fs.writeFileSync(path.join(directory, 'VERIFICATION.json'), JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify(report));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
