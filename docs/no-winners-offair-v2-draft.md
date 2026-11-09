# No Winners: Off-Air v2 Draft

Status: **built in Unity 2026-11-09, bot-tuned, not playtested.** All four steps are in (`Rules.cs`, desk props, tests). See [Built](#built-2026-11-09) for what changed from this draft and what the bots said. [GDD.md](GDD.md) still describes the v1 rules.

## Why

- **Off air feels like a menu.** One free action per week, chosen blind, with no visible result.
- **Actions cost nothing.** Bots: always Supplier wins 78%, no action 65%. Skipping is never right, so choosing isn't a decision.
- **The game is too easy for cards to matter.** The bot that saves cards wins 71%, the one that spends freely 65%. A 6-point gap means managing the hand barely matters.

## Summary

| Part | Change |
|---|---|
| 1. Printer | One slip a week, from week 3: a bet on tonight's draw. TAKE or TEAR |
| 2. Desk actions | Supplier, Bribe and Shred stay, one a week, and each now costs something. Doing nothing is a fourth choice: lie low |
| 3. Special dates | Payday, Telethon and Nephew, as built. Nephew now also pays a special card |
| 4. Difficulty | Smaller hand, more tickets, so saving cards is how you survive |
| 5. Special cards | SWAP, MIRROR, SHIFT, TWIN, JAM, REDRAW and WRAP. They sit in their own inventory and are used by re-punching an arithmetic card into them |

A week off air, in order:

1. **Sales and date:** tickets land, and the date is on the calendar and HUD.
2. **Printer:** one slip prints. TAKE or TEAR.
3. **Card punch:** optionally re-punch arithmetic cards into special cards.
4. **Desk action:** Supplier, Bribe, Shred, or lie low.
5. **Go live.**

All of it is untimed. Pillar 4 holds: off air is quiet and never hurries you.

## 1. The printer

A teleprinter on the desk prints one slip at the start of off air. The slip says what it wants on air, what it pays, and what failing costs. **TAKE** pins it to the monitor's edge. **TEAR** throws it away, for free.

When it prints:

- Not in weeks 1–2, while the board and desk actions are being learned.
- Not on inspection weeks.
- Never a slip that clashes with the week's date, for example no Favour on Nephew week.

That's at most about 6 slips a run.

| Slip | Condition on air | Reward | Fail |
|---|---|---|---|
| **Sponsor:** "Lucky 7 night" | Final number ends in 7 | +120 | −60 cash |
| **Quiet week:** "Inspector in the building" | Play at most 1 card | +100 | Prize +40 |
| **Rush job:** "Network wants it fast" | Announce within 10 s | +80 | Prize +40 |
| **Favour:** "Let a few win" | A small winner (1–3 holders) | +150 | −50 if clean |
| **Bulk order:** "10 tickets on the 60s" | None: paid now | +30 now | 10 extra tickets on 60–69 |
| **Collector:** "Wants a clean show" | Clean draw | **1 special card** | Prize +40 |

- Slip rewards are the main way in for special cards (section 5), so the printer and the card inventory feed each other.
- The receipt shows the result, for example "SPONSOR: ENDED IN 7, +120".
- **The design rule:** a slip is a bet that the player judges against their hand and the board. If bots show always-TAKE or always-TEAR is best for a slip, retune that slip.

## 2. Desk actions, with costs

Still one per week, chosen before the number is known. Each costs a different resource, so the question is which one you can afford to spend this week.

| Choice | Effect | Cost | Puts at risk |
|---|---|---|---|
| **Supplier** | +1 arithmetic card | **−60 cash**, paid even if a full hand loses the card | Your buffer against a scandal |
| **Shred** | Clears one square completely | **Its holders rebuy next door:** they spread across the squares ±1 either side | The board: one safe square, hotter neighbours |
| **Bribe** | +80 cash | **Prize +40 for the rest of the run.** A scandal resets the streak, not the bribes | Every later mistake costs more |
| **Lie low** | Nothing | **Prize −20**, never below the base 120 | Nothing. The quiet choice |

- **Lie low** makes skipping a decision, and it's the only way to bring the prize back down. Restraint pays slowly; greed pays now.
- **Shred spreading** turns a blind pick into a board bet. Clear 47 and you heat 46 and 48. Shred holders that sit at the edge of a row stay in that row; they don't wrap.
- Shred and Supplier can now serve a slip. For example, Sponsor wants a 7 ending, so you clear a _7 square your cards can reach.

## 3. Special dates

As built in Unity: one of each per run, shuffled over weeks 2, 3, 5, 6, 7, 9, 10 and 11, with a phone call the week before.

| Date | Effect | Change in v2 |
|---|---|---|
| **Payday** | Tickets ×2, cash ×2 | None |
| **Telethon** | Prize ×2, Live bonus ×5 | None |
| **Minister's Nephew** | Land on one of his 3 numbers for +200; counts as a small winner | **Also pays 1 special card** |

Dates and slips stack, so a Telethon with a Rush job slip is a loud week. That's intended, but at most one date and one slip a week.

## 4. Difficulty: make card management matter

**Target:** the smart bot wins about **50%**, down from 72%. The bot that spends freely should trail the card-saving bot by **at least 15 points**, against 6 now. Turn these knobs in order, re-running the bots after each, and stop when both targets are hit.

| Knob | Now | v2 start | Effect |
|---|---|---|---|
| Starting hand | 5 | **4** | Week 1 is tighter. Spending early hurts |
| Hand limit | 6 | **5** | Hoarding has a ceiling. Bots: 5 alone gives 66% |
| Extra tickets per week | +3 | **+4** | The board fills faster. Bots: +6 gives 68% |
| Supplier | Free | **−60 cash** | Cards cost money, so saving them saves money |
| Cards per week | 1 | 1 | Unchanged |

Special cards (section 5) make the game **easier**, because they open more escapes. Tune difficulty **with** special cards in the deck, or it will undershoot.

## 5. Special cards

### The cards

Everything that isn't + − × ÷ is a special card. NUDGE is out.

| Card | Effect | Example | On the board |
|---|---|---|---|
| **SWAP** | Swap the two digits | 89 → 98 | Reflects across the diagonal. Can't be played on 00, 11 … 99 |
| **MIRROR** | 99 − number | 47 → 52 | Jumps to the opposite side |
| **SHIFT +10 / −10 / +20 / −20** | Change the tens digit only | 47 → 67 | Straight up or down a column. Can't leave 00–99 |
| **TWIN** | Copy the tens digit to the units | 47 → 44 | Slides along the row to its double, toward the hot 77 and 88 |
| **WRAP +N** | Add N, wrapping past 99 to 00 | 98 +5 → 03 | Never "won't fit" |
| **JAM** | Redraw the units ball at random | 47 → 4? | The preview lights the whole row. A visible gamble |
| **REDRAW** | The machine draws a whole new number | 47 → ?? | Strongest card. At most one per run |

The route search for the reveal skips JAM and REDRAW, since their results aren't known in advance.

### Inventory and re-punching

- **Special cards don't go into the hand.** They sit in a separate **special inventory**, a rack beside the card reader, **max 3**. A new special with the rack full is lost, and the screen says so.
- **To use one, re-punch:** off air, at the card punch, choose an arithmetic card in your hand and a special in the rack. The arithmetic card is destroyed and the special takes its place in the hand. For example, +2 becomes SWAP.
- Re-punching is **free and doesn't use the desk action.** The price is the arithmetic card you give up, and it's a bet, because you don't know the number yet.
- You can re-punch as many as you like in one off-air session, but each needs its own arithmetic card. Once in the hand, a special plays like any card. It counts toward the card limit on inspection weeks.
- **Re-punching is off air only (decided 2026-11-09).** The number isn't known yet, so every re-punch is a bet.
- **Use it or lose it (decided 2026-11-09).** A re-punched special that isn't played on air that week is **destroyed at Announce**. It doesn't go back to the rack, and the arithmetic card doesn't come back. The receipt says so: "SWAP UNUSED: GONE".
- **Why this shape:** a re-punch costs two cards at once, the arithmetic card and the special, and pays off only if that week's roll needs it. Players hold specials in the rack until a week looks right, such as an inspection, a Nephew week or a slip they took, rather than converting early. With a smaller hand (section 4), that's the card management you asked for.
- **Inspection weeks:** you can play only one card, so re-punching two is always a loss. Expect players to re-punch at most one.

### Where special cards come from

| Source | How many |
|---|---|
| Start of run | 1 SWAP in the rack, to teach re-punching in week 1 |
| Collector slip (printer) | 1 random special |
| Nephew hit | 1 random special |
| Supplier | Never. Supplier stays arithmetic only |

Random specials are weighted: SWAP, MIRROR, SHIFT and TWIN are common, WRAP and JAM are uncommon, and REDRAW is rare, at most one per run.

Expect about 2–4 specials per run. Few enough to stay special, so arithmetic stays the core.

### Fiction

Punch cards are already the card fiction. A **card punch** on the desk re-punches cards: feed in +2, and SWAP comes out. Special cards have a different stock colour so they read at a glance.

## Validation

### Bots first, so the numbers can't be rationalised later

| Check | Target |
|---|---|
| Smart bot win rate | About 50% |
| Saves cards against spends freely | Gap of at least 15 points |
| Each fixed desk policy, including lie low | Within 5 points of each other. The smart policy beats them all |
| Each slip: always-TAKE, always-TEAR, smart | Smart wins by at least 5 points |
| Smart bot with specials against arithmetic only | Specials help, by no more than 10 points |
| Re-punched specials that go unused (smart bot) | About 30–50%. Lower means re-punching is free; higher means it's a coin toss |

### Then players

- Do they read the slip against their hand, and TEAR some?
- Do they ever lie low on purpose, and can they say why?
- Do they re-punch, and when? A bad sign is never re-punching at all. Losing a special unused should feel like their own bet going wrong, not like the game taking it.
- How often do re-punched specials go unused? Log it. If it's most weeks, the bet is too blind: show the special's possible landing squares on the board while choosing what to re-punch.
- Do they understand Shred spreading after the first time it burns them?
- Does it still feel like thinking, or like homework? GDD risk 1 matters more now.

## Build order and cut lines

The jam deadline is 14 Nov 2026, 8:00 PM. Each step is a valid place to stop.

1. **Action costs and lie low.** `Rules.cs` only, plus hover text. Smallest change, largest effect on "skip is never right".
2. **Difficulty knobs.** Config only, tuned with bots.
3. **Special cards and re-punching.** New card rules, the rack, the card punch and new card faces. The largest piece.
4. **Printer.** Prop, slip, TAKE/TEAR, and contracts checked at announce.

If time runs short, cut the printer first, then JAM, REDRAW and WRAP. Keep SWAP, MIRROR, SHIFT and TWIN.

## Risks

- **Load.** One date, one slip, one action and re-punching each week is a lot more than the friend's "high load" note allowed. Steps 1–3 add decisions mostly off air, where there's no clock. The printer adds the most reading, which is why it's built last.
- **Scope.** Four systems in five days, with the core game still unplayed by anyone. Get a human playtest of the current build alongside step 1.
- **Specials undo the difficulty.** Tune with them in, and cap the rack at 3.
- **Shred spreading may feel unfair** if it isn't shown. Preview the spread on the board while aiming.
- **Lie low may dominate late,** when the prize is high. Watch the policy bots.

## Open questions

1. ~~Should re-punching be allowed on air too?~~ **Decided 2026-11-09:** off air only, and an unused re-punched special is destroyed at Announce.
2. Should Supplier sometimes offer a special for more cash, such as −120? This draft says no, to keep specials scarce.
3. Rack size: 3 or 2?
4. Does lie low reduce the prize, or give a small cash reward instead?
5. Should the printer also carry the date calls, replacing the phone?

## Built (2026-11-09)

All four steps, in `lucky-number/Assets/Scripts`. Rules in `Rules.cs`; bots in `RulesTests.Bots_v2`; desk flow in `DeskPlayTests.V2_rack_slip_and_lie_low`. 22 of 22 PlayMode tests pass.

### Changed from the draft

| Draft | Built | Why |
|---|---|---|
| Lie low: prize −20 | **−5** | At −20, always lying low won 86% and the smart bot 87%. Lie low was the game |
| Extra tickets +4 a week | **+10** | Tickets barely move the win rate (bots: +4 → +10 is about −4 points) |
| Lie low is a choice | Going live **without** a desk action lies low | No prop needed; skipping is now the decision |
| Slip "Prize +40" | Same counter as a bribe: +40 for the rest of the run | The draft didn't say how long |
| TAKE/TEAR | Going live with an unread slip tears it | Never forced to answer |
| Supplier −60 | Refused when cash ≤ 60 | It would bankrupt you before the show |
| — | A random card (JAM, REDRAW) can't be ejected | Otherwise eject-and-replay rerolls it |
| — | Week log is now `weeks-v2.csv`, with slip, slipMet, repunched, unused | New columns; old logs stay readable |

Placement: the rack stands on top of the card reader; the slip lies on the desk right of the monitor stand and pins to the monitor's top-right corner. Specials are blue card stock.

### Bot results (1000 games; 600 for fixed policies)

| Check | Target | Got |
|---|---|---|
| Smart bot win rate | About 50% | **66%** |
| Saves cards against spends freely | Gap ≥ 15 | **11** (66 vs 55) |
| Fixed desk policies within 5 points | Within 5 | **No.** Supplier 51, Shred 52, Lie low 60, Bribe **6** |
| Slips: smart against always-TAKE / TEAR | Smart +5 | 66 against 62 / 61 |
| Specials against arithmetic only | Helps, ≤ 10 | +5 (66 vs 61) |
| Re-punched specials unused | 30–50% | 51% |

- **Neither difficulty target was hit.** A sweep over hand 4/5 and 3/4, tickets +4 to +10 and lie low 0 to 20 never opened the save-against-spend gap past about 11. Hand 3/4 brings the smart bot to about 56% but closes the gap to about 7. The card-value knob may need something new, such as Supplier costing more, rather than a smaller hand.
- **Always Bribe is a trap at 6%.** A permanent +40 a bribe outgrows +80 cash within two weeks. Either bribes should end at a scandal again, or pay more.
- The bots are simple (fixed heuristics for desk and re-punch), so treat these as direction, not truth.

### Not done

- The STAFF NOTICE poster on the wall is art and still lists the v1 actions ("Supplier +1 card", "Shred −2 holders").
- No human playtest yet.

