// All 2D art for No Winners: palette, screens, sprites, desk paper. Everything lands on the pixel grid.
import { writeFileSync, mkdirSync, readFileSync } from 'node:fs';
import { deflateSync, crc32 } from 'node:zlib';
import { dirname, join } from 'node:path';

const FONT = JSON.parse(readFileSync(new URL('./font8x8.json', import.meta.url)));
const rgba = (h) => [1, 3, 5, 7].map((i) => (h.length > i ? parseInt(h.slice(i, i + 2), 16) : 255));

export class Img {
  constructor(w, h, bg) { this.w = w; this.h = h; this.d = new Uint8Array(w * h * 4); if (bg) this.rect(0, 0, w, h, bg); }
  rect(x, y, w, h, c) {
    const [r, g, b, a] = rgba(c);
    for (let j = Math.max(0, y); j < Math.min(this.h, y + h); j++) for (let i = Math.max(0, x); i < Math.min(this.w, x + w); i++) {
      const o = (j * this.w + i) * 4; this.d[o] = r; this.d[o + 1] = g; this.d[o + 2] = b; this.d[o + 3] = a;
    }
    return this;
  }
  frame(x, y, w, h, c, t = 2) { this.rect(x, y, w, t, c); this.rect(x, y + h - t, w, t, c); this.rect(x, y, t, h, c); this.rect(x + w - t, y, t, h, c); return this; }
  chip(x, y, w, h, c) { this.rect(x + 2, y, w - 4, h, c); this.rect(x, y + 2, w, h - 4, c); return this; }
  disc(cx, cy, r, c) { for (let dy = -r; dy <= r; dy++) { const w = Math.floor(Math.sqrt(r * r - dy * dy)); this.rect(cx - w, cy + dy, w * 2 + 1, 1, c); } return this; }
  text(s, x, y, c, sx = 1, sy = sx) {
    [...s].forEach((ch, i) => (FONT[ch] || FONT['?']).forEach((row, r) => { for (let k = 0; k < 8; k++) if (row >> k & 1) this.rect(x + (i * 8 + k) * sx, y + r * sy, sx, sy, c); }));
    return this;
  }
  centred(s, y, c, sx = 1, sy = sx, cx = this.w / 2) { return this.text(s, Math.round(cx - s.length * 4 * sx), y, c, sx, sy); }
  blit(o, x, y) {
    for (let j = 0; j < o.h; j++) for (let i = 0; i < o.w; i++) {
      const s = (j * o.w + i) * 4; if (!o.d[s + 3]) continue;
      const t = ((y + j) * this.w + x + i) * 4; if (t < 0 || x + i >= this.w || x + i < 0 || t >= this.d.length) continue;
      for (let k = 0; k < 4; k++) this.d[t + k] = o.d[s + k];
    }
    return this;
  }
  png() {
    const raw = Buffer.alloc((this.w * 4 + 1) * this.h);
    for (let y = 0; y < this.h; y++) { raw[y * (this.w * 4 + 1)] = 0; Buffer.from(this.d.buffer, y * this.w * 4, this.w * 4).copy(raw, y * (this.w * 4 + 1) + 1); }
    const chunk = (type, data) => { const t = Buffer.from(type), len = Buffer.alloc(4), crc = Buffer.alloc(4); len.writeUInt32BE(data.length); crc.writeUInt32BE(crc32(Buffer.concat([t, data])) >>> 0); return Buffer.concat([len, t, data, crc]); };
    const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(this.w, 0); ihdr.writeUInt32BE(this.h, 4); ihdr[8] = 8; ihdr[9] = 6;
    return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
  }
}

