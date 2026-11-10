// Writes the No Winners theme as a General MIDI file: tools/music/midi/music_main.mid. Run: node tools/music/build.mjs
// One seamless loop for the title screen and the desk: a straight minor-key lounge groove in A minor, 112 BPM.
//
// Song form, played twice with the tunes handed to different instruments, then back to the top:
//   Verse (8)       low, laid-back sax over Am Dm Am E7; long-short rhythm
//   Pre-chorus (4)  Dm Em7 Fmaj7 G7: the bass climbs, the same figure steps up each bar, four-on-the-floor kick
//   Chorus (8)      the hook over Am F Bb E7: a one-bar call answered by another instrument
//   Bridge (8)      major-key lift round the circle of fifths; second time the drums drop out and build back
//
// The Bb in the chorus (a Neapolitan chord, a half step above the key's home note A) is the crooked part:
// a cheerful tune that keeps sliding onto a chord that shouldn't be there.
// The file ends exactly on the last bar line, so it loops with no gap.
import { writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const OUT = fileURLToPath(new URL('./midi/', import.meta.url));
const PPQ = 480, BPM = 112;

// ---------- SMF writer ----------

const vlq = n => { const b = [n & 0x7f]; while ((n >>= 7)) b.unshift((n & 0x7f) | 0x80); return b; };
const text = (type, s) => [0xff, type, ...vlq(s.length), ...Buffer.from(s, 'latin1')];
const chunk = (id, data) => [...Buffer.from(id), (data.length >>> 24) & 255, (data.length >>> 16) & 255, (data.length >>> 8) & 255, data.length & 255, ...data];

let seed = 7;
const rand = () => ((seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);

// A part: name, channel (9 = drums), GM program, volume, pan (0..127), and timed events in beats.
function part(name, ch, program, vol = 100, pan = 64) {
  const ev = [];
  return {
    name, ch, program, vol, pan, ev,
    note(m, t, d, v = 80) {
      const vel = Math.max(1, Math.min(127, Math.round(v + (rand() - 0.5) * 10)));
      ev.push({ t, k: 1, b: [0x90 | ch, m, vel] }, { t: t + d * 0.92, k: 0, b: [0x80 | ch, m, 0] });
    },
  };
}

function smf(title, parts) {
  const end = BARS * 4 * PPQ;
  const track = (head, ev) => {
    const out = [...head];
    let last = 0;
    for (const e of ev.map(e => ({ ...e, tick: Math.min(end, Math.round(e.t * PPQ)) })).sort((a, b) => a.tick - b.tick || a.k - b.k)) {
      out.push(...vlq(e.tick - last), ...e.b);
      last = e.tick;
    }
    out.push(...vlq(end - last), 0xff, 0x2f, 0x00);
    return chunk('MTrk', out);
  };
  const us = Math.round(60e6 / BPM);
  const marks = SECTIONS.flatMap(([at, name], i) => [...vlq(i ? (at - SECTIONS[i - 1][0]) * 4 * PPQ : 0), ...text(6, name)]);
  const conductor = chunk('MTrk', [
    0, ...text(3, title), 0, 0xff, 0x51, 3, us >> 16, (us >> 8) & 255, us & 255, 0, 0xff, 0x58, 4, 4, 2, 24, 8,
    0, 0xff, 0x59, 2, 0, 1, // A minor.
    ...marks, ...vlq(end - SECTIONS[SECTIONS.length - 1][0] * 4 * PPQ), 0xff, 0x2f, 0x00,
  ]);
  const tracks = parts.map(p => track([0, ...text(3, p.name), 0, 0xc0 | p.ch, p.program, 0, 0xb0 | p.ch, 7, p.vol, 0, 0xb0 | p.ch, 10, p.pan], p.ev));
  const head = chunk('MThd', [0, 1, 0, parts.length + 1, PPQ >> 8, PPQ & 255]);
  return Buffer.from([...head, ...conductor, ...tracks.flat()]);
}

// ---------- form and chords ----------

const FORM = [['Verse', 8], ['Pre-chorus', 4], ['Chorus', 8], ['Bridge', 8]];
const SECTIONS = []; // [first bar, marker name, type, pass, bars]
for (let pass = 1, bar = 0; pass <= 2; pass++) for (const [type, n] of FORM) { SECTIONS.push([bar, `${type} ${pass}`, type, pass, n]); bar += n; }
const BARS = SECTIONS.reduce((n, s) => n + s[4], 0);
const start = (type, pass) => SECTIONS.find(s => s[2] === type && s[3] === pass)[0];

// Chords: comp voicing (rootless, around middle C), bass root, bass fifth.
const CH = {
  Am: [[55, 60, 64, 71], 45, 52], F: [[57, 60, 64, 67], 41, 48], Bb: [[57, 62, 64, 65], 46, 53], E7: [[56, 59, 62, 65], 40, 47],
  Dm: [[53, 57, 60, 64], 38, 45], G7: [[53, 59, 64], 43, 50], C: [[52, 55, 59, 62], 36, 43], Fmaj7: [[52, 57, 60, 64], 41, 48],
  Bm7b5: [[53, 57, 62], 47, 53], Em7: [[55, 59, 62, 64], 40, 47],
};
const CHORDS = {
  Verse: ['Am', 'Dm', 'Am', 'E7', 'Am', 'Dm', 'Fmaj7', 'E7'],
  'Pre-chorus': ['Dm', 'Em7', 'Fmaj7', 'G7'], // The bass climbs D, E, F, G, and the chorus lands on A.
  Chorus: ['Am', 'F', 'Bb', 'E7', 'Am', 'F', 'Bb', 'E7'],
  Bridge: ['Dm', 'G7', 'C', 'Fmaj7', 'Bm7b5', 'E7', 'Am', 'E7'],
};
const PROG = SECTIONS.flatMap(s => CHORDS[s[2]]);

// ---------- melodies: bars of [midi, beat, beats]. 64 = E4, 69 = A4, 72 = C5, 76 = E5, 81 = A5 ----------

// Verse: low and laid back, built on a long-short rhythm (dotted quarter, eighth) so the chorus's busy eighths hit harder.
const VERSE = [
  [[69, 0, 1.5], [72, 1.5, 0.5], [71, 2, 0.5], [69, 2.5, 0.5], [64, 3, 1]],   // Am
  [[65, 0, 1.5], [69, 1.5, 0.5], [72, 2, 1], [69, 3, 1]],                     // Dm
  [[72, 0, 1.5], [71, 1.5, 0.5], [69, 2, 0.5], [67, 2.5, 0.5], [64, 3, 1]],   // Am
  [[68, 0, 1.5], [71, 1.5, 0.5], [74, 2, 2]],                                 // E7: left hanging, a question.
  [[69, 0, 1.5], [72, 1.5, 0.5], [71, 2, 0.5], [69, 2.5, 0.5], [64, 3, 1]],   // Am
  [[77, 0, 1.5], [76, 1.5, 0.5], [74, 2, 1], [72, 3, 1]],                     // Dm: starts higher this time.
  [[72, 0, 1], [76, 1, 1], [79, 2, 1.5], [76, 3.5, 0.5]],                     // Fmaj7
  [[74, 0, 1], [71, 1, 1], [68, 2, 2]],                                       // E7
];
// Little answers under the verse's held notes.
const VERSE_FILL = [null, null, null, [[76, 2.5, 0.5], [80, 3, 0.5], [83, 3.5, 0.5]], null, null, null, [[76, 2.5, 0.5], [74, 3, 0.5], [71, 3.5, 0.5]]];
// Two eighths that drop into the gap while the verse holds its first note, so the two lines interlock.
const VERSE_COUNTER = [
  [[76, 0.5, 0.5], [79, 1, 0.5]], [[77, 0.5, 0.5], [81, 1, 0.5]], [[79, 0.5, 0.5], [76, 1, 0.5]], [[80, 0.5, 0.5], [83, 1, 0.5]],
  [[76, 0.5, 0.5], [81, 1, 0.5]], [[69, 0.5, 0.5], [72, 1, 0.5]], null, null,
];

// Pre-chorus: the same knocking figure each bar, a step higher each time, then a run down into the chorus's first note.
const PRE = [
  [[74, 0, 0.5], [74, 0.5, 0.5], [72, 1, 0.5], [74, 1.5, 1], [77, 2.5, 0.5], [76, 3, 1]], // Dm
  [[76, 0, 0.5], [76, 0.5, 0.5], [74, 1, 0.5], [76, 1.5, 1], [79, 2.5, 0.5], [83, 3, 1]], // Em7
  [[77, 0, 0.5], [77, 0.5, 0.5], [76, 1, 0.5], [77, 1.5, 1], [81, 2.5, 0.5], [84, 3, 1]], // Fmaj7
  [[83, 0, 2], [79, 2, 0.5], [77, 2.5, 0.5], [74, 3, 0.5], [71, 3.5, 0.5]],               // G7: B held, then down to the chorus's A.
];

// Chorus: the hook. Every call shares one rhythm (eighth, eighth, quarter, four eighths) and ends on an upbeat that
// pulls into the next bar. The answers belong to a second instrument, so the tune is a conversation.
const HOOK = [
  [[69, 0, 0.5], [72, 0.5, 0.5], [76, 1, 1], [74, 2, 0.5], [72, 2.5, 0.5], [76, 3, 0.5], [81, 3.5, 0.5]], // Am: the call climbs.
  [[77, 0.5, 0.5], [76, 1, 0.5], [72, 1.5, 1], [69, 2.5, 0.5], [72, 3, 1]],                              // F: the answer falls back.
  [[70, 0, 0.5], [74, 0.5, 0.5], [77, 1, 1], [76, 2, 0.5], [74, 2.5, 0.5], [77, 3, 0.5], [82, 3.5, 0.5]], // Bb: same call, a half step up. The wrong chord.
  [[81, 0.5, 0.5], [80, 1, 0.5], [76, 1.5, 1], [74, 2.5, 0.5], [71, 3, 1]],                              // E7: answer, A sliding to G#.
  [[69, 0, 0.5], [72, 0.5, 0.5], [76, 1, 1], [74, 2, 0.5], [72, 2.5, 0.5], [76, 3, 0.5], [81, 3.5, 0.5]], // Am: the call again...
  [[84, 0, 1], [83, 1, 0.5], [81, 1.5, 0.5], [79, 2, 0.5], [77, 2.5, 0.5], [76, 3, 1]],                  // F: ...but this time it keeps going.
  [[70, 0, 0.5], [74, 0.5, 0.5], [82, 1, 1], [81, 2, 0.5], [77, 2.5, 0.5], [74, 3, 0.5], [76, 3.5, 0.5]], // Bb: leaps to the top.
  [[80, 0, 1.5], [83, 1.5, 0.5], [77, 2, 0.5], [76, 2.5, 1.5]],                                          // E7: G#, B, the b9 F, home on E.
];
const CALLS = [0, 2, 4, 5, 6, 7], ANSWERS = [1, 3];

// Bridge: the hook's rhythm in major, round the circle of fifths.
const BRIDGE = [
  [[77, 0, 0.5], [81, 0.5, 0.5], [84, 1, 1.5], [81, 2.5, 0.5], [77, 3, 1]],                   // Dm
  [[79, 0, 0.5], [83, 0.5, 0.5], [86, 1, 1.5], [83, 2.5, 0.5], [79, 3, 1]],                   // G7
  [[76, 0, 0.5], [79, 0.5, 0.5], [83, 1, 1.5], [81, 2.5, 0.5], [79, 3, 0.5], [76, 3.5, 0.5]], // C
  [[81, 0, 3], [79, 3, 0.5], [81, 3.5, 0.5]],                                                 // Fmaj7
  [[77, 0, 0.5], [81, 0.5, 0.5], [86, 1, 1.5], [84, 2.5, 0.5], [81, 3, 1]],                   // Bm7b5
  [[80, 0, 0.5], [83, 0.5, 0.5], [88, 1, 1], [86, 2, 0.5], [83, 2.5, 0.5], [80, 3, 1]],       // E7: the peak.
  [[81, 0, 1.5], [76, 1.5, 0.5], [72, 2, 0.5], [76, 2.5, 0.5], [81, 3, 1]],                   // Am
  [[77, 0, 1], [76, 1, 0.5], [74, 1.5, 0.5], [71, 2, 1], [68, 3, 1]],                         // E7: down into the verse.
];

// ---------- parts ----------

const D = { kick: 36, rim: 37, snare: 38, hat: 42, ohat: 46, ride: 51, crash: 49, tomL: 45, tomM: 47, tomH: 50 };

const lead = part('Lead (alto sax)', 0, 65, 100, 60);
const vibes = part('Vibes', 1, 11, 96, 44);
const glock = part('Glockenspiel', 2, 9, 70, 84);
const trumpet = part('Muted trumpet', 3, 59, 92, 88);
const keys = part('Electric piano', 4, 4, 92, 50);
const organ = part('Organ', 5, 16, 62, 76);
const strings = part('Strings', 6, 48, 70, 64);
const bass = part('Bass', 7, 33, 110, 64);
const drums = part('Drums', 9, 0, 100, 64);

// Who plays what. only(bars, picks) keeps the picked bars and rests the others.
const phrase = (p, bars, at, { shift = 0, vel = 80 } = {}) => bars.forEach((bar, i) => bar && bar.forEach(([m, b, d]) => p.note(m + shift, (at + i) * 4 + b, d, vel)));
const only = (bars, picks) => bars.map((bar, i) => (picks.includes(i) ? bar : null));
// Relay: the first half of each bar goes to one instrument, the second half to the other, so the tune ricochets between them.
const half = (bars, second) => bars.map(bar => bar && bar.filter(([, b]) => (b >= 2) === second));
const relay = (a, b, bars, at, opts) => { phrase(a, half(bars, false), at, opts); phrase(b, half(bars, true), at, opts); };
// Harmony: under each melody note, the nearest note of the bar's chord a third to a sixth lower. Always in the chord.
const pcs = name => new Set([...CH[name][0], CH[name][1]].map(m => m % 12));
const under = (bars, at) => bars.map((bar, i) => bar && bar.flatMap(([m, b, d]) => {
  const tones = pcs(PROG[at + i]);
  for (let h = m - 3; h >= m - 9; h--) if (tones.has(h % 12)) return [[h, b, d]];
  return [];
}));

// Pass 1.
const V1 = start('Verse', 1), P1 = start('Pre-chorus', 1), C1 = start('Chorus', 1), B1 = start('Bridge', 1);
phrase(lead, VERSE, V1, { vel: 86 });                                   // Verse: sax sings, vibes drop eighths into its held notes
phrase(vibes, VERSE_COUNTER, V1, { vel: 66 });                          // and answer the bar-4 and bar-8 questions.
phrase(vibes, VERSE_FILL, V1, { vel: 72 });
relay(lead, trumpet, only(PRE, [0, 1]), P1, { vel: 90 });               // Pre-chorus: sax and trumpet split every bar,
phrase(lead, only(PRE, [2, 3]), P1, { vel: 94 });                       // then the sax climbs with the trumpet harmonising under it.
phrase(trumpet, under(only(PRE, [2, 3]), P1), P1, { vel: 78 });
phrase(lead, only(HOOK, CALLS), C1, { vel: 98 });                       // Chorus: sax calls; vibes answer with glock on top.
phrase(vibes, only(HOOK, ANSWERS), C1, { vel: 86 });
phrase(glock, only(HOOK, ANSWERS), C1, { shift: 12, vel: 52 });
phrase(vibes, under(only(HOOK, [0, 2]), C1), C1, { vel: 58 });         // Vibes shadow the first calls a third below,
phrase(trumpet, under(only(HOOK, [4, 5, 6, 7]), C1), C1, { vel: 80 }); // trumpet harmonises the climb.
phrase(trumpet, only(BRIDGE, [0, 1]), B1, { vel: 86 });                 // Bridge: each pair has a lead and a harmony,
phrase(vibes, under(only(BRIDGE, [0, 1]), B1), B1, { vel: 66 });       // and the lead of one pair harmonises the next.
phrase(vibes, only(BRIDGE, [2, 3]), B1, { vel: 86 });
phrase(trumpet, under(only(BRIDGE, [2, 3]), B1), B1, { vel: 72 });
phrase(lead, only(BRIDGE, [4, 5, 6, 7]), B1, { vel: 98 });
phrase(vibes, under(only(BRIDGE, [4, 5]), B1), B1, { vel: 64 });
phrase(trumpet, under(only(BRIDGE, [6, 7]), B1), B1, { vel: 80 });
phrase(glock, only(BRIDGE, [6]), B1, { vel: 54 });

// Pass 2: the same tunes, woven differently.
const V2 = start('Verse', 2), P2 = start('Pre-chorus', 2), C2 = start('Chorus', 2), B2 = start('Bridge', 2);
relay(vibes, lead, VERSE, V2, { vel: 86 });                             // Verse: vibes start each bar, the sax finishes it,
phrase(trumpet, VERSE_COUNTER, V2, { vel: 70 });                        // trumpet drops the counter eighths in between.
phrase(glock, VERSE_FILL, V2, { vel: 56 });
phrase(trumpet, PRE, P2, { vel: 90 });                                  // Pre-chorus: trumpet leads, sax harmonises,
phrase(lead, under(PRE, P2), P2, { vel: 82 });
phrase(vibes, only(PRE, [3]), P2, { vel: 76 });                         // vibes join the run down.
relay(lead, trumpet, only(HOOK, [0, 2, 4, 5]), C2, { vel: 104 });      // Chorus: the calls ricochet sax to trumpet,
phrase(vibes, under(only(HOOK, [0, 2, 4, 5]), C2), C2, { vel: 66 });  // vibes harmonise them,
phrase(lead, only(HOOK, ANSWERS), C2, { vel: 92 });                     // and the sax takes the answers with glock on top.
phrase(glock, only(HOOK, ANSWERS), C2, { shift: 12, vel: 58 });
phrase(lead, only(HOOK, [6, 7]), C2, { vel: 104 });                    // Last climb: sax melody, trumpet and vibes stacked under.
phrase(trumpet, under(only(HOOK, [6, 7]), C2), C2, { vel: 84 });
phrase(vibes, under(under(only(HOOK, [6, 7]), C2), C2), C2, { vel: 66 });
phrase(vibes, only(BRIDGE, [0, 1]), B2, { vel: 82 });                   // Bridge: drums out. Vibes, then glock over vibes harmony,
phrase(glock, only(BRIDGE, [2, 3]), B2, { vel: 66 });
phrase(vibes, under(only(BRIDGE, [2, 3]), B2), B2, { vel: 60 });
relay(lead, trumpet, only(BRIDGE, [4, 5]), B2, { vel: 94 });            // sax and trumpet split the peak,
phrase(lead, only(BRIDGE, [6, 7]), B2, { vel: 98 });                    // then sing the way down together over the snare build.
phrase(trumpet, under(only(BRIDGE, [6, 7]), B2), B2, { vel: 82 });

// ---------- harmony, bass and drums, bar by bar ----------

const fill = t => {
  drums.note(D.kick, t, 0.25, 104);
  drums.note(D.snare, t + 1, 0.25, 96);
  for (const [m, b] of [[D.snare, 2], [D.snare, 2.5], [D.tomH, 3], [D.tomM, 3.25], [D.tomL, 3.5], [D.tomL, 3.75]]) drums.note(m, t + b, 0.25, 88 + b * 4);
};
const build = (t, from) => { for (let b = 0; b < 4; b += 0.25) drums.note(D.snare, t + b, 0.2, from + b * 10); };

PROG.forEach((name, bar) => {
  const [voicing, root, fifth] = CH[name], next = CH[PROG[(bar + 1) % BARS]][1], t = bar * 4;
  const [at, , type, pass, n] = SECTIONS.filter(s => s[0] <= bar).pop(), i = bar - at, end = i === n - 1;
  const breakdown = type === 'Bridge' && pass === 2 && i < 6; // Bridge 2 opens with the drums out.
  const approach = next > root ? next - 1 : next + 1;

  // Electric piano: lazy in the verse, pushing quarters in the pre-chorus, stabs everywhere else.
  const comp = type === 'Verse' ? [[0, 1.5, 54], [2.5, 1, 46]]
    : type === 'Pre-chorus' ? [0, 1, 2, 3].map(b => [b, 0.4, 50 + i * 5])
    : [[0, 0.75, 66], [1.5, 0.5, 58], [3, 0.5, 52]];
  for (const [b, d, v] of comp) voicing.forEach(m => keys.note(m, t + b, d, breakdown ? v - 10 : v));
  if (pass === 2 || type === 'Pre-chorus' || type === 'Chorus') voicing.forEach(m => organ.note(m, t, 3.9, type === 'Chorus' ? 56 : 44));
  if (type === 'Bridge' || (pass === 2 && type !== 'Verse')) voicing.slice(-2).forEach(m => strings.note(m, t, 4, type === 'Bridge' ? 62 : 52));

  // Bass: root, fifth, octave, then a chromatic step into the next chord. Driving eighths in the pre-chorus,
  // long notes under the breakdown. The kick only ever doubles a bass note.
  const line = type === 'Pre-chorus' ? [0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5].map((b, k) => [k === 7 ? approach : root, b, 0.5, k % 2 ? 76 : 94])
    : breakdown ? [[root, 0, 2, 84], [fifth, 2, 2, 76]]
    : [[root, 0, 1, 100], [fifth, 1.5, 0.5, 86], [root + 12, 2, 1, 94], [fifth, 3, 0.5, 80], [approach, 3.5, 0.5, 86]];
  for (const [m, b, d, v] of line) bass.note(m, t + b, d, v);

  // Drums.
  if (i === 0 && !breakdown) drums.note(D.crash, t, 2, type === 'Chorus' ? 108 : 92);
  if (breakdown) {
    for (const b of i % 2 ? [1, 2.5] : [0, 1.5, 3]) drums.note(D.rim, t + b, 0.25, 54);
    for (let b = 0; b < 4; b += 0.5) drums.note(D.hat, t + b, 0.25, b % 1 ? 40 : 56);
    return;
  }
  const cymbal = type === 'Bridge' ? D.ride : D.hat;
  for (let b = 0; b < 4; b += 0.5) drums.note(cymbal, t + b, 0.25, b % 1 ? 52 : 74);
  if (type === 'Verse' && pass === 1) {
    for (const b of i % 2 ? [1, 2.5] : [0, 1.5, 3]) drums.note(D.rim, t + b, 0.25, 60); // 3-2 clave.
    for (const b of [0, 2]) drums.note(D.kick, t + b, 0.25, 88);
    return;
  }
  if (type === 'Pre-chorus') {
    for (const b of [0, 1, 2, 3]) drums.note(D.kick, t + b, 0.25, 100);
    if (end) build(t, 60); else for (const b of [1, 3]) drums.note(D.snare, t + b, 0.25, 90);
    return;
  }
  if (type === 'Bridge' && pass === 2) { // Coming out of the breakdown: kick back, snare builds into verse 1.
    for (const b of [0, 2]) drums.note(D.kick, t + b, 0.25, 96);
    build(t, end ? 70 : 40);
    return;
  }
  if (i % 2 === 1 && !end) drums.note(D.ohat, t + 3.5, 0.5, 70);
  if (end) return fill(t);
  for (const b of type === 'Bridge' ? [0, 2] : [0, 1.5, 2]) drums.note(D.kick, t + b, 0.25, 104);
  for (const b of [1, 3]) drums.note(D.snare, t + b, 0.25, 96);
});

// ---------- write + self-check ----------

if (PROG.length !== BARS) throw new Error(`progression has ${PROG.length} bars, want ${BARS}`);
// Lock check: every onset on the 16th grid, and every kick lands with a bass note.
const ons = p => p.ev.filter(e => e.k).map(e => Math.round(e.t * PPQ));
for (const p of [lead, vibes, glock, trumpet, keys, organ, strings, bass, drums]) if (ons(p).some(t => t % (PPQ / 4))) throw new Error(p.name + ' off the 16th grid');
const bassAt = new Set(ons(bass)), kicks = drums.ev.filter(e => e.k && e.b[1] === D.kick).map(e => Math.round(e.t * PPQ));
if (kicks.some(t => !bassAt.has(t))) throw new Error('a kick lands without the bass');
// No part may restart a note that is still sounding: two lines given to one instrument would cut each other off.
for (const p of [lead, vibes, glock, trumpet]) {
  const notes = [];
  for (let k = 0; k < p.ev.length; k += 2) notes.push([p.ev[k].b[1], p.ev[k].t, p.ev[k + 1].t]);
  for (const [m, a, b] of notes) if (notes.some(([m2, a2, b2]) => m2 === m && a2 > a && a2 < b)) throw new Error(`${p.name}: note ${m} overlaps itself at beat ${a}`);
}
const buf = smf('No Winners - Theme', [lead, vibes, glock, trumpet, keys, organ, strings, bass, drums]);
let o = 0, n = 0;
while (o < buf.length) { o += 8 + buf.readUInt32BE(o + 4); n++; }
if (o !== buf.length || buf.toString('latin1', 0, 4) !== 'MThd' || n !== buf.readUInt16BE(10) + 1) throw new Error('bad SMF');
mkdirSync(OUT, { recursive: true });
writeFileSync(OUT + 'music_main.mid', buf);
console.log(`music_main.mid  ${buf.length} bytes, ${n - 1} tracks, ${BARS} bars at ${BPM} BPM = ${((BARS * 4 * 60) / BPM).toFixed(1)} s`);
console.log(SECTIONS.map(([at, name]) => `${name} @ bar ${at + 1}`).join(', '));
