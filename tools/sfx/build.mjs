// Generates every No Winners sound effect into lucky-number/Assets/Art/Resources/Audio. Run: node tools/sfx/build.mjs [name filter]
// The list it follows is docs/no-winners-sfx-list.md. Everything is synthesised, so the output is ours and repeatable:
// each sound is seeded from its own name.
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  SR, len, mtof, rng, osc, noise, dust, hit, resonate, env, adsr, curve, mul, mix, pad, reverse, slice, fade, concat, toMono, toSt,
  Track, biquad, lp, hp, bp, peak, onepole, lag, drive, echo, reverb, master, trimTail, loopify, wav, loudness, peakOf, fm,
} from './dsp.mjs';
import {
  office, studio, hall, tv, phoneLine, kick, snare, tom, hat, crash, timpani, roll, brass, chord, marimba, glock, bass, coin,
  voice, cough, applause, cheer, gasp, murmur, src, has, cue,
} from './kit.mjs';

const OUT = fileURLToPath(new URL('../../lucky-number/Assets/Art/Resources/Audio/', import.meta.url));
const HERE = fileURLToPath(new URL('./', import.meta.url));
const TAU = Math.PI * 2;
const SOUNDS = [];
const sfx = (name, meta, fn) => SOUNDS.push({ name, ...meta, fn });

// Broadcast audio: the studio, heard through the TV set in the office.
const onAir = (x, wet) => tv(studio(x, wet));
// Recorded sources come in at whatever level they were made; bring them to a peak of 1 before mixing.
const norm = x => mul(x, 1 / (peakOf(x) || 1));

// ================= ROOM AND DESK (off air, quiet) =================

sfx('room_tone', { group: 'Room', prio: 'Must', target: -40, stereo: true, loop: 3, desc: 'Fluorescent hum, CRT whine, air and distant traffic. 24 s seamless loop.' }, r => {
  if (has('office')) {
    const t = new Track(), d = 27;
    t.add(norm(src('office', 10, 10 + d)), 0, 1);
    t.add(mul(mix(osc('sine', 100, d), mul(osc('sine', 200, d), 0.4)), 0.004), 0, 1, 0.15);
    t.add(mul(osc('sine', 15734, d), 0.002));
    return t.out();
  }
  const d = 27, t = new Track();
  const flick = lag(noise(d, r).map(v => 1 + 0.08 * v), 0.4);
  const hum = mix(...[[50, 0.25], [100, 1], [200, 0.45], [300, 0.28], [400, 0.16], [600, 0.1], [800, 0.05]].map(([f, a]) => mul(osc('sine', f, d, { phase: r() }), a)));
  const buzz = mul(lp(osc('square', 100, d, { pw: 0.12 }), 1600, 0.7), 0.05);
  t.add(mul(mix(hum, buzz), mul(flick, 0.05)), 0, 1, 0.15);
  for (const pan of [-0.7, 0.7]) t.add(mul(hp(lp(noise(d, r, 'pink'), 900, 0.6), 60), 0.5), 0, 1, pan);
  const traffic = mul(lp(noise(d, r, 'brown'), 260), lag(noise(d, r).map(v => 0.6 + 0.4 * Math.abs(v)), 1.5));
  t.add(mul(traffic, 0.5), 0, 1, -0.2);
  for (const [at, dir] of [[3, 1], [14, -1]]) {
    const cd = 7, car = bp(noise(cd, r, 'pink'), x => 380 + 320 * Math.sin(Math.PI * x / cd), 0.8);
    const shape = curve([[0, 0], [cd * 0.5, 1, 1.8], [cd, 0, 0.6]]);
    const c = mul(car, mul(shape, 0.5));
    const L = mul(c, x => 0.5 - 0.4 * dir * (x / cd - 0.5)), R = mul(c, x => 0.5 + 0.4 * dir * (x / cd - 0.5));
    t.add({ L, R }, at);
  }
  t.add(mul(osc('sine', 15734, d), 0.0035), 0, 1, 0);
  return t.out();
});

sfx('room_clock_tick', { group: 'Room', prio: 'Should', target: -32, desc: 'Wall clock, tick half. Recorded.' }, () => office(src('clock'), 0.08));
sfx('room_clock_tock', { group: 'Room', prio: 'Should', target: -33, desc: 'Wall clock, tock half: the same tick a little lower. Alternate with tick.' }, () => office(src('clock', 0, null, { pitch: 0.86 }), 0.08));

const whoosh = (r, dir) => {
  const d = 0.48, n = noise(d, r, 'pink');
  const f = curve([[0, 350], [0.2, 1500, 0.8], [d, 500, 0.7]]);
  const body = mul(bp(n, f, 0.75), curve([[0, 0], [0.19, 1, 1.7], [d, 0, 0.6]]));
  const L = mul(body, x => 0.6 - 0.35 * dir * (x / d - 0.5) * 2), R = mul(body, x => 0.6 + 0.35 * dir * (x / d - 0.5) * 2);
  return { L, R };
};
sfx('room_look_left', { group: 'Room', prio: 'Should', target: -29, stereo: true, desc: 'Soft whoosh, turning left to the TV.' }, r => whoosh(r, -1));
sfx('room_look_right', { group: 'Room', prio: 'Should', target: -29, stereo: true, desc: 'Soft whoosh, turning right to the machine.' }, r => whoosh(r, 1));

// ---------- monitor UI ----------

const blip = (f, d, { pw = 0.5, tau = 0.012, cut = 6000 } = {}) => lp(mul(osc('square', f, d, { pw }), env(d, { a: 0.0015, tau, rel: 0.004 })), cut, 0.7);

