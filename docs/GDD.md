# No Winners: Game Design Document

Working title. Game jam theme: Lucky Number. Written 2026-11-07 from the work in [prototypes/](../prototypes/).

**Status: test-ready, not proven.** No playtest results are recorded for any version. Every balance number below comes from bots, which calculate perfectly and feel nothing. These rules are playable in the Unity project ([lucky-number/](../lucky-number/), scene `Assets/Scenes/Desk.unity`) with placeholder art, and in [desk.html](../prototypes/desk.html) (Repair mode). The flat prototypes keep the old rules.

## 1. The game

You secretly run the national lottery, and your job is to make sure nobody ever wins. Between shows you are off air, doing dirty work at your desk to prepare. Then you go live: the machine draws a truly random number, and you change it with a hand of maths cards before announcing it to the nation.

**Hook:** luck is your enemy. The draw is the only fair thing in the game, and you undo it.

**The twist this document adds:** every clean draw rolls the prize over, so the longer you keep the nation from winning, the more a big mistake costs. A small winner you steered the draw toward is a cheap escape: it costs the base prize and your streak survives.

| | |
|---|---|
| Format | Single player, first person, seated at one desk |
| Platform | PC, mouse and keyboard, in Unity (assumed from the existing project) |
| Run | 12 weeks. About 10–15 minutes (estimate, not measured) |
| Player | Likes short score-chasing runs and dark comedy, and doesn't mind light arithmetic (assumed) |

**Goals**

- **Player goal:** survive 12 weeks with cash above 0, and finish with as much as possible.
- **Experience goal:** scheming off air, then concentration, dread and laughter on air.
- **Design goal:** a weekly decision about *what to spend and what to risk*, not just a search for the free square.

## 2. What changed from the prototypes, and why

The prototypes held two versions, Repair and Build. This document picks Repair as the base, takes two ideas from Build, and changes the rules that bot runs showed were broken.

| Change | Problem in the prototypes | Evidence |
|---|---|---|
| **Economy rebuilt** | The lose condition never fires. Every bot won 100% of 500 games, including one that never plays a card and one that lets the timer run out every week. | Measured, `check.js` |
| **Rollover prize** | The clean streak is shown but does nothing, and nothing ever makes a payout a sensible choice. | Measured, see section 6 |
| **Honest draw, no rerolls** | The pitch says the draw is truly random, but the code rerolls until a route exists. With an empty hand, the machine rolled an already-free number on 2,800 of 2,800 normal weeks, so spending every card has no downside. It also quietly deletes tickets when the board is too full. | Measured, `shared.js` |
| **Inspection weeks replace hard weeks** | Once the draw is honest, impossible weeks happen naturally, so "hard week" means nothing. Inspections give players a known danger to plan around. | Derived, tested in section 6 |
| **Revenue comes from ticket sales** | A flat +10 per week sits apart from the board. Now the same tickets that pay you also heat the board. | Derived |
| **One card per week, not two** | At two cards the hand stays full, so saving cards barely matters: 74% win against 71% for spending freely. At one card it is 65% against 55%. | Measured (the two-card figures use otherwise matching rules) |
| **From Build: the reveal** | With no guarantee of a route, players must be able to tell bad luck from a missed answer. | Assumed, needs players |
| **Parked: Build as a mode** | It removes the random draw, nothing carries between weeks, and its timeout penalty didn't work: timing out scored the same as a random valid answer (164 against 165 cash). Its biggest risk is feeling like homework. | Measured and judgement |

### Revision of 2026-11-07: after feedback on POC v1

A designer friend played POC v1 and reported: hard weeks frustrate, cognitive load is high, it needs more spice, it should be faster and more arcade, and it should have a second loop. The designer's response:

| Decision | Rule that delivers it | Answers |
|---|---|---|
| Off air is the second loop and the time to think about what to do | One desk action per week, with no clock. **The number can't be seen or changed off air** | Second loop |
| On air is where the number is manipulated with cards | Draw, cards and Announce all happen on air | Keeps the core clear |
| Going over time isn't punished, and something happens | The show stalls for comic effect. Finishing in time earns a Live bonus | Frustration, load, spice |
| Effort that lands on 0–3 holders feels rewarding | Clean draws, and a new small-winner result | Frustration |
| Arithmetic stays the core | Cards and routes are unchanged | Keeps the game's identity |

