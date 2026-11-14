using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Tempulli i Humbur - sets up the jungle-temple atmosphere in the currently open scene (URP).
// Menu: Tools > Tempulli > Setup Atmosphere
// Never deletes or moves existing objects: existing lights/volumes/cameras are updated in place,
// missing ones are created. Everything done to the scene is one Undo step (Ctrl+Z).
public static class TempleSceneSetup
{
    // ---------- Tweakable values (see the notes at the end of the chat / top of each section) ----------

    // 1. Sun: warm golden late-afternoon light (#FFD8A0), low angle
    static readonly Color SunColor = new Color(1.00f, 0.847f, 0.627f); // #FFD8A0
    const float SunIntensity = 2.0f;       // higher = brighter scene
    const float SunPitch = 25f;            // X rotation: lower = longer shadows, more "evening"
    const float SunShadowStrength = 0.8f;  // 0 = no shadows, 1 = fully dark shadows

    // 2. Fog: exponential, greenish-grey
    static readonly Color FogColor = new Color(0.55f, 0.62f, 0.55f);
    const float FogDensity = 0.012f;       // higher = thicker mist (0.005 light ... 0.03 heavy)

    // 3. Ambient: darker with a green jungle tint (Trilight = sky / horizon / ground colors)
    static readonly Color AmbientSky     = new Color(0.36f, 0.43f, 0.38f);
    static readonly Color AmbientEquator = new Color(0.25f, 0.31f, 0.23f);
    static readonly Color AmbientGround  = new Color(0.12f, 0.14f, 0.10f);

    // 4. Post-processing
    const float BloomIntensity = 0.35f;
    const float BloomThreshold = 1.1f;
    const float PostExposure = 0f;         // EV: +0.5 brighter, -0.5 darker
    const float Contrast = 15f;            // -100..100
    const float Saturation = 5f;           // -100..100
    static readonly Color WarmFilter = new Color(1.00f, 0.96f, 0.90f); // slight warm tint
    const float VignetteIntensity = 0.22f;

    // 5. Terrain (only created when the scene has none)
    const float TerrainSize = 200f;
    const float TerrainMaxHeight = 20f;    // terrain height range in meters
    const float HillAmount = 0.3f;         // fraction of max height the hills reach (0.3 * 20 = 6 m)
    const float FlatCenterRadius = 30f;    // keep the middle flat so it doesn't cover the play area

    const string AssetFolder = "Assets/Settings/Atmosphere";
    const string NewProfilePath = AssetFolder + "/Tempulli_Atmosphere_Profile.asset";

