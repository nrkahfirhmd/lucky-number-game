// Generates every No Winners art asset into lucky-number/Assets/Art. Run: node tools/art/build.mjs
// Models are flat-shaded OBJ, authored in metres with +Y up and each prop's front facing +Z.
// Colour comes from one palette texture, so a prop is normally a single material.
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildTextures, PLATES, PLATE_H, PLATE_W, CARDS } from './textures.mjs';

const ROOT = fileURLToPath(new URL('../../lucky-number/Assets/Art/', import.meta.url));

const COLORS = {
  wall: '#50625c', dado: '#43544f', floor: '#3b3a3e', floor2: '#35343a', trim: '#2f3d3a', ceil: '#6a6a60', ceilLine: '#5c5c54',
  desk: '#6b4a2e', deskDark: '#5a3d25', deskEdge: '#7c5736', leg: '#3c2a1a', blotter: '#2f4a3a',
  metal: '#6e7268', metalDark: '#585c54', metalLight: '#858a7e', housing: '#b8b4a0', housingDark: '#9a9684', housingDeep: '#7e7a6a',
  tv: '#1a1a1e', tvLight: '#2c2c32', btn: '#d8241c', btnDark: '#a81a14', base: '#222222', machine: '#7a2020', machineDark: '#5a1616',
  brass: '#c8a040', brassDark: '#9a7a2c', paper: '#e8e2cc', phone: '#a02820', phoneDark: '#7a1c16', envelope: '#d8c89a', envelopeDark: '#c0b084',
  seal: '#a02820', steel: '#8a8a90', steelDark: '#5c5c62', black: '#0a0a0e', nearBlack: '#16161a', mug: '#d8d0b8', coffee: '#2a1a10',
  yellow: '#ffe14a', knob: '#3a3a38', door: '#4a3a2a', doorDark: '#3a2c1e', frame: '#8f8a78', frameDark: '#6f6a5a', tape: '#d8c89a',
  glowWarm: '#ffd9a0', glowTube: '#f2f2e6', ledRed: '#ff3b2f', ledDim: '#3a1a10', ledGreen: '#38f070', cord: '#1e1e22', white: '#f2f2e6', dome: '#cfe8ff',
};
const names = Object.keys(COLORS), SIZE = Math.ceil(Math.sqrt(names.length)), CELL = 8;
const swatch = (c) => {
  const i = names.indexOf(c);
  if (i < 0) throw new Error(`No colour ${c}`);
  return [((i % SIZE) + 0.5) / SIZE, 1 - (Math.floor(i / SIZE) + 0.5) / SIZE];
};

// ---- Mesh kit ----
const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const norm = (a) => { const l = Math.hypot(...a) || 1; return a.map((v) => v / l); };
const mean = (ps) => ps.reduce((s, p) => [s[0] + p[0] / ps.length, s[1] + p[1] / ps.length, s[2] + p[2] / ps.length], [0, 0, 0]);