sfx('ui_hover', { group: 'Monitor UI', prio: 'Must', target: -28, desc: 'Tiny CRT blip when the pointer lands on something.' }, () => mix(blip(1560, 0.03, { tau: 0.008, pw: 0.3 }), mul(osc('sine', 3120, 0.03), env(0.03, { tau: 0.005 }))));
sfx('ui_menu_move', { group: 'Monitor UI', prio: 'Must', target: -23, desc: 'Menu selection moves.' }, () => blip(990, 0.05, { tau: 0.014, pw: 0.35 }));
sfx('ui_menu_confirm', { group: 'Monitor UI', prio: 'Must', target: -21, desc: 'Menu select: two blips up.' }, () => {
  const t = new Track();
  t.add(blip(784, 0.055, { pw: 0.25, tau: 0.03 })).add(blip(1175, 0.11, { pw: 0.25, tau: 0.04 }), 0.06);
  return office(toMono(t.out()), 0.05);
});
sfx('ui_menu_back', { group: 'Monitor UI', prio: 'Must', target: -23, desc: 'Back or cancel: two blips down.' }, () => {
  const t = new Track();
  t.add(blip(1175, 0.045, { pw: 0.25, tau: 0.02 })).add(blip(740, 0.08, { pw: 0.25, tau: 0.03 }), 0.05);
  return office(toMono(t.out()), 0.05);
});
sfx('ui_slider_tick', { group: 'Monitor UI', prio: 'Should', target: -27, desc: 'One notch on the CRT strength or volume slider.' }, r => hit({ r, modes: [[2600, 0.004, 1], [5200, 0.002, 0.4], [1300, 0.005, 0.3]], noiseAmp: 0.2, noiseF: 5000 }));

// ---------- week start ----------

sfx('desk_tickets_print', { group: 'Desk', prio: 'Should', target: -22, desc: 'Dot-matrix printer chatter as the week\'s tickets come in. Recorded.' }, () =>
  office(src('printer', 0.1, 1.75, { fout: 0.2 }), 0.06));

sfx('desk_cash_register', { group: 'Desk', prio: 'Should', target: -20, desc: 'Old cash register: keys, bell, drawer. Recorded.' }, () =>
  office(src('register', 1.45, 4.3, { fout: 0.5 }), 0.05));

sfx('desk_calendar_flip', { group: 'Desk', prio: 'Should', target: -24, desc: 'Calendar page turns to the new week. Recorded.' }, () =>
  office(src('page', 0, 0.85, { fout: 0.1 }), 0.05));

sfx('desk_inspection_stamp', { group: 'Desk', prio: 'Should', target: -18, desc: 'Rubber stamp thuds on an inspection week.' }, r => {
  if (has('stamp')) {
    const t = new Track().add(norm(cue('stamp', 0.7)));
    t.add(mul(osc('sine', x => 60 + 40 * Math.exp(-x / 0.02), 0.3), env(0.3, { a: 0.001, tau: 0.06 })), 0.01, 0.35);
    return office(t.out(), 0.08);
  }
  const t = new Track();
  t.add(mul(bp(noise(0.08, r, 'pink'), 700, 0.6), curve([[0, 0], [0.08, 0.3, 2]])), 0);
  const thud = mul(osc('sine', x => 60 + 40 * Math.exp(-x / 0.02), 0.4), env(0.4, { a: 0.001, tau: 0.07 }));
  t.add(thud, 0.085);
  t.add(hit({ r, modes: [[210, 0.06, 0.6], [470, 0.04, 0.4], [1150, 0.015, 0.3]], noiseAmp: 0.8, noiseF: 1400, noiseQ: 0.7, noiseTau: 0.02 }), 0.085);
  t.add(mul(resonate(dust(0.15, r, 120), [[3000, 0.004, 1], [1800, 0.006, 0.5]]), 0.25), 0.1);
  t.add(mul(hp(noise(0.03, r), 2000), env(0.03, { tau: 0.008 })), 0.36, 0.2);
  return office(toMono(t.out()), 0.1);
});

// ---------- desk actions ----------

const handsetLift = r => mix(
  hit({ r, modes: [[900, 0.02, 1], [2300, 0.01, 0.6], [3600, 0.006, 0.3]], noiseAmp: 0.5, noiseF: 2500 }),
  pad(hit({ r, modes: [[5200, 0.003, 0.6]] }), 0.05),
  pad(hit({ r, modes: [[1900, 0.15, 0.12], [2400, 0.12, 0.08]] }), 0.04));
const hangUp = r => mix(
  hit({ r, modes: [[300, 0.04, 1], [750, 0.02, 0.6], [1700, 0.01, 0.4]], noiseAmp: 0.6, noiseF: 1800 }),
  hit({ r, modes: [[1850, 0.45, 0.32], [2380, 0.35, 0.22], [4700, 0.12, 0.1]] }));

sfx('desk_supplier_phone', { group: 'Desk', prio: 'Must', target: -19, desc: 'Supplier: recorded handset pick-up, a short mumble down the line, recorded hang-up.' }, r => {
  const t = new Track();
  t.add(norm(src('phone', 0, 0.45)), 0);
  const a = phoneLine(voice({ r, words: 3, f0: 104, rate: 1.15, tone: 'statement', rough: 0.2 }), r);
  const b = phoneLine(voice({ r, words: 2, f0: 100, rate: 1.2, tone: 'statement', rough: 0.2 }), r);
  const g = 0.35 / (peakOf(a) || 1);
  t.add(mul(a, g), 0.45);
  const after = 0.45 + a.length / SR + 0.18;
  t.add(mul(b, g), after);
  t.add(norm(src('phone', 4.4, 4.95)), after + b.length / SR + 0.2, 0.9);
  return office(t.out(), 0.06);
});

sfx('desk_bribe', { group: 'Desk', prio: 'Must', target: -20, desc: 'Bribe: an envelope slides across the desk, coins drop into a pile. Recorded.' }, () => {
  const t = new Track();
  t.add(norm(src('envelope', 0.25, 1.15, { fout: 0.08 })), 0);
  t.add(norm(src('coins', 0.05, 1.0, { fout: 0.15 })), 0.72, 0.7);
  return office(t.out(), 0.05);
});

sfx('desk_shred', { group: 'Desk', prio: 'Must', target: -18, desc: 'Shred: a recorded office shredder bites the paper, runs, and stops.' }, () => {
  const t = new Track();
  t.add(src('shred', 0, 1.7, { fout: 0.12 }), 0).add(src('shred', 6.05, 6.6, { fin: 0.12 }), 1.58);
  return office(t.out(), 0.04);
});

