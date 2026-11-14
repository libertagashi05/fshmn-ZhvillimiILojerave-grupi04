using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tempulli i Humbur - animacionet nga Mixamo (mocap) mbi Artën (Humanoid -> Humanoid retargeting).
// Skedarët: Assets/Animations/Mixamo/Mx_*.fbx (X Bot, pa lëkurë, 30 fps, "In Place" për vrapin).
// Thirret nga "Ndërto skenën e lojës": nëse skedarët ekzistojnë, këto klipe zëvendësojnë klipet e vjetra të Artës
// me të njëjtat emra (Run, Sprint, JumpUp, ... ), kështu që Animator-i dhe kodi mbeten të njëjtë.
// Kërcimi dhe rrëshqitja ndahen automatikisht në faza (ngritje/ajër/rënie/ulje, hyrje/cikël/dalje)
// sipas lartësisë së trupit (kurba RootT.y), dhe hapat (ngjarja "Hapi") vendosen kur këmba prek tokën.
public static class TempulliMixamo
{
    public const string Dir = "Assets/Animations/Mixamo/";
    const string Version = "tempulli-mx7";   // mx7: rrëshqitja kthehet te "Running Slide"
    static Avatar xbot;   // Avatar-i i saktë i X Bot (nga modeli me lëkurë në T-pose)

    public static bool Available => File.Exists(Dir + "Mx_Run.fbx");

