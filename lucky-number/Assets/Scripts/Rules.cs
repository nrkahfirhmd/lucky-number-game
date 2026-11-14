using System;
using System.Collections.Generic;
using System.Linq;

namespace NoWinners
{
    // docs/GDD.md, section 6. All tuning lives here. Every value is tuned against bots only.
    public static class Config
    {
        public const int Weeks = 12, StartCash = 100;
        // docs/no-winners-offair-v2-draft.md, section 4: smaller hand, more tickets. Tuned against bots only (RulesTests.Bots_v2).
        public const int TicketPrice = 3, Tickets1 = 30, TicketsGrowth = 10;
        public const int StartHand = 4, HandLimit = 5, Bribe = 80;
        // v2 section 2: every desk action costs something. Lie low is what doing nothing is.
        // The draft's -20 made lie low dominant (bots: 86% always lying low); -5 keeps it a choice.
        public const int SupplierCost = 60, LieLow = 5;
        public const int Prize = 120, Rollover = 40, MaxStreakPrize = 6, SmallMax = 3;
        public const int Draw = 1, InspectionCards = 1;
        public const int Live = 20;
                // v2 section 5: special cards, in their own rack.
        public const int RackMax = 3;
        // v2 section 1: the printer. One slip a week from week 3, not on inspections.
        public const int SlipFrom = 3, RushSeconds = 10, BulkTickets = 10, BulkFrom = 60;
        public const float Target = 20, StallEvery = 10;
        public static readonly int[] Inspections = { 4, 8, 12 };
        public static readonly int[] AddSub = { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, Mul = { 2, 3 }, Div = { 2, 3, 5 };
        // docs/no-winners-offair-draft.md, special dates. Untuned: starting guesses for playtest.
        public static readonly int[] DateWeeks = { 2, 3, 5, 6, 7, 9, 10, 11 }; // Not week 1, not inspections.
        public const int PaydayTickets = 2, TelethonLive = 5, TelethonPrize = 2, NephewReward = 200;
        // One exact number is reachable ~15-27% of weeks (5-6 cards), so he buys a few. 1 = the draft's original rule.
        public const int NephewNumbers = 3;
    }

    public enum Tier { Clean, Small, Scandal }
    public enum Special { None, Payday, Telethon, Nephew }
    public enum Ending { None, Won, Lost }
    public enum DeskAction { Supplier, Bribe, Shred, LieLow }
    public enum Slip { None, Sponsor, Quiet, Rush, Favour, Bulk, Collector }

    public readonly struct Card
    {
        public readonly char Op;
        public readonly int X;
        public Card(char op, int x = 0) { Op = op; X = x; }
        public bool IsSpecial => "+-*/".IndexOf(Op) < 0;
        public bool IsRandom => Op == 'J' || Op == 'D';
        public string Name => Op switch
        {
            'S' => "SWAP",
            'M' => "MIRROR",
            'T' => "SHIFT",
            'W' => "TWIN",
            'R' => "WRAP",
            'J' => "JAM",
            'D' => "REDRAW",
            _ => ""
        };
        public string Arg => Op switch
        {
            'T' => (X > 0 ? "+" : "") + X,
            'R' => "+" + X,
            _ => ""
        };
        public override string ToString() => IsSpecial ? Name + Arg : (Op switch { '*' => "×", '/' => "÷", _ => Op.ToString() }) + X;
    }

    public static class Slips
    {
        public static string Name(Slip s) => s switch
        {
            Slip.Sponsor => "SPONSOR",
            Slip.Quiet => "QUIET WEEK",
            Slip.Rush => "RUSH JOB",
            Slip.Favour => "FAVOUR",
            Slip.Bulk => "BULK ORDER",
            Slip.Collector => "COLLECTOR",
            _ => ""
        };

        public static string Quote(Slip s) => s switch
        {
            Slip.Sponsor => "Lucky 7 night.",
            Slip.Quiet => "Inspector in the building.",
            Slip.Rush => "Network wants it fast.",
            Slip.Favour => "Let a few win.",
            Slip.Bulk => $"{Config.BulkTickets} tickets on the {Config.BulkFrom}s.",
            Slip.Collector => "Wants a clean show.",
            _ => ""
        };

