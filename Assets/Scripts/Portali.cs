using UnityEngine;
using UnityEngine.SceneManagement;

// Portali: hark guri me një vorbull drite që rrotullohet në mes. Kur Arta vrapon përmes tij,
// ekrani errësohet për një çast dhe ajo del në një botë tjetër (Udhetimi ruan pikët, distancën dhe monedhat).
// Ngjyra e vorbullës tregon ku të çon: e artë = tempulli, vjollcë = nata, e kaltër = akulli, portokalli = vullkani.
// Krijohet nga TrackSpawner (SpawnPortal); harkun prej guri e ndërton ai, ky skript shton vorbullën dhe trigger-in.
public class Portali : MonoBehaviour
{
    public int bota;                 // indeksi te Udhetimi.Botet
    bool perdorur;
    Transform vorbull1, vorbull2, berthama;
    Vector3 berthamaMadhesia = Vector3.one;
    ParticleSystem grimcat;
    Material[] mats;
    float fara;
    static Texture2D spiralja;
    static Portali iAfert;           // portali më i afërt para Artës (për mesazhin në ekran)
    static GUIStyle stili;

    public static Portali Krijo(Transform parent, Vector3 localPos, int bota, float gjeresia, float lartesia, Material flame)
    {
        var go = new GameObject("Portali_" + Udhetimi.Botet[bota].skena);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var p = go.AddComponent<Portali>();
        p.bota = bota;
        p.Nderto(gjeresia, lartesia, flame);
        return p;
    }

    void Nderto(float w, float h, Material flame)
    {
        fara = Random.value * 10f;
        Color c = Udhetimi.Botet[bota].ngjyra;
        // trigger-i: i gjithë hapi i harkut (Arta s'mund ta kalojë pa hyrë)
        var bc = gameObject.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center = new Vector3(0, h * 0.5f, 0);
        bc.size = new Vector3(w + 1f, h, 1.2f);

        if (flame == null) return;
        var spiral = new Material(flame) { name = "Portali_Spirale" };
        spiral.SetTexture("_BaseMap", Spiralja());
        spiral.SetColor("_BaseColor", c * 2.2f);
        var spiral2 = new Material(spiral) { name = "Portali_Spirale2" };
        spiral2.SetColor("_BaseColor", Color.Lerp(c, Color.white, 0.35f) * 1.4f);
        var glow = new Material(flame) { name = "Portali_Drita" };   // tekstura e butë e grimcave = disk drite
        glow.SetColor("_BaseColor", c * 1.6f);
        var core = new Material(flame) { name = "Portali_Berthama" };
        core.SetColor("_BaseColor", Color.Lerp(c, Color.white, 0.7f) * 2.5f);
        mats = new[] { spiral, spiral2, glow, core };

        Vector3 mid = new Vector3(0, h * 0.5f, 0.05f);
        Quad("Portali_Shkelqimi", mid + new Vector3(0, 0, 0.1f), new Vector3(w * 1.25f, h * 1.15f, 1f), glow);
        vorbull1 = Quad("Portali_Vorbulla", mid, new Vector3(w, h, 1f), spiral);
        vorbull2 = Quad("Portali_Vorbulla2", mid - new Vector3(0, 0, 0.05f), new Vector3(w * 0.8f, h * 0.8f, 1f), spiral2);
        berthama = Quad("Portali_Berthama", mid - new Vector3(0, 0, 0.1f), new Vector3(w * 0.35f, h * 0.35f, 1f), core);
        berthamaMadhesia = berthama.localScale;

        // grimca që thithen drejt qendrës
        var pg = new GameObject("Portali_Grimcat");
        pg.transform.SetParent(transform, false);
        pg.transform.localPosition = mid;
        grimcat = pg.AddComponent<ParticleSystem>();
        grimcat.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = grimcat.main;
        main.loop = true; main.playOnAwake = false; main.prewarm = true;
        main.startLifetime = 1.4f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = c * 2f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 200;
        var em = grimcat.emission; em.rateOverTime = 70f;
        var sh = grimcat.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = Mathf.Min(w, h) * 0.55f; sh.radiusThickness = 0.15f;
        var vel = grimcat.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(2.2f);
        vel.radial = new ParticleSystem.MinMaxCurve(-2.2f);
        var col = grimcat.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var pr = pg.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = flame;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        grimcat.Play();

        // drita e portalit ndriçon harkun dhe shtegun
        var lg = new GameObject("Portali_Drita");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = mid + new Vector3(0, 0, -1.5f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = 16f; l.intensity = 5f; l.shadows = LightShadows.None;
    }

    Transform Quad(string name, Vector3 pos, Vector3 scale, Material m)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(transform, false);
        q.transform.localPosition = pos;
        q.transform.localScale = scale;
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q.transform;
    }

