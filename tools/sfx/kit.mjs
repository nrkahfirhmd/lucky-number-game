// Instruments, voices, crowds and recorded sources for the No Winners sound effects, built on dsp.mjs.
import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import {
  SR, len, mtof, clamp, osc, noise, dust, hit, env, adsr, curve, mul, mix, lp, hp, bp, peak, onepole, lag, drive,
  reverb, Track, blep, toSt, fade, slice, peakOf,
} from './dsp.mjs';

const TAU = Math.PI * 2;

// ---------- recorded sources ----------

const SOURCES = fileURLToPath(new URL('./sources/', import.meta.url));

export const has = name => existsSync(SOURCES + name + '.ogg');

// The first d seconds of a recording, starting just before its first transient.
export function cue(name, d, { thresholdDb = -24, fout = 0.08 } = {}) {
  const x = src(name), p = peakOf(x) * 10 ** (thresholdDb / 20);
  let i = 0;
  while (i < x.L.length && Math.abs(x.L[i]) < p && Math.abs(x.R[i]) < p) i++;
  const from = Math.max(0, i / SR - 0.015);
  return fade(slice(x, from, from + d), 0.002, fout);
}

// Decode part of a CC0 recording from sources/ (see sources/CREDITS.md) to stereo at SR, via ffmpeg.
// pitch above 1 plays it faster and higher, like a tape.
export function src(name, from = 0, to = null, { pitch = 1, fin = 0.004, fout = 0.03 } = {}) {
  const af = [`atrim=start=${from}${to == null ? '' : `:end=${to}`}`, 'asetpts=PTS-STARTPTS'];
  if (pitch !== 1) af.push(`aresample=${SR}`, `asetrate=${Math.round(SR * pitch)}`);
  af.push(`aresample=${SR}`);
  const raw = execFileSync('ffmpeg', ['-v', 'error', '-i', SOURCES + name + '.ogg', '-af', af.join(','), '-ac', '2', '-f', 'f32le', '-'], { maxBuffer: 1 << 29 });
  const x = new Float32Array(raw.buffer.slice(raw.byteOffset, raw.byteOffset + raw.length)), n = x.length / 2;
  const L = new Float32Array(n), R = new Float32Array(n);
  for (let i = 0; i < n; i++) { L[i] = x[2 * i]; R[i] = x[2 * i + 1]; }
  return fade({ L, R }, fin, fout);
}

// ---------- rooms and speakers ----------

export const office = (x, wet = 0.07) => reverb(x, { size: 0.32, damp: 0.6, wet, tail: 0.45, width: 0.6 });
export const studio = (x, wet = 0.16) => reverb(x, { size: 0.55, damp: 0.35, wet, tail: 1.2, width: 0.9, pre: 0.015 });
export const hall = (x, wet = 0.24) => reverb(x, { size: 0.68, damp: 0.3, wet, tail: 1.6, width: 1, pre: 0.02 });

// A 1980s set speaker in a drab office: no deep bass, soft top, a little cabinet bump and grit.
export function tv(x, grit = 1.25) {
  let y = hp(x, 115, 0.6);
  y = peak(y, 420, 1.1, 1.5);
  y = peak(y, 1900, 0.8, 2.5);
  y = lp(y, 8800, 0.6);
  return drive(y, grit);
}

// Telephone earpiece: narrow band, lots of grit, a bit of line hiss.
export function phoneLine(x, r) {
  let y = hp(x, 340, 0.9);
  y = lp(y, 3200, 0.9);
  y = peak(y, 1500, 1.2, 4);
  y = drive(mul(y, 2.2), 1.8);
  return mix(y, mul(hp(noise(y.length / SR, r), 2000), 0.012));
}

// ---------- drums and percussion ----------

export function kick(r, { f0 = 150, f1 = 48, tau = 0.24, click = 0.4 } = {}) {
  const d = tau * 5;
  const body = mul(osc('sine', t => f1 + (f0 - f1) * Math.exp(-t / 0.035), d), env(d, { a: 0.001, tau }));
  return mix(body, mul(hit({ r, modes: [[3000, 0.002, 0.3]], noiseAmp: click, noiseF: 2500, noiseTau: 0.002 }), 1));
}

