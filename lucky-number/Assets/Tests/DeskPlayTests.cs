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

        [UnityTest]
        public IEnumerator A_week_plays_through_the_props()
        {
            Game.KeepLog = false;
            yield return SceneManager.LoadSceneAsync("Desk");
            yield return null;
            game = Object.FindAnyObjectByType<Game>();
            cam = Camera.main;
            yield return Settle();
            LogAssert.NoUnexpectedReceived();
            Shot(cam, "1-off-air");

            // Off air: number hidden, cards refuse, one desk action.
            Assert.IsNull(game.S.Roll);
            game.Click(At(game.Room.Cards[0]));
            yield return null;
            Assert.IsFalse(game.Busy, "cards wait for on air");
            int cash = game.S.Cash;
            game.Click(At(game.Room.ActionProps[DeskAction.Bribe]));
            Assert.AreEqual(cash + Config.Bribe, game.S.Cash);
            Assert.AreEqual(DeskAction.Bribe, game.S.Acted);

            // Shredder aims at a real square through the monitor.
            game.S.Acted = null;
            int held = Enumerable.Range(0, 100).First(n => game.S.Board[n] > 0);
            game.Click(At(game.Room.ActionProps[DeskAction.Shred]));
            Assert.IsTrue(game.Shredding);
            var screen = game.Room.MonitorScreen.transform;
            var world = screen.TransformPoint(new Vector3((16 + held % 10 * 32 + 15) / 640f - .5f, .5f - (24 + held / 10 * 32 + 15) / 360f, 0));
            int before = game.S.Board[held];
            game.Click(cam.WorldToScreenPoint(world));
            Assert.AreEqual(Mathf.Max(0, before - Config.Shred), game.S.Board[held], $"shred on square {held}");

            // On air: seeded first roll, so one card reaches a free square.
            game.Click(At(game.Room.LiveSwitch));
            Assert.IsTrue(game.Live);
            int roll = game.S.Roll.Value;
            yield return Settle(Game.LockSeconds + .2f);
            Assert.IsTrue(game.Drawn);
            int playable = Enumerable.Range(0, game.S.Hand.Count).First(game.CanPlay);
            Assert.AreEqual(Room.Hex("#D9C79A"), (Color32)game.Room.CardFaces[playable].Tex.GetPixel(127, 0), "playable cards light up when the machine locks");
            Shot(cam, "2-on-air");

            int card = Enumerable.Range(0, game.S.Hand.Count).First(i => game.CanPlay(i) && game.S.Board[Run.Apply(roll, game.S.Hand[i]).Value] == 0);
            int to = Run.Apply(roll, game.S.Hand[card]).Value;
            game.Click(At(game.Room.Cards[card]));
            Assert.IsTrue(game.Busy);
            yield return Settle(Game.ReaderSeconds + .2f);
            Assert.IsFalse(game.Busy);
            Assert.AreEqual(to, game.Value);
            yield return null;
            Shot(cam, "3-card-played");

            // Eject and replay, then announce.
            game.Click(At(game.Room.UndoButton));
            Assert.AreEqual(roll, game.Value);
            yield return null;
            game.Click(At(game.Room.Cards[card]));
            yield return Settle(Game.ReaderSeconds + .2f);
            game.Click(At(game.Room.GoButton));
            Assert.AreEqual(Tier.Clean, game.Result.Air.Tier);
            Assert.AreEqual(1, game.S.Streak);
            Assert.AreEqual("tv", game.Station);
            yield return Settle();
            Shot(cam, "4-tv-clean");

            // Next week: a forced scandal goes TV, then reveal on the monitor, then week 3.
            game.Click(Vector2.zero);
            Assert.AreEqual(2, game.S.Week);
            yield return Settle();
            game.Click(At(game.Room.LiveSwitch));
            yield return Settle(Game.LockSeconds + .2f);
            for (int n = 0; n < 100; n++) game.S.Board[n] = 4;
            game.S.Cash = 50000;
            game.Click(At(game.Room.GoButton));
            Assert.AreEqual(Tier.Scandal, game.Result.Air.Tier);
            yield return Settle();
            Shot(cam, "5-tv-scandal");
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
            game.Click(Vector2.zero);
            yield return Settle();
            Shot(cam, "7-bankrupt");
            game.Click(Vector2.zero);
            Assert.AreEqual((1, Ending.None), (game.S.Week, game.S.Over), "play again starts a new run");

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
            yield return Mouse(cam.WorldToScreenPoint(game.Room.LiveSwitch.bounds.center), true);
            yield return Mouse(cam.WorldToScreenPoint(game.Room.LiveSwitch.bounds.center), false);
            Assert.IsTrue(game.Live, "a real mouse click flips GO LIVE");
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
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keys);
        }
    }
}