The prize cap and the way a Bribe raises the prize came from bot runs (section 6).

**Not fully answered:** the game won't feel arcade, and on-air arithmetic is unchanged, so the load complaint is eased by removing the deadline, not by removing the sums.

The rig-the-balls idea in the [redesign proposal](no-winners-redesign-proposal.md) is not adopted.

## 3. Pillars

1. **The draw is honest.** The game never secretly helps or hurts you. If it says random, it is random.
2. **The board reads first.** If an effect makes a free square harder to find, the effect loses.
3. **Every payout is a decision or a known risk.** You chose it, you failed to plan for it, or the reveal shows you it couldn't be avoided.
4. **Quiet off air, loud on air.** The back room barely moves and never hurries you, so the broadcast lands.
5. **Reward effort.** A good try that falls slightly short should still feel like getting away with it.

## 4. Core loop

One week is one round, in two halves.

**Off air: no clock. You can't see or change the number.**

1. **Sales:** new tickets land on the 00–99 board, and their money reaches your cash.
2. **Take stock:** read the board, your hand, the prize and the calendar.
3. **Desk action:** optionally do one piece of dirty work: Supplier, Bribe or Shred.
4. **Go live:** press the switch when ready.

**On air: soft clock. This is the only time cards can be played.**

5. **Draw:** the machine rolls 00–99, uniformly. Turn right to watch it, or stay at the monitor.
6. **Rig:** play operator cards to change the number. The board previews each result. If you pass the time target, the show starts stalling.
7. **Announce:** press the red button. Played cards are spent.
8. **Broadcast:** the view turns to the TV. Clean draw, small winner or scandal.
9. **Reveal:** if you paid, the monitor shows the best route you had, or says none existed.

```text
          tickets (more each week)
                   |
                   v
   +-------------> BOARD: free squares shrink <--- Shred
   |                 |
   | paying out      | which numbers are safe
   | empties a       v
   | square        RIG <--- honest roll, hand of cards <--- Supplier
   |                 |
   |                 v
   +----------- RESULT ---> Live bonus, if on time
                     | clean: streak +1
                     | small winner: streak holds
                     | scandal: streak and bribes back to 0
                     v
               PRIZE PER HOLDER <--- Bribe
```

Three things carry into the next week and change the next decision: the **board**, the **hand** and the **streak**. Cash decides how much risk you can absorb.

### Example week

Week 7, cash 1,046, clean streak 3, so the prize is 240 per holder. Next week is an inspection.

**Off air:** your hand is ×2, −3, +5, ÷3. You don't know the number yet, so you decide whether a thin hand needs Supplier, or whether to take a Bribe before the prize climbs further.

**On air:** the machine draws 37. Holder counts are illustrative.

| Move | Final number | Holders | Result | What it leaves you |
|---|---|---|---|---|
| Do nothing | 37 | 4 | Scandal, 960 | Streak back to 0, nearly all your cash gone |
| +5 | 42 | 1 | Small winner, 120 | 1 card spent, streak stays 3, and 42 is now free |
| ×2, then −3 | 74, then 71 | 0 | Clean | 2 cards spent, streak 4, prize 280 next week |

÷3 can't be played here because 37 ÷ 3 isn't whole.

## 5. Rules

### Win, lose and score

- Survive week 12 with cash above 0.
- Cash at 0 or below ends the run immediately.
- Final cash is the score. The end screen also shows clean draws and best streak.

### Board

- 100 squares, 00–99, each showing its holder count.
- Tickets stay between weeks, so free squares become scarcer.
- When a number pays out, its holders are removed and the square becomes free. So a small payout also carves out a safe square for later.

### Ticket habits

People buy numbers the way people do. If a number fits several rows, use the first match.

| Numbers | Why | Weight |
|---|---|---|
| 7, 8, 77, 88 | Lucky numbers | 8 |
| 4, 13 | Unlucky numbers | 0.25 |
| 1–31 | Birthdays | 3 |
| Everything else | No particular reason | 1 |

This gives the board a shape: low numbers run hot and high numbers stay cold, so × and + cards are escape routes.

### Cards

