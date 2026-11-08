// Small synthesis kit for the No Winners sound effects. No dependencies.
// Mono signals are Float32Array. Stereo signals are { L, R } of equal length.
// Parameters marked "param" take a number, a per-sample Float32Array, or a function of time in seconds.

export const SR = 44100;
const TAU = Math.PI * 2;
export const len = s => Math.max(1, Math.round(s * SR));
export const mtof = m => 440 * 2 ** ((m - 69) / 12);
export const dbToGain = d => 10 ** (d / 20);
export const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);

// ---------- randomness ----------

function hash(s) {
  let h = 2166136261;
  for (let i = 0; i < s.length; i++) h = Math.imul(h ^ s.charCodeAt(i), 16777619);
  return h >>> 0;
}

export function rng(seed) {
  let a = typeof seed === 'string' ? hash(seed) : seed >>> 0;
  const f = () => {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  f.range = (lo, hi) => lo + (hi - lo) * f();
  f.int = (lo, hi) => Math.floor(lo + (hi - lo + 1) * f());
  f.pick = arr => arr[Math.floor(f() * arr.length)];
  f.chance = p => f() < p;
  f.fork = tag => rng(hash(tag + ':' + Math.floor(f() * 1e9)));
  return f;
}

// ---------- plumbing ----------

function P(p) {
  if (typeof p === 'function') return i => p(i / SR);
  if (p instanceof Float32Array) return i => p[i < p.length ? i : p.length - 1];
  return () => p;
}

const isSt = x => x && x.L instanceof Float32Array;
// Lift a mono function so it also runs per channel on stereo input.
const chan = fn => (x, ...a) => (isSt(x) ? { L: fn(x.L, ...a), R: fn(x.R, ...a) } : fn(x, ...a));

export const silence = dur => new Float32Array(len(dur));
export const dur = x => (isSt(x) ? x.L.length : x.length) / SR;

export const mul = chan((x, g) => {
  const out = new Float32Array(x.length), f = P(g);
  for (let i = 0; i < x.length; i++) out[i] = x[i] * f(i);
  return out;
});

export const pad = chan((x, before = 0, after = 0) => {
  const a = len(before) * (before > 0), out = new Float32Array(a + x.length + len(after) * (after > 0));
  out.set(x, a);
  return out;
});

export const reverse = chan(x => x.slice().reverse());

export const slice = chan((x, from = 0, to) => x.slice(Math.round(from * SR), to == null ? undefined : Math.round(to * SR)));

export const fade = chan((x, fin = 0.002, fout = 0.004) => {
  const out = x.slice(), a = Math.min(len(fin), out.length), b = Math.min(len(fout), out.length);
  for (let i = 0; i < a; i++) out[i] *= i / a;
  for (let i = 0; i < b; i++) out[out.length - 1 - i] *= i / b;
  return out;
});

export function mix(...xs) {
  const items = xs.filter(Boolean);
  if (items.some(isSt)) {
    const t = new Track();
    for (const x of items) t.add(x);
    return t.out();
  }
  const n = Math.max(...items.map(x => x.length)), out = new Float32Array(n);
  for (const x of items) for (let i = 0; i < x.length; i++) out[i] += x[i];
  return out;
}

export function concat(...xs) {
  const n = xs.reduce((s, x) => s + x.length, 0), out = new Float32Array(n);
  let o = 0;
  for (const x of xs) { out.set(x, o); o += x.length; }
  return out;
}

export const toMono = x => (isSt(x) ? mul(mix(x.L, x.R), 0.5) : x);
export const toSt = x => (isSt(x) ? x : { L: x.slice(), R: x.slice() });

// Stereo timeline. Mono sources are equal-power panned, stereo sources are balanced.
export class Track {
  constructor() { this.L = new Float32Array(SR); this.R = new Float32Array(SR); this.n = 0; }
  grow(n) {
    if (n <= this.L.length) return;
    const m = Math.max(n, Math.floor(this.L.length * 1.5));
    const L = new Float32Array(m), R = new Float32Array(m);
    L.set(this.L); R.set(this.R);
    this.L = L; this.R = R;
  }
  add(x, at = 0, g = 1, pan = 0) {
    let o = Math.round(at * SR), skip = 0;
    if (o < 0) { skip = -o; o = 0; }
    const st = isSt(x), n = (st ? x.L.length : x.length) - skip;
    if (n <= 0) return this;
    this.grow(o + n);
    let gl, gr;
    if (st) { gl = g * Math.min(1, 1 - pan); gr = g * Math.min(1, 1 + pan); }
    else { const a = ((clamp(pan, -1, 1) + 1) * Math.PI) / 4; gl = g * Math.cos(a) * Math.SQRT2; gr = g * Math.sin(a) * Math.SQRT2; }
    const sl = st ? x.L : x, sr = st ? x.R : x;
    for (let i = 0; i < n; i++) { this.L[o + i] += sl[skip + i] * gl; this.R[o + i] += sr[skip + i] * gr; }
    this.n = Math.max(this.n, o + n);
    return this;
  }
  out() { return { L: this.L.slice(0, this.n), R: this.R.slice(0, this.n) }; }
}

// ---------- envelopes ----------

// Breakpoints [[t, v], [t, v, curve], ...]. A curve above 1 eases in, below 1 eases out.
export function curve(points, d) {
  const n = len(d ?? points[points.length - 1][0]), out = new Float32Array(n);
  let k = 0;
  for (let i = 0; i < n; i++) {
    const t = i / SR;
    while (k < points.length - 2 && t >= points[k + 1][0]) k++;
    const p0 = points[k], p1 = points[k + 1] ?? p0;
    if (t <= p0[0]) { out[i] = p0[1]; continue; }
    if (t >= p1[0]) { out[i] = p1[1]; continue; }
    const u = (t - p0[0]) / (p1[0] - p0[0]);
    out[i] = p0[1] + (p1[1] - p0[1]) * u ** (p1[2] ?? 1);
  }
  return out;
}

// Linear attack, optional hold, exponential decay, optional linear release at the very end.
export function env(d, { a = 0.002, tau = 0.1, hold = 0, rel = 0 } = {}) {
  const n = len(d), out = new Float32Array(n), na = len(a), nh = len(hold) * (hold > 0), nr = len(rel) * (rel > 0);
  for (let i = 0; i < n; i++) {
    let v = i < na ? i / na : i < na + nh ? 1 : Math.exp(-(i - na - nh) / (tau * SR));
    if (nr && i > n - nr) v *= (n - i) / nr;
    out[i] = v;
  }
  return out;
}

export function adsr(d, a, dcy, s, r) {
  const n = len(d), out = new Float32Array(n), na = len(a), nr = len(r);
  for (let i = 0; i < n; i++) {
    let v = i < na ? i / na : s + (1 - s) * Math.exp(-(i - na) / ((dcy / 3) * SR));
    if (i > n - nr) v *= ((n - i) / nr) ** 2;
    out[i] = v;
  }
  return out;
}

// ---------- sources ----------

export function blep(t, dt) {
  if (t < dt) { t /= dt; return t + t - t * t - 1; }
  if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
  return 0;
}

// type: sine, saw, square, tri. freq is a param. pw is the square's pulse width.
export function osc(type, freq, d, { phase = 0, pw = 0.5 } = {}) {
  const n = len(d), out = new Float32Array(n), f = P(freq);
  let ph = phase;
  for (let i = 0; i < n; i++) {
    const dt = Math.min(f(i) / SR, 0.49);
    let v;
    if (type === 'sine') v = Math.sin(TAU * ph);
    else if (type === 'saw') v = 2 * ph - 1 - blep(ph, dt);
    else if (type === 'square') v = (ph < pw ? 1 : -1) + blep(ph, dt) - blep((ph + 1 - pw) % 1, dt);
    else v = 4 * Math.abs(ph - 0.5) - 1;
    out[i] = v;
    ph += dt;
    ph -= Math.floor(ph);
  }
  return out;
}

export function noise(d, r, color = 'white') {
  const n = len(d), out = new Float32Array(n);
  let b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0, br = 0;
  for (let i = 0; i < n; i++) {
    const w = r() * 2 - 1;
    if (color === 'pink') {
      b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.969 * b2 + w * 0.153852;
      b3 = 0.8665 * b3 + w * 0.3104856; b4 = 0.55 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.016898;
      out[i] = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362) * 0.11;
      b6 = w * 0.115926;
    } else if (color === 'brown') {
      br = (br + 0.02 * w) / 1.02;
      out[i] = br * 3.5;
    } else out[i] = w;
  }
  return out;
}