        public static string Wants(Slip s) => s switch
        {
            Slip.Sponsor => "FINAL NUMBER ENDS IN 7",
            Slip.Quiet => "PLAY AT MOST 1 CARD",
            Slip.Rush => $"ANNOUNCE UNDER {Config.RushSeconds}S",
            Slip.Favour => $"A SMALL WINNER (1-{Config.SmallMax} HOLDERS)",
            Slip.Bulk => "NOTHING: PAID NOW",
            Slip.Collector => "A CLEAN DRAW",
            _ => ""
        };

        public static int Reward(Slip s) => s switch
        {
            Slip.Sponsor => 120,
            Slip.Quiet => 100,
            Slip.Rush => 80,
            Slip.Favour => 150,
            Slip.Bulk => 30,
            _ => 0
        };

        public static string Pays(Slip s) => s switch
        {
            Slip.Collector => "1 SPECIAL CARD",
            Slip.Bulk => $"+{Reward(s)} CASH NOW",
            _ => $"+{Reward(s)} CASH"
        };

        public static int Fine(Slip s) => s switch
        {
            Slip.Sponsor => 60,
            Slip.Favour => 50,
            _ => 0
        };

        public static string Fails(Slip s) => s switch
        {
            Slip.Sponsor => $"-{Fine(s)} CASH",
            Slip.Favour => $"-{Fine(s)} CASH IF NOBODY WINS",
            Slip.Bulk => $"{Config.BulkTickets} EXTRA TICKETS ON {Config.BulkFrom}-{Config.BulkFrom + 9}",
            _ => $"PRIZE +{Config.Rollover} FOR THE REST OF THE RUN"
        };
    }

    public class Route
    {
        public int To, Cost, Holders;
        public List<int> Path;
    }

    public class Announced
    {
        public int Holders, Pay, Bonus, Reward, SlipCash;
        public Tier Tier;
        public Slip Slip; // The pinned slip, if any, and how it went.
        public bool SlipMet;
        public Card? Gift; // A special card won this week (nephew or collector).
        public bool GiftLost; // The rack was full.
    }

    // One run. No scene dependencies. Pass a seeded Random to reproduce a game.
    public class Run
    {
        readonly Random rng;
        public int Cash = Config.StartCash, Week, Streak, Best, Cleans, Smalls, Scandals, Bribes, Sold, LostCards;
        public int Calm; // Taken off the prize by lying low. Never takes it below the base.
        public readonly List<Card> Rack = new List<Card> { new Card('S') }; // One SWAP, to teach re-punching in the first off air.
        public bool RedrawDealt; // REDRAW: at most one per run.
        public Slip Offer, Pinned; // The printer's slip this week, and the one you took.
        public Card? Supplied, Overflow; // The card waiting at the supplier or weekly deal when the hand is full.
        public readonly int[] Board = new int[100];
        public List<Card> Hand = new List<Card>();
        public DeskAction? Acted;
        public int? Roll; // Hidden off air: it doesn't exist until the show.
        public Ending Over;

        // Special dates by week. All None unless ShuffleDates runs, so the plain rules stay testable.
        public readonly Special[] Dates = new Special[Config.Weeks + 2];
        public readonly List<int> Nephews = new List<int>(); // His numbers: picked the week before his date, from empty squares.

        public Run(Random rng = null) { this.rng = rng ?? new Random(); }

        public static bool IsInspection(int week) => Array.IndexOf(Config.Inspections, week) >= 0;
        public static int CardLimit(int week) => IsInspection(week) ? Config.InspectionCards : int.MaxValue;

        Special DateOf(int week) => week < Dates.Length ? Dates[week] : Special.None;
        public Special Today => DateOf(Week);
        public Special Next => DateOf(Week + 1);

        // One of each date, in shuffled weeks.
        public void ShuffleDates()
        {
            var weeks = Config.DateWeeks.OrderBy(_ => rng.Next()).ToArray();
            Dates[weeks[0]] = Special.Payday;
            Dates[weeks[1]] = Special.Telethon;
            Dates[weeks[2]] = Special.Nephew;
        }

        // Bribes sit above the streak cap, so one always costs something. A scandal resets the streak, not the bribes.
        int RawPrize => Config.Prize + Config.Rollover * (Math.Min(Streak, Config.MaxStreakPrize) + Bribes);
        public int Prize => Math.Max(Config.Prize, RawPrize - Calm) * (Today == Special.Telethon ? Config.TelethonPrize : 1);
        void ClampCalm() => Calm = Math.Max(0, Math.Min(Calm, RawPrize - Config.Prize)); // No banking calm below the base.

        public int LiveBonus => Config.Live * (Today == Special.Telethon ? Config.TelethonLive : 1);

