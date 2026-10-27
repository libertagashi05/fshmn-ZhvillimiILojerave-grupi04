using UnityEngine;
using Unity.Cinemachine;

// Kamera që ndjek Artën nga pas (vendoset te "Main Camera"; referenca: player).
// - Zbutje eksponenciale: e njëjta ndjesi në 30, 60 ose 144 FPS.
// - Ndjek tokën butë dhe vetëm një pjesë të lartësisë së kërcimit (pa lëkundje të tepërt).
// - Kthehet butë në kthesa, ulet pak kur rrëshqet, "mbledh" pak kur ulet pas kërcimit.
// - FOV rritet me shpejtësinë (ndjesi shpejtësie); dridhje e lehtë.
// - Nëse një mur/bordurë fsheh Artën, kamera afrohet (pengesat nuk e afrojnë).
// CINEMACHINE (nëse cmRun është vendosur): ky skript llogarit vetëm caqet (trupi i kamerës + pika ku shikon);
// CinemachineCamera "CM_Vrapi" i ndjek ato, dridhjet vijnë nga Impulse, dhe në vdekje Brain-i
// kalon butë te "CM_Vdekja" (pamje nga përpara e Artës). Pa Cinemachine, kamera lëvizet drejtpërdrejt si më parë.
[DefaultExecutionOrder(-100)]
public class CameraFollow : MonoBehaviour
{
    public PlayerController player;
    public float back = 7f, up = 4.6f, lookAhead = 6f, smooth = 7f, turnSpeed = 8f;
    public float jumpFollow = 0.35f;      // sa nga lartësia e kërcimit ndjek kamera (0-1)
    public float slideDrop = 0.7f;        // sa ulet kamera kur rrëshqet
    public float baseFov = 62f, speedFov = 9f;
    public float lookHeight = 1.3f;       // sa lart shikon kamera mbi këmbët e Artës
    [Header("Cinemachine (opsionale)")]
    public CinemachineCamera cmRun, cmDeath;
    public Transform cmBody, cmLook;       // caqet që ndjek CM_Vrapi
    public CinemachineImpulseSource impulse;
    Vector3 fwd = Vector3.forward;
    float shake, shakeSeed;
    float groundY, groundVel;
    float dip, dipVel;
    float slideNow;
    bool deathShot;
    Vector3 bodyPos;
    Camera cam;
    static readonly RaycastHit[] hits = new RaycastHit[16];
    static readonly Collider[] over = new Collider[64];
    float liftNow;

    bool UseCM => cmRun != null && cmBody != null && cmLook != null;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (player != null) { groundY = player.GroundY; fwd = player.Forward; }
        shakeSeed = Random.value * 100f;
        bodyPos = UseCM ? cmBody.position : transform.position;
    }

    public void Shake(float amount)
    {
        if (UseCM && impulse != null) impulse.GenerateImpulseWithForce(amount * 1.6f);
        else shake = Mathf.Max(shake, amount);
    }

    // thirret kur Arta ulet pas kërcimit: kamera bën një "mbledhje" të vogël
    public void Land(float strength = 1f) { dipVel -= 2.2f * strength; }

    void LateUpdate()
    {
        if (player == null) return;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float k = 1f - Mathf.Exp(-smooth * dt);

        fwd = Vector3.Slerp(fwd, player.Forward, 1f - Mathf.Exp(-turnSpeed * dt)).normalized;
        Vector3 p = player.transform.position;

        // lartësia: toka e zbutur + pjesë e kërcimit
        groundY = Mathf.SmoothDamp(groundY, player.GroundY, ref groundVel, 0.18f, Mathf.Infinity, dt);
        float jumpPart = Mathf.Max(0f, p.y - player.GroundY) * jumpFollow;
        if (player.OnZipline) jumpPart = p.y - player.GroundY;   // te litari ndjek plotësisht
        slideNow = Mathf.Lerp(slideNow, player.IsSliding ? 1f : 0f, 1f - Mathf.Exp(-8f * dt));
        // pranverë e vogël për uljen
        dipVel += (-dip * 60f - dipVel * 11f) * dt;
        dip += dipVel * dt;

        Vector3 basePos = new Vector3(p.x, groundY + jumpPart, p.z);
        Vector3 head = basePos + Vector3.up * 1.7f;
        Vector3 target = basePos - fwd * back + Vector3.up * (up - slideNow * slideDrop + dip * 0.35f);

        // shmang muret (jo pengesat dhe jo vetë Artën)
        Vector3 d = target - head;
        int n = Physics.SphereCastNonAlloc(head, 0.35f, d.normalized, hits, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c.transform.IsChildOf(player.transform) || c.GetComponentInParent<Obstacle>() != null) continue;
            if (hits[i].distance > 0f && hits[i].distance < best) best = hits[i].distance;
        }
        if (best < float.MaxValue) target = head + d.normalized * Mathf.Max(1.5f, best - 0.2f);
        // pengesat e larta (trau i portës) nuk afrojnë kamerën, por kamera ngrihet mbi to që të mos kalojë përmes tyre
        int m = Physics.OverlapCapsuleNonAlloc(target, target + fwd * 4f, 0.45f, over, ~0, QueryTriggerInteraction.Ignore);
        float lift = 0f;
        for (int i = 0; i < m; i++)
            if (over[i].GetComponentInParent<Obstacle>() != null) lift = Mathf.Max(lift, over[i].bounds.max.y + 0.55f - target.y);
        liftNow = Mathf.Lerp(liftNow, lift, 1f - Mathf.Exp(-(lift > liftNow ? 14f : 4f) * dt));
        target.y += liftNow;

        Vector3 lookPoint = basePos + Vector3.up * (lookHeight - slideNow * 0.4f) + fwd * lookAhead;
        float fov = baseFov;
        if (player.maxSpeed > player.startSpeed)
        {
            float s01 = Mathf.Clamp01((player.Speed - player.startSpeed) / (player.maxSpeed - player.startSpeed));
            fov = baseFov + s01 * speedFov + (player.OnZipline ? 4f : 0f);
        }

        if (UseCM)
        {
            // Cinemachine: ky skript lëviz vetëm caqet; kamera e vërtetë drejtohet nga CinemachineBrain
            bodyPos = Vector3.Lerp(bodyPos, target, k);
            cmBody.position = bodyPos;
            cmLook.position = lookPoint;
            cmRun.Lens.FieldOfView = Mathf.Lerp(cmRun.Lens.FieldOfView, fov, 1f - Mathf.Exp(-3f * dt));
            if (player.IsDead && !deathShot && cmDeath != null)
            {
                deathShot = true;
                cmDeath.Priority = cmRun.Priority + 10;   // Brain-i bën kalim të butë te pamja e vdekjes
            }
            return;
        }

        Vector3 pos = Vector3.Lerp(transform.position, target, k);
        // dridhje e butë (Perlin) që shuhet
        if (shake > 0.001f)
        {
            float tt = Time.time * 25f;
            pos += new Vector3(Mathf.PerlinNoise(shakeSeed, tt) - 0.5f, Mathf.PerlinNoise(shakeSeed + 7f, tt) - 0.5f, 0f) * (shake * 2f);
            shake = Mathf.MoveTowards(shake, 0f, dt * 0.9f);
        }
        transform.position = pos;

        Vector3 look = lookPoint - transform.position;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-10f * dt));
        if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-3f * dt));
    }
}