// ---- Marquee screen palette, from desk.html's C ----
const C = { bg: '#23093A', panel: '#3A1058', line: '#7A3AA0', ink: '#FFF3D6', dim: '#BE9ADB', gold: '#FFC93C', good: '#5CF2C8', alarm: '#FF6D90', red: '#C9174F', pink: '#FF3D6E', socket: '#120420' };
const TIERS = [['#5A2A9E', '#FFF3D6', '#CFB4F5'], ['#C2187A', '#FFF3D6', '#FFC4E4'], ['#FF8A1F', '#23093A', '#5C1F00'], ['#F5D23C', '#23093A', '#5A4800']];
const BARS = ['#FFC93C', '#FF3D6E', '#5CF2C8', '#C2187A', '#7A3AA0', '#FF8A1F', '#FFF3D6'];
const W = 640, H = 360, SQ = 30, CELL = 32;
const pad = (n) => String(n).padStart(2, '0');

function bulbs(g, x, y, w, h, phase) {
  let i = 0;
  const dot = (a, b) => g.rect(a, b, 4, 4, (i++ + phase) % 3 === 0 ? C.gold : C.line);
  for (let a = x + 2; a < x + w - 4; a += 12) { dot(a, y + 2); dot(a, y + h - 6); }
  for (let b = y + 14; b < y + h - 8; b += 12) { dot(x + 2, b); dot(x + w - 6, b); }
}
function drum(g, d, x, y) { g.chip(x, y, 84, 88, C.ink); g.text(d, x + 10, y + 12, C.bg, 8); g.rect(x, y + 43, 84, 2, C.bg); }

function cellFree(g, x, y, teal) {
  g.chip(x, y, SQ, SQ, teal ? C.good : C.socket);
  g.chip(x + 2, y + 2, SQ - 4, SQ - 4, C.bg);
  g.chip(x + 3, y + 3, SQ - 6, SQ - 6, C.socket);
}
function monitorFrame(phase) {
  const g = new Img(W, H, C.bg);
  g.text('THE BIG DRAW!', 16, 8, C.gold).text('CASH', 288, 8, C.dim).text('STREAK', 392, 8, C.dim).text('PRIZE', 480, 8, C.dim);
  g.rect(16, 19, 608, 2, C.gold);
  g.rect(344, 24, 280, 320, C.panel);
  bulbs(g, 344, 24, 280, 320, phase);
  g.centred('BEHIND THE CURTAIN', 32, C.gold, 1, 1, 484);
  TIERS.forEach(([f], i) => { g.rect(358, 252 + i * 12, 8, 8, f); g.text(['1-2', '3-5', '6-10', '11+'][i], 372, 252 + i * 12, C.ink); });
  return g;
}
function stage(phase) {
  const g = new Img(W, H);
  for (let x = 0; x < W; x += 24) g.rect(x, 0, 24, H, x % 48 ? '#7A0F2E' : '#A81440');
  g.rect(120, 40, 400, 236, C.bg);
  bulbs(g, 120, 40, 400, 236, phase);
  g.centred('THE BIG DRAW!', 56, C.gold, 2, 3);
  g.chip(16, 16, 80, 24, C.pink).text('LIVE', 24, 20, C.ink, 2);
  return g;
}
const strip = (a, b) => {
  const g = new Img(400, 48);
  g.centred(a, 0, C.ink, 2);
  if (b) g.centred(b, 24, C.alarm, 2);
  return g;
};
function ticker(g, s) { g.rect(0, 318, W, 16, C.gold); g.text(s, 8, 322, C.bg); }

