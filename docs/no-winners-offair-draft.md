# No Winners: Off-Air and Special Dates Draft

Status: draft, written 2026-11-07. Not decided, not simulated. **2026-11-09: the in-tray and the three starting dates are playable in [desk.html](../prototypes/desk.html)** (Repair mode, toggle "Off-air in-tray" in the top bar to compare with the one-action desk). Rules in `shared.js` (`planDates`, `trayPapers`, `trayAct`), checked by `check.js`.

POC numbers, all guesses: 25 s session, 5 ticket slips (1–3 holders each) + 1 supplier + 1 minister envelope. The boss walks past the door behind you every 5.5–10 s, footsteps 1.2 s ahead, and his shadow crosses the monitor for 3 s; a shred while the shadow is on screen costs prize +40. GO LIVE ends the in-tray early. Dates: one each, shuffled over weeks 2, 3, 5, 6, 7, 9, 10, 11. Payday doubles tickets (so cash too). Holiday sells nothing, deals the card, no show. Nephew's ticket goes on a free square; landing it pays +150 and holds the streak, missing it is prize +40. `copy(poc.log)` now records `day` and `tray` per week. [GDD.md](GDD.md) still describes the current one-action desk. Parked while the game starts in Unity.

## Why

The designer played the current off-air phase and reported that it feels like a menu to choose from, not a second gameplay loop.

Likely cause: a desk action is one click, with no skill, no way to fail and no result visible that week. Better balance won't fix that. A second loop needs its own verb, several decisions per session, and a way to mess up.

The designer also wants special dates besides inspections.

## Off air: the in-tray (recommended)

Paper slides across the desk one sheet at a time, for about 20–30 seconds. The three desk actions become things the player handles.

| Paper | Player does | Result |
|---|---|---|
| Ticket slip, such as 42 with 3 holders | Shred or let pass | Shred removes those holders from the board |
| Supplier's envelope | Grab before it passes | +1 card |
| Minister's envelope | Take or refuse | +80 cash, prize +40 |

The boss patrols past the door. **If caught shredding, the player pays hush money: prize +40.** This reuses the prize, so there's no new meter.

**Why it might work**

- Shred gets a purpose: shaping which squares are free, before the number is known.
- It answers the friend's "faster, more arcade" note without touching the arithmetic.
- Off air works the hands and on air works the head, so the two halves feel different.

**Conflict to settle:** the designer said off air is the time to think, and the in-tray makes it busy. Thinking would mostly happen on air, which no longer has a deadline.

### Alternative: a visitor each week

A named person makes a request, and it comes back to help or hurt later. It's cheap and very spicy, but it's still choosing from options, so it may feel like a menu with better writing.

## Special dates

Show them on the calendar from the start, and shuffle them each run for replay value. Inspections stay on weeks 4, 8 and 12, so special dates use the other weeks. Each must change a decision, not just a number.

| Date | Effect | Decision it changes |
|---|---|---|
| **Payday** | Double tickets, double cash | Rich now, but the board fills fast |
| **Minister's Nephew** | Must make one named number win, for a big reward | Reverses the goal, and gives the arithmetic a new target |
| **Feast of 7s** | Ticket buyers pile onto lucky numbers | Shred and save differently beforehand |
| **Telethon** | Live bonus much bigger, prize doubled | Risk and speed both matter more |
| **Public holiday** | No show, in-tray only | A breather, which helps with hard-week frustration |

Start with **Minister's Nephew, Payday and Public holiday**, because they differ most from each other and from inspections.

### Unity build: Payday, Telethon, Nephew (2026-11-09)

The Unity build uses a different starting set from the `desk.html` POC above: **Payday, Telethon and Minister's Nephew**, with Public holiday dropped. Both versions are playable, so compare them in play before picking one.

**Why this set**

- **Nephew** is the only date that reverses the goal. Instead of looking for a free square, the player has to hit an exact number. It reuses the route search, and it's the funniest premise.
- **Telethon** gives the clock meaning for one week. This targets GDD risk 3: with no deadline, tension may drain away.
- **Payday** puts greed against the board. The extra tickets stay, so the cost outlasts the week.
- **Public holiday is dropped.** A week with no show leaves only off air, which is the weakest part of the game. It makes sense only once the in-tray ships.
- **Feast of 7s is dropped.** The board already favours lucky numbers, so this changes no decision.

**Rules as built** (`Rules.cs`: `Config`, `ShuffleDates`, `TierAt`, `CostOf`)

| Date | Effect | Rule detail |
|---|---|---|
| **Payday** | Tickets ×2 that week, so cash ×2 too | The extra tickets stay on the board |
| **Telethon** | Prize ×2, and Live bonus ×5 (+100) | A small winner still pays the base 120 |
| **Minister's Nephew** | 3 of his numbers marked on the board. Landing on any one pays **+200** | Counts as a small winner even with no card played, so the streak holds. More than 3 holders on his square is still a scandal. Missing pays nothing and costs nothing |

- **Placement:** one of each per run, shuffled over weeks 2, 3, 5, 6, 7, 9, 10 and 11. They are marked on the desk calendar (gold tiles: PAY, TELE, NEPH) from week 1.
- **The week before:** a phone call announces the date. The nephew's numbers are picked then, from empty squares, so off air gets a job: keep cards that reach them, or Shred holders off them.
- **On the day:** a gold HUD chip shows the date. On the Nephew's week, his squares are framed on the monitor, the preview shows the net result ("NEPHEW +80"), the receipt has a MINISTER row, and the reveal can show a "NEPHEW ROUTE".
- **Logging:** `weeks.csv` has a new `special` column.

**Why 3 numbers, not 1** (measured)

| Nephew numbers | Hand of 5 | Hand of 6 | Bot in a full run |
|---|---|---|---|
| 1 | 15% | 27% | Hit on 7% of Nephew weeks |
| 3 | about 40% | about 60% | Reachable on 38%, hit on 20% |

Hand-size figures come from a quick random-hand simulation; the bot figures come from `RulesTests.Bot_with_dates`. With one number, the week is mostly impossible, so it plays like luck rather than a plan. `Config.NephewNumbers = 1` restores the original rule.

**Bot effect on a whole run:** 61% win with dates against 64% without, from 3,000 games each. Dates add flavour without deciding the run. Rules are checked by `RulesTests`, and all 17 PlayMode tests pass.

**Differences from the POC to settle in play:** the POC Nephew pays +150 and a miss costs prize +40; here it pays +200 and a miss costs nothing. The POC uses one nephew number. The POC has Holiday, and this build has Telethon.

## Open questions

1. In-tray, visitor or something else? And should off air stay slow and thoughtful?
2. What are the in-tray's numbers: papers per session, patrol timing, and whether the boss's route can be read?
3. ~~Are the Nephew's reward and the penalty for missing the number worth a bot run?~~ Done in Unity: one number is too rare, so the build uses 3 (see above). Reward size and the miss penalty still need players.
4. ~~Do special dates shuffle fully, or does each run get one of each?~~ The Unity build uses one of each, shuffled.
5. Telethon or Public holiday as the third date?

## Next step

Once the Unity core loop runs, try the in-tray there, and judge it by playing it. Bots can't tell us whether it feels like gameplay. Update the GDD only after that.
