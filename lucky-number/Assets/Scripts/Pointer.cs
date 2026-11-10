using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace NoWinners
{
    // The mouse cursor, plus a border and name on whatever the pointer is over, so players can tell what's clickable.
    // Hand: you can use it. No-entry: you can't right now, and the name says why. Arrow: nothing there.
    // Reads Game's public state only, and attaches itself to the Game in each scene.
    public class Pointer : MonoBehaviour
    {
        public enum Kind { Arrow, Hand, No }

        // What the pointer is over this frame. Public so a test can check it.
        public static Kind Current { get; private set; }
        public static string Label { get; private set; }

        Game game;
        Collider focus;
        bool focusOk;
        Kind shown = (Kind)(-1);
        Texture2D arrow, hand, no;
        readonly Dictionary<string, Canvas8> tags = new Dictionary<string, Canvas8>();

        static readonly Color32 Gold = Room.Hex("#FFD23A"), Grey = Room.Hex("#8A8A90"), Plate = Room.Hex("#111111");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
            AddTo(Object.FindAnyObjectByType<Game>());
            SceneManager.sceneLoaded += (_, _) => AddTo(Object.FindAnyObjectByType<Game>());
        }

        static void AddTo(Game g)
        {
            if (g && !g.GetComponent<Pointer>()) g.gameObject.AddComponent<Pointer>();
        }

        void Awake()
        {
            useGUILayout = false;
            game = GetComponent<Game>();
            arrow = Resources.Load<Texture2D>("Cursors/cursor_arrow");
            hand = Resources.Load<Texture2D>("Cursors/cursor_hand");
            no = Resources.Load<Texture2D>("Cursors/cursor_no");
        }

        void OnDestroy()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            foreach (var pair in tags) if (pair.Value?.Tex != null) Object.Destroy(pair.Value.Tex);
            tags.Clear();
        }

        void LateUpdate()
        {
            focus = null;
            Label = null;
            var mouse = Mouse.current;
            Kind kind = Kind.Arrow;
            if (mouse != null && game.Room != null)
            {
                Vector2 at = mouse.position.ReadValue();
                if (game.Mode == Mode.Play && !game.EndCardUp) kind = InRoom(at);
                else if (OverMenu(at)) kind = Kind.Hand;
            }
            Current = kind;
            if (kind == shown) return;
            shown = kind;
            // Hotspots match CURSORS in tools/art/textures.mjs.
            if (kind == Kind.Hand) Cursor.SetCursor(hand, new Vector2(10, 2), CursorMode.Auto);
            else if (kind == Kind.No) Cursor.SetCursor(no, new Vector2(16, 16), CursorMode.Auto);
            else Cursor.SetCursor(arrow, new Vector2(2, 2), CursorMode.Auto);
        }

        Kind InRoom(Vector2 at)
        {
            if (game.Busy) return Kind.Arrow;
            if (game.Result != null) return Kind.Hand; // Any click moves the result on.
            if (game.Station == "calendar" || game.Station == "slip") return Kind.Hand; // Any click puts it down, or takes or tears the slip.
            if (game.Station != "monitor" && game.Station != "machine") return Kind.Arrow;
            var c = game.Pick(at, out var point);
            if (c == null) return Kind.Arrow;
            if (c == game.Room.MonitorScreen)
                return game.Shredding && BoardSquare(c, point) ? Kind.Hand : Kind.Arrow;
            if (!Describe(c, point, out var name, out bool ok)) return Kind.Arrow;
            focus = c;
            focusOk = ok;
            Label = name;
            return ok ? Kind.Hand : Kind.No;
        }

        static bool BoardSquare(Collider screen, Vector3 point)
        {
            var uv = screen.transform.InverseTransformPoint(point);
            float x = (uv.x + .5f) * 640, y = (.5f - uv.y) * 360;
            return x >= 16 && x < 16 + 10 * Screens.Cell && y >= 24 && y < 24 + 10 * Screens.Cell;
        }

        // Name for the tag, and whether a click would do anything now. The name says why not, when it wouldn't.
        bool Describe(Collider c, Vector3 point, out string name, out bool ok)
        {
            var r = game.Room;
            var s = game.S;
            bool offAir = !game.Live;
            int card = System.Array.IndexOf(r.CardColliders, c);
            if (card == Room.CardCount - 1 && game.Replacing)
            {
                ok = false;
                name = $"NEW CARD: {game.IncomingCard}";
                return true;
            }
            if (card >= 0 && card < s.Hand.Count && game.Dealing && offAir)
            {
                ok = true;
                name = (game.ConfirmCard == card ? "CLICK AGAIN TO CONFIRM: " : "") + $"REPLACE {s.Hand[card]} WITH {game.IncomingCard}";
                return true;
            }
            if (card >= 0 && card < s.Hand.Count && game.Supplying && offAir)
            {
                ok = true;
                name = (game.ConfirmCard == card ? "CLICK AGAIN TO CONFIRM: " : "") + $"REPLACE {s.Hand[card]} WITH {game.IncomingCard} (-{Config.SupplierCost} CASH)";
                return true;
            }
            if (card >= 0 && card < s.Hand.Count && game.Punching >= 0 && offAir)
            {
                ok = !s.Hand[card].IsSpecial;
                name = ok ? (game.ConfirmCard == card ? "CLICK AGAIN TO CONFIRM: " : "") + $"REPLACE {s.Hand[card]} WITH {game.IncomingCard}" : $"CARD {s.Hand[card]}: ALREADY A SPECIAL CARD";
                return true;
            }
            int rack = System.Array.IndexOf(r.RackColliders, c);
            if (rack >= 0)
            {
                ok = offAir && game.Result == null;
                name = $"RACK {s.Rack[rack]}" + (ok ? (game.Punching == rack ? ": PUT BACK" : ": REPLACE") : ": ADJUST CARD IN OFF AIR");
                return true;
            }
            if (c == r.SlipCollider || c.transform == r.Slip)
            {
                ok = true;
                name = $"SLIP: {Slips.Name(s.Pinned != Slip.None ? s.Pinned : s.Offer)}" + (s.Pinned != Slip.None ? " TAKEN" : "");
                return true;
            }
            if (card >= 0)
            {
                ok = game.CanPlay(card);
                string why = offAir ? "ON AIR ONLY" : !game.Drawn ? "WAIT FOR THE DRAW" : game.Played.Count >= Run.CardLimit(s.Week) ? "CARD LIMIT" : "WON'T FIT";
                name = $"CARD {s.Hand[card]}" + (ok ? "" : $": {why}");
                return true;
            }
            if (c == r.GoButton) { ok = game.Drawn; name = ok ? $"ANNOUNCE {game.Value:00}" : offAir ? "RED BUTTON: GO LIVE FIRST" : "RED BUTTON: WAIT FOR THE DRAW"; return true; }
            if (c == r.UndoButton) { ok = game.Played.Count > 0; name = ok ? "EJECT LAST CARD" : "EJECT: NO CARD IN"; return true; }
            if (c == r.LiveSwitch) { ok = offAir; name = ok ? "GO LIVE" : "GO LIVE: ALREADY ON AIR"; return true; }
            if (c == r.CalendarPaper) { ok = !game.Shredding; name = ok ? "CALENDAR" : "CALENDAR: SHREDDING"; return true; }
            foreach (var kv in r.ActionProps)
            {
                if (c != kv.Value) continue;
                string thing = kv.Key switch
                {
                    DeskAction.Supplier => "PHONE",
                    DeskAction.Bribe => "ENVELOPE",
                    _ => "SHREDDER"
                };
                string job = kv.Key switch
                {
                    DeskAction.Supplier => $"BUY A CARD, -{Config.SupplierCost} CASH",
                    DeskAction.Bribe => $"TAKE +{Config.Bribe} CASH, PRIZE +{Config.Rollover} FOR GOOD",
                    _ => "CLEAR ONE SQUARE"
                };
                bool cancel = (kv.Key == DeskAction.Shred && game.Shredding) || (kv.Key == DeskAction.Supplier && game.Supplying);
                ok = cancel || (offAir && s.Acted == null && (kv.Key != DeskAction.Supplier || s.Cash > Config.SupplierCost));
                name = cancel ? $"{thing}: CANCEL" : ok ? $"{thing}: {job}" : $"{thing}: {(offAir ? (s.Acted != null ? "ONE JOB A WEEK" : "NOT ENOUGH CASH") : "OFF AIR ONLY")}";
                return true;
            }
            name = null; ok = false;
            return false;
        }

        bool OverMenu(Vector2 at)
        {
            if (game.Mode == Mode.Start) return true;
            var hud = Game.ScreenToHud(at);
            if (game.Mode == Mode.Paused) return Any(3, i => Game.In(hud, 200, 168 + i * 40, 240, 32));
            if (game.Mode == Mode.Play) return Any(2, i => Game.In(hud, 176, 232 + i * 40, 288, 32));
            var cam = game.Cam ?? Camera.main;
            if (cam == null || !Physics.Raycast(cam.ScreenPointToRay(at), out var hit, 10) || hit.collider != game.Room.MonitorScreen) return false;
            var uv = hit.collider.transform.InverseTransformPoint(hit.point);
            var m = new Vector2Int(Mathf.FloorToInt((uv.x + .5f) * 640), Mathf.FloorToInt((.5f - uv.y) * 360));
            if (game.Mode == Mode.Title) return Any(3, i => Game.In(m, 352, 184 + i * 40, 272, 32));
            return Game.In(m, 352, 44, 272, Game.BackY + 32 - 44);
        }

        static bool Any(int n, System.Func<int, bool> f) { for (int i = 0; i < n; i++) if (f(i)) return true; return false; }

        // Corner brackets round the thing on screen, and its name on a plate above. Drawn in whole HUD pixels so it stays crisp.
        void OnGUI()
        {
            if (focus == null || Event.current.type != EventType.Repaint) return;
            var cam = game.Cam ?? Camera.main;
            if (cam == null) return;
            var b = focus.bounds;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var p = cam.WorldToScreenPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                if (p.z < 0) return;
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                y0 = Mathf.Min(y0, Screen.height - p.y); y1 = Mathf.Max(y1, Screen.height - p.y);
            }
            int s = Game.HudScale, pad = 3 * s, len = 8 * s, t = s;
            float Snap(float v) => Mathf.Round(v / s) * s;
            x0 = Snap(x0 - pad); y0 = Snap(y0 - pad); x1 = Snap(x1 + pad); y1 = Snap(y1 + pad);

            var old = GUI.color;
            GUI.color = focusOk ? Gold : Grey;
            void Bar(float x, float y, float w, float h) => GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            Bar(x0, y0, len, t); Bar(x0, y0, t, len);
            Bar(x1 - len, y0, len, t); Bar(x1 - t, y0, t, len);
            Bar(x0, y1 - t, len, t); Bar(x0, y1 - len, t, len);
            Bar(x1 - len, y1 - t, len, t); Bar(x1 - t, y1 - len, t, len);
            GUI.color = old;

            var tag = Tag(Label, focusOk);
            float w = tag.W * s, h = tag.H * s;
            float tx = Mathf.Clamp(Snap((x0 + x1 - w) / 2), 0, Screen.width - w);
            float ty = y0 - h - 2 * s;
            if (ty < 0) ty = y1 + 2 * s; // No room above: hang it below.
            GUI.DrawTexture(new Rect(tx, ty, w, h), tag.Tex);
        }

        Canvas8 Tag(string text, bool ok)
        {
            string key = (ok ? "1" : "0") + text;
            if (tags.TryGetValue(key, out var c)) return c;
            if (tags.Count >= 32)
            {
                foreach (var pair in tags) if (pair.Value?.Tex != null) Object.Destroy(pair.Value.Tex);
                tags.Clear();
            }
            c = new Canvas8(text.Length * 8 + 10, 14);
            c.Tex.filterMode = FilterMode.Point;
            c.Fill(Plate);
            c.Frame(0, 0, c.W, c.H, ok ? Gold : Grey, 1);
            c.Text(text, 5, 3, ok ? Gold : Grey);
            c.Apply();
            return tags[key] = c;
        }
    }
}