- Each card is an operator and a value: + and − with 1–9, × with 2 or 3, ÷ with 2, 3 or 5. Dealt at random.
- Calculation applies one card at a time, in the order played.
- A card can't be played if the result would leave 00–99 or wouldn't be whole.
- Cards can only be played on air. Played cards can be undone until Announce, then they are gone. Unplayed cards stay.
- If your hand is full, that week's card is lost. Say so on screen, because it is a real cost of hoarding.

### The draw

The machine rolls a uniform 00–99 with no rerolls. There is no route guarantee, and no route search during play. The search runs only for the reveal.

### Desk actions

Off air, you may take one action per week, before the number is known.

| Action | Effect | Cost |
|---|---|---|
| **Supplier** | +1 card, lost if your hand is full | The action |
| **Bribe** | +80 cash | Prize +40 until your next scandal, even above the streak cap |
| **Shred** | Remove up to 2 holders from one square of your choice | The action |

Shred is a bet: you choose the square without knowing the number.

### On-air clock

- The target is 20 seconds, starting when the machine locks. Nothing is on a clock off air.
- Announce on time for a **Live bonus of +20 cash**.
- Past the target, nothing is forced and no cash is lost. The show stalls every 10 seconds, in order: host's bad joke, sponsor message, confused audience interview, studio panic. Then the stalls repeat.
- Cards take about 0.6 s each to feed through the reader, which comes off the target.
- The target can be extended in settings.

### Results and rollover

**Prize per holder = 120 + 40 × clean streak + 40 × bribes taken.** The streak part stops rising at streak 6. A scandal resets both.

| Result | Holders on final number | Pays | Streak | Feel |
|---|---|---|---|---|
| **Clean** | 0 | Nothing | +1 | Big fanfare |
| **Small winner** | 1–3, and you played at least 1 card | Holders × **120**, the base prize | Holds | "Got away with it" |
| **Scandal** | 4 or more, or any holders with no card played | Holders × current prize | Back to 0 | Trouble |

A small winner needs effort. An untouched draw that happens to land on 1–3 holders is a scandal, however small.

The prize is always on screen, and the cost preview uses it.

| Streak, with no bribes | 0 | 1 | 2 | 3 | 4 | 5 | 6+ |
|---|---|---|---|---|---|---|---|
| Prize per holder | 120 | 160 | 200 | 240 | 280 | 320 | 360 |

### Inspections

- Weeks 4, 8 and 12 are inspection weeks, marked on the desk calendar from the start of the run.
- During an inspection you may play **at most one card**.
- Week 12 is the finale. It has the fewest clean routes (see section 6), so the real question is what prize and how much cash you bring to it.

### Reveal

After any payout, the monitor shows one of:

- **"A clean route was 37 ×2 −3 = 71"**, if one existed.
- **"No clean route existed. The cheapest was…"**, if none did.

## 6. Economy and balance

All values live in one config, and all are tuned against bots only. The "bot win %" column uses the bot that sees all routes, saves cards and picks desk actions by a simple rule, which wins about 72% at the start values.

| Value | Start | Bot win % at other values |
|---|---|---|
| Starting cash | 100 | 50: 71. 200: 79 |
| Ticket price | 3 cash | |
| Tickets in week 1 | 30 | |
| Extra tickets per later week | +3 | 0: 81. 6: 68 |
| Base prize per holder | 120 | 90: 79. 150: 65 |
| Rollover per clean draw | +40 | 20: 89. 60: 57 |
| Prize stops rising at streak | 6 | 4: 78. 8: 67 |
| Small-winner maximum holders | 3 | 0: 85. 2: 72. 5: 78 |
| Live bonus | +20 | 0: 67. 40: 78 |
| Bribe | +80 cash | 50: 69. 120: 73 |
| Shred | 2 holders | 1: 74. 3: 71 |
| Starting hand | 5 | |
| Cards per week | 1 | |
| Hand limit | 6 | 5: 66 |
| Cards allowed in an inspection | 1 | 2: 79 |
| Inspection weeks | 4, 8, 12 | |
| On-air target | 20 s | Not simulated |

**Live bonus at 0 also stands in for a player who always runs late:** 67% against 72%. So overtime is mildly costly over a run, even though no single stall takes anything.

### Bot results

