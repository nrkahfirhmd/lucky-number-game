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
        static readonly string[] OnAirTickers =
        {
            "   TICKET SALES UP AGAIN   *   NUMBER 77 STILL DUE, SAY EXPERTS   *",
            "   RECORD TICKET SALES   *   MATH EXPERTS DECLARE DRAW \"UNWINNABLE\"   *",
            "   FEVER IN THE STREETS   *   STATION PROMISES \"SOMEONE MUST WIN EVENTUALLY\"   *",
            "   CITIZENS DEMAND WINNER   *   LOTTERY COMMITTEE URGES PATIENCE   *",
            "   LUCKY CHARMS SELL OUT   *   EXPERTS RECOMMEND BUYING MORE TICKETS   *"
        };
        const string TickerTitle = "   TONIGHT ON THE BIG DRAW   *   COULD IT BE YOU?   *   NO   *",
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

        public Screens(Game game) { this.game = game; }

        bool monitorDirty = true, tvDirty = true, hudDirty = true;
        int lastClockLeft = int.MinValue;
        bool lastClockOn;
        string lastMachineLabel;

        public void MarkDirty()
        {
            monitorDirty = true;
            tvDirty = true;
            hudDirty = true;
        }

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
            MarkDirty();
            DrawCards();
            DrawRack();
            DrawSlip();
            DrawPlates();
            DrawCalendar();
            DrawPaper();
        }

        void DrawMonitor(float t)
        {
            bool rolling = game.Live && game.Played.Count > 0 && !Still && t - game.LandedAt * 1000 < 300;
            if (!monitorDirty && !rolling) return;
            if (!rolling) monitorDirty = false;

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
            if (game.Shredding && game.Preview >= 0) foreach (int n in Run.Neighbours(game.Preview)) Dashed(g, n, Alarm); // Where its holders go.
            if (game.PreviewRow >= 0) for (int n = game.PreviewRow * 10; n < game.PreviewRow * 10 + 10; n++) Dashed(g, n, Label); // JAM: anywhere in the row.
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
            int shown = game.Played.Count - (rolling ? 1 : 0);
            var cards = game.Played.Take(shown).Select(i => S.Hand[i]).ToList();
            var steps = new[] { S.Roll.Value }.Concat(game.Landed.Take(shown)).ToList();
            int now = steps[steps.Count - 1];
            for (int i = 0; i < steps.Count - 1; i++) Mark(g, steps[i], Dim);
            Mark(g, now, Ink);
            NumberBox(g, rolling ? $"{(int)(t / 60) * 37 % 100:00}" : $"{now:00}", Ink);
            Chain(g, Tokens(cards, steps), 184, Ink, Label);
            int h = S.Board[now], cost = S.CostOf(now, cards.Count);
            if (S.IsFavour(now)) Verdict(g, cost <= 0 ? $"NEPHEW +{-cost} CASH" : $"NEPHEW -{cost} CASH", cost <= 0 ? Good : Label);
            else if (h == 0) Verdict(g, "0 PERSON HOLDS", Good);
            else Verdict(g, $"{h} HOLD -{cost} CASH", Alarm);
        }

        void MonitorResult(Canvas8 g)
        {
            var a = Result.Air;
            Color32 c = a.Tier == Tier.Clean ? Good : a.Tier == Tier.Small ? Label : Alarm;
            DrawBoard(g);
            Mark(g, Result.Number, c);
            NumberBox(g, $"{Result.Number:00}", c);
            Chain(g, Tokens(Result.Path, Result.Steps), 184, c, Label);
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
        static void Dashed(Canvas8 g, int n, Color32? colour = null)
        {
            int x = 16 + n % 10 * Cell, y = 24 + n / 10 * Cell;
            var c = colour ?? Ink;
            for (int i = 0; i < Square + 4; i += 6)
            {
                g.Rect(x - 2 + i, y - 2, 3, 2, c); g.Rect(x - 2 + i, y + Square, 3, 2, c);
                g.Rect(x - 2, y - 2 + i, 2, 3, c); g.Rect(x + Square, y - 2 + i, 2, 3, c);
            }
        }

        static void NumberBox(Canvas8 g, string label, Color32 c)
        {
            g.Chip(352, 24, 272, 144, Panel);
            g.Centred(label, 48, c, 10, 12, 488);
        }

        // Start, then each card and the number it landed on, so nobody does the arithmetic in their head.
        static List<string> Tokens(List<Card> cards, List<int> steps)
        {
            var toks = new List<string> { $"{steps[0]:00}" };
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
            if (text.Length * 8 * sx > 256) { sx = 1; sy = 1; } // Long text shrinks to fit the chip.
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
            // Rows every Game.RowStep px from Game.RowY, BACK below. Game.ClickMenu hit-tests the same layout.
            int Y(int row) => Game.RowY + row * Game.RowStep;
            SetRow(g, Y(0), "SFX VOLUME", game.Sel == 0);
            Slider(g, Y(0), Prefs.Volume);
            SetRow(g, Y(1), "MUSIC VOLUME", game.Sel == 1);
            Slider(g, Y(1), Prefs.Music);
            SetRow(g, Y(2), "REDUCE MOTION", game.Sel == 2);
            Seg(g, 364, Y(2) + 24, 72, new[] { "OFF", "ON" }, Prefs.ReduceMotion ? 1 : 0);
            SetRow(g, Y(3), "TUTORIAL", game.Sel == 3);
            Seg(g, 364, Y(3) + 24, 72, new[] { "OFF", "ON" }, Prefs.Tutorial ? 1 : 0);
            MonButton(g, 352, Game.BackY, 272, "BACK", game.Sel == Game.SettingRows);
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
            if (focused) g.ChipFrame(352, y, 272, Game.RowStep - 4, Label, Panel); else g.Chip(352, y, 272, Game.RowStep - 4, Panel);
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

        void DrawTv(float t)
        {
            var m = game.Mode;
            bool offAir = !game.Live && Result == null && m == Mode.Play;
            if (offAir && !tvDirty) return;
            if (offAir) tvDirty = false;

            var g = Room.Tv;
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
            if ((game.Live || Result != null) && S.Over == Ending.None && game.SpecialName != null)
            {
                g.Rect(80, 8, game.SpecialName.Length * 8 + 16, 16, Label);
                g.Text(game.SpecialName, 88, 12, Bg);
            }
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
            g.Centred("DON'T LET", 72, Label, 4, 5);
            g.Centred("ANYONE WIN", 128, Label, 4, 5);
            if (Still || (int)(t / 500) % 2 == 0) g.Centred("PRESS ANY KEY", 216, Ink, 2, 2);
        }

        // On air, before the result: the stage waits on a mystery ball. Stalling dims the ball and runs the host's filler.
        void Live(Canvas8 g, float t)
        {
            bool stalling = game.Stall >= 0;
            Stage(g, t, S.Today == Special.Telethon ? TickerTelethon : OnAirTickers[(S.Week - 1) % OnAirTickers.Length]);
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
            if (Interview(Result, S.Week) is { } iv && Time.time - Result.At >= InterviewDelay) { InterviewTv(g, t, iv); return; }
            var a = Result.Air;
            string n = $"{Result.Number:00}";
            string ticker = a.Reward > 0
                ? $"   MINISTER'S NEPHEW WINS ON {n}   *   MINISTER \"AS SURPRISED AS ANYONE\"   *"
                : a.Tier switch
                {
                    Tier.Clean => "   NO WINNER AGAIN THIS WEEK   *   STATION SENDS ITS SYMPATHY   *",
                    Tier.Small => $"   {(a.Holders < Winners.Length ? Winners[a.Holders] : $"{a.Holders} WINNERS")} ON {n}   *   STATION \"DELIGHTED FOR THEM\"   *",
                    _ => $"   {n} FINALLY COMES IN   *   BOARD CALLS EMERGENCY MEETING   *"
                };
            Stage(g, t, ticker);
            Ball(g, n, Ink);
            string countLabel = a.Holders switch
            {
                0 => "NOBODY WON!",
                1 => "1 WINNER!",
                _ => $"{a.Holders} WINNERS!"
            };
            g.Centred(countLabel, 196, a.Tier == Tier.Scandal ? Alarm : Label, 2, 2);
            string subtext = a.Reward > 0 ? "WHAT ARE THE ODDS." : a.Tier switch
            {
                Tier.Clean => "WHAT A SHAME!",
                Tier.Small => "LUCKY THEM.",
                _ => "THE STATION IS THRILLED. REALLY."
            };
            g.Centred(subtext, 228, Ink);
        }

        // Street interview, cut to after the result card. Who speaks follows the result: a clean rig asks someone who held
        // the honest number you rigged away, a win asks a winner, a nephew hit asks the nephew. Fixed per week and number.
        public const float InterviewDelay = 2.6f;
        public struct Vox { public string Name, Held, Quote; public int Who; public bool Happy; }
        static readonly (string Name, Color32 Skin, Color32 Hair, Color32 Coat, int Style, bool Glasses, bool Tache)[] People =
        {
            ("MRS ADA, 71", H("#E8B48C"), H("#D8D8E0"), H("#5A2A9E"), 2, true, false),
            ("DES, 54, BUS DRIVER", H("#C98A5E"), H("#3A2414"), H("#1A2AA8"), 3, false, true),
            ("PRIYA, 29", H("#B07850"), H("#1A1018"), H("#C2187A"), 1, false, false),
            ("MR OKAFOR, 66", H("#7A4A30"), H("#C8C8C8"), H("#3A1058"), 0, true, false),
            ("GLENDA, 48", H("#F0C8A0"), H("#E8A040"), H("#FF8A1F"), 1, true, false),
            ("KEV, 37", H("#E0A880"), H("#8A5A30"), H("#7A0F2E"), 3, false, false),
            ("ROLAND, 23, NO RELATION", H("#D9A07A"), H("#8A8478"), H("#4A4E5E"), 1, false, false), // The Minister's nephew, in his uncle's grey.
        };
        const int Public = 6, NephewWho = 6;
        // {0} the interviewee's number, {1} the number that aired.
        static readonly string[] CleanQuotes =
        {
            "I HAD {0}. IT SAID {0} FOR A SECOND. I SWEAR IT DID.",
            "{0} IS MY HOUSE NUMBER. AND MY AGE. AND MY BUS. WHO PICKS {1}?",
            "I SAW {0}. I BLINKED. THEN IT WAS {1}. IS THAT ALLOWED?",
            "FORTY YEARS ON {0}. NEXT WEEK, SURELY.",
            "MY CAT CHOSE {0}. THE CAT IS VERY UPSET.",
            "WHO EVEN HAS {1}? NOBODY. THAT'S THE POINT, ISN'T IT.",
            "I'M BUYING TWO TICKETS NEXT WEEK. BOTH ON {0}. TO BE SAFE.",
            "THE BALL WAS ROLLING STRAIGHT FOR {0}. THEN IT WASN'T.",
        };
        // Five clean weeks or more: the public has noticed.
        static readonly string[] AngryQuotes =
        {
            "NOBODY WINS. EVER. WHO IS RUNNING THIS? I WANT A NAME.",
            "I'VE STARTED A PETITION. IT'S ON {0}. LIKE MY TICKET.",
            "MY WHOLE STREET HAD {0}. THE STREET IS FURIOUS.",
            "SOMEONE IN THAT BUILDING KNOWS SOMETHING. I CAN FEEL IT.",
            "{0} CAME UP. I SAW IT. THEN THE TELLY SAID {1}. EXPLAIN THAT.",
        };
        static readonly string[] WinQuotes =
        {
            "I WON! ON {0}! I'M BUYING A SLIGHTLY BETTER KETTLE.",
            "FORTY YEARS AND IT FINALLY HAPPENED. WHAT DO I DO WITH MY SATURDAYS NOW?",
            "I ONLY BOUGHT IT WITH MY BUS FARE CHANGE.",
            "I'M SPLITTING IT WITH MY SISTER. SHE DOESN'T KNOW YET.",
            "{0}! I TOLD YOU! I TOLD ALL OF YOU!",
        };
        static readonly string[] ScandalQuotes =
        {
            "ALL OF US HAD {0}. THE WHOLE BOWLING CLUB. DRINKS ARE ON THE STATE.",
            "THE STATION SAYS IT'S THRILLED. IT DOESN'T LOOK THRILLED.",
            "I'M QUITTING MY JOB. LIVE ON TELLY. HELLO, GARY.",
            "{0} IS THE PEOPLE'S NUMBER NOW.",
            "WE WON! WE ACTUALLY WON! IS THAT ALLOWED?",
        };
        static readonly string[] NephewQuotes =
        {
            "I JUST HAD A FEELING ABOUT {0}. A FAMILY FEELING.",
            "UNCLE? NO. I DON'T HAVE AN UNCLE. WHO'S ASKING?",
            "I'LL PUT IT TOWARDS SOMETHING SENSIBLE. LIKE A SECOND YACHT.",
            "WHAT ARE THE ODDS. HONESTLY. WHAT ARE THEY.",
        };

        public static Vox? Interview(WeekResult r, int week)
        {
            var a = r?.Air;
            if (a == null) return null;
            bool clean = a.Tier == Tier.Clean && a.Reward == 0;
            int n = clean ? r.From : r.Number, k = week * 7 + n;
            string Say(string[] q) => "\"" + string.Format(q[k % q.Length], $"{n:00}", $"{r.Number:00}") + "\"";
            string On(int h, string what) => h == 1 ? $"THE ONLY {what} ON {n:00}" : $"ONE OF {h} {what}S ON {n:00}";
            Vox Person(int who, string held, string[] q, bool happy) => new Vox { Who = who, Name = People[who].Name, Held = held, Quote = Say(q), Happy = happy };
            if (a.Reward > 0) return Person(NephewWho, "HIS UNCLE IS THE MINISTER", NephewQuotes, true);
            if (clean)
                return r.From == r.Number || r.FromHolders <= 0 ? (Vox?)null
                    : Person(k % Public, On(r.FromHolders, "TICKET"), r.StreakBefore + 1 >= 5 ? AngryQuotes : CleanQuotes, false);
            if (a.Holders <= 0) return null;
            return Person(k % Public, On(a.Holders, "WINNER"), a.Tier == Tier.Small ? WinQuotes : ScandalQuotes, true);
        }

        static void InterviewTv(Canvas8 g, float t, Vox v)
        {
            Stage(g, t, "   WE ASKED THE PUBLIC   *   THE PUBLIC HAD THOUGHTS   *", header: false);
            g.Centred("WE ASKED THE PUBLIC", 52, Label, 2, 2);
            Face(g, 88, 88, People[v.Who], v.Happy);
            var lines = CachedWrap(v.Quote, 44);
            for (int i = 0; i < lines.Count && i < 5; i++) g.Text(lines[i], 216, 96 + i * 24, Ink, 1, 2);
            g.Rect(80, 228, 480, 24, Label);
            g.Text(v.Name, 88, 232, Bg, 1, 2);
            g.Text(v.Held, 88, 258, Dim);
        }

        // Block-art head, 96x128. Losers frown with worried brows, winners grin.
        static void Face(Canvas8 g, int x, int y, (string Name, Color32 Skin, Color32 Hair, Color32 Coat, int Style, bool Glasses, bool Tache) p, bool happy)
        {
            g.Rect(x - 8, y, 112, 128, Panel);
            g.Rect(x + 8, y + 96, 80, 32, p.Coat);
            g.Rect(x + 38, y + 82, 20, 14, p.Skin);
            g.Rect(x + 24, y + 28, 48, 56, p.Skin);
            g.Rect(x + 18, y + 48, 6, 12, p.Skin);
            g.Rect(x + 72, y + 48, 6, 12, p.Skin);
            switch (p.Style)
            {
                case 0: g.Rect(x + 20, y + 32, 6, 16, p.Hair); g.Rect(x + 70, y + 32, 6, 16, p.Hair); break; // Bald, side tufts.
                case 1: g.Rect(x + 22, y + 20, 52, 14, p.Hair); g.Rect(x + 20, y + 30, 6, 30, p.Hair); g.Rect(x + 70, y + 30, 6, 30, p.Hair); break;
                case 2: g.Rect(x + 22, y + 20, 52, 12, p.Hair); g.Rect(x + 38, y + 8, 20, 14, p.Hair); break; // Bun.
                default: g.Rect(x + 26, y + 8, 44, 18, p.Coat); g.Rect(x + 16, y + 24, 64, 6, p.Coat); break; // Flat cap.
            }
            g.Rect(x + 34, y + 50, 6, 6, Bg);
            g.Rect(x + 56, y + 50, 6, 6, Bg);
            g.Rect(x + 32, y + 44, 10, 2, p.Hair);
            g.Rect(x + 54, y + 44, 10, 2, p.Hair);
            if (!happy) { g.Rect(x + 40, y + 42, 2, 2, p.Hair); g.Rect(x + 54, y + 42, 2, 2, p.Hair); }
            if (p.Glasses) { g.Frame(x + 30, y + 46, 14, 14, Bg); g.Frame(x + 52, y + 46, 14, 14, Bg); g.Rect(x + 44, y + 52, 8, 2, Bg); }
            if (p.Tache) g.Rect(x + 36, y + 64, 24, 6, p.Hair);
            g.Rect(x + 40, y + 72, 16, 4, Curtain2);
            g.Rect(x + 36, y + (happy ? 68 : 74), 4, 4, Curtain2);
            g.Rect(x + 56, y + (happy ? 68 : 74), 4, 4, Curtain2);
        }

        static void SeasonOver(Canvas8 g, float t)
        {
            Stage(g, t, TickerWon);
            g.Centred("STATION UNDER INVESTIGATION.", 128, Ink, 2, 3);
            g.Centred("AWAIT FURTHER NOTICE.", 168, Label, 2, 3);
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

        public Canvas8 Hud { get; private set; } = NewHud(0, 0);
        static Canvas8 NewHud(int px, int py) => new Canvas8(640 + 2 * px, 360 + 2 * py) { Ox = px, Oy = py, Tex = { filterMode = FilterMode.Point } };

        // The canvas covers the whole window, the same amount past each side of the centred 640x360 layout.
        void FitHud()
        {
            var v = Game.HudView;
            int px = Mathf.Max(-v.x, v.xMax - 640) + 1, py = Mathf.Max(-v.y, v.yMax - 360) + 1; // +1: the window may end mid HUD pixel.
            if (px == Hud.Ox && py == Hud.Oy) return;
            Object.Destroy(Hud.Tex);
            Hud = NewHud(px, py);
            hudDirty = true;
        }
        static readonly Color32 HGreen = H("#5BE37A"), HOrange = H("#FF8A3A");
        static readonly Color32 HInk = H("#F2F2E6"), HDim = H("#8A8A90"), HGold = H("#FFD23A"), HRed = H("#FF3B2F"), HPlate = H("#111111"), HLine = H("#4A4A50"),
            Clear = new Color32(0, 0, 0, 0);

        string lastHudHover;
        int lastHudCash = -1, lastHudWeek = -1, lastHudStreak = -1, lastHudPrize = -1;
        Mode lastHudMode = (Mode)(-1);
        bool lastHudEndCard, lastHudReplacing, lastHudBanner;
        int lastHudConfirm = -1;
        bool lastHudShred;
        string lastHudCoach, lastHudStation;
        bool lastHudLean, lastHudIntro;
        public RectInt NextBtn; // The coach's NEXT/SKIP chip, in HUD pixels. Empty when none is up.
        readonly List<RectInt> coachRects = new List<RectInt>();
        int lastCoachSig;

        public void DrawHud()
        {
            FitHud();
            bool bannerActive = game.Banner != null && (Time.time - game.BannerAt <= 2.2f);
            bool coachActive = game.Tutorial != null;
            bool stateChanged = hudDirty || game.HoverText != lastHudHover || (S != null && (S.Cash != lastHudCash || S.Week != lastHudWeek
                || S.Streak != lastHudStreak || S.Prize != lastHudPrize)) || game.Mode != lastHudMode
                || game.EndCardUp != lastHudEndCard || game.Replacing != lastHudReplacing
                || bannerActive != lastHudBanner || game.ConfirmCard != lastHudConfirm || game.Shredding != lastHudShred || game.Tutorial != lastHudCoach
                || game.Station != lastHudStation || game.Leaning != lastHudLean || game.IntroUp != lastHudIntro; // The frame a banner or coach ends must repaint, or it stays on screen.

            if (!bannerActive && !coachActive && !game.IntroUp && !stateChanged) return;
            if (coachActive && !bannerActive && !game.IntroUp)
            {
                // The coach only changes when its targets move on screen or the brackets blink: skip identical frames.
                CollectCoachRects();
                int sig = (Still || (int)(Time.unscaledTime * 3) % 2 == 0 ? 1 : 0) + (game.CanSkip ? 2 : 0);
                foreach (var r in coachRects) sig = System.HashCode.Combine(sig, r.x, r.y, r.width, r.height);
                if (!stateChanged && sig == lastCoachSig) return;
                lastCoachSig = sig;
            }

            hudDirty = false;
            lastHudHover = game.HoverText;
            if (S != null) { lastHudCash = S.Cash; lastHudWeek = S.Week; lastHudStreak = S.Streak; lastHudPrize = S.Prize; }
            lastHudMode = game.Mode;
            lastHudEndCard = game.EndCardUp;
            lastHudReplacing = game.Replacing;
            lastHudBanner = bannerActive;
            lastHudConfirm = game.ConfirmCard;
            lastHudShred = game.Shredding;
            lastHudCoach = game.Tutorial;
            lastHudStation = game.Station;
            lastHudLean = game.Leaning;
            lastHudIntro = game.IntroUp;

            var g = Hud;
            g.Fill(Clear);
            if (game.IntroUp) Intro(g);
            else if (game.Mode == Mode.Paused) PauseScreen(g);
            else if (game.Mode == Mode.Play)
            {
                bool coach = !game.EndCardUp && game.Tutorial != null;
                if (coach) CoachDim(g);
                Bar(g);
                if (coach) Coach(g);
                else NextBtn = default;
                if (!game.EndCardUp) Nav(g, coach);
                if (Result == null) Banner(g);
                if (Result != null && S.Over != Ending.None && game.EndCardUp) EndCard(g);
                if (game.Replacing) ReplacePrompt(g);
                if (game.Replacing || game.Shredding) EscHint(g);
            }
            g.Apply();
        }

        // Black screen, the premise one paragraph at a time. The newest paragraph fades in.
        void Intro(Canvas8 g)
        {
            g.Fill(new Color32(0, 0, 0, 255));
            var paras = Game.IntroText.Select(t => CachedWrap(t, 34)).ToList();
            int total = paras.Sum(p => p.Count * 24) + (paras.Count - 1) * 16, y = (360 - total) / 2 - 8;
            int shown = game.IntroShown;
            for (int i = 0; i < paras.Count; i++)
            {
                if (i < shown)
                {
                    var ink = i == 0 ? HGold : i == paras.Count - 1 ? HRed : HInk;
                    float k = i == shown - 1 ? Mathf.Clamp01(game.IntroLineAge / .8f) : 1;
                    var c = Color32.Lerp(new Color32(0, 0, 0, 255), ink, k);
                    for (int l = 0; l < paras[i].Count; l++) g.Centred(paras[i][l], y + l * 24, c, 2, 2);
                }
                y += paras[i].Count * 24 + 16;
            }
            if (shown >= paras.Count && (Still || (int)(Time.unscaledTime * 2) % 2 == 0)) g.Centred("CLICK TO CONTINUE", 332, HDim);
        }

        // The printer's boost, always on screen: waiting on the reader, or taken and still to be met.
        void SlipChip(Canvas8 g, int y)
        {
            bool taken = S.Pinned != Slip.None;
            var slip = taken ? S.Pinned : S.Offer;
            if (slip == Slip.None) return;
            string text = (taken ? $"SLIP: {Slips.Name(slip)}, {Slips.Wants(slip)}" : $"NEW SLIP AT READER: {Slips.Name(slip)}").ToUpper();
            if (text.Length > 46) text = text.Substring(0, 46); // Stays clear of the tutorial panel.
            int x = Game.HudView.x;
            g.Chip(x + 16, y, text.Length * 8 + 16, 24, taken ? HInk : HGold);
            g.Text(text, x + 24, y + 4, HPlate, 1, 2);
        }

        void ReplacePrompt(Canvas8 g)
        {
            g.ChipFrame(160, 32, 320, 28, HGold, HPlate);
            g.Centred($"NEW CARD: {game.IncomingCard}", 38, HGold, 1, 2);
            int pick = game.ConfirmCard;
            if (pick >= 0 && pick < S.Hand.Count)
            {
                g.ChipFrame(120, 64, 400, 28, HRed, HPlate);
                g.Centred($"REPLACE {S.Hand[pick]} WITH {game.IncomingCard}? CLICK AGAIN", 70, HRed, 1, 2);
            }
            else Outlined(g, "CLICK THE CARD TO REPLACE", 188, HInk);
        }

        // Bottom right, clear of the footnote.
        void EscHint(Canvas8 g)
        {
            var v = Game.HudView;
            g.Chip(v.xMax - 120, v.yMax - 28, 104, 20, HLine);
            g.Text("ESC: CANCEL", v.xMax - 112, v.yMax - 22, HInk);
        }

        void Bar(Canvas8 g)
        {
            bool inspection = Run.IsInspection(S.Week);
            var v = Game.HudView;
            g.Rect(v.x, v.y, v.width, 24, HPlate);
            g.Rect(v.x, v.y + 24, v.width, 2, HLine);
            var slots = new[]
            {
                ("WEEK", $"{S.Week:00}/{Config.Weeks}", inspection ? HRed : HInk), ("CASH", S.Cash.ToString(), S.Cash <= 0 ? HRed : HGreen),
                ("STREAK", $"{S.Streak:00}", HInk), ("PRIZE", S.Prize.ToString(), HOrange),
            };
            for (int i = 0; i < slots.Length; i++)
            {
                var (label, value, c) = slots[i];
                int x = v.x + 16 + i * (v.width / 4);
                g.Text(label, x, v.y + 8, HDim);
                g.Text(value, x + label.Length * 8 + 8, v.y + 4, c, 1, 2);
            }
            if (Result != null) return;
            SlipChip(g, v.y + (inspection || S.Today != Special.None ? 60 : 32)); // Under the date chip, or on top when there is none.
            if (inspection)
            {
                string badge = $"INSPECTION: {Config.InspectionCards} CARD ONLY";
                g.Chip(v.x + 16, v.y + 32, badge.Length * 8 + 16, 24, HRed);
                g.Text(badge, v.x + 24, v.y + 36, HPlate, 1, 2);
                return;
            }
            string date = S.Today switch
            {
                Special.Payday => $"PAYDAY: {Config.PaydayTickets}X TICKETS",
                Special.Telethon => $"TELETHON: PRIZE X{Config.TelethonPrize}",
                Special.Nephew => $"NEPHEW: {string.Join(" ", S.Nephews.Select(n => n.ToString("00")))}",
                _ => null
            };
            if (date == null) return;
            g.Chip(v.x + 16, v.y + 32, date.Length * 8 + 16, 24, HGold);
            g.Text(date, v.x + 24, v.y + 36, HPlate, 1, 2);
        }

        // The headline: a band that wipes across mid-screen for a couple of seconds. Red for ON AIR.
        void Banner(Canvas8 g)
        {
            float age = Time.time - game.BannerAt;
            if (game.Banner == null || age > 2.2f) return;
            float wipe = Still ? 1 : Mathf.Clamp01(age / .18f), close = Still ? 1 : Mathf.Clamp01((2.2f - age) / .25f);
            var v = Game.HudView;
            int h = Mathf.RoundToInt(40 * close / 2) * 2, w = Mathf.RoundToInt(v.width * wipe);
            if (h < 2) return;
            g.Rect(v.x + (v.width - w) / 2, 150 - h / 2 - 2, w, h + 4, HPlate);
            g.Rect(v.x + (v.width - w) / 2, 150 - h / 2, w, h, game.Live ? HRed : HGold);
            if (wipe >= 1 && close >= 1) g.Centred(game.Banner, 138, HPlate, 2, 3);
        }

        // Mouse-only controls for the keys A and D. Only the moves allowed right now are drawn; gold marks the one the coach asks for.
        void Nav(Canvas8 g, bool coach)
        {
            var gold = Still || (int)(Time.unscaledTime * 3) % 2 == 0;
            for (int n = -1; n <= 1; n += 2)
            {
                if (!game.NavOn(n)) continue;
                var o = Game.NavAt(n);
                bool hint = coach && CoachWantsLook(n);
                g.ChipFrame(o.x, o.y, Game.NavW, Game.NavH, hint && gold ? HGold : HLine, HPlate);
                string label = n < 0 ? "<" : ">";
                g.Centred(label, o.y + 8, hint ? HGold : HInk, 1, 1, o.x + Game.NavW / 2);
            }
        }

        // The coach points off screen: a look button is the way there.
        bool CoachWantsLook(int dir)
        {
            foreach (var col in game.TutorialTargets())
                if (!OnScreen(col.bounds, out _)) return game.Cam.transform.InverseTransformPoint(col.bounds.center).x > 0 == (dir > 0);
            return false;
        }

        // The HUD rectangle of a world box, and whether any of it is in view.
        bool OnScreen(Bounds b, out RectInt r)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            bool behind = false;
            for (int i = 0; i < 8; i++)
            {
                var p = game.Cam.WorldToScreenPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                behind |= p.z < 0;
                var h = Game.ScreenToHud(p);
                x0 = Mathf.Min(x0, h.x); y0 = Mathf.Min(y0, h.y); x1 = Mathf.Max(x1, h.x); y1 = Mathf.Max(y1, h.y);
            }
            var v = Game.HudView;
            int top = Mathf.Max(y0 - 4, v.y + 30); // Below the bar, so brackets never sit on its numbers.
            r = new RectInt(x0 - 4, top, x1 - x0 + 8, Mathf.Max(0, y1 + 4 - top));
            return !(behind || x1 < v.x || x0 >= v.xMax || y1 < v.y || y0 >= v.yMax);
        }

        // Spotlight: everything is dimmed except what the step is about, so there is one place to look.
        void CollectCoachRects()
        {
            coachRects.Clear();
            foreach (var col in game.TutorialTargets()) if (OnScreen(col.bounds, out var r)) coachRects.Add(r);
        }

        void CoachDim(Canvas8 g)
        {
            CollectCoachRects();
            if (coachRects.Count == 0 && !game.TutorialBar) return;
            g.Fill(new Color32(0, 0, 0, 150));
            foreach (var r in coachRects) g.Rect(r.x, r.y, r.width, r.height, Clear);
        }

        // The panel goes to the corner that covers the least of what the step points at (and the look buttons). Top right wins ties.
        int coachSpot = -1;
        Vector2Int CoachSpot(int ph)
        {
            var v = Game.HudView;
            var spots = new[] { new Vector2Int(v.xMax - 240, v.y + 32), new Vector2Int(v.x + 8, v.y + 32), new Vector2Int(v.xMax - 240, v.yMax - 8 - ph), new Vector2Int(v.x + 8, v.yMax - 8 - ph) };
            int Covered(Vector2Int p)
            {
                int sum = 0;
                void Add(RectInt r, int weight)
                {
                    int w = Mathf.Min(p.x + 232, r.xMax) - Mathf.Max(p.x, r.x), h = Mathf.Min(p.y + ph, r.yMax) - Mathf.Max(p.y, r.y);
                    if (w > 0 && h > 0) sum += w * h * weight;
                }
                foreach (var r in coachRects) Add(r, 1);
                foreach (int n in new[] { -1, 1 }) if (game.NavOn(n)) { var o = Game.NavAt(n); Add(new RectInt(o.x, o.y, Game.NavW, Game.NavH), 100); }
                return sum;
            }
            int best = 0;
            for (int i = 1; i < spots.Length; i++) if (Covered(spots[i]) < Covered(spots[best])) best = i;
            // Hysteresis: the camera sways with the pointer, so only move for a clear win.
            if (coachSpot < 0 || Covered(spots[best]) < Covered(spots[coachSpot]) * 3 / 4) coachSpot = best;
            return spots[coachSpot];
        }

        // The tutorial: a panel in the emptiest corner, and blinking brackets on what it's about.
        void Coach(Canvas8 g)
        {
            var c = Still || (int)(Time.unscaledTime * 3) % 2 == 0 ? HGold : HInk; // Swaps colour 1.5 times a second, never flashes off.
            foreach (var r in coachRects) Brackets(g, r.x, r.y, r.xMax, r.yMax, c);
            if (game.TutorialBar) { var v = Game.HudView; Brackets(g, v.x + 2, v.y + 2, v.xMax - 3, v.y + 25, c); }

            var lines = CachedWrap(game.Tutorial, 27);
            int ph = 32 + lines.Count * 12 + 40;
            var at = CoachSpot(ph);
            int left = at.x, top = at.y;
            g.ChipFrame(left, top, 232, ph, HGold, HPlate);
            g.Text($"TUTORIAL {game.TutorialStep + 1}/{Game.TutorialSteps}", left + 12, top + 10, HGold);
            for (int i = 0; i < lines.Count; i++) g.Text(lines[i], left + 12, top + 28 + i * 12, HInk);
            NextBtn = default;
            if (!game.CanSkip) return;
            // Mouse users advance with the chip, keyboard users with TAB.
            string label = game.TutorialInfo ? "NEXT (TAB)" : "SKIP (TAB)";
            var b = new RectInt(left + 220 - Game.NextW, top + ph - 28, Game.NextW, 20);
            g.Chip(b.x, b.y, b.width, b.height, game.TutorialInfo ? HGold : HLine);
            g.Centred(label, b.y + 6, game.TutorialInfo ? HPlate : HInk, 1, 1, b.x + b.width / 2);
            NextBtn = b;
        }

        static void Outlined(Canvas8 g, string text, int y, Color32 c)
        {
            foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1) })
                g.Centred(text, y + dy, HPlate, 1, 1, 320 + dx);
            g.Centred(text, y, c);
        }

        // Corner brackets with a dark rim, so they read on any background.
        static void Brackets(Canvas8 g, int x0, int y0, int x1, int y1, Color32 c)
        {
            const int L = 10;
            for (int pass = 0; pass < 2; pass++)
            {
                int t = pass == 0 ? 4 : 2, o = pass == 0 ? -1 : 0;
                var k = pass == 0 ? HPlate : c;
                foreach (var (x, y, dx, dy) in new[] { (x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1) })
                {
                    int ax = x + o * dx, ay = y + o * dy;
                    g.Rect(dx > 0 ? ax : ax - L + 1, dy > 0 ? ay : ay - t + 1, L, t, k);
                    g.Rect(dx > 0 ? ax : ax - t + 1, dy > 0 ? ay : ay - L + 1, t, L, k);
                }
            }
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

        void DrawClock()
        {
            bool on = game.Live && Result == null;
            int left = Mathf.CeilToInt(Config.Target - game.LiveSeconds);
            if (on == lastClockOn && left == lastClockLeft) return;
            lastClockOn = on;
            lastClockLeft = left;
            var c = Room.Clock;
            c.Fill(H("#080808"));
            c.Centred(on ? "ON AIR" : "OFF AIR", 6, H(on ? "#ff3b2f" : "#8a8a90"));
            c.Centred(!on ? "--" : left > 0 ? $"{left:00}" : "LATE", 24, H(on && left <= 5 ? "#ff3b2f" : "#ffd23a"), 4 - (on && left <= 0 ? 1 : 0));
            c.Apply();
        }

        void DrawMachineFace()
        {
            int? shown = Result != null ? Result.From : S.Roll;
            bool rolling = Time.time - game.RolledAt < Game.LockSeconds;
            string label = rolling ? $"{Random.Range(0, 100):00}" : shown == null ? "--" : $"{shown:00}";
            if (!rolling && label == lastMachineLabel) return;
            lastMachineLabel = label;
            var c = Room.MachineFace;
            c.Fill(H("#080808"));
            c.Centred(label, 16, H("#ffd23a"), 5);
            c.Apply();
        }

        void DrawCards()
        {
            int n = S.Hand.Count;
            for (int i = 0; i < Room.CardCount; i++)
            {
                var card = Room.Cards[i];
                if (game.Replacing && i == Room.CardCount - 1)
                {
                    card.gameObject.SetActive(true);
                    PunchCard(Room.CardFaces[i], game.IncomingCard, true, 6);
                    continue;
                }
                bool show = i < n && !game.Played.Contains(i);
                card.gameObject.SetActive(show);
                if (!show) continue;
                PunchCard(Room.CardFaces[i], S.Hand[i], game.CanPlay(i) || game.Supplying || game.Dealing || (game.Punching >= 0 && !S.Hand[i].IsSpecial));
            }
        }

        // The rack: specials wait here until re-punched. The picked one is framed.
        void DrawRack()
        {
            for (int i = 0; i < Room.Rack.Length; i++)
            {
                bool show = i < S.Rack.Count;
                Room.Rack[i].gameObject.SetActive(show);
                if (!show) continue;
                PunchCard(Room.RackFaces[i], S.Rack[i], !game.Live && Result == null, game.Punching == i ? 8 : 0);
            }
        }

        // The printer's slip: on the desk while it's offered, on the monitor's edge once taken.
        void DrawSlip()
        {
            var slip = S.Pinned != Slip.None ? S.Pinned : S.Offer;
            Room.Slip.gameObject.SetActive(slip != Slip.None && Result == null);
            if (slip == Slip.None) return;
            bool pinned = S.Pinned != Slip.None;
            Room.Slip.localScale = new Vector3(.2f, Room.SlipH, 1) * (pinned ? Room.SlipPinnedScale : 1);
            if (pinned) Room.Slip.SetPositionAndRotation(Room.SlipPinned, Room.SlipPinnedTurn);
            else Room.Slip.rotation = Room.SlipRestTurn; // Game.Update rises it out of the reader.
            Color32 ink = H("#1C1E28"), red = H("#B3241C"), dim = H("#8A7F60");
            var c = Room.SlipPaper;
            if (c.Unchanged(System.HashCode.Combine(slip, pinned))) return;
            c.Fill(H("#F4EFD8"));
            for (int y = 0; y < c.H; y += 12) { c.Rect(0, y, 3, 6, H("#C9C0A0")); c.Rect(c.W - 3, y, 3, 6, H("#C9C0A0")); } // Tractor-feed holes.
            string name = Slips.Name(slip);
            c.Centred(name, 8, red, name.Length > 9 ? 1 : 2, 2);
            c.Rect(8, 30, c.W - 16, 1, dim);
            int at = 36;
            foreach (var (label, text) in new[] { ("WANTS", Slips.Wants(slip)), ("PAYS", Slips.Pays(slip)), ("MISS", Slips.Fails(slip)) })
            {
                c.Text(label, 8, at, dim); at += 11;
                foreach (var line in Wrap(text, 18)) { c.Text(line, 8, at, ink); at += 11; }
                at += 2;
            }
            if (pinned) { c.Frame(36, 156, 88, 26, red, 2); c.Centred("TAKEN", 165, red); }
            else
            {
                c.Chip(8, 154, 68, 30, H("#2E7D4F")); c.Centred("TAKE", 161, H("#F4EFD8"), 2, 2, 42);
                c.Chip(84, 154, 68, 30, red); c.Centred("TEAR", 161, H("#F4EFD8"), 2, 2, 118);
            }
            c.Apply();
        }

        static readonly Dictionary<string, List<string>> WrapCache = new Dictionary<string, List<string>>();
        public static List<string> CachedWrap(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            string key = width + ":" + text;
            if (!WrapCache.TryGetValue(key, out var lines))
            {
                lines = Wrap(text, width).ToList();
                WrapCache[key] = lines;
            }
            return lines;
        }

        static IEnumerable<string> Wrap(string text, int width)
        {
            string line = "";
            foreach (var word in text.Split(' '))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width) { yield return line; line = ""; }
                line = line.Length == 0 ? word : line + " " + word;
            }
            if (line.Length > 0) yield return line;
        }

        // Punch-card fiction, decided in GDD section 11. Specials are on blue stock, so they read at a glance.
        // frame: a gold border this many pixels thick (the incoming or picked card), or 0.
        static void PunchCard(Canvas8 c, Card card, bool ok, int frame = 0)
        {
            if (c.Unchanged(System.HashCode.Combine(card.Op, card.X, ok, frame))) return;
            bool sp = card.IsSpecial;
            Color32 ink = H(ok ? "#2a2010" : "#4a4232"), hole = H(ok ? "#5a4420" : "#3a3424");
            c.Fill(H(sp ? (ok ? "#9CC3E0" : "#55687a") : ok ? "#D9C79A" : "#7a7058"));
            c.Rect(0, 0, 16, 16, H(sp ? "#1A2AA8" : "#6b4a2e"));
            c.Centred(sp ? "SPECIAL" : "CORRECT", 12, ink);
            string label = sp ? card.Name : card.ToString();
            if (sp) { c.Centred(label, label.Length > 4 ? 44 : 36, ink, label.Length > 4 ? 2 : 3, 3); if (card.Arg.Length > 0) c.Centred(card.Arg, 70, ink, 3); }
            else c.Centred(label, 52, ink, 4);
            int seed = label[0] + label[1];
            for (int x = 0; x < 7; x++)
                for (int y = 0; y < 3; y++)
                    if ((seed + x * 3 + y * 5) % 3 != 0) c.Rect(11 + x * 16, 108 + y * 20, 8, 12, hole);
            if (frame > 0) c.Frame(0, 0, 128, 176, frame == 8 ? H("#FFD23A") : HGold, frame);
            c.Apply();
        }

        static void Plate(Canvas8 c, string label, Color32 ink, int scale = 2)
        {
            if (c.Unchanged(System.HashCode.Combine(label, ink.r, ink.g, ink.b, scale))) return;
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
            Plate(Room.UndoPlate, "UNDO", on);
        }

        // Calendar tiles on the 256x320 paper. Game.CalendarAt hit-tests the same layout.
        public const int CalX = 8, CalY = 48, CalW = 57, CalH = 82, CalStepX = 61, CalStepY = 88;
        public static int CalendarWeekAt(int px, int py)
        {
            int col = (px - CalX) / CalStepX, row = (py - CalY) / CalStepY;
            if (px < CalX || py < CalY || col > 3 || row > 2 || (px - CalX) % CalStepX >= CalW || (py - CalY) % CalStepY >= CalH) return 0;
            return row * 4 + col + 1;
        }

        // A red map pin stuck in the top of the tile.
        static void Pin(Canvas8 c, int cx, int cy)
        {
            c.Rect(cx - 1, cy + 4, 3, 8, H("#3A3A40"));
            for (int dy = -6; dy <= 6; dy++)
                for (int dx = -6; dx <= 6; dx++)
                    if (dx * dx + dy * dy <= 36) c.Rect(cx + dx, cy + dy, 1, 1, dx * dx + dy * dy > 25 ? H("#5A0F0A") : H("#D8281C"));
            c.Rect(cx - 3, cy - 3, 2, 2, H("#FF9A8C"));
        }

        void DrawCalendar()
        {
            var c = Room.Calendar;
            if (c.Unchanged(System.HashCode.Combine(S, S.Week, game.CalendarWeek))) return; // S: a new run reshuffles the dates.
            c.Fill(H("#E8E2CC"));
            c.Rect(0, 0, c.W, 40, H("#B3241C"));
            c.Centred("THE CALENDAR", 12, H("#F2F2E6"), 2);
            for (int w = 1; w <= Config.Weeks; w++)
            {
                int x = CalX + (w - 1) % 4 * CalStepX, y = CalY + (w - 1) / 4 * CalStepY;
                bool insp = Run.IsInspection(w);
                var date = S.Dates[w];
                c.Rect(x, y, CalW, CalH, H(insp ? "#B3241C" : date == Special.Payday ? "#3FA05A" : date == Special.Telethon ? "#A8489E" : date == Special.Nephew ? "#2E8FB8" : "#D0C8AE"));
                if (date != Special.None) c.Frame(x, y, CalW, CalH, H("#1C1E28"), 2);
                c.Centred($"{w}{(insp ? "!" : "")}", y + 24, H(insp ? "#F2F2E6" : "#1C1E28"), 2, 3, x + 28);
                if (date != Special.None) c.Centred(date == Special.Payday ? "PAY" : date == Special.Telethon ? "TELE" : "NEPH", y + 56, H("#F2F2E6"), 1, 1, x + 28);
                if (w == game.CalendarWeek) c.Frame(x, y, CalW, CalH, H("#FFD23A"), 3); // Pointed at, in the close-up.
                if (w < S.Week) for (int j = 0; j < CalH; j++) c.Rect(x + (CalH - 1 - j) * (CalW - 3) / CalH, y + j, 3, 1, H("#1C1E28")); // Struck through with a "/".
                if (w == S.Week) Pin(c, x + CalW / 2, y + 8);
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
