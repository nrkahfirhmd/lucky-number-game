using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NoWinners.Tests
{
    // The sounds follow the game: every main moment of a week asks for its clip, and the volume row works.
    public class SfxPlayTests
    {
        static void Heard(Game game, string name) => Assert.Contains(name, game.Sfx.Heard, $"expected {name}");

        [UnityTest]
        public IEnumerator A_week_asks_for_its_sounds()
        {
            Game.KeepLog = false;
            yield return SceneManager.LoadSceneAsync("Desk");
            yield return null;
            var game = Object.FindAnyObjectByType<Game>();
            yield return null;
            Assert.GreaterOrEqual(game.Sfx.ClipCount, 58, "all generated clips load from Resources/Audio");
            Heard(game, "menu_tv_power_on");
            Heard(game, "room_tone");

            game.Begin();
            yield return null;
            Heard(game, "ui_menu_confirm");
            Heard(game, "desk_calendar_flip");

            game.TakeAction(DeskAction.Bribe);
            yield return null;
            Heard(game, "desk_bribe");
            game.TakeAction(DeskAction.Supplier); // One action a week.
            Heard(game, "desk_denied");

            game.GoLive();
            yield return null;
            Heard(game, "live_switch");
            Heard(game, "machine_spin_loop");
            yield return new WaitForSeconds(.4f);
            Heard(game, "machine_digit_flip_1");
            Heard(game, "live_onair_sting");
            yield return new WaitForSeconds(Game.LockSeconds);
            Heard(game, "machine_lock");

            game.Go();
            yield return null;
            Heard(game, "announce_button");
            yield return new WaitForSeconds(.6f);
            Heard(game, "announce_static");
            var tier = game.Result.Air.Tier;
            Heard(game, tier == Tier.Clean ? "result_clean" : tier == Tier.Small ? "result_small" : "result_scandal");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator The_volume_row_sets_the_master_volume()
        {
            Game.KeepLog = false;
            yield return SceneManager.LoadSceneAsync("Desk");
            yield return null;
            var game = Object.FindAnyObjectByType<Game>();
            float was = Prefs.Volume;
            game.Click(Vector2.zero); // Start card to the title menu.
            game.OpenSettings();
            yield return new WaitForSeconds(1.2f); // The camera leans in to the monitor.
            game.Click(game.MonitorToScreen(364 + 44, 44 + 36)); // A quarter along the volume slider.
            yield return null;
            Assert.AreEqual(.25f, Prefs.Volume, .03f);
            Assert.AreEqual(Prefs.Volume, AudioListener.volume, .001f);
            Assert.AreEqual(0, game.Sel, "the volume row takes focus");
            Heard(game, "ui_slider_tick");
            yield return new WaitForSecondsRealtime(.2f); // The monitor redraws on a 0.1 s timer.
            System.IO.Directory.CreateDirectory("Logs/Shots");
            System.IO.File.WriteAllBytes("Logs/Shots/settings-monitor.png", game.Room.Monitor.Tex.EncodeToPNG());
            Prefs.Volume = was; // Leave the player's saved setting alone: nothing here calls Save.
        }
    }
}