// ---- Punch cards: 23 faces, ok and disabled ----
const CARD_W = 128, CARD_H = 176;
export const CARDS = [...[1, 2, 3, 4, 5, 6, 7, 8, 9].map((v) => `+${v}`), ...[1, 2, 3, 4, 5, 6, 7, 8, 9].map((v) => `-${v}`), '×2', '×3', '÷2', '÷3', '÷5'];
function punchCard(label, ok) {
  const g = new Img(CARD_W, CARD_H, ok ? '#D9C79A' : '#7A7058');
  const ink = ok ? '#2A2010' : '#4A4232';
  for (let i = 0; i < 24; i += 2) g.rect(0, i, 24 - i, 2, '#00000000'); // Clipped corner, transparent.
  g.centred('CORRECT', 12, ink, 1, 1, 72);
  if (label) g.centred(label, 44, ink, 4, 5);
  const seed = label ? label.charCodeAt(0) + label.charCodeAt(1) : 0;
  for (let x = 0; x < 7; x++) for (let y = 0; y < 3; y++) if (!label || (seed + x * 3 + y * 5) % 3) g.rect(11 + x * 16, 108 + y * 20, 8, 12, ok ? '#5A4420' : '#3A3424');
  return g;
}

// ---- Label plates, lit row then dim row ----
export const PLATES = ['SUPPLIER', 'BRIBE', 'SHRED', 'GO LIVE', 'ON AIR', 'EJECT', 'ROLL IT', 'NEXT WEEK', 'PLAY AGAIN', 'GO LIVE FIRST'];
export const PLATE_W = 192, PLATE_H = 32;
function plates() {
  const g = new Img(PLATE_W, PLATE_H * PLATES.length * 2);
  PLATES.forEach((l, i) => [['#111114', l === 'ON AIR' ? '#FF3B2F' : '#F2F2E6'], ['#111114', '#6A6A70']].forEach(([bg, ink], row) => {
    const y = (i * 2 + row) * PLATE_H, s = l.length > 10 ? 1 : 2;
    g.rect(0, y, PLATE_W, PLATE_H, bg).frame(0, y, PLATE_W, PLATE_H, '#3A3A40');
    g.centred(l, y + (s === 2 ? 8 : 12), ink, s, s, PLATE_W / 2);
  }));
  return g;
}