        // A small winner needs effort: a few holders, reached with at least one card.
        public static Tier TierOf(int holders, int cards) =>
            holders == 0 ? Tier.Clean : holders <= Config.SmallMax && cards > 0 ? Tier.Small : Tier.Scandal;

        public bool IsFavour(int number) => Today == Special.Nephew && Nephews.Contains(number);

        // The nephew's square counts as a small winner even untouched: the minister asked for it.
        public Tier TierAt(int number, int cards) =>
            IsFavour(number) && Board[number] <= Config.SmallMax ? Tier.Small : TierOf(Board[number], cards);

        public int RewardOf(int number) => IsFavour(number) ? Config.NephewReward : 0;

        // Net: what the payout costs, minus the minister's reward. Can go negative.
        public int CostOf(int number, int cards) =>
            Board[number] * (TierAt(number, cards) == Tier.Small ? Config.Prize : Prize) - RewardOf(number);

        // One card on a number, or null if it leaves 00-99, isn't whole, does nothing, or is random (JAM, REDRAW).
        public static int? Apply(int v, Card c)
        {
            int tens = v / 10, units = v % 10;
            if (c.IsRandom || (c.Op == '/' && v % c.X != 0) || ((c.Op == 'S' || c.Op == 'W') && tens == units)) return null;
            int r = c.Op switch
            {
                '+' => v + c.X,
                '-' => v - c.X,
                '*' => v * c.X,
                '/' => v / c.X,
                'S' => units * 10 + tens,
                'M' => 99 - v,
                'T' => v + c.X,
                'W' => tens * 11,
                _ => (v + c.X) % 100
            };
            return r is >= 0 and <= 99 ? r : null;
        }

        public static bool CanApply(int v, Card c) => c.IsRandom || Apply(v, c) != null;

        // On air: what a card actually does. JAM redraws the units ball, REDRAW the whole number.
        public int Resolve(int v, Card c) => c.Op == 'J' ? v / 10 * 10 + rng.Next(10) : c.Op == 'D' ? rng.Next(100) : Apply(v, c).Value;

        static int Weight(int n)
        {
            if (n == 7 || n == 8 || n == 77 || n == 88) return 32;
            if (n == 4 || n == 13) return 1; // Checked first: both also sit inside 1-31.
            return n >= 1 && n <= 31 ? 12 : 4; // All weights x4, to stay whole: 8, 0.25, 3, 1.
        }
        static readonly int[] Weights = Enumerable.Range(0, 100).Select(Weight).ToArray();
        static readonly int TotalWeight = Weights.Sum();

        void AddTickets(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int r = rng.Next(TotalWeight), n = 0;
                while ((r -= Weights[n]) >= 0) n++;
                Board[n]++;
            }
        }

        // Weighted: SWAP, MIRROR, SHIFT, TWIN common; WRAP, JAM uncommon; REDRAW rare, once a run.
        static readonly (char op, int weight)[] SpecialWeights = { ('S', 4), ('M', 4), ('T', 4), ('W', 4), ('R', 2), ('J', 2), ('D', 1) };
        static readonly int[] Shifts = { 10, -10, 20, -20 };
        public Card RandomSpecial()
        {
            var pool = SpecialWeights.Where(w => w.op != 'D' || !RedrawDealt).ToArray();
            int r = rng.Next(pool.Sum(w => w.weight)), i = 0;
            while ((r -= pool[i].weight) >= 0) i++;
            char op = pool[i].op;
            if (op == 'D') RedrawDealt = true;
            return new Card(op, op == 'T' ? Shifts[rng.Next(Shifts.Length)] : op == 'R' ? Config.AddSub[rng.Next(Config.AddSub.Length)] : 0);
        }

        // Into the rack, or lost if it's full. Returns false when lost.
        public bool GiveSpecial(Card c)
        {
            if (Rack.Count >= Config.RackMax) return false;
            Rack.Add(c);
            return true;
        }

        // Off air, free, doesn't use the desk action: an arithmetic card in the hand becomes a special from the rack.
        public bool Repunch(int hand, int rack)
        {
            if (hand < 0 || hand >= Hand.Count || rack < 0 || rack >= Rack.Count || Hand[hand].IsSpecial) return false;
            Hand[hand] = Rack[rack];
            Rack.RemoveAt(rack);
            return true;
        }

