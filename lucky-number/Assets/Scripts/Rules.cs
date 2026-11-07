using System;
using System.Collections.Generic;
using System.Linq;

namespace NoWinners
{
    // docs/GDD.md, section 6. All tuning lives here. Every value is tuned against bots only.
    public static class Config
    {
        public const int Weeks = 12, StartCash = 100;
        public const int TicketPrice = 3, Tickets1 = 30, TicketsGrowth = 3;
        public const int Prize = 120, Rollover = 40, MaxStreakPrize = 6, SmallMax = 3;
        public const int StartHand = 5, Draw = 1, HandLimit = 6, InspectionCards = 1;
        public const int Live = 20, Bribe = 80, Shred = 2;
        public const float Target = 20, StallEvery = 10;
        public static readonly int[] Inspections = { 4, 8, 12 };
        public static readonly int[] AddSub = { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, Mul = { 2, 3 }, Div = { 2, 3, 5 };
    }

    public enum Tier { Clean, Small, Scandal }
    public enum Ending { None, Won, Lost }
    public enum DeskAction { Supplier, Bribe, Shred }

    public readonly struct Card
    {
        public readonly char Op; // + - * /
        public readonly int X;
        public Card(char op, int x) { Op = op; X = x; }
        public override string ToString() => (Op == '*' ? "×" : Op == '/' ? "÷" : Op.ToString()) + X;
    }

    public class Route
    {
        public int To, Cost, Holders;
        public List<int> Path;
    }

    public class Announced
    {
        public int Holders, Pay, Bonus;
        public Tier Tier;
    }

    // One run. No scene dependencies. Pass a seeded Random to reproduce a game.
    public class Run
    {
        readonly Random rng;
        public int Cash = Config.StartCash, Week, Streak, Best, Cleans, Smalls, Scandals, Bribes, Sold, LostCards;
        public readonly int[] Board = new int[100];
        public List<Card> Hand = new List<Card>();
        public DeskAction? Acted;
        public int? Roll; // Hidden off air: it doesn't exist until the show.
        public Ending Over;

        public Run(Random rng = null) { this.rng = rng ?? new Random(); }

        public static bool IsInspection(int week) => Array.IndexOf(Config.Inspections, week) >= 0;
        public static int CardLimit(int week) => IsInspection(week) ? Config.InspectionCards : int.MaxValue;

        // Bribes sit above the streak cap, so one always costs something.
        public int Prize => Config.Prize + Config.Rollover * (Math.Min(Streak, Config.MaxStreakPrize) + Bribes);

        // A small winner needs effort: a few holders, reached with at least one card.
        public static Tier TierOf(int holders, int cards) =>
            holders == 0 ? Tier.Clean : holders <= Config.SmallMax && cards > 0 ? Tier.Small : Tier.Scandal;

        public int CostOf(int number, int cards) =>
            Board[number] * (TierOf(Board[number], cards) == Tier.Small ? Config.Prize : Prize);

        // One card on a number, or null if it leaves 00-99 or isn't whole.
        public static int? Apply(int v, Card c)
        {
            if (c.Op == '/' && v % c.X != 0) return null;
            int r = c.Op == '+' ? v + c.X : c.Op == '-' ? v - c.X : c.Op == '*' ? v * c.X : v / c.X;
            return r >= 0 && r <= 99 ? r : (int?)null;
        }

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

        Card RandomCard()
        {
            char op = "+-*/"[rng.Next(4)];
            int[] values = op == '*' ? Config.Mul : op == '/' ? Config.Div : Config.AddSub;
            return new Card(op, values[rng.Next(values.Length)]);
        }

        void Deal()
        {
            if (Hand.Count < Config.HandLimit) Hand.Add(RandomCard());
            else LostCards++;
        }

        public void StartWeek()
        {
            Week++;
            Sold = Config.Tickets1 + Config.TicketsGrowth * (Week - 1);
            Cash += Sold * Config.TicketPrice;
            AddTickets(Sold);
            LostCards = 0;
            Acted = null;
            Roll = null;
            for (int i = 0, d = Week == 1 ? Config.StartHand : Config.Draw; i < d; i++) Deal();
        }

        // On air only. Honest: no rerolls, no route guarantee.
        public void DrawNumber() => Roll = rng.Next(100);

        // Off air, once a week. Returns false if it can't be taken.
        public bool TakeAction(DeskAction action, int square = -1)
        {
            if (Acted != null) return false;
            if (action == DeskAction.Shred && (square < 0 || Board[square] == 0)) return false;
            if (action == DeskAction.Supplier) Deal();
            else if (action == DeskAction.Bribe) { Cash += Config.Bribe; Bribes++; }
            else Board[square] = Math.Max(0, Board[square] - Config.Shred);
            Acted = action;
            return true;
        }

        // The number goes out.
        public Announced Announce(int number, int cards, bool onTime)
        {
            var a = new Announced { Holders = Board[number], Tier = TierOf(Board[number], cards), Pay = CostOf(number, cards), Bonus = onTime ? Config.Live : 0 };
            Cash += a.Bonus - a.Pay;
            Board[number] = 0;
            if (a.Tier == Tier.Clean) { Streak++; Cleans++; Best = Math.Max(Best, Streak); }
            else if (a.Tier == Tier.Small) Smalls++;
            else { Streak = 0; Bribes = 0; Scandals++; }
            if (Cash <= 0) Over = Ending.Lost;
            else if (Week >= Config.Weeks) Over = Ending.Won;
            return a;
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
                    int? r = Apply(v, hand[i]);
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
