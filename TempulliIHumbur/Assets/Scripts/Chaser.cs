using System.Collections.Generic;
using UnityEngine;

// Rojtari prej guri që e ndjek lojtarin nga pas. Në fillim është afër, pastaj mbetet pas.
// Kur lojtari përplaset, rojtari e kap.
public class Chaser : MonoBehaviour
{
    public PlayerController player;
    public Material stone, moss, eyes;
    float gap = 5f;
    float t;
    readonly List<(Vector3 p, float d)> trail = new List<(Vector3, float)>();
    Transform legL, legR, armL, armR, body;

    void Start()
    {
        Build();
        if (player != null) transform.position = player.transform.position - Vector3.forward * gap;
    }

    void Build()
    {
        body = new GameObject("Trupi").transform;
        body.SetParent(transform, false);
        transform.localScale = Vector3.one * 0.7f;
        Part(body, PrimitiveType.Cube, new Vector3(0, 2.3f, 0), new Vector3(1.5f, 1.4f, 0.9f), stone);
        Part(body, PrimitiveType.Cube, new Vector3(0, 3.35f, 0.05f), new Vector3(0.8f, 0.7f, 0.75f), stone);
        Part(body, PrimitiveType.Cube, new Vector3(0, 3.75f, 0), new Vector3(0.85f, 0.12f, 0.8f), moss);
        Part(body, PrimitiveType.Cube, new Vector3(0, 2.9f, -0.1f), new Vector3(1.55f, 0.2f, 0.95f), moss);
        for (int s = -1; s <= 1; s += 2)
            Part(body, PrimitiveType.Sphere, new Vector3(s * 0.18f, 3.4f, 0.42f), new Vector3(0.14f, 0.1f, 0.06f), eyes);
        legL = Limb(new Vector3(-0.4f, 1.6f, 0), new Vector3(0.55f, 1.6f, 0.6f));
        legR = Limb(new Vector3(0.4f, 1.6f, 0), new Vector3(0.55f, 1.6f, 0.6f));
        armL = Limb(new Vector3(-1.0f, 2.9f, 0), new Vector3(0.45f, 1.5f, 0.5f));
        armR = Limb(new Vector3(1.0f, 2.9f, 0), new Vector3(0.45f, 1.5f, 0.5f));
    }

    Transform Limb(Vector3 pivot, Vector3 size)
    {
        var p = new GameObject("Pivot").transform;
        p.SetParent(body, false);
        p.localPosition = pivot;
        Part(p, PrimitiveType.Cube, new Vector3(0, -size.y / 2, 0), size, stone);
        return p;
    }

    void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        Destroy(g.GetComponent<Collider>());
    }

    void Update()
    {
        if (player == null) return;
        bool playing = GameManager.Instance == null || GameManager.Instance.Playing;
        if (!playing && !player.IsDead) return;

        if (!player.IsDead)
        {
            trail.Add((player.transform.position, player.Distance));
            if (trail.Count > 600) trail.RemoveAt(0);
            // pas një pengimi rojtari afrohet (si në Temple Run), pastaj mbetet sërish pas
            bool stumbled = Time.time - player.LastStumbleTime < 5f;
            gap = Mathf.MoveTowards(gap, stumbled ? 4.5f : 16f, Time.deltaTime * (stumbled ? 7f : 2.2f));
        }
        else gap = Mathf.MoveTowards(gap, 1.6f, Time.deltaTime * 14f);

        float want = player.Distance - gap;
        Vector3 pos = trail.Count > 0 ? trail[0].p : player.transform.position;
        for (int i = trail.Count - 1; i > 0; i--)
        {
            if (trail[i - 1].d <= want)
            {
                float k = Mathf.InverseLerp(trail[i - 1].d, trail[i].d, want);
                pos = Vector3.Lerp(trail[i - 1].p, trail[i].p, k);
                break;
            }
        }
        if (player.IsDead && gap < 3f) pos = Vector3.Lerp(transform.position, player.transform.position - player.Forward * gap, 0.2f);
        Vector3 move = pos - transform.position;
        move.y = 0;
        if (move.sqrMagnitude > 0.0004f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 10f * Time.deltaTime);
        transform.position = new Vector3(pos.x, Mathf.Max(0f, pos.y - 0.05f), pos.z);

        t += Time.deltaTime * (player.IsDead ? 4f : player.Speed * 0.55f);
        float sw = Mathf.Sin(t) * 35f;
        legL.localRotation = Quaternion.Euler(sw, 0, 0);
        legR.localRotation = Quaternion.Euler(-sw, 0, 0);
        armL.localRotation = Quaternion.Euler(-sw * 0.8f - (player.IsDead ? 70 : 0), 0, 0);
        armR.localRotation = Quaternion.Euler(sw * 0.8f - (player.IsDead ? 70 : 0), 0, 0);
        body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Cos(t)) * 0.15f, 0);
    }
}
