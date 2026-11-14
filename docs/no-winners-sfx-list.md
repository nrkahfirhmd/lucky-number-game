# No Winners: SFX List

Written 2026-11-07 from the [GDD](GDD.md), the [art direction](no-winners-art-direction.md) and [art list](no-winners-art-list.md), and the game modes in `Game.cs`. The GDD puts sound in Milestone 3 (Feel).

**Priority:** Must means the core loop feels wrong without it. Should means polish. Could matches the Milestone 4 comedy work.

**Mix rule:** pillar 4, "quiet off air, loud on air". Room tone stays low and the broadcast is loud.

## Status (2026-11-08): every Must and Should is made

- 58 WAVs, 44.1 kHz 16-bit, in `lucky-number/Assets/Art/Resources/Audio`. All are synthesised by `node tools/sfx/build.mjs`, so they are original and rebuild the same every time. Pass a name fragment to rebuild just one, e.g. `node tools/sfx/build.mjs shred`.
- Audition them all in `tools/sfx/preview.html`, opened in a normal browser. `tools/sfx/manifest.json` lists each file with its length and level.
- Levels are pre-mixed to the mix rule: room tone about -40 dB, UI blips -21 to -28, desk actions about -20, broadcast -14 to -17. Put them on a mixer at 0 dB to start.
- Loops (`room_tone`, `machine_spin_loop`, `rig_dead_air`, `end_stand_by_tone`) are seamless.
- Broadcast sounds already pass through a "TV in the office" speaker filter, so play them flat.
- Variants: `machine_digit_flip_1..3` (rotate at 12 fps), `result_streak_1..6` (6 covers 6+), `room_clock_tick/tock`, `room_look_left/right`, `rig_timer_tick` plus `rig_timer_tick_final`.
- **Pause has no file.** The room is muffled in code (see below).
- Unity import rules live in `Art/Editor/NoWinnersArt.cs`: clips over about 3 s use Vorbis and the rest use ADPCM, and all are decompressed on load and preloaded.
- **Real recordings (2026-11-08).** Foley and crowds now use CC0 recordings from Freesound, kept in `tools/sfx/sources` and credited in `sources/CREDITS.md`. Some get synth layered on top. Recorded: office room tone, clock, printer, cash register, calendar page, phone pick-up and hang-up, envelope and coins, shredder, crumple, toggle switch, relay, button, rubber stamp, CRT degauss, bingo spinner (ball machine), footsteps, clipboard, cough, and the crowds (cheer, big and small applause, gasp, audience murmur). These stay synthesised: UI blips, music, voices, static, tones, signal loss, card reader.
- The build now needs `ffmpeg` on PATH to decode the recordings.
- Not made yet: the Could items (lean creak, shredder idle, ticker beeps as their own file, Milestone 4 comedy).
- **In the game (2026-11-08).** `Scripts/Sfx.cs` loads every clip from `Resources/Audio` and plays it by watching `Game`'s state each frame. `Game` only calls in for refused clicks (`desk_denied`, `rig_illegal`). Settings has a VOLUME row (saved as `volume`, default 80%). Pause muffles everything with a low-pass filter instead of a mixer snapshot. `Tests/SfxPlayTests.cs` checks a week's sounds and the volume row.

## Room and desk (off air, quiet)

| SFX | When it plays | Priority |
|---|---|---|
| Room tone: fluorescent hum, CRT whine, faint traffic outside | Always, low | Must |
| Wall clock tick | Off air, very quiet | Should |
| Camera turn whoosh, soft | A/D look to the TV, monitor or machine | Should |
| Lean in, chair creak | Space | Could |
| Hover blip on monitor UI | Hovering cards, squares or menu items | Must |
| Ticket sales cascade, quick clicks or a printer chatter | Week start, tickets land on the board | Should |
| Cash register "ka-ching", small | Sales money reaches your cash | Should |
| Calendar page flip | New week | Should |
| Inspection warning stamp or thud | Calendar shows week 4, 8 or 12 | Should |

## Desk actions

| SFX | Action | Priority |
|---|---|---|
| Phone pickup, short mumble, hang up | **Supplier** (+1 card) | Must |
| Envelope slide plus coin clink | **Bribe** (+80 cash) | Must |
| Shredder grind | **Shred**, when a square is chosen | Must |
| Shredder motor idle | Shred picking mode | Could |
| Card lost buzz | Hand full, card discarded | Must |
| Denied thunk | An action is not allowed, e.g. a second action that week | Must |

## Going live

| SFX | When it plays | Priority |
|---|---|---|
| Heavy toggle switch clack | Go Live | Must |
| "ON AIR" relay clunk plus a short studio sting | Right after the switch | Must |
| Ball machine spin loop: air blower and rattling balls | The draw | Must |
| Digit flip ticks at 12 fps | Machine number roll | Must |
| Lock clunk | Number locks, the on-air clock starts | Must |

## Rigging (on air)