    [MenuItem("Tempulli/Animacionet Mixamo (riimporto)")]
    public static void ReimportMenu()
    {
        foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Animations/Mixamo" }))
        {
            var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(g)) as ModelImporter;
            if (imp != null) imp.userData = "";
        }
        var c = Import();
        Debug.Log("Tempulli: Mixamo u riimportua (" + c.Count + " klipe). Zgjidh Tempulli > Ndërto skenën e lojës.");
    }

    // Y: si llogaritet lartësia e trupit
    enum Y { Original, Feet }

    static ModelImporterClipAnimation C(string name, string take, float a, float b, bool loop, Y y, bool bakeXZ, bool bakeRot = true, bool mirror = false)
    {
        return new ModelImporterClipAnimation
        {
            name = name, takeName = take, firstFrame = a, lastFrame = b,
            loopTime = loop, loopPose = loop, loop = loop,
            // rrotullimi: i "pjekur" në pozë (trupi ruan drejtimin e animacionit) përveç kthesave,
            // ku rrotullimi 90° i Mixamo hidhet (kthesën e bën kodi)
            lockRootRotation = bakeRot, keepOriginalOrientation = true,
            // lartësia: gjithmonë në pozë; "Feet" mban këmbët në tokën e lojtarit (kërcimi/rrëshqitja ndiqen nga fizika)
            lockRootHeightY = true, keepOriginalPositionY = y == Y.Original, heightFromFeet = y == Y.Feet,
            // lëvizja përpara: klipet "In Place" s'kanë; te të tjerat hidhet (lojtari lëviz nga kodi)
            lockRootPositionXZ = bakeXZ, keepOriginalPositionXZ = bakeXZ,
            mirror = mirror,
        };
    }

    // ---------- analiza e kurbave ----------
    static AnimationCurve Curve(AnimationClip clip, string prop)
    {
        var b = EditorCurveBinding.FloatCurve("", typeof(Animator), prop);
        return AnimationUtility.GetEditorCurve(clip, b);
    }
    static float[] Sample(AnimationCurve c, int frames, float fps)
    {
        var v = new float[frames + 1];
        for (int i = 0; i <= frames; i++) v[i] = c != null ? c.Evaluate(i / fps) : 0f;
        return v;
    }
    static int ArgMax(float[] v, int a, int b) { int k = a; for (int i = a; i <= b; i++) if (v[i] > v[k]) k = i; return k; }
    static int ArgMin(float[] v, int a, int b) { int k = a; for (int i = a; i <= b; i++) if (v[i] < v[k]) k = i; return k; }
    static float Median(float[] v, int a, int b) { var l = new List<float>(); for (int i = a; i <= b; i++) l.Add(v[i]); l.Sort(); return l[l.Count / 2]; }

    // momentet kur këmbët prekin tokën (minimumet e kurbave të këmbëve), në kohë 0-1 të klipit [a,b]
    static List<float> Steps(AnimationClip full, float fps, int a, int b)
    {
        var res = new List<float>();
        int n = Mathf.RoundToInt(full.length * fps);
        foreach (var p in new[] { "LeftFootT.y", "RightFootT.y" })
        {
            var c = Curve(full, p); if (c == null) continue;
            var v = Sample(c, n, fps);
            int k = ArgMin(v, Mathf.Clamp(a, 0, n), Mathf.Clamp(b, 0, n));
            res.Add(Mathf.Clamp01((k - a) / (float)Mathf.Max(1, b - a)));
        }
        if (res.Count == 0) { res.Add(0.05f); res.Add(0.55f); }
        return res;
    }
    static AnimationEvent[] Ev(string fn, params float[] t)
    {
        var l = new List<AnimationEvent>();
        foreach (var x in t) l.Add(new AnimationEvent { time = x, functionName = fn });
        return l.ToArray();
    }

    // ---------- importi ----------
    delegate ModelImporterClipAnimation[] Defs(AnimationClip full, string take, int last, float fps);

    static void Do(string file, Defs defs, Dictionary<string, AnimationClip> res)
    {
        string path = Dir + file + ".fbx";
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return;
        if (imp.userData != Version)
        {
            // hapi 1: Humanoid + një klip i plotë, për ta analizuar
            imp.animationType = ModelImporterAnimationType.Human;
            // skedarët "pa lëkurë" nuk kanë T-pose: Avatar-i i krijuar prej tyre del i shtrembër (muskujt e këmbës
            // së majtë dilnin deri në -2.8 dhe gjuri "kthehej" 180°). Ndaj përdoret Avatar-i i X Bot në T-pose.
            if (xbot != null) { imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; imp.sourceAvatar = xbot; }
            else imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importCameras = false; imp.importLights = false; imp.importBlendShapes = false;
            imp.animationCompression = ModelImporterAnimationCompression.Optimal;
            imp.resampleCurves = true;
            imp.clipAnimations = new ModelImporterClipAnimation[0];
            imp.SaveAndReimport();
            var d0 = imp.defaultClipAnimations;
            if (d0.Length == 0) { Debug.LogWarning("Tempulli: " + file + " nuk ka animacion."); return; }
            string take = d0[0].takeName;
            int last = Mathf.RoundToInt(d0[0].lastFrame);
            var tmp = d0[0]; tmp.name = "__full"; tmp.lockRootHeightY = true; tmp.keepOriginalPositionY = true;
            imp.clipAnimations = new[] { tmp };
            imp.SaveAndReimport();
            AnimationClip full = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is AnimationClip ac && ac.name == "__full") full = ac;
            float fps = full != null && full.frameRate > 0 ? full.frameRate : 30f;
            // hapi 2: klipet e lojës
            imp.clipAnimations = defs(full, take, last, fps);
            imp.userData = Version;
            imp.SaveAndReimport();
        }
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip c && !c.name.StartsWith("__")) res[c.name] = c;
    }

    public static Dictionary<string, AnimationClip> Import()
    {
        var res = new Dictionary<string, AnimationClip>();
        if (!Available) return res;
        xbot = XBotAvatar();

        // vrapi dhe sprinti: cikle "In Place", hapat te prekja e tokës
        Do("Mx_Run", (f, t, n, fps) => { var c = C("Run", t, 0, n, true, Y.Original, true); c.events = Ev("Hapi", Steps(f, fps, 0, n).ToArray()); return new[] { c }; }, res);
        Do("Mx_Sprint", (f, t, n, fps) => { var c = C("Sprint", t, 0, n, true, Y.Original, true); c.events = Ev("Hapi", Steps(f, fps, 0, n).ToArray()); return new[] { c }; }, res);

        // kërcimi me vrap: ndahet në ngritje / ajër / rënie / ulje sipas lartësisë së trupit
        Do("Mx_Jump", (f, t, n, fps) =>
        {
            // "Female Ninja Running Jump" (24 frame, gjunjët lart): fazat të matura në Blender
            // (shtytja ~3, maja 7, prekja e tokës 13); skedarët e tjerë ndahen automatikisht më poshtë
            if (n >= 22 && n <= 25)
            {
                var nu = C("JumpUp", t, 0, 5, false, Y.Feet, true); nu.events = Ev("Shtytja", 0.5f);
                var na = C("JumpAir", t, 5, 9, false, Y.Feet, true);
                var nf = C("JumpFall", t, 8, 12, false, Y.Feet, true);
                var nl = C("Land", t, 12, 19, false, Y.Feet, true); nl.events = Ev("Ulja", 0.14f);
                Debug.Log("Tempulli/Mixamo: kërcimi ninja (" + n + " frame)");
                return new[] { nu, na, nf, nl };
            }
            var y = Sample(Curve(f, "RootT.y"), n, fps);
            float base0 = Median(y, 0, Mathf.Min(6, n));
            int apex = ArgMax(y, 3, n - 3);
            int take0 = apex; while (take0 > 1 && y[take0 - 1] > base0) take0--;           // shkëputja nga toka
            int land = apex; while (land < n - 1 && y[land + 1] > base0) land++;           // prekja e tokës
            int upA = Mathf.Max(0, take0 - 4), upB = Mathf.Clamp(take0 + Mathf.RoundToInt((apex - take0) * 0.75f), take0 + 2, apex);
            var up = C("JumpUp", t, upA, upB, false, Y.Feet, true);
            up.events = Ev("Shtytja", Mathf.Clamp01((take0 - upA) / (float)Mathf.Max(1, upB - upA)));
            // pa cikël: segmenti i majës nuk lidhet fund->fillim (këputej krahu/këmba); gjendja mban pozën e fundit
            var air = C("JumpAir", t, Mathf.Max(upB - 1, apex - 3), Mathf.Min(n, apex + 3), false, Y.Feet, true);
            var fall = C("JumpFall", t, apex, Mathf.Max(apex + 2, land), false, Y.Feet, true);
            int lb = Mathf.Min(n, land + 14);
            var ld = C("Land", t, Mathf.Max(apex + 1, land - 1), lb, false, Y.Feet, true);
            ld.events = Ev("Ulja", 0.1f);
            Debug.Log($"Tempulli/Mixamo: kërcimi — shkëputja {take0}, maja {apex}, ulja {land} (nga {n} frame)");
            return new[] { up, air, fall, ld };
        }, res);

        // rënia e gjatë (nga litari ose nga skaji): cikël në ajër
        // salto (herë pas here në vend të kërcimit): "Running Forward Flip" (34 frame) — nga përkulja para shtytjes
        // (8), shtytja (10), rrotullimi 360°, prekja e tokës (27) deri te ngritja (31); shpejtësia e gjendjes
        // (SaltoSpeed) e vendos prekjen saktë kur Arta bie në tokë
        Do("Mx_Flip", (f, t, n, fps) =>
        {
            int a = n >= 31 ? 8 : 0, b = n >= 31 ? 31 : n, sh = n >= 31 ? 10 : 0, pr = n >= 31 ? 27 : n;
            var c = C("JumpFlip", t, a, b, false, Y.Feet, true);
            float L = Mathf.Max(1, b - a);
            c.events = new[]
            {
                new AnimationEvent { functionName = "Shtytja", time = (sh - a) / L },
                new AnimationEvent { functionName = "Ulja", time = (pr - a) / L },
            };
            return new[] { c };
        }, res);

        Do("Mx_FallIdle", (f, t, n, fps) => new[] { C("FallLong", t, 0, n, true, Y.Feet, false) }, res);

        // rrëshqitja: hyrje / cikël / dalje sipas uljes së trupit
        Do("Mx_Slide", (f, t, n, fps) =>
        {
            var y = Sample(Curve(f, "RootT.y"), n, fps);
            float base0 = Median(y, 0, Mathf.Min(6, n));
            int lo = ArgMin(y, 0, n);
            float thr = base0 - 0.6f * (base0 - y[lo]);
            int s0 = lo; while (s0 > 0 && y[s0 - 1] < thr) s0--;
            int s1 = lo; while (s1 < n && y[s1 + 1] < thr) s1++;
            int inA = Mathf.Max(0, s0 - 9), inB = Mathf.Min(n, s0 + 2);
            int lpA = inB, lpB = Mathf.Max(lpA + 4, s1 - 2);
            int outA = lpB, outB = Mathf.Min(n, s1 + 12);
            var si = C("SlideIn", t, inA, inB, false, Y.Feet, false); si.events = Ev("Rreshqitja", 0.75f);
            // pa cikël (këputej krahu në lidhjen fund->fillim): pas 0.5 s poza e rrëshqitjes mbahet
            var sl = C("SlideLoop", t, lpA, lpB, false, Y.Feet, false);
            var so = C("SlideOut", t, outA, outB, false, Y.Feet, false);
            Debug.Log($"Tempulli/Mixamo: rrëshqitja — hyrja {inA}-{inB}, cikli {lpA}-{lpB}, dalja {outA}-{outB} (nga {n})");
            return new[] { si, sl, so };
        }, res);

        // kthesat: djathtas nga Mixamo, majtas = pasqyrim; rrotullimi 90° hidhet (e bën kodi)
        Do("Mx_TurnR", (f, t, n, fps) =>
        {
            var r = C("TurnR", t, 0, n, false, Y.Original, true, false); r.events = Ev("Hapi", 0.3f);
            var l = C("TurnL", t, 0, n, false, Y.Original, true, false, true); l.events = Ev("Hapi", 0.3f);
            return new[] { r, l };
        }, res);

        Do("Mx_Stumble", (f, t, n, fps) => { var c = C("Stumble", t, 0, n, false, Y.Original, true); c.events = Ev("Hapi", 0.3f); return new[] { c }; }, res);
        Do("Mx_Death", (f, t, n, fps) => new[] { C("Death", t, 0, n, false, Y.Original, false) }, res);
        Do("Mx_Hang", (f, t, n, fps) => new[] { C("Hang", t, 0, n, true, Y.Original, false) }, res);

        // vagoni (bota e varrit): qëndrimi i përkulur, anim (kulmi i shmangies djathtas + pasqyra majtas) dhe ulja;
        // teposhtja e akullit: qëndrimi i skateboard-it (Arta zbret më këmbë si me snowboard)
        Do("Mx_VagonQendro", (f, t, n, fps) => new[] { C("VagonQendro", t, 0, n, true, Y.Original, true) }, res);
        Do("Mx_VagonAnohu", (f, t, n, fps) =>
        {
            // korniza ku trupi është më larg anash (RootT.x): poza e animit, e mbajtur
            var x = Sample(Curve(f, "RootT.x"), n, fps);
            int k = 0; float best = -1f;
            for (int i = 0; i <= n; i++) { float d = Mathf.Abs(x[i] - x[0]); if (d > best) { best = d; k = i; } }
            int a = Mathf.Clamp(k - 1, 0, n), b = Mathf.Clamp(k + 1, 0, n);
            if (b <= a) b = Mathf.Min(n, a + 1);
            return new[] { C("VagonAnohuD", t, a, b, true, Y.Original, true), C("VagonAnohuM", t, a, b, true, Y.Original, true, true, true) };
        }, res);
        Do("Mx_VagonUlu", (f, t, n, fps) =>
        {
            // nga fillimi i uljes deri te pika më e ulët e trupit (klipi mbahet aty sa zgjat ulja)
            var y = Sample(Curve(f, "RootT.y"), n, fps);
            int lo = ArgMin(y, 0, n), a = 0;
            for (int i = lo; i >= 0; i--) if (y[i] >= y[0] - 0.15f * (y[0] - y[lo])) { a = i; break; }
            return new[] { C("VagonUlu", t, a, Mathf.Max(a + 2, lo), false, Y.Original, true) };
        }, res);
        Do("Mx_Akull", (f, t, n, fps) => new[] { C("Akulli", t, 0, n, true, Y.Original, true) }, res);

        Debug.Log("Tempulli: animacionet Mixamo gati (" + res.Count + " klipe: " + string.Join(", ", res.Keys) + ").");
        return res;
    }

    static Avatar XBotAvatar()
    {
        string path = Dir + "Mx_XBot_TPose.fbx";
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return null;
        if (imp.userData != Version)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importCameras = false; imp.importLights = false; imp.importBlendShapes = false;
            imp.userData = Version;
            imp.SaveAndReimport();
        }
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Avatar a && a.isValid && a.isHuman) return a;
        Debug.LogWarning("Tempulli: Avatar-i i X Bot nuk u krijua; klipet përdorin Avatar-in e tyre.");
        return null;
    }

    // klipet e Mixamo zëvendësojnë ato të Artës me të njëjtin emër
    public static void Override(Dictionary<string, AnimationClip> clips)
    {
        var mx = Import();
        foreach (var kv in mx) clips[kv.Key] = kv.Value;
    }
}
