(function (root) {
    'use strict';
    const W = 1920, H = 1080, TAU = Math.PI * 2;
    const labels = { gate: 'Старт', workshop: 'Мастерская', conditions: 'Условия' };
    function create(canvas, images, data) {
        const ctx = canvas.getContext('2d');
        function glow(x, y, rx, ry, color, opacity) {
            ctx.save();
            ctx.translate(x, y); ctx.scale(rx, ry);
            const gradient = ctx.createRadialGradient(0, 0, 0, 0, 0, 1);
            gradient.addColorStop(0, color); gradient.addColorStop(1, 'rgba(0,0,0,0)');
            ctx.fillStyle = gradient; ctx.globalAlpha = opacity;
            ctx.fillRect(-1, -1, 2, 2); ctx.restore();
        }
        function layer(id, dx = 0, dy = 0, rotation = 0, sy = 1, hover = false) {
            const item = data.layers.find(layer => layer.id === id);
            const [sx, yy, sw, sh] = item.crop;
            const [x, y, width, height] = item.destination;
            ctx.save();
            ctx.translate((x + width / 2) * W + dx, (y + height) * H + dy);
            ctx.rotate(rotation); ctx.scale(1, sy);
            if (hover) { ctx.shadowColor = id === 'workshop' ? '#44f8e4' : '#ffb64e'; ctx.shadowBlur = 24; }
            ctx.drawImage(images[id], sx, yy, sw, sh, -width * W / 2, -height * H, width * W, height * H);
            ctx.restore();
        }
        function pick(x, y) {
            // The three world objects are separate zones; the foreground operator is not a menu action.
            if (x < .35 && y > .33 && y < .94) return 'workshop';
            if (x > .77 && y > .14 && y < .94) return 'conditions';
            if (x > .47 && x < .76 && y > .41 && y < .89) return 'gate';
            return null;
        }
        function render(time, options = {}) {
            const motion = options.motion !== false;
            const t = motion ? time : 0;
            const hover = options.hover || null;
            const selected = options.selected || null;
            const cycle = TAU * t / 6;
            ctx.setTransform(canvas.width / W, 0, 0, canvas.height / H, 0, 0);
            ctx.clearRect(0, 0, W, H);
            ctx.drawImage(images.background, 0, 0, W, H);
            // Ground contact shadows sit under the cutouts, not baked into their transparent pixels.
            glow(340, 1000, 380, 40, '#05080c', .35);
            glow(1670, 990, 260, 50, '#05080c', .4);
            layer('gate', 0, 0, 0, 1, hover === 'gate' || selected === 'gate');
            glow(1190, 755, 175, 220, '#ffc56d', .06 + .035 * (1 + Math.sin(cycle)));
            glow(1190, 930, 260, 38, '#ffba57', .13 + .05 * Math.sin(cycle));
            layer('workshop', 0, 0, 0, 1, hover === 'workshop' || selected === 'workshop');
            glow(355, 675, 155, 115, '#39e6d7', .045 + .025 * (1 + Math.sin(cycle * 2)));
            layer('drone', 2 * Math.sin(cycle), -7 * Math.sin(cycle * 2), .018 * Math.sin(cycle), 1, hover === 'workshop');
            glow(516, 700 - 7 * Math.sin(cycle * 2), 18, 35, '#44ffeb', .16 + .06 * Math.cos(cycle * 2));
            layer('conditions', 0, 0, 0, 1, hover === 'conditions' || selected === 'conditions');
            [375, 525, 682].forEach((y, index) => {
                const pulse = .035 + .055 * Math.pow(Math.max(0, Math.sin(cycle * 2 - index * .7)), 2);
                glow(1720, y, 115, 25, index === 2 ? '#ff4b27' : '#ffad43', pulse);
            });
            // Low-contrast rising steam and tiny sparks, all deterministic for a seamless six-second loop.
            for (let i = 0; i < 12; i++) {
                const phase = ((t / 6 + i / 12) % 1 + 1) % 1;
                const fade = Math.sin(phase * Math.PI);
                glow(1050 + (i % 4) * 85 + 18 * Math.sin(cycle + i), 925 - phase * 165,
                    28 + phase * 28, 12 + phase * 20, '#d9c3a4', fade * .038);
            }
            for (let i = 0; i < 14; i++) {
                const phase = ((t / 3 + i / 14) % 1 + 1) % 1;
                const x = 990 + ((i * 53) % 410) + 15 * Math.sin(cycle + i);
                const y = 912 - phase * (100 + i % 4 * 25);
                glow(x, y, 2, 4, '#ffd18c', Math.sin(phase * Math.PI) * .48);
            }
            // Idle is deliberately subtle: breathing anchored at the cropped feet, no whole-body bouncing.
            layer('hero', 0, 0, .0015 * Math.sin(cycle), 1 + .003 * Math.sin(cycle * 2));
            ctx.save();
            ctx.textAlign = 'center'; ctx.fillStyle = '#f0e5ce';
            ctx.shadowColor = 'rgba(0,0,0,.8)'; ctx.shadowBlur = 16;
            ctx.font = '600 58px Arial, sans-serif';
            ctx.fillText('ВЫЖИВИ В АРЕНЕ', 960, 145);
            if (hover || selected) {
                const id = hover || selected;
                const position = { workshop: [330, 1025], gate: [1200, 1000], conditions: [1700, 1045] }[id];
                ctx.font = '28px Arial, sans-serif'; ctx.fillStyle = id === 'workshop' ? '#a9fff1' : '#ffe1ab';
                ctx.fillText(labels[id], position[0], position[1]);
            }
            ctx.restore();
            return { time: t, hover, selected };
        }
        return { render, pick };
    }
    if (typeof module !== 'undefined' && module.exports) module.exports = { create };
    else root.ArenaMenuAnimation = { create };
})(typeof globalThis !== 'undefined' ? globalThis : this);
