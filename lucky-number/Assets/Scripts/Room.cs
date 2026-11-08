using System.Collections.Generic;
using UnityEngine;

namespace NoWinners
{
    // The room is the art from tools/art/build.mjs, saved as Art/Resources/NW_RoomArt. This adds what the game needs on top:
    // textures you can draw on, invisible click targets and the parts that move.
    // The art was laid out with the player looking along -Z, so it's turned 180 degrees: its x stays and its z flips.
    public class Room
    {
        public const int CardCount = 6;
        public const float CardBaseY = 0.8f, CardZ = 2.66f, CardStep = 0.145f;
        public static readonly Vector3 CardEuler = new Vector3(75.7f, 0, 0), ReaderSlot = new Vector3(-0.54f, 0.9f, 2.96f);
        public static readonly Vector3 GoRest = new Vector3(.6f, .788f, 2.72f);

        public readonly Canvas8 Monitor = new Canvas8(640, 360), Tv = new Canvas8(640, 360);
        public readonly Canvas8 Calendar = new Canvas8(256, 320, mips: true), Paper = new Canvas8(256, 300, mips: true);
        public readonly Canvas8 MachineFace = new Canvas8(128, 72), Clock = new Canvas8(128, 64);
        public readonly Canvas8 GoPlate, UndoPlate, LivePlate;
        public readonly Canvas8[] CardFaces = new Canvas8[CardCount];
        public readonly Transform[] Cards = new Transform[CardCount];
        public readonly Transform[] Balls = new Transform[14];
        public readonly Collider MonitorScreen, GoButton, UndoButton, LiveSwitch;
        public readonly Dictionary<DeskAction, Collider> ActionProps = new Dictionary<DeskAction, Collider>();
        public readonly Transform Lever, GoCap;

        readonly List<Material> crt = new List<Material>();
        public void SetCrt(float strength) { foreach (var m in crt) m.SetFloat("_Strength", strength); }

        readonly Renderer readerLamp;
        readonly Material lampOn, lampOff;
        readonly Material unlit = Resources.Load<Material>("NW_Unlit");
        readonly Mesh quad = MeshOf(PrimitiveType.Quad);
        readonly Transform root = new GameObject("Room").transform, art;

        static Mesh MeshOf(PrimitiveType t)
        {
            var g = GameObject.CreatePrimitive(t);
            var m = g.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(g);
            return m;
        }

        public static Color32 Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? (Color32)c : default;

