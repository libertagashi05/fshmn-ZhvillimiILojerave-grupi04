using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Kontrolli automatik i pamjes (vetëm me Autopilot): çdo 0.2 s heq një rreze nga kamera te pengesa e ardhshme
// (brenda 60 m, në vijën ku vrapon Arta, para kthesës së radhës). Nëse diçka tjetër e mbulon, shkruhet në Console
// dhe te Logs/tempulli_pamja_bllokuar.txt (objekti, segmenti, distanca).
// Kontrollohen: objektet me collider (fizika) dhe skenari dekorativ pa collider (shkëmbinj, pemë, kulla, male),
// me rreze kundër trekëndëshave të mesh-it (jo vetëm kutisë rrethuese, që do të jepte alarme të rreme).
//
// Testi i plotë (menuja "Tempulli > Testo pamjen"): Autopilot ~3 minuta në secilën botë, pa portale;
// pas vdekjes vazhdon në të njëjtën botë; në fund shkruan përmbledhjen dhe ndalon Play.
public class PamjaKontrolli : MonoBehaviour
{
    public const string CelesiTestit = "Tempulli_TestPamja";
    public const float KohaBota = 180f;
    const float Intervali = 0.2f, Larg = 60f;

    public static bool TestAktiv;
    static int testBota;
    static float testKoha;
    static readonly int[] bllokime = new int[Udhetimi.Botet.Length];
    static readonly int[] vdekje = new int[Udhetimi.Botet.Length];
    static readonly float[] metra = new float[Udhetimi.Botet.Length];

    float tani, vdiqKoha = -1f;
    Obstacle lastObs; string lastBlocker;
    readonly List<TrackSpawner.Deko> deko = new List<TrackSpawner.Deko>();
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<int> tris = new List<int>();
    static readonly RaycastHit[] hits = new RaycastHit[32];
    static Obstacle[] obsCache = new Obstacle[0];
    float obsCacheTime = -10f;

    static string Skedari => Path.Combine(Application.isEditor ? Path.Combine(Application.dataPath, "..", "Logs") : Application.persistentDataPath, "tempulli_pamja_bllokuar.txt");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Nis()
    {
        TestAktiv = false;
        if (PlayerPrefs.GetInt(CelesiTestit, 0) != 1) return;
        PlayerPrefs.DeleteKey(CelesiTestit);
        TestAktiv = true;
        testBota = 0; testKoha = 0f;
        System.Array.Clear(bllokime, 0, bllokime.Length);
        System.Array.Clear(vdekje, 0, vdekje.Length);
        System.Array.Clear(metra, 0, metra.Length);
        PlayerController.Autopilot = true;
        PlayerController.StressTest = false;
        Udhetimi.NeUdhetim = true;   // pa menu: vrapi nis menjëherë
        Shkruaj("==== Testi i pamjes " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " — Autopilot " + KohaBota + " s në çdo botë ====", true);
    }