function poster() {
  const g = new Img(256, 340, '#D8241C');
  g.frame(8, 8, 240, 324, '#F2F2E6', 4);
  [['LUCK', '#F2F2E6'], ['IS A', '#F2F2E6'], ['BUDGET', '#F2F2E6'], ['ITEM', '#FFE14A']].forEach(([l, c], i) => {
    const s = Math.min(5, Math.floor(216 / (l.length * 8)));
    g.centred(l, 40 + i * 64, c, s, 6);
  });
  g.rect(24, 288, 208, 4, '#F2F2E6');
  g.centred('MINISTRY OF CHANCE', 300, '#F2F2E6');
  return g;
}
function calendar() {
  const g = new Img(256, 320, '#E8E2CC');
  g.rect(0, 0, 256, 40, '#B3241C').centred('INSPECTIONS', 12, '#F2F2E6', 2, 2);
  for (let w = 1; w <= 12; w++) {
    const x = 8 + ((w - 1) % 4) * 61, y = 48 + Math.floor((w - 1) / 4) * 80, insp = w % 4 === 0;
    g.rect(x, y, 57, 74, insp ? '#B3241C' : '#D0C8AE');
    g.centred(`${w}${insp ? '!' : ''}`, y + 26, insp ? '#F2F2E6' : '#1C1E28', 2, 3, x + 28);
  }
  g.centred('ONE CARD ONLY ON RED WEEKS', 296, '#1C1E28');
  return g;
}
// The job and the desk actions, so the desk itself needs no labels for them.
function memo() {
  const g = new Img(256, 320, '#F4EFDC'), ink = '#1C1E28';
  g.rect(0, 0, 256, 24, '#1A2AA8').centred('STAFF NOTICE', 8, '#F4EFDC');
  g.centred('CHANGE THE', 34, ink, 2, 2).centred('NUMBER.', 54, ink, 2, 2).centred('NOBODY', 78, '#B3241C', 2, 2).centred('MAY WIN.', 98, '#B3241C', 2, 2);
  g.rect(12, 122, 232, 2, ink).centred('OFF AIR: ONE JOB A WEEK', 130, ink);
  const row = (y, name, line, draw) => { g.rect(12, y, 40, 40, '#D0C8AE'); draw(12, y); g.text(name, 60, y + 6, ink, 2, 2).text(line, 60, y + 28, '#55523F'); };
  row(146, 'SUPPLIER', '+1 CARD', (x, y) => { g.rect(x + 6, y + 8, 28, 6, '#A02820').rect(x + 8, y + 16, 24, 16, '#A02820').disc(x + 20, y + 24, 5, '#E8E2CC'); });
  row(194, 'BRIBE', '+80 CASH, PRIZE +40', (x, y) => { g.rect(x + 5, y + 10, 30, 20, '#D8C89A').frame(x + 5, y + 10, 30, 20, '#9A8A60', 1).disc(x + 20, y + 20, 4, '#A02820'); });
  row(242, 'SHRED', '-2 HOLDERS, 1 SQUARE', (x, y) => { g.rect(x + 6, y + 8, 28, 10, '#3A3A40'); for (let i = 0; i < 5; i++) g.rect(x + 8 + i * 5, y + 18, 3, 12 + (i % 2) * 6, '#F4EFDC'); });
  g.rect(0, 294, 256, 26, ink).centred('THEN THROW THE SWITCH.', 303, '#FFE14A');
  return g;
}
function newspaper(a, b) {
  const g = new Img(256, 300, '#EDE6CE');
  g.rect(0, 0, 256, 34, '#1C1E28').centred('THE DAILY TICKET', 13, '#EDE6CE');
  g.centred(a, 52, '#1C1E28', 2, 3).centred(b, 86, '#B3241C', 2, 3);
  for (let i = 0; i < 9; i++) g.rect(16, 158 + i * 14, i % 4 === 3 ? 120 : 224, 4, '#7D7766');
  g.rect(16, 122, 224, 2, '#1C1E28');
  return g;
}
function windowNight() {
  const g = new Img(256, 192, '#0C1430');
  let seed = 11; const r = (n) => (seed = (seed * 16807) % 2147483647) % n;
  let x = 0;
  while (x < 256) {
    const w = 18 + r(26), top = 60 + r(90), shade = ['#141C3C', '#182244', '#101830'][r(3)];
    g.rect(x, top, w, 192 - top, shade);
    for (let wy = top + 6; wy < 186; wy += 10) for (let wx = x + 4; wx < x + w - 5; wx += 8) if (r(5) < 2) g.rect(wx, wy, 4, 5, r(3) ? '#E8C060' : '#7FA0D0');
    x += w;
  }
  return g;
}
function ballNumbers() {
  const s = 64, g = new Img(s * 10, s * 10);
  for (let n = 0; n < 100; n++) {
    const x = (n % 10) * s, y = Math.floor(n / 10) * s;
    g.rect(x, y, s, s, ['#F2F2E6', '#FFD23A', '#E8431F'][n % 3]);
    g.disc(x + 32, y + 32, 22, '#F2F2E6');
    g.centred(pad(n), y + 24, '#0A0A12', 2, 2, x + 32);
  }
  return g;
}
function cursor() {
  const g = new Img(32, 32), rows = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 6, 3, 4, 4, 2];
  const pts = rows.map((w, y) => [w, y]);
  pts.forEach(([w, y]) => g.rect(0, y * 2, (w + 1) * 2, 2, '#0A0A12'));
  pts.forEach(([w, y]) => { if (y < 14) g.rect(2, y * 2, Math.max(0, (w - 1) * 2), 2, '#F2F2E6'); });
  return g;
}
function logo() {
  const g = new Img(320, 64, '#FFC93C');
  g.rect(0, 0, 320, 4, '#C9174F').rect(0, 60, 320, 4, '#C9174F');
  g.centred('NO WINNERS', 12, '#23093A', 3, 5);
  return g;
}
function fontAtlas() {
  const g = new Img(128, 56), keys = Object.keys(FONT).sort();
  keys.forEach((ch, i) => g.text(ch, (i % 16) * 8, Math.floor(i / 16) * 8, '#FFFFFF'));
  return { g, keys };
}

