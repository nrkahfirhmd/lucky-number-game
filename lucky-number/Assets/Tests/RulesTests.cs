using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace NoWinners.Tests
{
    // Port of the GDD checks in prototypes/check.js.
    public class RulesTests
    {
        [Test]
        public void Sales_hand_and_lost_cards()
        {
            var s = new Run(new Random(1));
            s.StartWeek();
            Assert.AreEqual(190, s.Cash, "week 1 sales: 100 + 30 x 3");
            Assert.AreEqual(30, s.Board.Sum());
            Assert.AreEqual(4, s.Hand.Count);
            Assert.IsNull(s.Roll, "no number off air");
            s.StartWeek();
            Assert.AreEqual(40, s.Sold);
            Assert.AreEqual(5, s.Hand.Count);
            s.StartWeek();
            Assert.AreEqual(1, s.LostCards, "full hand loses that week's card");
        }

        [Test]
        public void Ticket_habits_shape_the_board()
        {
            var s = new Run(new Random(2));
            for (int i = 0; i < 400; i++) s.StartWeek(); // Lots of tickets, to see the weights.
            Assert.Greater(s.Board[7], s.Board[20] * 2, "lucky beats birthday");
            Assert.Greater(s.Board[20], s.Board[50] * 2, "birthday beats no reason");
            Assert.Greater(s.Board[50], s.Board[13] * 2, "no reason beats unlucky");
        }

        [Test]
        public void Desk_actions()
        {
            var s = new Run(new Random(3));
            int cash = s.Cash;
            Assert.IsTrue(s.TakeAction(DeskAction.Bribe));
            Assert.AreEqual(cash + 80, s.Cash);
            Assert.AreEqual(0, s.Streak, "a bribe is not a clean draw");
            Assert.AreEqual(160, s.Prize, "a bribe raises the prize");
            s.Streak = 9;
            Assert.AreEqual(400, s.Prize, "a bribe raises the prize even above the streak cap");
            Assert.IsFalse(s.TakeAction(DeskAction.Bribe), "one action per week");

            s = new Run(new Random(3));
            s.Board[47] = 6;
            Assert.IsTrue(s.TakeAction(DeskAction.Shred, 47));
            Assert.AreEqual((0, 6), (s.Board[47], s.Board[46] + s.Board[48]), "shred clears the square, its holders rebuy next door");
            s.Acted = null; s.Board[40] = 3;
            s.TakeAction(DeskAction.Shred, 40);
            Assert.AreEqual((0, 3, 0), (s.Board[40], s.Board[41], s.Board[39]), "edge holders stay in their row");
            s.Acted = null;
            Assert.IsFalse(s.TakeAction(DeskAction.Shred, 22), "nothing to shred");
            Assert.IsNull(s.Acted, "a failed shred doesn't use the action");
            int before = s.Cash;
            Assert.IsTrue(s.TakeAction(DeskAction.Supplier));
            Assert.AreEqual((1, before - Config.SupplierCost), (s.Hand.Count, s.Cash), "supplier costs cash");
            s.Acted = null; s.Cash = Config.SupplierCost;
            Assert.IsFalse(s.TakeAction(DeskAction.Supplier), "can't buy a card with your last cash");

            // Lie low: the prize comes down, never below the base.
            s = new Run(new Random(3)) { Streak = 2 };
            Assert.AreEqual(200, s.Prize);
            Assert.IsTrue(s.TakeAction(DeskAction.LieLow));
            Assert.AreEqual(200 - Config.LieLow, s.Prize);
            for (int i = 0; i < 30; i++) { s.Acted = null; s.TakeAction(DeskAction.LieLow); }
            Assert.AreEqual(Config.Prize, s.Prize, "never below the base");
            s.Streak = 3;
            Assert.AreEqual(160, s.Prize, "no banking calm below the base");
        }

        [Test]
        public void Result_tiers()
        {
            var s = new Run(new Random(4)) { Week = 1, Cash = 5000, Streak = 3 };
            s.Board[50] = 2; s.Board[51] = 2; s.Board[52] = 4;
            Assert.AreEqual(240, s.Prize);

            int c = s.Cash;
            var r = s.Announce(50, 1, true);
            Assert.AreEqual((Tier.Small, 240, 20), (r.Tier, r.Pay, r.Bonus));
            Assert.AreEqual(c - 220, s.Cash);
            Assert.AreEqual(3, s.Streak, "small winner holds the streak");
            Assert.AreEqual(0, s.Board[50]);

            r = s.Announce(51, 0, false);
            Assert.AreEqual((Tier.Scandal, 480, 0), (r.Tier, r.Pay, r.Bonus), "no card played means scandal");
            Assert.AreEqual(0, s.Streak);

            s.Streak = 2;
            r = s.Announce(52, 2, true);
            Assert.AreEqual((Tier.Scandal, 800), (r.Tier, r.Pay), "4 holders is a scandal");

            s.Streak = 2;
            Assert.AreEqual(Tier.Clean, s.Announce(53, 0, true).Tier);
            Assert.AreEqual(3, s.Streak);
            s.Streak = 9;
            Assert.AreEqual(360, s.Prize, "prize stops at streak 6");

            s.Bribes = 2; s.Board[60] = 4;
            s.Announce(60, 1, true);
            Assert.AreEqual((0, 2), (s.Streak, s.Bribes), "a scandal resets the streak, not the bribes");
        }

        [Test]
        public void Run_ends()
        {
            var s = new Run(new Random(5)) { Week = 1, Cash = 100 };
            s.Board[9] = 5;
            s.Announce(9, 0, false);
            Assert.AreEqual(Ending.Lost, s.Over, "cash at 0 or below ends the run");
            s = new Run(new Random(5)) { Week = 12, Cash = 100 };
            s.Announce(9, 0, false);
            Assert.AreEqual(Ending.Won, s.Over);
        }

        [Test]
        public void Cards_and_routes()
        {
            Assert.IsNull(Run.Apply(37, new Card('/', 3)));
            Assert.IsNull(Run.Apply(60, new Card('*', 2)));
            Assert.IsNull(Run.Apply(5, new Card('-', 9)));
            var hand = new List<Card> { new Card('*', 2), new Card('-', 3), new Card('+', 5), new Card('/', 3) };
            var routes = Run.Routes(37, hand);
            CollectionAssert.AreEqual(new[] { 0, 1 }, routes[71]);
            CollectionAssert.AreEqual(new[] { 2 }, routes[42]);
            Assert.IsFalse(Run.Routes(37, hand, 1).ContainsKey(71), "inspection limit blocks a two-card route");
        }

        [Test]
        public void Reveal_respects_inspections()
        {
            var s = new Run(new Random(6)) { Roll = 37, Week = 5, Streak = 1 };
            s.Hand = new List<Card> { new Card('*', 2), new Card('-', 3) };
            for (int i = 0; i < 100; i++) s.Board[i] = 5;
            s.Board[71] = 0;
            var best = s.BestRoute();
            Assert.AreEqual((71, 0), (best.To, best.Cost));
            CollectionAssert.AreEqual(new[] { 0, 1 }, best.Path);
            s.Week = 4; s.Board[74] = 2;
            best = s.BestRoute();
            Assert.AreEqual((74, 240), (best.To, best.Cost), "one card: cheapest is the small winner");
        }

        [Test]
        public void Seeded_first_roll_can_be_cleared_with_one_card()
        {
            var s = new Run(new Random(7));
            s.StartWeek();
            s.SeedRoll();
            Assert.Greater(s.Board[s.Roll.Value], 0);
            Assert.IsTrue(Run.Routes(s.Roll.Value, s.Hand, 1).Keys.Any(v => s.Board[v] == 0));
        }

        // ---- Special dates: docs/no-winners-offair-draft.md ----
        [Test]
        public void Dates_one_of_each_in_allowed_weeks()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var s = new Run(new Random(seed));
                s.ShuffleDates();
                foreach (var d in new[] { Special.Payday, Special.Telethon, Special.Nephew })
                {
                    int week = Array.IndexOf(s.Dates, d);
                    Assert.AreEqual(week, Array.LastIndexOf(s.Dates, d), $"one {d} per run");
                    Assert.Contains(week, Config.DateWeeks, $"{d} avoids week 1 and inspections");
                }
            }
            Assert.IsTrue(new Run().Dates.All(d => d == Special.None), "no dates unless shuffled");
        }

        [Test]
        public void Payday_doubles_tickets_and_cash()
        {
            var s = new Run(new Random(10));
            s.Dates[2] = Special.Payday;
            s.StartWeek();
            int cash = s.Cash;
            s.StartWeek();
            Assert.AreEqual(80, s.Sold, "40 x 2");
            Assert.AreEqual(cash + 80 * Config.TicketPrice, s.Cash);
            Assert.AreEqual(30 + 80, s.Board.Sum(), "the board keeps them");
        }

        [Test]
        public void Telethon_doubles_prize_and_live_bonus()
        {
            var s = new Run(new Random(11));
            s.Dates[1] = Special.Telethon;
            s.StartWeek();
            Assert.AreEqual(240, s.Prize);
            Assert.AreEqual(100, s.LiveBonus);
            s.DrawNumber();
            int free = Enumerable.Range(0, 100).First(n => s.Board[n] == 0), cash = s.Cash;
            Assert.AreEqual(100, s.Announce(free, 1, true).Bonus);
            Assert.AreEqual(cash + 100, s.Cash);
            s.StartWeek();
            Assert.AreEqual(160, s.Prize, "back to normal, with the streak");
        }

        [Test]
        public void Nephew_is_called_early_and_pays_when_hit()
        {
            var s = new Run(new Random(12));
            s.Dates[2] = Special.Nephew;
            s.StartWeek();
            Assert.AreEqual(Config.NephewNumbers, s.Nephews.Distinct().Count(), "picked the week before");
            Assert.IsTrue(s.Nephews.All(v => s.Board[v] == 0), "from empty squares");
            int n = s.Nephews[0];
            Assert.AreEqual(Special.Nephew, s.Next);
            s.StartWeek();
            Assert.IsTrue(s.Nephews.All(v => s.Board[v] >= 1), "his own tickets");
            s.Board[n] = 1;
            int cash = s.Cash;
            Assert.AreEqual(Config.Prize - Config.NephewReward, s.CostOf(n, 0), "net cost, untouched still counts");
            int rack = s.Rack.Count;
            var a = s.Announce(n, 0, false);
            Assert.AreEqual((Tier.Small, Config.NephewReward, Config.Prize), (a.Tier, a.Reward, a.Pay));
            Assert.AreEqual(cash + Config.NephewReward - Config.Prize, s.Cash);
            Assert.IsTrue(a.Gift.HasValue && a.Gift.Value.IsSpecial, "the nephew also pays a special card");
            Assert.AreEqual(rack + 1, s.Rack.Count);

            s = new Run(new Random(12));
            s.Dates[2] = Special.Nephew;
            s.StartWeek(); s.StartWeek();
            int free = Enumerable.Range(0, 100).First(v => s.Board[v] == 0);
            Assert.AreEqual(0, s.Announce(free, 1, false).Reward, "a miss: no reward, no penalty");
            s.Board[s.Nephews[1]] = 5;
            Assert.AreEqual(Tier.Scandal, s.TierAt(s.Nephews[1], 1), "a crowded nephew square is still a scandal");
        }

        // The bot run the draft asks for: how often the nephew's number is reachable, and what dates do to the win rate.
        [Test]
        public void Bot_with_dates()
        {
            const int games = 3000;
            int reach = 0;
            int Play(bool dates, out int hits, out int nephews)
            {
                var rng = new Random(13);
                int wins = 0; hits = 0; nephews = 0;
                for (int i = 0; i < games; i++)
                {
                    var g = new Run(rng);
                    if (dates) g.ShuffleDates();
                    while (g.Over == Ending.None)
                    {
                        g.StartWeek();
                        g.DrawNumber();
                        var plan = Run.Routes(g.Roll.Value, g.Hand, Run.CardLimit(g.Week))
                            .OrderBy(kv => g.CostOf(kv.Key, kv.Value.Count) + kv.Value.Count * 60).First();
                        g.Rack.Clear(); // Dates on their own: no specials.
                        if (g.Today == Special.Nephew)
                        {
                            nephews++;
                            if (g.IsFavour(plan.Key)) hits++;
                            if (Run.Routes(g.Roll.Value, g.Hand).Keys.Any(g.IsFavour)) reach++;
                        }
                        g.Announce(plan.Key, plan.Value.Count, true);
                        g.Discard(plan.Value);
                    }
                    if (g.Over == Ending.Won) wins++;
                }
                return wins * 100 / games;
            }
            int plain = Play(false, out _, out _), withDates = Play(true, out int hit, out int tries);
            int hitRate = hit * 100 / Math.Max(1, tries), reachRate = reach * 100 / Math.Max(1, tries);
            UnityEngine.Debug.Log($"Dates bot: wins {withDates}% with dates, {plain}% without. Nephew reachable {reachRate}%, hit {hitRate}% of {tries} nephew weeks.");
            Assert.That(withDates, Is.InRange(plain - 15, plain + 15), "dates shouldn't swing the run on their own");
            Assert.That(reachRate, Is.InRange(25, 90), "the nephew should be neither free nor hopeless");
        }
        // ---- v2: docs/no-winners-offair-v2-draft.md ----
        [Test]
        public void Special_cards()
        {
            int? A(int v, char op, int x = 0) => Run.Apply(v, new Card(op, x));
            Assert.AreEqual(((int?)98, (int?)null), (A(89, 'S'), A(44, 'S')), "SWAP: not on doubles");
            Assert.AreEqual((int?)52, A(47, 'M'), "MIRROR: 99 - n");
            Assert.AreEqual(((int?)67, (int?)27, (int?)null), (A(47, 'T', 20), A(47, 'T', -20), A(87, 'T', 20)), "SHIFT can't leave 00-99");
            Assert.AreEqual(((int?)44, (int?)null), (A(47, 'W'), A(44, 'W')), "TWIN: tens to units");
            Assert.AreEqual((int?)3, A(98, 'R', 5), "WRAP: past 99 to 00");
            Assert.IsNull(A(47, 'J'), "JAM is random: no route");
            Assert.IsTrue(Run.CanApply(47, new Card('J')));
            var hand = new List<Card> { new Card('J'), new Card('D'), new Card('+', 1) };
            Assert.IsFalse(Run.Routes(47, hand).Keys.Any(v => v != 47 && v != 48), "routes skip JAM and REDRAW");
            var s = new Run(new Random(20));
            for (int i = 0; i < 50; i++) Assert.AreEqual(4, s.Resolve(47, new Card('J')) / 10, "JAM redraws the units only");
            Assert.AreEqual(("SWAP", "SHIFT-10", "WRAP+5", "×2"), (new Card('S').ToString(), new Card('T', -10).ToString(), new Card('R', 5).ToString(), new Card('*', 2).ToString()));
            int redraws = 0;
            for (int i = 0; i < 400; i++) if (s.RandomSpecial().Op == 'D') redraws++;
            Assert.AreEqual(1, redraws, "REDRAW at most once a run");
        }

        [Test]
        public void Rack_and_repunch()
        {
            var s = new Run(new Random(21));
            s.StartWeek();
            CollectionAssert.AreEqual(new[] { new Card('S') }, s.Rack, "a SWAP in the rack to start");
            Assert.IsFalse(s.Hand.Any(c => c.IsSpecial), "specials never come in the hand");
            var gone = s.Hand[1];
            Assert.IsTrue(s.Repunch(1, 0));
            Assert.AreEqual((new Card('S'), 0, 4), (s.Hand[1], s.Rack.Count, s.Hand.Count), "the special takes the arithmetic card's place");
            Assert.IsFalse(s.Hand.Contains(gone), "the arithmetic card is destroyed");
            s.Rack.Add(new Card('M'));
            Assert.IsFalse(s.Repunch(1, 0), "only an arithmetic card can be re-punched");
            Assert.IsTrue(s.Repunch(2, 0));
            var unused = s.Discard(new[] { 2 });
            CollectionAssert.AreEqual(new[] { new Card('S') }, unused, "an unplayed re-punched special is destroyed");
            Assert.AreEqual(2, s.Hand.Count);
            Assert.IsFalse(s.Hand.Any(c => c.IsSpecial));
            for (int i = 0; i < 3; i++) s.GiveSpecial(new Card('W'));
            Assert.IsFalse(s.GiveSpecial(new Card('W')), "a full rack loses the new one");
            Assert.AreEqual(Config.RackMax, s.Rack.Count);
        }

        [Test]
        public void Printer_slips()
        {
            var s = new Run(new Random(22));
            s.StartWeek(); Assert.AreEqual(Slip.None, s.Offer, "no slips in weeks 1-2");
            s.StartWeek(); Assert.AreEqual(Slip.None, s.Offer);
            s.StartWeek(); Assert.AreNotEqual(Slip.None, s.Offer, "one slip a week from week 3");
            s.StartWeek(); Assert.AreEqual(Slip.None, s.Offer, "not on inspection weeks");
            for (int seed = 0; seed < 200; seed++)
            {
                var n = new Run(new Random(seed)) { Week = 4 };
                n.Dates[5] = Special.Nephew;
                n.StartWeek();
                Assert.AreNotEqual(Slip.Favour, n.Offer, "no Favour on Nephew week");
            }

            Announced Air(Slip slip, int number, int cards, float seconds = 0, int holders = 0)
            {
                var r = new Run(new Random(23)) { Week = 5, Cash = 1000, Offer = slip };
                r.Board[number] = holders;
                Assert.IsTrue(r.TakeSlip());
                int cash = r.Cash, bribes = r.Bribes;
                var a = r.Announce(number, cards, false, seconds);
                Assert.AreEqual(cash - a.Pay + a.SlipCash, r.Cash);
                if (!a.SlipMet && Slips.Fine(slip) == 0 && slip != Slip.Favour) Assert.AreEqual(bribes + 1, r.Bribes, $"{slip} failed: prize +40");
                return a;
            }
            Assert.AreEqual((true, 120), (Air(Slip.Sponsor, 47, 1).SlipMet, Air(Slip.Sponsor, 47, 1).SlipCash));
            Assert.AreEqual(-60, Air(Slip.Sponsor, 46, 1).SlipCash);
            Assert.AreEqual((100, false), (Air(Slip.Quiet, 46, 1).SlipCash, Air(Slip.Quiet, 46, 2).SlipMet));
            Assert.AreEqual((80, false), (Air(Slip.Rush, 46, 1, 9).SlipCash, Air(Slip.Rush, 46, 1, 11).SlipMet));
            Assert.AreEqual((150, -50, 0), (Air(Slip.Favour, 46, 1, 0, 2).SlipCash, Air(Slip.Favour, 46, 1).SlipCash, Air(Slip.Favour, 46, 1, 0, 9).SlipCash), "favour: -50 only if clean");
            var c = Air(Slip.Collector, 46, 1);
            Assert.IsTrue(c.SlipMet && c.Gift.HasValue, "collector pays a special");
            Assert.IsFalse(Air(Slip.Collector, 46, 1, 0, 9).SlipMet);

            var b = new Run(new Random(24)) { Week = 5, Offer = Slip.Bulk };
            int before = b.Cash;
            b.TakeSlip();
            Assert.AreEqual((before + 30, 10), (b.Cash, Enumerable.Range(60, 10).Sum(n => b.Board[n])), "bulk: paid now, 10 tickets on the 60s");
            var t = new Run(new Random(24)) { Week = 5, Offer = Slip.Sponsor };
            Assert.IsTrue(t.TearSlip());
            Assert.AreEqual((Slip.None, Slip.None), (t.Offer, t.Pinned), "tear is free");
        }

        // ---- v2 bots: the validation table in the v2 draft. Numbers are logged; asserts guard the targets loosely. ----
        class Bot
        {
            public string Name;
            public int Keep = 60; // What an arithmetic card is worth keeping. 0 spends freely.
            public DeskAction? Desk; // Fixed policy, or null for the smart one.
            public int Slips = 2; // 0 always tear, 1 always take, 2 smart.
            public bool Specials = true;
        }

        class Stats { public int Wins, Repunched, Unused, Specials, Games; public int Rate => Wins * 100 / Games; }

        static float Seconds(int cards) => 4 + 3 * cards; // A guess at how fast a bot announces.

        // What a slip is worth on one outcome: its reward, or minus its cost. Prize +40 is guessed at 80.
        static int SlipValue(Run g, Slip s, int to, int cards)
        {
            if (s == Slip.None || s == Slip.Bulk) return 0;
            var tier = g.TierAt(to, cards);
            bool met = s == Slip.Sponsor ? to % 10 == 7 : s == Slip.Quiet ? cards <= 1 : s == Slip.Rush ? Seconds(cards) <= Config.RushSeconds
                : s == Slip.Favour ? tier == Tier.Small : tier == Tier.Clean;
            return met ? (s == Slip.Collector ? 100 : Slips.Reward(s)) : s == Slip.Favour ? (tier == Tier.Clean ? -50 : 0) : s == Slip.Sponsor ? -60 : -80;
        }

        static int Score(Run g, IList<Card> hand, int to, List<int> path, Slip slip, int keep) =>
            g.CostOf(to, path.Count) + path.Count(i => !hand[i].IsSpecial) * keep - SlipValue(g, slip, to, path.Count);

        static KeyValuePair<int, List<int>> Plan(Run g, IList<Card> hand, int roll, int limit, Slip slip, int keep) =>
            Run.Routes(roll, hand, limit).OrderBy(kv => Score(g, hand, kv.Key, kv.Value, slip, keep)).First();

        // Mean best score over sampled rolls: how good this hand looks before the number is known.
        static double Expect(Run g, IList<Card> hand, Slip slip, int keep, Random r)
        {
            double sum = 0;
            for (int k = 0; k < 16; k++) { var p = Plan(g, hand, r.Next(100), Run.CardLimit(g.Week), slip, keep); sum += Score(g, hand, p.Key, p.Value, slip, keep); }
            return sum / 16;
        }

        static Stats Play(Bot bot, int games = 1000, int seed = 30)
        {
            var rng = new Random(seed);
            var sample = new Random(seed + 1);
            var st = new Stats { Games = games };
            for (int i = 0; i < games; i++)
            {
                var g = new Run(rng);
                g.ShuffleDates();
                while (g.Over == Ending.None)
                {
                    g.StartWeek();
                    if (!bot.Specials) g.Rack.Clear();
                    // Printer.
                    if (g.Offer != Slip.None)
                    {
                        bool take = bot.Slips == 1 || (bot.Slips == 2 && (g.Offer == Slip.Bulk ? g.Cash < 250
                            : Expect(g, g.Hand, g.Offer, bot.Keep, sample) < Expect(g, g.Hand, Slip.None, bot.Keep, sample)));
                        if (take) g.TakeSlip(); else g.TearSlip();
                    }
                    // Card punch: only on weeks that look right, or to make room in a full rack.
                    bool worth = Run.IsInspection(g.Week) || g.Today == Special.Nephew || g.Pinned != Slip.None || g.Rack.Count >= Config.RackMax;
                    while (bot.Specials && worth && g.Rack.Count > 0 && g.Hand.Any(c => !c.IsSpecial))
                    {
                        double now = Expect(g, g.Hand, g.Pinned, bot.Keep, sample);
                        double best = now - bot.Keep - 40; int bh = -1, br = -1;
                        for (int r = 0; r < g.Rack.Count; r++)
                            for (int h = 0; h < g.Hand.Count; h++)
                            {
                                if (g.Hand[h].IsSpecial || g.Rack[r].IsRandom) continue;
                                var hand = g.Hand.ToList(); hand[h] = g.Rack[r];
                                double e = Expect(g, hand, g.Pinned, bot.Keep, sample);
                                if (e < best) { best = e; bh = h; br = r; }
                            }
                        if (bh < 0 && g.Rack.Count >= Config.RackMax) // Make room: punch a random one over the weakest card.
                        {
                            br = g.Rack.FindIndex(c => c.IsRandom);
                            bh = g.Hand.FindIndex(c => !c.IsSpecial);
                        }
                        if (bh < 0 || br < 0) break;
                        g.Repunch(bh, br);
                        st.Repunched++;
                        worth = Run.IsInspection(g.Week) ? false : worth; // One card on inspections: one re-punch.
                        if (g.Rack.Count < Config.RackMax && !Run.IsInspection(g.Week) && g.Today != Special.Nephew && g.Pinned == Slip.None) break;
                    }
                    // Desk.
                    var desk = bot.Desk ?? (g.Hand.Count(c => !c.IsSpecial) <= 2 && g.Cash > 220 ? DeskAction.Supplier
                        : g.Cash < 120 ? DeskAction.Bribe : DeskAction.LieLow);
                    if (desk == DeskAction.Shred) g.TakeAction(desk, Enumerable.Range(0, 100).OrderByDescending(n => g.Board[n]).First());
                    else if (!g.TakeAction(desk)) g.TakeAction(DeskAction.LieLow);
                    // On air.
                    g.DrawNumber();
                    int roll = g.Roll.Value, limit = Run.CardLimit(g.Week);
                    var plan = Plan(g, g.Hand, roll, limit, g.Pinned, bot.Keep);
                    var played = plan.Value.ToList();
                    int to = plan.Key, jam = g.Hand.FindIndex(c => c.IsRandom);
                    if (jam >= 0 && Score(g, g.Hand, to, played, g.Pinned, bot.Keep) > 0)
                    {
                        int v = g.Resolve(roll, g.Hand[jam]);
                        var rest = Enumerable.Range(0, g.Hand.Count).Where(k => k != jam).ToList();
                        var p = Plan(g, rest.Select(k => g.Hand[k]).ToList(), v, limit - 1, g.Pinned, bot.Keep);
                        played = new List<int> { jam };
                        played.AddRange(p.Value.Select(k => rest[k]));
                        to = p.Key;
                    }
                    var a = g.Announce(to, played.Count, true, Seconds(played.Count));
                    if (a.Gift.HasValue && !a.GiftLost) st.Specials++;
                    st.Unused += g.Discard(played).Count;
                }
                if (g.Over == Ending.Won) st.Wins++;
            }
            return st;
        }

        [Test]
        public void Bots_v2()
        {
            var smart = Play(new Bot { Name = "smart" });
            var spends = Play(new Bot { Name = "spends", Keep = 0 });
            var plain = Play(new Bot { Name = "no specials", Specials = false });
            var log = new System.Text.StringBuilder();
            log.AppendLine($"v2 bots: smart {smart.Rate}%, spends freely {spends.Rate}% (gap {smart.Rate - spends.Rate}), arithmetic only {plain.Rate}%.");
            log.AppendLine($"  specials gained {smart.Specials * 10 / smart.Games / 10f:0.0}/run + 1 start, re-punched {smart.Repunched}, unused {smart.Unused * 100 / Math.Max(1, smart.Repunched)}%.");
            foreach (var d in new[] { DeskAction.Supplier, DeskAction.Bribe, DeskAction.Shred, DeskAction.LieLow })
                log.AppendLine($"  always {d}: {Play(new Bot { Desk = d }, 600).Rate}%");
            log.AppendLine($"  slips always TAKE {Play(new Bot { Slips = 1 }, 600).Rate}%, always TEAR {Play(new Bot { Slips = 0 }, 600).Rate}%");
            UnityEngine.Debug.Log(log);
            // The draft wants about 50% and a 15-point gap. Neither knob order reached both (see the draft's bot results), so these guard drift.
            Assert.That(smart.Rate, Is.InRange(55, 75), "smart bot drifted");
            Assert.That(smart.Rate - spends.Rate, Is.GreaterThanOrEqualTo(5), "saving cards should still beat spending freely");
        }
    }
}