3,000 games per bot with `node prototypes/gdd-sim.js`, all using the same simple desk rule. Copied from one run, and they move a few points between runs.

| Bot | Win % | Cash if won | Clean weeks | Small winners | Scandals | Biggest payout |
|---|---|---|---|---|---|---|
| Never rigs | 2 | 300 | 2.6 | 0 | 3.4 | 821 |
| Random legal number | 2 | 318 | 2.6 | 2.1 | 1.5 | 1,143 |
| Looks 1 card deep | 58 | 1,048 | 7.1 | 3.0 | 0.9 | 1,021 |
| Looks 2 cards deep | 67 | 1,133 | 8.0 | 2.4 | 1.0 | 1,011 |
| All routes, spends freely | 65 | 1,088 | 8.1 | 2.1 | 1.0 | 1,099 |
| All routes, saves cards | 71 | 1,189 | 8.4 | 2.0 | 1.0 | 997 |
| Farms small winners | 33 | 389 | 1.2 | 7.3 | 0.9 | 627 |

Desk policies, with the bot that sees all routes and saves cards:

| Desk policy | Win % | Cash if won |
|---|---|---|
| No desk action | 65 | 978 |
| Always Supplier | 78 | 1,159 |
| Always Bribe | 54 | 1,726 |
| Always Shred | 68 | 1,108 |
| Simple rule, mostly Shred | 73 | 1,148 |

**What this supports:**

- Ignoring the board still loses.
- Chasing clean draws beats farming small winners (71% against 33%, with about three times the cash).
- Desk actions pose a real choice: Bribe is the score play and riskiest, Supplier is the safest. An earlier Bribe that only raised the streak won on both counts (82% and 1,596 cash), so it was changed.

**What it doesn't support:**

- **Shred looks weak when blind.** It adds little over no action (68% against 65%), and more holders per Shred doesn't help the bot. The bot only clears the emptiest square, and a player may choose better. This needs players, and Shred may need a rethink.
- **Always Supplier may be the safe default.** It beats the mixed rule on survival. Watch whether players ever need anything else.
- **Small winners don't improve survival.** With the rule off, the bot wins more often (85% against 72%), though with less cash (957 against 1,199). The streak survives, so the prize stays high. The rule's case is how it feels and what it does for score, which bots can't measure.
- **Win rates for people are unknown.** Bots see every route instantly, and never go over time. Target for a first-time player is winning about 1 run in 3, which is a guess.

### Pacing

From the bot that sees all routes, saves cards and uses the simple desk rule, counting only runs still alive.

| Week | Tickets sold | Free squares | Weeks with no clean route | Cash after sales | Runs alive |
|---|---|---|---|---|---|
| 1 | 30 | 76 | 0% | 270 | 100% |
| 3 | 36 | 47 | 2% | 515 | 100% |
| **4, inspection** | 39 | 37 | 18% | 648 | 100% |
| 6 | 45 | 24 | 10% | 906 | 99% |
| 7 | 48 | 19 | 17% | 1,046 | 99% |
| **8, inspection** | 51 | 15 | 58% | 1,178 | 97% |
| 10 | 57 | 10 | 37% | 1,356 | 89% |
| 11 | 60 | 9 | 47% | 1,450 | 86% |
| **12, inspection** | 63 | 8 | 77% | 1,510 | 82% |

Losses cluster late, which is the intended shape. **Late weeks without a clean route are barely fewer than before** (week 12 was 85%). What changes is what happens next: a small winner on those weeks costs less and keeps the streak, so the hard week hurts less rather than being rarer.

## 7. Presentation

### The room

**Decided (2026-11-06): first person at the desk.** See [no-winners-spatial-notes.md](no-winners-spatial-notes.md). Playable reference: [desk.html](../prototypes/desk.html).

The player has one seat, three look targets and no walking. This limit is a scope rule, because every prop invites another.

| Look target | Contains | Used for |
|---|---|---|
| Left: **TV** | The public broadcast | Announce and result |
| Centre: **Monitor and desk** | Board on an in-world CRT, cards, red button, undo, calendar, clock, phone, shredder, go-live switch | All of play |
| Right: **Machine** | The honest draw | The roll, on air |

Controls: click cards and buttons, A/D or arrow keys to look, Space to lean in.