class Model {
  constructor(name) { this.name = name; this.faces = []; this.tf = (p) => p; this.group = 'body'; }
  // Runs fn with points moved by rotation then translation.
  at([x, y, z], { rotX = 0, rotY = 0, rotZ = 0 }, fn) {
    const outer = this.tf;
    const cx = Math.cos(rotX), sx = Math.sin(rotX), cy = Math.cos(rotY), sy = Math.sin(rotY), cz = Math.cos(rotZ), sz = Math.sin(rotZ);
    this.tf = (p) => {
      let [a, b, c] = p;
      [a, b] = [a * cz - b * sz, a * sz + b * cz];
      [b, c] = [b * cx - c * sx, b * sx + c * cx];
      [a, c] = [a * cy + c * sy, -a * sy + c * cy];
      return outer([a + x, b + y, c + z]);
    };
    fn(); this.tf = outer;
  }
  part(name, fn) { const o = this.group; this.group = name; fn(); this.group = o; }
  // Outward is a world-space hint: the face is flipped to agree with it.
  face(pts, mat, uvs, outward) {
    const p = pts.map(this.tf);
    let n = norm(cross(sub(p[1], p[0]), sub(p[2], p[0])));
    if (outward && dot(n, outward(p)) < 0) { p.reverse(); uvs = [...uvs].reverse(); n = n.map((v) => -v); }
    this.faces.push({ p, uvs, n, mat, group: this.group });
  }
  // Convex solid: each face points away from the solid's centre.
  solid(faces, color, mat = 'palette') {
    const all = faces.flat().map(this.tf), c = mean(all), uv = swatch(color);
    faces.forEach((f) => this.face(f, mat, f.map(() => uv), (p) => sub(mean(p), c)));
  }
  // x and z are the centre, y is the bottom.
  box(x, y, z, w, h, d, color, mat) {
    this.taper(x, y, z, w, d, h, w, d, color, mat);
  }
  // Rectangle at the bottom, another at the top. Good for feet, shades and hoppers.
  taper(x, y, z, w0, d0, h, w1, d1, color, mat, dz = 0) {
    const a = [[-w0 / 2, 0, -d0 / 2], [w0 / 2, 0, -d0 / 2], [w0 / 2, 0, d0 / 2], [-w0 / 2, 0, d0 / 2]].map(([u, , v]) => [x + u, y, z + v]);
    const b = [[-w1 / 2, 0, -d1 / 2], [w1 / 2, 0, -d1 / 2], [w1 / 2, 0, d1 / 2], [-w1 / 2, 0, d1 / 2]].map(([u, , v]) => [x + u, y + h, z + v + dz]);
    this.solid([a, b, [a[0], a[1], b[1], b[0]], [a[1], a[2], b[2], b[1]], [a[2], a[3], b[3], b[2]], [a[3], a[0], b[0], b[3]]], color, mat);
  }
  // Rectangle facing +Z tapering to another further back. For the CRT's tube.
  taperZ(y, z0, w0, h0, z1, w1, h1, y1, color) {
    const r = (w, h, yy, z) => [[-w / 2, yy - h / 2, z], [w / 2, yy - h / 2, z], [w / 2, yy + h / 2, z], [-w / 2, yy + h / 2, z]];
    const a = r(w0, h0, y, z0), b = r(w1, h1, y1, z1);
    this.solid([a, b, [a[0], a[1], b[1], b[0]], [a[1], a[2], b[2], b[1]], [a[2], a[3], b[3], b[2]], [a[3], a[0], b[0], b[3]]], color);
  }
  // Cylinder or cone, bottom centre at x,y,z. Axis 'z' lays it on its back facing +Z.
  cyl(x, y, z, r0, r1, h, n, color, { axis = 'y', mat, from = 0, to = 1 } = {}) {
    const ring = (r, yy) => Array.from({ length: n }, (_, i) => {
      const t = (from + (to - from) * i / (to === 1 && from === 0 ? n : n - 1)) * Math.PI * 2, u = Math.cos(t) * r, v = Math.sin(t) * r;
      return axis === 'y' ? [x + u, y + yy, z + v] : axis === 'z' ? [x + u, y + v, z + yy] : [x + yy, y + u, z + v];
    });
    const a = ring(r0, 0), b = ring(r1, h), side = [];
    const count = to === 1 && from === 0 ? n : n - 1;
    for (let i = 0; i < count; i++) { const j = (i + 1) % n; side.push([a[i], a[j], b[j], b[i]]); }
    const closed = [...side];
    if (r0 > 0) closed.push(a);
    if (r1 > 0) closed.push(b);
    this.solid(closed.map((f) => f.filter((p, i) => f.findIndex((q) => q.join() === p.join()) === i)).filter((f) => f.length >= 3), color, mat);
  }
  sphere(x, y, z, r, lat, lon, color, mat) {
    const pt = (i, j) => { const a = Math.PI * i / lat, b = 2 * Math.PI * j / lon; return [x + r * Math.sin(a) * Math.cos(b), y + r * Math.cos(a), z + r * Math.sin(a) * Math.sin(b)]; };
    const faces = [];
    for (let i = 0; i < lat; i++) for (let j = 0; j < lon; j++) {
      const q = [pt(i, j), pt(i, j + 1), pt(i + 1, j + 1), pt(i + 1, j)];
      faces.push(i === 0 ? [q[0], q[3], q[2]] : i === lat - 1 ? [q[0], q[1], q[2]] : q);
    }
    this.solid(faces, color, mat);
  }
  // Textured quad facing +Z, centred. uv is [u0, v0, u1, v1] in OBJ space, v up. bulge makes a screen's convex face.
  plane(x, y, z, w, h, mat, { uv = [0, 0, 1, 1], div = 1, bulge = 0 } = {}) {
    const P = (i, j) => {
      const u = i / div, v = j / div, cu = u * 2 - 1, cv = v * 2 - 1;
      return { p: [x + (u - 0.5) * w, y + (v - 0.5) * h, z + bulge * (1 - cu * cu) * (1 - cv * cv)], t: [uv[0] + (uv[2] - uv[0]) * u, uv[1] + (uv[3] - uv[1]) * v] };
    };
    const front = this.tf([0, 0, 1]), org = this.tf([0, 0, 0]), facing = sub(front, org);
    for (let i = 0; i < div; i++) for (let j = 0; j < div; j++) {
      const q = [P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)];
      this.face(q.map((a) => a.p), mat, q.map((a) => a.t), () => facing);
    }
  }
  // Quad given by corners, flat colour, facing a world-space direction.
  flat(pts, color, facing, mat = 'palette') {
    const uv = swatch(color);
    this.face(pts, mat, pts.map(() => uv), () => facing);
  }
  obj() {
    const v = [], vt = [], vn = [], lines = [`# No Winners: ${this.name}. Generated by tools/art/build.mjs, edit that instead.`, 'mtllib no_winners.mtl'];
    const groups = new Map();
    for (const f of this.faces) { const k = `${f.group}|${f.mat}`; (groups.get(k) || groups.set(k, []).get(k)).push(f); }
    const body = [];
    for (const [k, faces] of groups) {
      const [g, m] = k.split('|');
      body.push(`g ${g}`, `usemtl ${m}`);
      for (const f of faces) {
        const idx = f.p.map((p, i) => {
          v.push(`v ${p.map((a) => a.toFixed(4)).join(' ')}`);
          vt.push(`vt ${f.uvs[i].map((a) => a.toFixed(5)).join(' ')}`);
          vn.push(`vn ${f.n.map((a) => a.toFixed(4)).join(' ')}`);
          return v.length;
        });
        body.push(`f ${idx.map((i) => `${i}/${i}/${i}`).join(' ')}`);
      }
    }
    return [...lines, ...v, ...vt, ...vn, ...body, ''].join('\n');
  }
}

const models = {};
const make = (name, fn) => { const m = new Model(name); fn(m); models[name] = m; return m; };

