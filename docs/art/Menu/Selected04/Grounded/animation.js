(function (root) {
    'use strict';
    const TAU = Math.PI * 2;
    const labels = { gate: 'Старт', workshop: 'Мастерская', conditions: 'Условия' };
    // Coordinates belong to the coherent painted scene, not independently positioned cutouts.
    const plans = {
        portrait: {
            gate: [.22, .25, .56, .29], workshop: [0, .43, .49, .32], conditions: [.65, .35, .35, .40],
            hero: [.36, .59, .28, .27], label: { gate: [.5, .245], workshop: [.21, .77], conditions: [.84, .77] },
            contacts: { gate: [.5, .51], workshop: [.22, .73], conditions: [.84, .73], hero: [.51, .84] },
            lights: { gate: [.5, .44], workshop: [.29, .63], conditions: [.89, .47, .525, .58] }
        },
        landscape: {
            gate: [.47, .38, .30, .37], workshop: [0, .35, .35, .48], conditions: [.76, .17, .24, .65],
            hero: [.35, .56, .10, .37], label: { gate: [.625, .79], workshop: [.20, .86], conditions: [.875, .86] },
            contacts: { gate: [.62, .74], workshop: [.20, .81], conditions: [.86, .81], hero: [.41, .927] },
            lights: { gate: [.62, .61], workshop: [.23, .60], conditions: [.88, .36, .46, .56] }
        }
    };
    function create(canvas, images, data) {
        const ctx = canvas.getContext('2d');
        function layout() {
            const portrait = canvas.height > canvas.width;
            const key = portrait ? 'portrait' : 'landscape';
            const W = portrait ? 1080 : 1080 * canvas.width / canvas.height;
            const H = portrait ? 1080 * canvas.height / canvas.width : 1080;
            const item = data.layers.find(item => item.id === key);
            const scale = Math.max(W / item.width, H / item.height);
            const ox = (W - item.width * scale) / 2, oy = (H - item.height * scale) / 2;
            function point(u, v) { return { x: ox + u * item.width * scale, y: oy + v * item.height * scale }; }
            function rect(box) {
                const p = point(box[0], box[1]), q = point(box[0] + box[2], box[1] + box[3]);
                return { x: p.x, y: p.y, w: q.x - p.x, h: q.y - p.y };
            }
            return { W, H, portrait, key, item, plan: plans[key], scale, ox, oy, point, rect };
        }
        function zones() {
            const view = layout();
            return Object.keys(labels).map(id => {
                const raw = view.rect(view.plan[id]);
                const x = Math.max(0, raw.x), y = Math.max(0, raw.y);
                return { id, x: x / view.W, y: y / view.H, w: (Math.min(view.W, raw.x + raw.w) - x) / view.W,
                    h: (Math.min(view.H, raw.y + raw.h) - y) / view.H };
            });
        }
        function pick(x, y) {
            const view = layout(), actor = view.rect(view.plan.hero);
            const px = x * view.W, py = y * view.H;
            if (px >= actor.x && px <= actor.x + actor.w && py >= actor.y && py <= actor.y + actor.h) return null;
            const zone = zones().find(zone => x >= zone.x && x <= zone.x + zone.w && y >= zone.y && y <= zone.y + zone.h);
            return zone ? zone.id : null;
        }
        function glow(point, rx, ry, color, alpha) {
            ctx.save(); ctx.globalCompositeOperation = 'screen'; ctx.translate(point.x, point.y); ctx.scale(rx, ry);
            const fill = ctx.createRadialGradient(0, 0, 0, 0, 0, 1);
            fill.addColorStop(0, color); fill.addColorStop(1, 'rgba(0,0,0,0)');
            ctx.globalAlpha = alpha; ctx.fillStyle = fill; ctx.fillRect(-1, -1, 2, 2); ctx.restore();
        }
        function render(time, options = {}) {
            const view = layout(), { W, H, plan } = view;
            const t = options.motion === false ? 0 : time, phase = t / 6 * TAU;
            const focus = options.hover || options.selected;
            ctx.setTransform(canvas.width / W, 0, 0, canvas.height / H, 0, 0);
            ctx.clearRect(0, 0, W, H);
            // One image carries all geometry, shadows, occlusion and contact with the pavement.
            ctx.drawImage(images[view.key], view.ox, view.oy, view.item.width * view.scale, view.item.height * view.scale);
            const gate = view.rect(plan.gate), bench = view.rect(plan.workshop), tower = view.rect(plan.conditions);
            glow(view.point(...plan.lights.gate), gate.w * .22, gate.h * .28, '#ffd18a', .07 + .05 * Math.sin(phase));
            glow(view.point(...plan.lights.workshop), bench.w * .24, bench.h * .18, '#51edcf', .05 + .03 * Math.sin(phase * 2));
            plan.lights.conditions.slice(1).forEach((y, i) => glow(view.point(plan.lights.conditions[0], y),
                tower.w * .20, tower.h * .025, '#ff7b38', .035 + .085 * Math.max(0, Math.sin(phase * 2 - i))));
            for (let i = 0; i < 10; i++) {
                const p = ((t / 6 + i / 10) % 1 + 1) % 1;
                const ground = view.point(...plan.contacts.gate);
                glow({ x: ground.x + gate.w * (((i * 17) % 50) / 100 - .25) + 8 * Math.sin(phase + i), y: ground.y - p * gate.h * .20 },
                    20 + p * 18, 8 + p * 13, '#d9c6ab', Math.sin(p * Math.PI) * .08);
            }
            if (focus && labels[focus]) {
                const p = view.point(...plan.contacts[focus]);
                const rect = view.rect(plan[focus]);
                glow(p, rect.w * .38, 20, focus === 'workshop' ? '#7affdb' : '#ffd08b', .12);
            }
            ctx.save(); ctx.textAlign = 'center'; ctx.fillStyle = '#f0e6d1'; ctx.shadowColor = '#000'; ctx.shadowBlur = 12;
            ctx.font = '600 ' + (view.portrait ? 76 : 58) + 'px Arial, sans-serif';
            if (view.portrait) { ctx.fillText('ВЫЖИВИ', W / 2, H * .065); ctx.fillText('В АРЕНЕ', W / 2, H * .065 + 87); }
            else ctx.fillText('ВЫЖИВИ В АРЕНЕ', W / 2, H * .12);
            for (const id of Object.keys(labels)) {
                const p = view.point(...plan.label[id]);
                ctx.font = (focus === id ? '600 ' : '') + '40px Arial, sans-serif';
                ctx.fillStyle = id === 'workshop' ? '#c1ffec' : '#ffe2b4';
                const half = ctx.measureText(labels[id]).width / 2;
                ctx.fillText(labels[id], Math.max(half + 20, Math.min(W - half - 20, p.x)), Math.min(H - 35, p.y));
            }
            ctx.restore();
            return view;
        }
        return { render, pick, zones, layout };
    }
    if (typeof module !== 'undefined' && module.exports) module.exports = { create, plans };
    else root.ArenaMenuAnimation = { create, plans };
})(typeof globalThis !== 'undefined' ? globalThis : this);
