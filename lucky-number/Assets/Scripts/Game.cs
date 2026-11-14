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
        public static float Volume = .8f; // Sound effects.
        public static float Music = .8f;
        public static bool ReduceMotion;
        public static bool Tutorial = true; // Week 1's coach. Turns itself off once finished or skipped.
        public static void Load() { Volume = PlayerPrefs.GetFloat("volume", .8f); Music = PlayerPrefs.GetFloat("music", .8f); ReduceMotion = PlayerPrefs.GetInt("reduceMotion") == 1; Tutorial = PlayerPrefs.GetInt("tutorialDone") == 0; }
        public static void Save() { PlayerPrefs.SetFloat("volume", Volume); PlayerPrefs.SetFloat("music", Music); PlayerPrefs.SetInt("reduceMotion", ReduceMotion ? 1 : 0); PlayerPrefs.SetInt("tutorialDone", Tutorial ? 0 : 1); PlayerPrefs.Save(); }
    }

    public class WeekResult
    {
        public float At; // When it aired: the TV and the end cards time from here.
        public int Number, From, Cards, CashBefore, StreakBefore;
        public int Asked = -1, AskedHolders; // The street interview's square after a clean draw, and its tickets. See Screens.Asked.
        public float AskAt; // When the TV cuts to the interview.
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
        public bool Supplying { get; private set; }
        public bool Dealing => S.Overflow != null && !Live && Result == null;
        public bool Replacing => Punching >= 0 || Supplying || Dealing;
        int confirm = -1; // The hand card picked to be replaced; a second click confirms.
        public int ConfirmCard => Replacing ? confirm : -1;
        public string SpecialName => S.Today switch { Special.Payday => "PAYDAY", Special.Telethon => "TELETHON", Special.Nephew => "MINISTER'S NEPHEW", _ => null };
        public Card IncomingCard => Dealing ? S.Overflow.Value : Supplying ? S.PreviewSupplier() : S.Rack[Punching];
        public int Stall { get; private set; } = -1;
        public string Station { get; private set; } = "monitor";
        public Room Room { get; private set; }
        public Sfx Sfx { get; private set; }
        public float RolledAt = -100, LiveAt;
        public bool Drawn => Live && Time.time >= LiveAt; // The number exists, and cards can be played.
        float frozen; // Seconds the tutorial held the on-air clock.
        public float LiveSeconds => Mathf.Max(0, Time.time - LiveAt - frozen);
        public int Value => Landed.Count > 0 ? Landed[Landed.Count - 1] : S.Roll ?? 0;
        public bool Busy { get; private set; }

        // The black screen that opens a tutorial run: the premise, a line at a time. Any key or click shows the rest, then moves on.
        public static bool ShowIntro = true;
        public static readonly string[] IntroText =
        {
            "YEAR 2000.",
            "EVERYONE IS GAMBLING ON THE LOTTERY.",
            "THEY BUY A NUMBER, HOPING IT WILL CHANGE THEIR LIVES.",
            "LITTLE DO THEY KNOW, I AM THE ONE WHO ORCHESTRATES THEIR FATE.",
        };
        public bool IntroUp { get; private set; }
        float introAt;
        public int IntroShown => Prefs.ReduceMotion ? IntroText.Length : Mathf.Min(IntroText.Length, 1 + (int)((Time.unscaledTime - introAt) / 2.4f));
        public float IntroLineAge => Prefs.ReduceMotion ? 99 : (Time.unscaledTime - introAt) % 2.4f;
        void IntroAdvance() { if (IntroShown < IntroText.Length) introAt -= 100; else { IntroUp = false; GoLive(); } } // The show starts once the backstory is read. } // The week-1 banner would have run out behind the black screen.

        Screens screens;
        public Screens Screens => screens;
        Camera cam;
        public Camera Cam => cam;
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
            useGUILayout = false;
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

        void SetMode(Mode m) { Mode = m; Sel = 0; Time.timeScale = m == Mode.Paused || (m == Mode.Settings && SettingsFrom == Mode.Paused) ? 0 : 1; Refresh(); }

        public void Begin()
        {
            StartRun();
            SetMode(Mode.Play);
            IntroUp = ShowIntro && Prefs.Tutorial; // Only when the guide runs: a returning player goes straight to the desk.
            introAt = Time.unscaledTime;
            if (!IntroUp) GoLive(); // Each week opens on air; the off-air prep comes after the show, for the next week.
        }
        public void ToTitle() { StartRun(); SetMode(Mode.Title); }
        public void Pause() { if (Mode == Mode.Play && !Busy) SetMode(Mode.Paused); }
        public void Resume() { if (Mode == Mode.Paused) SetMode(Mode.Play); }
        public void OpenSettings() { SettingsFrom = Mode; SetMode(Mode.Settings); }
        public void CloseSettings() { Prefs.Save(); SetMode(SettingsFrom); }

        public void SetVolume(float v) => Prefs.Volume = Mathf.Clamp01(Mathf.Round(v * 20) / 20);
        public void SetMusic(float v) => Prefs.Music = Mathf.Clamp01(Mathf.Round(v * 20) / 20);
        public const int SettingRows = 4; // SFX volume, music volume, reduce motion, tutorial. BACK comes after.
        public const int RowY = 44, RowStep = 56; // Settings rows on the monitor. Screens.SettingsPanel draws the same layout.
        public const int BackY = RowY + SettingRows * RowStep + 8;

        // Menu buttons, top to bottom, for whichever menu is up. Empty when none is.
        System.Action[] Buttons()
        {
            if (Mode == Mode.Title) return new System.Action[] { Begin, OpenSettings, Application.Quit };
            if (Mode == Mode.Paused) return new System.Action[] { Resume, OpenSettings, ToTitle };
            if (Mode == Mode.Play && EndCardUp) return new System.Action[] { Begin, ToTitle };
            return new System.Action[0];
        }

        // Settings rows: left/right or a click changes the focused one.
        void Nudge(int dir)
        {
            if (Sel == 0) SetVolume(Prefs.Volume + dir * .1f);
            else if (Sel == 1) SetMusic(Prefs.Music + dir * .1f);
            else if (Sel == 2) Prefs.ReduceMotion = dir > 0;
            else if (Sel == 3) SetTutorial(dir > 0);
        }

        // On: shows now if it's still week 1, else from the next run. Off: gone at once.
        public void SetTutorial(bool on)
        {
            Prefs.Tutorial = on;
            if (!on) tutorial = Steps.Length;
            else if (S.Week == 1 && tutorial >= Steps.Length) tutorial = 0;
        }

        public bool EndCardUp => Result != null && S.Over != Ending.None
            && Time.time - Result.At >= (S.Over == Ending.Won ? 2 : Prefs.ReduceMotion ? 1.5f : 2.4f);

        // The HUD is drawn at 640x360 and scaled by whole numbers, centred in the window.
        public static int HudScale => Mathf.Max(1, Mathf.Min(Screen.width / 640, Screen.height / 360));
        public static UnityEngine.Rect HudRect { get { int s = HudScale; return new UnityEngine.Rect((Screen.width - 640 * s) / 2, (Screen.height - 360 * s) / 2, 640 * s, 360 * s); } }
        // The window's edges in HUD pixels. Wider or taller than 16:9 they fall outside 0..640 and 0..360; the bar, coach and buttons hug them.
        public static RectInt HudView
        {
            get
            {
                var r = HudRect; int s = HudScale, l = (int)r.x / s, t = (int)r.y / s;
                return new RectInt(-l, -t, 640 + l + (Screen.width - (int)r.xMax) / s, 360 + t + (Screen.height - (int)r.yMax) / s);
            }
        }
        public static Vector2 HudToScreen(int x, int y) { var r = HudRect; return new Vector2(r.x + x * HudScale, Screen.height - r.y - y * HudScale); }
        public static Vector2Int ScreenToHud(Vector2 at) { var r = HudRect; return new Vector2Int(Mathf.FloorToInt((at.x - r.x) / HudScale), Mathf.FloorToInt((Screen.height - at.y - r.y) / HudScale)); }

        // A pixel on the 640x360 monitor picture, through the real screen collider.
        public Vector2 MonitorToScreen(int x, int y) =>
            cam.WorldToScreenPoint(Room.MonitorScreen.transform.TransformPoint(new Vector3(x / 640f - .5f, .5f - y / 360f, 0)));
        Vector2Int MonitorPixel(Collider c, Vector3 point)
        {
            if (c != Room.MonitorScreen) return new Vector2Int(-1, -1);
            var uv = c.transform.InverseTransformPoint(point);
            return new Vector2Int(Mathf.FloorToInt((uv.x + .5f) * 640), Mathf.FloorToInt((.5f - uv.y) * 360));
        }

        public static bool In(Vector2Int p, int x, int y, int w, int h) => p.x >= x && p.x < x + w && p.y >= y && p.y < y + h;
        public static int StationIndex(string s) => s == "tv" ? 0 : s == "machine" ? 2 : 1;
        public static string StationName(int i) => i == 0 ? "tv" : i == 2 ? "machine" : "monitor";

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
            // Settings: rows every RowStep px from RowY, BACK below them. Screens.SettingsPanel draws the same layout.
            if (In(m, 352, BackY, 272, 32)) { CloseSettings(); return; }
            for (int row = 0; row < SettingRows; row++)
                if (In(m, 352, RowY + row * RowStep, 272, RowStep - 4)) Sel = row;
            if (In(m, 364, RowY + 20, 176, 32)) SetVolume((m.x - 364) / 176f);
            if (In(m, 364, RowY + RowStep + 20, 176, 32)) SetMusic((m.x - 364) / 176f);
            for (int i = 0; i < 2; i++)
                if (In(m, 364 + i * 80, RowY + 2 * RowStep + 24, 72, 24)) Prefs.ReduceMotion = i == 1;
                else if (In(m, 364 + i * 80, RowY + 3 * RowStep + 24, 72, 24)) SetTutorial(i == 1);
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

        public void StartRun() { tutorial = Prefs.Tutorial ? 0 : Steps.Length; S = new Run(); S.ShuffleDates(); Sel = 0; runId = DateTime.Now.ToString("yyyyMMdd-HHmmss"); NextWeek(); }

        // A list of numbers for the calendar: "07, 23 or 61".
        public static string Numbers(IEnumerable<int> ns) { var s = ns.Select(n => n.ToString("00")).ToList(); return s.Count < 2 ? string.Join("", s) : string.Join(", ", s.Take(s.Count - 1)) + " or " + s[s.Count - 1]; }

        // What a week on the calendar means, for the close-up's hover.
        public string WeekInfo(int w)
        {
            var d = S.Dates[w];
            string when = w < S.Week ? " (done)" : w == S.Week ? " (this week)" : "";
            string what = Run.IsInspection(w) ? $"INSPECTION DAY. An inspector is in the studio, so you can play at most {Config.InspectionCards} card on air."
                : d == Special.Payday ? "PAYDAY. Twice as many people buy tickets, doubling your ticket cash."
                : d == Special.Telethon ? $"TELETHON. Winner payouts are doubled, and announcing within {Config.Target:0}s pays +{Config.Live * Config.TelethonLive} cash."
                : d == Special.Nephew ? $"MINISTER'S NEPHEW. Land on one of his {Config.NephewNumbers} chosen numbers for +{Config.NephewReward} cash and a special card. "
                    + (S.Nephews.Count > 0 ? $"His numbers are {Numbers(S.Nephews)}." : "The minister phones in his numbers the week before.")
                : "Regular broadcast week with no special rules.";
            return $"WEEK {w}{when}: {what}";
        }

        // What a card does, for hover text.
        public static string Explain(Card c) => c.Op switch
        {
            '+' => $"Adds {c.X} to the number",
            '-' => $"Subtracts {c.X} from the number",
            '*' => $"Multiplies the number by {c.X}",
            '/' => $"Divides the number by {c.X} if exact",
            'S' => "Swaps the two digits (52 becomes 25)",
            'M' => "Flips to 99 minus the number (12 becomes 87)",
            'T' => $"Shifts the tens digit by {c.Arg}",
            'W' => "Copies tens digit to ones (47 becomes 44)",
            'R' => $"Adds {c.X}, wrapping past 99 to 00 (99 + {c.X} = {(99 + c.X) % 100:00})",
            'J' => "Redraws the ones digit at random",
            'D' => "Draws a whole new number (00 to 99)",
            _ => ""
        };

        public static string ExplainSlip(Slip s) => s switch
        {
            Slip.Sponsor => $"End on a number ending in 7 for +{Slips.Reward(s)} cash (miss: -{Slips.Fine(s)} cash).",
            Slip.Quiet => $"Play at most 1 card for +{Slips.Reward(s)} cash (miss: prize +{Config.Rollover}).",
            Slip.Rush => $"Announce within {Config.RushSeconds}s for +{Slips.Reward(s)} cash (miss: prize +{Config.Rollover}).",
            Slip.Favour => $"Let 1 to {Config.SmallMax} holders win for +{Slips.Reward(s)} cash (miss: -{Slips.Fine(s)} cash if 0 win).",
            Slip.Bulk => $"Get +{Slips.Reward(s)} cash now, adding {Config.BulkTickets} tickets across {Config.BulkFrom} to {Config.BulkFrom + 9}.",
            Slip.Collector => $"Get a clean draw (0 winners) for 1 special card (miss: prize +{Config.Rollover}).",
            _ => ""
        };

        public static string DateName(Special d) => d switch
        {
            Special.Payday => "PAYDAY",
            Special.Telethon => "TELETHON",
            Special.Nephew => "NEPHEW",
            _ => ""
        };

        void NextWeek()
        {
            Live = false; confirm = -1; Stall = -1; Shredding = false; Supplying = false; Result = null; Preview = -1; PreviewRow = -1; Punching = -1; punched = 0;
            Played.Clear();
            Landed.Clear();
            offAt = Time.time;
            S.StartWeek();
            // Each week deals one card; when the hand is full the replace prompt says so instead.
            Headline((S.Today switch { Special.Payday => $"WEEK {S.Week}: PAYDAY", Special.Telethon => $"WEEK {S.Week}: TELETHON", Special.Nephew => $"WEEK {S.Week}: NEPHEW", _ => Run.IsInspection(S.Week) ? $"WEEK {S.Week}: INSPECTION" : $"WEEK {S.Week}" }) + (S.Week > 1 && !Dealing ? " +1 CARD" : ""));
            Station = "monitor";
            Refresh();
        }

        public void TakeAction(DeskAction action)
        {
            if (Live || Result != null || S.Acted != null) { Sfx.Play("desk_denied"); return; }
            S.SkipOverflow(); confirm = -1;
            if (action == DeskAction.Shred) { Shredding = !Shredding; Supplying = false; Punching = -1; if (Shredding) Look("monitor"); Refresh(); return; } // Aim it at the board.
            if (action == DeskAction.Supplier && S.Hand.Count >= Config.HandLimit)
            {
                if (S.Cash <= Config.SupplierCost) { Sfx.Play("desk_denied"); return; }
                Supplying = !Supplying; Shredding = false; Punching = -1;
                if (Supplying) S.PreviewSupplier();
                Refresh();
                return;
            }
            if (!S.TakeAction(action)) { Sfx.Play("desk_denied"); return; }
            Headline(action == DeskAction.Bribe ? $"BRIBE: +{Config.Bribe} CASH, PRIZE +{Config.Rollover}" : $"PHONE: +1 CARD, -{Config.SupplierCost} CASH");
            Refresh();
        }

        public void SupplyCard(int hand)
        {
            if (!S.TakeAction(DeskAction.Supplier, hand)) { Sfx.Play("desk_denied"); return; }
            Headline($"PHONE: NEW CARD, -{Config.SupplierCost} CASH");
            Supplying = false;
            Refresh();
        }

        public void ShredSquare(int n)
        {
            if (!S.TakeAction(DeskAction.Shred, n)) return;
            Headline($"SHREDDED {n:00}: HOLDERS MOVED NEXT DOOR");
            Shredding = false; Preview = -1;
            Refresh();
        }

        // The card punch: pick a special in the rack, then the arithmetic card it replaces. Off air only.
        public void PickRack(int r)
        {
            if (Live || Result != null || r >= S.Rack.Count) { Sfx.Play("desk_denied"); return; }
            S.SkipOverflow(); confirm = -1;
            Punching = Punching == r ? -1 : r;
            Shredding = false; Supplying = false;
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
            S.SkipOverflow(); confirm = -1;
            S.TearSlip(); // An unread slip goes in the bin.
            S.TakeAction(DeskAction.LieLow); // No desk action: you lay low.
            S.DrawNumber();
            if (firstRun) { S.SeedRoll(); firstRun = false; }
            Live = true; Shredding = false; Supplying = false; Preview = -1; Punching = -1; frozen = 0;
            Headline($"WEEK {S.Week}: ON AIR");
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
            // A press before the cut jumps to the street interview, so it's never skipped; the next press reveals.
            if (Result != null && !Result.Revealed && Time.time < Result.AskAt && Screens.Interview(Result, S.Week) != null) { Result.AskAt = Time.time; return; }
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
            int from = S.Roll.Value, number = Value, rate = S.Prize, asked = Screens.Asked(S.Board, from, number);
            float seconds = LiveSeconds;
            string steps = string.Join(" ", best.Path.Select(i => S.Hand[i].ToString()));
            Result = new WeekResult
            {
                At = Time.time, Number = number, From = from, Asked = asked, AskedHolders = asked >= 0 ? S.Board[asked] : 0, AskAt = Time.time + Screens.InterviewDelay, Best = best, Cards = Played.Count,
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

        int lastSettingsSig;

        void Update()
        {
            if (Drawn != wasDrawn) { wasDrawn = Drawn; Refresh(); } // Cards wake up when the machine locks.
            ReadInput();
            CheckStall();
            StepTutorial();
            if (Drawn && Result == null && Tutorial != null) frozen += Time.deltaTime; // The guide never races the clock.
            int settingsSig = Mode == Mode.Settings ? System.HashCode.Combine(Sel, Prefs.Volume, Prefs.Music, Prefs.ReduceMotion, Prefs.Tutorial) : 0;
            if (settingsSig != lastSettingsSig) { lastSettingsSig = settingsSig; screens.MarkDirty(); lastDraw = -100; }
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

        // On-screen controls for mouse-only play, same actions as the keys: look left (-1), look right (1). Lean is a click on the monitor.
        // The coach narrows what is allowed, so the buttons and keys share one gate.
        public const int NavW = 24, NavH = 24, NextW = 104;
        public static Vector2Int NavAt(int n) => n < 0 ? new Vector2Int(HudView.x + 8, 200) : new Vector2Int(HudView.xMax - 8 - NavW, 200);
        public bool Leaning => lean;

        public bool NavOn(int n)
        {
            if (Mode != Mode.Play || EndCardUp) return false;
            if (Station != "tv" && Station != "monitor" && Station != "machine") return false;
            int to = StationIndex(Station) + n;
            return to >= 0 && to <= 2 && CanLook(StationName(to));
        }

        void Press(int n)
        {
            if (n == 2) { if (CanLean) lean = !lean; return; }
            int to = Mathf.Clamp(StationIndex(Station) + n, 0, 2);
            if (StationName(to) != Station && CanLook(StationName(to))) Look(StationName(to));
        }

        // 0 nothing, -1/1/2 a nav button, 3 the coach's NEXT/SKIP.
        public int HudHit(Vector2 at)
        {
            if (Mode != Mode.Play || EndCardUp) return 0;
            var p = ScreenToHud(at);
            foreach (int n in new[] { -1, 1 }) { var o = NavAt(n); if (NavOn(n) && In(p, o.x, o.y, NavW, NavH)) return n; }
            return CanSkip && screens.NextBtn.Contains(p) ? 3 : 0;
        }

        void ReadInput()
        {
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            if (IntroUp) { if (keys != null && keys.anyKey.wasPressedThisFrame) IntroAdvance(); }
            else if (keys != null && keys.escapeKey.wasPressedThisFrame)
            {
                if (Mode == Mode.Settings) CloseSettings();
                else if (Mode == Mode.Paused) Resume();
                else if (Mode == Mode.Play && confirm >= 0 && Replacing) { confirm = -1; Refresh(); }
                else if (Mode == Mode.Play && Shredding) { Shredding = false; Refresh(); }
                else if (Mode == Mode.Play && Supplying) { Supplying = false; Refresh(); }
                else if (Mode == Mode.Play && Dealing) { S.SkipOverflow(); Refresh(); }
                else if (Mode == Mode.Play && Punching >= 0) { Punching = -1; Refresh(); }
                else if (Mode == Mode.Play && (Station == "calendar" || Station == "slip")) Look("monitor");
                else Pause();
            }
            else if (keys != null && Mode == Mode.Start) { if (keys.anyKey.wasPressedThisFrame) SetMode(Mode.Title); }
            else if (keys != null && (Mode != Mode.Play || EndCardUp)) MenuKeys(keys);
            else if (keys != null)
            {
                if (keys.spaceKey.wasPressedThisFrame) Press(2);
                if (keys.aKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame) Press(-1);
                if (keys.dKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame) Press(1);
            }
            if (keys != null && keys.tabKey.wasPressedThisFrame) TutorialSkip();
            if (mouse == null) return;
            Vector2 at = mouse.position.ReadValue();
            pointer = new Vector2(at.x / Screen.width * 2 - 1, at.y / Screen.height * 2 - 1);
            string wasHover = hover;
            if (Mode == Mode.Play) Hover(at);
            if (hover != wasHover) lastDraw = -100;
            if (mouse.leftButton.wasPressedThisFrame) Click(at);
        }

        int lastPickFrame = -1;
        Vector2 lastPickAt;
        Collider lastPickCollider;
        Vector3 lastPickPoint;

        public Collider Pick(Vector2 at, out Vector3 point)
        {
            if (Time.frameCount == lastPickFrame && at == lastPickAt)
            {
                point = lastPickPoint;
                return lastPickCollider;
            }
            bool hit = Physics.Raycast(cam.ScreenPointToRay(at), out var h, 10);
            point = hit ? h.point : default;
            lastPickFrame = Time.frameCount;
            lastPickAt = at;
            lastPickPoint = point;
            lastPickCollider = hit ? h.collider : null;
            return lastPickCollider;
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
            if (c != null && Tutorial != null && !InCoach(c)) c = null; // The coach quiets everything it isn't teaching.
            int week = Station == "calendar" && c == Room.CalendarPaper ? CalendarAt(point) : 0;
            if (week != CalendarWeek) { CalendarWeek = week; Refresh(); }
            int preview = Shredding ? SquareAt(c, point) : -1;
            hover = null;
            int over = c == null ? -1 : Array.IndexOf(Room.CardColliders, c);
            int rack = c == null ? -1 : Array.IndexOf(Room.RackColliders, c);
            hoverCard = over;
            int row = -1;
            if (over >= 0 && CanPlay(over))
            {
                var k = S.Hand[over];
                if (k.Op == 'J') row = Value / 10;
                else if (!k.IsRandom) preview = Run.Apply(Value, k).Value;
            }
            if (preview != Preview || row != PreviewRow) { Preview = preview; PreviewRow = row; lastDraw = -100; screens.MarkDirty(); }
            if (week > 0) { hover = WeekInfo(week); return; }
            if (c == Room.CalendarPaper && Station != "calendar" && !Shredding) { hover = "CALENDAR: Check upcoming special weeks and inspections. Click to read up close."; return; }
            if (over >= 0 && over < S.Hand.Count && !Live && (Dealing || Supplying || Punching >= 0))
            {
                hover = Punching >= 0 && S.Hand[over].IsSpecial ? $"{S.Hand[over]}: Already a special card. Pick a number card to replace."
                    : $"REPLACE {S.Hand[over]}: Swap with {IncomingCard}" + (Supplying ? $" for {Config.SupplierCost} cash" : "") + (confirm == over ? ". Click again to confirm." : ". Click to select.");
                return;
            }
            if (over >= 0 && over < S.Hand.Count)
            {
                var k = S.Hand[over];
                hover = $"{k}: {Explain(k)}. " + (CanPlay(over) ? "Click to play it." : WhyNot());
                return;
            }
            if (rack >= 0 && rack < S.Rack.Count)
            {
                hover = $"RACK ({S.Rack[rack]}): {Explain(S.Rack[rack])}" + (Live ? ". Off air only." : ". Expires tonight if unused. Click to swap into your hand.");
                return;
            }
            if (c != null && c.transform == Room.Slip)
            {
                var slip = S.Pinned != Slip.None ? S.Pinned : S.Offer;
                int button = Station == "slip" && S.Offer != Slip.None && !Live ? SlipButton(point) : 0;
                hover = button == 1 ? $"TAKE: {ExplainSlip(slip)} Click to pin it to the monitor."
                    : button == 2 ? "TEAR: Reject this side job for free. Click to tear it up."
                    : Station == "slip" ? $"{Slips.Name(slip)}: \"{Slips.Quote(slip)}\" {ExplainSlip(slip)}"
                    : $"SLIP ({Slips.Name(slip)}): {ExplainSlip(slip)} Click to read up close.";
                return;
            }
            if (c == Room.GoButton) { hover = Drawn ? $"RED BUTTON: Announce {Value:00} (+{Config.Live} cash within {Config.Target:0}s). Click to announce." : Live ? "RED BUTTON: Announces your number on air. Wait for the machine to draw." : "RED BUTTON: Announces your number on air. Pull the GO LIVE lever first."; return; }
            if (c == Room.UndoButton) { hover = Played.Count > 0 ? "UNDO: Take back the last card played. Click to undo." : "UNDO: Takes back the last card played. No card played yet."; return; }
            if (c == Room.CalendarPaper && Shredding) { hover = "CALENDAR: Check upcoming special weeks and inspections. Finish shredding first (ESC cancels)."; return; }
            if (c == null) return;
            string ToolTail(string click, bool cancel = false, bool needCash = false) => cancel ? click
                : Live ? "Off air only."
                : S.Acted != null ? "Already used a desk tool this week."
                : needCash && S.Cash <= Config.SupplierCost ? $"Needs more than {Config.SupplierCost} cash."
                : $"One tool per week. {click}";
            if (c == Room.LiveSwitch) hover = "GO LIVE: Draw the ball and start the show" + (!Live && S.Acted == null ? $" (skip tools to lie low: prize -{Config.LieLow}, min {Config.Prize})" : "") + (Live ? ". Already on air." : ". Click to pull the lever.");
            else if (c == Room.ActionProps[DeskAction.Supplier]) hover = $"PHONE (SUPPLIER): Pay {Config.SupplierCost} cash for 1 extra card now. " + ToolTail(Supplying ? "Click to cancel." : "Click to call the supplier.", Supplying, true);
            else if (c == Room.ActionProps[DeskAction.Bribe]) hover = $"ENVELOPE (BRIBE): Take +{Config.Bribe} cash now, raising the prize by +{Config.Rollover} for the run. " + ToolTail("Click to take the bribe.");
            else if (c == Room.ActionProps[DeskAction.Shred]) hover = "SHREDDER: Clear all tickets from one square (buyers move to adjacent squares). " + ToolTail(Shredding ? "Click to cancel." : "Click to aim at the board.", Shredding);
        }

        string WhyNot() => !Live ? "Only works on air; pull the GO LIVE lever first."
            : !Drawn ? "Wait for the ball to finish rolling."
            : Played.Count >= Run.CardLimit(S.Week) ? $"Inspection week: {Run.CardLimit(S.Week)} card max on air."
            : "Cannot use on this number.";

        // Public so a test can click real props through the real raycast.
        public void Click(Vector2 at)
        {
            if (Busy) return;
            if (IntroUp) { IntroAdvance(); return; }
            if (Mode == Mode.Start) { SetMode(Mode.Title); return; }
            if (Mode != Mode.Play || EndCardUp) { ClickMenu(at); return; }
            int hud = HudHit(at);
            if (hud == 3) { TutorialSkip(); return; }
            if (hud != 0) { Press(hud); return; }
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
            if (ClickBlocked(c)) { Sfx.Play("desk_denied"); return; }
            if (c == Room.CalendarPaper && !Shredding && !Replacing) { Look("calendar"); return; }
            int card = Array.IndexOf(Room.CardColliders, c);
            if (card >= 0 && card < S.Hand.Count && Replacing && !(Punching >= 0 && S.Hand[card].IsSpecial))
            {
                if (confirm != card) { confirm = card; Refresh(); return; } // First click asks, second click does it.
                confirm = -1;
                if (Dealing) { S.ReplaceOverflow(card); Refresh(); }
                else if (Supplying) SupplyCard(card);
                else Repunch(card);
                return;
            }
            if (Station == "machine") { foreach (var kv in Room.ActionProps) if (c == kv.Value) TakeAction(kv.Key); return; } // The side table.
            if (Shredding && SquareAt(c, point) >= 0) { ShredSquare(SquareAt(c, point)); return; }
            if (c == Room.MonitorScreen) { if (!Shredding && CanLeanIn) lean = !lean; return; } // Click the monitor to lean in, again to sit back.
            if (c == Room.GoButton) { Go(); return; }
            if (c == Room.UndoButton) { Undo(); return; }
            if (c == Room.LiveSwitch) { GoLive(); return; }
            foreach (var kv in Room.ActionProps) if (c == kv.Value) { TakeAction(kv.Key); return; }
            int rack = Array.IndexOf(Room.RackColliders, c);
            if (rack >= 0) { PickRack(rack); return; }
            if (c.transform == Room.Slip) { Look("slip"); return; } // Read it up close first, like the calendar.
            if (card >= 0 && card < S.Hand.Count) PlayCard(card);
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
                bool up = Mode == Mode.Play && i == hoverCard && i < n && (CanPlay(i) || Supplying || Dealing || (Punching >= 0 && !S.Hand[i].IsSpecial));
                float d = i - (n - 1) / 2f;
                Vector3 pos, scale = Room.CardSize;
                Quaternion rot;
                if (Replacing && i == Room.CardCount - 1)
                {
                    var t = cam.transform;
                    pos = t.TransformPoint(new Vector3(0, .06f, .35f));
                    rot = t.rotation;
                    scale *= .62f;
                }
                else if (Replacing)
                {
                    var t = cam.transform;
                    pos = t.TransformPoint(new Vector3(d * .082f, -.095f + (up ? .03f : 0), .35f + i * .0005f - (up ? .006f : 0)));
                    rot = t.rotation;
                    scale *= .62f;
                }
                else if (Focus)
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

        // Tutorial sequence. A step clears on its action (Done), or by NEXT/TAB when it has none.
        // Phase is how far the run must have got (0 before the first show, 1 live, 2 result, 3 off air after it): a step can't be skipped into a phase not yet reached.
        // Targets are what the HUD brackets and the only things the player may point at or click. Looks and Lean are the camera moves allowed.
        sealed class Step
        {
            public string Text; public int Phase; public Func<Game, bool> Done; public Func<Game, IEnumerable<Collider>> Targets;
            public string[] Looks = Mon; public bool Lean, Clicks, Bar;
        }
        static readonly string[] Mon = { "monitor" }, Side = { "monitor", "machine" };
        static Collider Box(Transform t) => t.gameObject.activeInHierarchy ? t.GetComponent<Collider>() : null;
        static IEnumerable<Collider> Hand(Game g) => Enumerable.Range(0, g.S.Hand.Count).Select(i => Box(g.Room.Cards[i]));
        static IEnumerable<Collider> Props(Game g) => g.Room.ActionProps.Values;
        static readonly Step[] Steps =
        {
            new Step { Text = $"Each square shows how many people picked that number. Keep your cash above zero for {Config.Weeks} weeks to keep your job.",
                Phase = 1, Targets = g => new[] { g.Room.MonitorScreen }, Bar = true },
            new Step { Text = "A ball just rolled. Play a card to move the number onto an empty square. Press SPACE to lean in.", Phase = 1,
                Lean = true, Clicks = true, Done = g => g.Played.Count > 0 || g.Result != null, Targets = g => Enumerable.Range(0, g.S.Hand.Count).Where(g.CanPlay).Select(i => Box(g.Room.Cards[i])) },
            new Step { Text = $"Press the red button to announce the number. Finish within {Config.Target:0}s for +{Config.Live} cash, or press UNDO to take a card back.", Phase = 1,
                Lean = true, Clicks = true, Done = g => g.Result != null, Targets = g => new[] { g.Room.GoButton, g.Room.UndoButton } },
            new Step { Text = "Click through the TV broadcast and receipt to start the next week.", Phase = 2,
                Looks = new[] { "tv", "monitor" }, Clicks = true, Done = g => g.S.Week > 1, Targets = g => Enumerable.Empty<Collider>() },
            new Step { Text = "You are off air now. Press D or click > to check the side table before the next show.", Phase = 3, Looks = Side, Done = g => g.Station == "machine", Targets = Props },
            new Step { Text = "Pick one desk tool to use before the show. Hover over any item to see what it does, then click it.", Phase = 3,
                Looks = Side, Clicks = true, Done = g => g.S.Acted != null, Targets = g => Props(g).Append(g.Room.LiveSwitch) },
            new Step { Text = "The rack holds special cards. Click one, then click a card in your hand to replace it.", Phase = 3,
                Looks = Side, Clicks = true, Done = g => g.punched > 0 || g.S.Rack.Count == 0,
                Targets = g => (g.Punching < 0 ? g.Room.Rack.Take(g.S.Rack.Count).Select(Box)
                    : Enumerable.Range(0, g.S.Hand.Count).Where(i => !g.S.Hand[i].IsSpecial).Select(i => Box(g.Room.Cards[i]))) },
            new Step { Text = "When you are ready, pull the GO LIVE lever to start the next draw.", Phase = 3,
                Looks = Side, Clicks = true, Done = g => g.Live, Targets = g => new[] { g.Room.LiveSwitch } },
        };
        public static int TutorialSteps => Steps.Length;
        int tutorial; // Set from Prefs.Tutorial in StartRun.
        public int TutorialStep => tutorial; // Counts from 0. TutorialSteps once done.
        public string Tutorial => Mode == Mode.Play && tutorial < Steps.Length ? Steps[tutorial].Text : null;
        public bool TutorialBar => Tutorial != null && Steps[tutorial].Bar;
        public bool TutorialInfo => Tutorial != null && Steps[tutorial].Done == null;
        public IEnumerable<Collider> TutorialTargets() => Tutorial == null ? Enumerable.Empty<Collider>() : Steps[tutorial].Targets(this).Where(c => c != null);

        int Phase => Result != null ? 2 : Live ? 1 : S.Week > 1 ? 3 : 0;
        // TAB or the SKIP button moves on one step, never into a phase the week hasn't reached (it would strand the player).
        public bool CanSkip => Tutorial != null && (tutorial + 1 >= Steps.Length || Steps[tutorial + 1].Phase <= Phase);
        public void TutorialSkip() { if (CanSkip) tutorial++; }
        public bool CanLook(string station) => Tutorial == null || Array.IndexOf(Steps[tutorial].Looks, station) >= 0;
        public bool CanLean => Tutorial == null || Steps[tutorial].Lean;
        // A prop is fair game when the step points at it, or an action it started (aiming, replacing) is waiting on the board and hand.
        bool InCoach(Collider c) => Shredding || Replacing || TutorialTargets().Contains(c);
        public bool ClickBlocked(Collider c) => Tutorial != null && !(c == Room.MonitorScreen && CanLeanIn) && !(Steps[tutorial].Clicks && InCoach(c));
        public bool CanLeanIn => Station == "monitor" && Result == null && CanLean;

        Vector2 lastHudPointer;
        Vector3 lastHudCamPos;
        int lastHudStep = -1;
        float lastHudTime;

        void StepTutorial()
        {
            if (tutorial >= Steps.Length) return;
            // Going live ends any off-air step still up: its action can't happen on air, so it would hold the coach (and the clicks) forever.
            while (tutorial < Steps.Length && (Steps[tutorial].Phase < Phase || (Live && Steps[tutorial].Phase == 3) || Steps[tutorial].Done?.Invoke(this) == true)) tutorial++;
            if (tutorial == Steps.Length) { Prefs.Tutorial = false; Prefs.Save(); }
            if (Tutorial != null)
            {
                if (!CanLook(Station)) Look(Steps[tutorial].Looks[0]); // Stranded off the allowed views (skipped ahead, or a calendar left open).
                if (!CanLean) lean = false;
                bool moved = (pointer - lastHudPointer).sqrMagnitude > 0.0001f || (cam.transform.position - lastHudCamPos).sqrMagnitude > 0.00001f;
                if (tutorial != lastHudStep || (moved && Time.unscaledTime - lastHudTime > 0.033f))
                {
                    screens.DrawHud();
                    lastHudPointer = pointer;
                    lastHudCamPos = cam.transform.position;
                    lastHudStep = tutorial;
                    lastHudTime = Time.unscaledTime;
                }
            }
        }

        // The footnote: real type on a solid plate, since the 8x8 pixel font is too small to read over the 3D desk.
        // Each hover line is "TITLE: what it does. Click to ...", so the plate shows it as a title tab, a body, and an action row.
        // Built-in font only: WebGL has no OS fonts, and an OS font there draws nothing.
        static GUIStyle noteBody, noteTab, noteChip;
        static readonly Color NotePlate = new Color32(0x0E, 0x0E, 0x12, 0xF5), NoteEdge = new Color32(0x4A, 0x4A, 0x50, 0xFF),
            NoteInk = new Color32(0xF2, 0xF2, 0xE6, 0xFF), NoteDim = new Color32(0xB4, 0xB4, 0xBC, 0xFF), NoteDark = new Color32(0x11, 0x11, 0x11, 0xFF),
            NoteGold = new Color32(0xFF, 0xD2, 0x3A, 0xFF), NoteRed = new Color32(0xFF, 0x3B, 0x2F, 0xFF), NoteGrey = new Color32(0x8A, 0x8A, 0x90, 0xFF);
        string noteText; // The last note drawn, kept so it can fade out after the pointer leaves.
        float noteIn, noteOut = -1;
        Pointer.Kind noteKind;
        Note split; string splitText; bool splitBlocked;

        public struct Note { public string Title, Body, Action; }

        // Splits a hover line for the plate. The title is an all-caps lead-in before the first ": " (brackets may hold lower case).
        public static Note SplitNote(string text, bool blocked)
        {
            var n = new Note { Body = text.Trim() };
            int colon = n.Body.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0 && colon <= 28)
            {
                string lead = n.Body.Substring(0, colon), bare = System.Text.RegularExpressions.Regex.Replace(lead, @"\(.*?\)", "");
                if (!bare.Any(char.IsLower) && !lead.Contains(". ")) { n.Title = lead.ToUpperInvariant(); n.Body = n.Body.Substring(colon + 2).Trim(); }
            }
            if (n.Body.Length > 0) n.Body = char.ToUpperInvariant(n.Body[0]) + n.Body.Substring(1);
            int mid = n.Body.IndexOf(". Click ", StringComparison.Ordinal);
            int click = n.Body.StartsWith("Click ", StringComparison.Ordinal) ? 0 : mid >= 0 ? mid + 2 : -1;
            if (click >= 0 && !blocked) { n.Action = n.Body.Substring(click + 6); n.Body = n.Body.Substring(0, click).Trim(); }
            else if (blocked)
            {
                int last = n.Body.TrimEnd('.').LastIndexOf(". ", StringComparison.Ordinal);
                if (last > 0) { n.Action = n.Body.Substring(last + 2); n.Body = n.Body.Substring(0, last + 1); }
            }
            return n;
        }

        static void Fill(UnityEngine.Rect r, Color c, float a) { GUI.color = new Color(c.r, c.g, c.b, c.a * a); GUI.DrawTexture(r, Texture2D.whiteTexture); }
        static UnityEngine.Rect Snap(float x, float y, float w, float h) => new UnityEngine.Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Round(w), Mathf.Round(h));

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (IntroUp) { GUI.color = Color.black; GUI.DrawTexture(new UnityEngine.Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white; }
            var hud = screens.Hud; var hr = HudRect; int hs = HudScale;
            GUI.DrawTexture(new UnityEngine.Rect(hr.x - hud.Ox * hs, hr.y - hud.Oy * hs, hud.W * hs, hud.H * hs), hud.Tex);
            if (Mode != Mode.Play || Result != null) { noteText = null; return; }
            DrawNote();
            GUI.color = Color.white;
        }

        void DrawNote()
        {
            float now = Time.unscaledTime;
            if (!string.IsNullOrEmpty(hover))
            {
                if (noteText == null) noteIn = now; // Slides in from nothing; moving between props swaps in place.
                noteText = hover; noteOut = -1; noteKind = Pointer.Current;
            }
            else if (noteText != null && noteOut < 0) noteOut = now;
            if (noteText == null) return;
            const float inTime = .14f, outTime = .09f;
            float a = noteOut >= 0 ? 1 - Mathf.Max(0, now - noteOut - .05f) / outTime : 1; // A short grace hides raycast jitter on prop edges.
            if (a <= 0) { noteText = null; noteOut = -1; return; }
            float rise = Prefs.ReduceMotion ? 1 : Mathf.Clamp01((now - noteIn) / inTime);
            rise = 1 - (1 - rise) * (1 - rise) * (1 - rise); // Ease out.
            if (!Prefs.ReduceMotion) a *= rise;

            int s = HudScale, font = Mathf.Max(13, 7 * s), u = Mathf.Max(2, s);
            noteBody ??= new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, padding = new RectOffset(), margin = new RectOffset() };
            noteTab ??= new GUIStyle(noteBody) { wordWrap = false, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            noteChip ??= new GUIStyle(noteTab) { alignment = TextAnchor.MiddleCenter };
            noteBody.fontSize = font; noteTab.fontSize = font - 1; noteChip.fontSize = Mathf.Max(10, font - 3);

            bool blocked = noteKind == Pointer.Kind.No;
            if (noteText != splitText || blocked != splitBlocked) { split = SplitNote(noteText, blocked); splitText = noteText; splitBlocked = blocked; }
            var n = split;
            Color accent = blocked ? NoteRed : noteKind == Pointer.Kind.Hand ? NoteGold : NoteGrey;
            string chip = n.Action == null ? null : blocked ? "NOT NOW" : "CLICK";

            float pad = 5 * u, bar = 2 * u, gap = 3 * u;
            float maxW = Mathf.Min(500 * s, Screen.width - 16 * s); // Ends before the ESC hint at HUD x 520.
            float chipW = chip == null ? 0 : noteChip.CalcSize(new GUIContent(chip)).x + 4 * u, chipH = noteChip.fontSize + 3 * u;
            float tabW = n.Title == null ? 0 : noteTab.CalcSize(new GUIContent(n.Title)).x + 2 * pad;
            // Shrink-wrap to the text so a short note is a small plate, a long one wraps at maxW.
            float want = Mathf.Max(noteBody.CalcSize(new GUIContent(n.Body)).x,
                n.Action == null ? 0 : chipW + gap + noteBody.CalcSize(new GUIContent(n.Action)).x);
            float w = Mathf.Clamp(want + 2 * pad + bar + 1, Mathf.Max(tabW + 8 * u, 64 * s), maxW), inner = w - 2 * pad - bar;
            float bodyH = n.Body.Length > 0 ? noteBody.CalcHeight(new GUIContent(n.Body), inner) : 0;
            float actH = n.Action == null ? 0 : Mathf.Max(chipH, noteBody.CalcHeight(new GUIContent(n.Action), inner - chipW - gap));
            float rule = n.Action != null && bodyH > 0 ? 2 * gap + u / 2 : 0;
            float h = bodyH + rule + actH + 2 * pad, tabH = n.Title == null ? 0 : noteTab.fontSize + 4 * u;
            float x = 8 * s, y = Screen.height - h - 8 * s + (1 - rise) * 6 * s;

            // Hard pixel drop shadow, plate, 1px edge, accent spine.
            if (tabH > 0) Fill(Snap(x + u, y - tabH + u, tabW, tabH), Color.black, .45f * a);
            Fill(Snap(x + u, y + u, w, h), Color.black, .45f * a);
            Fill(Snap(x, y, w, h), NotePlate, a);
            Fill(Snap(x, y, w, 1), NoteEdge, a); Fill(Snap(x, y + h - 1, w, 1), NoteEdge, a); Fill(Snap(x + w - 1, y, 1, h), NoteEdge, a);
            Fill(Snap(x, y, bar, h), accent, a);

            // Title tab: sits on the plate like a folder tab, in the tone of the cursor.
            if (tabH > 0)
            {
                Fill(Snap(x, y - tabH, tabW, tabH + 1), accent, a); // +1 covers the plate's top edge, so tab and spine read as one piece.
                GUI.color = new Color(1, 1, 1, a);
                noteTab.normal.textColor = NoteDark;
                GUI.Label(Snap(x + pad, y - tabH, tabW - pad, tabH), n.Title, noteTab);
            }

            float cx = x + bar + pad, cy = y + pad;
            GUI.color = new Color(1, 1, 1, a);
            if (bodyH > 0)
            {
                noteBody.normal.textColor = NoteInk;
                GUI.Label(Snap(cx, cy, inner, bodyH), n.Body, noteBody);
                cy += bodyH;
            }
            if (n.Action == null) return;
            if (rule > 0) { Fill(Snap(cx, cy + gap, inner, 1), NoteEdge, a); cy += rule; }
            // Action row: a key-cap chip, then what the click does (or why it can't).
            float chipY = cy + Mathf.Max(0, (Mathf.Min(actH, font + 4 * u) - chipH) / 2);
            Fill(Snap(cx, chipY, chipW, chipH), accent, a);
            GUI.color = new Color(1, 1, 1, a);
            noteChip.normal.textColor = NoteDark;
            GUI.Label(Snap(cx, chipY, chipW, chipH), chip, noteChip);
            noteBody.normal.textColor = blocked ? NoteRed : NoteDim;
            GUI.Label(Snap(cx + chipW + gap, cy, inner - chipW - gap, actH), n.Action, noteBody);
        }
    }
}
