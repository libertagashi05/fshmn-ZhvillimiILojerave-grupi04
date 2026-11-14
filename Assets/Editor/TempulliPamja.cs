using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Tempulli i Humbur - cilësia e figurës (thirret edhe nga "Ndërto skenën e lojës").
// Menuja: Tempulli > Përmirëso pamjen (cilësia)
//  - teksturat: filtrim anizotropik 8x + trilinear + mipmap Kaiser (shtegu dhe muret mbeten të qarta edhe larg),
//    kompresim me cilësi të lartë (pa "njolla" ngjyrash)
//  - URP (PC): rezolucion i plotë; hijet/MSAA/SSAO vendosen nga TempulliPerformanca (të balancuara për 60 FPS)
//  - Quality (PC): anizotropi e detyruar për të gjitha teksturat
public static class TempulliPamja
{
    static readonly string[] TexDirs = { "Assets/Textures/Tempulli", "Assets/Models/arta_tekstura" };

    [MenuItem("Tempulli/Përmirëso pamjen (cilësia)")]
    public static void Apply()
    {
        int t = Textures();
        RenderPipeline();
        Quality();
        AssetDatabase.SaveAssets();
        Debug.Log("Tempulli: pamja u përmirësua (" + t + " tekstura u riimportuan, URP PC: SMAA, hije " +
                  TempulliPerformanca.PcShadowRes + "/" + TempulliPerformanca.PcCascades + " kaskada).");
    }

    static int Textures()
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", TexDirs))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            bool clouds = path.Contains("ret_albedo") || path.Contains("bari_albedo");
            bool ch = false;
            if (ti.anisoLevel != 8) { ti.anisoLevel = 8; ch = true; }
            if (ti.filterMode != FilterMode.Trilinear) { ti.filterMode = FilterMode.Trilinear; ch = true; }
            if (ti.textureCompression != TextureImporterCompression.CompressedHQ) { ti.textureCompression = TextureImporterCompression.CompressedHQ; ch = true; }
            // Kaiser i mban mip-et më të mprehta; bari dhe retë kanë alfa, aty mbetet Box (pa cepa të fortë)
            var mf = clouds ? TextureImporterMipFilter.BoxFilter : TextureImporterMipFilter.KaiserFilter;
            if (ti.mipmapFilter != mf) { ti.mipmapFilter = mf; ch = true; }
            if (path.EndsWith("arta_rrobat_normal.png") && ti.maxTextureSize != 4096) { ti.maxTextureSize = 4096; ch = true; }
            if (ch) { ti.SaveAndReimport(); n++; }
        }
        return n;
    }

    static void RenderPipeline()
    {
        var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        if (pc != null)
        {
            var so = new SerializedObject(pc);
            void F(string p, float v) { var x = so.FindProperty(p); if (x != null) x.floatValue = v; }
            void B(string p, bool v) { var x = so.FindProperty(p); if (x != null) x.boolValue = v; }
            F("m_RenderScale", 1f);
            B("m_SupportsHDR", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pc);
        }
        // MSAA, hijet dhe SSAO: vlerat e balancuara për FPS janë te TempulliPerformanca.cs
        // (më parë këtu: MSAA 4x + hije 4096/4 kaskada/90 m, që e rëndonin shumë GPU-në)
        TempulliPerformanca.ApplyPipeline();
        var mob = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        if (mob != null)
        {
            // celulari: 0.8 -> 0.9 (më i qartë, ende i lehtë për GPU-në)
            var so = new SerializedObject(mob);
            var rs = so.FindProperty("m_RenderScale"); if (rs != null && rs.floatValue < 0.9f) rs.floatValue = 0.9f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mob);
        }
    }

    static void Quality()
    {
        int cur = QualitySettings.GetQualityLevel();
        var names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            if (!names[i].Contains("PC")) continue;
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.lodBias = 2f;
        }
        QualitySettings.SetQualityLevel(cur, false);
    }
}
