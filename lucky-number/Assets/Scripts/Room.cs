using System.Collections.Generic;
using UnityEngine;

namespace NoWinners
{
    // The room, in placeholder boxes. Layout comes from prototypes/desk.html with z flipped.
    // ponytail: props built in code so the rules could get playable first. Swap for prefabs at the Art milestone.
    public class Room
    {
        public const int CardCount = 6;
        public const float CardBaseY = 0.8f, CardZ = 2.66f, CardStep = 0.145f;
        public static readonly Vector3 CardEuler = new Vector3(75.7f, 0, 0), ReaderSlot = new Vector3(-0.6f, 0.87f, 2.88f);

        public readonly Canvas8 Monitor = new Canvas8(640, 360), Tv = new Canvas8(640, 360);
        public readonly Canvas8[] CardFaces = new Canvas8[CardCount];
        public readonly Transform[] Cards = new Transform[CardCount];
        public readonly Transform[] Balls = new Transform[14];
        public Canvas8 Calendar, MachineFace, Clock, GoPlate, UndoPlate;
        public readonly Dictionary<DeskAction, Canvas8> ActionPlates = new Dictionary<DeskAction, Canvas8>();
        public Canvas8 LivePlate;
        public Collider MonitorScreen, GoButton, UndoButton, LiveSwitch;
        public readonly Dictionary<DeskAction, Collider> ActionProps = new Dictionary<DeskAction, Collider>();
        public Transform Lever, GoCap, Reader;
        public Renderer ReaderLamp;

        readonly Material lit = Resources.Load<Material>("NW_Lit"), unlit = Resources.Load<Material>("NW_Unlit");
        readonly Dictionary<string, Material> colours = new Dictionary<string, Material>();
        readonly Mesh cube = MeshOf(PrimitiveType.Cube), quad = MeshOf(PrimitiveType.Quad), sphere = MeshOf(PrimitiveType.Sphere), cylinder = MeshOf(PrimitiveType.Cylinder);
        readonly Transform root = new GameObject("Room").transform;

        static Mesh MeshOf(PrimitiveType t)
        {
            var g = GameObject.CreatePrimitive(t);
            var m = g.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(g);
            return m;
        }

        public static Color32 Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? (Color32)c : default;

        Material Lit(string hex)
        {
            if (colours.TryGetValue(hex, out var m)) return m;
            m = new Material(lit) { color = Hex(hex) };
            return colours[hex] = m;
        }