// ---- Room ----
const RX = 4, RZ = 3.5, RH = 3;
make('room_shell', (m) => {
  m.part('floor', () => {
    for (let i = 0; i < 8; i++) for (let j = 0; j < 7; j++) {
      const x = -RX + i, z = -RZ + j;
      m.flat([[x, 0, z], [x + 1, 0, z], [x + 1, 0, z + 1], [x, 0, z + 1]], (i + j) % 2 ? 'floor' : 'floor2', [0, 1, 0]);
    }
  });
  m.part('walls', () => {
    const wall = (a, b, y0, y1, c, f) => m.flat([[a[0], y0, a[1]], [b[0], y0, b[1]], [b[0], y1, b[1]], [a[0], y1, a[1]]], c, f);
    const sides = [[[-RX, -RZ], [RX, -RZ], [0, 0, 1]], [[-RX, RZ], [RX, RZ], [0, 0, -1]], [[-RX, -RZ], [-RX, RZ], [1, 0, 0]], [[RX, -RZ], [RX, RZ], [-1, 0, 0]]];
    sides.forEach(([a, b, f]) => { wall(a, b, 0, 1, 'dado', f); wall(a, b, 1, RH, 'wall', f); });
  });
  m.part('trim', () => {
    const t = 0.03;
    m.box(0, 0, -RZ + t / 2, 2 * RX, 0.2, t, 'trim'); m.box(0, 0, RZ - t / 2, 2 * RX, 0.2, t, 'trim');
    m.box(-RX + t / 2, 0, 0, t, 0.2, 2 * RZ, 'trim'); m.box(RX - t / 2, 0, 0, t, 0.2, 2 * RZ, 'trim');
    const r = 0.02;
    m.box(0, 1, -RZ + r / 2, 2 * RX, 0.04, r, 'trim'); m.box(0, 1, RZ - r / 2, 2 * RX, 0.04, r, 'trim');
    m.box(-RX + r / 2, 1, 0, r, 0.04, 2 * RZ, 'trim'); m.box(RX - r / 2, 1, 0, r, 0.04, 2 * RZ, 'trim');
  });
  m.part('ceiling', () => {
    m.flat([[-RX, RH, -RZ], [RX, RH, -RZ], [RX, RH, RZ], [-RX, RH, RZ]], 'ceil', [0, -1, 0]);
    for (let x = -RX + 1; x < RX; x++) m.flat([[x - 0.01, RH - 0.002, -RZ], [x + 0.01, RH - 0.002, -RZ], [x + 0.01, RH - 0.002, RZ], [x - 0.01, RH - 0.002, RZ]], 'ceilLine', [0, -1, 0]);
    for (let z = -RZ + 1; z < RZ; z++) m.flat([[-RX, RH - 0.002, z - 0.01], [RX, RH - 0.002, z - 0.01], [RX, RH - 0.002, z + 0.01], [-RX, RH - 0.002, z + 0.01]], 'ceilLine', [0, -1, 0]);
  });
});
make('ceiling_light', (m) => {
  m.box(0, -0.08, 0, 1.3, 0.08, 0.34, 'frame');
  m.part('tubes', () => { m.flat([[-0.6, -0.082, -0.13], [0.6, -0.082, -0.13], [0.6, -0.082, 0.13], [-0.6, -0.082, 0.13]], 'glowTube', [0, -1, 0], 'glow'); });
  for (let i = -2; i <= 2; i++) m.box(i * 0.26, -0.095, 0, 0.012, 0.014, 0.3, 'frameDark');
});
make('window', (m) => {
  const W = 1.6, H = 1.2, t = 0.06;
  m.part('city', () => m.plane(0, 0, 0.005, W, H, 'window_night'));
  m.box(0, H / 2, 0.03, W + 2 * t, t, 0.06, 'frame'); m.box(0, -H / 2 - t, 0.03, W + 2 * t, t, 0.06, 'frame');
  m.box(-W / 2 - t / 2, -H / 2, 0.03, t, H, 0.06, 'frame'); m.box(W / 2 + t / 2, -H / 2, 0.03, t, H, 0.06, 'frame');
  m.box(0, -H / 2 - t - 0.03, 0.06, W + 0.2, 0.03, 0.12, 'frameDark');
  m.part('blinds', () => {
    m.box(0, H / 2 - 0.06, 0.05, W, 0.06, 0.05, 'frameDark');
    for (let i = 0; i < 6; i++) m.at([0, H / 2 - 0.11 - i * 0.075, 0.05], { rotX: 0.6 }, () => m.box(0, -0.005, 0, W - 0.02, 0.01, 0.045, 'frame'));
  });
  m.box(W / 2 - 0.05, H / 2 - 0.6, 0.075, 0.008, 0.54, 0.008, 'cord');
});
make('cabinet', (m) => {
  m.box(0, 0, 0, 0.68, 1.4, 0.6, 'metal');
  m.box(0, 1.4, 0, 0.7, 0.02, 0.62, 'metalDark');
  m.box(0, 0, 0.01, 0.64, 0.05, 0.58, 'metalDark');
  for (let d = 0; d < 3; d++) {
    const y = 0.08 + d * 0.44;
    m.box(0, y, 0.305, 0.62, 0.4, 0.012, 'metalLight');
    m.box(0, y + 0.19, 0.316, 0.14, 0.024, 0.024, 'nearBlack');
    m.box(0, y + 0.27, 0.313, 0.16, 0.07, 0.004, 'paper');
    m.box(0, y + 0.262, 0.312, 0.176, 0.086, 0.002, 'steelDark');
  }
});
make('door', (m) => {
  m.box(0, 0, 0.02, 1.0, 2.1, 0.04, 'door');
  m.box(-0.54, 0, 0.03, 0.08, 2.14, 0.06, 'trim'); m.box(0.54, 0, 0.03, 0.08, 2.14, 0.06, 'trim'); m.box(0, 2.1, 0.03, 1.16, 0.08, 0.06, 'trim');
  m.box(0, 1.2, 0.042, 0.72, 0.72, 0.006, 'doorDark'); m.box(0, 0.2, 0.042, 0.72, 0.8, 0.006, 'doorDark');
  m.at([0.4, 1.02, 0.05], { rotX: Math.PI / 2 }, () => { m.cyl(0, 0, 0, 0.03, 0.03, 0.05, 8, 'brass'); m.cyl(0, 0.05, 0, 0.03, 0, 0.012, 8, 'brass'); });
});
// Paper on a wall, taped at the corners. Origin at the bottom centre, on the wall.
const wallPaper = (name, mat, w, h) => make(name, (m) => {
  m.plane(0, h / 2, 0.004, w, h, mat);
  const t = 0.07;
  [[-w / 2 + 0.02, h - 0.015], [w / 2 - 0.02, h - 0.015], [-w / 2 + 0.02, 0.015], [w / 2 - 0.02, 0.015]].forEach(([x, y]) => m.box(x, y - 0.015, 0.005, t, 0.03, 0.001, 'tape'));
});
wallPaper('poster', 'poster', 0.75, 1);
wallPaper('calendar', 'calendar', 0.42, 0.525);
wallPaper('memo', 'memo', 0.42, 0.525);
wallPaper('newspaper', 'newspaper', 0.48, 0.5625);
// A picture in a frame, standing off the wall. Origin at the bottom centre, on the wall.
const framed = (name, w, h, frame = 'leg', t = 0.025) => make(name, (m) => {
  m.box(0, 0, 0.012, w + 2 * t, h + 2 * t, 0.012, 'nearBlack');
  m.plane(0, t + h / 2, 0.025, w, h, name);
  m.box(0, 0, 0.022, w + 2 * t, t, 0.012, frame); m.box(0, h + t, 0.022, w + 2 * t, t, 0.012, frame);
  m.box(-w / 2 - t / 2, t, 0.022, t, h, 0.012, frame); m.box(w / 2 + t / 2, t, 0.022, t, h, 0.012, frame);
});
// Each entry: model, size and where it hangs. Wall pictures read their own texture of the same name.
const WALL_ART = [
  // Left wall, around the TV. The game zooms in here, so these can be small.
  ['wall_days', 'paper', 0.64, 0.28, [-3.975, 2.42, -0.8], Math.PI / 2],
  ['wall_promo', 'paper', 0.44, 0.62, [-3.975, 1.15, 0.6], Math.PI / 2],
  ['wall_chart', 'frame', 0.4, 0.32, [-3.975, 1.92, 0.6], Math.PI / 2],
  ['wall_minister', 'frame', 0.22, 0.29, [-3.975, 2.38, 0.6], Math.PI / 2],
  ['wall_employee', 'frame', 0.3, 0.375, [-3.975, 2.12, -2.05], Math.PI / 2],
  // Right wall, around the ball machine. Seen wide and from further off, so bigger.
  ['wall_warning', 'paper', 0.7, 0.5, [3.975, 1.95, -1.4], -Math.PI / 2],
  ['wall_log', 'paper', 0.32, 0.44, [3.975, 1.2, -2.5], -Math.PI / 2],
  ['wall_smile', 'frame', 0.36, 0.45, [3.975, 1.95, -0.3], -Math.PI / 2],
  ['wall_firstball', 'frame', 0.3, 0.39, [3.975, 1.25, -0.3], -Math.PI / 2],
  ['wall_privilege', 'paper', 0.52, 0.73, [3.975, 1.2, 0.5], -Math.PI / 2],
  ['wall_filed', 'paper', 0.44, 0.62, [3.975, 1.25, 1.3], -Math.PI / 2],
];
WALL_ART.forEach(([n, kind, w, h]) => (kind === 'frame' ? framed(n, w, h) : wallPaper(n, n, w, h)));