// Sparse random impulses, density per second as a param. Good for crackle and grit.
export function dust(d, r, density, { bipolar = true } = {}) {
  const n = len(d), out = new Float32Array(n), f = P(density);
  for (let i = 0; i < n; i++) if (r() < f(i) / SR) out[i] = (bipolar && r() < 0.5 ? -1 : 1) * (0.3 + 0.7 * r());
  return out;
}

// Struck object: a bank of decaying sine modes [[freq, tau, amp], ...] plus a filtered noise click.
export function hit({ modes = [], r, d, noiseAmp = 0, noiseTau = 0.003, noiseF = 3000, noiseQ = 0.8, pitch = 1, attack = 0.0002 }) {
  const longest = Math.max(noiseTau * 3, ...modes.map(m => m[1] * 5));
  const n = len(d ?? longest + 0.01), out = new Float32Array(n), na = Math.max(1, len(attack));
  for (const [f, tau, amp] of modes) {
    const ph = r ? r() * TAU : 0, w = (TAU * f * pitch) / SR, k = Math.exp(-1 / (tau * SR));
    let e = amp;
    for (let i = 0; i < n; i++) { out[i] += e * Math.sin(w * i + ph) * (i < na ? i / na : 1); e *= k; }
  }
  if (noiseAmp) {
    const nz = bp(mul(noise(n / SR, r), env(n / SR, { a: 0.0001, tau: noiseTau })), noiseF * pitch, noiseQ);
    for (let i = 0; i < n; i++) out[i] += nz[i] * noiseAmp;
  }
  return out;
}

