// Downloads any recorded source listed in sources/sources.json that is missing, then rewrites sources/CREDITS.md.
// Run: node tools/sfx/fetch.mjs, then node tools/sfx/build.mjs. Every source is CC0 from Freesound (HQ preview).
// Freesound's CDN rate-limits bursts with 403, so this goes slowly and simply reports what it could not get.
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const DIR = fileURLToPath(new URL('./sources/', import.meta.url));
const list = JSON.parse(readFileSync(DIR + 'sources.json', 'utf8'));
const missing = [];

for (const s of list) {
  const file = DIR + s.name + '.ogg';
  if (existsSync(file)) continue;
  const res = await fetch(s.preview, { headers: { Referer: 'https://freesound.org/' } });
  if (!res.ok) { missing.push(`${s.name} (HTTP ${res.status})`); continue; }
  writeFileSync(file, Buffer.from(await res.arrayBuffer()));
  try { execFileSync('ffprobe', ['-v', 'error', '-show_entries', 'stream=codec_name', file]); console.log('got', s.name); }
  catch { execFileSync('rm', [file]); missing.push(`${s.name} (not audio)`); }
  await new Promise(r => setTimeout(r, 3000));
}

const have = list.filter(s => existsSync(DIR + s.name + '.ogg'));
writeFileSync(DIR + 'CREDITS.md', `# Recorded sources

All CC0 1.0 (public domain), from [Freesound](https://freesound.org). Credit is not required, but given anyway.
Files are Freesound's HQ previews. The build trims, layers and processes them. See \`tools/sfx/build.mjs\`.

| File | Sound | Author |
|---|---|---|
${have.map(s => `| \`${s.name}.ogg\` | [${s.title}](${s.page}) | ${s.author} |`).join('\n')}
`);
console.log(missing.length ? `still missing, synth fallback used: ${missing.join(', ')}` : 'all sources present');