        // After the show: played cards leave the hand, and re-punched specials that weren't played are destroyed.
        public List<Card> Discard(ICollection<int> played)
        {
            var unused = Hand.Where((c, i) => !played.Contains(i) && c.IsSpecial).ToList();
            Hand = Hand.Where((c, i) => !played.Contains(i) && !c.IsSpecial).ToList();
            return unused;
        }

        Card RandomCard()
        {
            char op = "+-*/"[rng.Next(4)];
            int[] values = op == '*' ? Config.Mul : op == '/' ? Config.Div : Config.AddSub;
            return new Card(op, values[rng.Next(values.Length)]);
        }

        void Deal()
        {
            if (Hand.Count < Config.HandLimit) Hand.Add(RandomCard());
            else { Overflow = RandomCard(); LostCards++; }
        }

        public bool ReplaceOverflow(int hand)
        {
            if (Overflow == null || hand < 0 || hand >= Hand.Count) return false;
            Hand[hand] = Overflow.Value;
            Overflow = null;
            return true;
        }

        public void SkipOverflow() => Overflow = null;

        static readonly Slip[] AvailableSlips = { Slip.Sponsor, Slip.Quiet, Slip.Rush, Slip.Favour, Slip.Bulk, Slip.Collector };

        public void StartWeek()
        {
            Week++;
            Sold = (Config.Tickets1 + Config.TicketsGrowth * (Week - 1)) * (Today == Special.Payday ? Config.PaydayTickets : 1);
            Cash += Sold * Config.TicketPrice;
            AddTickets(Sold);
            if (Next == Special.Nephew)
            {
                var empty = Enumerable.Range(0, 100).Where(n => Board[n] == 0).OrderBy(_ => rng.Next()).ToList();
                if (empty.Count < Config.NephewNumbers) empty.AddRange(Enumerable.Range(0, 100).Except(empty).OrderBy(_ => rng.Next()));
                Nephews.Clear();
                Nephews.AddRange(empty.Take(Config.NephewNumbers).OrderBy(n => n));
            }
            if (Today == Special.Nephew) foreach (int n in Nephews) Board[n]++;
            LostCards = 0;
            Acted = null;
            Roll = null;
            Supplied = null;
            Overflow = null;
            Pinned = Slip.None;
            Offer = Slip.None;
            if (Week >= Config.SlipFrom && !IsInspection(Week))
            {
                var slips = AvailableSlips.Where(k => !(k == Slip.Favour && Today == Special.Nephew)).ToArray();
                Offer = slips[rng.Next(slips.Length)];
            }
            for (int i = 0, d = Week == 1 ? Config.StartHand : Config.Draw; i < d; i++) Deal();
        }

        // On air only. Honest: no rerolls, no route guarantee.
        public void DrawNumber() => Roll = rng.Next(100);

        // TAKE pins the slip. Bulk pays now and adds its tickets now; the rest are checked at Announce.
        public bool TakeSlip()
        {
            if (Offer == Slip.None || Roll != null) return false;
            Pinned = Offer;
            Offer = Slip.None;
            if (Pinned == Slip.Bulk)
            {
                Cash += Slips.Reward(Slip.Bulk);
                for (int i = 0; i < Config.BulkTickets; i++) Board[Config.BulkFrom + rng.Next(10)]++;
            }
            return true;
        }

        public bool TearSlip()
        {
            if (Offer == Slip.None || Roll != null) return false;
            Offer = Slip.None;
            return true;
        }

        public Card PreviewSupplier() => Supplied ??= RandomCard();

        // Off air, once a week. Returns false if it can't be taken.
        public bool TakeAction(DeskAction action, int square = -1)
        {
            if (Acted != null) return false;
            if (action == DeskAction.Shred && (square < 0 || Board[square] == 0)) return false;
            if (action == DeskAction.Supplier && (Cash <= Config.SupplierCost || (Hand.Count >= Config.HandLimit && (square < 0 || square >= Hand.Count)))) return false; // It would bankrupt you, or needs a slot to replace.
            if (action == DeskAction.Supplier)
            {
                Cash -= Config.SupplierCost;
                if (Hand.Count >= Config.HandLimit) { Hand[square] = PreviewSupplier(); Supplied = null; }
                else Deal();
            }
            else if (action == DeskAction.Bribe) { Cash += Config.Bribe; Bribes++; }
            else if (action == DeskAction.Shred)
            {
                // The holders rebuy next door, in the same row: one safe square, hotter neighbours.
                int col = square % 10;
                for (int h = Board[square]; h > 0; h--)
                    Board[col == 0 ? square + 1 : col == 9 ? square - 1 : square + (rng.Next(2) == 0 ? -1 : 1)]++;
                Board[square] = 0;
            }
            else { Calm += Config.LieLow; ClampCalm(); }
            Acted = action;
            return true;
        }