sfx('desk_card_lost', { group: 'Desk', prio: 'Must', target: -21, desc: 'Hand full: the new card is crumpled (recorded), with a low falling buzz.' }, () => {
  const buzz = lp(mul(osc('square', curve([[0, 260], [0.35, 130, 0.7]]), 0.38), env(0.38, { a: 0.005, hold: 0.2, tau: 0.06 })), 1800, 0.8);
  const t = new Track().add(norm(src('crumple', 0.05, 0.7, { fout: 0.12 }))).add(mul(buzz, 0.3), 0.18);
  return office(t.out());
});

sfx('desk_denied', { group: 'Desk', prio: 'Must', target: -21, desc: 'Dull thunk: that action is not allowed.' }, r =>
  office(lp(hit({ r, modes: [[150, 0.05, 1], [310, 0.03, 0.5], [620, 0.015, 0.25]], noiseAmp: 0.3, noiseF: 800 }), 1500)));

// ================= GOING LIVE =================

sfx('live_switch', { group: 'Going live', prio: 'Must', target: -16, desc: 'Heavy go-live toggle: a recorded switch with a synthesised body thunk under it.' }, r => {
  const t = new Track();
  t.add(norm(src('toggle')), 0);
  t.add(hit({ r, modes: [[180, 0.04, 0.9], [360, 0.025, 0.5], [95, 0.06, 0.6]] }), 0.2, 0.55);
  return office(t.out(), 0.08);
});

sfx('live_relay', { group: 'Going live', prio: 'Must', target: -18, desc: 'ON AIR relay clicks in (recorded) and the sign starts to buzz.' }, () => {
  const t = new Track();
  t.add(norm(src('relay')), 0);
  const coil = mul(lp(osc('square', 100, 0.3, { pw: 0.3 }), 1200), env(0.3, { a: 0.003, tau: 0.08 }));
  t.add(mul(coil, 0.15), 0.01);
  const sign = mul(lp(mix(osc('square', 100, 0.75, { pw: 0.15 }), mul(osc('sine', 200, 0.75), 0.5)), 2200), curve([[0, 0], [0.08, 0.06], [0.55, 0.05], [0.75, 0]]));
  t.add(sign, 0.06);
  return office(t.out(), 0.08);
});

sfx('live_onair_sting', { group: 'Going live', prio: 'Must', target: -15, stereo: true, desc: 'Short studio sting from the TV as the show goes live: da-DAH.' }, r => {
  const t = new Track();
  t.add(chord(brass, [67, 71, 74], 0.12, { vel: 0.8 }), 0);
  t.add(chord(brass, [60, 64, 67, 72], 0.75, { vel: 1 }), 0.16);
  t.add(bass(mtof(36), 0.7), 0.16, 0.6);
  t.add(kick(r), 0.16, 0.7).add(snare(r), 0.16, 0.5).add(crash(r, { d: 1.6, tau: 0.6 }), 0.16, 0.35);
  t.add(timpani(r, mtof(48), 0.9), 0.16, 0.5);
  return onAir(t.out());
});

sfx('machine_spin_loop', { group: 'Going live', prio: 'Must', target: -20, stereo: true, loop: 0.5, desc: 'Ball machine: blower and rattling balls. 4 s seamless loop.' }, r => {
  if (has('bingo')) {
    const d = 5, t = new Track();
    t.add(norm(src('bingo', 1, 1 + d)));
    t.add(mul(hp(lp(noise(d, r, 'pink'), 1400, 0.6), 120), 0.12), 0, 1, -0.4).add(mul(hp(lp(noise(d, r, 'pink'), 1400, 0.6), 120), 0.12), 0, 1, 0.4);
    return t.out();
  }
  const d = 4.5, t = new Track();
  const air = mul(hp(lp(noise(d, r, 'pink'), 1400, 0.6), 120), x => 0.8 + 0.2 * Math.sin(TAU * 0.5 * x));
  const motor = mul(lp(mix(osc('saw', x => 118 * (1 + 0.006 * Math.sin(TAU * 3 * x)), d), mul(osc('sine', 236, d), 0.5)), 600), 0.12);
  t.add(mul(air, 0.5), 0, 1, -0.4).add(mul(hp(lp(noise(d, r, 'pink'), 1500, 0.6), 120), 0.5), 0, 1, 0.4).add(motor);
  const busy = lag(noise(d, r).map(v => 0.5 + 0.5 * Math.abs(v)), 0.15);
  for (let s = 0; s < d; s += -Math.log(1 - r()) / 46) {
    const b = busy[Math.min(busy.length - 1, Math.round(s * SR))], f = r.range(2200, 3600), g = r() ** 2 * b;
    const ball = r.chance(0.15)
      ? hit({ r, modes: [[r.range(800, 1200), 0.02, 1], [r.range(1900, 2400), 0.01, 0.4]], noiseAmp: 0.2 })
      : hit({ r, modes: [[f, 0.012, 1], [f * 1.73, 0.007, 0.5], [f * 0.52, 0.02, 0.3]], noiseAmp: 0.2, noiseF: 6000 });
    t.add(ball, s, g * 0.6, r.range(-0.7, 0.7));
  }
  return reverb(t.out(), { size: 0.4, damp: 0.5, wet: 0.08, tail: 0 });
});

[0.95, 1, 1.06].forEach((p, k) => sfx(`machine_digit_flip_${k + 1}`, { group: 'Going live', prio: 'Must', target: -25, desc: `One flap of the number drum, variant ${k + 1}. Fire at 12 fps, rotating variants.` }, r =>
  hit({ r, pitch: p, modes: [[2800, 0.006, 1], [4300, 0.004, 0.6], [1400, 0.008, 0.4], [400, 0.01, 0.2]], noiseAmp: 0.5, noiseF: 5000, noiseTau: 0.002 })));

sfx('machine_lock', { group: 'Going live', prio: 'Must', target: -16, desc: 'The number locks: ratchet, then a heavy clunk. The on-air clock starts here.' }, r => {
  const t = new Track();
  t.add(hit({ r, modes: [[3400, 0.004, 0.6], [1800, 0.006, 0.3]], noiseAmp: 0.3 }), 0);
  t.add(hit({ r, modes: [[3600, 0.004, 0.7], [1900, 0.006, 0.3]], noiseAmp: 0.3 }), 0.04);
  t.add(hit({ r, modes: [[70, 0.09, 1], [160, 0.05, 0.7], [620, 0.03, 0.5], [1500, 0.015, 0.4], [2900, 0.006, 0.25]], noiseAmp: 0.7, noiseF: 1800 }), 0.08);
  t.add(mul(resonate(dust(0.12, r, 200), [[2400, 0.004, 1], [1300, 0.006, 0.5]]), 0.25), 0.1);
  return office(toMono(t.out()), 0.1);
});

