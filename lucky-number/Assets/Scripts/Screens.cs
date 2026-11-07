using System.Linq;
using UnityEngine;

namespace NoWinners
{
    // Draws the monitor, TV and desk labels from game state. Marquee palette on screens, Teletext on the desk.
    public class Screens
    {
        public const int Square = 30, Cell = 32;
        static Color32 H(string hex) => Room.Hex(hex);
        static readonly Color32 Bg = H("#23093A"), Panel = H("#3A1058"), Line = H("#7A3AA0"), Ink = H("#FFF3D6"), Dim = H("#BE9ADB"), Label = H("#FFC93C"),
            Good = H("#5CF2C8"), Money = H("#FFC93C"), Alarm = H("#FF6D90"), Me = H("#FFFFFF"), Socket = H("#120420");
        static readonly Color32[][] Tiers =
        {
            new[] { H("#5A2A9E"), H("#FFF3D6"), H("#CFB4F5") }, new[] { H("#C2187A"), H("#FFF3D6"), H("#FFC4E4") },
            new[] { H("#FF8A1F"), H("#23093A"), H("#5C1F00") }, new[] { H("#F5D23C"), H("#23093A"), H("#5A4800") },
        };
        static readonly Color32[] Bars = { H("#FFC93C"), H("#FF3D6E"), H("#5CF2C8"), H("#C2187A"), H("#7A3AA0"), H("#FF8A1F"), H("#FFF3D6") };
        static readonly string[] Ticker = { "   TICKET SALES UP AGAIN   *   MINISTER \"DELIGHTED\"   *   NUMBER 77 STILL DUE, SAY EXPERTS   *" };

        public static string Holders(int n) => n == 1 ? "1 holder" : $"{n} holders";
        static int TierOf(int h) => h <= 2 ? 0 : h <= 5 ? 1 : h <= 10 ? 2 : 3;

        readonly Game game;
        Room Room => game.Room;
        Run S => game.S;
        WeekResult Result => game.Result;

        public Screens(Game game) { this.game = game; }

        // Moving things, at the art's stepped frame rate.
        public void DrawTimed()
        {
            float t = Time.time * 1000;
            DrawMonitor(t);
            DrawTv(t);
            DrawClock();
            DrawMachineFace();
        }

        // Things that only change when the game does.
        public void DrawDesk()
        {
            DrawCards();
            DrawPlates();
            DrawCalendar();
        }

        void DrawMonitor(float t)
        {
            var g = Room.Monitor;
            int? number = Result != null ? Result.Number : game.Drawn ? game.Value : (int?)null;
            g.Fill(Bg);

            g.Text("THE BIG DRAW!", 16, 8, Label);
            g.Text($"WK {S.Week:00}/{Config.Weeks}", 184, 8, Ink);
            g.Text("CASH", 288, 8, Dim); g.Text(S.Cash.ToString(), 328, 8, Money);
            g.Text("STREAK", 392, 8, Dim); g.Text($"{S.Streak:00}", 448, 8, Good);
            g.Text("PRIZE", 480, 8, Dim); g.Text(S.Prize.ToString(), 528, 8, Money);
            if (Run.IsInspection(S.Week)) g.Text("INSPECT", 568, 8, Alarm);
            g.Rect(16, 19, 608, 2, Label);

            for (int n = 0; n < 100; n++)
            {
                int x = 16 + n % 10 * Cell, y = 24 + n / 10 * Cell, h = S.Board[n];
                if (h > 0)
                {
                    var tier = Tiers[TierOf(h)];
                    g.Chip(x, y, Square, Square, tier[0]);
                    g.Text($"{n:00}", x + 7, y + 5, tier[1]);
                    string count = Mathf.Min(h, 99).ToString();
                    g.Text(count, x + Square - 5 - count.Length * 8, y + 18, tier[2]);
                }
                else
                {
                    // Free squares differ in shape as well as colour: a socket, with no count.
                    g.Chip(x, y, Square, Square, Socket);
                    g.Chip(x + 2, y + 2, Square - 4, Square - 4, Bg);
                    g.Chip(x + 3, y + 3, Square - 6, Square - 6, Socket);
                    g.Text($"{n:00}", x + 7, y + 11, Good);
                }
            }

            bool reveal = Result != null && Result.Air.Holders > 0;
            int dash = reveal ? Result.Best.To : game.Preview;
            if (dash >= 0) Dashed(g, 16 + dash % 10 * Cell, 24 + dash / 10 * Cell);
            if (number != null && (Result != null || t % 1000 < 650)) g.Frame(14 + number.Value % 10 * Cell, 22 + number.Value / 10 * Cell, Square + 4, Square + 4, Me, 3);

            DrawPanel(g, t, number, reveal);
            g.Apply();
        }