        // Where a shred's holders could go: the squares either side in the same row.
        public static IEnumerable<int> Neighbours(int square)
        {
            if (square % 10 > 0) yield return square - 1;
            if (square % 10 < 9) yield return square + 1;
        }

        // The number goes out. seconds: time on air, for the Rush slip.
        public Announced Announce(int number, int cards, bool onTime, float seconds = 0)
        {
            var a = new Announced { Holders = Board[number], Tier = TierAt(number, cards), Reward = RewardOf(number), Bonus = onTime ? LiveBonus : 0, Slip = Pinned };
            a.Pay = CostOf(number, cards) + a.Reward; // Gross payout; the reward is shown on its own line.
            Cash += a.Bonus + a.Reward - a.Pay;
            Board[number] = 0;
            if (a.Tier == Tier.Clean) { Streak++; Cleans++; Best = Math.Max(Best, Streak); }
            else if (a.Tier == Tier.Small) Smalls++;
            else { Streak = 0; Scandals++; }
            CheckSlip(a, number, cards, seconds);
            if (a.Reward > 0) Gift(a); // The nephew also pays a special card.
            ClampCalm();
            Pinned = Slip.None;
            if (Cash <= 0) Over = Ending.Lost;
            else if (Week >= Config.Weeks) Over = Ending.Won;
            return a;
        }

        void Gift(Announced a)
        {
            a.Gift = RandomSpecial();
            a.GiftLost = !GiveSpecial(a.Gift.Value);
        }

        void CheckSlip(Announced a, int number, int cards, float seconds)
        {
            var s = a.Slip;
            if (s == Slip.None || s == Slip.Bulk) { a.SlipMet = s == Slip.Bulk; return; }
            a.SlipMet = s switch
            {
                Slip.Sponsor => number % 10 == 7,
                Slip.Quiet => cards <= 1,
                Slip.Rush => seconds <= Config.RushSeconds,
                Slip.Favour => a.Tier == Tier.Small,
                _ => a.Tier == Tier.Clean
            };
            if (a.SlipMet) { a.SlipCash = Slips.Reward(s); if (s == Slip.Collector) Gift(a); }
            else if (s == Slip.Sponsor || (s == Slip.Favour && a.Tier == Tier.Clean)) a.SlipCash = -Slips.Fine(s);
            else if (s != Slip.Favour) Bribes++; // Prize +40 for the rest of the run, like a bribe.
            Cash += a.SlipCash;
        }

        // Every number reachable from start -> shortest list of hand indexes that gets there.
        public static Dictionary<int, List<int>> Routes(int start, IList<Card> hand, int maxCards = int.MaxValue)
        {
            var found = new Dictionary<int, List<int>> { [start] = new List<int>() };
            var path = new List<int>();
            void Walk(int v)
            {
                if (path.Count >= maxCards) return;
                for (int i = 0; i < hand.Count; i++)
                {
                    if (path.Contains(i)) continue;
                    int? r = Apply(v, hand[i]); // JAM and REDRAW aren't known in advance, so routes skip them.
                    if (r == null) continue;
                    path.Add(i);
                    if (!found.TryGetValue(r.Value, out var known) || known.Count > path.Count) found[r.Value] = new List<int>(path);
                    Walk(r.Value);
                    path.RemoveAt(path.Count - 1);
                }
            }
            Walk(start);
            return found;
        }

        // Reveal only: the cheapest result you could have reached, then the fewest cards. Call before announcing.
        public Route BestRoute()
        {
            Route best = null;
            foreach (var kv in Routes(Roll.Value, Hand, CardLimit(Week)))
            {
                int cost = CostOf(kv.Key, kv.Value.Count);
                if (best == null || cost < best.Cost || (cost == best.Cost && kv.Value.Count < best.Path.Count))
                    best = new Route { To = kv.Key, Path = kv.Value, Cost = cost, Holders = Board[kv.Key] };
            }
            return best;
        }

        // First run only (GDD onboarding): a held roll that one card can clear.
        public void SeedRoll()
        {
            foreach (int n in Enumerable.Range(0, 100).OrderBy(_ => rng.Next()))
                if (Board[n] > 0 && Routes(n, Hand, 1).Keys.Any(v => Board[v] == 0)) { Roll = n; return; }
        }
    }
}