        // Points one material on a prop at a texture the game draws on.
        void Show(string prop, string material, Texture tex, string slot = "_BaseMap")
        {
            foreach (var r in art.Find(prop).GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i].name != material) continue;
                    mats[i] = new Material(mats[i]);
                    mats[i].SetTexture(slot, tex);
                    if (mats[i].HasProperty("_Strength")) crt.Add(mats[i]);
                    if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", Color.white);
                }
                r.sharedMaterials = mats;
            }
        }

        Collider Hit(string name, Vector3 pos, Vector3 size)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root);
            g.transform.position = pos;
            g.transform.localScale = size;
            return g.AddComponent<BoxCollider>();
        }

        GameObject Plane(Texture tex, float w, float h, float x, float y, float z, Vector3 euler)
        {
            var g = new GameObject("Plane");
            g.transform.SetParent(root);
            g.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(euler));
            g.transform.localScale = new Vector3(w, h, 1);
            g.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = new Material(unlit) { mainTexture = tex };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        // Live text on one of the art's label wedges. x and z are the wedge's, from desk_plates in build.mjs.
        Canvas8 Plate(int cw, int ch, float w, float h, float x, float z)
        {
            var c = new Canvas8(cw, ch, mips: true);
            Plane(c.Tex, w, h, x, .8155f, 2.9f - z - .0015f, new Vector3(28.6f, 0, 0));
            return c;
        }

        public Room()
        {
            Lights();
            var prefab = Resources.Load<GameObject>("NW_RoomArt");
            if (!prefab) throw new MissingReferenceException("NW_RoomArt is missing. Run No Winners > Build Art Prefabs and Room.");
            art = Object.Instantiate(prefab, root).transform;
            art.name = "Art";
            art.rotation = Quaternion.Euler(0, 180, 0);

            Show("monitor", "screen_monitor", Monitor.Tex, "_MainTex");
            Show("tv", "screen_tv", Tv.Tex, "_MainTex");
            Show("machine_base", "machine_face", MachineFace.Tex);
            Show("clock", "clock_face", Clock.Tex);
            Show("calendar", "calendar", Calendar.Tex);
            Show("newspaper", "newspaper", Paper.Tex);

            // Balls: one model, with the number picked by the texture offset.
            var ball = art.Find("ball");
            for (int i = 0; i < Balls.Length; i++)
            {
                var b = Object.Instantiate(ball, art);
                b.gameObject.SetActive(true);
                int n = (i * 7 + 3) % 100;
                var r = b.GetComponentInChildren<Renderer>();
                r.material.mainTextureOffset = new Vector2(n % 10 / 10f, .9f - n / 10 / 10f);
                Balls[i] = b;
            }

            // Cards are drawn by the game, so they stay plain quads.
            for (int i = 0; i < CardCount; i++)
            {
                CardFaces[i] = new Canvas8(128, 176, mips: true);
                var card = Plane(CardFaces[i].Tex, .125f, .172f, (i - 2.5f) * CardStep, CardBaseY, CardZ, CardEuler);
                card.AddComponent<BoxCollider>();
                Cards[i] = card.transform;
            }

            // Click targets. A unit box scaled to the screen, so the board's square maths works in local space.
            MonitorScreen = Hit("MonitorScreen", new Vector3(0, 1.063f, 2.95f), new Vector3(.8f, .45f, .01f));
            GoButton = Hit("GoButton", new Vector3(.6f, .8f, 2.72f), new Vector3(.2f, .06f, .2f));
            UndoButton = Hit("UndoButton", new Vector3(-.53f, .79f, 2.72f), new Vector3(.14f, .04f, .1f));
            LiveSwitch = Hit("LiveSwitch", new Vector3(.55f, .82f, 2.97f), new Vector3(.14f, .12f, .1f));
            // Desk actions sit on the side table, seen when you look right at the machine.
            ActionProps[DeskAction.Supplier] = Hit("Phone", new Vector3(1.75f, .85f, 1.9f), new Vector3(.17f, .1f, .22f));
            ActionProps[DeskAction.Bribe] = Hit("Envelope", new Vector3(1.75f, .815f, 2.17f), new Vector3(.12f, .03f, .19f));
            ActionProps[DeskAction.Shred] = Hit("Shredder", new Vector3(1.75f, .85f, 2.46f), new Vector3(.12f, .09f, .18f));

            // Live labels. Supplier, Bribe and Shred are explained on the wall poster instead.
            LivePlate = Plate(192, 30, .15f, .025f, .55f, .005f);
            GoPlate = Plate(256, 48, .17f, .032f, .6f, .3f);
            UndoPlate = Plate(192, 32, .12f, .02f, -.53f, .28f);

            // Moving parts.
            GoCap = art.Find("big_button_cap");
            Lever = new GameObject("Lever").transform;
            Lever.SetParent(root);
            Lever.position = new Vector3(.55f, .801f, 2.97f);
            art.Find("switch_lever").SetParent(Lever, true);

            readerLamp = art.Find("card_reader_lamp").GetComponentInChildren<Renderer>();
            lampOn = readerLamp.sharedMaterial;
            lampOff = new Material(lampOn) { color = Hex("#3a1a10"), mainTexture = null };
            SetLamp(false);
        }

        public void SetLamp(bool on) => readerLamp.sharedMaterial = on ? lampOn : lampOff;

        // Same values as the art's layout.json, whose three.js units are pi times Unity's.
        void Lights()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = (Color)Hex("#8fa0c8") * .286f;
            RenderSettings.ambientGroundColor = (Color)Hex("#2a2018") * .286f;
            RenderSettings.ambientEquatorColor = (RenderSettings.ambientSkyColor + RenderSettings.ambientGroundColor) * .5f;
            RenderSettings.skybox = null;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("#fff1d6");
            sun.intensity = .48f;
            sun.shadows = LightShadows.Soft;
            sun.transform.SetPositionAndRotation(new Vector3(2.5f, 2.8f, -1), Quaternion.LookRotation(new Vector3(0, .7f, 2.6f) - new Vector3(2.5f, 2.8f, -1)));

            Point("Lamp", "#ffb66b", .95f, 5, new Vector3(-.77f, 1.25f, 2.68f));
            Point("Glow", "#ff50b0", .064f, 2.5f, new Vector3(0, 1.05f, 2.6f)); // A hint of the monitor's pink.
            Point("Table Lamp", "#ffb66b", .48f, 2.5f, new Vector3(1.85f, 1.3f, 2.53f)); // Lights the desk actions on the side table.
            Point("Ceiling", "#e8f0e0", 2.2f, 9, new Vector3(0, 2.85f, 1.6f)); // Keeps the TV and machine readable.
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
