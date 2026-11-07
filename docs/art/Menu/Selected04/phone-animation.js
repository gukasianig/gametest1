(function (root) {
    'use strict';
    const TAU = Math.PI * 2;
    const labels = { gate: 'Старт', workshop: 'Мастерская', conditions: 'Условия' };
    const profiles = {
        portrait: {
            hero: [.40, .74, .24, .26], gate: [.08, .23, .84, .34],
            workshop: [0, .56, .58, .33], conditions: [.64, .56, .36, .39], drone: [.35, .63, .15, .06]
        },
        landscape: {
            hero: [.32, .60, .145, .40], gate: [.42, .32, .38, .56],
            workshop: [0, .31, .32, .63], conditions: [.79, .10, .21, .84], drone: [.22, .57, .075, .095]
        }
    };
    function fit(crop, box, width, height) {
        const [x, y, bw, bh] = box;
        const scale = Math.min(bw * width / crop[2], bh * height / crop[3]);
        const w = crop[2] * scale, h = crop[3] * scale;
        // Bottom alignment keeps equipment grounded. Both axes always use the SAME scale.
        return { x: x * width + (bw * width - w) / 2, y: (y + bh) * height - h, w, h };
    }
    function create(canvas, images, data) {
        const ctx = canvas.getContext('2d');
        const items = Object.fromEntries(data.layers.map(item => [item.id, item]));
        function layout() {
            const portrait = canvas.height > canvas.width;
            const W = portrait ? 1080 : 1080 * canvas.width / canvas.height;
            const H = portrait ? 1080 * canvas.height / canvas.width : 1080;
            const profile = profiles[portrait ? 'portrait' : 'landscape'];
            const rects = Object.fromEntries(Object.entries(profile).map(([id, box]) => [id, fit(items[id].crop, box, W, H)]));
            return { W, H, portrait, rects };
        }
        function zones() {
            const { W, H, portrait, rects } = layout();
            return Object.keys(labels).map(id => {
                const rect = rects[id];
                return { id, x: rect.x / W, y: rect.y / H,
                    w: rect.w / W, h: Math.min(1 - rect.y / H, (rect.h + (portrait ? 55 : 44)) / H) };
            });
        }
        function pick(x, y) {
            const { W, H, rects } = layout();
            const hero = rects.hero;
            if (x >= hero.x / W && x <= (hero.x + hero.w) / W && y >= hero.y / H) return null;
            const zone = zones().find(zone => x >= zone.x && x <= zone.x + zone.w && y >= zone.y && y <= zone.y + zone.h);
            return zone ? zone.id : null;
        }
        function glow(x, y, rx, ry, color, opacity) {
            ctx.save(); ctx.translate(x, y); ctx.scale(Math.max(rx, 1), Math.max(ry, 1));
            const gradient = ctx.createRadialGradient(0, 0, 0, 0, 0, 1);
            gradient.addColorStop(0, color); gradient.addColorStop(1, 'rgba(0,0,0,0)');
            ctx.fillStyle = gradient; ctx.globalAlpha = opacity;
            ctx.fillRect(-1, -1, 2, 2); ctx.restore();
        }
        function background(id, W, H) {
            const item = items[id];
            const image = images[id];
            const sourceAspect = item.width / item.height, aspect = W / H;
            let sw = item.width, sh = item.height;
            if (sourceAspect > aspect) sw = sh * aspect;
            else sh = sw / aspect;
            // Cover by cropping the backdrop, never by stretching it.
            ctx.drawImage(image, (item.width - sw) / 2, (item.height - sh) / 2, sw, sh, 0, 0, W, H);
        }
        function draw(id, rects, hover, dx = 0, dy = 0, angle = 0, breathe = 1) {
            const item = items[id], rect = rects[id];
            ctx.save();
            ctx.translate(rect.x + rect.w / 2 + dx, rect.y + rect.h + dy);
            ctx.rotate(angle); ctx.scale(1, breathe);
            if (hover) { ctx.shadowColor = id === 'workshop' ? '#4fffe9' : '#ffbd5a'; ctx.shadowBlur = 20; }
            ctx.drawImage(images[id], ...item.crop, -rect.w / 2, -rect.h, rect.w, rect.h);
            ctx.restore();
        }
        function render(time, options = {}) {
            const { W, H, portrait, rects } = layout();
            const t = options.motion === false ? 0 : time, phase = TAU * t / 6;
            const focus = options.hover || options.selected;
            ctx.setTransform(canvas.width / W, 0, 0, canvas.height / H, 0, 0);
            ctx.clearRect(0, 0, W, H);
            background(portrait ? 'background_portrait' : 'background', W, H);
            const gate = rects.gate, workshop = rects.workshop, tower = rects.conditions, drone = rects.drone;
            glow(workshop.x + workshop.w / 2, workshop.y + workshop.h, workshop.w * .55, 28, '#05090d', .35);
            glow(tower.x + tower.w / 2, tower.y + tower.h, tower.w * .55, 30, '#05090d', .4);
            draw('gate', rects, focus === 'gate');
            glow(gate.x + gate.w / 2, gate.y + gate.h * .65, gate.w * .24, gate.h * .35, '#ffc678', .075 + .025 * Math.sin(phase));
            glow(gate.x + gate.w / 2, gate.y + gate.h, gate.w * .36, 28, '#ffc06c', .12 + .035 * Math.sin(phase));
            draw('workshop', rects, focus === 'workshop');
            glow(workshop.x + workshop.w * .54, workshop.y + workshop.h * .62, workshop.w * .24, workshop.h * .15,
                '#43f4df', .06 + .02 * Math.sin(phase * 2));
            draw('drone', rects, focus === 'workshop', 4 * Math.sin(phase), -9 * Math.sin(phase * 2), .018 * Math.sin(phase));
            glow(drone.x + drone.w * .55, drone.y + drone.h - 9 * Math.sin(phase * 2), 13, 24, '#4affed', .18);
            draw('conditions', rects, focus === 'conditions');
            [.30, .49, .67].forEach((y, index) => glow(tower.x + tower.w * .56, tower.y + tower.h * y,
                tower.w * .25, tower.h * .025, index === 2 ? '#ff4932' : '#ffb74e', .04 + .05 * Math.max(0, Math.sin(phase * 2 - index))));
            for (let i = 0; i < 12; i++) {
                const progress = ((t / 6 + i / 12) % 1 + 1) % 1;
                glow(gate.x + gate.w * (.25 + (i % 4) * .16) + 12 * Math.sin(phase + i), gate.y + gate.h - progress * gate.h * .28,
                    22 + progress * 22, 10 + progress * 18, '#e2c9a8', Math.sin(progress * Math.PI) * .04);
            }
            for (let i = 0; i < 12; i++) {
                const progress = ((t / 3 + i / 12) % 1 + 1) % 1;
                glow(gate.x + gate.w * (.2 + ((i * 31) % 60) / 100), gate.y + gate.h * (1 - progress * .28),
                    1.5, 3, '#ffd092', Math.sin(progress * Math.PI) * .5);
            }
            draw('hero', rects, false, 0, 0, .001 * Math.sin(phase), 1 + .003 * Math.sin(phase * 2));
            ctx.save(); ctx.textAlign = 'center'; ctx.fillStyle = '#f1e8d5'; ctx.shadowColor = '#000'; ctx.shadowBlur = 14;
            ctx.font = '600 ' + (portrait ? 76 : 58) + 'px Arial, sans-serif';
            if (portrait) {
                ctx.fillText('ВЫЖИВИ', W / 2, H * .075);
                ctx.fillText('В АРЕНЕ', W / 2, H * .075 + 87);
            } else ctx.fillText('ВЫЖИВИ В АРЕНЕ', W / 2, 140);
            // Touch devices need visible object labels; there is no hover prerequisite.
            for (const id of Object.keys(labels)) {
                const rect = rects[id];
                const baseline = portrait && id === 'gate' ? rect.y - 22 : Math.min(H - 34, rect.y + rect.h + 47);
                ctx.font = (focus === id ? '600 ' : '') + '42px Arial, sans-serif';
                ctx.fillStyle = id === 'workshop' ? '#b6ffed' : '#ffe3af';
                ctx.fillText(labels[id], rect.x + rect.w / 2, baseline);
            }
            ctx.restore();
            return { portrait, width: W, height: H, rects };
        }
        return { render, pick, zones, layout };
    }
    if (typeof module !== 'undefined' && module.exports) module.exports = { create, fit, profiles };
    else root.ArenaMenuAnimation = { create, fit, profiles };
})(typeof globalThis !== 'undefined' ? globalThis : this);
