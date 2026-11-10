# No Winners — Codebase Memory & Agent Guide

> **Working Title / Jam Theme:** No Winners (Lucky Number)  
> **Platform / Engine:** Unity 6 (URP, Input System), PC, first-person seated desk view.  
> **Repository Root:** `/Users/nurkahfirahmada/WERK/Game Development/lucky-number-game`

---

## 1. Project Overview & Premise

In *No Winners*, the player secretly runs the national lottery from a drab back-office desk. The player's job is to ensure **nobody ever wins** the lottery:
1. **The Draw is Honest:** The ball machine rolls a truly random number (00–99).
2. **The Rig is Mathematical:** Before announcing to the nation, the player plays cards (`+`, `-`, `×`, `÷`, and specials) from their hand to manipulate the drawn number towards empty or low-ticket squares.
3. **The Rollover Trap:** Every clean draw (0 ticket holders) increases the rollover prize (`+40` cash/week up to a cap). If a draw lands on holders (>3), a scandal payout drains cash. Small winners (1–3 holders) pay the base prize (`120`) and preserve the streak without triggering scandal.
4. **Run Format:** 12 weeks. Starting cash: `100`. Reaching `<= 0` cash loses immediately; surviving 12 weeks wins.

---

## 2. Codebase Map & Directory Layout

```
lucky-number-game/
├── AGENTS.md                          # Universal codebase memory (Antigravity, Claude, Codex)
├── CLAUDE.md                          # Link / mirror for Claude Code sessions
├── GEMINI.md                          # Link / mirror for Gemini sessions
├── docs/                              # GDD and design specs
│   ├── GDD.md                         # Core design doc (v1 base, bot simulations)
│   ├── no-winners-offair-v2-draft.md  # v2 off-air rules (slips, rack, action costs)
│   ├── no-winners-art-direction.md    # Art direction: Teletext office + Marquee CRT screens
│   ├── no-winners-sfx-list.md         # Master SFX cue list & priorities
│   └── no-winners-spatial-notes.md    # Desk geometry, angles, camera focus
├── lucky-number/                      # Unity Project
│   ├── Assets/
│   │   ├── Art/                       # Generated 3D models (OBJ) and palette textures
│   │   ├── Resources/
│   │   │   ├── Audio/                 # Synthesized WAV audio clips (procedural)
│   │   │   └── Cursors/               # Custom cursor textures (arrow, hand, no)
│   │   ├── Scenes/
│   │   │   └── Desk.unity             # Primary game scene
│   │   ├── Scripts/                   # Assembly: NoWinners.asmdef
│   │   │   ├── Rules.cs               # Pure C# game simulation, economy, bot solver
│   │   │   ├── Game.cs                # MonoBehaviour game loop, camera, input, logging
│   │   │   ├── Screens.cs             # Pixel-grid CRT renderer for Monitor, TV, HUD
│   │   │   ├── Room.cs                # 3D props, materials, colliders, dynamic textures
│   │   │   ├── Canvas8.cs             # 8x8 software rasterizer onto Texture2D
│   │   │   ├── Pointer.cs             # Custom software cursors, hover tags, focus labels
│   │   │   └── Sfx.cs                 # Event-driven audio manager watching game state
│   │   └── Tests/                     # Assembly: NoWinners.Tests.asmdef
│   │       ├── RulesTests.cs          # Fast NUnit logic tests & bot simulations
│   │       ├── DeskPlayTests.cs       # Integration tests (raycasts, prop clicks, screenshots)
│   │       └── SfxPlayTests.cs        # Audio cue playback verification
├── prototypes/                        # Browser prototypes (HTML/JS) used for balance
│   ├── desk.html                      # Interactive 2D desk prototype (Repair mode)
│   ├── screens.html                   # Screen visual layout prototype
│   ├── check.js                       # Bot simulation script for testing balance
│   └── shared.js                      # Shared prototype rules & pathfinding
└── tools/                             # Node.js procedural asset generators
    ├── art/
    │   ├── build.mjs                  # Generates OBJ models & palettes into Assets/Art
    │   ├── textures.mjs               # Procedural texture generator
    │   └── font8x8.json               # 8x8 bitmap font glyph definitions
    └── sfx/
        ├── build.mjs                  # Synthesizes all SFX WAV files into Resources/Audio
        ├── dsp.mjs                    # DSP algorithms (filters, envelopes, oscillators)
        └── kit.mjs                    # Instrument & sound synthesis presets
```

---

## 3. Core Mechanics & State (Rules.cs / v2 Tuning)

### Run & Economy Constants (`Config`)
- `Weeks`: 12 weeks per run.
- `StartCash`: 100.
- `TicketPrice`: 3 cash per ticket.
- `Tickets1`: 30 sold week 1, growing by `+10` per week (`TicketsGrowth`).
- `StartHand`: 4 cards week 1, deal 1 card/week (`Draw = 1`). `HandLimit`: 5 cards (cards dealt over limit are lost).
- `Base Prize`: 120, `Rollover`: +40 per clean week up to `MaxStreakPrize = 6`.
- `Bribe`: +80 cash, permanently raises prize by +40, resets clean streak.
- `LieLow`: Costs 5 cash, adds calm that temporarily offsets rollover prize down to base (120).
- `Supplier`: Costs 60 cash, deals 1 extra card.
- `Shred`: Clears 1 square on the 00–99 board; holders rebuy into immediate row neighbors (`square - 1` or `square + 1`).

### Card System
- **Arithmetic Cards:** Op `+`, `-`, `*`, `/`.
  - Add/Sub: 1..9. Mul: 2, 3. Div: 2, 3, 5 (division only legal if evenly divisible).
