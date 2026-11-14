using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Tempulli i Humbur - analizon çdo klip të Artës (pa Play): mostron klipin 120 herë/s mbi modelin e Artës
// (dy cikle, që të shihet edhe kalimi fund->fillim) dhe gjen ku kockat rrotullohen papritur.
// Raporti: Logs/tempulli_analiza_klipeve.txt
public static class TempulliAnaliza
{
    static readonly HumanBodyBones[] Watch = { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
        HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm };

    [MenuItem("Tempulli/Analizo klipet e animacionit")]
    public static void Run()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Animations/ArtaAnimator.controller");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/arta.fbx");
        if (ctrl == null || model == null) { Debug.LogWarning("Tempulli: mungon kontrolleri ose modeli."); return; }
        var go = (GameObject)Object.Instantiate(model);
        go.hideFlags = HideFlags.HideAndDontSave;
        var anim = go.GetComponent<Animator>(); if (anim == null) anim = go.AddComponent<Animator>();
        var sb = new StringBuilder();
        sb.AppendLine("Analiza e klipeve " + System.DateTime.Now.ToString("HH:mm:ss"));
        var bones = new List<Transform>(); var names = new List<string>();
        foreach (var b in Watch) { var t = anim.GetBoneTransform(b); if (t) { bones.Add(t); names.Add(b.ToString()); } }
        var seen = new HashSet<AnimationClip>();
        foreach (var clip in ctrl.animationClips)
        {
            if (clip == null || !seen.Add(clip)) continue;
            const float hz = 120f; float dt = 1f / hz;
            bool loop = clip.isLooping;
            float total = loop ? clip.length * 2f : clip.length;
            var prev = new Quaternion[bones.Count];
            float worst = 0; string where = ""; int bad = 0;
            for (int i = 0; i <= Mathf.CeilToInt(total * hz); i++)
            {
                float t = i * dt; float ct = loop ? t % clip.length : Mathf.Min(t, clip.length);
                clip.SampleAnimation(go, ct);
                for (int k = 0; k < bones.Count; k++)
                {
                    var q = bones[k].localRotation;
                    if (i > 0)
                    {
                        float w = Quaternion.Angle(prev[k], q) / dt;
                        if (w > 2200f) bad++;
                        if (w > worst) { worst = w; where = names[k] + " @ " + ct.ToString("0.000") + "s (" + (ct / clip.length).ToString("0.00") + ")"; }
                    }
                    prev[k] = q;
                }
            }
            sb.AppendLine($"{clip.name,-10} gjatësia {clip.length:0.00}s loop={loop}  kulmi {worst:0}°/s te {where}  mostra>2200: {bad}");
            // muskujt (kurbat humanoide) me kërcimin më të madh midis dy mostrave
            var jumps = new List<(float d, string n)>();
            foreach (var bnd in AnimationUtility.GetCurveBindings(clip))
            {
                if (bnd.type != typeof(Animator)) continue;
                var cv = AnimationUtility.GetEditorCurve(clip, bnd); if (cv == null) continue;
                float mx = 0, at = 0, pv = cv.Evaluate(0), pa = 0, pb = 0;
                for (int i = 1; i <= Mathf.CeilToInt(clip.length * hz); i++)
                {
                    float tt = Mathf.Min(i * dt, clip.length), vv = cv.Evaluate(tt);
                    if (Mathf.Abs(vv - pv) > mx) { mx = Mathf.Abs(vv - pv); at = tt; pa = pv; pb = vv; }
                    pv = vv;
                }
                jumps.Add((mx, $"{bnd.propertyName} Δ{mx:0.000} @{at:0.000}s ({pa:0.00}->{pb:0.00})"));
            }
            jumps.Sort((x, y) => y.d.CompareTo(x.d));
            for (int j = 0; j < Mathf.Min(3, jumps.Count); j++) sb.AppendLine("      " + jumps[j].n);
        }
        Object.DestroyImmediate(go);
        File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/tempulli_analiza_klipeve.txt"), sb.ToString());
        Debug.Log(sb.ToString());
    }
}
