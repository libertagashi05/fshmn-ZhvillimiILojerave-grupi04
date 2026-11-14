using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Tempulli i Humbur - balanced render settings for a steady 60 FPS (PC quality level).
// Menu: Tempulli > Optimizo performancën (FPS)
// Also called by "Përmirëso pamjen" / "Ndërto skenën e lojës", so rebuilding the scene no longer
// brings back the heavy settings. Changes nothing in the scene except terrain LOD settings;
// deletes nothing. One Ctrl+Z reverts it.
public static class TempulliPerformanca
{
    // ---- PC values (raise them again if you have FPS to spare) ----
    public const int   PcMsaa = 1;               // 1 = off. The camera already uses SMAA, so MSAA 4x was double anti-aliasing
    public const int   PcShadowRes = 2048;       // was 4096: 4x fewer shadow-map pixels
    public const int   PcCascades = 2;           // was 4: every cascade re-renders the shadow casters
    public const float PcShadowDistance = 60f;   // was 90: fog hides the far shadows anyway
    public const int   PcSoftShadowQuality = 2;  // 1 Low, 2 Medium, 3 High (was 3)
    const float TerrainPixelError = 5f;          // 1 = densest terrain mesh, 5 = still smooth, much cheaper

    const string PcAssetPath = "Assets/Settings/PC_RPAsset.asset";
    const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

    [MenuItem("Tempulli/Optimizo performancën (FPS)")]
    public static void Apply()
    {
        Undo.SetCurrentGroupName("Tempulli Optimizo performancën");
        int group = Undo.GetCurrentGroup();
        ApplyPipeline();
        SetPcVSync(1);
        int terrains = OptimizeTerrains();
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        Debug.Log("Tempulli: performanca u optimizua (PC: MSAA off + SMAA, hije " + PcShadowRes + "/" + PcCascades +
                  " kaskada/" + PcShadowDistance + " m, SSAO në gjysmë-rezolucion, " + terrains + " terrene).");
    }

    // URP asset + SSAO renderer feature
    public static void ApplyPipeline()
    {
        var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcAssetPath);
        if (pc != null)
        {
            Undo.RecordObject(pc, "Tempulli URP PC");
            var so = new SerializedObject(pc);
            SetInt(so, "m_MSAA", PcMsaa);
            SetInt(so, "m_MainLightShadowmapResolution", PcShadowRes);
            SetInt(so, "m_ShadowCascadeCount", PcCascades);
            SetFloat(so, "m_ShadowDistance", PcShadowDistance);
            SetInt(so, "m_SoftShadowQuality", PcSoftShadowQuality);
            // torches have shadows off, so the extra 2048 shadow atlas for point lights is never needed
            SetBool(so, "m_AdditionalLightShadowsSupported", false);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(pc);
        }

        // SSAO: half resolution and depth-only source (no extra depth-normals pass that redraws the whole scene)
        var pcData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRendererPath);
        if (pcData != null)
        {
            foreach (var f in pcData.rendererFeatures)
            {
                if (f == null || !f.GetType().Name.Contains("AmbientOcclusion")) continue;
                Undo.RecordObject(f, "Tempulli SSAO");
                var so = new SerializedObject(f);
                SetBool(so, "m_Settings.Downsample", true);
                SetInt(so, "m_Settings.Source", 0); // 0 = Depth, 1 = Depth Normals
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(f);
            }
        }
    }

    // VSync on the PC quality level: smooth frame pacing synced to the monitor (GameManager no longer caps PC at 60).
    // Mobile ignores VSync and keeps targetFrameRate 60.
    public static void SetPcVSync(int count)
    {
        int cur = QualitySettings.GetQualityLevel();
        var names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            if (!names[i].Contains("PC")) continue;
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.vSyncCount = count;
        }
        QualitySettings.SetQualityLevel(cur, false);
    }

    // Only for the before/after FPS measurement (TempulliMatesi): puts back the old heavy PC values
    // (MSAA 4x, 4096 shadows / 4 cascades / 90 m / High soft shadows, full-res SSAO with depth-normals).
    // The measurement always calls Apply() afterwards.
    public static void ApplyLegacyForComparison()
    {
        var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcAssetPath);
        if (pc != null)
        {
            var so = new SerializedObject(pc);
            SetInt(so, "m_MSAA", 4);
            SetInt(so, "m_MainLightShadowmapResolution", 4096);
            SetInt(so, "m_ShadowCascadeCount", 4);
            SetFloat(so, "m_ShadowDistance", 90f);
            SetInt(so, "m_SoftShadowQuality", 3);
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var pcData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRendererPath);
        if (pcData != null)
            foreach (var f in pcData.rendererFeatures)
            {
                if (f == null || !f.GetType().Name.Contains("AmbientOcclusion")) continue;
                var so = new SerializedObject(f);
                SetBool(so, "m_Settings.Downsample", false);
                SetInt(so, "m_Settings.Source", 1);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
    }

    // Terrains in the open scene: coarser (still smooth) mesh LOD and GPU instancing
    static int OptimizeTerrains()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return 0;
        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Terrain>(true))
            {
                Undo.RecordObject(t, "Tempulli Terrain");
                t.drawInstanced = true;
                t.heightmapPixelError = Mathf.Max(t.heightmapPixelError, TerrainPixelError);
                EditorUtility.SetDirty(t);
                n++;
            }
        if (n > 0) EditorSceneManager.MarkSceneDirty(scene);
        return n;
    }

    static void SetInt(SerializedObject so, string p, int v) { var x = so.FindProperty(p); if (x != null) x.intValue = v; }
    static void SetFloat(SerializedObject so, string p, float v) { var x = so.FindProperty(p); if (x != null) x.floatValue = v; }
    static void SetBool(SerializedObject so, string p, bool v) { var x = so.FindProperty(p); if (x != null) x.boolValue = v; }
}