export function snare(r, { tone = 190, snap = 1, tau = 0.13 } = {}) {
  const d = tau * 5;
  const t1 = hit({ r, modes: [[tone, 0.05, 0.7], [tone * 1.7, 0.03, 0.35]] });
  const wires = hp(bp(mul(noise(d, r), env(d, { a: 0.0005, tau })), 4200, 0.45), 900);
  return mix(t1, mul(wires, 0.9 * snap));
}

export function tom(r, f = 140, tau = 0.25) {
  const d = tau * 5;
  const body = mul(osc('sine', t => f * (1 + 0.35 * Math.exp(-t / 0.03)), d), env(d, { a: 0.001, tau }));
  return mix(body, mul(lp(mul(noise(0.05, r), env(0.05, { tau: 0.008 })), 2500), 0.25));
}

// Inharmonic square bank, the core of cymbals and hats.
function metal(d, k = 1) {
  const fs = [205.3, 304.4, 369.6, 522.7, 540, 800].map(f => f * k);
  return mix(...fs.map(f => mul(osc('square', f, d, { phase: f % 1 }), 1 / 6)));
}

export function hat(r, open = false) {
  const tau = open ? 0.28 : 0.035, d = tau * 5;
  const m = hp(mix(metal(d, 1.6), mul(noise(d, r), 0.5)), 7000, 0.8);
  return mul(m, env(d, { a: 0.0005, tau }));
}

export function crash(r, { d = 2.6, tau = 0.9, swell = 0 } = {}) {
  const m = hp(mix(mul(metal(d, 2.2), 0.7), noise(d, r)), 4200, 0.6);
  const e = swell ? curve([[0, 0], [swell, 1, 2.2], [swell + 0.001, 1], [d, 0.02, 0.4]]) : env(d, { a: 0.002, tau });
  return mul(peak(m, 7500, 0.8, 3), e);
}

export function timpani(r, f, tau = 1.0, vel = 1) {
  const modes = [[f, tau, 1], [f * 1.5, tau * 0.7, 0.5], [f * 1.98, tau * 0.5, 0.35], [f * 2.44, tau * 0.35, 0.2], [f * 2.9, tau * 0.25, 0.1]];
  const thump = mul(lp(mul(noise(0.08, r), env(0.08, { tau: 0.012 })), 600), 0.6);
  return mul(mix(hit({ r, modes }), thump), vel);
}

// Repeated strokes with a crescendo, for rolls.
export function roll(r, stroke, d, { rate = 22, from = 0.2, to = 1 } = {}) {
  const t = new Track();
  for (let s = 0; s < d; s += 1 / rate) t.add(stroke(), s + r.range(-0.004, 0.004), (from + (to - from) * (s / d) ** 1.5) * r.range(0.85, 1));
  return toSt(t.out()).L;
}

// ---------- pitched instruments ----------

// Bright game-show brass: three detuned saws, a filter that opens on the attack, a pitch scoop and late vibrato.
export function brass(f, d, { vel = 1, bright = 1, vib = 0.006, scoop = 0.025, rel = 0.14, wah = null, detune = 1 } = {}) {
  const total = d + rel;
  const pitch = t => f * (1 - scoop * Math.exp(-t / 0.03)) * (1 + vib * Math.sin(TAU * 5.6 * t) * clamp((t - 0.16) / 0.25, 0, 1));
  const saws = [-0.0045, 0, 0.0052].map(c => osc('saw', t => pitch(t) * (1 + c * detune), total, { phase: Math.abs(c) * 97 % 1 }));
  const src = mul(mix(...saws), 1 / 3);
  const cut = t => {
    const open = t < 0.045 ? t / 0.045 : 0.72 + 0.28 * Math.exp(-(t - 0.045) / 0.18);
    return clamp((f * 1.3 + f * (2.2 + 5.5 * vel * bright) * open) * (wah ? wah(t) : 1), 80, 16000);
  };
  const y = lp(src, cut, 0.9);
  return drive(mul(y, adsr(total, 0.016, 0.18, 0.78, rel)), 1.4);
}