// Excite a set of resonant modes with any signal (impulses, noise). Mode list as in hit().
export function resonate(x, modes) {
  const out = new Float32Array(x.length);
  for (const [f, tau, amp] of modes) {
    const y = biquad(x, 'bpk', f, Math.max(0.5, Math.PI * f * tau));
    for (let i = 0; i < x.length; i++) out[i] += y[i] * amp;
  }
  return out;
}

// Two-operator FM, for bells, coins and chimes.
export function fm(freq, d, { ratio = 1.4, index = 2, tau = 0.5, itau = 0.2, a = 0.001, r } = {}) {
  const n = len(d), out = new Float32Array(n), ph = r ? r() * TAU : 0, na = len(a);
  for (let i = 0; i < n; i++) {
    const t = i / SR, I = index * Math.exp(-t / itau);
    out[i] = Math.sin(TAU * freq * t + ph + I * Math.sin(TAU * freq * ratio * t)) * Math.exp(-t / tau) * (i < na ? i / na : 1);
  }
  return out;
}

// ---------- filters ----------

// RBJ biquads. type: lp, hp, bp (0 dB peak), bpk (constant skirt, for resonators), notch, peak, lowshelf, highshelf.
export const biquad = chan((x, type, freq, q = 0.707, gainDb = 0) => {
  const out = new Float32Array(x.length), f = P(freq), Q = P(q), A = 10 ** (gainDb / 40);
  let b0 = 0, b1 = 0, b2 = 0, a1 = 0, a2 = 0, x1 = 0, x2 = 0, y1 = 0, y2 = 0;
  const set = (fr, qq) => {
    const w = (TAU * clamp(fr, 10, SR * 0.48)) / SR, c = Math.cos(w), s = Math.sin(w), al = s / (2 * Math.max(qq, 0.05));
    let a0, B0, B1, B2, A1, A2;
    switch (type) {
      case 'lp': B0 = (1 - c) / 2; B1 = 1 - c; B2 = B0; a0 = 1 + al; A1 = -2 * c; A2 = 1 - al; break;
      case 'hp': B0 = (1 + c) / 2; B1 = -(1 + c); B2 = B0; a0 = 1 + al; A1 = -2 * c; A2 = 1 - al; break;
      case 'bp': B0 = al; B1 = 0; B2 = -al; a0 = 1 + al; A1 = -2 * c; A2 = 1 - al; break;
      case 'bpk': B0 = s / 2; B1 = 0; B2 = -s / 2; a0 = 1 + al; A1 = -2 * c; A2 = 1 - al; break;
      case 'notch': B0 = 1; B1 = -2 * c; B2 = 1; a0 = 1 + al; A1 = -2 * c; A2 = 1 - al; break;
      case 'peak': B0 = 1 + al * A; B1 = -2 * c; B2 = 1 - al * A; a0 = 1 + al / A; A1 = -2 * c; A2 = 1 - al / A; break;
      case 'lowshelf': {
        const sq = 2 * Math.sqrt(A) * al;
        B0 = A * (A + 1 - (A - 1) * c + sq); B1 = 2 * A * (A - 1 - (A + 1) * c); B2 = A * (A + 1 - (A - 1) * c - sq);
        a0 = A + 1 + (A - 1) * c + sq; A1 = -2 * (A - 1 + (A + 1) * c); A2 = A + 1 + (A - 1) * c - sq; break;
      }
      case 'highshelf': {
        const sq = 2 * Math.sqrt(A) * al;
        B0 = A * (A + 1 + (A - 1) * c + sq); B1 = -2 * A * (A - 1 + (A + 1) * c); B2 = A * (A + 1 + (A - 1) * c - sq);
        a0 = A + 1 - (A - 1) * c + sq; A1 = 2 * (A - 1 - (A + 1) * c); A2 = A + 1 - (A - 1) * c - sq; break;
      }
      default: throw new Error('filter type ' + type);
    }
    b0 = B0 / a0; b1 = B1 / a0; b2 = B2 / a0; a1 = A1 / a0; a2 = A2 / a0;
  };
  const fixed = typeof freq === 'number' && typeof q === 'number';
  if (fixed) set(freq, q);
  for (let i = 0; i < x.length; i++) {
    if (!fixed && (i & 15) === 0) set(f(i), Q(i));
    const y = b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
    x2 = x1; x1 = x[i]; y2 = y1; y1 = y;
    out[i] = y;
  }
  return out;
});