The Announce sequence is the signature moment: press the button, static burst, the view turns to the TV, and the show smiles at the nation.

### Art direction

**Decided (2026-11-07): Teletext room, Marquee screens.** A drab office, with the show's gaudy world on the screens. Playable as **X Chosen** in [desk.html](../prototypes/desk.html). Palette, type, motion and CRT specs are in [the art direction doc](no-winners-art-direction.md), and the asset list is in [the art list](no-winners-art-list.md).

Marquee has the weakest free-square read of the five directions, which pulls against pillar 2, and rollover makes a missed free square more expensive. If players miss them, try the teal outline the art doc names as the first fix.

Two older points in the art doc no longer apply:

- **Heat:** crowded squares light up. The "darker as holders increase" wording, and the legend in `repair.html`, are out of date.
- **CRT:** it runs as a shader on the in-world monitor and TV, not as a full-screen filter with pointer warp.

The art list needs updates for these rules: the on-air clock and timer tag are no longer Build-only, and it needs a desk calendar, an on-screen prize display, a phone, a shredder, a bribe envelope and a go-live switch.

### Comedy

All cheap, all text or simple props, and all tied to real game state.

- **Stalling:** the host's bad joke, a sponsor message, a confused audience interview, then studio panic. This is where the premise gets to be funniest, so write these first.

- **The host strains with the streak.** "NO WINNER. AGAIN. WHAT A SHAME." The rollover gives this a mechanical reason: the show gets louder as the prize grows.
- **Ticker:** one headline after each draw. "NUMBER 77 STILL DUE, SAY EXPERTS."
- **Small winners on air:** the show treats that person as a national hero, while you quietly count what you saved.
- **The 40-year player:** one fixed square with a small marker. Selecting it shows their story, and their letters pile up on the desk.
- **Bankruptcy:** the broadcast loses signal and collapses to "PLEASE STAND BY".

Use no real lottery brands, logos or show names. "The Big Draw" and "No Winners" both still need a name check.

### Onboarding

A memo on the desk gives the job in two lines. There is no tutorial screen. On a first run, the first week's roll and hand are seeded so one card reaches a free square. Rollover is explained by the host at the first clean draw, inspections by a phone call the week before the first one, and desk actions by a note on the desk in week 2.

### Accessibility

- On-air target extended, in settings.
- CRT strength 0–100%. At 0 the game must be fully playable.
- Reduce-motion setting: no roll, noise, static or marker blink.
- At most 3 flashes per second, and no full-screen white flashes.
- Safe and danger are never colour alone: free squares differ in shape, and counts are printed.
- Hover shows exactly where a card lands, so players never need to do the arithmetic in their head.

## 8. Production

**Engine:** Unity 6000.4.5f1, URP 17.4.0, Universal Renderer. Rules are one plain C# class with no scene dependencies (`Assets/Scripts/Rules.cs`), with the rule checks in `Assets/Tests`. `desk.html` stays the reference for layout, cameras and the screen shader. Each week is logged to `weeks.csv` in the persistent data folder, for section 9.

### Build order

Jam deadline is **14 Nov 2026, 8:00 PM**, so each milestone is a valid place to stop.

1. **Rules in the prototype.** Done: `desk.html` plays this revision. The wider playtest is postponed: **decided 2026-11-07 to start Unity now**, so the Unity build will be what gets tested.
2. **Playable in Unity.** Done, pending a person playing it: board, cards, Announce, TV, machine, week loop, end screens, placeholder art. Not yet: title screen, settings (long target, CRT strength, reduce motion).
3. **Feel.** Static cut, machine roll, button, cash counting, sound.
4. **Art.** Chosen direction, CRT shader, room dressing.
5. **Comedy.** Stalling first, then ticker and host lines, then the 40-year player.

### Cut, and when to add it back

