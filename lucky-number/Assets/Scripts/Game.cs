using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NoWinners
{
    public class WeekResult
    {
        public int Number, From, Cards;
        public string Route, Line;
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
            new[] { "A JOKE FROM THE HOST", "HOST: \"Why did the ball cross the drum? ...We're still checking.\"" },
            new[] { "A WORD FROM OUR SPONSOR", "SPONSOR: \"This delay is brought to you by Sunny Gravy.\"" },
            new[] { "WE ASK THE AUDIENCE", "AUDIENCE: \"Are we on? What did I buy? Who is this?\"" },
            new[] { "TECHNICAL DIFFICULTIES", "STUDIO: \"Someone find the number! Anyone! Cut to the fish tank!\"" },
        };
        public static string StallTitle(int i) => Stalls[i % Stalls.Length][0];
        static readonly Vector3 Eye = new Vector3(0, 1.32f, 1.92f), LeanEye = new Vector3(0, 1.06f, 2.4f);
        static readonly Vector3 LookMonitor = new Vector3(0, .92f, 3), LookLean = new Vector3(0, 1.06f, 2.945f), LookTv = new Vector3(-3.85f, 1.85f, .8f), LookMachine = new Vector3(3.1f, 1.1f, 1.4f);
        static bool firstRun = true; // Survives a restart, so only the very first week is seeded.

        // State the screens read.
        public Run S { get; private set; }
        public readonly List<int> Played = new List<int>();
        public WeekResult Result { get; private set; }
        public int Preview { get; private set; } = -1;
        public bool Live { get; private set; }
        public bool Shredding { get; private set; }
        public int Stall { get; private set; } = -1;
        public string Station { get; private set; } = "monitor";
        public Room Room { get; private set; }
        public string Notice = "";
        public float RolledAt = -100, LiveAt;
        public bool Drawn => Live && Time.time >= LiveAt; // The number exists, and cards can be played.
        public float LiveSeconds => Mathf.Max(0, Time.time - LiveAt);
        public int Value => Played.Aggregate(S.Roll ?? 0, (v, i) => Run.Apply(v, S.Hand[i]).Value);
        public bool Busy { get; private set; }

        Screens screens;
        Camera cam;
        Vector3 camPos = Eye, camLook = LookMonitor;
        Vector2 pointer;
        bool lean, wasDrawn;
        float offAt, lastDraw = -100, pressedAt = -100;
        string hover;
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
            StartRun();
        }

        // ---- The week ----
        public void StartRun() { S = new Run(); runId = DateTime.Now.ToString("yyyyMMdd-HHmmss"); NextWeek(); }

        void NextWeek()
        {
            Notice = "";
            Live = false; Stall = -1; Shredding = false; Result = null; Preview = -1;
            Played.Clear();
            offAt = Time.time;
            S.StartWeek();
            if (S.Week == 2) Notice = "NOTE ON DESK: \"Off air you get one favour a week. Supplier, bribe or shredder, in front of the monitor.\"";
            if (S.Week == Config.Inspections[0] - 1) Notice = "PHONE: \"Inspectors next week. One card, no more. It's on the calendar.\"";
            Station = "monitor";
            Refresh();
        }

        public void TakeAction(DeskAction action)
        {
            if (Live || Result != null || S.Acted != null) return;
            if (action == DeskAction.Shred) { Shredding = !Shredding; Refresh(); return; }
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
            if (Busy) return;
            if (Result != null && Station != "monitor" && (RevealWaiting || S.Over != Ending.None))
            {
                Result.Revealed = true;
                Station = "monitor";
                Refresh();
                return;
            }
            if (S.Over != Ending.None) { StartRun(); return; }
            if (Result != null) { NextWeek(); return; }
            pressedAt = Time.time;
            if (!Drawn) return;
            Air();
        }

        public bool RevealWaiting => Result != null && Result.Air.Holders > 0 && !Result.Revealed;

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
                Number = number, From = from, Best = best, Cards = Played.Count,
                Route = string.Join(" ", Played.Select(i => S.Hand[i].ToString())),
                Line = steps.Length > 0 ? $"{from:00} {steps} = {best.To:00}" : $"{from:00} AS DRAWN",
                Air = S.Announce(number, Played.Count, seconds <= Config.Target),
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
            float over = LiveSeconds - Config.Target;
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
            if (Time.time - lastDraw > .1f) { screens.DrawTimed(); lastDraw = Time.time; }
            AnimateMachine();
            Room.GoCap.localPosition = new Vector3(.62f, Time.time - pressedAt < .2f ? .78f : .795f, 2.8f);
            Room.Lever.localEulerAngles = new Vector3(Live ? -34 : 34, 0, 0);
            MoveCamera();
        }

        public void Look(string station) => Station = station;

        void ReadInput()
        {
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.escapeKey.wasPressedThisFrame && Shredding) { Shredding = false; Refresh(); }
                if (keys.spaceKey.wasPressedThisFrame) lean = !lean;
                int look = Station == "tv" ? 0 : Station == "monitor" ? 1 : 2;
                if (keys.aKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame) look = Mathf.Max(0, look - 1);
                if (keys.dKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame) look = Mathf.Min(2, look + 1);
                Look(look == 0 ? "tv" : look == 1 ? "monitor" : "machine");
            }
            if (mouse == null) return;
            Vector2 at = mouse.position.ReadValue();
            pointer = new Vector2(at.x / Screen.width * 2 - 1, at.y / Screen.height * 2 - 1);
            Hover(at);
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
            if (c != Room.MonitorScreen) return -1;
            var uv = c.transform.InverseTransformPoint(point);
            int col = Mathf.FloorToInt(((uv.x + .5f) * 640 - 16) / Screens.Cell), row = Mathf.FloorToInt(((.5f - uv.y) * 360 - 24) / Screens.Cell);
            return col >= 0 && col < 10 && row >= 0 && row < 10 ? row * 10 + col : -1;
        }

        void Hover(Vector2 at)
        {
            Collider c = null;
            var point = default(Vector3);
            if (Station == "monitor" && Result == null) c = Pick(at, out point);
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
            if (Result != null) { Go(); return; }
            if (Station != "monitor") return;
            var c = Pick(at, out var point);
            if (c == null) return;
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
            float ease = 1 - Mathf.Exp(-Mathf.Min(Time.deltaTime, .05f) * 5);
            bool leaning = lean && Station == "monitor";
            Vector3 look = leaning ? LookLean : Station == "tv" ? LookTv : Station == "machine" ? LookMachine : LookMonitor;
            camPos = Vector3.Lerp(camPos, leaning ? LeanEye : Eye, ease);
            camLook = Vector3.Lerp(camLook, look, ease);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Station == "tv" ? 26 : 50, ease);
            cam.transform.position = camPos + new Vector3(pointer.x * .03f, pointer.y * .015f, 0);
            cam.transform.LookAt(camLook + new Vector3(pointer.x * .05f, pointer.y * .03f, 0));
        }

        string Hint()
        {
            if (Result != null) return $"{Notice} {(RevealWaiting ? "Click to see what you could have done." : S.Over != Ending.None ? "Click to play again." : "Click anywhere to continue.")}".Trim();
            if (Shredding) return "Click a square on the monitor to shred it. Click the shredder to cancel.";
            if (hover != null) return hover;
            if (Notice.Length > 0) return Notice;
            return Live ? "On air: rig the number with your cards, then press the red button." : "Off air, no clock. Take one desk action if you want, then flip GO LIVE.";
        }

        // ponytail: plain IMGUI line for notes and stalls. Replace with notes on the desk at the Comedy milestone.
        GUIStyle hintStyle;
        void OnGUI()
        {
            hintStyle ??= new GUIStyle(GUI.skin.box) { fontSize = Mathf.Max(14, Screen.height / 45), wordWrap = true };
            float w = Screen.width * .8f, h = hintStyle.CalcHeight(new GUIContent(Hint()), w);
            GUI.Box(new UnityEngine.Rect((Screen.width - w) / 2, Screen.height - h - 8, w, h), Hint(), hintStyle);
        }
    }
}
