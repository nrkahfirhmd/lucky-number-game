using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NoWinners
{
    // Draws the monitor, TV, HUD and desk labels from game state.
    // Monitor, TV and HUD follow temp/design_handoff_no_winners_screens (screens.js) pixel for pixel, on a 640x360, 8 px grid.
    public class Screens
    {
        public const int Square = 30, Cell = 32;
        static Color32 H(string hex) => Room.Hex(hex);
        static readonly Color32 Bg = H("#23093A"), Panel = H("#3A1058"), Line = H("#7A3AA0"), Ink = H("#FFF3D6"), Dim = H("#BE9ADB"), Label = H("#FFC93C"),
            Good = H("#5CF2C8"), Alarm = H("#FF6D90"), Socket = H("#120420"), Curtain = H("#A81440"), Curtain2 = H("#7A0F2E"), Black = new Color32(0, 0, 0, 255);
        // Holder tiers: fill, number, count.
        static readonly Color32[][] Tiers =
        {
            new[] { H("#5A2A9E"), H("#FFF3D6"), H("#CFB4F5") }, new[] { H("#C2187A"), H("#FFF3D6"), H("#FFC4E4") },
            new[] { H("#FF8A1F"), H("#23093A"), H("#5C1F00") }, new[] { H("#F5D23C"), H("#23093A"), H("#5A4800") },
        };
        static readonly Color32[] Bars = { H("#FFC93C"), H("#FF3D6E"), H("#5CF2C8"), H("#C2187A"), H("#7A3AA0"), H("#FF8A1F"), H("#FFF3D6") };
        const string TickerOn = "   TICKET SALES UP AGAIN   *   NUMBER 77 STILL DUE, SAY EXPERTS   *",
            TickerTitle = "   TONIGHT ON THE BIG DRAW   *   COULD IT BE YOU?   *   NO   *",
            TickerWon = "   THAT WAS THE SEASON   *   SEE YOU IN THE SPRING   *",
            TickerTelethon = "   TELETHON NIGHT   *   PRIZE DOUBLED   *   GIVE GENEROUSLY   *   TO US   *";
        static readonly string[] Winners = { "NO WINNERS", "ONE WINNER", "TWO WINNERS", "THREE WINNERS" };

        public static string Holders(int n) => n == 1 ? "1 holder" : $"{n} holders";
        static int TierOf(int h) => h <= 2 ? 0 : h <= 5 ? 1 : h <= 10 ? 2 : 3;

        readonly Game game;
        Room Room => game.Room;
        Run S => game.S;
        WeekResult Result => game.Result;
        static bool Still => Prefs.ReduceMotion;

        public Screens(Game game) { this.game = game; Hud.Tex.filterMode = FilterMode.Point; }

        // Moving things, at the art's stepped frame rate.
        public void DrawTimed()
        {
            float t = Time.time * 1000;
            DrawMonitor(t);
            DrawTv(t);
            DrawHud();
            DrawClock();
            DrawMachineFace();
        }

        // Things that only change when the game does.
        public void DrawDesk()
        {
            DrawCards();
            DrawPlates();
            DrawCalendar();
            DrawPaper();
        }

        // ---- Monitor: the show's board and number. No player stats. ----
        void DrawMonitor(float t)
        {
            var g = Room.Monitor;
            g.Fill(Bg);
            g.Text("THE BIG DRAW", 16, 8, Label);
            if (game.Mode == Mode.Title || game.Mode == Mode.Start)
            {
                DrawBoard(g, empty: true);
                NumberBox(g, "--", Dim);
                string[] items = { "START", "SETTINGS", "QUIT" };
                for (int i = 0; i < items.Length; i++) MonButton(g, 352, 184 + i * 40, 272, items[i], game.Sel == i);
            }
            else if (game.Mode == Mode.Settings) { DrawBoard(g); SettingsPanel(g); }
            else if (Result != null) MonitorResult(g);
            else MonitorWeek(g, t);
            g.Apply();
        }

        void MonitorWeek(Canvas8 g, float t)
        {
            DrawBoard(g);
            if (S.Today == Special.Nephew) foreach (int n in S.Nephews) Mark(g, n, Label); // The minister's numbers, all week.
            if (game.Preview >= 0) Dashed(g, game.Preview);
            if (!game.Drawn)
            {
                NumberBox(g, "??", Dim); // Locked until the machine stops.
                if (game.Live) return;
                g.ChipFrame(352, 224, 272, 40, Line, Bg);
                g.Text("TICKETS", 420, 236, Dim, 1, 2);
                g.Text($"+{S.Sold * Config.TicketPrice}", 492, 236, Label, 2, 2);
                return;
            }
            // A card that just landed rolls the digits; the marks and verdict wait for it.
            bool rolling = game.Played.Count > 0 && !Still && t - game.LandedAt * 1000 < 300;
            var cards = game.Played.Take(game.Played.Count - (rolling ? 1 : 0)).Select(i => S.Hand[i]).ToList();
            var steps = Steps(S.Roll.Value, cards);
            int now = steps[steps.Count - 1];
            for (int i = 0; i < steps.Count - 1; i++) Mark(g, steps[i], Dim);
            Mark(g, now, Ink);
            NumberBox(g, rolling ? $"{(int)(t / 60) * 37 % 100:00}" : $"{now:00}", Ink);
            Chain(g, Tokens(S.Roll.Value, cards), 184, Ink, Label);
            int h = S.Board[now], cost = S.CostOf(now, cards.Count);
            if (S.IsFavour(now)) Verdict(g, cost <= 0 ? $"NEPHEW +{-cost}" : $"NEPHEW -{cost}", cost <= 0 ? Good : Label);
            else if (h == 0) Verdict(g, "NOBODY HOLDS", Good);
            else Verdict(g, $"{h} HOLD -{cost}", Alarm);
        }

        void MonitorResult(Canvas8 g)
        {
            var a = Result.Air;
            Color32 c = a.Tier == Tier.Clean ? Good : a.Tier == Tier.Small ? Label : Alarm;
            DrawBoard(g);
            Mark(g, Result.Number, c);
            NumberBox(g, $"{Result.Number:00}", c);
            Chain(g, Tokens(Result.From, Result.Path), 184, c, Label);
            if (a.Tier != Tier.Scandal) { Verdict(g, a.Tier == Tier.Clean ? "NOBODY WON" : a.Reward > 0 ? "NEPHEW WON" : "SMALL WINNER", c); return; }
            Verdict(g, "SCANDAL", c, 2, 3);
            if (Result.Best.To == Result.Number) return;
            // The route that was there: clean if one existed, else the cheapest.
            bool clean = Result.Best.Cost <= 0;
            Color32 route = clean ? Good : Label;
            Mark(g, Result.Best.To, route);
            g.Centred(Result.Best.Cost < 0 ? "NEPHEW ROUTE" : clean ? "CLEAN ROUTE" : "CHEAPEST ROUTE", 280, Dim, 1, 1, 488);
            var toks = Result.BestPath.Select(k => k.ToString()).ToList();
            toks.Add($"{Result.Best.To:00}");
            Chain(g, toks, 296, route, route);
        }

        void DrawBoard(Canvas8 g, bool empty = false)
        {
            for (int n = 0; n < 100; n++)
            {
                int x = 16 + n % 10 * Cell, y = 24 + n / 10 * Cell, h = empty ? 0 : S.Board[n];
                if (Result != null && n == Result.Number && !empty) h = Result.Air.Holders; // Announcing clears the square; the board still shows who held it.
                if (h == 0)
                {
                    // Free squares differ in shape as well as colour: a square socket, no count.
                    g.Rect(x, y, Square, Square, Line);
                    g.Rect(x + 1, y + 1, Square - 2, Square - 2, Socket);
                    g.Text($"{n:00}", x + 7, y + 11, Good);
                    continue;
                }
                var tier = Tiers[TierOf(h)];
                string count = Mathf.Min(h, 99).ToString();
                int ix = count.Length > 1 ? x + 3 : x + 8;
                g.Chip(x, y, Square, Square, tier[0]);
                g.Text($"{n:00}", x + 7, y + 4, tier[1]);
                Person(g, ix, y + 19, tier[2]);
                g.Text(count, ix + 7, y + 18, tier[2]);
            }
        }

        static readonly string[] PersonRows = { "01110", "01110", "00000", "11111", "11111", "11111" };
        static void Person(Canvas8 g, int x, int y, Color32 c)
        {
            for (int r = 0; r < PersonRows.Length; r++)
                for (int k = 0; k < 5; k++)
                    if (PersonRows[r][k] == '1') g.Rect(x + k, y + r, 1, 1, c);
        }

        // 2 px frame that fits exactly in the gaps between cells.
        static void Mark(Canvas8 g, int n, Color32 c) => g.Frame(14 + n % 10 * Cell, 22 + n / 10 * Cell, 34, 34, c);

        // Hover preview: where a card would land, or the square the shredder aims at.
        static void Dashed(Canvas8 g, int n)
        {
            int x = 16 + n % 10 * Cell, y = 24 + n / 10 * Cell;
            for (int i = 0; i < Square + 4; i += 6)
            {
                g.Rect(x - 2 + i, y - 2, 3, 2, Ink); g.Rect(x - 2 + i, y + Square, 3, 2, Ink);
                g.Rect(x - 2, y - 2 + i, 2, 3, Ink); g.Rect(x + Square, y - 2 + i, 2, 3, Ink);
            }
        }

        static void NumberBox(Canvas8 g, string label, Color32 c)
        {
            g.Chip(352, 24, 272, 144, Panel);
            g.Centred(label, 48, c, 10, 12, 488);
        }

        static List<int> Steps(int from, List<Card> cards)
        {
            var steps = new List<int> { from };
            foreach (var k in cards) steps.Add(Run.Apply(steps[steps.Count - 1], k) ?? steps[steps.Count - 1]);
            return steps;
        }

        // Start, then each card and the number it lands on, so nobody does the arithmetic in their head.
        static List<string> Tokens(int from, List<Card> cards)
        {
            var steps = Steps(from, cards);
            var toks = new List<string> { $"{from:00}" };
            for (int i = 0; i < cards.Count; i++) { toks.Add(cards[i].ToString()); toks.Add($"{steps[i + 1]:00}"); }
            return toks;
        }

        static bool IsCard(string s) => !char.IsDigit(s[0]);
        static int TokenWidth(string s) => s.Length * 8 + (IsCard(s) ? 16 : 0);
        static int ChainWidth(List<string> toks) => toks.Sum(TokenWidth) + 8 * (toks.Count - 1);

        static void Chain(Canvas8 g, List<string> toks, int y, Color32 last, Color32 cardInk)
        {
            // Too wide for the column: drop the in-between numbers, then the oldest steps.
            if (ChainWidth(toks) > 272) toks = toks.Where((s, i) => i == 0 || i == toks.Count - 1 || IsCard(s)).ToList();
            while (ChainWidth(toks) > 272) toks.RemoveAt(0);
            int x = 488 - ChainWidth(toks) / 2;
            for (int i = 0; i < toks.Count; i++)
            {
                string s = toks[i];
                int w = TokenWidth(s);
                if (IsCard(s)) { g.Chip(x, y, w, 24, Panel); g.Text(s, x + 8, y + 4, cardInk, 1, 2); }
                else g.Text(s, x, y + 4, i == toks.Count - 1 ? last : Dim, 1, 2);
                x += w + 8;
            }
        }

        static void Verdict(Canvas8 g, string text, Color32 c, int sx = 2, int sy = 2)
        {
            g.ChipFrame(352, 224, 272, 40, c, Bg);
            g.Centred(text, 244 - 4 * sy, c, sx, sy, 488);
        }

        static void MonButton(Canvas8 g, int x, int y, int w, string label, bool sel)
        {
            if (sel) { g.Chip(x, y, w, 32, Label); g.Text(">", x + 12, y + 8, Bg, 1, 2); g.Centred(label, y + 8, Bg, 2, 2, x + w / 2); }
            else { g.ChipFrame(x, y, w, 32, Line, Bg); g.Centred(label, y + 8, Ink, 2, 2, x + w / 2); }
        }

        void SettingsPanel(Canvas8 g)
        {
            g.Text("SETTINGS", 352, 24, Label, 2, 2);
            // Rows every 64 px from y 44, BACK below. Game.ClickMenu hit-tests the same layout.
            SetRow(g, 44, "CRT STRENGTH", game.Sel == 0);
            Slider(g, 44, Prefs.Crt);
            SetRow(g, 108, "VOLUME", game.Sel == 1);
            Slider(g, 108, Prefs.Volume);
            SetRow(g, 172, "REDUCE MOTION", game.Sel == 2);
            Seg(g, 364, 196, 72, new[] { "OFF", "ON" }, Prefs.ReduceMotion ? 1 : 0);
            SetRow(g, 236, "ON-AIR TARGET", game.Sel == 3);
            Seg(g, 364, 260, 104, new[] { "NORMAL", "LONG" }, Prefs.Long ? 1 : 0);
            MonButton(g, 352, 300, 272, "BACK", game.Sel == Game.SettingRows);
        }

        static void Slider(Canvas8 g, int y, float v)
        {
            int fill = Mathf.RoundToInt(176 * v);
            g.Rect(364, y + 32, 176, 8, Line);
            g.Rect(364, y + 32, fill, 8, Label);
            g.Rect(364 + fill - 4, y + 24, 8, 24, Ink);
            g.Right($"{Mathf.RoundToInt(v * 100)}%", 612, y + 28, Ink, 1, 2);
        }

        static void SetRow(Canvas8 g, int y, string label, bool focused)
        {
            if (focused) g.ChipFrame(352, y, 272, 56, Label, Panel); else g.Chip(352, y, 272, 56, Panel);
            g.Text(label, 364, y + 8, focused ? Ink : Dim);
        }

        static void Seg(Canvas8 g, int x, int y, int w, string[] opts, int sel)
        {
            for (int i = 0; i < opts.Length; i++)
            {
                int xx = x + i * (w + 8);
                g.Chip(xx, y, w, 24, i == sel ? Good : Bg);
                g.Centred(opts[i], y + 4, i == sel ? Bg : Dim, 1, 2, xx + w / 2);
            }
        }

        // ---- TV: pure show ----
        void DrawTv(float t)
        {
            var g = Room.Tv;
            var m = game.Mode;
            if (m == Mode.Start || m == Mode.Title || (m == Mode.Settings && game.SettingsFrom == Mode.Title)) TitleCard(g, t);
            else if (Result != null)
            {
                float p = (Time.time - Result.At) * 1000;
                if (S.Over == Ending.Lost) Collapse(g, p, t);
                else if (S.Over == Ending.Won && p >= 2000) SeasonOver(g, t);
                else ResultTv(g, t);
            }
            else if (game.Live) Live(g, t);
            else OffAir(g);
            g.Apply();
        }

        void OffAir(Canvas8 g)
        {
            g.Fill(H("#101020"));
            for (int i = 0; i < Bars.Length; i++) g.Rect(i * 92, 0, 92, 240, Bars[i]);
            g.Centred("DRAW COMING UP", 262, H("#F2F2E6"), 2, 3);
            g.Centred($"{S.Sold} NEW TICKETS SOLD", 312, H("#F2F2E6"));
        }

        static void Stage(Canvas8 g, float t, string ticker, bool live = true, bool header = true)
        {
            for (int x = 0; x < g.W; x += 24) g.Rect(x, 0, 24, g.H, x % 48 != 0 ? Curtain2 : Curtain);
            g.Rect(56, 40, 528, 236, Bg);
            Bulbs(g, 56, 40, 528, 236, t);
            if (live) { g.Rect(8, 8, 56, 16, Alarm); g.Text("LIVE", 20, 12, Bg); }
            if (header) g.Centred("THE BIG DRAW!", 52, Label, 2, 3);
            // Ticker: 30 px/s in whole pixels, repeated so it loops.
            g.Rect(0, 318, g.W, 16, Label);
            int p = ticker.Length * 8, off = Still ? 0 : (int)(t * .03f);
            for (int x = 8 - off % p - p; x < g.W; x += p) g.Text(ticker, x, 322, Bg);
        }

        static void Bulbs(Canvas8 g, int x, int y, int w, int h, float t)
        {
            int phase = Still ? 0 : (int)(t / 180) % 3, i = 0;
            void Dot(int a, int b) => g.Rect(a, b, 4, 4, (i++ + 3 - phase) % 3 == 0 ? Label : Line);
            for (int a = x + 2; a < x + w - 4; a += 12) { Dot(a, y + 2); Dot(a, y + h - 6); }
            for (int b = y + 14; b < y + h - 8; b += 12) { Dot(x + 2, b); Dot(x + w - 6, b); }
        }

        static void Ball(Canvas8 g, string s, Color32 face)
        {
            g.Disc(320, 130, 44, Label);
            g.Disc(320, 130, 40, face);
            g.Text(s, 320 - s.Length * 16, 110, Bg, 4, 5);
        }

        static void TitleCard(Canvas8 g, float t)
        {
            Stage(g, t, TickerTitle, live: false, header: false);
            g.Centred("NO WINNERS", 96, Label, 4, 6);
            g.Centred("THE LOTTERY NOBODY WINS", 160, Ink, 1, 2);
            if (Still || (int)(t / 500) % 2 == 0) g.Centred("PRESS ANY KEY", 216, Ink, 2, 2);
        }

        // On air, before the result: the stage waits on a mystery ball. Stalling dims the ball and runs the host's filler.
        void Live(Canvas8 g, float t)
        {
            bool stalling = game.Stall >= 0;
            Stage(g, t, S.Today == Special.Telethon ? TickerTelethon : TickerOn);
            Ball(g, "??", stalling ? Dim : Ink);
            if (stalling)
            {
                g.Centred(Game.StallTitle(game.Stall), 196, Alarm, 2, 2);
                g.Centred(Game.StallLine(game.Stall), 228, Dim);
            }
            else g.Centred("THE BALLS ARE ROLLING", 196, Ink, 2, 2);
        }

        void ResultTv(Canvas8 g, float t)
        {
            var a = Result.Air;
            string n = $"{Result.Number:00}";
            string ticker = a.Reward > 0 ? $"   MINISTER'S NEPHEW WINS ON {n}   *   MINISTER \"AS SURPRISED AS ANYONE\"   *"
                : a.Tier == Tier.Clean ? "   NO WINNER AGAIN THIS WEEK   *   STATION SENDS ITS SYMPATHY   *"
                : a.Tier == Tier.Small ? $"   {(a.Holders < Winners.Length ? Winners[a.Holders] : $"{a.Holders} WINNERS")} ON {n}   *   STATION \"DELIGHTED FOR THEM\"   *"
                : $"   {n} FINALLY COMES IN   *   BOARD CALLS EMERGENCY MEETING   *";
            Stage(g, t, ticker);
            Ball(g, n, Ink);
            g.Centred(a.Holders == 0 ? "NOBODY WON!" : a.Holders == 1 ? "1 WINNER!" : $"{a.Holders} WINNERS!", 196, a.Tier == Tier.Scandal ? Alarm : Label, 2, 2);
            g.Centred(a.Reward > 0 ? "WHAT ARE THE ODDS." : a.Tier == Tier.Clean ? "WHAT A SHAME!" : a.Tier == Tier.Small ? "LUCKY THEM." : "THE STATION IS THRILLED. REALLY.", 228, Ink);
        }

        static void SeasonOver(Canvas8 g, float t)
        {
            Stage(g, t, TickerWon);
            g.Centred($"{Config.Weeks} WEEKS.", 128, Ink, 2, 3);
            g.Centred("NOBODY WON.", 168, Label, 2, 3);
        }

        static void StandBy(Canvas8 g)
        {
            g.Fill(Bg);
            g.Centred("PLEASE STAND BY", 168, Label, 2, 3);
        }

        // Bankrupt: the picture squashes to a line, the line shrinks, black, then STAND BY. p is ms since the result aired.
        void Collapse(Canvas8 g, float p, float t)
        {
            if (p < 1000 || (p < 1400 && !Still))
            {
                ResultTv(g, t);
                if (p >= 1000) g.Squash(Mathf.Max(2, Mathf.RoundToInt(360 * (1 - (p - 1000) / 400) / 2) * 2));
                return;
            }
            g.Fill(Black);
            if (Still) { if (p >= 1500) StandBy(g); return; }
            if (p < 1700) g.Rect(0, 179, 640, 2, Ink);
            else if (p < 1900) { int w = Mathf.RoundToInt((640 - 632 * (p - 1700) / 200) / 2) * 2; g.Rect(320 - w / 2, 179, w, 2, Ink); }
            else if (p >= 2400) StandBy(g);
        }

        // ---- HUD: the player's own numbers, flat over the game window. No CRT. ----
        public readonly Canvas8 Hud = new Canvas8(640, 360);
        static readonly Color32 HInk = H("#F2F2E6"), HDim = H("#8A8A90"), HGold = H("#FFD23A"), HRed = H("#FF3B2F"), HPlate = H("#111111"), HLine = H("#4A4A50"),
            Clear = new Color32(0, 0, 0, 0);

        void DrawHud()
        {
            var g = Hud;
            g.Fill(Clear);
            if (game.Mode == Mode.Paused) PauseScreen(g);
            else if (game.Mode == Mode.Play)
            {
                Bar(g);
                if (Result != null && S.Over != Ending.None) { if (game.EndCardUp) EndCard(g); }
                else if (Result != null && Result.Revealed) ResultCard(g);
            }
            g.Apply();
        }

        void Bar(Canvas8 g)
        {
            bool inspection = Run.IsInspection(S.Week);
            g.Rect(0, 0, 640, 24, HPlate);
            g.Rect(0, 24, 640, 2, HLine);
            var slots = new[]
            {
                ("WEEK", $"{S.Week:00}/{Config.Weeks}", inspection ? HRed : HInk), ("CASH", S.Cash.ToString(), S.Cash < 0 ? HRed : HGold),
                ("STREAK", $"{S.Streak:00}", HInk), ("PRIZE", S.Prize.ToString(), HGold),
            };
            for (int i = 0; i < slots.Length; i++)
            {
                var (label, value, c) = slots[i];
                int x = 16 + i * 160;
                g.Text(label, x, 8, HDim);
                g.Text(value, x + label.Length * 8 + 8, 4, c, 1, 2);
            }
            if (Result != null) return;
            if (inspection)
            {
                g.Chip(16, 32, 160, 24, HRed);
                g.Text($"INSPECTION: {Config.InspectionCards} CARD", 24, 36, HPlate, 1, 2);
                return;
            }
            string date = S.Today == Special.Payday ? $"PAYDAY: {Config.PaydayTickets}X TICKETS"
                : S.Today == Special.Telethon ? $"TELETHON: PRIZE X{Config.TelethonPrize}"
                : S.Today == Special.Nephew ? $"NEPHEW: {string.Join(" ", S.Nephews.Select(n => n.ToString("00")))}" : null;
            if (date == null) return;
            g.Chip(16, 32, date.Length * 8 + 16, 24, HGold);
            g.Text(date, 24, 36, HPlate, 1, 2);
        }

        static void HudButton(Canvas8 g, int x, int y, int w, string label, bool sel)
        {
            if (sel) { g.Chip(x, y, w, 32, HGold); g.Text(">", x + 12, y + 8, HPlate, 1, 2); g.Centred(label, y + 8, HPlate, 2, 2, x + w / 2); }
            else { g.ChipFrame(x, y, w, 32, HLine, HPlate); g.Centred(label, y + 8, HInk, 2, 2, x + w / 2); }
        }

        static void HudRows(Canvas8 g, int x, int xr, int y, (string, string, Color32)[] rows)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                var (label, value, c) = rows[i];
                g.Text(label, x, y + i * 24 + 4, HDim);
                g.Right(value, xr, y + i * 24, c, 1, 2);
            }
        }

        // The receipt for the week.
        void ResultCard(Canvas8 g)
        {
            var a = Result.Air;
            bool scandal = a.Tier == Tier.Scandal;
            var rows = new List<(string, string, Color32)>
            {
                ("ON TIME", a.Bonus > 0 ? $"+{a.Bonus}" : "0", a.Bonus > 0 ? HGold : HDim),
                ("PAYOUT", a.Pay > 0 ? $"-{a.Pay}" : "0", a.Pay > 0 ? HRed : HDim),
                ("CASH", $"{Result.CashBefore} > {S.Cash}", scandal ? HRed : HGold),
                ("STREAK", $"{Result.StreakBefore:00} > {S.Streak:00}", scandal ? HRed : HInk),
            };
            if (a.Reward > 0) rows.Insert(1, ("MINISTER", $"+{a.Reward}", HGold));
            int extra = (rows.Count - 4) * 24; // Grows downward; the click anywhere still works.
            g.ChipFrame(176, 64, 288, 232 + extra, HLine, HPlate);
            g.Text($"WEEK {S.Week:00}", 192, 80, HInk, 2, 2);
            HudRows(g, 192, 448, 120, rows.ToArray());
            g.Rect(192, 224 + extra, 256, 2, HLine);
            HudButton(g, 192, 240 + extra, 256, "NEXT WEEK", true);
        }

        void EndCard(Canvas8 g)
        {
            bool won = S.Over == Ending.Won;
            g.ChipFrame(152, 40, 336, 280, HLine, HPlate);
            g.Centred(won ? "SEASON COMPLETE" : "BANKRUPT", 56, won ? HGold : HRed, 2, 2);
            g.Centred("FINAL CASH", 88, HDim);
            g.Centred(Mathf.Max(0, S.Cash).ToString(), 104, won ? HGold : HRed, 4, 4);
            HudRows(g, 176, 464, 152, new[]
            {
                won ? ("WEEKS SURVIVED", $"{S.Week}/{Config.Weeks}", HInk) : ("ENDED IN WEEK", $"{S.Week:00}", HInk),
                ("CLEAN DRAWS", $"{S.Cleans:00}", HInk), ("BEST STREAK", $"{S.Best:00}", HInk),
            });
            HudButton(g, 176, 232, 288, "PLAY AGAIN", game.Sel == 0);
            HudButton(g, 176, 272, 288, "BACK TO TITLE", game.Sel == 1);
        }

        // A dark overlay over the paused game.
        void PauseScreen(Canvas8 g)
        {
            g.Fill(new Color32(0, 0, 0, 170));
            g.Centred("PAUSED", 112, HInk, 2, 3);
            string[] items = { "RESUME", "SETTINGS", "QUIT TO TITLE" };
            for (int i = 0; i < items.Length; i++) HudButton(g, 200, 168 + i * 40, 240, items[i], game.Sel == i);
        }

        // ---- Desk ----
        void DrawClock()
        {
            var c = Room.Clock;
            bool on = game.Live && Result == null;
            int left = Mathf.CeilToInt(Prefs.Target - game.LiveSeconds);
            c.Fill(H("#080808"));
            c.Centred(on ? "ON AIR" : "OFF AIR", 6, H(on ? "#ff3b2f" : "#8a8a90"));
            c.Centred(!on ? "--" : left > 0 ? $"{left:00}" : "LATE", 24, H(on && left <= 5 ? "#ff3b2f" : "#ffd23a"), 4 - (on && left <= 0 ? 1 : 0));
            c.Apply();
        }

        void DrawMachineFace()
        {
            var c = Room.MachineFace;
            c.Fill(H("#080808"));
            int? shown = Result != null ? Result.From : S.Roll;
            string label = Time.time - game.RolledAt < Game.LockSeconds ? $"{Random.Range(0, 100):00}" : shown == null ? "--" : $"{shown:00}";
            c.Centred(label, 16, H("#ffd23a"), 5);
            c.Apply();
        }

        void DrawCards()
        {
            int n = S.Hand.Count;
            for (int i = 0; i < Room.CardCount; i++)
            {
                var card = Room.Cards[i];
                bool show = i < n && !game.Played.Contains(i);
                card.gameObject.SetActive(show);
                if (!show) continue;
                card.position = new Vector3((i - (n - 1) / 2f) * Room.CardStep, Room.CardBaseY, Room.CardZ);
                PunchCard(Room.CardFaces[i], S.Hand[i], game.CanPlay(i));
            }
        }

        // Punch-card fiction, decided in GDD section 11.
        static void PunchCard(Canvas8 c, Card card, bool ok)
        {
            Color32 ink = H(ok ? "#2a2010" : "#4a4232"), hole = H(ok ? "#5a4420" : "#3a3424");
            c.Fill(H(ok ? "#D9C79A" : "#7a7058"));
            c.Rect(0, 0, 16, 16, H("#6b4a2e"));
            c.Centred("CORRECT", 12, ink);
            string label = card.ToString();
            c.Centred(label, 52, ink, 4);
            int seed = label[0] + label[1];
            for (int x = 0; x < 7; x++)
                for (int y = 0; y < 3; y++)
                    if ((seed + x * 3 + y * 5) % 3 != 0) c.Rect(11 + x * 16, 108 + y * 20, 8, 12, hole);
            c.Apply();
        }

        static void Plate(Canvas8 c, string label, Color32 ink, int scale = 2)
        {
            c.Fill(H("#111111"));
            c.Centred(label, (c.H - 8 * scale) / 2, ink, scale);
            c.Apply();
        }

        void DrawPlates()
        {
            bool off = !game.Live && Result == null;
            Color32 on = H("#f2f2e6"), dark = H("#6a6a70");
            bool airing = game.Live && Result == null;
            Plate(Room.LivePlate, airing ? "ON AIR" : "GO LIVE", airing ? H("#ff3b2f") : off ? on : dark);

            string go = S.Over != Ending.None ? "PLAY AGAIN" : Result != null ? "NEXT WEEK" : game.Drawn ? $"RUN {game.Value:00}" : game.Live ? "DRAWING" : "GO LIVE FIRST";
            Plate(Room.GoPlate, go, game.Drawn || Result != null ? on : H("#8a8a90"));
            Plate(Room.UndoPlate, "EJECT", on);
        }

        // Calendar tiles on the 256x320 paper. Game.CalendarAt hit-tests the same layout.
        public const int CalX = 8, CalY = 48, CalW = 57, CalH = 82, CalStepX = 61, CalStepY = 88;
        public static int CalendarWeekAt(int px, int py)
        {
            int col = (px - CalX) / CalStepX, row = (py - CalY) / CalStepY;
            if (px < CalX || py < CalY || col > 3 || row > 2 || (px - CalX) % CalStepX >= CalW || (py - CalY) % CalStepY >= CalH) return 0;
            return row * 4 + col + 1;
        }

        void DrawCalendar()
        {
            var c = Room.Calendar;
            c.Fill(H("#E8E2CC"));
            c.Rect(0, 0, c.W, 40, H("#B3241C"));
            c.Centred("THE CALENDAR", 12, H("#F2F2E6"), 2);
            for (int w = 1; w <= Config.Weeks; w++)
            {
                int x = CalX + (w - 1) % 4 * CalStepX, y = CalY + (w - 1) / 4 * CalStepY;
                bool insp = Run.IsInspection(w);
                var date = S.Dates[w];
                c.Rect(x, y, CalW, CalH, H(insp ? "#B3241C" : date != Special.None ? "#E0A82E" : "#D0C8AE"));
                c.Centred($"{w}{(insp ? "!" : "")}", y + 24, H(insp ? "#F2F2E6" : "#1C1E28"), 2, 3, x + 28);
                if (date != Special.None) c.Centred(date == Special.Payday ? "PAY" : date == Special.Telethon ? "TELE" : "NEPH", y + 56, H("#1C1E28"), 1, 1, x + 28);
                if (w == game.CalendarWeek) c.Frame(x, y, CalW, CalH, H("#FFD23A"), 3); // Pointed at, in the close-up.
                if (w < S.Week) c.Rect(x + 4, y + CalH - 10, CalW - 8, 4, H("#1C1E28"));
                if (w == S.Week) c.Frame(x + 2, y + 2, CalW - 4, CalH - 4, H("#1A2AA8"), 4);
            }
            c.Apply();
        }

        // The wall newspaper keeps the last result's headline.
        void DrawPaper()
        {
            var c = Room.Paper;
            bool won = Result != null && Result.Air.Tier != Tier.Clean;
            if (Result == null && paperDrawn) return;
            paperDrawn = true;
            c.Fill(H("#EDE6CE"));
            c.Rect(0, 0, c.W, 34, H("#1C1E28"));
            c.Centred("THE DAILY TICKET", 13, H("#EDE6CE"));
            c.Centred(won ? "SOMEONE WON" : "NO WINNER", 52, H("#1C1E28"), 2, 3);
            c.Centred(won ? "SHOCK DRAW" : "AGAIN", 86, H("#B3241C"), 2, 3);
            c.Rect(16, 122, 224, 2, H("#1C1E28"));
            for (int i = 0; i < 9; i++) c.Rect(16, 158 + i * 14, i % 4 == 3 ? 120 : 224, 4, H("#7D7766"));
            c.Apply();
        }

        bool paperDrawn;
    }
}
