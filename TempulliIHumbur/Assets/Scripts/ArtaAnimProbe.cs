using System.Collections.Generic;
using System.Text;
using UnityEngine;

// TESTI I ANIMACIONEVE (shtohet vetë kur zgjidhet "Testo animacionet" në menu).
// - Ndryshon shpejtësinë çdo 15 s: 10 -> 14 -> 18 m/s (vrap, mes, sprint)
// - Autopiloti bën kërcime/rrëshqitje të shpeshta (kalime të shpejta mes gjendjeve)
// - Mat "këputjet": shpejtësinë këndore të kockave kryesore pas animacionit + rig-ut.
//   Një kërcim i papritur (snap) duket si kulm i madh në një frame të vetëm.
// - Çdo 15 s shkruan në Console: gjendjet e vizituara, kalimet, kulmet dhe ku ndodhën.
[DefaultExecutionOrder(1000)]
public class ArtaAnimProbe : MonoBehaviour
{
    public PlayerController player;
    public float snapDegPerSec = 2200f;    // mbi këtë = këputje e dukshme
    static readonly float[] Speeds = { 10f, 14f, 18f };
    Animator anim;
    Transform[] bones;
    Quaternion[] prev;
    readonly Dictionary<string, int> visits = new Dictionary<string, int>();
    readonly Dictionary<string, int> trans = new Dictionary<string, int>();
    readonly Dictionary<string, float> peakByState = new Dictionary<string, float>();
    string lastState = "";
    int snaps, frames, phase;
    static int runs;
    void Again() { if (PlayerController.StressTest && GameManager.Instance != null) GameManager.Instance.Restart(true); }
    float t0, worst;
    string worstWhere = "";

    static readonly HumanBodyBones[] Watch = { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm };

    void Start()
    {
        anim = GetComponent<Animator>();
        var list = new List<Transform>();
        if (anim != null && anim.isHuman) foreach (var b in Watch) { var t = anim.GetBoneTransform(b); if (t) list.Add(t); }
        bones = list.ToArray(); prev = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++) prev[i] = bones[i].localRotation;
        t0 = Time.time;
        PlayerController.TestSpeed = Speeds[0];
        Debug.Log("Tempulli TEST: filloi testi i animacioneve (" + bones.Length + " kocka nën vëzhgim).");
    }

    static string Name(AnimatorStateInfo s, Animator a)
    {
        foreach (var n in new[] { "Vrapi", "Kercimi_Ngritje", "Kercimi_Ajer", "Kercimi_Renie", "Ulja", "Rreshqitja_Hyrje", "Rreshqitja",
                                  "Rreshqitja_Dalje", "Kthesa_Majtas", "Kthesa_Djathtas", "Litari", "Pengimi", "Vdekja" })
            if (s.IsName(n)) return n;
        return "?";
    }

    void LateUpdate()
    {
        if (anim == null || player == null) return;
        if (player.IsDead)
        {
            if (frames > 0) { Report(true); Invoke(nameof(Again), 2.5f); }   // testi vazhdon: loja rifillon vetë
            return;
        }
        if (GameManager.Instance != null && !GameManager.Instance.Playing) return;
        float dt = Time.deltaTime; if (dt <= 0f) return;
        frames++;
        string st = Name(anim.GetCurrentAnimatorStateInfo(0), anim);
        if (anim.IsInTransition(0)) st += "→" + Name(anim.GetNextAnimatorStateInfo(0), anim);
        string baseSt = st.Split('→')[0];
        visits[baseSt] = (visits.TryGetValue(baseSt, out var v) ? v : 0) + 1;
        if (st != lastState && st.Contains("→")) trans[st] = (trans.TryGetValue(st, out var c) ? c : 0) + 1;
        lastState = st;
        for (int i = 0; i < bones.Length; i++)
        {
            float w = Quaternion.Angle(prev[i], bones[i].localRotation) / dt;
            prev[i] = bones[i].localRotation;
            if (w > (peakByState.TryGetValue(st, out var pk) ? pk : 0f)) peakByState[st] = w;
            if (w > snapDegPerSec) { snaps++; if (w > worst) { worst = w; worstWhere = bones[i].name + " te " + st + " (" + player.Speed.ToString("0") + " m/s)"; } }
        }
        if (Time.time - t0 > 15f)
        {
            Report(false);
            phase = (phase + 1) % Speeds.Length;
            PlayerController.TestSpeed = Speeds[phase];
            t0 = Time.time;
        }
    }

    void Report(bool end)
    {
        var sb = new StringBuilder("Tempulli TEST " + (end ? "(fund) " : "") + "shpejtësia " + player.Speed.ToString("0") + " m/s, " + frames + " frame: ");
        sb.Append("gjendjet {"); foreach (var kv in visits) sb.Append(kv.Key + ":" + kv.Value + " "); sb.Append("} ");
        sb.Append("kalimet {"); foreach (var kv in trans) sb.Append(kv.Key + ":" + kv.Value + " "); sb.Append("} ");
        sb.Append("këputje(>" + snapDegPerSec + "°/s): " + snaps + (snaps > 0 ? " më e madhja " + worst.ToString("0") + "°/s " + worstWhere : ""));
        float top = 0; string topSt = "";
        foreach (var kv in peakByState) if (kv.Value > top) { top = kv.Value; topSt = kv.Key; }
        sb.Append(" | kulmi " + top.ToString("0") + "°/s te " + topSt);
        Debug.Log(sb.ToString());
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/tempulli_testi_animacioneve.txt"), System.DateTime.Now.ToString("HH:mm:ss ") + sb + "\n"); } catch { }
        visits.Clear(); trans.Clear(); peakByState.Clear(); snaps = 0; worst = 0; frames = 0;
        if (end) runs++;
    }
}