export const lp = (x, f, q = 0.707) => biquad(x, 'lp', f, q);
export const hp = (x, f, q = 0.707) => biquad(x, 'hp', f, q);
export const bp = (x, f, q = 1) => biquad(x, 'bp', f, q);
export const peak = (x, f, q, g) => biquad(x, 'peak', f, q, g);
export const lowshelf = (x, f, g) => biquad(x, 'lowshelf', f, 0.707, g);
export const highshelf = (x, f, g) => biquad(x, 'highshelf', f, 0.707, g);

export const onepole = chan((x, f, high = false) => {
  const out = new Float32Array(x.length), k = Math.exp((-TAU * f) / SR);
  let y = 0;
  for (let i = 0; i < x.length; i++) { y = x[i] + k * (y - x[i]); out[i] = high ? x[i] - y : y; }
  return out;
});

// Smooth a control signal with a one-pole lag of the given time constant.
export function lag(x, tau) {
  const out = new Float32Array(x.length), k = Math.exp(-1 / (tau * SR));
  let y = x[0] ?? 0;
  for (let i = 0; i < x.length; i++) { y = x[i] + k * (y - x[i]); out[i] = y; }
  return out;
}

export const drive = chan((x, k = 2) => {
  const out = new Float32Array(x.length), n = Math.tanh(k);
  for (let i = 0; i < x.length; i++) out[i] = Math.tanh(x[i] * k) / n;
  return out;
});

