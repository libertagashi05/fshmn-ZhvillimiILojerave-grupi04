using System.Collections.Generic;
using UnityEngine;

// Tempulli i Humbur - "zbutësi i pozës" (inertialization e thjeshtë) mbi Artën.
// Kur Animator-i ndërpret një kalim me një tjetër (p.sh. ulja -> vrap ndërpritet nga rrëshqitja), Unity e hedh
// pozën e përzier dhe trupi "kërcen" në një frame (u matën deri në 111° te gjuri). Ky komponent e vëren kërcimin
// (rrotullim më i shpejtë se çdo lëvizje e vërtetë e animacioneve) dhe e shuan butë për ~0.14 s, nga poza e
// mëparshme drejt pozës së re. Lëvizjet normale (deri ~1500°/s te mocap-i) nuk preken.
[DefaultExecutionOrder(900)]   // pas Animator-it dhe Animation Rigging, para testit të animacioneve (1000)
public class ArtaZbutesi : MonoBehaviour
{
    public float maxDegPerSec = 1800f;
    public float blendTime = 0.14f;

    Transform[] bones;
    Quaternion[] lastRaw, lastOut, offset;
    Transform hips;
    Vector3 lastRawHip, lastOutHip, hipOffset;
    float t = 1f;
    bool ready;
    static int logs;

    void Start()
    {
        var anim = GetComponent<Animator>();
        var list = new List<Transform>();
        if (anim != null && anim.isHuman)
        {
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var b = anim.GetBoneTransform((HumanBodyBones)i);
                if (b != null) list.Add(b);
            }
            hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        }
        bones = list.ToArray();
        lastRaw = new Quaternion[bones.Length]; lastOut = new Quaternion[bones.Length]; offset = new Quaternion[bones.Length];
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (bones == null || bones.Length == 0 || dt <= 0f) return;
        if (!ready)
        {
            for (int i = 0; i < bones.Length; i++) lastRaw[i] = lastOut[i] = bones[i].localRotation;
            if (hips) lastRawHip = lastOutHip = hips.localPosition;
            ready = true; return;
        }
        // kërcim = një kockë rrotullohet më shpejt se lejohet (krahasuar me pozën e papërpunuar të frame-it të kaluar)
        float lim = maxDegPerSec * dt;
        bool pop = false; float maxA = 0f; int maxI = 0;
        for (int i = 0; i < bones.Length; i++)
        {
            float a = Quaternion.Angle(lastRaw[i], bones[i].localRotation);
            if (a > maxA) { maxA = a; maxI = i; }
        }
        pop = maxA > lim;
        if (pop && PlayerController.StressTest && logs < 20)
        {
            logs++;
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/tempulli_keputjet.txt"),
                $"{System.DateTime.Now:HH:mm:ss} ZBUTËSI: kërcim {maxA:0}° te {bones[maxI].name} frame {Time.frameCount} -> u zbut\n"); } catch { }
        }
        for (int i = 0; i < bones.Length; i++) lastRaw[i] = bones[i].localRotation;
        Vector3 rawHip = hips ? hips.localPosition : Vector3.zero;
        if (pop)
        {
            for (int i = 0; i < bones.Length; i++) offset[i] = lastOut[i] * Quaternion.Inverse(bones[i].localRotation);
            hipOffset = lastOutHip - rawHip;
            t = 0f;
        }
        if (t < 1f)
        {
            t = Mathf.Min(1f, t + dt / blendTime);
            float k = 1f - Mathf.SmoothStep(0f, 1f, t);
            for (int i = 0; i < bones.Length; i++)
                bones[i].localRotation = Quaternion.Slerp(Quaternion.identity, offset[i], k) * bones[i].localRotation;
            if (hips) hips.localPosition = rawHip + hipOffset * k;
        }
        for (int i = 0; i < bones.Length; i++) lastOut[i] = bones[i].localRotation;
        lastRawHip = rawHip;
        if (hips) lastOutHip = hips.localPosition;
    }
}