export function chord(inst, notes, d, opts) {
  return mix(...notes.map((m, i) => mul(inst(mtof(m), d, opts), 1 / Math.sqrt(notes.length) * (i === notes.length - 1 ? 1.1 : 1))));
}

export function marimba(r, f, tau = 0.32) {
  return hit({ r, modes: [[f, tau, 1], [f * 3.93, tau * 0.22, 0.32], [f * 9.2, tau * 0.06, 0.1]], noiseAmp: 0.12, noiseF: 2600, noiseTau: 0.0015 });
}

export function glock(r, f, tau = 0.6) {
  return hit({ r, modes: [[f, tau, 1], [f * 2.76, tau * 0.32, 0.35], [f * 5.4, tau * 0.11, 0.16], [f * 8.93, tau * 0.05, 0.08]], noiseAmp: 0.08, noiseF: 7000, noiseTau: 0.001 });
}

export function bass(f, d, { bright = 1 } = {}) {
  const src = mix(osc('saw', f, d), mul(osc('sine', f / 2, d), 0.6));
  const y = lp(src, t => f * (1.5 + 6 * bright * Math.exp(-t / 0.08)), 1.1);
  return mul(y, adsr(d, 0.004, 0.25, 0.55, 0.06));
}

export function coin(r, pitch = 1, tau = 0.11) {
  const f = 4300 * pitch * r.range(0.96, 1.04);
  return hit({ r, modes: [[f, tau, 1], [f * 2.31, tau * 0.6, 0.55], [f * 3.87, tau * 0.35, 0.3], [f * 0.62, tau * 0.4, 0.2]], noiseAmp: 0.35, noiseF: 8000, noiseTau: 0.0012 });
}

// ---------- voices ----------

const V = { a: [750, 1150, 2500], e: [500, 1800, 2500], i: [320, 2250, 3000], o: [520, 880, 2450], u: [340, 850, 2300], er: [480, 1350, 1700], ae: [680, 1700, 2450], uh: [600, 1200, 2450] };
const AP = { l: [360, 1100, 2700], r: [400, 1150, 1550], w: [300, 650, 2200], y: [280, 2150, 3000], m: [260, 1000, 2300], n: [260, 1600, 2550] };

const seg = (d, o = {}) => ({ d, F: null, v: 0, b: 0, fr: 0, ff: 4000, fq: 1.5, nas: 0, acc: 0, ...o });

function consonant(c, rate) {
  const s = x => x / rate;
  switch (c) {
    case 's': return [seg(s(0.085), { fr: 0.3, ff: 6500, fq: 2.2 })];
    case 'sh': return [seg(s(0.09), { fr: 0.34, ff: 2800, fq: 2 })];
    case 'f': return [seg(s(0.07), { fr: 0.1, ff: 5200, fq: 0.7 })];
    case 'h': return [seg(s(0.055), { b: 0.55 })];
    case 'p': case 't': case 'k':
      return [seg(s(0.045)), seg(0.012, { fr: 0.5, ff: { p: 1100, t: 4300, k: 2500 }[c], fq: 1 }), seg(s(0.03), { b: 0.3 })];
    case 'b': case 'd': case 'g':
      return [seg(s(0.03), { nas: 0.12, F: AP.m }), seg(0.009, { fr: 0.25, ff: { b: 1000, d: 3800, g: 2300 }[c], fq: 1, v: 0.3 })];
    case 'm': case 'n': return [seg(s(0.06), { v: 0.25, nas: 0.5, F: AP[c] })];
    default: return [seg(s(0.05), { v: 0.7, F: AP[c] })];
  }
}

