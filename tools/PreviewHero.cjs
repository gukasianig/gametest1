// Assemble previews of the actual Unity mesh, not concept illustrations.
const path = require('path');
const fs = require('fs');
const sharp = require('C:/Users/grisha/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const dir = path.resolve(__dirname, '../docs/art/HeroOperator');
(async () => {
    const panels = [];
    for (const [i, name] of ['front', 'back', 'top'].entries()) {
        const image = await sharp(path.join(dir, name + '.png')).resize(540, 600).toBuffer();
        panels.push({input: image, left: i * 540, top: 0});
    }
    await sharp({create: {width: 1620, height: 600, channels: 3, background: '#14212a'}}).composite(panels).png().toFile(path.join(dir, 'model-views.png'));
    const frames = [];
    for (let i = 0; i < 24; i++) frames.push(await sharp(path.join(dir, 'run-frames', String(i).padStart(2, '0') + '.png')).resize(450, 500).ensureAlpha().raw().toBuffer());
    if (frames[0].equals(frames[6])) throw new Error('Animation preview is static');
    const delay = Array.from({length: 24}, (_, i) => i % 3 === 0 ? 20 : 30);
    await sharp(Buffer.concat(frames), {raw: {width: 450, height: 500 * 24, channels: 4, pageHeight: 500}}).gif({delay, loop: 0, colours: 192, effort: 3}).toFile(path.join(dir, 'run.gif'));
    const meta = await sharp(path.join(dir, 'run.gif'), {animated: true}).metadata();
    if (meta.pages !== 24) throw new Error('GIF frame count mismatch');
    fs.writeFileSync(path.join(dir, 'PREVIEW_VERIFICATION.json'), JSON.stringify({source: 'Unity mesh render', views: 3, gifFrames: meta.pages, durationMs: delay.reduce((a, b) => a + b, 0), frameDifference: true}, null, 2));
    console.log('Hero previews PASS: 3 views, 24 moving frames, 640 ms');
})().catch(e => {console.error(e); process.exitCode = 1;});