        static void Dashed(Canvas8 g, int x, int y)
        {
            for (int i = 0; i < Square + 4; i += 6)
            {
                g.Rect(x - 2 + i, y - 2, 3, 2, Me); g.Rect(x - 2 + i, y + Square, 3, 2, Me);
                g.Rect(x - 2, y - 2 + i, 2, 3, Me); g.Rect(x + Square, y - 2 + i, 2, 3, Me);
            }
        }

        static void Bulbs(Canvas8 g, int x, int y, int w, int h, float t)
        {
            int phase = (int)(t / 180) % 3, i = 0;
            void Dot(int a, int b) => g.Rect(a, b, 4, 4, (i++ + phase) % 3 == 0 ? Label : Line);
            for (int a = x + 2; a < x + w - 4; a += 12) { Dot(a, y + 2); Dot(a, y + h - 6); }
            for (int b = y + 14; b < y + h - 8; b += 12) { Dot(x + 2, b); Dot(x + w - 6, b); }
        }

        static void Drums(Canvas8 g, string s, int x, int y)
        {
            for (int i = 0; i < s.Length; i++)
            {
                int dx = x + i * 92;
                g.Chip(dx, y, 84, 88, Ink);
                g.Text(s[i].ToString(), dx + 10, y + 12, Bg, 8);
                g.Rect(dx, y + 43, 84, 2, Bg);
            }
        }

        void DrawPanel(Canvas8 g, float t, int? number, bool reveal)
        {
            const int px = 344, pw = 280, lx = px + 14;
            g.Rect(px, 24, pw, 320, Panel);
            Bulbs(g, px, 24, pw, 320, t);
            g.Centred("BEHIND THE CURTAIN", 32, Label, 1, 1, px + pw / 2);

            string title = Result != null ? "ROLLED" : "THE BALLS SAY";
            g.Text(title, lx, 46, Label);
            if (Result == null && game.Drawn) g.Text($"{S.Roll:00}", lx + (title.Length + 1) * 8, 46, Ink);
            if (number == null) g.Text("--", lx, 66, Dim, 10);
            else Drums(g, $"{number:00}", lx, 66);

            if (Result == null)
            {
                int left = game.Live ? Mathf.CeilToInt(Config.Target - game.LiveSeconds) : 0;
                g.Rect(px + pw - 88, 40, 76, 20, game.Live ? Alarm : Line);
                g.Text(!game.Live ? "OFF AIR" : left > 0 ? $"AIR {left:00}" : "STALLING", px + pw - 84, 44, game.Live ? Bg : Ink, 1, 2);
            }

            if (game.Shredding) g.Text("SHRED: CLICK A SQUARE", lx, 156, Alarm);
            else if (Result == null && game.Stall >= 0) g.Text(Game.StallTitle(game.Stall), lx, 156, Alarm);
            else if (Result == null && Run.IsInspection(S.Week)) g.Text("INSPECTION: 1 CARD ONLY", lx, 156, Alarm);

            string route = Result != null ? $"{Result.From:00} {Result.Route}" : game.Drawn ? $"{S.Roll:00} {string.Join(" ", game.Played.Select(i => S.Hand[i].ToString()))}" : "";
            g.Text(route, lx, 168, Ink);

            bool bad;
            string[] lines;
            if (Result != null)
            {
                var a = Result.Air;
                bad = a.Holders > 0;
                lines = !bad ? new[] { "NOBODY HOLDS THIS!", "SHOW GOES ON!" }
                    : a.Tier == Tier.Small ? new[] { $"SMALL WINNER: {Holders(a.Holders).ToUpper()}.", $"PAID {a.Pay}. STREAK HOLDS." }
                    : new[] { $"SCANDAL: {Holders(a.Holders).ToUpper()}.", $"PAID {a.Pay}. STREAK LOST." };
                if (reveal) lines = lines.Concat(new[] { Result.Best.Cost > 0 ? $"NO CLEAN ROUTE. CHEAPEST {Result.Best.Cost}:" : "A CLEAN ROUTE WAS", Result.Line }).ToArray();
            }
            else if (number == null)
            {
                bad = false;
                lines = game.Live ? new[] { "THE MACHINE IS DRAWING." } : new[] { "NUMBER HIDDEN UNTIL THE SHOW.", "CARDS WAIT FOR ON AIR." };
            }
            else
            {
                int h = S.Board[number.Value];
                bad = h > 0;
                lines = bad ? new[] { $"{(Run.TierOf(h, game.Played.Count) == Tier.Small ? "SMALL WINNER" : "SCANDAL")}: {Holders(h).ToUpper()}.", $"COSTS {S.CostOf(number.Value, game.Played.Count)} CASH." }
                    : new[] { "NOBODY HOLDS THIS!", "SHOW GOES ON!" };
            }
            for (int i = 0; i < lines.Length; i++) g.Text(lines[i], lx, 188 + i * 13, bad ? Alarm : Good);

            const int ly = 252;
            string[] legend = { "1-2", "3-5", "6-10", "11+" };
            for (int i = 0; i < 4; i++) { g.Rect(lx, ly + i * 12, 8, 8, Tiers[i][0]); g.Text(legend[i], lx + 14, ly + i * 12, Ink); }
            g.Text($"{S.Sold} TICKETS +{S.Sold * Config.TicketPrice}", lx + 72, ly, Dim);
            if (S.LostCards > 0 && Result == null) g.Text("HAND FULL: CARD LOST", lx + 72, ly + 12, Alarm);
            if (!game.Live && Result == null) g.Text(S.Acted != null ? "DESK: DONE" : "DESK: 1 ACTION", lx + 72, ly + 36, S.Acted != null ? Dim : Good);
            if (Result != null && Result.Air.Bonus > 0) g.Text($"ON TIME +{Result.Air.Bonus}", lx + 72, ly + 36, Money);
            if (S.Over != Ending.None)
            {
                g.Text(S.Over == Ending.Lost ? $"BANKRUPT IN WEEK {S.Week}" : $"SURVIVED. CASH {S.Cash}", lx + 72, ly + 24, Money);
                g.Text($"CLEAN DRAWS {S.Cleans}  BEST STREAK {S.Best}", lx, ly + 52, Ink);
            }
        }