// ================= RIGGING (on air) =================

const cardFeed = (r, withEject = true) => {
  const d = 0.55, t = new Track();
  const slide = mul(bp(noise(d, r), 2600, 0.7), curve([[0, 0], [0.05, 1], [0.48, 0.8], [d, 0]]));
  t.add(mix(mul(slide, 0.4), mul(lp(noise(d, r, 'pink'), 500), curve([[0, 0], [0.05, 0.3], [d, 0]]))));
  const pins = new Float32Array(len(d));
  for (let s = 0.06; s < 0.52; s += (1 / 75) * r.range(0.85, 1.15)) pins[Math.round(s * SR)] = r.range(0.6, 1);
  t.add(mul(resonate(pins, [[3100, 0.003, 1], [1900, 0.004, 0.6], [5200, 0.002, 0.4]]), 2.2));
  t.add(mul(lp(osc('saw', 150, d), 700), curve([[0, 0], [0.04, 0.1], [0.5, 0.1], [d, 0]])));
  if (withEject) t.add(hit({ r, modes: [[2300, 0.008, 1], [1200, 0.012, 0.5]], noiseAmp: 0.5, noiseF: 3500 }), 0.55, 0.8);
  return toMono(t.out());
};

sfx('rig_card_feed', { group: 'Rigging', prio: 'Must', target: -19, desc: 'Punch card feeds through the reader, about 0.6 s.' }, r => office(cardFeed(r)));
sfx('rig_undo', { group: 'Rigging', prio: 'Must', target: -20, desc: 'Undo: the card is pulled back out of the reader.' }, r => {
  const t = new Track().add(reverse(slice(cardFeed(r, false), 0, 0.5)));
  t.add(hit({ r, modes: [[1700, 0.01, 1], [900, 0.015, 0.4]], noiseAmp: 0.4, noiseF: 2500 }), 0.5, 0.7);
  return office(toMono(t.out()));
});

sfx('rig_number_snap', { group: 'Rigging', prio: 'Must', target: -22, desc: 'The board result updates: a quick two-step chirp.' }, () =>
  lp(mul(osc('square', x => (x < 0.022 ? 1320 : 1760), 0.075, { pw: 0.3 }), env(0.075, { a: 0.001, tau: 0.022, rel: 0.005 })), 7000));

sfx('rig_illegal', { group: 'Rigging', prio: 'Must', target: -20, desc: 'Buzzer: that card would leave 00-99, is not whole, or breaks the inspection limit.' }, () => {
  const d = 0.3, src = mix(osc('square', 147, d), osc('square', 155, d));
  return drive(lp(mul(src, env(d, { a: 0.003, hold: 0.2, tau: 0.03, rel: 0.02 })), 2200, 0.8), 1.6);
});

const block = (r, p) => hit({ r, pitch: p, modes: [[1850, 0.018, 1], [2900, 0.009, 0.4], [640, 0.02, 0.2]], noiseAmp: 0.3, noiseF: 4000 });
sfx('rig_timer_tick', { group: 'Rigging', prio: 'Must', target: -22, desc: 'Timer tick, last 5 seconds at 2 Hz.' }, r => office(block(r, 1), 0.05));
sfx('rig_timer_tick_final', { group: 'Rigging', prio: 'Must', target: -19, desc: 'Final second tick, higher and brighter.' }, r => office(mix(block(r, 1.33), mul(block(r, 2), 0.4)), 0.05));

sfx('rig_dead_air', { group: 'Rigging', prio: 'Should', target: -40, loop: 0.4, desc: 'Dead-air hiss from the TV once the target passes. 3 s seamless loop.' }, r => {
  const d = 3.4;
  const hiss = mul(hp(noise(d, r, 'pink'), 1500), 0.4);
  const hum = mul(mix(osc('sine', 60, d), mul(osc('sine', 120, d), 0.5), mul(osc('sine', 180, d), 0.3)), 0.06);
  const crack = mul(resonate(dust(d, r, 6), [[2500, 0.002, 1]]), 0.6);
  return tv(mix(hiss, hum, crack), 1);
});

// ================= STALLS (comedy, every 10 s past the target) =================

const rimshot = r => {
  const t = new Track();
  t.add(tom(r, 150), 0, 0.7).add(snare(r), 0.17, 0.8).add(kick(r), 0.17, 0.6).add(crash(r, { d: 1.2, tau: 0.35 }), 0.36, 0.5).add(snare(r), 0.36, 0.5);
  return toMono(t.out());
};

sfx('stall_bad_joke', { group: 'Stalls', prio: 'Should', target: -16, stereo: true, desc: 'Host tells a bad joke, rimshot, one lone cough in the audience.' }, r => {
  const t = new Track();
  const host = voice({ r, words: 5, f0: 108, rate: 1.1, tone: 'punchline', energy: 1 });
  t.add(host, 0.05);
  const after = 0.05 + host.length / SR + 0.12;
  t.add(rimshot(r), after, 0.8);
  t.add(has('cough') ? hall(mul(norm(cue('cough', 1)), 0.6), 0.2) : hall(cough(r, { f0: 130 }), 0.3), after + 1.25, 0.35, 0.5);
  return onAir(t.out(), 0.12);
});