// Render a list of segments through a three-formant voice. pitch(u, accent, t) returns Hz, u is 0..1 through the line.
export function speak(segs, { r, pitch, female = false, whisper = false, rough = 0 }) {
  let next = V.uh;
  for (let k = segs.length - 1; k >= 0; k--) { if (segs[k].F) next = segs[k].F; else segs[k].F = next; }
  const n = segs.reduce((s, x) => s + len(x.d), 0);
  const tr = ['F1', 'F2', 'F3', 'v', 'b', 'fr', 'ff', 'nas', 'acc'].reduce((o, k) => ((o[k] = new Float32Array(n)), o), {});
  const fs = female ? [1.16, 1.17, 1.12] : [1, 1, 1];
  let o = 0;
  for (const s of segs) {
    const m = len(s.d);
    for (let i = o; i < o + m; i++) {
      tr.F1[i] = s.F[0] * fs[0]; tr.F2[i] = s.F[1] * fs[1]; tr.F3[i] = s.F[2] * fs[2];
      tr.v[i] = s.v; tr.b[i] = s.b; tr.fr[i] = s.fr; tr.ff[i] = s.ff; tr.nas[i] = s.nas; tr.acc[i] = s.acc;
    }
    o += m;
  }
  const F1 = lag(tr.F1, 0.018), F2 = lag(tr.F2, 0.02), F3 = lag(tr.F3, 0.022);
  const vA = lag(tr.v, 0.009), bA = lag(tr.b, 0.008), frA = lag(tr.fr, 0.003), ffA = lag(tr.ff, 0.002), nA = lag(tr.nas, 0.01), acc = lag(tr.acc, 0.06);

  const f0 = new Float32Array(n);
  for (let i = 0; i < n; i++) f0[i] = pitch(i / n, acc[i], i / SR);
  const F0 = lag(f0, 0.03);

  const src = new Float32Array(n);
  let ph = 0, jit = 0;
  for (let i = 0; i < n; i++) {
    jit += (r() - 0.5) * 0.003; jit *= 0.996;
    const f = F0[i] * (1 + jit + rough * (r() - 0.5) * 0.12), dt = Math.min(f / SR, 0.45);
    ph += dt; ph -= Math.floor(ph);
    src[i] = 2 * ph - 1 - blep(ph, dt);
  }
  const glot = onepole(src, female ? 1800 : 1400);
  const asp = hp(noise(n / SR, r), 500);
  const exc = new Float32Array(n);
  for (let i = 0; i < n; i++) {
    exc[i] = whisper ? asp[i] * (vA[i] + bA[i]) * 0.35 : glot[i] * vA[i] + asp[i] * (bA[i] + 0.035 * vA[i]) * 0.35;
  }
  const bw = female ? [100, 120, 170] : [85, 105, 150];
  const q = (F, w) => { const a = new Float32Array(n); for (let i = 0; i < n; i++) a[i] = F[i] / w; return a; };
  const body = mix(bp(exc, F1, q(F1, bw[0])), mul(bp(exc, F2, q(F2, bw[1])), 0.62), mul(bp(exc, F3, q(F3, bw[2])), 0.32));
  const nasal = mul(lp(mul(glot, nA), 330), 0.9);
  const fric = mul(bp(noise(n / SR, r), ffA, 1.8), frA);
  return mix(mul(body, 3.2), whisper ? null : nasal, fric);
}

const ONSETS = ['', '', 'b', 'd', 'g', 'p', 't', 'k', 'm', 'n', 'l', 'r', 's', 'sh', 'f', 'h', 'w', 'y'];
const CODAS = ['n', 'm', 's', 't', 'l', 'r', 'k'];
const VOWS = ['a', 'e', 'i', 'o', 'u', 'uh', 'ae', 'er', 'a', 'e', 'o'];

