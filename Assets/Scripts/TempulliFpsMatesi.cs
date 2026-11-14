using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

// Tempulli i Humbur - matës i FPS-së (vetëm për testim; nuk vendoset në skenë).
// E shton vetë menuja "Tempulli > Mat FPS"; pas një ngrohjeje mat kohën e çdo frame-i,
// ku shkon koha e CPU-së (skriptet, fizika, animacionet, renderimi, editori) dhe nxjerr një përmbledhje.
public class TempulliFpsMatesi : MonoBehaviour
{
    public bool Done { get; private set; }
    public string Result { get; private set; }

    public string label = "";
    public float warmup = 4f, duration = 25f;
    public bool uncapped;   // pa kufi FPS (targetFrameRate -1): tregon sa kohë i duhet vërtet një frame-i

    // pjesët kryesore të frame-it (emrat e markerave të Profiler-it të Unity-t)
    static readonly HashSet<string> Markers = new HashSet<string>
    {
        "PlayerLoop", "EditorLoop",
        "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "FixedUpdate.ScriptRunBehaviourFixedUpdate",
        "FixedUpdate.PhysicsFixedUpdate", "Physics.Simulate", "Physics.SyncColliderTransform",
        "PreLateUpdate.DirectorUpdateAnimationBegin", "PreLateUpdate.DirectorUpdateAnimationEnd", "Update.DirectorUpdate",
        "PreLateUpdate.ParticleSystemBeginUpdateAll", "PostLateUpdate.ParticleSystemEndUpdateAll",
        "PostLateUpdate.FinishFrameRendering", "PostLateUpdate.UpdateAllRenderers", "PostLateUpdate.PlayerSendFrameComplete",
        "RenderPipelineManager.DoRenderLoop_Internal()", "Inl_UniversalRenderPipeline.RenderSingleCameraInternal",
        "UpdateScene.Culling", "Gfx.WaitForPresentOnGfxThread", "Gfx.WaitForRenderThread", "WaitForTargetFPS",
        "GC.Collect", "GUI.Repaint", "PostLateUpdate.UpdateAudio",
    };

    readonly List<float> ms = new List<float>(4096);
    readonly FrameTiming[] ft = new FrameTiming[1];
    double cpuMain, cpuRender, gpu; int ftCount;
    ProfilerRecorder setPass, tris, shadowCasters, gcAlloc;
    double sSetPass, sTris, sShadow, sGc, maxGc; int recCount;
    readonly List<(string name, ProfilerRecorder rec)> timers = new List<(string, ProfilerRecorder)>();
    double[] tSum, tMax;
    float t;
    bool started;
    int skip;
    string beginInfo = "";