// ---- Desk and heroes ----
make('desk', (m) => {
  const y = 0.74;
  m.box(0, y, 0, 1.9, 0.03, 0.95, 'desk');
  m.box(0, y - 0.02, 0, 1.92, 0.02, 0.97, 'deskEdge');
  m.box(0, y - 0.09, -0.02, 1.8, 0.07, 0.88, 'deskDark');
  for (const [x, z] of [[-0.88, -0.4], [0.88, -0.4], [-0.88, 0.4], [0.88, 0.4]]) m.taper(x, 0, z, 0.05, 0.05, y - 0.08, 0.075, 0.075, 'leg');
  m.box(0, 0.3, -0.42, 1.7, 0.36, 0.02, 'deskDark'); // Modesty panel.
  // Pedestal drawers, on the player's right.
  m.box(0.62, 0.05, 0.02, 0.5, 0.62, 0.84, 'deskDark');
  for (let i = 0; i < 3; i++) { m.box(0.62, 0.08 + i * 0.2, 0.445, 0.46, 0.18, 0.012, 'desk'); m.box(0.62, 0.15 + i * 0.2, 0.456, 0.12, 0.018, 0.018, 'brass'); }
  // Blotter under the card hand.
  m.box(0, y + 0.03, 0.24, 1.02, 0.003, 0.34, 'blotter');
  m.box(-0.5, y + 0.03, 0.24, 0.02, 0.006, 0.34, 'leg'); m.box(0.5, y + 0.03, 0.24, 0.02, 0.006, 0.34, 'leg');
});
make('monitor', (m) => {
  // Origin on the desk, under the bezel's front edge. The stand sits back under the tube.
  const cy = 0.29, front = -0.1;
  m.cyl(0, 0, -0.25, 0.17, 0.15, 0.022, 12, 'housingDark');
  m.taper(0, 0.022, -0.25, 0.12, 0.12, 0.03, 0.2, 0.2, 'housingDeep');
  m.part('housing', () => {
    m.box(0, cy - 0.28, front - 0.13, 0.9, 0.56, 0.13, 'housing');
    m.taperZ(cy - 0.01, front - 0.13, 0.86, 0.52, front - 0.42, 0.42, 0.28, cy + 0.02, 'housingDark');
    for (let i = -3; i <= 3; i++) m.box(i * 0.07, cy + 0.281, front - 0.06, 0.04, 0.003, 0.09, 'housingDeep'); // Vents.
    // Bezel, proud of the tube.
    const bw = 0.05, bh = 0.055, z = front;
    m.box(0, cy + 0.225, z - 0.002, 0.9, bh, 0.016, 'housing'); m.box(0, cy - 0.28, z - 0.002, 0.9, bh, 0.016, 'housing');
    m.box(-0.425, cy - 0.28, z - 0.002, bw, 0.56, 0.016, 'housing'); m.box(0.425, cy - 0.28, z - 0.002, bw, 0.56, 0.016, 'housing');
    // Inner lip, dark, so the bright screen has a soft edge.
    m.box(0, cy + 0.225, z - 0.012, 0.81, 0.01, 0.02, 'housingDeep'); m.box(0, cy - 0.235, z - 0.012, 0.81, 0.01, 0.02, 'housingDeep');
    m.box(-0.405, cy - 0.225, z - 0.012, 0.01, 0.45, 0.02, 'housingDeep'); m.box(0.405, cy - 0.225, z - 0.012, 0.01, 0.45, 0.02, 'housingDeep');
    // Tube backing behind the glass, so there's never a hole.
    m.box(0, cy - 0.225, z - 0.028, 0.81, 0.45, 0.01, 'black');
    // Controls in the chin.
    [0.31, 0.36].forEach((x) => m.cyl(x, cy - 0.255, z + 0.006, 0.012, 0.01, 0.012, 8, 'knob', { axis: 'z' }));
    m.box(-0.34, cy - 0.268, z + 0.005, 0.12, 0.022, 0.002, 'housingDeep');
  });
  m.part('led', () => m.box(0.25, cy - 0.262, z0(front), 0.012, 0.012, 0.004, 'ledGreen', 'glow'));
  m.part('screen', () => m.plane(0, cy, front - 0.015, 0.8, 0.45, 'screen_monitor', { div: 16, bulge: 0.012 }));
  function z0(f) { return f + 0.006; }
});
make('tv', (m) => {
  // Hung on the wall. Origin at the wall, front +Z.
  m.box(0, -0.08, 0.02, 0.5, 0.16, 0.04, 'base');
  m.box(0, -0.5, 0.08, 1.7, 1.0, 0.12, 'tv');
  m.box(0, -0.5, 0.15, 1.7, 0.065, 0.02, 'tvLight'); m.box(0, 0.435, 0.15, 1.7, 0.065, 0.02, 'tvLight');
  m.box(-0.825, -0.435, 0.15, 0.05, 0.87, 0.02, 'tvLight'); m.box(0.825, -0.435, 0.15, 0.05, 0.87, 0.02, 'tvLight');
  m.box(0, -0.435, 0.135, 1.6, 0.87, 0.01, 'black');
  // Chin below the case: speaker slats and two dials.
  m.box(0, -0.62, 0.08, 1.7, 0.12, 0.12, 'tvLight');
  for (let i = 0; i < 18; i++) m.box(-0.6 + i * 0.04, -0.6, 0.141, 0.018, 0.08, 0.003, 'nearBlack');
  [0.5, 0.62].forEach((x) => m.cyl(x, -0.56, 0.14, 0.03, 0.025, 0.018, 10, 'steel', { axis: 'z' }));
  m.part('screen', () => m.plane(0, 0, 0.145, 1.54, 0.87, 'screen_tv', { div: 16, bulge: 0.02 }));
});
make('machine_base', (m) => {
  // Origin on the floor. Front +Z carries the honest number.
  m.box(0, 0, 0, 0.66, 0.06, 0.66, 'machineDark');
  m.box(0, 0.06, 0, 0.6, 0.74, 0.6, 'machine');
  m.box(0, 0.76, 0, 0.64, 0.04, 0.64, 'brass');
  m.box(0, 0.34, 0.298, 0.54, 0.34, 0.008, 'brassDark');
  m.box(0, 0.36, 0.3, 0.52, 0.30, 0.008, 'nearBlack');
  m.part('face', () => m.plane(0, 0.51, 0.3055, 0.5, 0.28, 'machine_face'));
  for (let i = 0; i < 3; i++) { m.box(-0.3, 0.15 + i * 0.05, 0, 0.004, 0.018, 0.4, 'machineDark'); m.box(0.3, 0.15 + i * 0.05, 0, 0.004, 0.018, 0.4, 'machineDark'); }
  [[-0.27, 0.13], [0.27, 0.13], [-0.27, 0.73], [0.27, 0.73]].forEach(([x, y]) => m.cyl(x, y, 0.3, 0.012, 0.008, 0.006, 6, 'brass', { axis: 'z' }));
  m.cyl(0, 0.8, 0, 0.3, 0.26, 0.08, 16, 'brass'); // Collar under the dome.
  m.cyl(0, 0.88, 0, 0.05, 0.05, 0.74, 8, 'steelDark'); // Axle.
  m.cyl(0, 1.66, 0, 0.09, 0.05, 0.06, 10, 'brass'); // Dome cap.
});
make('machine_dome', (m) => m.sphere(0, 1.25, 0, 0.45, 10, 20, 'dome', 'glass'));
make('ball', (m) => {
  // UVs are a planar wrap of one 0..1 cell. Pick the number with the material's tiling and offset.
  const lat = 8, lon = 12, r = 0.07;
  const P = (i, j) => { const a = Math.PI * i / lat, b = 2 * Math.PI * j / lon; return [r * Math.sin(a) * Math.cos(b), r * Math.cos(a), r * Math.sin(a) * Math.sin(b)]; };
  const T = ([x, y]) => [x / r / 2 + 0.5, y / r / 2 + 0.5];
  for (let i = 0; i < lat; i++) for (let j = 0; j < lon; j++) {
    const q = i === 0 ? [P(i, j), P(i + 1, j), P(i + 1, j + 1)] : i === lat - 1 ? [P(i, j), P(i + 1, j), P(i, j + 1)] : [P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)];
    m.face(q, 'ball_numbers', q.map(T), (p) => mean(p));
  }
});
make('big_button_base', (m) => {
  m.box(0, 0, 0, 0.2, 0.012, 0.2, 'base');
  // Hazard ring: 16 wedges, yellow and black.
  for (let i = 0; i < 16; i++) {
    const a = i / 16, b = (i + 1) / 16, c = i % 2 ? 'yellow' : 'nearBlack';
    const P = (t, r, y) => [Math.cos(t * Math.PI * 2) * r, y, Math.sin(t * Math.PI * 2) * r];
    const y = 0.015;
    m.solid([[P(a, 0.075, 0.012), P(b, 0.075, 0.012), P(b, 0.095, 0.012), P(a, 0.095, 0.012)], [P(a, 0.075, y), P(b, 0.075, y), P(b, 0.095, y), P(a, 0.095, y)]], c);
  }
  m.cyl(0, 0.012, 0, 0.075, 0.075, 0.012, 16, 'nearBlack');
  [[-0.088, -0.088], [0.088, -0.088], [-0.088, 0.088], [0.088, 0.088]].forEach(([x, z]) => m.cyl(x, 0.012, z, 0.006, 0.004, 0.003, 6, 'steel'));
});
make('big_button_cap', (m) => {
  // Origin at the cap's rest. Press by moving down about 2 cm.
  m.cyl(0, 0, 0, 0.068, 0.066, 0.03, 20, 'btnDark');
  m.cyl(0, 0.03, 0, 0.066, 0.05, 0.016, 20, 'btn');
});
make('undo_button_base', (m) => { m.box(0, 0, 0, 0.14, 0.012, 0.1, 'base'); m.box(0, 0.012, 0, 0.13, 0.006, 0.09, 'nearBlack'); });
make('undo_button_cap', (m) => { m.box(0, 0, 0, 0.11, 0.014, 0.07, 'steelDark'); m.taper(0, 0.014, 0, 0.11, 0.07, 0.008, 0.09, 0.05, 'steel'); });
make('lamp', (m) => {
  m.cyl(0, 0, 0, 0.09, 0.08, 0.02, 12, 'base');
  m.at([0, 0.02, 0], { rotX: -0.2 }, () => m.box(0, 0, 0, 0.018, 0.34, 0.018, 'base'));
  m.at([0, 0.35, -0.07], { rotX: 0.75 }, () => m.box(0, 0, 0, 0.018, 0.27, 0.018, 'base'));
  m.cyl(0, 0.35, -0.07, 0.016, 0.016, 0.016, 8, 'steel', { axis: 'y' });
  m.at([0, 0.56, 0.12], { rotX: 0.35 }, () => {
    m.cyl(0, -0.09, 0, 0.11, 0.05, 0.1, 14, 'brass');
    m.cyl(0, 0.01, 0, 0.05, 0.03, 0.03, 10, 'brassDark');
    m.part('bulb', () => m.sphere(0, -0.075, 0, 0.032, 6, 10, 'glowWarm', 'glow'));
  });
});
make('phone', (m) => {
  m.taper(0, 0, 0, 0.22, 0.17, 0.05, 0.17, 0.12, 'phone');
  m.taper(0, 0.05, -0.025, 0.17, 0.12, 0.012, 0.15, 0.1, 'phoneDark');
  m.cyl(0, 0.05, 0.03, 0.042, 0.038, 0.012, 14, 'phoneDark');
  m.cyl(0, 0.062, 0.03, 0.03, 0.03, 0.002, 12, 'paper');
  [-0.075, 0.075].forEach((x) => m.box(x, 0.062, -0.025, 0.018, 0.018, 0.04, 'phoneDark'));
  m.box(-0.13, 0.002, -0.03, 0.05, 0.008, 0.008, 'cord');
});
make('phone_handset', (m) => {
  // Origin between the two cradles.
  m.box(0, 0, 0, 0.17, 0.024, 0.038, 'phone');
  [-0.085, 0.085].forEach((x) => { m.taper(x, -0.008, 0, 0.044, 0.05, 0.034, 0.036, 0.036, 'phoneDark'); m.cyl(x, -0.009, 0, 0.018, 0.018, 0.002, 8, 'nearBlack'); });
});
make('envelope', (m) => {
  m.box(0, 0, 0, 0.18, 0.012, 0.11, 'envelope');
  const y = 0.0122;
  m.flat([[-0.09, y, -0.055], [0.09, y, -0.055], [0, y, 0.015]], 'envelopeDark', [0, 1, 0]);
  m.cyl(0, y, 0.008, 0.016, 0.014, 0.004, 10, 'seal');
});
make('shredder', (m) => {
  // A desk-top shredder over its own little bin.
  m.box(0, 0, 0, 0.15, 0.035, 0.1, 'steelDark');
  m.box(0, 0.035, 0, 0.17, 0.035, 0.11, 'nearBlack');
  m.box(0, 0.07, 0, 0.13, 0.002, 0.012, 'black');
  m.box(0.055, 0.07, 0.04, 0.03, 0.002, 0.015, 'ledDim');
  m.box(0, 0.002, 0.051, 0.12, 0.022, 0.002, 'nearBlack');
});
make('switch_base', (m) => {
  m.box(0, 0, 0, 0.14, 0.022, 0.1, 'nearBlack');
  m.box(0, 0.022, 0, 0.05, 0.006, 0.07, 'black');
  m.part('lamp_on_air', () => m.box(0.05, 0.022, 0.03, 0.014, 0.004, 0.014, 'ledRed', 'glow'));
  m.part('lamp_off_air', () => m.box(-0.05, 0.022, 0.03, 0.014, 0.004, 0.014, 'ledDim'));
});
make('switch_lever', (m) => {
  // Origin at the pivot. Rotate around X: about -0.6 off air, +0.6 on air.
  m.box(0, 0, 0, 0.018, 0.075, 0.018, 'steel');
  m.cyl(0, 0.07, 0, 0.018, 0.016, 0.022, 10, 'white');
});
make('card_reader', (m) => {
  m.box(0, 0, 0, 0.19, 0.07, 0.13, 'metalDark');
  m.taper(0, 0.07, 0, 0.19, 0.13, 0.012, 0.17, 0.11, 'metal');
  m.box(0, 0.082, -0.01, 0.15, 0.002, 0.012, 'black');
  m.taper(0, 0.084, -0.03, 0.15, 0.02, 0.05, 0.15, 0.03, 'metalLight', undefined, -0.01); // Back rest for the card going in.
  m.box(-0.055, 0.015, 0.066, 0.1, 0.03, 0.002, 'nearBlack');
});
// A small wooden rack under the monitor's chin, for the special cards. Cards lean back on its rail; poses come from code.
make('card_rack', (m) => {
  m.box(0, 0, 0, 0.27, 0.012, 0.06, 'deskEdge');
  m.box(0, 0.012, 0.022, 0.26, 0.008, 0.008, 'deskDark'); // Front lip the cards stand on.
  m.at([0, 0.012, -0.018], { rotX: -0.42 }, () => m.box(0, 0, 0, 0.26, 0.026, 0.006, 'deskDark')); // Back rail, low so it never reads as a block.
  m.box(0, 0.003, 0.031, 0.08, 0.008, 0.001, 'brass'); // Name plate.
});
make('card_reader_lamp', (m) => m.box(0, 0, 0, 0.022, 0.012, 0.01, 'ledRed', 'glow'));
make('clock', (m) => {
  m.box(0, 0, 0, 0.26, 0.15, 0.1, 'nearBlack');
  m.box(0, 0.15, 0, 0.1, 0.012, 0.03, 'tvLight');
  m.box(0, 0.015, 0.05, 0.24, 0.125, 0.002, 'black');
  m.part('face', () => m.plane(0, 0.077, 0.052, 0.23, 0.12, 'clock_face'));
});
make('mug', (m) => {
  m.cyl(0, 0, 0, 0.04, 0.042, 0.09, 12, 'mug');
  m.cyl(0, 0.0905, 0, 0.036, 0.036, 0.0005, 12, 'coffee');
  m.box(0.05, 0.025, 0, 0.01, 0.04, 0.012, 'mug');
  m.box(0.044, 0.06, 0, 0.018, 0.01, 0.012, 'mug'); m.box(0.044, 0.018, 0, 0.018, 0.01, 0.012, 'mug');
});
make('side_table', (m) => {
  // Long side along Z, so its three things spread left to right from the chair.
  const W = 0.5, D = 1.0, H = 0.8;
  m.box(0, H - 0.03, 0, W, 0.03, D, 'desk');
  m.box(0, H - 0.05, 0, W - 0.04, 0.02, D - 0.04, 'deskDark');
  for (const [x, z] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) m.box(x * (W / 2 - 0.03), 0, z * (D / 2 - 0.03), 0.04, H - 0.05, 0.04, 'leg');
  m.box(0, 0.2, 0, W - 0.06, 0.02, D - 0.06, 'deskDark'); // Lower shelf.
  m.box(0.05, 0.22, -0.2, 0.3, 0.08, 0.22, 'paper'); // Paper waiting for the shredder.
  m.box(0, 0.22, 0.22, 0.32, 0.18, 0.24, 'envelopeDark'); // A box of envelopes.
});
// A punch card with its clipped corner. Hand poses come from code.
make('card', (m) => {
  const w = 0.125, h = 0.172, c = 0.024;
  const pts = [[-w / 2, -h / 2], [w / 2, -h / 2], [w / 2, h / 2], [-w / 2 + c, h / 2], [-w / 2, h / 2 - c]];
  const P = pts.map(([x, y]) => [x, y, 0]);
  m.face(P, 'card_face', pts.map(([x, y]) => [x / w + 0.5, y / h + 0.5]), () => [0, 0, 1]);
  m.face([...P].reverse(), 'palette', pts.map(() => swatch('envelopeDark')), () => [0, 0, -1]);
});
// Label plates. One part per label, UVs on the lit row of plates.png. The dim row sits directly below.
make('desk_plates', (m) => {
  const row = (i) => [0, 1 - (i * 2 + 1) * PLATE_H / (PLATE_H * PLATES.length * 2), 1, 1 - (i * 2) * PLATE_H / (PLATE_H * PLATES.length * 2)];
  // Each plate leans on a small wedge, high enough to clear the top edge of the card hand from the chair.
  const place = (label, x, z, w, h) => m.part(`plate_${label.replace(/ /g, '_')}`, () => {
    m.taper(x, 0, z - 0.012, w, 0.05, 0.03, w, 0.016, 'nearBlack', undefined, -0.012);
    m.at([x, 0.04, z], { rotX: -0.5 }, () => m.plane(0, 0, 0.001, w, h, 'plates', { uv: row(PLATES.indexOf(label)) }));
  });
  // Desk actions are explained on the staff notice, so only the controls keep a label.
  // Two matching clusters: cards on the left (reader, eject), the show on the right (switch, red button).
  place('GO LIVE', 0.56, 0.005, 0.15, 0.025);
  place('EJECT', -0.52, 0.2, 0.12, 0.02);
  place('ROLL IT', 0.52, 0.3, 0.17, 0.032);
});

