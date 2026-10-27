using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.VFX;

// Përdorimi i paketave në lojë (thirret nga TempulliSetup):
//  - Shader Graph: SG_GuriMyshk (gur me myshk) dhe SG_Uji (ujë që rrjedh me shkumë)  -> Assets/Shaders
//  - ProBuilder:   harku i portës dhe kolonat e rrënojave, gjeneruar me API dhe ruajtur si Mesh  -> Assets/Models/PB_*.asset
//  - VFX Graph:    pluhur i artë në ajër dhe mjegull e ulët rreth Artës  -> Assets/VFX
// Nëse diçka mungon (p.sh. paketa s'është instaluar), kthehet te materialet/format e vjetra.
public static class TempulliPaketaPerdorimi
{
    // ---------------- Shader Graph ----------------
    public static Material SGMat(string name, string graph, Material src, Color tint, float smooth, float normalScale = 1f)
    {
        var sh = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/" + graph);
        if (sh == null || ShaderUtil.ShaderHasError(sh))
        {
            Debug.LogWarning("Tempulli: Shader Graph " + graph + " nuk u ngarkua — përdoret materiali i vjetër " + (src != null ? src.name : "-"));
            return src;
        }
        string p = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); } else m.shader = sh;
        if (src != null)
        {
            if (src.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", src.GetTexture("_BaseMap")); m.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap")); }
            if (src.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
        }
        m.SetColor("_BaseColor", tint);          // alfa = sasia e myshkut (gur) / tejdukshmëria (ujë)
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_BumpScale", normalScale);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------- ProBuilder ----------------
    public static Mesh ArchMesh(float outerRadius, float thickness, float depth)
    {
        var pb = ShapeGenerator.GenerateArch(PivotLocation.Center, 180f, outerRadius, thickness, depth, 13, true, true, true, true, true);
        return Save(pb, "Assets/Models/PB_Harku.asset", true);
    }

    public static Mesh ColumnMesh()
    {
        var pb = ShapeGenerator.GenerateCylinder(PivotLocation.Center, 10, 0.45f, 3f, 0, -1);
        return Save(pb, "Assets/Models/PB_Kolona.asset", false);
    }

    static Mesh Save(ProBuilderMesh pb, string path, bool fixUpright)
    {
        pb.ToMesh(); pb.Refresh();
        var m = Object.Instantiate(pb.GetComponent<MeshFilter>().sharedMesh);
        Object.DestroyImmediate(pb.gameObject);
        var v = m.vertices; var n = m.normals;
        m.RecalculateBounds(); var b = m.bounds;
        if (fixUpright)
        {
            // harku duhet të qëndrojë mbi këmbët: poshtë duhet të jenë skajet (|x| i madh)
            float far = 0f;
            for (int i = 0; i < v.Length; i++) if (v[i].y < b.min.y + 0.05f) far = Mathf.Max(far, Mathf.Abs(v[i].x - b.center.x));
            if (far < b.extents.x * 0.5f)
                for (int i = 0; i < v.Length; i++) { v[i] = new Vector3(-v[i].x, -v[i].y, v[i].z); n[i] = new Vector3(-n[i].x, -n[i].y, n[i].z); }
        }
        // pivot: mes poshtë
        Vector3 mn = v[0], mx = v[0];
        foreach (var p in v) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
        Vector3 off = new Vector3((mn.x + mx.x) / 2f, mn.y, (mn.z + mx.z) / 2f);
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            v[i] -= off;
            Vector3 a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
            // UV triplanare në metra (1 teksturë = 4 m, si kutitë e shtegut)
            uv[i] = (a.x >= a.y && a.x >= a.z ? new Vector2(v[i].z, v[i].y) : a.y >= a.z ? new Vector2(v[i].x, v[i].z) : new Vector2(v[i].x, v[i].y)) / 4f;
        }
        m.vertices = v; m.normals = n; m.uv = uv;
        m.RecalculateTangents(); m.RecalculateBounds();
        m.name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    // ---------------- VFX Graph ----------------
    public static void AddAmbientVFX(Transform player)
    {
        if (!SystemInfo.supportsComputeShaders) { Debug.LogWarning("Tempulli: kjo pajisje s'ka compute shaders — VFX Graph anashkalohet."); return; }
        foreach (var name in new[] { "VFX_Pluhuri", "VFX_Mjegulla" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/VFX/" + name + ".vfx");
            if (asset == null) { Debug.LogWarning("Tempulli: " + name + ".vfx nuk u gjet."); continue; }
            var go = new GameObject(name);
            go.transform.SetParent(player, false);
            var ve = go.AddComponent<VisualEffect>();
            ve.visualEffectAsset = asset;
        }
    }
}
