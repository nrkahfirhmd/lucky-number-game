using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace NoWinners.Tests
{
    // TEMPORARY audit harness. Delete after use.
    public class AuditTests
    {
        class Bot
        {
            public string Name = "";
            public int Keep = 60;
            public DeskAction? Desk;
            public bool NoDesk;
            public int Slips = 2;
            public bool Specials = true;
            public bool NeverPlay;
            public bool FirstRoute;
        }

        class Stats
        {
            public int Wins, Games, Repunched, Unused, Specials, Bribes, Scandals, Smalls, Cleans;
            public int[] LostWeek = new int[Config.Weeks + 1];
            public List<int> Cash = new List<int>();
            public int Rate => Wins * 100 / Games;
        }

        static float Seconds(int cards) => 4 + 3 * cards;

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

        static KeyValuePair<int, List<int>> Plan(Run g, IList<Card> hand, int roll, int limit, Slip slip, int keep)
        {
            var routes = Run.Routes(roll, hand, limit);
            if (routes.Count == 0) return new KeyValuePair<int, List<int>>(roll, new List<int>());
            return routes.OrderBy(kv => Score(g, hand, kv.Key, kv.Value, slip, keep)).First();
        }

        static double Expect(Run g, IList<Card> hand, Slip slip, int keep, Random r)
        {
            double sum = 0;
            for (int k = 0; k < 16; k++) { var p = Plan(g, hand, r.Next(100), Run.CardLimit(g.Week), slip, keep); sum += Score(g, hand, p.Key, p.Value, slip, keep); }
            return sum / 16;
        }

        static Stats Play(Bot bot, int games, int seed)
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
                    if (g.Offer != Slip.None)
                    {
                        bool take = bot.Slips == 1 || (bot.Slips == 2 && (g.Offer == Slip.Bulk ? g.Cash < 250
                            : Expect(g, g.Hand, g.Offer, bot.Keep, sample) < Expect(g, g.Hand, Slip.None, bot.Keep, sample)));
                        if (take) g.TakeSlip(); else g.TearSlip();
                    }
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
                        if (bh < 0 && g.Rack.Count >= Config.RackMax)
                        {
                            br = g.Rack.FindIndex(c => c.IsRandom);
                            bh = g.Hand.FindIndex(c => !c.IsSpecial);
                        }
                        if (bh < 0 || br < 0) break;
                        g.Repunch(bh, br);
                        st.Repunched++;
                        worth = Run.IsInspection(g.Week) ? false : worth;
                        if (g.Rack.Count < Config.RackMax && !Run.IsInspection(g.Week) && g.Today != Special.Nephew && g.Pinned == Slip.None) break;
                    }
                    if (!bot.NoDesk)
                    {
                        var desk = bot.Desk ?? (g.Hand.Count(c => !c.IsSpecial) <= 2 && g.Cash > 220 ? DeskAction.Supplier
                            : g.Cash < 120 ? DeskAction.Bribe : DeskAction.LieLow);
                        if (desk == DeskAction.Shred) g.TakeAction(desk, Enumerable.Range(0, 100).OrderByDescending(n => g.Board[n]).First());
                        else if (desk == DeskAction.Supplier && g.Hand.Count >= Config.HandLimit)
                        {
                            if (!g.TakeAction(desk, Math.Max(0, g.Hand.FindIndex(c => !c.IsSpecial)))) g.TakeAction(DeskAction.LieLow);
                        }
                        else if (!g.TakeAction(desk)) g.TakeAction(DeskAction.LieLow);
                    }
                    g.DrawNumber();
                    int roll = g.Roll.Value, limit = Run.CardLimit(g.Week);
                    var plan = bot.FirstRoute
                        ? Run.Routes(roll, g.Hand, limit).Where(kv => g.TierAt(kv.Key, kv.Value.Count) == Tier.Clean).OrderBy(kv => kv.Value.Count).FirstOrDefault()
                        : Plan(g, g.Hand, roll, limit, g.Pinned, bot.Keep);
                    if (bot.FirstRoute && plan.Value == null) plan = new KeyValuePair<int, List<int>>(roll, new List<int>());
                    var played = bot.NeverPlay ? new List<int>() : plan.Value.ToList();
                    int to = bot.NeverPlay ? roll : plan.Key, jam = g.Hand.FindIndex(c => c.IsRandom);
                    if (!bot.NeverPlay && jam >= 0 && Score(g, g.Hand, to, played, g.Pinned, bot.Keep) > 0)
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
                st.Cash.Add(g.Cash);
                st.Bribes += g.Bribes;
                st.Scandals += g.Scandals;
                st.Smalls += g.Smalls;
                st.Cleans += g.Cleans;
                if (g.Over == Ending.Won) st.Wins++; else st.LostWeek[g.Week]++;
            }
            return st;
        }

        static string Pct(List<int> xs, double q)
        {
            var s = xs.OrderBy(v => v).ToList();
            return s[Math.Min(s.Count - 1, (int)(q * s.Count))].ToString();
        }

        [Test]
        public void Audit_balance()
        {
            var log = new System.Text.StringBuilder();
            int N = 100;
            var sw = Stopwatch.StartNew();
            var smart = Play(new Bot(), N, 30);
            sw.Stop();
            var spends = Play(new Bot { Keep = 0 }, N, 30);
            var plain = Play(new Bot { Specials = false }, N, 30);
            var noDesk = Play(new Bot { NoDesk = true }, N, 30);
            var never = Play(new Bot { NeverPlay = true }, N, 30);
            var firstClean = Play(new Bot { FirstRoute = true }, N, 30);
            var take = Play(new Bot { Slips = 1 }, N, 30);
            var tear = Play(new Bot { Slips = 0 }, N, 30);

            log.AppendLine($"AUDIT games/policy {N}; smart sim {sw.ElapsedMilliseconds} ms total, {sw.Elapsed.TotalMilliseconds / N:0.00} ms/run");
            log.AppendLine($"AUDIT win%: smart {smart.Rate} | spends {spends.Rate} | arithmetic-only {plain.Rate} | no-desk {noDesk.Rate} | never-play {never.Rate} | first-clean {firstClean.Rate} | slips-take {take.Rate} | slips-tear {tear.Rate}");
            log.AppendLine($"AUDIT smart cash p10/p50/p90: {Pct(smart.Cash, .1)}/{Pct(smart.Cash, .5)}/{Pct(smart.Cash, .9)}; spends p50 {Pct(spends.Cash, .5)}; never p50 {Pct(never.Cash, .5)}");
            log.AppendLine($"AUDIT smart per-run: cleans {smart.Cleans / (float)N:0.0}, smalls {smart.Smalls / (float)N:0.0}, scandals {smart.Scandals / (float)N:0.0}, bribes {smart.Bribes / (float)N:0.0}, specials {smart.Specials / (float)N:0.0}, repunched {smart.Repunched / (float)N:0.0}, unused {smart.Unused * 100 / Math.Max(1, smart.Repunched)}%");
            log.AppendLine($"AUDIT loss week histogram (smart): {string.Join(" ", Enumerable.Range(1, Config.Weeks).Select(w => $"w{w}:{smart.LostWeek[w] * 100f / N:0.0}%"))}");

            foreach (var d in new[] { DeskAction.Supplier, DeskAction.Bribe, DeskAction.Shred, DeskAction.LieLow })
                log.AppendLine($"AUDIT always {d}: {Play(new Bot { Desk = d }, N, 30).Rate}%");
            foreach (var keep in new[] { 0, 20, 40, 60, 100, 200 })
                log.AppendLine($"AUDIT keep {keep}: {Play(new Bot { Keep = keep }, N, 30).Rate}%");
            // Does cash pressure ever bite? Track how often the run is below 50 / 20 at week start.
            CheckPressure(log, N);
            UnityEngine.Debug.Log(log.ToString());
        }

        static void CheckPressure(System.Text.StringBuilder log, int games)
        {
            var rng = new Random(77);
            int low50 = 0, low20 = 0, weeks = 0, boardFill = 0, prizeSum = 0, handFull = 0, draws = 0;
            var byWeek = new int[Config.Weeks + 1];
            for (int i = 0; i < games; i++)
            {
                var g = new Run(rng); g.ShuffleDates();
                while (g.Over == Ending.None)
                {
                    g.StartWeek();
                    weeks++; draws++;
                    if (g.Cash < 50) low50++;
                    if (g.Cash < 20) low20++;
                    boardFill += g.Board.Sum();
                    prizeSum += g.Prize;
                    if (g.Hand.Count >= Config.HandLimit) handFull++;
                    byWeek[g.Week] += g.Board.Sum();
                    g.DrawNumber();
                    var plan = Run.Routes(g.Roll.Value, g.Hand, Run.CardLimit(g.Week)).OrderBy(kv => g.CostOf(kv.Key, kv.Value.Count) + kv.Value.Count * 60).First();
                    var a = g.Announce(plan.Key, plan.Value.Count, true, 8);
                    g.Discard(plan.Value);
                }
            }
            log.AppendLine($"AUDIT pressure: weeks {weeks}, cash<50 {low50 * 100f / weeks:0.0}%, cash<20 {low20 * 100f / weeks:0.0}%, hand full {handFull * 100f / draws:0.0}%, mean board holders {boardFill / (float)weeks:0.0}, mean prize {prizeSum / (float)weeks:0.0}");
            log.AppendLine($"AUDIT board by week: {string.Join(" ", Enumerable.Range(1, Config.Weeks).Select(w => $"w{w}:{byWeek[w] / (float)Math.Max(1, games):0}"))}");
        }
    }
}