- **Special Cards (`IsSpecial`):**
  - `S` (SWAP): Swaps tens and units digits (e.g. 37 -> 73, illegal if tens == units).
  - `M` (MIRROR): 99 - v (e.g. 10 -> 89).
  - `T` (SHIFT): Shifts tens digit by `+-10` or `+-20` within 00-99.
  - `W` (TWIN): Copies tens digit to units (e.g. 47 -> 44, illegal if tens == units).
  - `R` (WRAP): Adds value with modulo 100 wrap-around (`(v + X) % 100`).
  - `J` (JAM): Randomly redraws the units digit (random outcome on air).
  - `D` (REDRAW): Redraws the entire number 00–99 (random outcome on air, max 1 per run).
- **Special Rack (`RackMax = 3`):** Specials sit in a wooden rack below the monitor. Player can re-punch an arithmetic hand card into a special during off-air for free. Unused re-punched specials are destroyed after the show.

### Printer Slips (from Week 3, except Inspections)
- `Sponsor`: Ends in 7 -> +120 reward, -60 fine if failed.
- `Quiet`: Play <= 1 card -> +100 reward, fails: Prize +40.
- `Rush`: Announce within 10s -> +80 reward, fails: Prize +40.
- `Favour`: Small winner (1–3) -> +150 reward, -50 fine if clean.
- `Bulk`: Paid immediately (+30), adds 10 tickets to squares 60–69.
- `Collector`: Clean draw -> grants 1 special card, fails: Prize +40.

### Special Dates
- `Payday`: 2x ticket sales and cash.
- `Telethon`: 2x prize, 5x live bonus (Live bonus base = 20 for finishing within 20s).
- `Nephew`: Minister wants 1 of 3 designated numbers hit; landing on one awards +200 cash & a special card.
- `Inspections` (Weeks 4, 8, 12): Player can play at most 1 card on air.

---

## 4. Architecture & Script Relationships

### `Game.cs` (Main Loop & Controller)
- Implements `Mode` states: `Start`, `Title`, `Play`, `Paused`, `Settings`.
- Manages camera viewpoints: Monitor (`camLook = LookMonitor`), Leaned-in (`LookLean`), TV (`LookTv`), Machine (`LookMachine`).
- Spacebar triggers `Focus` mode (leaned-in camera with hand fanned along bottom).
- Animates card insertion into reader slot (`FeedReader` coroutine, 0.58s lock).
- Handles on-air timer (`Target = 20s`). Stalls trigger comically every 10s past target (`CheckStall()`) without cash penalty.
- Records run telemetry to `weeks-v2.csv` (`Application.persistentDataPath`).

### `Screens.cs` & `Canvas8.cs` (CRT Visuals)
- Custom software rasterization onto `Texture2D` at 640x360 with 8x8 font grid.
- Full CRT post-processing shader applied to screens (`Room.SetCrt(1)`).
- Monitor shows: 10x10 board with color-coded holder tiers, stats header, clock, pinned slip, punch rack.
- TV shows: Studio background, animated draw result, host commentary, comic stall cards, ticker tape.

### `Room.cs` & `Pointer.cs` (3D Environment & Interaction)
- Loads `NW_RoomArt` with procedural dynamic textures attached to materials.
- Sets up colliders on physical objects for raycasting: Monitor, Big Red "GO" Button, Undo Button, On-Air Lever, Calendar, Action props (Bribe envelope, Shredder, Phone, Slip, Rack).
- `Pointer.cs` handles cursor states: `Arrow` (default), `Hand` (clickable), `No` (denied/locked, with contextual hover explanation).

### `Sfx.cs` (Audio Pipeline)
- Fully procedural audio: watches game state transitions each frame.
- Sound clips load dynamically from `Resources/Audio/` (generated by `tools/sfx/build.mjs`).
- Pre-balanced for quiet off-air ambient and loud on-air broadcast. Low-pass filter ducks sound when paused.

---

## 5. Development Workflows & Commands

### Running Tests
- **Unit Tests:** Fast logic tests run via Unity Test Runner (`RulesTests.cs`).
- **Play Tests:** Real interaction & render checks in `DeskPlayTests.cs` (writes frame snapshots to `Logs/Shots/`).
- **Audio Tests:** Verify audio cues in `SfxPlayTests.cs`.

### Asset Generation
- **3D Art & Textures:**
  ```bash
  node tools/art/build.mjs
  ```
  Outputs models and textures directly into `lucky-number/Assets/Art/`.
- **Audio Synthesis:**
  ```bash
  node tools/sfx/build.mjs
  ```
  Synthesizes procedural WAV files into `lucky-number/Assets/Art/Resources/Audio/`.

### Bot Balance Simulation
- In `prototypes/`:
  ```bash
  node prototypes/check.js
  ```
  Runs fast headless Monte Carlo simulations across 500+ runs to verify win rates and difficulty curves.

---

## 6. Coding Conventions & Best Practices

1. **Keep Rules Pure:** Logic in `Rules.cs` (`Run`, `Config`, `Card`, `Routes`) must never depend on `UnityEngine` or scene objects. This keeps headless testing instant and deterministic.
2. **Deterministic PRNG:** `Run` accepts a seeded `System.Random`. Tests use fixed seeds for repeatable assertions.
3. **8-Pixel Grid Alignment:** Any additions to `Screens.cs` or HUD rendering must stay on the 8px grid and utilize `Canvas8` primitives.
4. **Terse, Explicit Comments:** Only comment non-obvious design reasons or references to specific GDD sections. Keep diffs focused and minimal.
