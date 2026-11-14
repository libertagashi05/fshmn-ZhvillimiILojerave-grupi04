using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Arta e re (personazh realist nga RenderHub "Realistic Female Adventurer", i përpunuar në Blender: Blender/arta2.blend).
// arta2.fbx ka skeletin "mixamorig" (Humanoid), prandaj të gjitha animacionet tona (arta.fbx + Mixamo)
// i kalohen me retargeting: arta.fbx mbetet burimi i klipeve, arta2.fbx është vetëm pamja.
// Herën e parë që gjendet arta2.fbx, rindërtohen vetë Game.unity dhe botët (ose: Tempulli > Arta e re).
[InitializeOnLoad]
public static class TempulliArta2
{
    public const string Modeli = "Assets/Models/arta2.fbx";
    const string Tex = "Assets/Models/arta2_tekstura/";
    const string MatDir = "Assets/Materials/Arta2";
    const string Gati = "tempulli-arta2-v2";   // v2: fbx-i me skelet (v1 kishte vetëm mesh-in)

    public static bool Ekziston => File.Exists(Modeli);

    static TempulliArta2()
    {
        if (!Ekziston || SessionState.GetBool("Tempulli_Arta2_Auto2", false)) return;
        SessionState.SetBool("Tempulli_Arta2_Auto2", true);
        EditorApplication.delayCall += Prit;
    }