    [MenuItem("Tools/Tempulli/Setup Atmosphere")]
    public static void SetupAtmosphere()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("[Tempulli] No open scene.");
            return;
        }

        // Group everything below into a single Undo entry
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Tempulli Setup Atmosphere");

        var report = new List<string>();
        SetupSun(scene, report);
        SetupFogAndAmbient(report);
        SetupPostProcessing(scene, report);
        SetupTerrain(scene, report);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Tempulli] Atmosphere set up:\n- " + string.Join("\n- ", report));
    }

    // ---------- 1. Directional light ----------
    static void SetupSun(Scene scene, List<string> report)
    {
        // Prefer the light already set as the scene's Sun, otherwise the first directional light
        Light sun = RenderSettings.sun;
        if (sun == null || sun.type != LightType.Directional || sun.gameObject.scene != scene)
        {
            sun = null;
            foreach (var l in FindInScene<Light>(scene))
                if (l.type == LightType.Directional) { sun = l; break; }
        }

        float yaw = -150f; // default facing for a new light
        if (sun == null)
        {
            var go = new GameObject("Directional Light (Sun)");
            Undo.RegisterCreatedObjectUndo(go, "Create Sun");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            report.Add("Created Directional Light");
        }
        else
        {
            Undo.RecordObject(sun, "Update Sun");
            Undo.RecordObject(sun.transform, "Update Sun");
            yaw = sun.transform.eulerAngles.y; // keep its compass direction, only change the height angle
            report.Add("Updated Directional Light '" + sun.name + "'");
        }

        // Rotation only (position untouched): low sun = long, dramatic shadows
        sun.transform.rotation = Quaternion.Euler(SunPitch, yaw, 0f);
        sun.color = SunColor;
        sun.intensity = SunIntensity;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = SunShadowStrength;
        EditorUtility.SetDirty(sun);
        EditorUtility.SetDirty(sun.transform);

        RecordRenderSettings();
        RenderSettings.sun = sun;
    }

    // ---------- 2 + 3. Fog and ambient light ----------
    static void SetupFogAndAmbient(List<string> report)
    {
        RecordRenderSettings();

        // Exponential fog: thickens smoothly with distance -> misty, mysterious depth
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogDensity = FogDensity;

        // Trilight ambient: darker overall, green tint from the jungle canopy, very dark ground
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor = AmbientGround;

        report.Add("Fog: exponential, density " + FogDensity + "; ambient: green Trilight");
    }

    // ---------- 4. Post-processing (URP Global Volume) ----------
    static void SetupPostProcessing(Scene scene, List<string> report)
    {
        // Reuse an existing global Volume if the scene has one
        Volume volume = null;
        foreach (var v in FindInScene<Volume>(scene))
            if (v.isGlobal) { volume = v; break; }

        if (volume == null)
        {
            var go = new GameObject("Global Volume");
            Undo.RegisterCreatedObjectUndo(go, "Create Global Volume");
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            report.Add("Created Global Volume");
        }
        else
        {
            Undo.RecordObject(volume, "Update Global Volume");
            report.Add("Updated Global Volume '" + volume.name + "'");
        }

        // Update the volume's existing profile; create a profile asset only if it has none
        VolumeProfile profile = volume.sharedProfile;
        if (profile == null)
        {
            EnsureFolder(AssetFolder);
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(NewProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, NewProfilePath);
            }
            volume.sharedProfile = profile;
        }
        volume.weight = 1f;
        EditorUtility.SetDirty(volume);

        // Bloom: subtle glow on bright spots (sun, torches), not a haze over everything
        var bloom = GetOrAdd<Bloom>(profile);
        bloom.intensity.Override(BloomIntensity);
        bloom.threshold.Override(BloomThreshold);
        bloom.scatter.Override(0.6f);

        // Color Adjustments: slightly warm, a bit more contrast
        var color = GetOrAdd<ColorAdjustments>(profile);
        color.postExposure.Override(PostExposure);
        color.contrast.Override(Contrast);
        color.saturation.Override(Saturation);
        color.colorFilter.Override(WarmFilter);

        // Vignette: light darkening of the screen edges to focus the eye
        var vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(VignetteIntensity);
        vignette.smoothness.Override(0.4f);
        vignette.color.Override(Color.black);

        // Tonemapping: ACES for a filmic look
        var tone = GetOrAdd<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);

        EditorUtility.SetDirty(profile);
        report.Add("Profile '" + profile.name + "': Bloom, Color Adjustments, Vignette, Tonemapping (ACES)");

        // URP only draws post-processing on cameras that have it switched on
        Camera cam = Camera.main;
        if (cam == null || cam.gameObject.scene != scene)
        {
            cam = null;
            foreach (var c in FindInScene<Camera>(scene)) { cam = c; break; }
        }
        if (cam != null)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = Undo.AddComponent<UniversalAdditionalCameraData>(cam.gameObject);
            else Undo.RecordObject(data, "Enable Post Processing");
            data.renderPostProcessing = true;
            EditorUtility.SetDirty(data);
            report.Add("Post Processing enabled on camera '" + cam.name + "'");
        }
    }

    // Returns the profile's component of type T (activating it), or adds one undoably
    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T comp))
        {
            Undo.RecordObject(comp, "Update " + typeof(T).Name);
        }
        else
        {
            Undo.RecordObject(profile, "Add " + typeof(T).Name);
            comp = profile.Add<T>(false);
            comp.name = typeof(T).Name;
            comp.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            // Store the component inside the profile asset so it is saved with it
            if (AssetDatabase.Contains(profile))
                AssetDatabase.AddObjectToAsset(comp, profile);
            Undo.RegisterCreatedObjectUndo(comp, "Add " + typeof(T).Name);
        }
        comp.active = true;
        EditorUtility.SetDirty(comp);
        return comp;
    }

    // ---------- 5. Terrain ----------
    static void SetupTerrain(Scene scene, List<string> report)
    {
        foreach (var t in FindInScene<Terrain>(scene))
        {
            report.Add("Terrain already exists ('" + t.name + "'), left unchanged");
            return;
        }

        EnsureFolder(AssetFolder);

        // Heightmap: two layers of Perlin noise = soft rolling hills
        var data = new TerrainData();
        data.heightmapResolution = 257;
        data.size = new Vector3(TerrainSize, TerrainMaxHeight, TerrainSize);
        int res = data.heightmapResolution;
        var heights = new float[res, res];
        float seedX = Random.Range(0f, 1000f), seedZ = Random.Range(0f, 1000f);
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float u = (float)x / (res - 1), v = (float)z / (res - 1);
                float n = Mathf.PerlinNoise(seedX + u * 3f, seedZ + v * 3f) * 0.75f
                        + Mathf.PerlinNoise(seedX + u * 9f, seedZ + v * 9f) * 0.25f;
                // Flatten towards the center so hills don't cover the play area
                float dist = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * TerrainSize;
                float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FlatCenterRadius, FlatCenterRadius + 30f, dist));
                heights[z, x] = n * HillAmount * mask;
            }
        }
        data.SetHeights(0, 0, heights);

        // Simple mossy jungle-floor ground layer, so the terrain isn't plain grey
        data.terrainLayers = new[] { CreateGroundLayer() };

        string dataPath = AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/Tempulli_TerrainData.asset");
        AssetDatabase.CreateAsset(data, dataPath);

        // Terrain object, centered on the world origin, sunk slightly so its flat center sits just under y = 0
        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Jungle Terrain";
        go.transform.position = new Vector3(-TerrainSize / 2f, -0.05f, -TerrainSize / 2f);
        // Performance: GPU instancing and a coarser (still smooth) mesh LOD than the default pixel error of 1
        var terrain = go.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 5f;
        Undo.RegisterCreatedObjectUndo(go, "Create Terrain");

        // URP terrain material
        var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader != null)
        {
            var mat = new Material(shader) { name = "Tempulli_TerrainMat" };
            AssetDatabase.CreateAsset(mat, AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/Tempulli_TerrainMat.mat"));
            go.GetComponent<Terrain>().materialTemplate = mat;
        }

        report.Add("Created 200x200 terrain with gentle hills (assets in " + AssetFolder + ")");
    }

    static TerrainLayer CreateGroundLayer()
    {
        // Small procedural texture: mix of moss green and dark soil
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Tempulli_GroundTex", wrapMode = TextureWrapMode.Repeat };
        var moss = new Color(0.20f, 0.30f, 0.12f);
        var soil = new Color(0.22f, 0.17f, 0.11f);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Soft blotches + fine grain (low contrast, so tiling seams are barely visible)
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.7f + Random.value * 0.3f;
                pixels[y * size + x] = Color.Lerp(soil, moss, n);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        AssetDatabase.CreateAsset(tex, AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/Tempulli_GroundTex.asset"));

        var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(6f, 6f) };
        AssetDatabase.CreateAsset(layer, AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/Tempulli_Ground.terrainlayer"));
        return layer;
    }

    // ---------- Helpers ----------

    // All components of type T in the given scene (including inactive objects)
    static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<T>(true))
                yield return c;
    }

    // RenderSettings (fog/ambient/sun) are stored in a hidden per-scene object; record it for Undo
    static void RecordRenderSettings()
    {
        var getter = typeof(RenderSettings).GetMethod("GetRenderSettings", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (getter != null && getter.Invoke(null, null) is Object rs)
            Undo.RecordObject(rs, "Tempulli Render Settings");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
