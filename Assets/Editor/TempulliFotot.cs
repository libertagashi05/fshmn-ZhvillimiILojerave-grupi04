using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Tempulli i Humbur - fotot per prezantimin (vetem ne Editor, nuk hyn ne loje).
// Ruhen te dosja "Fotot" ne rrenje te projektit (jashte Assets, qe Unity mos t'i importoje).
public static class TempulliFotot
{
    const string Dosja = "Fotot";
    const int W = 2560, H = 1440;
    const double Intervali = 0.6;
    static bool auto;
    static double next;
    static int nr;

    static string Emri(string prefix, string ext)
    {
        Directory.CreateDirectory(Dosja);
        nr++;
        return Path.Combine(Dosja, prefix + "_" + System.DateTime.Now.ToString("HHmmss") + "_" + nr.ToString("000") + ext);
    }

    [MenuItem("Tempulli/Fotot/Kapja automatike gjate lojes (ndez-fik)")]
    static void NdezFik()
    {
        auto = !auto;
        EditorApplication.update -= Tick;
        if (auto) { next = 0; EditorApplication.update += Tick; }
        Debug.Log("Tempulli fotot: kapja automatike " + (auto ? "NDEZUR" : "FIKUR"));
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + Intervali;
        Rendero(Camera.main, Emri("loja", ".jpg"), true);
    }

    [MenuItem("Tempulli/Fotot/Foto nga kamera e lojes")]
    static void FotoKamera() { Rendero(Camera.main, Emri("kamera", ".png"), false); }

    [MenuItem("Tempulli/Fotot/Foto e lojes me UI (vetem ne Play)")]
    static void FotoUI()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("Tempulli fotot: shtyp Play me pare"); return; }
        var p = Emri("loja_ui", ".png");
        ScreenCapture.CaptureScreenshot(p, 1);
        Debug.Log("Tempulli fotot: " + p);
    }

    [MenuItem("Tempulli/Fotot/Foto nga pamja e skenes (Scene view)")]
    static void FotoSkena()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.camera == null) { Debug.LogWarning("Tempulli fotot: hap Scene view"); return; }
        var go = new GameObject("TempulliFotoKamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        if (Camera.main != null) cam.CopyFrom(Camera.main);
        cam.transform.SetPositionAndRotation(sv.camera.transform.position, sv.camera.transform.rotation);
        cam.fieldOfView = sv.camera.fieldOfView;
        cam.orthographic = sv.camera.orthographic;
        cam.orthographicSize = sv.camera.orthographicSize;
        cam.nearClipPlane = Mathf.Max(0.05f, sv.camera.nearClipPlane);
        cam.farClipPlane = Mathf.Max(1500f, sv.camera.farClipPlane);
        try
        {
            var src = Camera.main != null ? Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            var dst = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            dst.renderPostProcessing = src == null || src.renderPostProcessing;
            dst.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }
        catch (System.Exception e) { Debug.LogWarning("Tempulli fotot: " + e.Message); }
        Rendero(cam, Emri("skena", ".png"), false);
        Object.DestroyImmediate(go);
    }

    static void Rendero(Camera cam, string path, bool jpg)
    {
        if (cam == null) { Debug.LogWarning("Tempulli fotot: s'ka kamere"); return; }
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.antiAliasing = 1;
        rt.Create();
        bool ok = false;
        try
        {
            var req = new RenderPipeline.StandardRequest();
            req.destination = rt;
            if (RenderPipeline.SupportsRenderRequest(cam, req)) { RenderPipeline.SubmitRenderRequest(cam, req); ok = true; }
        }
        catch (System.Exception e) { Debug.LogWarning("Tempulli fotot (kerkesa): " + e.Message); }
        if (!ok)
        {
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = old;
        }
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, jpg ? tex.EncodeToJPG(90) : tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        if (!jpg) Debug.Log("Tempulli fotot: " + path + (ok ? "" : " (Camera.Render)"));
    }
}