// Gibberish speech. tone: statement, question, excited, punchline, hesitant.
export function voice({ r, words = 3, syl = [1, 3], f0 = 120, female = false, rate = 1, tone = 'statement', whisper = false, energy = 1, gap = [0.04, 0.12], hesitate = 0, rough = 0 }) {
  const segs = [seg(0.03)];
  for (let w = 0; w < words; w++) {
    const k = r.int(syl[0], syl[1]), stressAt = r.int(0, k - 1);
    for (let s = 0; s < k; s++) {
      const stressed = s === stressAt;
      if (r.chance(0.8)) segs.push(...consonant(r.pick(ONSETS) || 'h', rate));
      let d = (r.range(0.085, 0.15) / rate) * (stressed ? 1.35 : 1), vw = r.pick(VOWS);
      if (hesitate && s === k - 1 && r.chance(hesitate)) { d *= 3.2; vw = 'uh'; }
      segs.push(seg(d, { v: energy * (stressed ? 1 : 0.78), F: V[vw], b: 0.03, acc: stressed ? 1 : 0 }));
      if (r.chance(0.22)) segs.push(...consonant(r.pick(CODAS), rate));
    }
    const pause = hesitate && r.chance(hesitate * 0.6) ? r.range(0.25, 0.45) : r.range(gap[0], gap[1]) / rate;
    if (w < words - 1) segs.push(seg(pause));
  }
  segs.push(seg(0.05));
  const shape = {
    statement: (u, a) => (1.1 - 0.22 * u) * (1 + 0.18 * a),
    question: (u, a) => (1.05 - 0.15 * u) * (1 + 0.15 * a) * (u > 0.72 ? 1 + 0.5 * ((u - 0.72) / 0.28) ** 1.5 : 1),
    excited: (u, a) => (1.28 - 0.18 * u) * (1 + 0.32 * a),
    punchline: (u, a) => (u < 0.7 ? 1 + 0.25 * u : 1.18 - 0.5 * (u - 0.7)) * (1 + 0.28 * a),
    hesitant: (u, a) => (1.02 - 0.08 * u) * (1 + 0.1 * a) * (u > 0.8 ? 1 + 0.3 * ((u - 0.8) / 0.2) : 1),
  }[tone];
  const vib = r.range(4.5, 6);
  return speak(segs, { r, female, whisper, rough, pitch: (u, a, t) => f0 * shape(u, a) * (1 + 0.006 * Math.sin(TAU * vib * t)) });
}

// One crowd member's shout: yeah, woo, hey or oh, rising then falling.
export function shout({ r, d = 0.8, f0 = 200, female = false, energy = 1 }) {
  const kind = r.pick(['yeah', 'woo', 'hey', 'oh', 'yeah', 'ah']);
  const S = {
    yeah: [seg(0.06, { v: 0.6, F: AP.y }), seg(d * 0.45, { v: energy, F: V.e }), seg(d * 0.55, { v: energy, F: V.ae })],
    woo: [seg(0.07, { v: 0.6, F: AP.w }), seg(d, { v: energy, F: V.u })],
    hey: [seg(0.05, { b: 0.6 }), seg(d * 0.6, { v: energy, F: V.e }), seg(d * 0.4, { v: energy * 0.9, F: V.i })],
    oh: [seg(d, { v: energy, F: V.o })],
    ah: [seg(0.04, { b: 0.4 }), seg(d, { v: energy, F: V.a })],
  }[kind];
  S.push(seg(0.04));
  const lift = kind === 'woo' ? 1.45 : 1.2, vib = r.range(5, 7);
  return speak(S, {
    r, female, rough: 0.15,
    pitch: (u, a, t) => f0 * (u < 0.3 ? 0.85 + (lift - 0.85) * (u / 0.3) : lift - (lift - 0.8) * ((u - 0.3) / 0.7)) * (1 + 0.012 * Math.sin(TAU * vib * t)),
  });
}

export function cough(r, { f0 = 140, female = false } = {}) {
  const one = s => [seg(0.02), seg(0.014, { fr: 0.8 * s, ff: 1500, fq: 0.7 }), seg(0.12 * s, { b: 0.9 * s, v: 0.22 * s, F: V.a }), seg(0.02)];
  return speak([...one(1), seg(0.07), ...one(0.7)], { r, female, rough: 1, pitch: u => f0 * (1.1 - 0.3 * u) });
}

// ---------- crowds ----------