export const crush = chan((x, bits = 8, hold = 1) => {
  const out = new Float32Array(x.length), q = 2 ** (bits - 1);
  let v = 0;
  for (let i = 0; i < x.length; i++) { if (i % hold === 0) v = Math.round(x[i] * q) / q; out[i] = v; }
  return out;
});

// Feedback delay with a low-pass in the loop.
export const echo = chan((x, time, fb = 0.3, wet = 0.3, tone = 4000, tail = 1) => {
  const n = x.length + len(tail), out = new Float32Array(n), d = len(time), buf = new Float32Array(d);
  const k = Math.exp((-TAU * tone) / SR);
  let lpState = 0;
  for (let i = 0; i < n; i++) {
    const inp = i < x.length ? x[i] : 0, del = buf[i % d];
    lpState = del + k * (lpState - del);
    buf[i % d] = inp + lpState * fb;
    out[i] = inp + del * wet;
  }
  return out;
});

// Freeverb. Returns stereo, with a tail added.
export function reverb(x, { size = 0.6, damp = 0.35, wet = 0.25, dry = 1, width = 1, tail = 1.2, pre = 0.008 } = {}) {
  const s = toSt(x), n0 = s.L.length, n = n0 + len(tail), np = len(pre);
  const fbk = size * 0.28 + 0.7, d1 = damp * 0.4, d2 = 1 - d1;
  const run = spread => {
    const combs = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617].map(l => ({ b: new Float32Array(l + spread), i: 0, f: 0 }));
    const alls = [556, 441, 341, 225].map(l => ({ b: new Float32Array(l + spread), i: 0 }));
    const out = new Float32Array(n);
    for (let i = 0; i < n; i++) {
      const j = i - np, inp = j >= 0 && j < n0 ? (s.L[j] + s.R[j]) * 0.015 : 0;
      let o = 0;
      for (const c of combs) {
        const y = c.b[c.i];
        c.f = y * d2 + c.f * d1;
        c.b[c.i] = inp + c.f * fbk;
        if (++c.i >= c.b.length) c.i = 0;
        o += y;
      }
      for (const a of alls) {
        const y = a.b[a.i];
        a.b[a.i] = o + y * 0.5;
        if (++a.i >= a.b.length) a.i = 0;
        o = y - o;
      }
      out[i] = o;
    }
    return out;
  };
  const wl = run(0), wr = run(23), w1 = wet * 3 * (width / 2 + 0.5), w2 = wet * 3 * ((1 - width) / 2);
  const L = new Float32Array(n), R = new Float32Array(n);
  for (let i = 0; i < n; i++) {
    const dl = i < n0 ? s.L[i] : 0, dr = i < n0 ? s.R[i] : 0;
    L[i] = dl * dry + wl[i] * w1 + wr[i] * w2;
    R[i] = dr * dry + wr[i] * w1 + wl[i] * w2;
  }
  return { L, R };
}

// ---------- finishing ----------

export function peakOf(x) {
  const s = toSt(x);
  let p = 0;
  for (let i = 0; i < s.L.length; i++) p = Math.max(p, Math.abs(s.L[i]), Math.abs(s.R[i]));
  return p;
}

// Loudest 300 ms RMS of the mono sum, in dBFS. A rough stand-in for momentary loudness.
export function loudness(x) {
  const m = toMono(x), w = Math.min(len(0.3), m.length), hop = Math.max(1, w >> 2);
  let best = 0;
  for (let o = 0; o + w <= m.length; o += hop) {
    let s = 0;
    for (let i = o; i < o + w; i++) s += m[i] * m[i];
    best = Math.max(best, s / w);
  }
  return 10 * Math.log10(best + 1e-12);
}