sfx('stall_sponsor', { group: 'Stalls', prio: 'Should', target: -16, stereo: true, desc: 'Cheesy sponsor jingle on marimba and glockenspiel, with a breathless tagline.' }, r => {
  const t = new Track(), b = 60 / 132;
  const mel = [[76, 0, 0.5], [79, 0.5, 0.5], [84, 1, 1], [81, 2, 0.5], [79, 2.5, 0.5], [76, 3, 1], [77, 4, 0.5], [81, 4.5, 0.5], [79, 5, 1], [76, 6, 0.5], [74, 6.5, 0.5], [72, 7, 1]];
  for (const [m, at] of mel) t.add(marimba(r, mtof(m)), at * b, 0.7).add(marimba(r, mtof(m - 12)), at * b + 0.004, 0.25);
  for (const [m, at] of [[48, 0], [55, 1], [53, 2], [55, 3], [53, 4], [57, 5], [55, 6], [48, 7]]) t.add(bass(mtof(m - 12), b * 0.9, { bright: 0.6 }), at * b, 0.45);
  for (let k = 0; k < 16; k++) t.add(hat(r), k * b * 0.5, k % 2 ? 0.12 : 0.2);
  for (let k = 0; k < 8; k++) t.add(kick(r, { tau: 0.18 }), k * b, 0.4);
  t.add(glock(r, mtof(96)), 8 * b, 0.5).add(glock(r, mtof(91)), 8.5 * b, 0.5);
  t.add(voice({ r, words: 2, f0: 230, female: true, tone: 'excited', rate: 1.35, energy: 1 }), 8 * b + 0.05, 0.9);
  return onAir(t.out(), 0.12);
});

sfx('stall_interview', { group: 'Stalls', prio: 'Should', target: -17, stereo: true, desc: 'Confused audience interview: a question, a long uncertain answer, murmuring.' }, r => {
  const t = new Track();
  t.add(has('walla') ? mul(norm(src('walla', 2, 6.8, { fin: 0.3, fout: 0.6 })), 0.3) : murmur(r, 4.6, { people: 8, level: 0.4 }));
  const q = voice({ r, words: 3, f0: 112, rate: 1.15, tone: 'question' });
  t.add(q, 0.1, 1, -0.15);
  const a = voice({ r, words: 4, syl: [1, 2], f0: 205, female: true, rate: 0.9, tone: 'hesitant', hesitate: 0.6, energy: 0.85 });
  t.add(a, 0.1 + q.length / SR + 0.35, 0.9, 0.15);
  return onAir(t.out(), 0.12);
});

sfx('stall_panic', { group: 'Stalls', prio: 'Should', target: -18, stereo: true, desc: 'Studio panic: shuffling feet, a dropped clipboard, a mic bump, a frantic whisper.' }, r => {
  const t = new Track();
  if (has('footsteps')) t.add(mul(norm(src('footsteps', 5.3, 6.9, { fin: 0.05, fout: 0.3 })), 0.45), 0, 1, 0.2);
  else {
    for (let k = 0; k < 7; k++) {
      const s = r.range(0, 1.4);
      t.add(mul(lp(noise(0.08, r, 'pink'), r.range(700, 1300)), env(0.08, { a: 0.004, tau: 0.03 })), s, r.range(0.3, 0.7), r.range(-0.6, 0.6));
    }
    t.add(mul(bp(noise(1.2, r), 2000, 0.6), mul(lag(noise(1.2, r).map(Math.abs), 0.02), 0.25)), 0.1, 1, 0.3);
  }
  if (has('clipboard')) t.add(norm(src('clipboard', 4.95, 6.1, { fout: 0.3 })), 0.7, 0.8, -0.2);
  else {
    let at = 0.7, g = 1;
    for (const gap of [0, 0.13, 0.08, 0.05, 0.03, 0.02]) {
      at += gap;
      t.add(hit({ r, modes: [[650 * r.range(0.9, 1.1), 0.03, 1], [1600, 0.015, 0.7], [2900, 0.008, 0.4]], noiseAmp: 0.4, noiseF: 2500 }), at, g * 0.8, -0.2);
      g *= 0.62;
    }
  }
  t.add(lp(hit({ r, modes: [[80, 0.06, 1], [160, 0.04, 0.4]], noiseAmp: 0.6, noiseF: 400 }), 600), 1.35, 0.9);
  t.add(voice({ r, words: 2, f0: 120, whisper: true, rate: 1.4, energy: 1 }), 1.55, 1.3, 0.1);
  t.add(voice({ r, words: 2, f0: 200, female: true, whisper: true, rate: 1.5, energy: 1 }), 2.1, 1, -0.3);
  return onAir(t.out(), 0.14);
});

// ================= ANNOUNCE AND BROADCAST =================

sfx('announce_button', { group: 'Announce', prio: 'Must', target: -14, desc: 'Big red button: a recorded press and release, with a synthesised low thump for weight.' }, r => {
  const t = new Track();
  t.add(norm(src('button', 0.45, 0.95, { fout: 0.08 })), 0);
  t.add(hit({ r, modes: [[85, 0.08, 1.2], [170, 0.04, 0.5], [640, 0.03, 0.3]] }), 0.05, 0.6);
  t.add(norm(src('button', 2.45, 2.85, { fout: 0.08 })), 0.3, 0.55);
  return office(t.out(), 0.08);
});

sfx('announce_static', { group: 'Announce', prio: 'Must', target: -17, stereo: true, desc: 'Channel-change static, about 0.3 s, hard cut. Mid-brightness, no harsh top.' }, r => {
  const d = 0.32, gate = curve([[0, 0], [0.003, 1], [0.29, 0.85], [d, 0]]);
  const make = () => {
    const w = noise(d, r), sweep = lag(noise(d, r).map(v => 2500 + 2000 * v), 0.01);
    const band = bp(w, sweep, 0.8), crack = resonate(dust(d, r, 500), [[1800, 0.002, 1], [4200, 0.0015, 0.6]]);
    const buzz = mul(lp(osc('square', 60, d, { pw: 0.08 }), 900), 0.25);
    return lp(mul(mix(mul(w, 0.35), band, mul(crack, 0.8), buzz), gate), 7500, 0.6);
  };
  return { L: make(), R: make() };
});