// ---- Review mocks: the frames above with sample play values on them ----
function sampleBoard() {
  let seed = 7;
  const r = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
  const weight = (n) => ([7, 8, 77, 88].includes(n) ? 8 : n === 4 || n === 13 ? 0.25 : n >= 1 && n <= 31 ? 3 : 1);
  const total = Array.from({ length: 100 }, (_, n) => weight(n)).reduce((a, b) => a + b);
  const b = Array(100).fill(0);
  for (let t = 0; t < 190; t++) { let v = r() * total, n = 0; while (n < 99 && (v -= weight(n)) >= 0) n++; b[n]++; }
  Object.assign(b, { 37: 4, 42: 1, 74: 1, 71: 0 });
  return b;
}
const tierOf = (h) => (h <= 2 ? 0 : h <= 5 ? 1 : h <= 10 ? 2 : 3);
function mockMonitor() {
  const g = monitorFrame(0), b = sampleBoard(), at = (n) => [16 + (n % 10) * CELL, 24 + Math.floor(n / 10) * CELL];
  g.text('WK 07/12', 184, 8, C.ink).text('1046', 328, 8, C.gold).text('03', 448, 8, C.good).text('240', 528, 8, C.gold);
  for (let n = 0; n < 100; n++) {
    const [x, y] = at(n);
    if (b[n]) { const [f, num, cnt] = TIERS[tierOf(b[n])]; g.chip(x, y, SQ, SQ, f).text(pad(n), x + 7, y + 5, num); const c = String(b[n]); g.text(c, x + SQ - 5 - c.length * 8, y + 18, cnt); }
    else { cellFree(g, x, y); g.text(pad(n), x + 7, y + 11, C.good); }
  }
  const [mx, my] = at(71); g.frame(mx - 2, my - 2, SQ + 4, SQ + 4, '#FFFFFF', 3);
  g.text('THE BALLS SAY', 358, 46, C.gold).text('37', 470, 46, C.ink);
  drum(g, '7', 358, 66); drum(g, '1', 450, 66);
  g.text('37 ×2 -3', 358, 168, C.ink).text('NOBODY HOLDS THIS!', 358, 188, C.good).text('SHOW GOES ON!', 358, 201, C.good);
  g.rect(536, 40, 76, 20, C.alarm).text('AIR 14', 540, 44, C.bg, 1, 2);
  return g;
}
function mockTV() {
  const g = stage(0);
  drum(g, '7', 232, 96); drum(g, '1', 324, 96);
  g.blit(strip('NOBODY WON!', 'WHAT A SHAME!'), 120, 204);
  ticker(g, '  MINISTER "DELIGHTED"   *   NUMBER 77 STILL DUE, SAY EXPERTS   *   TICKET SALES UP AGAIN   *');
  return g;
}