| SFX | When it plays | Priority |
|---|---|---|
| Punch card feed, reader chatter, about 0.6 s | Playing a card | Must |
| Number snap blip | Board result updates | Must |
| Illegal card buzz | Result out of 00–99 or not whole, or over the inspection limit | Must |
| Card pull-back, reversed feed | Undo | Must |
| Timer tick at 2 Hz | Last 5 s before the 20 s target | Must |
| Dead-air hiss | Clock passes the target | Should |

## Stalls (comedy, repeat every 10 s past the target)

| SFX | Priority |
|---|---|
| Host bad joke, then a rimshot and a lone cough | Should |
| Sponsor jingle | Should |
| Confused audience interview murmur | Should |
| Studio panic: shuffling, a dropped clipboard, a "we're live!" whisper | Should |

## Announce and broadcast

| SFX | When it plays | Priority |
|---|---|---|
| Big red button press, chunky mechanical | Announce | Must |
| Static burst, about 0.3 s, then a hard cut | Channel change to the TV | Must |
| Show theme, short | The TV broadcast opens | Must |
| **Clean**: big fanfare, crowd cheer, ticker beeps | 0 holders | Must |
| **Small winner**: polite applause, a small "ding" | 1–3 holders | Must |
| **Scandal**: gasps, a sad trombone or stinger, alarm | 4+ holders | Must |
| Live bonus chime | Announced on time, +20 | Should |
| Streak up blip, pitch rising with the streak | Clean streak +1 | Should |
| Cash count-down ticker, fast ticks | Payout on the cut back | Must |
| Reveal beep | Monitor shows the best route after a payout | Should |
| Ticker scroll beeps | Headline after each draw | Could |

## End states

| SFX | When it plays | Priority |
|---|---|---|
| Signal loss: tube collapse "thwip" to a white line, then a test-tone hum | Bankrupt | Must |
| "PLEASE STAND BY" tone, 1 kHz | After the collapse | Must |
| Finale fanfare, longer than a clean draw | Week 12 survived | Must |
| Cash total count-up | End card | Should |

## Menus

| SFX | When it plays | Priority |
|---|---|---|
| TV power-on: degauss "thunk", then rising whine | Start screen, any key | Must |
| Menu move blip | Arrow keys or hover | Must |
| Menu confirm | Select | Must |
| Back or cancel | Esc | Must |
| Room ducks behind a muffled filter | Paused | Should |
| Slider tick | CRT strength or volume | Should |

## Milestone 4 comedy

| SFX | Priority |
|---|---|
| Phone ringing, the angry citizen calls | Could |
| Newspaper thump landing on the desk | Could |
| Teletext page chirp for the 40-year player page | Could |

## Off-air prep, rewards and backstory (added 2026-11-10)

| SFX | When it plays | Priority |
|---|---|---|
| `desk_slip_print`: short printer chatter | The reader prints the week's slip (replaces the ticket print that week) | Should |
| `desk_slip_pin`: paper flick, pin pressed in | TAKE the slip | Should |
| `desk_slip_tear`: rip and crumple | TEAR the slip (silent when going live bins it) | Should |
| `desk_rack_pick`: card lifted | Pick a special in the rack | Should |
| `desk_repunch`: three keypunch thunks | A re-punch lands in the hand | Should |
| `desk_card_place`: slide and slap | A dealt card replaces a hand card; `ui_menu_move` on the first (asking) click | Should |
| `room_lean_in` / `room_lean_out`: chair creak | SPACE or a monitor click | Should |
| `desk_cash_register` (reused) / `desk_denied` (reused) | Slip or nephew pays / slip failed, after the result | Should |
| `result_gift_card`: rising chime; `desk_card_lost` (reused) if the rack is full | A special card is won | Should |
| `stall_interview` (reused): question, uncertain answer, murmur | The TV cuts to the street interview: 2.6 s after the result, or on the first red-button press | Should |
| `intro_line`: low swell; `intro_reveal`: dark brass sting | Each backstory line; the last line | Should |

## Music (added 2026-11-10)

One loop, `music_main.wav`, for the title and the desk. Written as MIDI by `tools/music/build.mjs`, rendered in a DAW (`tools/music/don't_let_anybody_win_music.wav`), then cut to exactly 56 bars at 112 BPM (120 s) with the reverb tail folded onto the start so it loops cleanly, then made fuller: +3 dB at 80 Hz, +2 dB at 8 kHz, 1.25x stereo width, 2.5:1 glue compression, limited to -1 dB (about -12 LUFS). Imported as streaming. `Sfx.Music()` starts it on the first frame of the title screen, with no power-on or theme sting: 0.9 on the title, 0.6 at the desk, 0.35 under the backstory, 0.25 on air, fading out when a run ends. Settings has separate SFX VOLUME (`Prefs.Volume`) and MUSIC VOLUME (`Prefs.Music`) sliders.

## Notes

- About 30 Must sounds. Many share a source: one set of blips covers hover, menus, number snaps and the timer.
- Reduce-motion doesn't affect audio. Consider making the static burst quieter when it's on.