    // spirale me 3 krahë që zbehet nga qendra (krijohet një herë)
    static Texture2D Spiralja()
    {
        if (spiralja != null) return spiralja;
        const int N = 128;
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u);
                float arms = 0.5f + 0.5f * Mathf.Sin(3f * a + r * 9f);
                float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 3f + 0.25f);
                float val = Mathf.Pow(arms, 2.2f) * fade + Mathf.Clamp01(1f - r * 2.2f) * 0.6f;
                px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(val));
            }
        spiralja = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Portali_Spiralja" };
        spiralja.SetPixels(px);
        spiralja.Apply(true, true);
        return spiralja;
    }

    void Update()
    {
        float t = Time.time + fara;
        if (vorbull1 != null) vorbull1.localRotation = Quaternion.Euler(0, 0, t * 95f);
        if (vorbull2 != null) vorbull2.localRotation = Quaternion.Euler(0, 0, -t * 140f);
        if (berthama != null) berthama.localScale = berthamaMadhesia * (1f + 0.1f * Mathf.Sin(t * 5f));

        // mesazhi në ekran: ky portal është i afërt dhe para Artës
        var pl = GameManager.Instance != null ? GameManager.Instance.player : null;
        if (pl == null || perdorur) { if (iAfert == this) iAfert = null; return; }
        float para = Vector3.Dot(transform.position - pl.transform.position, pl.Forward);
        bool afer = para > 0f && para < 60f && (transform.position - pl.transform.position).sqrMagnitude < 70f * 70f;
        if (afer) iAfert = this;
        else if (iAfert == this) iAfert = null;
    }

    public void Hyr(PlayerController p)
    {
        if (perdorur || p == null || p.IsDead) return;
        perdorur = true;
        if (iAfert == this) iAfert = null;
        p.OnEvent?.Invoke("portal");
        KalimiPortalit.Fillo(p, bota);
    }

    void OnGUI()
    {
        if (iAfert != this || GameManager.Instance == null || !GameManager.Instance.Playing) return;
        float s = Mathf.Min(Screen.height / 720f, Screen.width / 1280f) * 1.1f;
        if (stili == null) stili = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        stili.fontSize = Mathf.RoundToInt(26 * s);
        Color c = Udhetimi.Botet[bota].ngjyra;
        float puls = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
        var r = new Rect(0, Screen.height * 0.16f, Screen.width, 44 * s);
        var old = GUI.color;
        stili.normal.textColor = Color.white;
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), "PORTALI  →  " + Udhetimi.Botet[bota].emri.ToUpper(), stili);
        GUI.color = new Color(Mathf.Lerp(c.r, 1f, 0.3f), Mathf.Lerp(c.g, 1f, 0.3f), Mathf.Lerp(c.b, 1f, 0.3f), puls);
        GUI.Label(r, "PORTALI  →  " + Udhetimi.Botet[bota].emri.ToUpper(), stili);
        GUI.color = old;
    }

    void OnDestroy()
    {
        if (iAfert == this) iAfert = null;
        if (mats != null) foreach (var m in mats) if (m != null) Destroy(m);
    }
}

// Kalimi mes botëve: ekrani errësohet (me ngjyrën e portalit), ngarkohet skena tjetër dhe ndriçohet sërish.
// Mbijeton ndërrimin e skenës (DontDestroyOnLoad) dhe fshihet vetë në fund.
public class KalimiPortalit : MonoBehaviour
{
    const float Erresimi = 0.4f, Mbajtja = 0.25f, Ndricimi = 0.6f;
    static KalimiPortalit aktiv;
    PlayerController player;
    int bota, faza;      // 0 errësim, 1 pritje pas ngarkimit, 2 ndriçim
    float a, koha;
    Texture2D tex;
    GUIStyle stili;

    public static bool NeKalim => aktiv != null && aktiv.faza == 0;

    public static void Fillo(PlayerController p, int bota)
    {
        if (aktiv != null) return;
        var go = new GameObject("Kalimi_Portalit");
        DontDestroyOnLoad(go);
        aktiv = go.AddComponent<KalimiPortalit>();
        aktiv.player = p;
        aktiv.bota = bota;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (faza == 0)
        {
            a = Mathf.Min(1f, a + dt / Erresimi);
            if (a >= 1f)
            {
                // ruaj gjendjen në çastin e ngarkimit dhe kalo në botën tjetër
                if (player != null)
                {
                    Udhetimi.DistancaPara += player.Distance;
                    Udhetimi.Monedhat = player.Coins;
                    Udhetimi.Shpejtesia = player.Speed;
                }
                Udhetimi.Portale++;
                Udhetimi.NeUdhetim = true;
                faza = 1; koha = 0f;
                Time.timeScale = 1f;
                SceneManager.LoadScene(Udhetimi.Botet[bota].skena);
            }
        }
        else if (faza == 1)
        {
            koha += dt;
            if (koha >= Mbajtja) faza = 2;
        }
        else
        {
            a = Mathf.Max(0f, a - dt / Ndricimi);
            if (a <= 0f) Destroy(gameObject);
        }
    }

    void OnGUI()
    {
        if (a <= 0f) return;
        GUI.depth = -50;
        if (tex == null) { tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply(); }
        Color c = Udhetimi.Botet[bota].ngjyra;
        var old = GUI.color;
        GUI.color = new Color(c.r * 0.08f, c.g * 0.08f, c.b * 0.08f, a);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), tex);
        if (faza > 0)
        {
            float s = Mathf.Min(Screen.height / 720f, Screen.width / 1280f) * 1.1f;
            if (stili == null) stili = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            stili.fontSize = Mathf.RoundToInt(54 * s);
            stili.normal.textColor = Color.white;
            GUI.color = new Color(Mathf.Lerp(c.r, 1f, 0.4f), Mathf.Lerp(c.g, 1f, 0.4f), Mathf.Lerp(c.b, 1f, 0.4f), a);
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 90 * s), Udhetimi.Botet[bota].emri.ToUpper(), stili);
        }
        GUI.color = old;
    }

    void OnDestroy()
    {
        if (aktiv == this) aktiv = null;
        if (tex != null) Destroy(tex);
    }
}