export function applause(r, d, { people = 24, shape = () => 1, rate = [3.4, 5.6], tight = 1 } = {}) {
  const protos = Array.from({ length: 14 }, () => {
    const f = r.range(900, 2700), body = r.range(300, 600);
    const n = mix(bp(noise(0.045, r), f, r.range(0.8, 2)), mul(lp(noise(0.045, r), body), 0.4));
    return mul(n, env(0.045, { a: 0.0006, tau: r.range(0.004, 0.009) }));
  });
  const t = new Track();
  for (let p = 0; p < people; p++) {
    const hands = [r.pick(protos), r.pick(protos)], per = 1 / r.range(rate[0], rate[1]), g = r.range(0.35, 1), pan = r.range(-0.85, 0.85);
    for (let s = r.range(0, 0.25); s < d; s += per * (1 + (r() - 0.5) * 0.14 / tight)) {
      if (r() < shape(s)) t.add(r.pick(hands), s, g * r.range(0.6, 1) * shape(s), pan);
    }
  }
  return t.out();
}

export function cheer(r, d, { people = 34, shape = () => 1, whistles = 3, roar = 1 } = {}) {
  const t = new Track();
  for (let p = 0; p < people; p++) {
    const female = r.chance(0.45), f0 = female ? r.range(250, 420) : r.range(140, 250), pan = r.range(-0.85, 0.85), g = r.range(0.25, 1);
    for (let s = r.range(0, 0.45); s < d - 0.3;) {
      const sd = Math.min(r.range(0.4, 1.3), d - s);
      if (sd < 0.25) break;
      t.add(shout({ r, d: sd, f0: f0 * r.range(0.92, 1.1), female, energy: 1 }), s, g * shape(s), pan);
      s += sd + r.range(0.08, 0.45);
    }
  }
  for (let w = 0; w < whistles; w++) {
    const s = r.range(0.2, d * 0.6), wd = r.range(0.4, 0.9), f = r.range(1900, 2600), up = r.range(1.15, 1.45);
    const wh = mul(osc('sine', u => f * (1 + (up - 1) * Math.min(1, u / (wd * 0.4))) * (1 + 0.01 * Math.sin(TAU * 6 * u)), wd), curve([[0, 0], [0.05, 1], [wd * 0.7, 0.8], [wd, 0]]));
    t.add(mix(wh, mul(bp(noise(wd, r), f * 1.2, 2), 0.1)), s, 0.12 * shape(s), r.range(-0.7, 0.7));
  }
  const bed = mul(bp(noise(d, r, 'pink'), 900, 0.45), t2 => 0.25 * roar * shape(t2));
  t.add(bed, 0, 1, -0.3).add(mul(bp(noise(d, r, 'pink'), 1100, 0.45), t2 => 0.25 * roar * shape(t2)), 0, 1, 0.3);
  return t.out();
}

export function gasp(r, { people = 26 } = {}) {
  const t = new Track();
  for (let p = 0; p < people; p++) {
    const female = r.chance(0.5), f0 = female ? r.range(230, 360) : r.range(120, 200), at = r.range(0, 0.12);
    const inhale = [seg(r.range(0.22, 0.35), { b: 0.9, F: V.a }), seg(0.04)];
    const ooh = [seg(r.range(0.5, 0.9), { v: r.range(0.4, 0.8), b: 0.15, F: V.o }), seg(0.25, { v: 0.4, F: V.u }), seg(0.05)];
    const line = speak([...inhale, ...ooh], { r, female, pitch: u => f0 * (1.15 - 0.35 * u) });
    t.add(line, at, r.range(0.35, 1), r.range(-0.85, 0.85));
  }
  return t.out();
}

export function murmur(r, d, { people = 9, level = 1 } = {}) {
  const t = new Track();
  for (let p = 0; p < people; p++) {
    const female = r.chance(0.5), f0 = female ? r.range(190, 280) : r.range(95, 150), pan = r.range(-0.8, 0.8);
    for (let s = r.range(0, 0.6); s < d;) {
      const line = voice({ r, words: r.int(2, 5), f0, female, rate: r.range(0.95, 1.2), energy: 0.7, tone: r.pick(['statement', 'question', 'statement']) });
      t.add(line, s, r.range(0.25, 0.6) * level, pan);
      s += line.length / SR + r.range(0.2, 1.2);
    }
  }
  return lp(t.out(), 2600, 0.7);
}