sfx('broadcast_theme', { group: 'Announce', prio: 'Must', target: -14, stereo: true, desc: 'Short gaudy game-show theme as the broadcast opens.' }, r => {
  const t = new Track(), b = 0.4;
  t.add(roll(r, () => snare(r, { tau: 0.08 }), 2 * b, { from: 0.15, to: 0.9 }), 0, 0.5);
  t.add(crash(r, { d: 0.8, swell: 0.78 }), 0, 0.3);
  const stab = (notes, at, d = 0.18, vel = 1) => t.add(chord(brass, notes, d, { vel }), at * b);
  stab([60, 64, 67, 72], 2); stab([60, 64, 67, 72], 2.5, 0.14, 0.85);
  t.add(kick(r), 2 * b, 0.8).add(crash(r, { d: 2, tau: 0.7 }), 2 * b, 0.35);
  for (const [m, at, d] of [[67, 3, 0.18], [72, 3.5, 0.18], [76, 4, 0.18], [79, 4.5, 0.38], [81, 5, 0.38]]) t.add(brass(mtof(m), d, { vel: 1, bright: 1.15 }), at * b, 0.55);
  stab([53, 57, 60, 65], 6, 0.3); stab([55, 59, 62, 65, 67], 7, 0.3);
  stab([60, 64, 67, 72, 76], 8, 1.1);
  t.add(brass(mtof(84), 1.1, { vel: 1, bright: 1.2 }), 8 * b, 0.45);
  for (const [m, at] of [[36, 2], [36, 2.5], [41, 6], [43, 7], [36, 8]]) t.add(bass(mtof(m), at === 8 ? 1 : 0.35), at * b, 0.6);
  for (let k = 2; k <= 8; k++) t.add(kick(r), k * b, 0.55);
  for (const k of [3, 5, 7]) t.add(snare(r), k * b, 0.45);
  t.add(snare(r), 7.5 * b, 0.3).add(snare(r), 7.75 * b, 0.4);
  t.add(crash(r, { d: 2.4, tau: 0.55 }), 8 * b, 0.45).add(timpani(r, mtof(36), 0.9), 8 * b, 0.6);
  [84, 88, 91, 96].forEach((m, k) => t.add(glock(r, mtof(m)), 8 * b + 0.07 * k, 0.3));
  return onAir(t.out(), 0.15);
});

const tickerBeeps = (r, n = 3) => {
  const t = new Track();
  for (let k = 0; k < n; k++) t.add(mul(osc('sine', 1600, 0.05), env(0.05, { a: 0.002, tau: 0.02 })), k * 0.11, 0.3);
  return toMono(t.out());
};

sfx('result_clean', { group: 'Broadcast', prio: 'Must', target: -14, stereo: true, desc: 'Clean draw: big fanfare, timpani, crowd cheer, ticker beeps.' }, r => {
  const t = new Track();
  t.add(roll(r, () => timpani(r, mtof(43), 0.5), 0.36, { rate: 24, from: 0.3, to: 1 }), 0, 0.4);
  [[72, 0], [76, 0.12], [79, 0.24]].forEach(([m, at]) => t.add(brass(mtof(m), 0.11, { vel: 1, bright: 1.2 }), at, 0.55));
  t.add(chord(brass, [60, 64, 67, 72, 76, 79], 1.6, { vel: 1, bright: 1.1 }), 0.36);
  t.add(brass(mtof(84), 1.6, { vel: 1, bright: 1.3 }), 0.36, 0.5);
  t.add(bass(mtof(36), 1.5), 0.36, 0.6).add(timpani(r, mtof(36), 1.8), 0.36, 0.8).add(kick(r), 0.36, 0.8);
  t.add(crash(r, { d: 3, tau: 1.1 }), 0.36, 0.45);
  [84, 88, 91, 96, 100].forEach((m, k) => t.add(glock(r, mtof(m)), 0.4 + 0.06 * k, 0.28));
  t.add(norm(src('cheer', 4.2, 9.2, { fin: 0.7, fout: 1.3 })), 0.3, 0.8);
  t.add(norm(src('applause_big', 3, 8, { fin: 0.8, fout: 1.3 })), 0.4, 0.55);
  t.add(tickerBeeps(r), 3.9, 0.6);
  return onAir(t.out(), 0.12);
});

sfx('result_small', { group: 'Broadcast', prio: 'Must', target: -17, stereo: true, desc: 'Small winner: a polite ding-ding and thin, recorded polite applause. Got away with it.' }, r => {
  const t = new Track();
  t.add(glock(r, mtof(79), 0.7), 0, 0.8).add(glock(r, mtof(84), 0.8), 0.18, 0.8);
  t.add(fm(mtof(79), 1.2, { r, ratio: 3.5, index: 1.5, tau: 0.5 }), 0, 0.2);
  t.add(norm(src('applause_small', 0, 3.4, { fin: 0.25, fout: 1.2 })), 0.3, 0.45);
  return onAir(t.out(), 0.08);
});

sfx('result_scandal', { group: 'Broadcast', prio: 'Must', target: -15, stereo: true, desc: 'Scandal: the crowd gasps, a low stinger, a sad trombone, then a newsroom alarm.' }, r => {
  const t = new Track();
  t.add(norm(src('gasp', 0, 1.9, { fout: 0.6 })), 0, 0.55);
  t.add(chord(brass, [36, 42, 47, 48], 0.9, { vel: 0.9, bright: 0.7 }), 0.05, 0.8);
  t.add(timpani(r, mtof(36), 1.6, 1), 0.05, 0.9).add(kick(r, { f0: 120, f1: 40, tau: 0.35 }), 0.05, 0.7);
  const plunger = (d, wobble) => x => (0.35 + 0.65 * Math.min(1, x / 0.12)) * (wobble ? 0.75 + 0.25 * Math.sin(TAU * 6 * x) : 1);
  [[55, 1.0, 0.36], [54, 1.42, 0.36], [53, 1.84, 0.36], [52, 2.26, 1.15]].forEach(([m, at, d], k) =>
    t.add(brass(mtof(m), d, { vel: 0.8, bright: 0.6, vib: k === 3 ? 0.03 : 0.004, wah: plunger(d, k === 3), scoop: 0.04 }), at, 1.2));
  const ad = 1.3, alarm = mul(lp(osc('square', x => (Math.floor(x / 0.18) % 2 ? 770 : 960), ad, { pw: 0.5 }), 2600), curve([[0, 0], [0.02, 1], [ad - 0.2, 1], [ad, 0]]));
  t.add(alarm, 3.45, 0.22);
  t.add(has('walla') ? mul(norm(src('walla', 10, 13.6, { fin: 0.6, fout: 0.8 })), 0.3) : murmur(r, 3.6, { people: 10, level: 0.3 }), 1.0);
  return onAir(t.out(), 0.12);
});

