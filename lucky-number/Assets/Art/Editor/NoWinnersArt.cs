using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Import rules for the generated art, plus menu items that turn it into materials, prefabs and a dressed room.
// The source is tools/art/build.mjs. Everything under Models and Textures gets overwritten by it.
public class NoWinnersArtImport : AssetPostprocessor
{
    public override uint GetVersion() => 3; // Raise this when the rules change, so existing assets reimport.

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(NoWinnersArt.Root + "/Textures")) return;
        var t = (TextureImporter)assetImporter;
        // Paper on the desk is seen small and at an angle, where point filtering garbles the words.
        var paper = assetPath.Contains("/Desk/") || assetPath.Contains("/Walls/");
        t.filterMode = paper ? FilterMode.Trilinear : FilterMode.Point;
        t.mipmapEnabled = paper;
        t.anisoLevel = paper ? 8 : 1;
        t.textureCompression = TextureImporterCompression.Uncompressed;
        t.npotScale = TextureImporterNPOTScale.None;
        t.wrapMode = assetPath.Contains("ball_numbers") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        t.alphaIsTransparency = true;
        // Screens and sprites get blitted into the 640x360 screen textures at runtime.
        t.isReadable = assetPath.Contains("/Screens/") || assetPath.Contains("/Sprites/");
        if (assetPath.Contains("cursor")) t.textureType = TextureImporterType.Cursor;
    }

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(NoWinnersArt.Root + "/Models")) return;
        var m = (ModelImporter)assetImporter;
        m.globalScale = 1;
        m.useFileScale = false;
        m.importNormals = ModelImporterNormals.Import;
        m.importTangents = ModelImporterTangents.None;
        m.importAnimation = false;
        m.importBlendShapes = false;
        m.importCameras = false;
        m.importLights = false;
        m.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        m.materialLocation = ModelImporterMaterialLocation.InPrefab;
    }

    // Sound effects come from tools/sfx/build.mjs. Short ones stay ADPCM for instant, cheap playback;
    // loops and long broadcast pieces use Vorbis to keep the build small.
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(NoWinnersArt.Root + "/Resources/Audio")) return;
        var a = (AudioImporter)assetImporter;
        var longClip = new FileInfo(assetPath).Length > 44100 * 2 * 3; // Over about 3 s of mono.
        a.forceToMono = false;
        a.loadInBackground = false;
        var s = a.defaultSampleSettings;
        s.loadType = AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = longClip ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
        s.quality = 0.7f;
        s.preloadAudioData = true; // No hitch the first time a sound plays.
        s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        a.defaultSampleSettings = s;
    }
}

public static class NoWinnersArt
{
    public const string Root = "Assets/Art";
    const string Lit = "Universal Render Pipeline/Lit", Unlit = "Universal Render Pipeline/Unlit", Crt = "No Winners/CRT Screen";

    [System.Serializable] class Item { public string model; public float[] pos; public float rotY; }
    [System.Serializable] class Light3 { public string color, sky, ground; public float intensity, range; public float[] pos, target; }
    [System.Serializable] class Lights { public Light3 hemi, sun, lamp, glow, ceiling, table; }
    [System.Serializable] class Cam { public float fov; public float[] eye, lookMonitor, lookTV, lookMachine; }
    [System.Serializable] class Layout { public Item[] items; public Lights lights; public Cam camera; }

    static Layout Load() => JsonUtility.FromJson<Layout>(File.ReadAllText(Root + "/layout.json"));