// ---- Where things go. Same positions as desk.html: right-handed, player looks along -Z ----
const DESK_TOP = 0.773;
const TABLE_TOP = 0.8;
const Place = (model, pos, rotY = 0) => ({ model, pos, rotY });
const layout = {
  items: [
    Place('room_shell', [0, 0, 0]),
    Place('ceiling_light', [0, RH, -1.6]),
    Place('window', [-2.2, 1.75, -3.5]),
    ...[0, 1, 2].map((i) => Place('cabinet', [1.7 + i * 0.72, 0, -3.18])),
    Place('poster', [3.2, 1.7, -3.495]),
    Place('door', [2.2, 0, 3.48], Math.PI),
    Place('desk', [0, 0, -2.9]),
    Place('monitor', [0, DESK_TOP, -2.85]),
    Place('tv', [-4, 1.85, -0.8], Math.PI / 2),
    Place('machine_base', [3.1, 0, -1.4], -Math.PI / 2),
    Place('machine_dome', [3.1, 0, -1.4], -Math.PI / 2),
    // Nothing sits in front of the monitor except the card hand and the low special-card rack, so the board is never covered.
    // Left of the monitor: the card reader, which also prints the week's slip, with eject in front. Mug tucked behind.
    Place('card_reader', [-0.56, DESK_TOP, -3.0]),
    Place('card_reader_lamp', [-0.49, DESK_TOP + 0.05, -2.934]),
    Place('undo_button_base', [-0.52, DESK_TOP, -2.8]),
    Place('undo_button_cap', [-0.52, DESK_TOP + 0.018, -2.8]),
    Place('mug', [-0.78, DESK_TOP, -3.08]),
    Place('card_rack', [-0.3, DESK_TOP, -2.885]),
    // Right of the monitor, mirroring the left: the go-live switch with the red button in front, the on-air clock behind.
    Place('switch_base', [0.56, DESK_TOP, -2.97]),
    Place('switch_lever', [0.56, DESK_TOP + 0.028, -2.97]),
    Place('big_button_base', [0.52, DESK_TOP, -2.72]),
    Place('big_button_cap', [0.52, DESK_TOP + 0.015, -2.72]),
    Place('clock', [0.72, DESK_TOP, -3.22], -0.35),
    // Pushed to the desk's corner and turned inward, so it frames the calendar rather than covering it.
    Place('lamp', [-0.88, DESK_TOP, -2.72], 1.2),
    // Wall paper, in the gaps either side of the monitor. Newspaper beside the TV, where you look at a result.
    Place('calendar', [-0.93, 0.87, -3.475]),
    Place('memo', [0.93, 0.87, -3.475]),
    Place('newspaper', [-3.975, 1.4, -1.98], Math.PI / 2),
    Place('desk_plates', [0, DESK_TOP, -2.9]),
    // Side table between the desk and the ball machine, seen when you look right. The three desk actions, facing the chair,
    // kept left of the machine's number window.
    Place('side_table', [1.75, 0, -2.25]),
    Place('lamp', [1.85, TABLE_TOP, -2.65]),
    Place('shredder', [1.75, TABLE_TOP, -2.46], -Math.PI / 2),
    Place('envelope', [1.75, TABLE_TOP, -2.17], -Math.PI / 2),
    Place('phone', [1.75, TABLE_TOP, -1.9], -Math.PI / 2),
    Place('phone_handset', [1.775, TABLE_TOP + 0.084, -1.9], -Math.PI / 2),
    ...WALL_ART.map(([n, , , , pos, rotY]) => Place(n, pos, rotY)),
  ],
  // Teletext room, with a hint of the monitor's pink. Values from CHOSEN in desk.html.
  lights: {
    hemi: { sky: '#8fa0c8', ground: '#2a2018', intensity: 0.9 },
    sun: { color: '#fff1d6', intensity: 1.5, pos: [2.5, 2.8, 1], target: [0, 0.7, -2.6] },
    lamp: { color: '#ffb66b', intensity: 3, range: 5, pos: [-0.77, 1.25, -2.68] },
    glow: { color: '#ff50b0', intensity: 0.2, range: 2.5, pos: [0, 1.05, -2.6] },
    ceiling: { color: '#e8f0e0', intensity: 7, range: 9, pos: [0, 2.85, -1.6] },
    table: { color: '#ffb66b', intensity: 1.5, range: 2.5, pos: [1.85, 1.3, -2.53] }, // Side-table lamp. // Keeps the TV and machine readable.
  },
  camera: { fov: 50, eye: [0, 1.32, -1.92], lookMonitor: [0, 0.92, -3], lookTV: [-3.85, 1.85, -0.8], lookMachine: [3.1, 1.1, -1.4], lean: [0, 1.06, -2.4] },
  hand: { z: -2.66, y: 0.8, tiltX: -Math.PI / 2 + 0.25, step: 0.145 },
};
const MATERIALS = {
  palette: 'Lit, palette.png', glow: 'Unlit, palette.png', glass: 'Lit transparent', screen_monitor: 'CRT shader', screen_tv: 'CRT shader',
  machine_face: 'Unlit, runtime', clock_face: 'Unlit, runtime', window_night: 'Unlit', poster: 'Lit', calendar: 'Lit', memo: 'Lit', newspaper: 'Lit',
  plates: 'Unlit', card_face: 'Unlit', ball_numbers: 'Lit',
  ...Object.fromEntries(WALL_ART.map(([n]) => [n, 'Lit, Walls/' + n + '.png'])),
};

// ---- Write ----
// Overwrites in place, so Unity's .meta files and GUIDs survive a rebuild. A renamed asset leaves its old file behind.
mkdirSync(join(ROOT, 'Models'), { recursive: true });
const { fontChars } = buildTextures(ROOT, { colors: COLORS, size: SIZE, cell: CELL });
writeFileSync(join(ROOT, 'Models', 'no_winners.mtl'), Object.keys(MATERIALS).map((n) => `newmtl ${n}\nKd 0.8 0.8 0.8\n`).join('\n'));
let tris = 0;
for (const m of Object.values(models)) {
  writeFileSync(join(ROOT, 'Models', `${m.name}.obj`), m.obj());
  tris += m.faces.reduce((s, f) => s + f.p.length - 2, 0);
}
writeFileSync(join(ROOT, 'layout.json'), JSON.stringify({ ...layout, materials: MATERIALS, cards: CARDS, plates: PLATES, plateSize: [PLATE_W, PLATE_H], fontChars: fontChars.join('') }, null, 1));
console.log(`${Object.keys(models).length} models, ${tris} triangles`);
