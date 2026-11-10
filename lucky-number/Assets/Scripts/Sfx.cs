using System.Collections.Generic;
using UnityEngine;

namespace NoWinners
{
    // Plays the generated sound effects (tools/sfx, docs/no-winners-sfx-list.md) by watching the game's public state.
    // Game only calls in for what it can't show as state: a refused click.
    // Clips are pre-mixed to "quiet off air, loud on air", so every source plays at its own level times Prefs.Volume. Music has its own slider, Prefs.Music.
    public class Sfx : MonoBehaviour
    {
        Game game;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<AudioSource, float> level = new Dictionary<AudioSource, float>(); // Each SFX source's own level, before the slider.
        AudioSource desk, tv, room, machine, deadAir, standBy, music;
        AudioLowPassFilter duck;
        readonly List<(float at, string name)> queue = new List<(float, string)>();

        // Last sounds asked for, newest last. Tests read it.
        public readonly List<string> Heard = new List<string>();
        public int ClipCount => clips.Count;

        // What was true last frame.
        Run run;
        int week, played, flipVariant, tickStep = -1, stall = -1;
        Mode mode;
        int sel;
        bool live, drawn, busy, shredding, revealed, endCard, started;
        DeskAction? acted;
        int lost, rack, punching = -1, confirm = -1, hand, introShown;
        Slip offer, pinned;
        bool leaning;
        WeekResult result;
        string station, hover;
        int preview = -1;
        float volume, musicVolume, musicLevel, nextClock, lastFlip, musicAt = float.MaxValue;
        public float SfxVolume => desk.volume;
        bool reduce, tock;

        void Awake()
        {
            game = GetComponent<Game>();
            foreach (var c in Resources.LoadAll<AudioClip>("Audio")) clips[c.name] = c;
            var host = new GameObject("Sfx");
            host.transform.SetParent(transform, false);
            AudioSource Source(bool loop)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0;
                level[s] = 1;
                return s;
            }
            desk = Source(false); tv = Source(false);
            room = Source(true); machine = Source(true); deadAir = Source(true); standBy = Source(true); music = Source(true);
            level.Remove(music);
            duck = host.AddComponent<AudioLowPassFilter>(); // Pause muffles everything behind the menu.
            duck.cutoffFrequency = 22000;
        }

        bool IsTv(string name) => name.StartsWith("stall_") || name.StartsWith("broadcast_") || name.StartsWith("live_onair") || name.StartsWith("end_finale")
            || name == "announce_static" || name == "result_clean" || name == "result_small" || name == "result_scandal";

        public void Play(string name, float volumeScale = 1)
        {
            Heard.Add(name);
            if (Heard.Count > 64) Heard.RemoveAt(0);
            if (clips.TryGetValue(name, out var clip)) (IsTv(name) ? tv : desk).PlayOneShot(clip, volumeScale);
        }

        void Later(float seconds, string name) => queue.Add((Time.time + seconds, name));

        void Loop(AudioSource s, string name, float vol = 1)
        {
            level[s] = vol;
            if (s.isPlaying || !clips.TryGetValue(name, out var clip)) return;
            s.clip = clip;
            s.Play();
            Heard.Add(name);
        }

        void StopShow()
        {
            queue.Clear();
            tv.Stop(); machine.Stop(); deadAir.Stop(); standBy.Stop();
        }

        // After Game.Update, so this frame's changes are all in.
        void LateUpdate()
        {
            foreach (var kv in level) kv.Key.volume = kv.Value * Prefs.Volume;
            duck.cutoffFrequency = game.Mode == Mode.Paused ? 900 : 22000;
            Music();
            if (game.IntroUp) { Intro(); return; } // The week's sounds wait for the backstory to end.
            introShown = 0;
            if (!started) { started = true; Begin(); return; }

            Menus();
            if (game.S != run) { run = game.S; StopShow(); Remember(); week = 0; } // A new run: week 1 still gets its sounds.
            if (game.Mode == Mode.Play) Week();
            Remember();

            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (Time.time < queue[i].at) continue;
                var name = queue[i].name;
                queue.RemoveAt(i);
                if (name == "end_stand_by_tone") { tv.Stop(); Loop(standBy, name); }
                else if (name == "end_signal_loss") { tv.Stop(); Play(name); }
                else Play(name);
            }

