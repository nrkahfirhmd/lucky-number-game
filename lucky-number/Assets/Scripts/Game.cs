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

    // Player settings, kept in PlayerPrefs. The game is fully playable at CRT 0.
    public static class Prefs
    {
        public static float Crt = 1, Volume = .8f;
        public static bool ReduceMotion, Long;
        // ponytail: the handoff names LONG without a number; 30 s is a guess, tune at playtest.
        public static float Target => Long ? 30 : Config.Target;
        public static void Load() { Crt = PlayerPrefs.GetFloat("crt", 1); Volume = PlayerPrefs.GetFloat("volume", .8f); ReduceMotion = PlayerPrefs.GetInt("reduceMotion") == 1; Long = PlayerPrefs.GetInt("longTarget") == 1; }
        public static void Save() { PlayerPrefs.SetFloat("crt", Crt); PlayerPrefs.SetFloat("volume", Volume); PlayerPrefs.SetInt("reduceMotion", ReduceMotion ? 1 : 0); PlayerPrefs.SetInt("longTarget", Long ? 1 : 0); PlayerPrefs.Save(); }
    }

    public class WeekResult
    {
        public float At; // When it aired: the TV and the end cards time from here.
        public int Number, From, Cards, CashBefore, StreakBefore;
        public string Route, Line;
        public List<Card> Path, BestPath; // Copied at air time: the hand is trimmed right after.
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
        public WeekResult Result { get; private set; }
        public int Preview { get; private set; } = -1;
        public bool Live { get; private set; }
        public bool Shredding { get; private set; }
        public int Stall { get; private set; } = -1;
        public string Station { get; private set; } = "monitor";
        public Room Room { get; private set; }
        public Sfx Sfx { get; private set; }
        public string Notice = "";
        public float RolledAt = -100, LiveAt;
        public bool Drawn => Live && Time.time >= LiveAt; // The number exists, and cards can be played.
        public float LiveSeconds => Mathf.Max(0, Time.time - LiveAt);
        public int Value => Played.Aggregate(S.Roll ?? 0, (v, i) => Run.Apply(v, S.Hand[i]).Value);
        public bool Busy { get; private set; }

        Screens screens;
        public Screens Screens => screens;
        Camera cam;
        Vector3 camPos = Eye, camLook = LookMonitor;
        Vector2 pointer;
        bool lean, wasDrawn;
        float offAt, lastDraw = -100, pressedAt = -100;
        string hover;
        public string HoverText => hover;
        string runId, logPath;
        public static bool KeepLog = true;

        void Awake()
        {
            cam = Camera.main;
            cam.fieldOfView = 50;
            cam.nearClipPlane = .05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            Room = new Room();
            screens = new Screens(this);
            logPath = Path.Combine(Application.persistentDataPath, "weeks.csv");
            Prefs.Load();
            Room.SetCrt(Prefs.Crt);
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

        public void SetCrt(float v) { Prefs.Crt = Mathf.Clamp01(Mathf.Round(v * 20) / 20); Room.SetCrt(Prefs.Crt); }
        public void SetVolume(float v) => Prefs.Volume = Mathf.Clamp01(Mathf.Round(v * 20) / 20);
        public const int SettingRows = 4; // CRT, volume, reduce motion, on-air target. BACK comes after.

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
            if (Sel == 0) SetCrt(Prefs.Crt + dir * .1f);
            else if (Sel == 1) SetVolume(Prefs.Volume + dir * .1f);
            else if (Sel == 2) Prefs.ReduceMotion = dir > 0;
            else if (Sel == 3) Prefs.Long = dir > 0;
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
            if (In(m, 352, 300, 272, 32)) { CloseSettings(); return; }
            for (int row = 0; row < SettingRows; row++)
                if (In(m, 352, 44 + row * 64, 272, 56)) Sel = row;
            if (In(m, 364, 64, 176, 32)) SetCrt((m.x - 364) / 176f);
            if (In(m, 364, 128, 176, 32)) SetVolume((m.x - 364) / 176f);
            for (int i = 0; i < 2; i++)
            {
                if (In(m, 364 + i * 80, 196, 72, 24)) Prefs.ReduceMotion = i == 1;
                if (In(m, 364 + i * 112, 260, 104, 24)) Prefs.Long = i == 1;
            }
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
        public void StartRun() { S = new Run(); Sel = 0; runId = DateTime.Now.ToString("yyyyMMdd-HHmmss"); NextWeek(); }

        void NextWeek()
        {
            Notice = "";
            Live = false; Stall = -1; Shredding = false; Result = null; Preview = -1;
            Played.Clear();
            offAt = Time.time;
            S.StartWeek();
            if (S.Week == 2) Notice = "NOTE ON DESK: \"Off air you get one favour a week. Supplier, bribe or shredder, on the side table. Look right.\"";
            if (S.Week == Config.Inspections[0] - 1) Notice = "PHONE: \"Inspectors next week. One card, no more. It's on the calendar.\"";
            Station = "monitor";
            Refresh();
        }

        public void TakeAction(DeskAction action)
        {
            if (Live || Result != null || S.Acted != null) { Sfx.Play("desk_denied"); return; }
            if (action == DeskAction.Shred) { Shredding = !Shredding; if (Shredding) Look("monitor"); Refresh(); return; } // Aim it at the board.
            int lost = S.LostCards;
            S.TakeAction(action);
            Notice = action == DeskAction.Bribe ? $"Minister's envelope: +{Config.Bribe} cash. The prize is now {S.Prize} a holder."
                : S.LostCards > lost ? "SUPPLIER: \"Your hand's full, boss. That one's gone.\"" : "SUPPLIER: \"One card. You never saw me.\"";
            Refresh();
        }

        public void ShredSquare(int n)
        {
            if (!S.TakeAction(DeskAction.Shred, n)) return;
            Shredding = false; Preview = -1;
            Notice = $"Shredded. {n:00} now has {(S.Board[n] > 0 ? Screens.Holders(S.Board[n]) : "no holders")}.";
            Refresh();
        }

        public void GoLive()
        {
            if (Live || Result != null) return;
            S.DrawNumber();
            if (firstRun) { S.SeedRoll(); firstRun = false; }
            Live = true; Shredding = false; Preview = -1; Notice = "";
            RolledAt = Time.time;
            LiveAt = RolledAt + LockSeconds;
            Refresh();
        }

        public bool CanPlay(int i) => Drawn && Result == null && !Busy && i < S.Hand.Count && !Played.Contains(i)
            && Played.Count < Run.CardLimit(S.Week) && Run.Apply(Value, S.Hand[i]) != null;

        public void PlayCard(int i)
        {
            if (CanPlay(i)) StartCoroutine(FeedReader(i));
            else if (!Busy && i < S.Hand.Count && !Played.Contains(i)) Sfx.Play(Drawn && Result == null ? "rig_illegal" : "desk_denied"); // Off air the cards just won't move.
        }

        // Punch cards take real seconds, and they come off the on-air target.
        IEnumerator FeedReader(int i)
        {
            Busy = true;
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
            Played.Add(i);
            LandedAt = Time.time;
            Preview = -1;
            Refresh();
        }

        public void Undo()
        {
            if (Result != null || Busy || Played.Count == 0) return;
            Played.RemoveAt(Played.Count - 1);
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
            Notice = "";
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
                BestPath = best.Path.Select(i => S.Hand[i]).ToList(),
                CashBefore = S.Cash, StreakBefore = S.Streak, // Initialisers run in order, so these read before Announce.
                Air = S.Announce(number, Played.Count, seconds <= Prefs.Target),
            };
            Log(best, number, rate, seconds);
            if (Result.Air.Tier == Tier.Clean && S.Cleans == 1) Notice = $"HOST: \"Nobody won! So next week's prize grows to {S.Prize} a winner!\"";
            S.Hand = S.Hand.Where((_, i) => !Played.Contains(i)).ToList();
            Played.Clear();
            Station = "tv";
            Refresh();
        }

        // GDD section 9. The Unity build is what gets playtested, so keep the numbers.
        void Log(Route best, int number, int rate, float seconds)
        {
            if (!KeepLog) return;
            try
            {
                if (!File.Exists(logPath)) File.WriteAllText(logPath, "run,week,desk,offAirSeconds,roll,final,cards,cleanRoute,holders,result,prize,cash,onAirSeconds\n");
                File.AppendAllText(logPath, $"{runId},{S.Week},{S.Acted},{LiveAt - LockSeconds - offAt:0},{Result.From},{number},{Result.Cards},{best.Cost == 0},{Result.Air.Holders},{Result.Air.Tier},{rate},{S.Cash},{seconds:0}\n");
            }
            catch (IOException e) { Debug.LogWarning($"Week log not written: {e.Message}"); }
        }

        // The show stalls past the on-air target. Nothing is forced and no cash is lost.
        void CheckStall()
        {
            if (!Live || Result != null) return;
            float over = LiveSeconds - Prefs.Target;
            int i = over < 0 ? -1 : Mathf.FloorToInt(over / Config.StallEvery);
            if (i == Stall) return;
            Stall = i;
            if (i >= 0) Notice = Stalls[i % Stalls.Length][1];
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
            Room.GoCap.position = Room.GoRest + (Time.time - pressedAt < .2f ? Vector3.down * .015f : Vector3.zero);
            Room.Lever.localEulerAngles = new Vector3(Live ? -34 : 34, 0, 0);
            MoveCamera();
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
                else Pause();
            }
            else if (keys != null && Mode == Mode.Start) { if (keys.anyKey.wasPressedThisFrame) SetMode(Mode.Title); }
            else if (keys != null && (Mode != Mode.Play || EndCardUp)) MenuKeys(keys);
            else if (keys != null)
            {
                if (keys.spaceKey.wasPressedThisFrame) lean = !lean;
                int look = Station == "tv" ? 0 : Station == "monitor" ? 1 : 2;
                if (keys.aKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame) look = Mathf.Max(0, look - 1);
                if (keys.dKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame) look = Mathf.Min(2, look + 1);
                Look(look == 0 ? "tv" : look == 1 ? "monitor" : "machine");
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

        void Hover(Vector2 at)
        {
            Collider c = null;
            var point = default(Vector3);
            if ((Station == "monitor" || Station == "machine") && Result == null) c = Pick(at, out point);
            int preview = Shredding ? SquareAt(c, point) : -1;
            hover = null;
            int over = c == null ? -1 : Array.FindIndex(Room.Cards, t => t.GetComponent<Collider>() == c);
            for (int i = 0; i < Room.CardCount; i++)
                if (!Busy && Room.Cards[i].gameObject.activeSelf)
                    Room.Cards[i].position = new Vector3(Room.Cards[i].position.x, Room.CardBaseY + (i == over && CanPlay(i) ? .02f : 0), Room.CardZ);
            if (over >= 0 && CanPlay(over)) preview = Run.Apply(Value, S.Hand[over]).Value;
            if (preview != Preview) { Preview = preview; lastDraw = -100; }
            if (c == null || Live) return;
            if (c == Room.LiveSwitch) hover = "GO LIVE: the machine draws, and you rig the number on air.";
            else if (c == Room.ActionProps[DeskAction.Supplier]) hover = "SUPPLIER: +1 card, lost if your hand is full.";
            else if (c == Room.ActionProps[DeskAction.Bribe]) hover = $"BRIBE: +{Config.Bribe} cash, but the prize rises {Config.Rollover} until your next scandal.";
            else if (c == Room.ActionProps[DeskAction.Shred]) hover = $"SHRED: remove up to {Config.Shred} holders from one square.";
        }

        // Public so a test can click real props through the real raycast.
        public void Click(Vector2 at)
        {
            if (Busy) return;
            if (Mode == Mode.Start) { SetMode(Mode.Title); return; }
            if (Mode != Mode.Play || EndCardUp) { ClickMenu(at); return; }
            if (Result != null) { Go(); return; }
            if (Station != "monitor" && Station != "machine") return;
            var c = Pick(at, out var point);
            if (c == null) return;
            if (Station == "machine") { foreach (var kv in Room.ActionProps) if (c == kv.Value) TakeAction(kv.Key); return; } // The side table.
            if (Shredding && SquareAt(c, point) >= 0) { ShredSquare(SquareAt(c, point)); return; }
            if (c == Room.GoButton) { Go(); return; }
            if (c == Room.UndoButton) { Undo(); return; }
            if (c == Room.LiveSwitch) { GoLive(); return; }
            foreach (var kv in Room.ActionProps) if (c == kv.Value) { TakeAction(kv.Key); return; }
            int card = Array.FindIndex(Room.Cards, t => t.GetComponent<Collider>() == c);
            if (card >= 0) PlayCard(card);
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
            Vector3 look = leaning ? LookLean : at == "tv" ? LookTv : at == "machine" ? LookMachine : LookMonitor;
            camPos = Vector3.Lerp(camPos, leaning ? LeanEye : Eye, ease);
            camLook = Vector3.Lerp(camLook, look, ease);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, at == "tv" ? 26 : 50, ease);
            cam.transform.position = camPos + new Vector3(pointer.x * .03f, pointer.y * .015f, 0);
            cam.transform.LookAt(camLook + new Vector3(pointer.x * .05f, pointer.y * .03f, 0));
        }

        string Hint()
        {
            if (Result != null) return $"{Notice} {(S.Over != Ending.None ? "" : !Result.Revealed ? "Click to see the board." : "Click anywhere for next week.")}".Trim();
            if (Shredding) return "Click a square on the monitor to shred it. Esc to cancel.";
            if (hover != null) return hover;
            if (Notice.Length > 0) return Notice;
            return Live ? "On air: rig the number with your cards, then press the red button." : "Off air, no clock. Look right for one desk action if you want, then flip GO LIVE.";
        }

        // ponytail: plain IMGUI line for notes and stalls. Replace with notes on the desk at the Comedy milestone.
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
