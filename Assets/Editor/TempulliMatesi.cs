using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Tempulli i Humbur - mat FPS-në me autopilot (25 s) dhe shkruan rezultatin te Logs/tempulli_fps.txt.
// Menuja: Tempulli > Mat FPS (autopilot)            -> një matje me cilësimet aktuale
//         Tempulli > Mat FPS para-pas optimizimit   -> cilësimet e vjetra (të rënda) -> matje -> "Optimizo performancën" -> matje
// (Projekti ka "Enter Play Mode Options" pa domain reload, prandaj gjendja statike ruhet gjatë Play.)
[InitializeOnLoad]
public static class TempulliMatesi
{
    const string RequestFile = "Temp/tempulli_fps.request";   // nëse ekziston pas kompilimit: nis matjen para/pas
    const string ReportFile = "Logs/tempulli_fps.txt";
    static string phase = "";                                 // "", "single", "before", "after"
    static TempulliFpsMatesi current;

    static TempulliMatesi()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Tick;
        EditorApplication.update += CheckRequest;
    }

    // një herë në sekondë: a ka kërkesë për matje (skedari Temp/tempulli_fps.request)?
    static double nextCheck;
    static void CheckRequest()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        if (phase != "" || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!File.Exists(RequestFile)) return;
        string req = File.ReadAllText(RequestFile).Trim();
        File.Delete(RequestFile);
        if (req == "apply-single") { TempulliPerformanca.Apply(); Single(); }
        else if (req == "uncapped") { File.WriteAllText(ReportFile, System.DateTime.Now + "\n"); Start("uncapped"); }
        else if (req == "single") Single();
        else BeforeAfter();
    }

    [MenuItem("Tempulli/Mat FPS (autopilot)")]
    public static void Single()
    {
        File.WriteAllText(ReportFile, System.DateTime.Now + "\n");
        Start("single");
    }

    [MenuItem("Tempulli/Mat FPS para-pas optimizimit")]
    public static void BeforeAfter()
    {
        File.WriteAllText(ReportFile, System.DateTime.Now + "\n");
        TempulliPerformanca.ApplyLegacyForComparison();   // cilësimet e vjetra, vetëm për matjen "para"
        Start("before");
    }

    static void Start(string p)
    {
        if (EditorApplication.isPlaying) return;
        // pastro matës të mbetur nga një matje e ndërprerë
        foreach (var m in Resources.FindObjectsOfTypeAll<TempulliFpsMatesi>())
            if (m != null && !EditorUtility.IsPersistent(m)) Object.DestroyImmediate(m.gameObject);
        phase = p;
        current = null;
        PlayerController.StressTest = false;
        PlayerController.TestSpeed = 0f;
        PlayerController.Autopilot = true;
        // loja nis vetë pa menunë (GameManager.autoStart)
        var f = typeof(GameManager).GetField("autoStart", BindingFlags.Static | BindingFlags.NonPublic);
        if (f != null) f.SetValue(null, true);
        EditorApplication.isPlaying = true;
    }

    static void OnPlayMode(PlayModeStateChange s)
    {
        if (phase == "") return;
        if (s == PlayModeStateChange.EnteredPlayMode)
        {
            // objekt i zakonshëm i skenës së Play: fshihet vetë kur del nga Play
            var go = new GameObject("TempulliFpsMatesi");
            current = go.AddComponent<TempulliFpsMatesi>();
            current.label = phase == "before" ? "PARA optimizimit (cilësimet e vjetra)" : phase == "after" ? "PAS optimizimit"
                          : phase == "uncapped" ? "PA KUFI FPS (targetFrameRate -1)" : "Matja";
            current.uncapped = phase == "uncapped";
        }
        else if (s == PlayModeStateChange.EnteredEditMode)
        {
            string done = phase;   // Tick e shënon "-written" kur rezultati u ruajt
            phase = "";
            current = null;
            if (done == "before-written")
            {
                TempulliPerformanca.Apply();
                EditorApplication.delayCall += () => Start("after");
                return;
            }
            PlayerController.Autopilot = false;
            if (done.EndsWith("-written"))
                Debug.Log("Tempulli: matja e FPS-së përfundoi -> " + Path.GetFullPath(ReportFile) + "\n" + File.ReadAllText(ReportFile));
            else if (done == "before")
                TempulliPerformanca.Apply();   // u ndërpre me dorë: mos i lër cilësimet e vjetra
        }
    }

    static void Tick()
    {
        if (current == null || phase.EndsWith("-written") || !EditorApplication.isPlaying || !current.Done) return;
        File.AppendAllText(ReportFile, current.Result + "\n");
        phase += "-written";
        EditorApplication.isPlaying = false;
    }
}