export function buildTextures(root, palette) {
  const out = (p, g) => { const f = join(root, 'Textures', p); mkdirSync(dirname(f), { recursive: true }); writeFileSync(f, g.png()); };
  const swatches = Object.values(palette.colors);
  const pal = new Img(palette.size * palette.cell, palette.size * palette.cell);
  swatches.forEach((c, i) => pal.rect((i % palette.size) * palette.cell, Math.floor(i / palette.size) * palette.cell, palette.cell, palette.cell, c));
  out('palette.png', pal);

  [0, 1, 2].forEach((p) => { out(`Screens/mon_frame_${p}.png`, monitorFrame(p)); out(`Screens/tv_stage_${p}.png`, stage(p)); });
  const bars = (word) => { const g = new Img(W, H, '#101020'); BARS.forEach((b, i) => g.rect(i * 92, 0, 92, 240, b)); g.centred(word, 262, '#F2F2E6', 2, 3); return g; };
  out('Screens/tv_standby.png', bars('DRAW COMING UP'));
  out('Screens/tv_live_studio.png', bars('LIVE IN THE STUDIO'));
  out('Screens/tv_line_clean.png', strip('NOBODY WON!', 'WHAT A SHAME!'));
  out('Screens/tv_line_congrats.png', strip('', 'CONGRATULATIONS!'));
  out('Screens/tv_line_small.png', strip('', 'A NATIONAL HERO!'));
  out('Screens/tv_line_scandal.png', strip('', 'NATIONAL SCANDAL!'));
  const card = (lines) => { const g = new Img(W, H, C.bg); bulbs(g, 16, 16, W - 32, H - 32, 0); lines.forEach(([s, y, c, sx, sy]) => g.centred(s, y, c, sx, sy)); return g; };
  out('Screens/tv_title.png', card([['NO WINNERS', 96, C.gold, 5, 7], ['THE NATION IS WATCHING.', 196, C.ink, 2, 2], ['PRESS THE RED BUTTON', 268, C.alarm, 2, 2]]));
  out('Screens/tv_win.png', card([['12 WEEKS.', 88, C.ink, 4, 5], ['NOBODY WON.', 140, C.gold, 4, 5], ['THE MINISTER IS DELIGHTED.', 232, C.alarm, 2, 2]]));
  { const g = new Img(W, H, '#0A0A12'); g.centred('PLEASE STAND BY', 164, '#F2F2E6', 3, 4); out('Screens/tv_please_stand_by.png', g); }

  const one = (fn) => { const g = new Img(34, 34); fn(g); return g; };
  out('Sprites/cell_free.png', one((g) => cellFree(g, 2, 2)));
  out('Sprites/cell_free_teal.png', one((g) => cellFree(g, 2, 2, true)));
  TIERS.forEach(([f], i) => out(`Sprites/cell_tier${i + 1}.png`, one((g) => g.chip(2, 2, SQ, SQ, f))));
  out('Sprites/cell_mark.png', one((g) => g.frame(0, 0, 34, 34, '#FFFFFF', 3)));
  out('Sprites/cell_preview.png', one((g) => { for (let i = 0; i < 34; i += 6) { g.rect(i, 0, 3, 2, '#FFFFFF').rect(i, 32, 3, 2, '#FFFFFF').rect(0, i, 2, 3, '#FFFFFF').rect(32, i, 2, 3, '#FFFFFF'); } }));
  const drums = new Img(840, 88); [...'0123456789'].forEach((d, i) => drum(drums, d, i * 84, 0)); out('Sprites/drums.png', drums);
  const { g: fa, keys } = fontAtlas(); out('Sprites/font8x8.png', fa);
  out('Sprites/cursor.png', cursor());
  out('Sprites/logo.png', logo());

  const atlas = new Img(CARD_W * 12, CARD_H * 4);
  [true, false].forEach((ok, row) => CARDS.forEach((l, i) => { const cols = 12; atlas.blit(punchCard(l, ok), (i % cols) * CARD_W, (row * 2 + Math.floor(i / cols)) * CARD_H); }));
  out('Desk/card_faces.png', atlas);
  out('Desk/card_blank.png', punchCard('', true));
  out('Desk/plates.png', plates());
  out('Desk/poster.png', poster());
  out('Desk/calendar.png', calendar());
  out('Desk/memo.png', memo());
  out('Desk/newspaper_clean.png', newspaper('NO WINNER', 'AGAIN'));
  out('Desk/newspaper_shock.png', newspaper('SOMEONE WON', 'SHOCK DRAW'));
  out('Desk/window_night.png', windowNight());
  out('Desk/ball_numbers.png', ballNumbers());
  out('Mocks/mock_monitor.png', mockMonitor());
  out('Mocks/mock_tv.png', mockTV());
  return { fontChars: keys };
}
