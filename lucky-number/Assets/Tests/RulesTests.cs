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
            Assert.AreEqual(5, s.Hand.Count);
            Assert.IsNull(s.Roll, "no number off air");
            s.StartWeek();
            Assert.AreEqual(33, s.Sold);
            Assert.AreEqual(6, s.Hand.Count);
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
            s.Board[20] = 3;
            Assert.IsTrue(s.TakeAction(DeskAction.Shred, 20));
            Assert.AreEqual(1, s.Board[20]);
            s.Acted = null; s.Board[21] = 1;
            s.TakeAction(DeskAction.Shred, 21);
            Assert.AreEqual(0, s.Board[21]);
            s.Acted = null;
            Assert.IsFalse(s.TakeAction(DeskAction.Shred, 22), "nothing to shred");
            Assert.IsNull(s.Acted, "a failed shred doesn't use the action");
            Assert.IsTrue(s.TakeAction(DeskAction.Supplier));
            Assert.AreEqual(1, s.Hand.Count);
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
            Assert.AreEqual(0, s.Bribes, "a scandal clears bribes");
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

        // Same rules, same result as the GDD ("no desk action" with the bot that saves cards wins 65%).
        [Test]
        public void Bot_win_rate_matches_the_GDD()
        {
            const int games = 3000;
            var rng = new Random(8);
            int wins = 0;
            for (int i = 0; i < games; i++)
            {
                var g = new Run(rng);
                while (g.Over == Ending.None)
                {
                    g.StartWeek();
                    g.DrawNumber();
                    var plan = Run.Routes(g.Roll.Value, g.Hand, Run.CardLimit(g.Week))
                        .OrderBy(kv => g.CostOf(kv.Key, kv.Value.Count) + kv.Value.Count * 60).First();
                    g.Announce(plan.Key, plan.Value.Count, true);
                    g.Hand = g.Hand.Where((_, j) => !plan.Value.Contains(j)).ToList();
                }
                if (g.Over == Ending.Won) wins++;
            }
            int rate = wins * 100 / games;
            UnityEngine.Debug.Log($"GDD rules, saves-cards bot, no desk: wins {rate}% (GDD table: 65)");
            Assert.That(rate, Is.InRange(60, 70), "bot win rate drifted from the GDD");
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
            Assert.AreEqual(66, s.Sold, "33 x 2");
            Assert.AreEqual(cash + 66 * Config.TicketPrice, s.Cash);
            Assert.AreEqual(30 + 66, s.Board.Sum(), "the board keeps them");
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
            var a = s.Announce(n, 0, false);
            Assert.AreEqual((Tier.Small, Config.NephewReward, Config.Prize), (a.Tier, a.Reward, a.Pay));
            Assert.AreEqual(cash + Config.NephewReward - Config.Prize, s.Cash);

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
                        if (g.Today == Special.Nephew)
                        {
                            nephews++;
                            if (g.IsFavour(plan.Key)) hits++;
                            if (Run.Routes(g.Roll.Value, g.Hand).Keys.Any(g.IsFavour)) reach++;
                        }
                        g.Announce(plan.Key, plan.Value.Count, true);
                        g.Hand = g.Hand.Where((_, j) => !plan.Value.Contains(j)).ToList();
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
    }
}