// Gain to a loudness target, then a lookahead peak limiter to the ceiling. Always returns stereo.
export function master(x, { target = -18, ceiling = -1 } = {}) {
  const s = toSt(x), g0 = dbToGain(target - loudness(s)), n = s.L.length, ceil = dbToGain(ceiling);
  const L = mul(s.L, g0), R = mul(s.R, g0);
  const h = len(0.0015), want = new Float32Array(n);
  for (let i = 0; i < n; i++) want[i] = Math.min(1, ceil / Math.max(Math.abs(L[i]), Math.abs(R[i]), 1e-9));
  // Minimum over a centred window, then a box average of half that width: the gain is already down at each peak.
  const mn = new Float32Array(n), dq = new Int32Array(n);
  let head = 0, tail = 0;
  for (let i = 0; i < n + h; i++) {
    if (i < n) { while (tail > head && want[dq[tail - 1]] >= want[i]) tail--; dq[tail++] = i; }
    const c = i - h;
    if (c >= 0) { while (dq[head] < c - h) head++; mn[c] = want[dq[head]]; }
  }
  const rel = Math.exp(-1 / (0.08 * SR));
  let gs = 1, acc = h;
  const box = new Float32Array(n);
  for (let i = 0; i < n; i++) { gs = mn[i] < gs ? mn[i] : mn[i] + rel * (gs - mn[i]); box[i] = gs; }
  for (let i = 0; i < n; i++) {
    acc += box[i] - (i >= h ? box[i - h] : 1);
    const g = acc / h;
    L[i] = clamp(L[i] * g, -ceil, ceil);
    R[i] = clamp(R[i] * g, -ceil, ceil);
  }
  return { L, R };
}

// Cut the tail once its 20 ms RMS falls this far below the loudest window, then fade out over the last bit.
export function trimTail(x, belowDb = 40, fadeOut = 0.2) {
  const s = toSt(x), w = len(0.02), n = s.L.length, rms = [];
  for (let o = 0; o < n; o += w) {
    let e = 0;
    for (let i = o; i < Math.min(n, o + w); i++) e += s.L[i] * s.L[i] + s.R[i] * s.R[i];
    rms.push(e / (2 * w));
  }
  const floor = Math.max(...rms) * dbToGain(-belowDb) ** 2;
  let last = rms.length - 1;
  while (last > 0 && rms[last] < floor) last--;
  const end = Math.min(n, (last + 1) * w + len(0.01));
  const out = { L: s.L.slice(0, end), R: s.R.slice(0, end) }, f = Math.min(len(fadeOut), Math.floor(end / 3));
  for (let i = 0; i < f; i++) { const g = (i / f) ** 2; out.L[end - 1 - i] *= g; out.R[end - 1 - i] *= g; }
  return out;
}

// Render (dur + xfade) seconds, then fold the tail over the head so the clip loops without a seam.
export function loopify(x, xfade) {
  const s = toSt(x), X = len(xfade), D = s.L.length - X;
  const fold = a => {
    const out = a.slice(0, D);
    for (let i = 0; i < X; i++) { const u = (i / X) * (Math.PI / 2); out[i] = a[i] * Math.sin(u) + a[D + i] * Math.cos(u); }
    return out;
  };
  return { L: fold(s.L), R: fold(s.R) };
}

// 16-bit PCM WAV with TPDF dither.
export function wav(x, mono) {
  const s = toSt(x), ch = mono ? 1 : 2, n = s.L.length, buf = Buffer.alloc(44 + n * ch * 2), r = rng(n);
  buf.write('RIFF', 0); buf.writeUInt32LE(36 + n * ch * 2, 4); buf.write('WAVE', 8); buf.write('fmt ', 12);
  buf.writeUInt32LE(16, 16); buf.writeUInt16LE(1, 20); buf.writeUInt16LE(ch, 22); buf.writeUInt32LE(SR, 24);
  buf.writeUInt32LE(SR * ch * 2, 28); buf.writeUInt16LE(ch * 2, 32); buf.writeUInt16LE(16, 34); buf.write('data', 36);
  buf.writeUInt32LE(n * ch * 2, 40);
  const q = v => clamp(Math.round(v * 32767 + (r() - r())), -32768, 32767);
  let o = 44;
  for (let i = 0; i < n; i++) {
    if (mono) { buf.writeInt16LE(q((s.L[i] + s.R[i]) / 2), o); o += 2; }
    else { buf.writeInt16LE(q(s.L[i]), o); buf.writeInt16LE(q(s.R[i]), o + 2); o += 4; }
  }
  return buf;
}