sfx('result_live_bonus', { group: 'Broadcast', prio: 'Should', target: -18, stereo: true, desc: 'Live bonus +20: bright chime run with a coin sparkle.' }, r => {
  const t = new Track();
  [84, 88, 91, 96].forEach((m, k) => t.add(glock(r, mtof(m), 0.5), k * 0.06, 0.6, -0.3 + 0.2 * k));
  for (let k = 0; k < 5; k++) t.add(coin(r, r.range(1.2, 1.5), 0.05), 0.2 + k * 0.045, 0.18, r.range(-0.6, 0.6));
  return reverb(t.out(), { size: 0.5, damp: 0.35, wet: 0.16, tail: 0.6 });
});

for (let k = 1; k <= 6; k++) {
  sfx(`result_streak_${k}`, { group: 'Broadcast', prio: 'Should', target: -21, desc: `Clean streak goes up to ${k}${k === 6 ? '+' : ''}. Pitch rises a whole tone per step.` }, () => {
    const f = 660 * 2 ** ((2 * (k - 1)) / 12), t = new Track();
    t.add(blip(f, 0.06, { pw: 0.25, tau: 0.03 })).add(mix(blip(f * 1.5, 0.14, { pw: 0.25, tau: 0.06 }), mul(osc('sine', f * 3, 0.14), env(0.14, { tau: 0.05 }))), 0.065);
    return office(toMono(t.out()), 0.06);
  });
}

sfx('result_cash_tick', { group: 'Broadcast', prio: 'Must', target: -26, desc: 'One tick of the cash counting down after a payout. Repeat fast.' }, r =>
  hit({ r, modes: [[3200, 0.006, 1], [5400, 0.003, 0.4], [1600, 0.004, 0.2]], noiseAmp: 0.2, noiseF: 6000 }));

sfx('result_reveal_beep', { group: 'Broadcast', prio: 'Should', target: -21, desc: 'Two teletext beeps as the monitor shows the best route.' }, () => {
  const beep = mix(mul(osc('sine', 1040, 0.09), env(0.09, { a: 0.003, hold: 0.06, tau: 0.01 })), mul(lp(osc('square', 1040, 0.09), 3000), env(0.09, { a: 0.003, hold: 0.06, tau: 0.01 }), 0.15));
  const t = new Track().add(beep).add(beep, 0.14);
  return office(toMono(t.out()), 0.05);
});

// ================= END STATES =================

sfx('end_signal_loss', { group: 'End', prio: 'Must', target: -17, desc: 'Bankrupt: picture collapses with a falling thwip and a pop, then the tube hums down.' }, r => {
  const t = new Track();
  const sweep = mul(mix(osc('sine', curve([[0, 1800], [0.28, 55, 0.35]]), 0.3), mul(osc('saw', curve([[0, 900], [0.28, 40, 0.35]]), 0.3), 0.3)), curve([[0, 0], [0.01, 0.8], [0.26, 0.6], [0.3, 0]]));
  t.add(sweep);
  t.add(mul(lp(noise(0.3, r), curve([[0, 8000], [0.3, 300, 0.4]])), curve([[0, 0.5], [0.3, 0]])));
  t.add(hit({ r, modes: [[120, 0.03, 1], [60, 0.05, 0.6]], noiseAmp: 0.8, noiseF: 1200, noiseTau: 0.004 }), 0.29, 0.8);
  const hd = 1.9, hum = mul(mix(osc('sine', 50, hd), mul(osc('sine', 100, hd), 0.7), mul(osc('sine', 150, hd), 0.3)), env(hd, { a: 0.01, tau: 0.55 }));
  t.add(mul(hum, 0.5), 0.3);
  t.add(mul(osc('sine', 15734, 1.4), env(1.4, { a: 0.005, tau: 0.35 })), 0.3, 0.04);
  t.add(mul(resonate(dust(1.2, r, x => 30 * Math.exp(-x / 0.4)), [[2200, 0.002, 1]]), 0.3), 0.35);
  return office(toMono(t.out()), 0.06);
});

sfx('end_stand_by_tone', { group: 'End', prio: 'Must', target: -24, loop: 0.3, desc: '"PLEASE STAND BY" 1 kHz test tone through the TV. 2 s seamless loop.' }, () =>
  tv(mix(osc('sine', 1000, 2.3), mul(osc('sine', 2000, 2.3), 0.02)), 1.05));

sfx('end_finale_fanfare', { group: 'End', prio: 'Must', target: -14, stereo: true, desc: 'Week 12 survived: long fanfare with a key change, timpani, cheering and applause.' }, r => {
  const t = new Track(), b = 0.5;
  const mel = [[67, 0, 0.45], [72, 0.5, 0.45], [76, 1, 0.45], [79, 1.5, 1.4], [81, 3, 0.45], [79, 3.5, 0.45],
    [77, 4, 0.9], [81, 5, 0.45], [84, 5.5, 1.3], [83, 7, 0.22], [79, 7.25, 0.22], [81, 7.5, 0.45], [86, 8, 2.6]];
  for (const [m, at, d] of mel) t.add(brass(mtof(m), d, { vel: 1, bright: 1.2 }), at * b, 0.55);
  const pads = [[[60, 64, 67], 0, 1.9], [[60, 64, 67], 2, 1.9], [[53, 57, 60, 65], 4, 1.9], [[55, 59, 62, 67], 6, 1.9], [[62, 66, 69, 74], 8, 2.6]];
  for (const [n, at, d] of pads) t.add(chord(brass, n, d * b, { vel: 0.7, bright: 0.7 }), at * b, 0.5);
  for (const [m, at, d] of [[36, 0, 0.9], [36, 2, 0.9], [41, 4, 0.9], [43, 6, 0.9], [38, 8, 2.4]]) t.add(bass(mtof(m), d), at * b, 0.6);
  for (let k = 0; k < 8; k++) t.add(kick(r), k * b, 0.5);
  for (const k of [1, 3, 5]) t.add(snare(r), k * b, 0.4);
  t.add(roll(r, () => snare(r, { tau: 0.07 }), b, { from: 0.2, to: 0.8 }), 7 * b, 0.45);
  t.add(roll(r, () => timpani(r, mtof(45), 0.4), b, { rate: 20, from: 0.3, to: 1 }), 7 * b, 0.4);
  t.add(crash(r, { d: 2.4, tau: 0.8 }), 0, 0.35).add(crash(r, { d: 3.5, tau: 1.3 }), 8 * b, 0.5);
  t.add(timpani(r, mtof(38), 2), 8 * b, 0.9).add(kick(r), 8 * b, 0.8);
  [86, 90, 93, 98, 102].forEach((m, k) => t.add(glock(r, mtof(m)), 8 * b + 0.06 * k, 0.3));
  t.add(norm(src('cheer', 4.2, 11.4, { fin: 1.2, fout: 1.6 })), 1.4, 0.75);
  t.add(norm(src('applause_big', 3, 10, { fin: 1.2, fout: 1.6 })), 1.6, 0.5);
  return onAir(t.out(), 0.13);
});

