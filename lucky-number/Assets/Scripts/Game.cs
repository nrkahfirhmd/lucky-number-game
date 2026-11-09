using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NoWinners
{
    public enum Mode { Start, Title, Play, Paused, Settings }

    // Player settings, kept in PlayerPrefs.
    public static class Prefs
    {
        public static float Volume = .8f;
        public static bool ReduceMotion;
        public static void Load() { Volume = PlayerPrefs.GetFloat("volume", .8f); ReduceMotion = PlayerPrefs.GetInt("reduceMotion") == 1; }
        public static void Save() { PlayerPrefs.SetFloat("volume", Volume); PlayerPrefs.SetInt("reduceMotion", ReduceMotion ? 1 : 0); PlayerPrefs.Save(); }
    }

    public class WeekResult
    {
        public float At; // When it aired: the TV and the end cards time from here.
        public int Number, From, Cards, CashBefore, StreakBefore;
        public string Route, Line;
        public List<Card> Path, BestPath; // Copied at air time: the hand is trimmed right after.
        public List<int> Steps; // The number after each played card. JAM and REDRAW are random, so they're kept, not recomputed.
        public List<Card> Unused; // Re-punched specials that weren't played: destroyed.
        public Route Best;
        public Announced Air;
        public bool Revealed;
    }

    // One seat, three look targets. Runs the week from docs/GDD.md, section 4.
    public class Game : MonoBehaviour
    {
        public const float LockSeconds = 1.6f, ReaderSeconds = .58f;
        static readonly string[][] Stalls =
        {
            // Title, desk notice, TV line. The TV line fits the on-air stage at 1x.
            new[] { "A JOKE FROM THE HOST", "HOST: \"Why did the ball cross the drum? ...We're still checking.\"", "WHY DID THE BALL CROSS THE DRUM?" },
            new[] { "A WORD FROM OUR SPONSOR", "SPONSOR: \"This delay is brought to you by Sunny Gravy.\"", "THIS DELAY IS BROUGHT TO YOU BY SUNNY GRAVY." },
            new[] { "WE ASK THE AUDIENCE", "AUDIENCE: \"Are we on? What did I buy? Who is this?\"", "ARE WE ON? WHAT DID I BUY?" },
            new[] { "TECHNICAL DIFFICULTIES", "STUDIO: \"Someone find the number! Anyone! Cut to the fish tank!\"", "PLEASE ENJOY THE FISH TANK." },
        };
        public static string StallTitle(int i) => Stalls[i % Stalls.Length][0];
        public static string StallLine(int i) => Stalls[i % Stalls.Length][2];
        static readonly Vector3 Eye = new Vector3(0, 1.32f, 1.92f), LeanEye = new Vector3(0, 1.06f, 2.4f);
        static readonly Vector3 LookMonitor = new Vector3(0, .92f, 3), LookLean = new Vector3(0, 1.06f, 2.945f), LookTv = new Vector3(-3.85f, 1.85f, .8f), LookMachine = new Vector3(3.1f, 1.1f, 1.4f);
        static bool firstRun = true; // Survives a restart, so only the very first week is seeded.

        // State the screens read.
        public Mode Mode { get; private set; } = Mode.Start;
        public Mode SettingsFrom { get; private set; } = Mode.Title;
        public int Sel { get; private set; } // The focused button or setting row on whichever menu is up.
        public float LandedAt = -100; // The last card landed: the monitor rolls its digits from here.
        public Run S { get; private set; }
        public readonly List<int> Played = new List<int>();
        public string Banner { get; private set; } // A short headline band: "WEEK 3", "ON AIR".
        public float BannerAt { get; private set; } = -100;
        void Headline(string text) { Banner = text; BannerAt = Time.time; }
        public readonly List<int> Landed = new List<int>(); // The number after each played card, in order.
        public int Punching { get; private set; } = -1; // The rack slot picked at the card punch, waiting for a hand card.
        public int PreviewRow { get; private set; } = -1; // JAM's preview: the whole row it could land in.
        public WeekResult Result { get; private set; }
        public int Preview { get; private set; } = -1;
        public bool Live { get; private set; }
        public bool Shredding { get; private set; }
        public int Stall { get; private set; } = -1;
        public string Station { get; private set; } = "monitor";
        public Room Room { get; private set; }
        public Sfx Sfx { get; private set; }
        public float RolledAt = -100, LiveAt;
        public bool Drawn => Live && Time.time >= LiveAt; // The number exists, and cards can be played.
        public float LiveSeconds => Mathf.Max(0, Time.time - LiveAt);
        public int Value => Landed.Count > 0 ? Landed[Landed.Count - 1] : S.Roll ?? 0;
        public bool Busy { get; private set; }

        Screens screens;
        public Screens Screens => screens;
        Camera cam;
        Vector3 camPos = Eye, camLook = LookMonitor;
        Vector2 pointer;
        bool lean, wasDrawn;
        int feeding = -1, hoverCard = -1; // The card flying into the reader, and the one under the pointer.
        readonly bool[] cardOut = new bool[Room.CardCount]; // Shown last frame: a card that just appeared snaps into place.
        // Focus (space): the hand fans along the bottom of the screen, in front of the leaned-in camera.
        public bool Focus => lean && Station == "monitor" && Mode == Mode.Play && Result == null;
        float offAt, lastDraw = -100, pressedAt = -100;
        int punched; // Re-punches this week, for the log.
        float slipAt; // When the reader started printing the slip on the desk.
        bool slipOut;
        string hover;
        public string HoverText => hover;
        public int CalendarWeek { get; private set; } // The week tile pointed at in the calendar close-up, or 0.
        string runId, logPath;
        public static bool KeepLog = true;
        public static string LogFile = "weeks-v2.csv"; // v2 columns: a new file, so old logs stay readable. Tests point it elsewhere.

        void Awake()
        {
            cam = Camera.main;
            cam.fieldOfView = 50;
            cam.nearClipPlane = .05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            Room = new Room();
            screens = new Screens(this);
            logPath = Path.Combine(Application.persistentDataPath, LogFile);
            Prefs.Load();
            Room.SetCrt(1); // Full CRT, always: no longer a setting.
            Sfx = gameObject.AddComponent<Sfx>();
            StartRun();
            Mode = Mode.Start; // The TV title card, then the menu on the monitor.
        }

        // ---- Menus: title and settings on the monitor, pause and end cards on the HUD ----
        void SetMode(Mode m) { Mode = m; Sel = 0; Time.timeScale = m == Mode.Paused || (m == Mode.Settings && SettingsFrom == Mode.Paused) ? 0 : 1; Refresh(); }

        public void Begin() { StartRun(); SetMode(Mode.Play); }
        public void ToTitle() { StartRun(); SetMode(Mode.Title); }
        public void Pause() { if (Mode == Mode.Play && !Busy) SetMode(Mode.Paused); }
        public void Resume() { if (Mode == Mode.Paused) SetMode(Mode.Play); }
        public void OpenSettings() { SettingsFrom = Mode; SetMode(Mode.Settings); }
        public void CloseSettings() { Prefs.Save(); SetMode(SettingsFrom); }

        public void SetVolume(float v) => Prefs.Volume = Mathf.Clamp01(Mathf.Round(v * 20) / 20);
        public const int SettingRows = 2; // Volume, reduce motion. BACK comes after.
        public const int BackY = 180; // Settings BACK button, on the monitor. Screens.SettingsPanel draws the same layout.

        // Menu buttons, top to bottom, for whichever menu is up. Empty when none is.
        System.Action[] Buttons()
        {
            if (Mode == Mode.Title) return new System.Action[] { Begin, OpenSettings, Application.Quit };
            if (Mode == Mode.Paused) return new System.Action[] { Resume, OpenSettings, ToTitle };
            if (Mode == Mode.Play && EndCardUp) return new System.Action[] { StartRun, ToTitle };
            return new System.Action[0];
        }

        // Settings rows: left/right or a click changes the focused one.
        void Nudge(int dir)
        {
            if (Sel == 0) SetVolume(Prefs.Volume + dir * .1f);
            else if (Sel == 1) Prefs.ReduceMotion = dir > 0;
        }

        public bool EndCardUp => Result != null && S.Over != Ending.None
            && Time.time - Result.At >= (S.Over == Ending.Won ? 2 : Prefs.ReduceMotion ? 1.5f : 2.4f);

        // The HUD is drawn at 640x360 and scaled by whole numbers, centred in the window.
        public static int HudScale => Mathf.Max(1, Mathf.Min(Screen.width / 640, Screen.height / 360));
        public static UnityEngine.Rect HudRect { get { int s = HudScale; return new UnityEngine.Rect((Screen.width - 640 * s) / 2, (Screen.height - 360 * s) / 2, 640 * s, 360 * s); } }
        public static Vector2 HudToScreen(int x, int y) { var r = HudRect; return new Vector2(r.x + x * HudScale, Screen.height - r.y - y * HudScale); }
        static Vector2Int ScreenToHud(Vector2 at) { var r = HudRect; return new Vector2Int(Mathf.FloorToInt((at.x - r.x) / HudScale), Mathf.FloorToInt((Screen.height - at.y - r.y) / HudScale)); }

        // A pixel on the 640x360 monitor picture, through the real screen collider.
        public Vector2 MonitorToScreen(int x, int y) =>
            cam.WorldToScreenPoint(Room.MonitorScreen.transform.TransformPoint(new Vector3(x / 640f - .5f, .5f - y / 360f, 0)));
        Vector2Int MonitorPixel(Collider c, Vector3 point)
        {
            if (c != Room.MonitorScreen) return new Vector2Int(-1, -1);
            var uv = c.transform.InverseTransformPoint(point);
            return new Vector2Int(Mathf.FloorToInt((uv.x + .5f) * 640), Mathf.FloorToInt((.5f - uv.y) * 360));
        }

        static bool In(Vector2Int p, int x, int y, int w, int h) => p.x >= x && p.x < x + w && p.y >= y && p.y < y + h;

        void ClickMenu(Vector2 at)
        {
            var buttons = Buttons();
            if (Mode == Mode.Paused || Mode == Mode.Play)
            {
                var p = ScreenToHud(at);
                for (int i = 0; i < buttons.Length; i++)
                    if (Mode == Mode.Paused ? In(p, 200, 168 + i * 40, 240, 32) : In(p, 176, 232 + i * 40, 288, 32)) { buttons[i](); return; }
                return;
            }
            var m = MonitorPixel(Pick(at, out var point), point);
            if (Mode == Mode.Title)
            {
                for (int i = 0; i < buttons.Length; i++) if (In(m, 352, 184 + i * 40, 272, 32)) { buttons[i](); return; }
                return;
            }
            // Settings: rows every 64 px from y 44, BACK below them. Screens.SettingsPanel draws the same layout.
            if (In(m, 352, BackY, 272, 32)) { CloseSettings(); return; }
            for (int row = 0; row < SettingRows; row++)
                if (In(m, 352, 44 + row * 64, 272, 56)) Sel = row;
            if (In(m, 364, 64, 176, 32)) SetVolume((m.x - 364) / 176f);
            for (int i = 0; i < 2; i++)
                if (In(m, 364 + i * 80, 132, 72, 24)) Prefs.ReduceMotion = i == 1;
        }

        void MenuKeys(Keyboard keys)
        {
            int count = Mode == Mode.Settings ? SettingRows + 1 : Buttons().Length;
            if (count == 0) return;
            bool up = keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame, down = keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame;
            if (up) Sel = (Sel + count - 1) % count;
            if (down) Sel = (Sel + 1) % count;
            bool enter = keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame;
            if (Mode == Mode.Settings)
            {
                if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) Nudge(-1);
                if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) Nudge(1);
                if (enter && Sel == SettingRows) CloseSettings();
                return;
            }
            if (enter) Buttons()[Mathf.Min(Sel, count - 1)]();
        }

        // ---- The week ----
        public void StartRun() { S = new Run(); S.ShuffleDates(); Sel = 0; runId = DateTime.Now.ToString("yyyyMMdd-HHmmss"); NextWeek(); }

        // A list of numbers for the calendar: "07, 23 or 61".
        public static string Numbers(IEnumerable<int> ns) { var s = ns.Select(n => n.ToString("00")).ToList(); return s.Count < 2 ? string.Join("", s) : string.Join(", ", s.Take(s.Count - 1)) + " or " + s[s.Count - 1]; }

        // What a week on the calendar means, for the close-up's hover.
        public string WeekInfo(int w)
        {
            var d = S.Dates[w];
            string when = w < S.Week ? " (done)" : w == S.Week ? " (this week)" : "";
            string what = Run.IsInspection(w) ? $"INSPECTION. You may play at most {Config.InspectionCards} card on air."
                : d == Special.Payday ? $"PAYDAY. Tickets x{Config.PaydayTickets}, so cash x{Config.PaydayTickets}. The extra tickets stay on the board."
                : d == Special.Telethon ? $"TELETHON. Prize x{Config.TelethonPrize}, and +{Config.Live * Config.TelethonLive} if you announce on time."
                : d == Special.Nephew ? $"MINISTER'S NEPHEW. Land on one of his {Config.NephewNumbers} numbers for +{Config.NephewReward}. "
                    + (S.Nephews.Count > 0 ? $"He has {Numbers(S.Nephews)}." : "The minister phones his numbers the week before.")
                : "An ordinary week.";
            return $"WEEK {w}{when}: {what}";
        }

        // What a special card does, for hover text.
        public static string Explain(Card c) => c.Op == 'S' ? "swap the two digits. Not on 00, 11 ... 99"
            : c.Op == 'M' ? "99 minus the number: jump to the opposite side" : c.Op == 'T' ? $"tens digit {c.Arg}, straight along the column"
            : c.Op == 'W' ? "copy the tens digit to the units: 47 becomes 44" : c.Op == 'R' ? $"add {c.X}, wrapping past 99 to 00"
            : c.Op == 'J' ? "redraw the units ball at random" : c.Op == 'D' ? "the machine draws a whole new number" : "";

        public static string DateName(Special d) => d == Special.Payday ? "PAYDAY" : d == Special.Telethon ? "TELETHON" : d == Special.Nephew ? "NEPHEW" : "";

        void NextWeek()
        {
            Live = false; Stall = -1; Shredding = false; Result = null; Preview = -1; PreviewRow = -1; Punching = -1; punched = 0;
            Played.Clear();
            Landed.Clear();
            offAt = Time.time;
            S.StartWeek();
            Headline($"WEEK {S.Week}");
            Station = "monitor";
            Refresh();
        }

        public void TakeAction(DeskAction action)
        {
            if (Live || Result != null || S.Acted != null) { Sfx.Play("desk_denied"); return; }
            if (action == DeskAction.Shred) { Shredding = !Shredding; if (Shredding) Look("monitor"); Refresh(); return; } // Aim it at the board.
            if (!S.TakeAction(action)) { Sfx.Play("desk_denied"); return; }
            Refresh();
        }

        public void ShredSquare(int n)
        {
            if (!S.TakeAction(DeskAction.Shred, n)) return;
            Shredding = false; Preview = -1;
            Refresh();
        }

        // The card punch: pick a special in the rack, then the arithmetic card it replaces. Off air only.
        public void PickRack(int r)
        {
            if (Live || Result != null || r >= S.Rack.Count) { Sfx.Play("desk_denied"); return; }
            Punching = Punching == r ? -1 : r;
            Shredding = false;
            Refresh();
        }

        public void Repunch(int hand)
        {
            if (!S.Repunch(hand, Punching)) { Sfx.Play("desk_denied"); return; }
            Punching = -1;
            punched++;
            Refresh();
        }

        // The printer's slip: TAKE pins it, TEAR throws it away.
        public void Printer(bool take)
        {
            if (Live || Result != null || !(take ? S.TakeSlip() : S.TearSlip())) { Sfx.Play("desk_denied"); return; }
            Refresh();
        }

        public void GoLive()
        {
            if (Live || Result != null) return;
            S.TearSlip(); // An unread slip goes in the bin.
            S.TakeAction(DeskAction.LieLow); // No desk action: you lay low.
            S.DrawNumber();
            if (firstRun) { S.SeedRoll(); firstRun = false; }
            Live = true; Shredding = false; Preview = -1; Punching = -1;
            Headline("ON AIR");
            RolledAt = Time.time;
            LiveAt = RolledAt + LockSeconds;
            Refresh();
        }

        public bool CanPlay(int i) => Drawn && Result == null && !Busy && i < S.Hand.Count && !Played.Contains(i)
            && Played.Count < Run.CardLimit(S.Week) && Run.CanApply(Value, S.Hand[i]);

        public void PlayCard(int i)
        {
            if (CanPlay(i)) StartCoroutine(FeedReader(i));
            else if (!Busy && i < S.Hand.Count && !Played.Contains(i)) Sfx.Play(Drawn && Result == null ? "rig_illegal" : "desk_denied"); // Off air the cards just won't move.
        }

        // Punch cards take real seconds, and they come off the on-air target.
        IEnumerator FeedReader(int i)
        {
            Busy = true;
            feeding = i;
            var card = Room.Cards[i];
            Vector3 from = card.position, scale = card.localScale;
            Quaternion rot = card.rotation, flat = Quaternion.identity;
            for (float t = 0; t < .34f; t += Time.deltaTime)
            {
                float k = t / .34f;
                card.position = Vector3.Lerp(from, Room.ReaderSlot, k);
                card.rotation = Quaternion.Slerp(rot, flat, k);
                card.localScale = scale * (1 - k * .25f);
                yield return null;
            }
            card.gameObject.SetActive(false);
            Room.SetLamp(true);
            yield return new WaitForSeconds(ReaderSeconds - .34f);
            Room.SetLamp(false);
            card.SetPositionAndRotation(from, rot);
            card.localScale = scale;
            Busy = false;
            feeding = -1;
            Landed.Add(S.Resolve(Value, S.Hand[i])); // JAM and REDRAW roll here, once.
            Played.Add(i);
            LandedAt = Time.time;
            Preview = -1;
            Refresh();
        }

        public void Undo()
        {
            if (Result != null || Busy || Played.Count == 0) return;
            if (S.Hand[Played[Played.Count - 1]].IsRandom) { Sfx.Play("desk_denied"); return; } // The ball's already out.
            Played.RemoveAt(Played.Count - 1);
            Landed.RemoveAt(Landed.Count - 1);
            Refresh();
        }

        // The red button: announce, then step through broadcast, reveal and next week.
        public void Go()
        {
            if (Busy || Mode != Mode.Play) return;
            if (Result != null && S.Over != Ending.None) return; // The end card's buttons take it from here.
            if (Result != null && !Result.Revealed)
            {
                Result.Revealed = true; // The board, and the HUD's receipt for the week.
                Station = "monitor";
                Refresh();
                return;
            }
            if (Result != null) { NextWeek(); return; }
            pressedAt = Time.time;
            if (!Drawn) return;
            Air();
        }

        // The number goes out. Everything the reveal and the log need is read before announcing changes it.
        void Air()
        {
            var best = S.BestRoute();
            int from = S.Roll.Value, number = Value, rate = S.Prize;
            float seconds = LiveSeconds;
            string steps = string.Join(" ", best.Path.Select(i => S.Hand[i].ToString()));
            Result = new WeekResult
            {
                At = Time.time, Number = number, From = from, Best = best, Cards = Played.Count,
                Route = string.Join(" ", Played.Select(i => S.Hand[i].ToString())),
                Line = steps.Length > 0 ? $"{from:00} {steps} = {best.To:00}" : $"{from:00} AS DRAWN",
                Path = Played.Select(i => S.Hand[i]).ToList(),
                Steps = new[] { from }.Concat(Landed).ToList(),
                BestPath = best.Path.Select(i => S.Hand[i]).ToList(),
                CashBefore = S.Cash, StreakBefore = S.Streak, // Initialisers run in order, so these read before Announce.
                Air = S.Announce(number, Played.Count, seconds <= Config.Target, seconds),
            };
            Result.Unused = S.Discard(Played); // Before Log, which reads it.
            Log(best, number, rate, seconds);
            Played.Clear();
            Landed.Clear();
            Station = "tv";
            Refresh();
        }

        // GDD section 9. The Unity build is what gets playtested, so keep the numbers.
        void Log(Route best, int number, int rate, float seconds)
        {
            if (!KeepLog) return;
            try
            {
                if (!File.Exists(logPath)) File.WriteAllText(logPath, "run,week,desk,offAirSeconds,roll,final,cards,cleanRoute,holders,result,prize,cash,onAirSeconds,special,slip,slipMet,repunched,unused\n");
                File.AppendAllText(logPath, $"{runId},{S.Week},{S.Acted},{LiveAt - LockSeconds - offAt:0},{Result.From},{number},{Result.Cards},{best.Cost <= 0},{Result.Air.Holders},{Result.Air.Tier},{rate},{S.Cash},{seconds:0},{S.Today},{Result.Air.Slip},{Result.Air.SlipMet},{punched},{Result.Unused.Count}\n");
            }
            catch (IOException e) { Debug.LogWarning($"Week log not written: {e.Message}"); }
        }

        // The show stalls past the on-air target. Nothing is forced and no cash is lost.
        void CheckStall()
        {
            if (!Live || Result != null) return;
            float over = LiveSeconds - Config.Target;
            int i = over < 0 ? -1 : Mathf.FloorToInt(over / Config.StallEvery);
            if (i == Stall) return;
            Stall = i;
            Refresh();
        }

        // ---- Input ----
        void Update()
        {
            if (Drawn != wasDrawn) { wasDrawn = Drawn; Refresh(); } // Cards wake up when the machine locks.
            ReadInput();
            CheckStall();
            if (Time.unscaledTime - lastDraw > .1f) { screens.DrawTimed(); lastDraw = Time.unscaledTime; }
            AnimateMachine();
            bool printing = Room.Slip.gameObject.activeSelf && S.Pinned == Slip.None;
            if (printing && !slipOut) slipAt = Time.time; // A new slip: start printing it.
            slipOut = printing;
            if (printing) // The slip rises out of the reader's slot.
            {
                float k = Prefs.ReduceMotion ? 1 : Mathf.SmoothStep(0, 1, (Time.time - slipAt - .4f) / 1.4f);
                Room.Slip.position = Room.SlipRest - Room.Slip.up * (1 - k) * Room.SlipH;
            }
            Room.GoCap.position = Room.GoRest + (Time.time - pressedAt < .2f ? Vector3.down * .015f : Vector3.zero);
            Room.Lever.localEulerAngles = new Vector3(Live ? -34 : 34, 0, 0);
            MoveCamera();
            LayoutCards();
        }

        public void Look(string station) => Station = station;

        void ReadInput()
        {
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame)
            {
                if (Mode == Mode.Settings) CloseSettings();
                else if (Mode == Mode.Paused) Resume();
                else if (Mode == Mode.Play && Shredding) { Shredding = false; Refresh(); }
                else if (Mode == Mode.Play && Punching >= 0) { Punching = -1; Refresh(); }
                else if (Mode == Mode.Play && (Station == "calendar" || Station == "slip")) Look("monitor");
                else Pause();
            }
            else if (keys != null && Mode == Mode.Start) { if (keys.anyKey.wasPressedThisFrame) SetMode(Mode.Title); }
            else if (keys != null && (Mode != Mode.Play || EndCardUp)) MenuKeys(keys);
            else if (keys != null)
            {
                if (keys.spaceKey.wasPressedThisFrame) lean = !lean;
                int look = Station == "tv" ? 0 : Station == "machine" ? 2 : 1, was = look;
                if (keys.aKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame) look = Mathf.Max(0, look - 1);
                if (keys.dKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame) look = Mathf.Min(2, look + 1);
                if (look != was) Look(look == 0 ? "tv" : look == 1 ? "monitor" : "machine");
            }
            if (mouse == null) return;
            Vector2 at = mouse.position.ReadValue();
            pointer = new Vector2(at.x / Screen.width * 2 - 1, at.y / Screen.height * 2 - 1);
            if (Mode == Mode.Play) Hover(at);
            if (mouse.leftButton.wasPressedThisFrame) Click(at);
        }

        Collider Pick(Vector2 at, out Vector3 point)
        {
            Physics.SyncTransforms();
            bool hit = Physics.Raycast(cam.ScreenPointToRay(at), out var h, 10);
            point = hit ? h.point : default;
            return hit ? h.collider : null;
        }

        int SquareAt(Collider c, Vector3 point)
        {
            var m = MonitorPixel(c, point);
            if (m.x < 0) return -1;
            int col = Mathf.FloorToInt((m.x - 16f) / Screens.Cell), row = Mathf.FloorToInt((m.y - 24f) / Screens.Cell);
            return col >= 0 && col < 10 && row >= 0 && row < 10 ? row * 10 + col : -1;
        }

        int CalendarAt(Vector3 point)
        {
            var uv = Room.CalendarPaper.transform.InverseTransformPoint(point);
            return Screens.CalendarWeekAt(Mathf.FloorToInt((uv.x + .5f) * 256), Mathf.FloorToInt((.5f - uv.y) * 320));
        }

        void Hover(Vector2 at)
        {
            Collider c = null;
            var point = default(Vector3);
            if ((Station == "monitor" || Station == "machine" || Station == "calendar" || Station == "slip") && Result == null) c = Pick(at, out point);
            int week = Station == "calendar" && c == Room.CalendarPaper ? CalendarAt(point) : 0;
            if (week != CalendarWeek) { CalendarWeek = week; Refresh(); }
            int preview = Shredding ? SquareAt(c, point) : -1;
            hover = null;
            int over = c == null ? -1 : Array.FindIndex(Room.Cards, t => t.GetComponent<Collider>() == c);
            int rack = c == null ? -1 : Array.FindIndex(Room.Rack, t => t.GetComponent<Collider>() == c);
            hoverCard = over;
            int row = -1;
            if (over >= 0 && CanPlay(over))
            {
                var k = S.Hand[over];
                if (k.Op == 'J') row = Value / 10;
                else if (!k.IsRandom) preview = Run.Apply(Value, k).Value;
            }
            if (preview != Preview || row != PreviewRow) { Preview = preview; PreviewRow = row; lastDraw = -100; }
            if (week > 0) { hover = WeekInfo(week); return; }
            if (c == Room.CalendarPaper && Station != "calendar" && !Shredding) { hover = "CALENDAR: click to read it up close."; return; }
            if (over >= 0 && S.Hand[over].IsSpecial) { hover = $"{S.Hand[over]}: {Explain(S.Hand[over])}."; return; }
            if (rack >= 0 && rack < S.Rack.Count)
            {
                hover = $"RACK: {S.Rack[rack]}, {Explain(S.Rack[rack])}. " + (Live ? "Re-punch off air only." : "Click it, then the hand card it replaces. Unplayed tonight, it's gone.");
                return;
            }
            if (c != null && c.transform == Room.Slip)
            {
                var slip = S.Pinned != Slip.None ? S.Pinned : S.Offer;
                int button = Station == "slip" && S.Offer != Slip.None && !Live ? SlipButton(point) : 0;
                hover = button == 1 ? $"TAKE: pin it to the monitor. {Slips.Wants(slip)} pays {Slips.Pays(slip)}; miss it and {Slips.Fails(slip)}."
                    : button == 2 ? "TEAR: throw it away. Free."
                    : Station == "slip" ? $"{Slips.Name(slip)}: {Slips.Quote(slip)} Wants {Slips.Wants(slip)}, pays {Slips.Pays(slip)}. Miss: {Slips.Fails(slip)}."
                    : $"SLIP: {Slips.Name(slip)}. Click to read it up close.";
                return;
            }
            if (c == null || Live) return;
            if (c == Room.LiveSwitch) hover = "GO LIVE: the machine draws, and you rig the number on air." + (S.Acted == null ? $" No desk action yet: you'll lie low, prize -{Config.LieLow}." : "");
            else if (c == Room.ActionProps[DeskAction.Supplier]) hover = $"SUPPLIER: +1 card for {Config.SupplierCost} cash. Paid even if a full hand loses the card.";
            else if (c == Room.ActionProps[DeskAction.Bribe]) hover = $"BRIBE: +{Config.Bribe} cash, but the prize rises {Config.Rollover} for the rest of the run.";
            else if (c == Room.ActionProps[DeskAction.Shred]) hover = "SHRED: clear one square. Its holders rebuy next door, either side in the same row.";
        }

        // Public so a test can click real props through the real raycast.
        public void Click(Vector2 at)
        {
            if (Busy) return;
            if (Mode == Mode.Start) { SetMode(Mode.Title); return; }
            if (Mode != Mode.Play || EndCardUp) { ClickMenu(at); return; }
            if (Result != null) { Go(); return; }
            if (Station == "calendar") { Look("monitor"); return; } // Any click puts it down.
            if (Station == "slip") // Up close: TAKE or TEAR on the slip; anywhere else puts it down.
            {
                var hit = Pick(at, out var on);
                int button = hit != null && hit.transform == Room.Slip && S.Offer != Slip.None && !Live ? SlipButton(on) : 0;
                if (button > 0) Printer(button == 1);
                Look("monitor");
                return;
            }
            if (Station != "monitor" && Station != "machine") return;
            var c = Pick(at, out var point);
            if (c == null) return;
            if (c == Room.CalendarPaper && !Shredding) { Look("calendar"); return; }
            if (Station == "machine") { foreach (var kv in Room.ActionProps) if (c == kv.Value) TakeAction(kv.Key); return; } // The side table.
            if (Shredding && SquareAt(c, point) >= 0) { ShredSquare(SquareAt(c, point)); return; }
            if (c == Room.GoButton) { Go(); return; }
            if (c == Room.UndoButton) { Undo(); return; }
            if (c == Room.LiveSwitch) { GoLive(); return; }
            foreach (var kv in Room.ActionProps) if (c == kv.Value) { TakeAction(kv.Key); return; }
            int rack = Array.FindIndex(Room.Rack, t => t.GetComponent<Collider>() == c);
            if (rack >= 0) { PickRack(rack); return; }
            if (c.transform == Room.Slip) { Look("slip"); return; } // Read it up close first, like the calendar.
            int card = Array.FindIndex(Room.Cards, t => t.GetComponent<Collider>() == c);
            if (card >= 0 && Punching >= 0) Repunch(card);
            else if (card >= 0) PlayCard(card);
        }

        // Which button on the 160x192 slip a point is on: 1 TAKE, 2 TEAR, 0 neither. Screens.DrawSlip draws the same layout.
        int SlipButton(Vector3 point)
        {
            var uv = Room.Slip.InverseTransformPoint(point);
            int x = Mathf.FloorToInt((uv.x + .5f) * 160), y = Mathf.FloorToInt((.5f - uv.y) * 192);
            return In(new Vector2Int(x, y), 8, 154, 68, 30) ? 1 : In(new Vector2Int(x, y), 84, 154, 68, 30) ? 2 : 0;
        }

        // Cards on the desk mat, or fanned along the bottom of the screen in focus. The pointed-at card lifts if it can be used.
        void LayoutCards()
        {
            int n = S.Hand.Count;
            float ease = 1 - Mathf.Exp(-Mathf.Min(Time.deltaTime, .05f) * 18);
            for (int i = 0; i < Room.CardCount; i++)
            {
                var card = Room.Cards[i];
                bool shown = card.gameObject.activeSelf, snap = shown && !cardOut[i];
                cardOut[i] = shown;
                if (!shown || i == feeding) continue;
                bool up = Mode == Mode.Play && i == hoverCard && i < n && (CanPlay(i) || (Punching >= 0 && !S.Hand[i].IsSpecial));
                float d = i - (n - 1) / 2f;
                Vector3 pos, scale = Room.CardSize;
                Quaternion rot;
                if (Focus)
                {
                    // ponytail: 0.35 m from the eye keeps the fan in front of the monitor glass; 0.6 scale keeps the board's bottom row readable.
                    var t = cam.transform;
                    pos = t.TransformPoint(new Vector3(d * .075f, -.158f - d * d * .004f + (up ? .045f : 0), .35f + i * .0005f - (up ? .006f : 0)));
                    rot = t.rotation * Quaternion.Euler(0, 0, up ? 0 : -d * 5);
                    scale *= .6f;
                }
                else
                {
                    pos = new Vector3(d * Room.CardStep, Room.CardBaseY + (up ? .02f : 0), Room.CardZ);
                    rot = Quaternion.Euler(Room.CardEuler);
                }
                float k = snap ? 1 : ease;
                card.SetPositionAndRotation(Vector3.Lerp(card.position, pos, k), Quaternion.Slerp(card.rotation, rot, k));
                card.localScale = Vector3.Lerp(card.localScale, scale, k);
            }
        }

        // ---- Picture ----
        public void Refresh()
        {
            screens.DrawDesk();
            lastDraw = -100;
        }

        void AnimateMachine()
        {
            float spin = Time.time - RolledAt < LockSeconds ? 7 : .6f;
            for (int i = 0; i < Room.Balls.Length; i++)
            {
                float a = Time.time * spin * (.6f + i * .03f) + i * 2.1f;
                float lift = spin > 1 ? Mathf.Abs(Mathf.Sin(a * 1.3f + i)) * .5f : .04f * Mathf.Abs(Mathf.Sin(a));
                float r = .28f * (.5f + i % 3 * .3f);
                Room.Balls[i].position = new Vector3(3.1f + Mathf.Cos(a) * r, .95f + lift + i % 4 * .03f, 1.4f + Mathf.Sin(a) * r);
            }
        }

        void MoveCamera()
        {
            float ease = 1 - Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime, .05f) * 5);
            bool menu = Mode == Mode.Title || Mode == Mode.Settings; // Menus live on the monitor, seen up close.
            string at = Mode == Mode.Start ? "tv" : menu ? "monitor" : Station;
            bool leaning = menu || (lean && at == "monitor" && Mode == Mode.Play);
            var paper = Room.CalendarPaper.transform.position; // Close-up: square on, near enough to fill the view.
            bool slip = at == "slip", calendar = at == "calendar" || slip;
            if (slip) paper = S.Pinned != Slip.None ? Room.SlipPinned : Room.SlipRest; // Its resting place, so the print-out doesn't drag the camera.
            Vector3 look = leaning ? LookLean : at == "tv" ? LookTv : at == "machine" ? LookMachine : calendar ? paper : LookMonitor;
            camPos = Vector3.Lerp(camPos, leaning ? LeanEye : calendar ? paper + new Vector3(0, 0, slip ? -.34f : -.68f) : Eye, ease);
            camLook = Vector3.Lerp(camLook, look, ease);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, at == "tv" ? 26 : 50, ease);
            var sway = calendar ? Vector2.zero : pointer; // The close-up holds still, so the week under the pointer stays put.
            cam.transform.position = camPos + new Vector3(sway.x * .03f, sway.y * .015f, 0);
            cam.transform.LookAt(camLook + new Vector3(sway.x * .05f, sway.y * .03f, 0));
        }

        // Only what's under the pointer: notices and instructions were cut as noise.
        string Hint() => hover ?? "";

        // ponytail: plain IMGUI line for hover text. Replace with tags on the props if it reads as clutter.
        GUIStyle hintStyle;
        void OnGUI()
        {
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(HudRect, screens.Hud.Tex);
            if (Mode != Mode.Play || Hint().Length == 0) return;
            hintStyle ??= new GUIStyle(GUI.skin.box) { fontSize = Mathf.Max(14, Screen.height / 45), wordWrap = true };
            float w = Screen.width * .8f, h = hintStyle.CalcHeight(new GUIContent(Hint()), w);
            GUI.Box(new UnityEngine.Rect((Screen.width - w) / 2, Screen.height - h - 8, w, h), Hint(), hintStyle);
        }
    }
}