| Feature | Add it only if |
|---|---|
| Swap card (37 becomes 73) | Hands feel flat in playtest. In one early bot test, on a looser economy, it barely changed results. |
| Sales rising with the streak | Rollover feels too abstract. In bot tests it made almost no difference to results, so it was removed. It is still good flavour for the host's lines. |
| Auditor or suspicion meter | Rollover and inspections don't create enough pressure. It would be a second meter, working against the load complaint, so try tuning first. |
| More desk actions, such as planting your own winner | Playtest shows the three actions feel thin. |
| An on-air twist, such as a leak that adds holders to your number | On air feels too routine. Tested in bots, and it cost about 14 points of win rate. |
| Rig-the-balls arcade mode ([proposal](no-winners-redesign-proposal.md)) | Players reject arithmetic even when it's untimed. |
| Build as a mode or special week | Playtest prefers it (section 9), or the game has time for a one-off emergency broadcast. |
| Shop, meta-progression, multiple endings | The game grows beyond a jam. They answer no current question. |

## 9. Validation

### Hypotheses

| Hypothesis | Status | We would doubt it if |
|---|---|---|
| Removing the deadline lowers the load complaint | Assumed | Players still call it tiring, or stop reading numbers |
| Off air feels like a second loop, not a pause | Assumed | Players skip the desk, or describe it as a menu |
| Desk choices made without knowing the number feel like bets, not guesses | Assumed | Players can't say why they picked an action |
| Stalling is funny, and players still want to finish | Assumed | Nobody cares about the Live bonus, or they wait to see the jokes every week |
| Small winners feel rewarding | Assumed | Players react to them as failures |
| Honest draws, rollover and cash pressure make players weigh spending against risk | Plausible, bots only | Players always take the clean route, or always the first one they find |
| Finding routes feels like a puzzle, not homework | Assumed | They reach for paper or a calculator |
| The desk view adds more than it costs | Assumed | Flat `repair.html` is equally wanted, or leaning in to read gets in the way |
| The reveal makes failure feel fair | Assumed | Players still blame the game after seeing it |

### First test



**Build:** the Unity build with this document's rules. No new art, sound or comedy.

**Who:** 5 people who haven't seen the game, if possible. Don't explain anything. Each plays until they stop wanting to, up to three runs. Also have the friend replay it, and compare against the POC v1 notes.

**Watch, before asking anything:**

- Do they understand the goal within week 1?
- How long do they spend off air, and do they use the desk?
- Do they run over time on purpose? Do they laugh at the stalling?
- Do they ever choose a small winner when a clean one was reachable? Do they say why?
- Do they look ahead to the calendar and change what they do?
- Do they use the machine and TV looks, or ignore them?
- After a loss, do they start another run unprompted?

**Log for each week:** desk action, off-air seconds, roll, final number, cards spent, clean route available, holders, result, prize, cash, on-air seconds.

**Then ask:** "What were you trying to do? When did you feel clever? When did you feel cheated?"

### Decisions

These thresholds are starting guesses, set now so results can't be rationalised later.

| Result | Decision |
|---|---|
| At least 3 of 5 replay unprompted, and at least 3 use the desk with varied choices | Keep these rules and move to Unity |
| They replay but always take the same desk action | Rebalance the actions and retest once. If still the same, cut the weak ones |
| They understand it but don't replay, and late weeks feel impossible | Reduce tickets per week first, see section 10 |
| They call it homework, even untimed | Show every reachable square for the selected card, then retest. If that doesn't help, lower the arithmetic: fewer ×, ÷ |
| They clearly prefer Build's run | Reopen the Repair or Build decision, using the rollover in either |

## 10. Risks

Ordered by how much they could change the game.

1. **On-air arithmetic is unchanged.** The friend's load complaint may survive, since only the deadline went. The first fix is showing every reachable square for the selected card.
2. **Off air feels like a menu, not a second loop.** The designer played it and said so. One action from three options, without knowing the number, is thin. Replacement ideas and special dates are in the [off-air draft](no-winners-offair-draft.md), not yet decided.
3. **No deadline may drain the tension.** With a small penalty for overtime, players may take as long as they like every week. Stalling has to make them want to finish, or enjoy waiting, and that's unknown.
4. **Late weeks may still feel impossible.** No clean route on about 37–47% of weeks 10–11 and 77% of week 12. Small winners soften this but don't remove it. The first fix is fewer tickets per week, with the ticket price raised to hold income.
5. **Blind Shred may be a dud, and Supplier may be the default.** See section 6.
6. **One late scandal can still end a run.** Competent bots' biggest payout averages about 1,000–1,100. Check whether players see it coming.
7. **Bots can't test the feel rules.** Small winners, stalling and the Live bonus all exist for how they feel, and the numbers can't speak to that.
8. **Thinking time could make the run long.** The 10–15 minute estimate predates the soft clock. Measure it in the first test.
9. **The hand may still feel full.** Bots hold about 5 of 6 cards for most of the run. Hand limit is the next knob: at 5 the win rate drops to 66%.
10. **The first-person board may be too small to read.** Readable at 1280×720, untested on small screens. The fallback is a closer default camera.
11. **Re-announcing the same free number has no penalty.** This isn't designed for yet. Watch for it in logs, and only add a rule if it becomes a pattern.
12. **Prop creep.** The phone, shredder and envelope stay on the desk. Hold the line at one seat and three look targets.