sfx('end_count_up_tick', { group: 'End', prio: 'Should', target: -25, desc: 'One tick of the final cash counting up on the end card.' }, () =>
  mix(blip(1760, 0.035, { tau: 0.01, pw: 0.3 }), mul(osc('sine', 3520, 0.035), env(0.035, { tau: 0.006 }), 0.3)));

// ================= MENUS =================

sfx('menu_tv_power_on', { group: 'Menus', prio: 'Must', target: -17, desc: 'TV powers on for the start screen: a recorded CRT degauss, plus the rising flyback whine.' }, () => {
  const t = new Track();
  t.add(norm(src('degauss', 0, 2.3, { fout: 0.5 })), 0);
  t.add(mul(osc('sine', curve([[0, 3000], [0.6, 11000, 0.6]]), 0.9), curve([[0, 0], [0.4, 0.02], [0.9, 0]])), 0.15);
  t.add(mul(osc('sine', 15734, 1.5), curve([[0, 0], [0.3, 0.01], [1.5, 0.005]])), 0.2);
  return office(t.out(), 0.05);
});

// ================= BUILD =================

const filter = process.argv[2];
mkdirSync(OUT, { recursive: true });
const made = [];
for (const s of SOUNDS) {
  if (filter && !s.name.includes(filter)) continue;
  const t0 = Date.now();
  let x = s.fn(rng(s.name));
  if (!s.stereo) x = toMono(x);
  x = master(s.loop ? loopify(x, s.loop) : x, { target: s.target });
  if (!s.loop) x = trimTail(x);
  const bad = [...x.L, ...x.R].some(v => !Number.isFinite(v));
  if (bad) throw new Error(s.name + ' produced NaN');
  writeFileSync(join(OUT, s.name + '.wav'), wav(x, !s.stereo));
  const info = { name: s.name, group: s.group, prio: s.prio, desc: s.desc, loop: !!s.loop, stereo: !!s.stereo, seconds: +(x.L.length / SR).toFixed(3), loudness: +loudness(x).toFixed(1), peak: +(20 * Math.log10(peakOf(x))).toFixed(1) };
  made.push(info);
  console.log(`${s.name.padEnd(28)} ${String(info.seconds).padStart(6)} s  ${String(info.loudness).padStart(6)} dB  peak ${info.peak} dB  ${Date.now() - t0} ms`);
}

if (!filter) {
  writeFileSync(join(HERE, 'manifest.json'), JSON.stringify(made, null, 2) + '\n');
  const rows = g => made.filter(m => m.group === g).map(m => `<tr><td><button data-src="${m.name}.wav"${m.loop ? ' data-loop' : ''}>▶</button></td><td><code>${m.name}</code>${m.loop ? ' <span class="tag">loop</span>' : ''}</td><td>${m.prio}</td><td>${m.seconds.toFixed(2)} s</td><td>${m.desc}</td></tr>`).join('\n');
  const groups = [...new Set(made.map(m => m.group))];
  writeFileSync(join(HERE, 'preview.html'), `<!doctype html>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>No Winners SFX</title>
<style>
:root { --bg:#16181a; --fg:#e9e4d4; --dim:#9a968a; --line:#2c3030; --accent:#e8b830; }
body { background:var(--bg); color:var(--fg); font:14px/1.45 ui-monospace, Menlo, monospace; margin:0 auto; max-width:1000px; padding:24px 16px; }
h1 { font-size:18px; letter-spacing:.08em; } h2 { font-size:13px; color:var(--accent); letter-spacing:.12em; text-transform:uppercase; margin-top:28px; }
p { color:var(--dim); } table { width:100%; border-collapse:collapse; } td { border-top:1px solid var(--line); padding:6px 8px; vertical-align:top; }
td:nth-child(3), td:nth-child(4) { color:var(--dim); white-space:nowrap; } button { background:var(--accent); color:#111; border:0; width:30px; height:26px; cursor:pointer; font-size:12px; }
button.on { background:var(--fg); } .tag { color:var(--accent); font-size:11px; } code { color:var(--fg); }
</style>
<h1>NO WINNERS — SOUND EFFECTS</h1>
<p>${made.length} sounds, generated by <code>node tools/sfx/build.mjs</code> into <code>lucky-number/Assets/Art/Resources/Audio</code>. Loops repeat until stopped. Space stops everything.</p>
${groups.map(g => `<h2>${g}</h2><table>${rows(g)}</table>`).join('\n')}
<script>
const base = '../../lucky-number/Assets/Art/Resources/Audio/';
let cur = null, btn = null;
const stop = () => { if (cur) { cur.pause(); cur = null; } if (btn) { btn.classList.remove('on'); btn.textContent = '▶'; btn = null; } };
document.addEventListener('click', e => {
  const b = e.target.closest('button[data-src]'); if (!b) return;
  const same = b === btn; stop(); if (same) return;
  cur = new Audio(base + b.dataset.src); cur.loop = b.hasAttribute('data-loop'); cur.play();
  btn = b; b.classList.add('on'); b.textContent = '■'; cur.onended = stop;
});
document.addEventListener('keydown', e => { if (e.code === 'Space') { e.preventDefault(); stop(); } });
</script>
`);
  console.log(`\n${made.length} sounds written to ${OUT}`);
}