        void DrawTv(float t)
        {
            var g = Room.Tv;
            if (Result != null) OnAir(g, t);
            else
            {
                for (int i = 0; i < Bars.Length; i++) g.Rect(i * 92, 0, 92, 240, Bars[i]);
                g.Rect(0, 240, g.W, 120, H("#101020"));
                bool stalling = game.Stall >= 0;
                g.Centred(stalling ? Game.StallTitle(game.Stall) : game.Live ? "LIVE IN THE STUDIO" : "DRAW COMING UP", 262, H("#F2F2E6"), stalling ? 1 : 2, 3);
                g.Centred($"{S.Sold} NEW TICKETS SOLD", 312, H("#F2F2E6"));
            }
            g.Apply();
        }

        void OnAir(Canvas8 g, float t)
        {
            var a = Result.Air;
            for (int x = 0; x < g.W; x += 24) g.Rect(x, 0, 24, g.H, H(x % 48 != 0 ? "#7A0F2E" : "#A81440"));
            g.Rect(120, 40, 400, 236, Bg);
            Bulbs(g, 120, 40, 400, 236, t);
            g.Centred("THE BIG DRAW!", 56, Label, 2, 3);
            Drums(g, $"{Result.Number:00}", 232, 96);
            g.Centred(a.Holders == 0 ? "NOBODY WON!" : a.Holders == 1 ? "1 WINNER!" : $"{a.Holders} WINNERS!", 204, Ink, 2);
            g.Centred(a.Tier == Tier.Clean ? "WHAT A SHAME!" : a.Tier == Tier.Scandal ? "NATIONAL SCANDAL!" : "A NATIONAL HERO!", 228, Alarm, 2);
            string news = Ticker[0];
            g.Rect(0, 318, g.W, 16, Label);
            g.Text(news + news, 8 - (int)(t / 40) % (news.Length * 8), 322, Bg);
        }

        void DrawClock()
        {
            var c = Room.Clock;
            bool on = game.Live && Result == null;
            int left = Mathf.CeilToInt(Config.Target - game.LiveSeconds);
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
            foreach (var kv in Room.ActionPlates) Plate(kv.Value, kv.Key.ToString().ToUpper(), off && S.Acted == null ? on : dark);
            bool airing = game.Live && Result == null;
            Plate(Room.LivePlate, airing ? "ON AIR" : "GO LIVE", airing ? H("#ff3b2f") : off ? on : dark);

            string go = S.Over != Ending.None ? "PLAY AGAIN" : Result != null ? "NEXT WEEK" : game.Drawn ? $"RUN {game.Value:00}" : game.Live ? "DRAWING" : "GO LIVE FIRST";
            Plate(Room.GoPlate, go, game.Drawn || Result != null ? on : H("#8a8a90"));
            Plate(Room.UndoPlate, "EJECT", on);
        }

        void DrawCalendar()
        {
            var c = Room.Calendar;
            c.Fill(H("#E8E2CC"));
            c.Centred("INSPECTIONS", 8, H("#B3241C"), 2);
            for (int w = 1; w <= Config.Weeks; w++)
            {
                int x = 8 + (w - 1) % 4 * 61, y = 30 + (w - 1) / 4 * 56;
                bool insp = Run.IsInspection(w);
                c.Rect(x, y, 57, 52, H(insp ? "#B3241C" : "#D0C8AE"));
                c.Centred($"{w}{(insp ? "!" : "")}", y + 18, H(insp ? "#F2F2E6" : "#1C1E28"), 2, 2, x + 28);
                if (w < S.Week) c.Rect(x + 4, y + 44, 49, 3, H("#1C1E28"));
                if (w == S.Week) c.Frame(x + 2, y + 2, 53, 48, H("#1A2AA8"), 4);
            }
            c.Apply();
        }
    }
}
