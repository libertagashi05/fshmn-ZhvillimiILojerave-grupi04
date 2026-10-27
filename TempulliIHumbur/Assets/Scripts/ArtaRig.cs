using UnityEngine;
using UnityEngine.Animations.Rigging;

// Rregullime procedurale mbi animacionet (Animation Rigging), vendoset te "Arta":
//  - koka/qafa shikojnë përpara dhe, afër kthesës, nga drejtimi i ri (si vrapues i vërtetë)
//  - në litar duart kapin litarin me IK (sido që të lëvizë trupi)
// Peshat ndryshojnë butë; në vdekje çdo gjë fiket që animacioni të luhet i pastër.
[DefaultExecutionOrder(100)]   // pas PlayerController, para Animator-it
public class ArtaRig : MonoBehaviour
{
    public PlayerController player;
    public MultiAimConstraint headAim, neckAim;
    public TwoBoneIKConstraint handL, handR;
    public Transform lookTarget, handTargetL, handTargetR;
    [Range(0, 1)] public float lookWeight = 0.65f;
    public float lookDistance = 12f;
    float wLook, wHands;
    Vector3 lookPos;

    void Start() { if (player) lookPos = player.transform.position + player.Forward * lookDistance + Vector3.up * 1.6f; }

    void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;
        var p = player.transform.position;
        Vector3 fwd = player.Forward;
        Vector3 want = p + fwd * lookDistance + Vector3.up * 1.6f;
        // afër kthesës koka kthehet nga rruga e re
        var sp = TrackSpawner.Instance;
        var j = sp != null ? sp.NextJunction() : null;
        if (j != null)
        {
            float along = Vector3.Dot(p - j.center, fwd);
            if (along > -16f && along < 2f)
            {
                float k = Mathf.InverseLerp(-16f, -4f, along);
                want = Vector3.Lerp(want, j.center + j.newDir * lookDistance + Vector3.up * 1.6f, k * 0.8f);
            }
        }
        lookPos = Vector3.Lerp(lookPos, want, 1f - Mathf.Exp(-6f * dt));
        if (lookTarget) lookTarget.position = lookPos;

        bool alive = !player.IsDead;
        float lw = alive && !player.OnZipline ? (player.IsSliding ? lookWeight * 0.4f : lookWeight) : 0f;
        float hw = alive && player.OnZipline ? 1f : 0f;
        wLook = Mathf.MoveTowards(wLook, lw, dt * 3f);
        wHands = Mathf.MoveTowards(wHands, hw, dt * 4f);
        if (headAim) headAim.weight = wLook;
        if (neckAim) neckAim.weight = wLook * 0.5f;
        if (handL) handL.weight = wHands;
        if (handR) handR.weight = wHands;
    }
}