        GameObject Mesh(string name, Mesh mesh, Material m, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root);
            g.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        GameObject Box(string hex, float w, float h, float d, float x, float y, float z, bool shadow = true, float yaw = 0)
        {
            var g = Mesh("Box", cube, Lit(hex), new Vector3(x, y, z), new Vector3(w, h, d), new Vector3(0, yaw, 0));
            if (!shadow) g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        GameObject Plane(Texture tex, float w, float h, float x, float y, float z, Vector3 euler)
        {
            var g = Mesh("Plane", quad, new Material(unlit) { mainTexture = tex }, new Vector3(x, y, z), new Vector3(w, h, 1), euler);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        Canvas8 Picture(int cw, int ch, float w, float h, float x, float y, float z, Vector3 euler, out GameObject plane)
        {
            var c = new Canvas8(cw, ch, mips: true);
            plane = Plane(c.Tex, w, h, x, y, z, euler);
            return c;
        }

        Canvas8 Picture(int cw, int ch, float w, float h, float x, float y, float z, Vector3 euler) => Picture(cw, ch, w, h, x, y, z, euler, out _);

        Canvas8 Plate(int cw, int ch, float w, float h, float x, float y, float z, float tilt) => Picture(cw, ch, w, h, x, y, z, new Vector3(tilt, 0, 0));

        public Room()
        {
            Lights();
            const string wall = "#50625c", floor = "#3b3a3e", trim = "#2f3d3a", ceil = "#6a6a60", desk = "#6b4a2e", leg = "#3c2a1a", metal = "#6e7268", housing = "#b8b4a0", tv = "#1a1a1e", btn = "#d8241c", baseCol = "#222222", machineCol = "#7a2020";

            // Shell
            Box(floor, 8, .1f, 7, 0, -.05f, 0, false);
            Box(wall, 8, 3, .1f, 0, 1.5f, 3.55f, false);
            Box(wall, .1f, 3, 7, -4.05f, 1.5f, 0, false);
            Box(wall, .1f, 3, 7, 4.05f, 1.5f, 0, false);
            Box(wall, 8, 3, .1f, 0, 1.5f, -3.55f, false);
            Box(ceil, 8, .1f, 7, 0, 3.05f, 0, false);
            Box(trim, 8, .4f, .11f, 0, .2f, 3.54f, false);
            Box(trim, .11f, .4f, 7, -4.04f, .2f, 0, false);
            Box(trim, .11f, .4f, 7, 4.04f, .2f, 0, false);

            // Window at night
            var win = Picture(256, 192, 1.6f, 1.2f, -2.2f, 1.75f, 3.49f, Vector3.zero);
            win.Fill(Hex("#0c1430"));
            for (int i = 0; i < 90; i++) win.Rect(i * 67 % 250, 70 + i * 41 % 118, 3, 4, Hex(i % 3 != 0 ? "#e8c060" : "#7fa0d0"));
            win.Apply();
            for (int i = 0; i < 9; i++) Box("#8f8a78", 1.66f, .025f, .03f, -2.2f, 1.2f + i * .135f, 3.46f);

            // Cabinets
            for (int i = 0; i < 3; i++)
            {
                float x = 1.7f + i * .72f;
                Box(metal, .68f, 1.4f, .6f, x, .7f, 3.18f);
                for (int d = 0; d < 3; d++) Box("#2a2a28", .1f, .03f, .03f, x, .25f + d * .45f, 2.87f);
            }

            // Poster
            var poster = Picture(256, 340, .75f, 1, 3.2f, 2.2f, 3.49f, Vector3.zero);
            poster.Fill(Hex("#D8241C"));
            string[] words = { "LUCK", "IS A", "BUDGET", "ITEM" };
            for (int i = 0; i < 4; i++) poster.Centred(words[i], 48 + i * 64, Hex(i == 3 ? "#FFE14A" : "#F2F2E6"), 5);
            poster.Apply();

            // Desk and small props
            Box(desk, 1.9f, .06f, .95f, 0, .74f, 2.9f);
            foreach (var (x, z) in new[] { (-.88f, 3.3f), (.88f, 3.3f), (-.88f, 2.5f), (.88f, 2.5f) }) Box(leg, .06f, .72f, .06f, x, .36f, z);
            Box("#d8d0b8", .08f, .1f, .08f, .88f, .82f, 2.95f); // mug
            Box("#222222", .14f, .03f, .14f, -.85f, .785f, 3.1f); // lamp
            Box("#222222", .02f, .5f, .02f, -.85f, 1.02f, 3.1f);
            Box("#c8a040", .18f, .08f, .12f, -.85f, 1.28f, 3.05f);

            // Calendar: inspections are known from week 1.
            Box("#3a3a38", .28f, .22f, .01f, -.72f, 1, 3.17f, true, -17.2f);
            Box("#3a3a38", .02f, .1f, .02f, -.72f, .85f, 3.18f);
            Calendar = Picture(256, 200, .26f, .2f, -.718f, 1, 3.163f, new Vector3(0, -17.2f, 0));

            // Monitor. The board never leaves it, so it keeps a bare unlit screen until the CRT shader.
            Box(housing, .9f, .56f, .5f, 0, 1.06f, 3.2f);
            Box(housing, .3f, .05f, .3f, 0, .795f, 3.1f);
            MonitorScreen = Plane(Monitor.Tex, .8f, .45f, 0, 1.06f, 2.945f, Vector3.zero).AddComponent<BoxCollider>();

            // Wall TV
            Box(tv, 1.7f, 1, .14f, -3.93f, 1.85f, .8f, true, -90);
            Plane(Tv.Tex, 1.54f, .87f, -3.85f, 1.85f, .8f, new Vector3(0, -90, 0));

            // Ball machine
            Box(machineCol, .6f, .8f, .6f, 3.1f, .4f, 1.4f);
            var glass = Mesh("Glass", sphere, Resources.Load<Material>("NW_Glass"), new Vector3(3.1f, 1.25f, 1.4f), Vector3.one * .9f);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            string[] ballCols = { "#ffffff", "#FFD23A", "#E8431F" };
            for (int i = 0; i < Balls.Length; i++) Balls[i] = Mesh("Ball", sphere, Lit(ballCols[i % 3]), Vector3.zero, Vector3.one * .14f).transform;
            MachineFace = Picture(128, 72, .5f, .28f, 2.795f, .5f, 1.4f, new Vector3(0, 90, 0));

            // Cards
            for (int i = 0; i < CardCount; i++)
            {
                CardFaces[i] = new Canvas8(128, 176, mips: true);
                var card = Plane(CardFaces[i].Tex, .125f, .172f, (i - 2.5f) * CardStep, CardBaseY, CardZ, CardEuler);
                card.AddComponent<BoxCollider>();
                Cards[i] = card.transform;
            }

            // Announce and eject
            var go = Mesh("GoButton", cylinder, Lit(btn), new Vector3(.62f, .795f, 2.8f), new Vector3(.13f, .025f, .13f));
            GoButton = go.AddComponent<BoxCollider>();
            GoCap = go.transform;
            Box(baseCol, .2f, .02f, .2f, .62f, .78f, 2.8f);
            UndoButton = Box("#8a8a90", .12f, .04f, .08f, -.66f, .79f, 2.78f).AddComponent<BoxCollider>();
            GoPlate = Plate(256, 48, .26f, .05f, .62f, .79f, 3, 50);
            UndoPlate = Plate(128, 48, .12f, .045f, -.66f, .772f, 2.7f, 90);

            // Off-air desk actions and the go-live switch, in the gap between monitor and cards.
            var phone = Box("#a02820", .2f, .04f, .11f, -.3f, .79f, 2.88f);
            Box("#7a1c16", .18f, .035f, .04f, -.3f, .825f, 2.88f);
            var envelope = Box("#d8c89a", .17f, .02f, .1f, -.06f, .78f, 2.88f);
            Box("#a02820", .03f, .006f, .03f, -.06f, .793f, 2.88f);
            var shredder = Box("#3a3a40", .16f, .06f, .1f, .17f, .8f, 2.88f);
            Box("#0a0a0a", .12f, .004f, .012f, .17f, .832f, 2.88f);
            var live = Box("#2a2a30", .12f, .03f, .1f, .38f, .785f, 2.88f);
            Lever = Box("#e8e2cc", .025f, .07f, .025f, .38f, .825f, 2.88f).transform;
            ActionProps[DeskAction.Supplier] = phone.AddComponent<BoxCollider>();
            ActionProps[DeskAction.Bribe] = envelope.AddComponent<BoxCollider>();
            ActionProps[DeskAction.Shred] = shredder.AddComponent<BoxCollider>();
            LiveSwitch = live.AddComponent<BoxCollider>();
            ActionPlates[DeskAction.Supplier] = Plate(192, 30, .19f, .03f, -.3f, .812f, 2.8f, 28.6f);
            ActionPlates[DeskAction.Bribe] = Plate(192, 30, .19f, .03f, -.06f, .812f, 2.8f, 28.6f);
            ActionPlates[DeskAction.Shred] = Plate(192, 30, .19f, .03f, .17f, .812f, 2.8f, 28.6f);
            LivePlate = Plate(192, 30, .19f, .03f, .38f, .812f, 2.8f, 28.6f);

            // The job, and the clock on the desk.
            Box("#3a3a38", .015f, .06f, .015f, .74f, .945f, 3.15f);
            var memo = Picture(160, 70, .3f, .13f, .74f, 1.04f, 3.14f, new Vector3(0, 11.5f, 0));
            memo.Fill(Hex("#F4EFDC"));
            memo.Centred("CHANGE THE NUMBER.", 18, Hex("#1C1E28"));
            memo.Centred("NOBODY MAY WIN.", 42, Hex("#B3241C"));
            memo.Apply();
            Box("#1a1a1e", .26f, .15f, .1f, .74f, .845f, 3.14f);
            Clock = Picture(128, 64, .23f, .12f, .74f, .845f, 3.088f, Vector3.zero);

            // Punch-card reader
            Reader = Box("#9a9484", .3f, .14f, .22f, -.6f, .84f, 3).transform;
            Box("#111111", .2f, .012f, .02f, -.6f, .87f, 2.885f);
            ReaderLamp = Box("#3a1a10", .03f, .02f, .01f, -.5f, .815f, 2.885f, false).GetComponent<Renderer>();
        }

        public void SetLamp(bool on) => ReaderLamp.sharedMaterial = Lit(on ? "#ff5a2a" : "#3a1a10");

        void Lights()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#8fa0c8");
            RenderSettings.ambientEquatorColor = Hex("#5d6070");
            RenderSettings.ambientGroundColor = Hex("#2a2018");
            RenderSettings.skybox = null;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("#fff1d6");
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.SetPositionAndRotation(new Vector3(2.5f, 2.8f, -1), Quaternion.LookRotation(new Vector3(0, .7f, 2.6f) - new Vector3(2.5f, 2.8f, -1)));

            Point("Lamp", "#ffb66b", 1.6f, 5, new Vector3(-.85f, 1.4f, 3));
            Point("Glow", "#ff50b0", .15f, 2.5f, new Vector3(0, 1.05f, 2.6f)); // A hint of the monitor's pink.
        }

        static void Point(string name, string hex, float intensity, float range, Vector3 pos)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Hex(hex);
            l.intensity = intensity;
            l.range = range;
            l.transform.position = pos;
        }
    }
}
