using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NoWinners.Tests
{
    // Plays a week through real raycasts on the real props, and saves frames to Logs/Shots to look at.
    public class DeskPlayTests
    {
        Game game;
        Camera cam;

        static void Shot(Camera cam, string name)
        {
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0);
            RenderTexture.active = null;
            Directory.CreateDirectory("Logs/Shots");
            File.WriteAllBytes($"Logs/Shots/{name}.png", tex.EncodeToPNG());
            Object.Destroy(rt);
        }

        Vector2 At(Component c) => cam.WorldToScreenPoint(c.GetComponent<Collider>().bounds.center);

        IEnumerator Settle(float seconds = 1.2f)
        {
            yield return new WaitForSeconds(seconds);
        }

        [SetUp]
        public void Quiet_start()
        {
            Game.ShowIntro = false;
            PlayerPrefs.SetInt("tutorialDone", 1); // The coach limits clicks and pauses the clock: tests that aren't about it switch it off.
        }

        [UnityTest]
        public IEnumerator The_tutorial_row_turns_the_coach_off_and_on()
        {
            Game.KeepLog = false;
            yield return SceneManager.LoadSceneAsync("Desk");
            yield return null;
            game = Object.FindAnyObjectByType<Game>();
            bool was = Prefs.Tutorial;
            game.Click(Vector2.zero); // Start card to the title menu.
            game.OpenSettings();
            yield return new WaitForSeconds(1.2f); // The camera leans in to the monitor.
            game.Click(game.MonitorToScreen(364 + 36, Game.RowY + 3 * Game.RowStep + 36)); // OFF
            Assert.IsFalse(Prefs.Tutorial);
            Assert.AreEqual(Game.TutorialSteps, game.TutorialStep, "off hides it at once");
            game.Click(game.MonitorToScreen(364 + 80 + 36, Game.RowY + 3 * Game.RowStep + 36)); // ON
            Assert.IsTrue(Prefs.Tutorial);
            Assert.AreEqual(0, game.TutorialStep, "on in week 1 starts it over");
            yield return new WaitForSecondsRealtime(.2f); // The monitor redraws on a 0.1 s timer.
            Directory.CreateDirectory("Logs/Shots");
            File.WriteAllBytes("Logs/Shots/settings-tutorial.png", game.Room.Monitor.Tex.EncodeToPNG());
            Prefs.Tutorial = was; // Nothing here calls Save.
        }

        [UnityTest]
        public IEnumerator A_week_plays_through_the_props()
        {
            Game.KeepLog = false;
            int hadTutorial = PlayerPrefs.GetInt("tutorialDone"); // Restored below: don't eat the developer's own tutorial.
            PlayerPrefs.DeleteKey("tutorialDone");
            yield return SceneManager.LoadSceneAsync("Desk");
            yield return null;
            game = Object.FindAnyObjectByType<Game>();
            cam = Camera.main;
            yield return Settle();
            LogAssert.NoUnexpectedReceived();

            // Start: the TV title card. A click goes to the menu on the monitor.
            Assert.AreEqual(Mode.Start, game.Mode);
            Shot(cam, "0-start");
            game.Click(Vector2.zero);
            Assert.AreEqual(Mode.Title, game.Mode);
            yield return Settle();

            // Title: the menu is on the monitor, START focused. Click it through the real screen collider.
            Assert.AreEqual(Room.Hex("#FFC93C"), (Color32)game.Room.Monitor.Tex.GetPixel(356, 359 - 200), "START is the focused button");
            Shot(cam, "0-title");
            game.Click(game.MonitorToScreen(488, 200));
            Assert.AreEqual(Mode.Play, game.Mode);
            Assert.AreEqual(1, game.S.Week);
            Assert.IsTrue(game.Live, "a run opens on air: the show first, the prep after");
            int roll = game.S.Roll.Value;
            yield return Settle(Game.LockSeconds + .2f);
            Assert.IsTrue(game.Drawn);
            Shot(cam, "1-on-air");
            StringAssert.StartsWith("This is your monitor", game.Tutorial, "the tutorial starts on a first run");
            Assert.IsFalse(game.CanLook("machine"), "the coach keeps the camera on the step");
            Assert.IsFalse(game.CanLean);
            for (int i = 0; i < 5; i++) game.TutorialSkip(); // TAB: one step each. Monitor, cards, bar, calendar, clock.
            StringAssert.StartsWith("The number is out", game.Tutorial);

            // On air: seeded first roll, so one card reaches a free square.
            int playable = Enumerable.Range(0, game.S.Hand.Count).First(game.CanPlay);
            Assert.AreEqual(Room.Hex("#D9C79A"), (Color32)game.Room.CardFaces[playable].Tex.GetPixel(127, 0), "playable cards light up when the machine locks");
            Shot(cam, "2-on-air");
            var tv = game.Room.Tv.Tex;
            File.WriteAllBytes("Logs/Shots/2-tv-live.png", tv.EncodeToPNG());
            Assert.AreEqual(Room.Hex("#FFF3D6"), (Color32)tv.GetPixel(300, 359 - 160), "on air, the TV stage shows the mystery ball");

            int card = Enumerable.Range(0, game.S.Hand.Count).First(i => game.CanPlay(i) && game.S.Board[Run.Apply(roll, game.S.Hand[i]).Value] == 0);
            int to = Run.Apply(roll, game.S.Hand[card]).Value;
            game.Click(At(game.Room.Cards[card]));
            Assert.IsTrue(game.Busy);
            yield return Settle(Game.ReaderSeconds + .2f);
            Assert.IsFalse(game.Busy);
            Assert.AreEqual(to, game.Value);
            yield return null;
            Shot(cam, "3-card-played");
            StringAssert.StartsWith("Happy with the number", game.Tutorial);
            game.Click(At(game.Room.GoButton));
            Assert.AreEqual(Tier.Clean, game.Result.Air.Tier);
            Assert.AreEqual(1, game.S.Streak);
            Assert.AreEqual("tv", game.Station);
            yield return Settle();
            Assert.AreEqual(Room.Hex("#FFF3D6"), (Color32)tv.GetPixel(300, 359 - 160), "the result ball shows the number");
            Shot(cam, "4-tv-clean");

            // A click before the cut jumps to the street interview, so it's never missed.
            game.Click(Vector2.zero);
            Assert.AreEqual(("tv", false), (game.Station, game.Result.Revealed));
            Assert.IsNotNull(Screens.Interview(game.Result, game.S.Week));
            yield return null;
            Assert.AreEqual(Room.Hex("#FFC93C"), (Color32)tv.GetPixel(300, 359 - 240), "the interview's name band");
            Shot(cam, "4-tv-interview");

            // The board and the week's receipt, then off air to prepare week 2.
            game.Click(Vector2.zero);
            Assert.AreEqual("monitor", game.Station);
            yield return Settle(.3f);
            game.Click(Vector2.zero);
            Assert.AreEqual((2, false), (game.S.Week, game.Live), "after the show comes the prep for next week");
            yield return null;
            StringAssert.StartsWith("Next, the side table", game.Tutorial);
            Assert.IsTrue(game.CanLook("machine"));
            Assert.IsTrue(game.NavOn(1), "the > button is up for mouse-only play");

            // Off air: number hidden, cards refuse, one desk action.
            Assert.IsNull(game.S.Roll);
            game.Click(At(game.Room.Cards[0]));
            yield return null;
            Assert.IsFalse(game.Busy, "cards wait for on air");
            int cash = game.S.Cash;
            game.Look("machine"); // The desk actions are on the side table.
            yield return Settle();
            Shot(cam, "1b-side-table");
            StringAssert.StartsWith("You can use one helper", game.Tutorial);
            CollectionAssert.Contains(game.TutorialTargets().ToList(), game.Room.ActionProps[DeskAction.Bribe], "the tutorial brackets the props it's about");
            File.WriteAllBytes("Logs/Shots/1b-tutorial-hud.png", game.Screens.Hud.Tex.EncodeToPNG());
            game.Click(At(game.Room.ActionProps[DeskAction.Bribe]));
            Assert.AreEqual(cash + Config.Bribe, game.S.Cash);
            Assert.AreEqual(DeskAction.Bribe, game.S.Acted);
            yield return null;
            StringAssert.StartsWith("These are special cards", game.Tutorial);
            Assert.IsTrue(game.ClickBlocked(game.Room.ActionProps[DeskAction.Shred]), "the coach only lets you touch what it teaches");
            Assert.IsTrue(game.ClickBlocked(game.Room.LiveSwitch), "the special-cards step points at the rack, not the lever");
            Assert.IsTrue(game.CanSkip, "a step can be skipped inside the same phase");
            game.SetTutorial(false); // The rest of the run is the props, not the coach.
            Assert.IsNull(game.Tutorial, "the tutorial was switched off");
            if (hadTutorial == 0) PlayerPrefs.DeleteKey("tutorialDone");

            // Shredder aims at a real square through the monitor.
            game.S.Acted = null;
            int held = Enumerable.Range(0, 100).First(n => game.S.Board[n] > 0);
            game.Click(At(game.Room.ActionProps[DeskAction.Shred]));
            Assert.IsTrue(game.Shredding);
            Assert.AreEqual("monitor", game.Station, "shredding turns you to the board");
            yield return Settle();
            var screen = game.Room.MonitorScreen.transform;
            var world = screen.TransformPoint(new Vector3((16 + held % 10 * 32 + 15) / 640f - .5f, .5f - (24 + held / 10 * 32 + 15) / 360f, 0));
            game.Click(cam.WorldToScreenPoint(world));
            Assert.AreEqual(0, game.S.Board[held], $"shred clears square {held}");

            // Week 2 on air: undo and replay, then a forced scandal goes TV, then reveal on the monitor, then week 3's prep.
            game.Click(At(game.Room.LiveSwitch));
            Assert.IsTrue(game.Live);
            roll = game.S.Roll.Value;
            yield return Settle(Game.LockSeconds + .2f);
            card = Enumerable.Range(0, game.S.Hand.Count).FirstOrDefault(game.CanPlay);
            if (game.CanPlay(card))
            {
                game.Click(At(game.Room.Cards[card]));
                yield return Settle(Game.ReaderSeconds + .2f);
                game.Click(At(game.Room.UndoButton));
                Assert.AreEqual(roll, game.Value, "UNDO takes the card back");
                yield return null;
                game.Click(At(game.Room.Cards[card]));
                yield return Settle(Game.ReaderSeconds + .2f);
            }
            for (int n = 0; n < 100; n++) game.S.Board[n] = 4;
            game.S.Cash = 50000;
            game.Click(At(game.Room.GoButton));
            Assert.AreEqual(Tier.Scandal, game.Result.Air.Tier);
            yield return Settle();
            Shot(cam, "5-tv-scandal");
            game.Click(Vector2.zero); // To the interview: a winner.
            game.Click(Vector2.zero);
            Assert.AreEqual("monitor", game.Station);
            Assert.IsTrue(game.Result.Revealed);
            yield return Settle();
            Shot(cam, "6-reveal");
            game.Click(Vector2.zero);
            Assert.AreEqual(3, game.S.Week);

            // Past the target: the show stalls, and nothing is forced.
            game.Click(At(game.Room.LiveSwitch));
            game.LiveAt = Time.time - Config.Target - 1;
            yield return null;
            yield return null;
            Assert.AreEqual(0, game.Stall);
            Assert.IsNull(game.Result);

            // Bankruptcy reaches the end screen.
            for (int n = 0; n < 100; n++) game.S.Board[n] = 50;
            game.S.Cash = 10;
            game.LiveAt = Time.time - 1;
            game.Click(At(game.Room.GoButton));
            Assert.AreEqual(Ending.Lost, game.S.Over);
            Assert.IsFalse(game.EndCardUp, "the TV collapses before the card");
            game.Click(Vector2.zero);
            Assert.AreEqual(Ending.Lost, game.S.Over, "a stray click doesn't skip the ending");
            yield return Settle(2.6f);
            Assert.IsTrue(game.EndCardUp);
            Assert.AreEqual(Room.Hex("#23093A"), (Color32)tv.GetPixel(20, 359 - 20), "the TV is on STAND BY");
            Shot(cam, "7-bankrupt");
            File.WriteAllBytes("Logs/Shots/7-hud.png", game.Screens.Hud.Tex.EncodeToPNG());
            File.WriteAllBytes("Logs/Shots/7-tv.png", tv.EncodeToPNG());
            game.Click(Game.HudToScreen(320, 248));
            Assert.AreEqual((1, Ending.None, true), (game.S.Week, game.S.Over, game.Live), "play again starts a new run, on air");

            // Through week 1's show to week 2's prep, so the lever is usable below.
            yield return Settle(Game.LockSeconds + .2f);
            game.S.Cash = 50000;
            game.Go(); game.Go(); game.Go(); game.Go(); // Announce, interview, reveal, next week.
            Assert.AreEqual((2, false), (game.S.Week, game.Live));

            // The look at the machine.
            game.Look("machine");
            yield return Settle();
            Shot(cam, "8-machine");
            game.Look("monitor");
            yield return Settle();

            // Real device input: hover, click and keys through the Input System.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            var keys = InputSystem.AddDevice<Keyboard>();
            IEnumerator Mouse(Vector2 at, bool down) { InputSystem.QueueStateEvent(mouse, new MouseState { position = at, buttons = (ushort)(down ? 1 : 0) }); yield return null; yield return null; }
            Vector2 live = cam.WorldToScreenPoint(game.Room.LiveSwitch.bounds.center);
            yield return Mouse(live, false);
            live = cam.WorldToScreenPoint(game.Room.LiveSwitch.bounds.center); // The camera sways with the pointer: aim again.
            yield return Mouse(live, false);
            Assert.AreEqual((Pointer.Kind.Hand, "GO LIVE"), (Pointer.Current, Pointer.Label), "a usable prop gets the hand and its name");
            yield return Mouse(cam.WorldToScreenPoint(game.Room.LiveSwitch.bounds.center), true);
            yield return Mouse(cam.WorldToScreenPoint(game.Room.LiveSwitch.bounds.center), false);
            Assert.IsTrue(game.Live, "a real mouse click flips GO LIVE");
            yield return Mouse(cam.WorldToScreenPoint(game.Room.LiveSwitch.bounds.center), false);
            Assert.AreEqual((Pointer.Kind.No, "GO LIVE: ALREADY ON AIR"), (Pointer.Current, Pointer.Label), "an unusable prop says why");
            yield return Settle(Game.LockSeconds + .2f);
            int hoverCard = Enumerable.Range(0, game.S.Hand.Count).FirstOrDefault(game.CanPlay);
            if (game.CanPlay(hoverCard))
            {
                yield return Mouse(cam.WorldToScreenPoint(game.Room.Cards[hoverCard].GetComponent<Collider>().bounds.center), false);
                Assert.AreEqual(Run.Apply(game.Value, game.S.Hand[hoverCard]).Value, game.Preview, "hover previews where the card lands");
            }
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.A));
            yield return null; yield return null;
            Assert.AreEqual("tv", game.Station, "A looks left");

            // Esc pauses and stops the clock; Esc again resumes.
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            Assert.AreEqual((Mode.Paused, 0f), (game.Mode, Time.timeScale));
            yield return new WaitForSecondsRealtime(.2f);
            File.WriteAllBytes("Logs/Shots/9-pause-hud.png", game.Screens.Hud.Tex.EncodeToPNG());
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            Assert.AreEqual((Mode.Play, 1f), (game.Mode, Time.timeScale));
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keys);
        }

        // The calendar: click it for a close-up, point at a week to read what it means, Esc to put it down.
        [UnityTest]
        public IEnumerator Calendar_close_up_explains_weeks()
        {
            Game.KeepLog = false;
            yield return SceneManager.LoadSceneAsync("Desk");
            yield return null;
            game = Object.FindAnyObjectByType<Game>();
            cam = Camera.main;
            game.Begin();
            yield return Settle();

            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            var keys = InputSystem.AddDevice<Keyboard>();
            IEnumerator Mouse(Vector2 at, bool down) { InputSystem.QueueStateEvent(mouse, new MouseState { position = at, buttons = (ushort)(down ? 1 : 0) }); yield return null; yield return null; }
            Vector2 Tile(int w) => cam.WorldToScreenPoint(game.Room.CalendarPaper.transform.TransformPoint(new Vector3(
                (Screens.CalX + (w - 1) % 4 * Screens.CalStepX + Screens.CalW / 2) / 256f - .5f, .5f - (Screens.CalY + (w - 1) / 4 * Screens.CalStepY + Screens.CalH / 2) / 320f, 0)));

            Vector2 paper = cam.WorldToScreenPoint(game.Room.CalendarPaper.bounds.center);
            yield return Mouse(paper, false);
            Assert.AreEqual((Pointer.Kind.Hand, "CALENDAR"), (Pointer.Current, Pointer.Label), "the calendar is clickable from the chair");
            yield return Mouse(paper, true);
            yield return Mouse(paper, false);
            Assert.AreEqual("calendar", game.Station);
            yield return Settle();

            yield return Mouse(Tile(4), false);
            Assert.AreEqual(4, game.CalendarWeek);
            StringAssert.Contains("INSPECTION", game.HoverText);
            int date = System.Array.IndexOf(game.S.Dates, Special.Nephew);
            yield return Mouse(Tile(date), false);
            Assert.AreEqual(date, game.CalendarWeek);
            StringAssert.Contains("NEPHEW", game.HoverText);
            yield return new WaitForSeconds(.2f);
            Shot(cam, "c-calendar");

            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            Assert.AreEqual(("monitor", Mode.Play), (game.Station, game.Mode), "Esc puts the calendar down, it doesn't pause");
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keys);
        }

        // v2: the rack and card punch, lie low, the printer's slip, and a random card on air.
        [UnityTest]
        public IEnumerator V2_rack_slip_and_lie_low()
        {
            // Logging on, to a test file: writing the log once threw before the hand was trimmed.
            Game.KeepLog = true;
            Game.LogFile = "weeks-test.csv";
            File.Delete(Path.Combine(Application.persistentDataPath, Game.LogFile));
            yield return SceneManager.LoadSceneAsync("Desk");
            yield return null;
            game = Object.FindAnyObjectByType<Game>();
            cam = Camera.main;
            game.Begin();
            yield return Settle(Game.LockSeconds + .2f);
            LogAssert.NoUnexpectedReceived();
            game.S.Cash = 50000;
            game.Go(); game.Go(); game.Go(); game.Go(); // Announce, interview, reveal, next week. // Week 1's show, then off air for week 2.
            Assert.AreEqual((2, false), (game.S.Week, game.Live));
            yield return Settle();

            // The first off air: a SWAP in the rack. Pick it, then a hand card.
            Assert.IsTrue(game.Room.Rack[0].gameObject.activeSelf, "the starting SWAP is in the rack");
            Shot(cam, "v2-1-rack");
            game.Click(At(game.Room.Rack[0]));
            Assert.AreEqual(0, game.Punching);
            var gone = game.S.Hand[1];
            game.Click(At(game.Room.Cards[1]));
            Assert.AreEqual((-1, 'S', 0), (game.Punching, game.S.Hand[1].Op, game.S.Rack.Count), "re-punched");
            Assert.IsFalse(game.Room.Rack[0].gameObject.activeSelf);
            Assert.AreEqual(Room.Hex("#55687a"), (Color32)game.Room.CardFaces[1].Tex.GetPixel(127, 0), "specials on blue stock, dimmed off air");
            yield return null;
            Shot(cam, "v2-2-repunched");

            // No desk action: going live lies low.
            int prize = game.S.Prize;
            game.Click(At(game.Room.LiveSwitch));
            Assert.AreEqual(DeskAction.LieLow, game.S.Acted);
            Assert.AreEqual("WEEK 2: ON AIR", game.Banner, "going live is a headline");
            yield return new WaitForSeconds(.4f);
            File.WriteAllBytes("Logs/Shots/v2-banner-hud.png", game.Screens.Hud.Tex.EncodeToPNG());
            Assert.AreEqual(Mathf.Max(Config.Prize, prize - Config.LieLow), game.S.Prize);
            game.Click(At(game.Room.Rack[0]));
            Assert.AreEqual(-1, game.Punching, "no re-punching on air");
            yield return Settle(Game.LockSeconds + .2f);

            // A JAM on air: units redrawn, can't be undone. Then announce without playing the SWAP: it's gone.
            game.S.Hand[0] = new Card('J');
            game.Refresh();
            int tens = game.Value / 10;
            game.Click(At(game.Room.Cards[0]));
            yield return Settle(Game.ReaderSeconds + .2f);
            Assert.AreEqual(tens, game.Value / 10, "JAM keeps the tens");
            game.Click(At(game.Room.UndoButton));
            Assert.AreEqual(1, game.Played.Count, "a random card can't be undone");
            game.S.Cash = 50000;
            game.Click(At(game.Room.GoButton));
            Assert.IsNotNull(game.Result);
            CollectionAssert.AreEqual(new[] { new Card('S') }, game.Result.Unused, "unplayed SWAP destroyed at announce");
            Assert.IsFalse(game.S.Hand.Any(c => c.IsSpecial));
            game.Click(Vector2.zero);
            yield return Settle(.3f);
            game.Click(Vector2.zero);
            Assert.AreEqual(3, game.S.Week);

            // The card reader prints a slip out of its slot. TAKE pins it to the monitor's edge.
            bool still = Prefs.ReduceMotion;
            Prefs.ReduceMotion = false; // A saved setting would skip the print.
            game.S.Offer = Slip.Sponsor;
            game.Refresh();
            yield return Settle(.5f);
            Assert.Less(game.Room.Slip.position.y, Room.SlipRest.y - .05f, "the slip is still printing out of the reader");
            yield return Settle(1.6f);
            Assert.AreEqual(Room.SlipRest.y, game.Room.Slip.position.y, .001f, "printed");
            Prefs.ReduceMotion = still;
            Shot(cam, "v2-4-slip");
            File.WriteAllBytes("Logs/Shots/v2-4-slip-paper.png", game.Room.SlipPaper.Tex.EncodeToPNG());
            // A click picks it up to read, like the calendar. It doesn't take it.
            game.Click(cam.WorldToScreenPoint(game.Room.Slip.position));
            Assert.AreEqual(("slip", Slip.None, Slip.Sponsor), (game.Station, game.S.Pinned, game.S.Offer));
            yield return Settle();
            Shot(cam, "v2-4b-slip-close");
            // Off the buttons: put down, still offered. Then pick it up again and press TAKE (pixel 42,169 of 160x192).
            game.Click(cam.WorldToScreenPoint(game.Room.Slip.TransformPoint(new Vector3(0, .3f, 0))));
            Assert.AreEqual(("monitor", Slip.Sponsor), (game.Station, game.S.Offer));
            yield return Settle();
            game.Click(cam.WorldToScreenPoint(game.Room.Slip.position));
            yield return Settle();
            game.Click(cam.WorldToScreenPoint(game.Room.Slip.TransformPoint(new Vector3(42 / 160f - .5f, .5f - 169 / 192f, 0))));
            Assert.AreEqual((Slip.Sponsor, Slip.None, "monitor"), (game.S.Pinned, game.S.Offer, game.Station));
            yield return null;
            Assert.AreEqual(Room.SlipPinned, game.Room.Slip.position);
            yield return Settle();
            Shot(cam, "v2-5-pinned");

            // Shred spreads its holders next door.
            game.Look("machine");
            yield return Settle();
            game.Click(At(game.Room.ActionProps[DeskAction.Shred]));
            yield return Settle();
            game.S.Board[45] = 4; game.S.Board[44] = 0; game.S.Board[46] = 0;
            var screen = game.Room.MonitorScreen.transform;
            game.Click(cam.WorldToScreenPoint(screen.TransformPoint(new Vector3((16 + 5 * 32 + 15) / 640f - .5f, .5f - (24 + 4 * 32 + 15) / 360f, 0))));
            Assert.AreEqual((0, 4), (game.S.Board[45], game.S.Board[44] + game.S.Board[46]));
            Assert.AreEqual(3, File.ReadAllLines(Path.Combine(Application.persistentDataPath, Game.LogFile)).Length, "header and two weeks logged");
            File.Delete(Path.Combine(Application.persistentDataPath, Game.LogFile));
            Game.KeepLog = false;
            Game.LogFile = "weeks-v2.csv";
        }

        // Focus (space): the hand fans along the bottom of the screen, and the cards still play.
        [UnityTest]
        public IEnumerator Focus_fans_the_hand_at_the_bottom()
        {
            Game.KeepLog = false;
            yield return SceneManager.LoadSceneAsync("Desk");
            yield return null;
            game = Object.FindAnyObjectByType<Game>();
            cam = Camera.main;
            game.Begin(); // Opens on air.
            yield return Settle(Game.LockSeconds + .2f);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keys = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Space));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            Assert.IsTrue(game.Focus);
            yield return Settle();
            for (int i = 0; i < game.S.Hand.Count; i++)
            {
                var p = cam.WorldToScreenPoint(game.Room.Cards[i].position);
                Assert.Less(p.y, Screen.height * .3f, $"card {i} sits at the bottom of the screen");
                Assert.That(p.x, Is.InRange(0, Screen.width), $"card {i} is on screen");
            }
            Shot(cam, "f-focus");
            int card = Enumerable.Range(0, game.S.Hand.Count).FirstOrDefault(game.CanPlay);
            if (game.CanPlay(card))
            {
                int to = Run.Apply(game.Value, game.S.Hand[card]).Value;
                game.Click(At(game.Room.Cards[card]));
                yield return Settle(Game.ReaderSeconds + .2f);
                Assert.AreEqual(to, game.Value, "a card plays from the focus fan");
            }
            InputSystem.RemoveDevice(keys);
        }
    }
}
