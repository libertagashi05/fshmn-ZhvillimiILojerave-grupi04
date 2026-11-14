using System.Collections.Generic;
using UnityEngine;

// Tempulli i Humbur - vagoni i varrit: Arta udhëton në një vagon mbi shina (tema.vagon).
// Vagoni është vetëm pamje (pa collider): përplasjet i kontrollon PlayerController (anohu për gurët anash, ulu për trarët).
// Shtohet nga BotaAplikuesi; materialet merren nga TrackSpawner (druri, hekuri, ari i botës).
public class Vagoni : MonoBehaviour
{
    public PlayerController player;
    Transform trupi;
    readonly List<Transform> rrotat = new List<Transform>();
    float rrotullimi;

    void Start()
    {
        if (player == null) player = GetComponent<PlayerController>();
        var sp = TrackSpawner.Instance;
        Material hekur = sp != null ? sp.stoneDark : null;
        Material dru = sp != null && sp.wood != null ? sp.wood : hekur;
        Material ar = sp != null && sp.gold != null ? sp.gold : hekur;

        trupi = new GameObject("Vagoni").transform;
        trupi.SetParent(transform, false);
        // trupi i hapur: dyshemeja, dy anët, përpara dhe mbrapa
        Kuti("Vagoni_Dysheme", new Vector3(0, 0.22f, 0), new Vector3(1.25f, 0.1f, 1.7f), hekur);
        // muret e ulëta (deri në 0.66 m), që Arta të shihet nga beli e lart
        Kuti("Vagoni_Majtas", new Vector3(-0.6f, 0.44f, 0), new Vector3(0.08f, 0.44f, 1.7f), dru);
        Kuti("Vagoni_Djathtas", new Vector3(0.6f, 0.44f, 0), new Vector3(0.08f, 0.44f, 1.7f), dru);
        Kuti("Vagoni_Para", new Vector3(0, 0.44f, 0.82f), new Vector3(1.25f, 0.44f, 0.08f), dru);
        Kuti("Vagoni_Mbrapa", new Vector3(0, 0.44f, -0.82f), new Vector3(1.25f, 0.44f, 0.08f), dru);
        // brezat prej hekuri dhe buza e artë
        foreach (float y in new[] { 0.32f, 0.56f })
            for (int s = -1; s <= 1; s += 2)
                Kuti("Vagoni_Brez", new Vector3(s * 0.645f, y, 0), new Vector3(0.02f, 0.07f, 1.72f), hekur);
        for (int s = -1; s <= 1; s += 2)
        {
            Kuti("Vagoni_Buza", new Vector3(s * 0.6f, 0.68f, 0), new Vector3(0.14f, 0.05f, 1.78f), ar);
            Kuti("Vagoni_Buza", new Vector3(0, 0.68f, s * 0.82f), new Vector3(1.33f, 0.05f, 0.14f), ar);
        }
        // rrotat
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var r = Pjese(PrimitiveType.Cylinder, "Vagoni_Rrota", hekur);
                r.localPosition = new Vector3(sx * 0.5f, 0.15f, sz * 0.55f);
                r.localScale = new Vector3(0.3f, 0.04f, 0.3f);
                r.localRotation = Quaternion.Euler(0, 0, 90f);
                var bosht = Pjese(PrimitiveType.Cube, "Vagoni_Rrota_Kryq", ar);   // kryqi që tregon rrotullimin
                bosht.SetParent(r, false);
                bosht.localPosition = new Vector3(0, -sx * 1.05f, 0);   // në faqen e jashtme të rrotës
                bosht.localScale = new Vector3(0.9f, 0.4f, 0.12f);
                rrotat.Add(r);
            }
    }

    void LateUpdate()
    {
        if (trupi == null || player == null) return;
        // vagoni anohet pak bashkë me Artën dhe dridhet lehtë mbi shina
        float v = player.IsDead ? 0f : player.Speed;
        float dridhje = Mathf.Sin(Time.time * (6f + v * 0.9f)) * 0.012f * Mathf.Clamp01(v / 10f);
        trupi.localRotation = Quaternion.Euler(0, 0, -player.Prirja * 6f);
        trupi.localPosition = new Vector3(0, dridhje, 0);
        // Arta qëndron mbi dyshemenë e vagonit (jo në tokë), që të shihet mbi buzë nga beli e lart
        var model = player.animator != null ? player.animator.transform : null;
        if (model != null && model != player.transform) model.localPosition = new Vector3(0f, 0.32f + dridhje, 0f);
        rrotullimi += v * Time.deltaTime / 0.15f * Mathf.Rad2Deg;
        foreach (var r in rrotat) r.localRotation = Quaternion.Euler(rrotullimi, 0, 0) * Quaternion.Euler(0, 0, 90f);
    }

    Transform Kuti(string emri, Vector3 pos, Vector3 madhesia, Material mat)
    {
        var t = Pjese(PrimitiveType.Cube, emri, mat);
        t.localPosition = pos;
        t.localScale = madhesia;
        return t;
    }

    Transform Pjese(PrimitiveType lloji, string emri, Material mat)
    {
        var g = GameObject.CreatePrimitive(lloji);
        g.name = emri;
        DestroyImmediate(g.GetComponent<Collider>());   // menjëherë: përndryshe CharacterController përplaset me të
        g.transform.SetParent(trupi, false);
        if (mat != null) g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return g.transform;
    }
}