    static void Prit()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Prit;
            return;
        }
        var imp = AssetImporter.GetAtPath(Modeli) as ModelImporter;
        if (imp == null || imp.userData == Gati) return;   // është futur tashmë në lojë
        Debug.Log("Tempulli: u gjet Arta e re (arta2.fbx) — po rindërtohen loja dhe botët...");
        NdertoGjithcka();
    }

    [MenuItem("Tempulli/Arta e re (rindërto lojën dhe botët)")]
    public static void NdertoGjithcka()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Tempulli: ndalo Play para se të rindërtosh."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        TempulliSetup.BuildScene();
        TempulliBotet.NdertoBotet();
        Debug.Log("Tempulli: Arta e re është në lojë (Game + botët). Shtyp Play.");
    }

    // Importi Humanoid + teksturat + materialet URP. Kthen true nëse Avatar-i doli i vlefshëm.
    public static bool Pergatit()
    {
        var imp = AssetImporter.GetAtPath(Modeli) as ModelImporter;
        if (imp == null) return false;
        Teksturat();
        var mats = Materialet();
        bool ndrysho = imp.userData != Gati;
        if (ndrysho)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.importBlendShapes = false; imp.importCameras = false; imp.importLights = false;
            imp.importVisibility = false;
            imp.skinWeights = ModelImporterSkinWeights.Custom; imp.maxBonesPerVertex = 4;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.optimizeMeshPolygons = true; imp.optimizeMeshVertices = true;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.optimizeGameObjects = false;
            imp.userData = Gati;
        }
        var ekz = imp.GetExternalObjectMap();
        foreach (var kv in mats)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
            if (ekz.TryGetValue(id, out var cur) && cur == kv.Value) continue;
            imp.AddRemap(id, kv.Value); ndrysho = true;
        }
        if (ndrysho) imp.SaveAndReimport();

        Avatar av = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Modeli)) if (o is Avatar a) av = a;
        if (av == null || !av.isValid || !av.isHuman)
        {
            Debug.LogWarning("Tempulli: Avatar-i i Artës së re nuk doli Humanoid i vlefshëm — përdoret Arta e vjetër.");
            return false;
        }
        if (ndrysho) Debug.Log("Tempulli: Arta e re u importua si Humanoid (" + mats.Count + " materiale URP).");
        return true;
    }

    static void Teksturat()
    {
        if (!AssetDatabase.IsValidFolder(Tex.TrimEnd('/'))) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Tex.TrimEnd('/') }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            string n = Path.GetFileNameWithoutExtension(p);
            bool normal = n.EndsWith("_normal"), maska = n.EndsWith("_maska");
            bool alfa = n.EndsWith("_baza") && p.EndsWith(".png");
            bool ch = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ch = true; }
            if (maska && ti.sRGBTexture) { ti.sRGBTexture = false; ch = true; }
            if (alfa && !ti.alphaIsTransparency)
            {
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.4f;
                ch = true;
            }
            if (ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; ch = true; }
            if (ti.anisoLevel < 4) { ti.anisoLevel = 4; ch = true; }
            if (ch) ti.SaveAndReimport();
        }
    }

    static Dictionary<string, Material> Materialet()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "Arta2");
        var d = new Dictionary<string, Material>
        {
            //                  emri            lëmimi  alfa   dy anë
            { "A2_Koka",        Mat("A2_Koka",       0.85f) },
            { "A2_Trupi",       Mat("A2_Trupi",      0.85f) },
            { "A2_Krahet",      Mat("A2_Krahet",     0.85f) },
            { "A2_Thonjte",     Mat("A2_Thonjte",    1f) },
            { "A2_Syte",        Mat("A2_Syte",       0.85f) },
            { "A2_Vetullat",    Mat("A2_Vetullat",   0.1f, 0.35f, true) },
            { "A2_Qerpiket",    Mat("A2_Qerpiket",   0.1f, 0.3f, true) },
            { "A2_Floket",      Mat("A2_Floket",     0.45f, 0.4f, true) },
            { "A2_Floket_Baza", Mat("A2_Floket_Baza", 0.3f, 0.3f) },
            { "A2_Bluza",       Mat("A2_Bluza",      1f) },
            { "A2_Fundi",       Mat("A2_Fundi",      1f) },
            { "A2_Kepucet",     Mat("A2_Kepucet",    1f) },
            { "A2_Corapet",     Mat("A2_Corapet",    0.4f, -1f, false, new Color(0.03f, 0.024f, 0.021f).gamma) },
        };
        AssetDatabase.SaveAssets();
        return d;
    }

    static Texture2D Ngarko(string n)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + n + ".png");
        return t != null ? t : AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + n + ".jpg");
    }

    // lemimi: me maskë është shumëzues i kanalit A (R = metalik); pa maskë është vlera e drejtpërdrejtë
    static Material Mat(string emri, float lemimi, float alfa = -1f, bool dyAne = false, Color? ngjyra = null)
    {
        string p = MatDir + "/" + emri + ".mat";
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null && GraphicsSettings.currentRenderPipeline != null) sh = GraphicsSettings.currentRenderPipeline.defaultShader;
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        else if (m.shader != sh) m.shader = sh;

        Texture2D baza = Ngarko(emri + "_baza"), normal = Ngarko(emri + "_normal"), maska = Ngarko(emri + "_maska");
        m.SetFloat("_WorkflowMode", 1f); m.DisableKeyword("_SPECULAR_SETUP");
        m.SetColor("_BaseColor", ngjyra ?? Color.white);
        m.SetTexture("_BaseMap", baza);
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        m.SetFloat("_SmoothnessTextureChannel", 0f); m.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        if (maska != null) { m.SetTexture("_MetallicGlossMap", maska); m.SetFloat("_Metallic", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
        else { m.SetTexture("_MetallicGlossMap", null); m.SetFloat("_Metallic", 0f); m.DisableKeyword("_METALLICSPECGLOSSMAP"); }
        m.SetFloat("_Smoothness", lemimi);

        bool clip = alfa >= 0f;
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_AlphaClip", clip ? 1f : 0f);
        m.SetFloat("_Cutoff", clip ? alfa : 0.5f);
        if (clip) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
        m.renderQueue = clip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
        m.SetFloat("_Cull", dyAne ? 0f : 2f);
        m.doubleSidedGI = dyAne;
        EditorUtility.SetDirty(m);
        return m;
    }
}