**What this document can't establish:** that any of this is fun. Rules and bots can only show that a decision exists and isn't trivially solved.

## 11. Decisions

Settled 2026-11-07:

| Decision | Result |
|---|---|
| Jam deadline | **14 Nov 2026, 8:00 PM.** Cut scope to fit. |
| Clock | **None off air. A soft 20 s target on air**, with no punishment for going over. Replaces the earlier 45 s clock with a punishment. |
| Card fiction | **Punch cards and reader** (about 0.6 s per card, which comes off the on-air target). Others stay in the [spatial notes](no-winners-spatial-notes.md) and the POC's Cards menu. |
| Art | **C. Marquee screens in the A. Teletext room**, as already set in [art-direction](no-winners-art-direction.md). |
| Repair or Build | **Repair.** Build stays parked. |
| Font | **Press Start 2P.** SIL Open Font License, free to use and ship. Bedstead is dropped. |
| Name | Undecided: "No Winners" or "The Big Draw". Decide later, with a name check before release. |
| "Like Inscryption" | The friend meant something to do besides playing cards. **Here, off air and its desk actions.** |
| Core | **Arithmetic stays the core.** The rig-the-balls proposal is not adopted. |
| On air and off air | **On air:** manipulate the number with cards. **Off air:** Bribe, Supplier, Shred and similar, and the number can't be manipulated. |
| The number off air | **Hidden.** Seeing it would move the thinking off air. |
| Rewarded results | **"0, 1, 2, 3" means holder counts on the final square.** |
| Past the on-air target | **Something happens, with no gain:** the show stalls, and the Live bonus is missed. |

Still open:

| Question | Default in this document | Settled by |
|---|---|---|
| Are these three desk actions the right ones? | Yes, as my proposal. You named them, plus "etc." | You, then playtest |

## 12. Source map

| File | Role now |
|---|---|
| [no-winners-prototype.md](../prototypes/no-winners-prototype.md) | Original Repair design. This document replaces its rules. |
| [no-winners-build-prototype.md](../prototypes/no-winners-build-prototype.md) | Parked Build design |
| [no-winners-art-direction.md](no-winners-art-direction.md) | Art decision and spec, with the two exceptions in section 7 |
| [no-winners-art-list.md](no-winners-art-list.md) | Asset list, which needs the updates in section 7 |
| [no-winners-spatial-notes.md](no-winners-spatial-notes.md) | Why first person |
| [desk.html](../prototypes/desk.html) | Playable first-person POC. Repair plays **this document's** rules, and Build keeps its parked rules. |
| [no-winners-offair-draft.md](no-winners-offair-draft.md) | Draft: in-tray off-air loop and special dates, not decided |
| [no-winners-redesign-proposal.md](no-winners-redesign-proposal.md) | Rig-the-balls alternative, not adopted. Its prototype was deleted. |
| [repair.html](../prototypes/repair.html), [build.html](../prototypes/build.html), [spatial-poc.html](../prototypes/spatial-poc.html), [art-frame.html](../prototypes/art-frame.html) | Flat versions and look-dev, with the old rules |
| [check.js](../prototypes/check.js) | Rule checks and bots for the **old** rules, plus checks for this document's rules as `desk.html` plays them |
| [gdd-sim.js](../prototypes/gdd-sim.js) | Bots for **this document's** rules. Try a variant with `GDD='{"prize":90}' node prototypes/gdd-sim.js` (takes a few minutes) |

`prototypes/` is in `.gitignore`, so links into it and `gdd-sim.js` work locally only unless that changes. `docs/` is not ignored.