    static void Shkruaj(string rresht, bool iRi = false)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Skedari));
            if (iRi) File.WriteAllText(Skedari, rresht + "\n");
            else File.AppendAllText(Skedari, rresht + "\n");
        }
        catch (System.Exception e) { Debug.LogWarning("Tempulli: s'u shkrua " + Skedari + ": " + e.Message); }
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.player == null) return;
        var p = gm.player;
        if (TestAktiv) Testi(gm, p);
        if (!PlayerController.Autopilot || !gm.Playing || p.IsDead) return;
        tani -= Time.deltaTime;
        if (tani > 0f) return;
        tani = Intervali;
        Kontrollo(p);
    }

    // ---------- testi: koha në çdo botë, rinisja pas vdekjes, kalimi te bota tjetër ----------
    void Testi(GameManager gm, PlayerController p)
    {
        int b = Udhetimi.BotaAktuale;
        testKoha += Time.unscaledDeltaTime;
        metra[b] = Mathf.Max(metra[b], p.TotalDistance);
        if (p.IsDead)
        {
            if (vdiqKoha < 0f) { vdiqKoha = Time.unscaledTime; vdekje[b]++; Shkruaj("  vdiq: " + p.DeathReason + " (" + (int)p.TotalDistance + " m)"); }
            else if (Time.unscaledTime - vdiqKoha > 1.5f) Ngarko(SceneManager.GetActiveScene().name, p);
            return;
        }
        if (testKoha < KohaBota) return;
        // bota tjetër që ekziston
        Shkruaj("[" + Udhetimi.Botet[b].emri + "] bllokime: " + bllokime[b] + ", vdekje: " + vdekje[b] +
                (TrackSpawner.Instance != null ? ", dekor i fshehur: " + TrackSpawner.Instance.DekoTeFshehura + ", i shtyrë: " + TrackSpawner.Instance.DekoTeShtyra : ""));
        do testBota++; while (testBota < Udhetimi.Botet.Length && !Udhetimi.Ekziston(testBota));
        testKoha = 0f;
        if (testBota >= Udhetimi.Botet.Length) { Mbaro(); return; }
        Udhetimi.DistancaPara = 0f;
        Ngarko(Udhetimi.Botet[testBota].skena, null);
    }

    static void Ngarko(string skena, PlayerController p)
    {
        Udhetimi.NeUdhetim = true;
        Udhetimi.DistancaPara = 0f;
        Udhetimi.Monedhat = 0;
        Time.timeScale = 1f;
        SceneManager.LoadScene(skena);
    }

    void Mbaro()
    {
        TestAktiv = false;
        int gjithsej = 0;
        foreach (var n in bllokime) gjithsej += n;
        string msg = "==== Fund: " + gjithsej + " bllokime gjithsej " + (gjithsej == 0 ? "(OK)" : "(shiko rreshtat më lart)") + " ====";
        Shkruaj(msg);
        if (gjithsej == 0) Debug.Log("Tempulli: " + msg + "  " + Skedari);
        else Debug.LogWarning("Tempulli: " + msg + "  " + Skedari);
        PlayerController.Autopilot = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ---------- kontrolli i pamjes ----------
    void Kontrollo(PlayerController p)
    {
        var cam = Camera.main;
        var sp = TrackSpawner.Instance;
        if (cam == null || sp == null) return;
        // kamera brenda një kulle (kullat s'kanë collider, CameraFollow s'i shmang dot vetë)
        sp.DekoAktive(deko);
        foreach (var dk in deko)
            if (dk.t.name.StartsWith("Deko_Kulla") && dk.b.Contains(cam.transform.position) && lastBlocker != "kamera:" + Emri(dk.t))
            {
                lastBlocker = "kamera:" + Emri(dk.t);
                int bb = Udhetimi.BotaAktuale;
                bllokime[bb]++;
                string r = "[" + Udhetimi.Botet[bb].emri + "] " + (int)p.TotalDistance + " m: kamera brenda kullës \"" + Emri(dk.t) + "\"";
                Debug.LogWarning("Tempulli pamja: " + r);
                Shkruaj(r);
            }
        var obs = Pengesa(p, sp, out float largesia);
        if (obs == null) return;
        var col = obs.GetComponent<Collider>();
        Vector3 cak = col != null ? col.bounds.center : obs.transform.position;
        Vector3 nga = cam.transform.position;
        Vector3 d = cak - nga;
        float dist = d.magnitude;
        if (dist < 1f) return;
        var ray = new Ray(nga, d / dist);

        string blocker = null; float bDist = float.MaxValue;
        // 1. objektet me collider
        int n = Physics.RaycastNonAlloc(ray, hits, dist - 0.3f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c.transform.IsChildOf(p.transform) || c.GetComponentInParent<Obstacle>() != null) continue;
            if (hits[i].distance < bDist) { bDist = hits[i].distance; blocker = Emri(c.transform); }
        }
        // 2. skenari dekorativ (pa collider): kutia e parë, pastaj trekëndëshat
        sp.DekoAktive(deko);
        foreach (var dk in deko)
        {
            if (!dk.b.IntersectRay(ray, out float bd) || bd > dist) continue;
            if (RrezeMesh(dk.t, ray, dist, out float md) && md < bDist) { bDist = md; blocker = Emri(dk.t); }
        }
        if (blocker == null) return;

        if (obs == lastObs && blocker == lastBlocker) return;   // e njëjta pamje e bllokuar: shkruaje një herë
        lastObs = obs; lastBlocker = blocker;
        int b = Udhetimi.BotaAktuale;
        bllokime[b]++;
        string seg = obs.transform.root != null ? obs.transform.root.name : "-";
        string rresht = "[" + Udhetimi.Botet[b].emri + "] " + (int)p.TotalDistance + " m: pengesa " + obs.name + " (" + seg + ", " +
                        largesia.ToString("0") + " m para) e mbuluar nga \"" + blocker + "\" në " + bDist.ToString("0.0") + " m nga kamera";
        Debug.LogWarning("Tempulli pamja: " + rresht);
        Shkruaj(rresht);
    }

    static string Emri(Transform t)
    {
        var root = t.root;
        return (root != null && root != t ? root.name + "/" : "") + t.name;
    }

    // pengesa më e afërt para Artës, në vijën e saj, brenda 60 m dhe para kthesës së radhës
    Obstacle Pengesa(PlayerController p, TrackSpawner sp, out float largesia)
    {
        if (Time.time - obsCacheTime > 1f) { obsCache = FindObjectsByType<Obstacle>(FindObjectsSortMode.None); obsCacheTime = Time.time; }
        Vector3 o = p.transform.position, f = p.Forward, r = Vector3.Cross(Vector3.up, f);
        float kufiri = Larg;
        var j = sp.NextJunction();
        if (j != null) kufiri = Mathf.Min(kufiri, Vector3.Dot(j.center - o, f) - 1f);
        Obstacle best = null; largesia = float.MaxValue;
        foreach (var ob in obsCache)
        {
            if (ob == null || !ob.isActiveAndEnabled) continue;
            var c = ob.GetComponent<Collider>();
            if (c == null || !c.enabled) continue;
            Vector3 v = c.bounds.center - o;
            float para = Vector3.Dot(v, f);
            if (para < 3f || para > kufiri || para >= largesia) continue;
            if (Mathf.Abs(Vector3.Dot(v, r)) > sp.pathWidth * 0.5f + 1f || Mathf.Abs(v.y) > 6f) continue;
            if (ob.name.StartsWith("Muri_Perballe") || ob.name.StartsWith("Porta_Shtylla")) continue;   // muri i kthesës / shtyllat anash
            best = ob; largesia = para;
        }
        return best;
    }

    // rreze kundër trekëndëshave të çdo mesh-i aktiv të grupit (vetëm kur kutia rrethuese goditet)
    bool RrezeMesh(Transform g, Ray ray, float maxDist, out float dist)
    {
        dist = float.MaxValue;
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            var tr = mf.transform;
            Vector3 lo = tr.InverseTransformPoint(ray.origin), ld = tr.InverseTransformDirection(ray.direction);
            mesh.GetVertices(verts);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                mesh.GetTriangles(tris, s);
                for (int i = 0; i + 2 < tris.Count; i += 3)
                {
                    if (!Trekendesh(lo, ld, verts[tris[i]], verts[tris[i + 1]], verts[tris[i + 2]], out float tl)) continue;
                    float w = Vector3.Distance(ray.origin, tr.TransformPoint(lo + ld * tl));
                    if (w < maxDist && w < dist) dist = w;
                }
            }
        }
        return dist < maxDist;
    }

    // Möller–Trumbore (pa kujdes për anën e trekëndëshit)
    static bool Trekendesh(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0f;
        Vector3 e1 = b - a, e2 = c - a, pv = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, pv);
        if (Mathf.Abs(det) < 1e-7f) return false;
        float inv = 1f / det;
        Vector3 tv = o - a;
        float u = Vector3.Dot(tv, pv) * inv;
        if (u < 0f || u > 1f) return false;
        Vector3 qv = Vector3.Cross(tv, e1);
        float v = Vector3.Dot(d, qv) * inv;
        if (v < 0f || u + v > 1f) return false;
        t = Vector3.Dot(e2, qv) * inv;
        return t > 0f;
    }
}