    void Begin()
    {
        started = true;
        setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        // markerat ekzistojnë pasi janë thirrur një herë (prandaj pas ngrohjes); skriptet shfaqen si "Emri.Update() [Invoke]"
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var h in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;
            string n = d.Name;
            if (!Wanted(n)) continue;
            var r = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default);
            r.Start();
            timers.Add((n, r));
        }
        tSum = new double[timers.Count]; tMax = new double[timers.Count];
    }

    static bool Wanted(string n) =>
        Markers.Contains(n) || n.StartsWith("Tempulli.") || n.EndsWith("[Invoke]") ||
        n.StartsWith("Initialization.") || n.StartsWith("EarlyUpdate.") || n.StartsWith("PreUpdate.") ||
        n.StartsWith("Update.") || n.StartsWith("FixedUpdate.") || n.StartsWith("PreLateUpdate.") || n.StartsWith("PostLateUpdate.") ||
        n.StartsWith("Gfx.") || n.Contains("WaitFor") || n.StartsWith("Editor") || n.StartsWith("Inl_") ||
        n.Contains("RenderPipeline") || n.Contains("RenderCamera");

    void Start()
    {
        if (uncapped) { Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; }
    }

    void OnDisable()
    {
        if (!started) return;
        setPass.Dispose(); tris.Dispose(); shadowCasters.Dispose(); gcAlloc.Dispose();
        foreach (var x in timers) x.rec.Dispose();
        timers.Clear();
        started = false;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (Done || t < warmup) return;
        if (!started)
        {
            float t0 = Time.realtimeSinceStartup;
            Begin();
            beginInfo = $"{timers.Count} markera, përgatitja {Time.realtimeSinceStartup - t0:0.0} s";
            skip = 3;   // frame-t menjëherë pas përgatitjes nuk numërohen
            return;
        }
        if (skip > 0) { skip--; t = warmup; return; }
        if (t > warmup + duration) { Finish(); return; }

        ms.Add(Time.unscaledDeltaTime * 1000f);
        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, ft) > 0 && ft[0].cpuMainThreadFrameTime > 0)
        {
            cpuMain += ft[0].cpuMainThreadFrameTime; cpuRender += ft[0].cpuRenderThreadFrameTime; gpu += ft[0].gpuFrameTime; ftCount++;
        }
        if (setPass.Valid)
        {
            sSetPass += setPass.LastValue; sTris += tris.LastValue; sShadow += shadowCasters.LastValue;
            sGc += gcAlloc.LastValue; if (gcAlloc.LastValue > maxGc) maxGc = gcAlloc.LastValue; recCount++;
        }
        for (int i = 0; i < timers.Count; i++)
        {
            double v = timers[i].rec.LastValue * 1e-6;   // ns -> ms
            tSum[i] += v; if (v > tMax[i]) tMax[i] = v;
        }
    }

    void Finish()
    {
        var sb = new StringBuilder();
        sb.AppendLine("== " + label + " ==  (" + beginInfo + ")");
        if (ms.Count == 0) { sb.AppendLine("asnjë frame u mat"); Result = sb.ToString(); Done = true; return; }
        float total = 0f; foreach (var m in ms) total += m;
        var sorted = new List<float>(ms); sorted.Sort();
        float P(float q) => sorted[Mathf.Clamp(Mathf.RoundToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
        int over20 = 0, over33 = 0; foreach (var m in ms) { if (m > 20f) over20++; if (m > 33.3f) over33++; }
        int n = ms.Count;
        sb.AppendLine($"frames {n}, FPS mesatare {n / (total / 1000f):0.0}");
        sb.AppendLine($"koha e frame-it ms: mesatare {total / n:0.00}, p50 {P(0.5f):0.00}, p95 {P(0.95f):0.00}, p99 {P(0.99f):0.00}, max {sorted[n - 1]:0.00}");
        sb.AppendLine($"frame > 20 ms: {over20}, frame > 33 ms: {over33}");
        if (ftCount > 0)
            sb.AppendLine($"FrameTiming ms (mesatare): CPU main {cpuMain / ftCount:0.00}, CPU render {cpuRender / ftCount:0.00}, GPU {gpu / ftCount:0.00}");
        if (recCount > 0)
            sb.AppendLine($"render (mesatare): setpass {sSetPass / recCount:0}, trekendesha {sTris / recCount:0}, shadow casters {sShadow / recCount:0}; GC/frame mesatare {sGc / recCount:0} B, max {maxGc:0} B");
        // pjesët më të shtrenjta (mesatare dhe maksimumi për frame)
        var idx = new List<int>(); for (int i = 0; i < timers.Count; i++) idx.Add(i);
        idx.Sort((a, b) => tSum[b].CompareTo(tSum[a]));
        sb.AppendLine("koha sipas pjesëve (ms/frame mesatare | max):");
        for (int k = 0; k < idx.Count && k < 40; k++)
        {
            int i = idx[k];
            if (tSum[i] / n < 0.01 && tMax[i] < 1) continue;
            sb.AppendLine($"  {timers[i].name,-55} {tSum[i] / n,7:0.00} | {tMax[i],7:0.00}");
        }
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) sb.AppendLine($"lojtari: distanca {player.Distance:0} m, i vdekur: {(player.IsDead ? "PO" : "JO")}");
        sb.AppendLine($"ekrani {Screen.width}x{Screen.height}, cilesia {QualitySettings.names[QualitySettings.GetQualityLevel()]}, targetFrameRate {Application.targetFrameRate}, vSync {QualitySettings.vSyncCount}");
        Result = sb.ToString();
        Done = true;
    }
}