    // The art is authored right-handed and the importer mirrors X, so placement mirrors too.
    static Vector3 V(float[] p) => new Vector3(-p[0], p[1], p[2]);
    static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    static Material Mat(string name, string shader, string texture = null, Vector2? tiling = null, Vector2? offset = null)
    {
        var path = $"{Root}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find(shader);
        if (texture != null)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/{texture}");
            var slot = shader == Crt ? "_MainTex" : "_BaseMap";
            m.SetTexture(slot, t);
            m.SetTextureScale(slot, tiling ?? Vector2.one);
            m.SetTextureOffset(slot, offset ?? Vector2.zero);
        }
        if (shader == Lit) { m.SetFloat("_Smoothness", 0); m.SetFloat("_SpecularHighlights", 0); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Dictionary<string, Material> Materials()
    {
        Directory.CreateDirectory(Root + "/Materials");
        AssetDatabase.Refresh();
        var d = new Dictionary<string, Material>
        {
            ["palette"] = Mat("palette", Lit, "palette.png"),
            ["glow"] = Mat("glow", Unlit, "palette.png"),
            ["screen_monitor"] = Mat("screen_monitor", Crt, "Mocks/mock_monitor.png"),
            ["screen_tv"] = Mat("screen_tv", Crt, "Mocks/mock_tv.png"),
            ["machine_face"] = Mat("machine_face", Unlit),
            ["clock_face"] = Mat("clock_face", Unlit),
            ["window_night"] = Mat("window_night", Unlit, "Desk/window_night.png"),
            ["poster"] = Mat("poster", Lit, "Desk/poster.png"),
            ["calendar"] = Mat("calendar", Lit, "Desk/calendar.png"),
            ["memo"] = Mat("memo", Lit, "Desk/memo.png"),
            ["newspaper"] = Mat("newspaper", Lit, "Desk/newspaper_clean.png"),
            ["plates"] = Mat("plates", Unlit, "Desk/plates.png"),
            ["card_face"] = Mat("card_face", Unlit, "Desk/card_faces.png", new Vector2(1 / 12f, 1 / 4f), new Vector2(0, 3 / 4f)),
            ["ball_numbers"] = Mat("ball_numbers", Lit, "Desk/ball_numbers.png", new Vector2(.1f, .1f), new Vector2(.7f, .6f)),
        };
        // Wall pictures: one material per texture in Walls, named after the file.
        foreach (var f in Directory.GetFiles(Root + "/Textures/Walls", "*.png"))
        {
            var n = Path.GetFileNameWithoutExtension(f);
            d[n] = Mat(n, Lit, "Walls/" + Path.GetFileName(f));
        }
        foreach (var n in new[] { "machine_face", "clock_face" }) d[n].SetColor("_BaseColor", C("#080808"));

        var card = d["card_face"];
        card.SetFloat("_AlphaClip", 1); card.SetFloat("_Cutoff", .5f); card.EnableKeyword("_ALPHATEST_ON");
        card.renderQueue = (int)RenderQueue.AlphaTest;

        var glass = Mat("glass", Lit);
        glass.SetColor("_BaseColor", new Color(.81f, .91f, 1, .22f));
        glass.SetFloat("_Surface", 1); glass.SetFloat("_Blend", 0); glass.SetFloat("_ZWrite", 0);
        glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.renderQueue = (int)RenderQueue.Transparent;
        d["glass"] = glass;
        return d;
    }

    const string RoomPrefab = Root + "/Resources/NW_RoomArt.prefab";

    // The game loads the room at runtime, so keep its prefab current after the art is regenerated.
    [InitializeOnLoadMethod]
    static void KeepRoomPrefabCurrent() => EditorApplication.delayCall += () =>
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Root + "/layout.json")) return;
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/desk.obj")) return;
        if (File.Exists(RoomPrefab) && File.GetLastWriteTimeUtc(RoomPrefab) >= File.GetLastWriteTimeUtc(Root + "/layout.json")) return;
        BuildPrefabs();
        AssetDatabase.SaveAssets();
    };

    public static Dictionary<string, GameObject> BuildPrefabs()
    {
        var mats = Materials();
        Directory.CreateDirectory(Root + "/Prefabs");
        AssetDatabase.Refresh();
        var layout = Load();
        var prefabs = new Dictionary<string, GameObject>();
        foreach (var f in Directory.GetFiles(Root + "/Models", "*.obj"))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace('\\', '/')));
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] && mats.TryGetValue(slots[i].name, out var m)) slots[i] = m;
                    else Debug.LogError($"No material for slot '{(slots[i] ? slots[i].name : "null")}' on {name}");
                }
                r.sharedMaterials = slots;
                if (name == "machine_dome") r.shadowCastingMode = ShadowCastingMode.Off;
            }
            prefabs[name] = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(go);
        }

        // Everything placed, as one prefab the game can load. The ball is a hidden template.
        Directory.CreateDirectory(Root + "/Resources");
        AssetDatabase.Refresh();
        var room = new GameObject("NW_RoomArt");
        foreach (var it in layout.items)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[it.model]);
            go.transform.SetParent(room.transform);
            go.transform.SetPositionAndRotation(V(it.pos), Quaternion.Euler(0, -it.rotY * Mathf.Rad2Deg, 0));
            go.name = it.model;
        }
        var ball = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["ball"]);
        ball.transform.SetParent(room.transform);
        ball.name = "ball";
        ball.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(room, RoomPrefab);
        Object.DestroyImmediate(room);
        return prefabs;
    }

    [MenuItem("No Winners/Build Art Prefabs and Room")]
    public static void Build()
    {
        var prefabs = BuildPrefabs();
        var layout = Load();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (var it in layout.items)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[it.model], scene);
            go.transform.SetPositionAndRotation(V(it.pos), Quaternion.Euler(0, -it.rotY * Mathf.Rad2Deg, 0));
        }

        // desk.html's values are three.js physical units, where the diffuse term carries a 1/pi that Unity's lights don't.
        const float toUnity = 1 / Mathf.PI;
        var L = layout.lights;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = C(L.hemi.sky) * L.hemi.intensity * toUnity;
        RenderSettings.ambientGroundColor = C(L.hemi.ground) * L.hemi.intensity * toUnity;
        RenderSettings.ambientEquatorColor = (RenderSettings.ambientSkyColor + RenderSettings.ambientGroundColor) * .5f;

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.color = C(L.sun.color); sun.intensity = L.sun.intensity * toUnity; sun.shadows = LightShadows.Soft;
        sun.transform.position = V(L.sun.pos);
        sun.transform.LookAt(V(L.sun.target));
        void Point(string n, Light3 l)
        {
            var p = new GameObject(n).AddComponent<Light>();
            p.type = LightType.Point; p.color = C(l.color); p.intensity = l.intensity * toUnity; p.range = l.range;
            p.transform.position = V(l.pos);
        }
        Point("Lamp", L.lamp); Point("Monitor Glow", L.glow); Point("Ceiling", L.ceiling); Point("Table Lamp", L.table);

        var cam = new GameObject("Player Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera"; cam.fieldOfView = layout.camera.fov; cam.nearClipPlane = .05f; cam.farClipPlane = 40;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        cam.transform.position = V(layout.camera.eye);
        cam.transform.LookAt(V(layout.camera.lookMonitor));

        EditorSceneManager.SaveScene(scene, Root + "/DeskRoom.unity");
        AssetDatabase.SaveAssets();
        Debug.Log($"No Winners art: {prefabs.Count} prefabs, {layout.items.Length} placed in {Root}/DeskRoom.unity");
    }

    // The project starts on the 2D Renderer, which ignores 3D lights. The GDD calls for this switch.
    [MenuItem("No Winners/Switch URP to Universal Renderer")]
    public static void UseUniversalRenderer()
    {
        const string path = "Assets/Settings/UniversalRenderer.asset";
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if (!data) { data = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(data, path); }
        // Assigned per quality level in this project, so GraphicsSettings.defaultRenderPipeline is null.
        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/UniversalRP.asset"));
        so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = data;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("No Winners art: URP now uses " + path);
    }

    // For checking the art from the command line: -executeMethod NoWinnersArt.Capture -captureDir <folder>
    public static void Capture()
    {
        var args = System.Environment.GetCommandLineArgs();
        var dir = args[System.Array.IndexOf(args, "-captureDir") + 1];
        var scene = EditorSceneManager.OpenScene(Root + "/DeskRoom.unity");
        var layout = Load();
        var cam = Camera.main;
        var views = new Dictionary<string, float[]> { ["desk"] = layout.camera.lookMonitor, ["tv"] = layout.camera.lookTV, ["machine"] = layout.camera.lookMachine };
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render(); // The first frame after opening a scene can render before lighting is ready.
        foreach (var kv in views)
        {
            cam.transform.LookAt(V(kv.Value));
            cam.Render();
            RenderTexture.active = rt;
            var img = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            File.WriteAllBytes(Path.Combine(dir, kv.Key + ".png"), img.EncodeToPNG());
        }
        cam.targetTexture = null;
        Debug.Log("No Winners art: captured to " + dir);
    }
}