            if (Time.unscaledTime >= nextClock && game.Tutorial == null) // Silent while the guide is up: the ticking made players feel rushed.
            {
                nextClock = Time.unscaledTime + 1;
                Play(tock ? "room_clock_tock" : "room_clock_tick");
                tock = !tock;
            }
        }

        void Begin()
        {
            Loop(room, "room_tone");
            Play("menu_tv_power_on");
            Later(1.3f, "broadcast_theme");
            musicAt = Time.unscaledTime + 4.5f; // Once the theme has finished.
            nextClock = Time.unscaledTime + 2;
            run = game.S; week = run.Week;
            Remember();
        }

        // One loop for the menus and the desk (tools/music). Loud on the title, a bed under the desk,
        // nearly gone on air so the broadcast carries, out when a run ends.
        void Music()
        {
            if (Time.unscaledTime < musicAt || !clips.TryGetValue("music_main", out var clip)) return;
            if (!music.isPlaying) { music.clip = clip; musicLevel = 0; music.Play(); Heard.Add("music_main"); }
            var m = game.Mode;
            float want = game.S.Over != Ending.None ? 0
                : m == Mode.Start || m == Mode.Title || (m == Mode.Settings && game.SettingsFrom == Mode.Title) ? .9f
                : game.IntroUp ? .35f
                : game.Live ? .25f
                : .6f;
            musicLevel = Mathf.MoveTowards(musicLevel, want, Time.unscaledDeltaTime * .5f);
            music.volume = musicLevel * Prefs.Music;
        }

        void Menus()
        {
            var m = game.Mode;
            if (m != mode)
            {
                bool back = mode == Mode.Settings || (mode == Mode.Play && m == Mode.Paused) || m == Mode.Title && mode != Mode.Start;
                Play(back ? "ui_menu_back" : "ui_menu_confirm");
                if (mode == Mode.Start) { tv.Stop(); queue.Clear(); }
                if (m == Mode.Title || m == Mode.Start) StopShow();
            }
            else if (game.Sel != sel && (m != Mode.Play || game.EndCardUp)) Play("ui_menu_move");
            if (m == Mode.Settings)
            {
                if (Prefs.Volume != volume || Prefs.Music != musicVolume) Play("ui_slider_tick");
                else if (Prefs.ReduceMotion != reduce) Play("ui_menu_move");
            }
        }

        void Week()
        {
            var s = game.S;
            if (s.Week != week)
            {
                StopShow();
                Play("desk_calendar_flip");
                if (s.Offer == Slip.None) Later(.35f, "desk_tickets_print"); // Otherwise the slip's print covers it.
                Later(1.9f, "desk_cash_register");
                if (Run.IsInspection(s.Week)) Later(.9f, "desk_inspection_stamp");
            }

            if (game.HoverText != hover && game.HoverText != null) Play("ui_hover", .8f);
            if (game.Preview != preview && game.Preview >= 0) Play("ui_hover", game.Shredding ? .5f : .8f);
            int now = Game.StationIndex(game.Station), was = Game.StationIndex(station);
            if (station != null && now != was) Play(now < was ? "room_look_left" : "room_look_right");
            if (game.Station == "calendar" && station != "calendar") Play("desk_calendar_flip");

            if (s.Acted != acted && s.Acted != null)
            {
                if (s.Acted == DeskAction.Supplier) { Play("desk_supplier_phone"); if (s.LostCards > lost) Later(2.2f, "desk_card_lost"); }
                else if (s.Acted == DeskAction.Bribe) Play("desk_bribe");
                else if (s.Acted == DeskAction.Shred) Play("desk_shred");
                // Lie low is silent: it happens as the lever goes, under live_switch.
            }
            else if (game.Shredding != shredding) Play(game.Shredding ? "ui_menu_confirm" : "ui_menu_back");

            Prep(s);

            if (game.Live && !live)
            {
                Play("live_switch");
                Later(.1f, "live_relay");
                Later(.35f, "live_onair_sting");
                machine.Stop();
                Loop(machine, "machine_spin_loop");
            }
            bool rolling = game.Live && Time.time - game.RolledAt < Game.LockSeconds;
            if (rolling && Time.time - lastFlip >= 1 / 12f)
            {
                lastFlip = Time.time;
                Play($"machine_digit_flip_{flipVariant % 3 + 1}", .9f);
                flipVariant++;
            }
            if (game.Drawn && !drawn) { Play("machine_lock"); level[machine] = .3f; }

            if (game.Busy && !busy) Play("rig_card_feed");
            if (game.Result == null && game.Played.Count > played) Play("rig_number_snap");
            if (game.Result == null && game.Played.Count < played) Play("rig_undo");
            Clock();

            if (game.Result != null && result != game.Result) Announce();
            if (game.Result != null && game.Result.Revealed && !revealed && game.Result.Air.Pay > 0)
            {
                Play("result_reveal_beep");
                int n = Mathf.Clamp(game.Result.Air.Pay / 25, 6, 24);
                for (int i = 0; i < n; i++) Later(.45f + i * .035f, "result_cash_tick");
            }
            if (game.EndCardUp && !endCard) for (int i = 0; i < 16; i++) Later(i * .045f, "end_count_up_tick");
        }

        // The last five seconds tick at 2 Hz. Past the target: dead air, then a stall every 10 s.
        void Clock()
        {
            if (!game.Drawn || game.Result != null) { tickStep = -1; deadAir.Stop(); stall = -1; return; }
            float left = Config.Target - game.LiveSeconds;
            int step = Mathf.CeilToInt(left * 2);
            if (step != tickStep && step >= 1 && step <= 10 && tickStep != -1) Play(step <= 2 ? "rig_timer_tick_final" : "rig_timer_tick");
            tickStep = step;
            if (left < 0) Loop(deadAir, "rig_dead_air");
            if (game.Stall != stall && game.Stall >= 0)
                Play(new[] { "stall_bad_joke", "stall_sponsor", "stall_interview", "stall_panic" }[game.Stall % 4]);
            stall = game.Stall;
        }

        void Announce()
        {
            var r = game.Result;
            var a = r.Air;
            var s = game.S;
            machine.Stop(); deadAir.Stop();
            Play("announce_button");
            Later(.12f, "announce_static");
            if (s.Over == Ending.Won) Later(.45f, "end_finale_fanfare");
            else Later(.45f, a.Tier == Tier.Clean ? "result_clean" : a.Tier == Tier.Small ? "result_small" : "result_scandal");
            if (a.Bonus > 0) Later(1.3f, "result_live_bonus");
            if (s.Streak > r.StreakBefore) Later(1.7f, $"result_streak_{Mathf.Clamp(s.Streak, 1, 6)}");
            if (a.SlipCash > 0 || a.Reward > 0) Later(2.1f, "desk_cash_register"); // A slip paid, or the minister did.
            else if (a.Slip != Slip.None && !a.SlipMet) Later(2.1f, "desk_denied");
            if (a.Gift != null) Later(2.6f, a.GiftLost ? "desk_card_lost" : "result_gift_card");
            if (s.Over == Ending.Lost)
            {
                bool still = Prefs.ReduceMotion; // Screens.Collapse skips the squash with reduce motion.
                Later(still ? 1f : 1.1f, "end_signal_loss");
                Later(still ? 1.5f : 2.4f, "end_stand_by_tone");
            }
        }

        // Off-air prep: the slip, the rack and card swaps, and leaning in.
        void Prep(Run s)
        {
            int handNow = HandSig(s);
            if (s.Offer != Slip.None && offer == Slip.None) Later(.4f, "desk_slip_print"); // Game prints it from 0.4 s.
            if (s.Pinned != Slip.None && pinned == Slip.None) Play("desk_slip_pin");
            else if (s.Offer == Slip.None && offer != Slip.None && !game.Live) Play("desk_slip_tear"); // Going live bins it quietly.
            if (game.Punching >= 0 && game.Punching != punching) Play("desk_rack_pick");
            if (s.Rack.Count < rack && !game.Live) Play("desk_repunch");
            else if (handNow != hand && s.Week == week && !game.Live && game.Result == null && s.Acted == acted && s.Rack.Count == rack) Play("desk_card_place");
            if (game.ConfirmCard >= 0 && game.ConfirmCard != confirm) Play("ui_menu_move"); // First click asks.
            if (game.Leaning != leaning) Play(game.Leaning ? "room_lean_in" : "room_lean_out");
        }

        static int HandSig(Run s) { int h = s.Hand.Count; foreach (var c in s.Hand) h = h * 31 + c.Op * 397 + c.X; return h; }

        // The backstory: a swell per line, a sting on the last.
        void Intro()
        {
            int n = game.IntroShown;
            if (n > introShown) Play(n >= Game.IntroText.Length ? "intro_reveal" : "intro_line");
            introShown = n;
        }

        void Remember()
        {
            var s = game.S;
            offer = s.Offer; pinned = s.Pinned; rack = s.Rack.Count; hand = HandSig(s);
            punching = game.Punching; confirm = game.ConfirmCard; leaning = game.Leaning;
            mode = game.Mode; sel = game.Sel;
            week = s.Week; acted = s.Acted; lost = s.LostCards;
            live = game.Live; drawn = game.Drawn; busy = game.Busy; shredding = game.Shredding;
            played = game.Played.Count;
            result = game.Result; revealed = game.Result != null && game.Result.Revealed; endCard = game.EndCardUp;
            station = game.Station; hover = game.HoverText; preview = game.Preview;
            volume = Prefs.Volume; musicVolume = Prefs.Music; reduce = Prefs.ReduceMotion;
        }
    }
}
