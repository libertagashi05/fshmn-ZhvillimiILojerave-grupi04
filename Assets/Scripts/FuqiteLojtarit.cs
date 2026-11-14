using System.Collections.Generic;
using UnityEngine;

// Fuqitë aktive të Artës (shtohet vetë nga PlayerController):
//  Magnet  (10 s) - monedhat brenda ~8 m fluturojnë te Arta
//  Mburojë (30 s ose deri në përplasjen e parë) - flluskë drite; përplasja e radhës thyen pengesën në vend që të vrasë
//  Vrap    (5 s)  - shpejtësi ~1.8x, pengesat thyhen, kthesat dhe gropat merren vetë; pastaj ngadalëson butë
// Në ekran: një kartë për çdo fuqi aktive (poshtë monedhave), me ikonë, unazë që zbrazet dhe sekondat e mbetura.
//  - hyn duke rrëshqitur nga e djathta me "pop"; ikona shkëlqen një herë
//  - kur rimerret e njëjta fuqi: unaza mbushet butë dhe karta ndriçon
//  - 3 sekondat e fundit: pulson, unaza bëhet e kuqe, "tik" çdo sekondë; në lojë flluska/aura paralajmërojnë
//  - kur mbaron: zvogëlohet dhe zhduket, kartat e tjera ngjiten butë
public class FuqiteLojtarit : MonoBehaviour
{
    public const float KohaMagnet = 10f, KohaMburoje = 30f, KohaVrap = 5f;
    const float RrezjaMagnet = 8f, Paralajmerim = 3f;

    public PlayerController player;
    float magnet, mburoje, vrap, pathyeshem, niveliVrap, kerkimi;
    float njoftimKoha = -10f; LlojiFuqise njoftimi;
    static readonly Collider[] buf = new Collider[48];

    // efektet në lojë
    Transform flluska; Material flluskaMat; float flluskaPlas;
    ParticleSystem gjurma, aura;
    float auraKoha;

    public float SpeedMul => 1f + 0.8f * niveliVrap;
    public bool Vrapon => vrap > 0f || niveliVrap > 0.35f;
    public bool Pathyeshem => Vrapon || pathyeshem > 0f;
    public bool KaMburoje => mburoje > 0f;

    bool Luhet => player != null && !player.IsDead && (GameManager.Instance == null || GameManager.Instance.Playing);

    float Mbetur(int i) => i == 0 ? magnet : i == 1 ? mburoje : vrap;
    static float Gjithsej(int i) => i == 0 ? KohaMagnet : i == 1 ? KohaMburoje : KohaVrap;

    public void Aktivizo(LlojiFuqise l)
    {
        switch (l)
        {
            case LlojiFuqise.Magnet: magnet = KohaMagnet; break;
            case LlojiFuqise.Mburoje: mburoje = KohaMburoje; break;
            case LlojiFuqise.Vrap: vrap = KohaVrap; break;
        }
        njoftimi = l; njoftimKoha = Time.time;
        KartaMerret((int)l);
        player.OnEvent?.Invoke("fuqi");
    }

    // thirret kur Arta përplaset me pengesë gjatë vrapit të shpejtë ose pas mburojës: pengesa thyhet
    public void Thyej(Collider c, Vector3 pika)
    {
        var obs = c.GetComponentInParent<Obstacle>();
        var root = obs != null ? obs.gameObject : c.gameObject;
        foreach (var col in root.GetComponentsInChildren<Collider>()) col.enabled = false;
        if (player.dust) player.dust.Emit(30);   // pluhuri është fëmijë i Artës: lëshohet aty ku është ajo
        if (player.sparkle) { player.sparkle.transform.position = pika + Vector3.up; player.sparkle.Emit(15); }
        player.OnEvent?.Invoke("smash");
    }

    // përplasje vdekjeprurëse: nëse ka mburojë, ajo shpenzohet dhe Arta shpëton
    public bool PerdorMburojen(Collider c, Vector3 pika)
    {
        if (mburoje <= 0f) return false;
        mburoje = 0f;
        pathyeshem = 1.5f;      // pak kohë pa përplasje, që të dalë nga pengesa
        flluskaPlas = 1f;
        Thyej(c, pika);
        return true;
    }

    void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;
        if (Luhet)
        {
            magnet = Mathf.Max(0f, magnet - dt);
            mburoje = Mathf.Max(0f, mburoje - dt);
            vrap = Mathf.Max(0f, vrap - dt);
            pathyeshem = Mathf.Max(0f, pathyeshem - dt);
            // vrapi: përshpejtim i shpejtë, ngadalësim i butë (~1.2 s)
            niveliVrap = Mathf.MoveTowards(niveliVrap, vrap > 0f ? 1f : 0f, dt * (vrap > 0f ? 3f : 0.85f));
            if (magnet > 0f) Magneti(dt);
        }
        else if (player.IsDead) { magnet = vrap = mburoje = 0f; niveliVrap = 0f; }
        Efektet(dt);
        KartatUpdate(Time.unscaledDeltaTime);
    }

    void Magneti(float dt)
    {
        kerkimi -= dt;
        if (kerkimi > 0f) return;
        kerkimi = 0.1f;
        int n = Physics.OverlapSphereNonAlloc(player.transform.position + Vector3.up, RrezjaMagnet, buf, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var c = buf[i].GetComponent<Coin>();
            if (c != null) c.Pull(player);
        }
    }

    // ================= efektet në lojë (3D) =================
    void Efektet(float dt)
    {
        float t = Time.time;
        // flluska e mburojës; në 3 sekondat e fundit dridhet dhe fiket-ndizet
        bool duket = mburoje > 0f || flluskaPlas > 0f;
        if (duket && flluska == null) KrijoFlluskën();
        if (flluska != null)
        {
            flluska.gameObject.SetActive(duket);
            if (duket)
            {
                float puls = 1f + 0.04f * Mathf.Sin(t * 6f);
                float alfa = 0.22f;
                Vector3 drid = Vector3.zero;
                if (flluskaPlas > 0f)
                {
                    flluskaPlas = Mathf.Max(0f, flluskaPlas - dt * 3f);   // plas: zmadhohet dhe zbehet
                    puls *= 1f + (1f - flluskaPlas) * 0.8f;
                    alfa *= flluskaPlas;
                }
                else if (mburoje < Paralajmerim)
                {
                    float shpejt = Mathf.Lerp(26f, 12f, mburoje / Paralajmerim);   // sa më afër fundit, aq më shpesh
                    alfa *= Mathf.PerlinNoise(t * shpejt, 3.1f) > 0.42f ? 1f : 0.12f;
                    drid = new Vector3(Mathf.PerlinNoise(t * 30f, 0f) - 0.5f, Mathf.PerlinNoise(0f, t * 30f) - 0.5f, Mathf.PerlinNoise(t * 30f, 7f) - 0.5f) * 0.12f;
                }
                flluska.localScale = new Vector3(2.4f, 2.6f, 2.4f) * puls;
                flluska.localPosition = new Vector3(0, 1.0f, 0) + drid;
                if (flluskaMat != null) flluskaMat.SetColor("_BaseColor", new Color(0.4f, 0.8f, 1f, alfa));
            }
        }
        // gjurma e vrapit të shpejtë (dridhet kur po mbaron)
        if (niveliVrap > 0.05f && gjurma == null) KrijoGjurmen();
        if (gjurma != null)
        {
            var em = gjurma.emission;
            float flak = vrap > 0f && vrap < Paralajmerim ? (Mathf.Sin(t * 22f) > 0f ? 1f : 0.25f) : 1f;
            em.rateOverTime = 90f * niveliVrap * flak;
        }
        // aura paralajmëruese rreth këmbëve: Magneti ose Vrapi po mbarojnë
        bool mMbaron = magnet > 0f && magnet < Paralajmerim, vMbaron = vrap > 0f && vrap < Paralajmerim;
        if ((mMbaron || vMbaron) && Luhet)
        {
            if (aura == null) KrijoAuren();
            auraKoha -= dt;
            if (aura != null && auraKoha <= 0f)
            {
                auraKoha = 0.3f;
                int lloji = mMbaron && vMbaron ? (Mathf.RoundToInt(t * 3f) % 2 == 0 ? 0 : 2) : (mMbaron ? 0 : 2);
                var ep = new ParticleSystem.EmitParams { startColor = Fuqia.Ngjyrat[lloji] * 1.8f, applyShapeToPosition = true };
                aura.Emit(ep, 14);
            }
        }
    }

    void KrijoFlluskën()
    {
        var baza = TrackSpawner.Instance != null ? TrackSpawner.Instance.cloud : null;   // URP Unlit transparent
        if (baza == null) return;
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "Mburoja_Flluska";
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(player.transform, false);
        g.transform.localPosition = new Vector3(0, 1.0f, 0);
        flluskaMat = new Material(baza) { name = "Mburoja" };
        flluskaMat.SetTexture("_BaseMap", null);
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = flluskaMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        flluska = g.transform;
    }

    ParticleSystem Grimca(string emri, Vector3 pos)
    {
        var mat = TrackSpawner.Instance != null ? TrackSpawner.Instance.flame : null;   // aditive, shkëlqen
        if (mat == null) return null;
        var g = new GameObject(emri);
        g.transform.SetParent(player.transform, false);
        g.transform.localPosition = pos;
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    void KrijoGjurmen()
    {
        gjurma = Grimca("Vrapi_Gjurma", new Vector3(0, 1.0f, 0));
        if (gjurma == null) return;
        var main = gjurma.main;
        main.startLifetime = 0.35f; main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startColor = new Color(1.6f, 1.5f, 0.4f, 1f);
        main.maxParticles = 200;
        var sh = gjurma.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.7f, 1.7f, 0.3f);
        var sz = gjurma.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        gjurma.Play();
    }

    // unazë shkëndijash që ngrihen rreth këmbëve (vetëm me Emit, kur fuqia po mbaron)
    void KrijoAuren()
    {
        aura = Grimca("Fuqia_Paralajmerim", new Vector3(0, 0.15f, 0));
        if (aura == null) return;
        aura.transform.localRotation = Quaternion.Euler(-90f, 0, 0);   // rrethi i formës shtrihet në tokë
        var main = aura.main;
        main.loop = false;
        main.startLifetime = 0.55f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.gravityModifier = -0.25f;   // ngrihen butë
        main.maxParticles = 120;
        var em = aura.emission; em.enabled = false;
        var sh = aura.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.75f; sh.radiusThickness = 0f;
        var col = aura.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    // ================= kartat në ekran =================
    class Karta
    {
        public int lloji;
        public float hyrja;        // 0 -> 1: rrëshqet brenda
        public float ndricim;      // 1 -> 0: shkëlqim pas marrjes/rimarrjes
        public float fundi = 1f;   // 1 -> 0: zvogëlohet dhe zhduket
        public bool mbaron;
        public float vendi;        // pozicioni në kolonë (ngjitet butë)
        public float unaza;        // sa e mbushur duket unaza (0-1)
        public int tikFundit = 99;
    }
    readonly List<Karta> kartat = new List<Karta>();
    readonly Karta[] sipasLlojit = new Karta[3];

    void KartaMerret(int i)
    {
        var k = sipasLlojit[i];
        if (k != null)
        {
            // rimarrje: e njëjta kartë ndriçon dhe unaza mbushet butë
            k.mbaron = false; k.fundi = 1f; k.ndricim = 1f; k.tikFundit = 99;
            return;
        }
        k = new Karta { lloji = i, ndricim = 1f, vendi = kartat.Count };
        kartat.Add(k);
        sipasLlojit[i] = k;
    }

    void KartatUpdate(float dt)
    {
        for (int n = 0; n < kartat.Count; n++)
        {
            var k = kartat[n];
            float mbetur = Mbetur(k.lloji), cak = mbetur / Gjithsej(k.lloji);
            k.unaza = k.unaza < cak ? Mathf.MoveTowards(k.unaza, cak, dt * 2.2f) : cak;
            k.hyrja = Mathf.MoveTowards(k.hyrja, 1f, dt / 0.35f);
            k.ndricim = Mathf.MoveTowards(k.ndricim, 0f, dt / 0.6f);
            k.vendi = Mathf.Lerp(k.vendi, n, 1f - Mathf.Exp(-12f * dt));
            if (!k.mbaron && mbetur <= 0f) k.mbaron = true;
            // "tik" në çdo sekondë të 3 të fundit
            if (mbetur > 0f && mbetur <= Paralajmerim && Luhet)
            {
                int sek = Mathf.CeilToInt(mbetur);
                if (sek < k.tikFundit) { k.tikFundit = sek; player.OnEvent?.Invoke("tik"); }
            }
            else if (mbetur > Paralajmerim) k.tikFundit = 99;
        }
        for (int n = kartat.Count - 1; n >= 0; n--)
        {
            var k = kartat[n];
            if (!k.mbaron) continue;
            k.fundi -= dt / 0.3f;
            if (k.fundi <= 0f) { kartat.RemoveAt(n); sipasLlojit[k.lloji] = null; }
        }
    }

    // ---------- teksturat (krijohen një herë) ----------
    static Texture2D panelTex, rrethTex, unazeTex, shkelqimTex, segmentTex, pxTex;
    static Texture2D[] ikonat;
    static GUIStyle panelSt, numriSt, emriSt, bigSt;
    static readonly string[] sekondat = new string[32], decimalet = new string[31];
    static readonly string[] Thirrjet = { "MAGNET!", "MBUROJË!", "VRAP I SHPEJTË!" };
    static readonly Color Ari = new Color(1f, 0.82f, 0.42f), Kuqe = new Color(1f, 0.28f, 0.22f);
    const int Segmente = 48;

    static void Teksturat()
    {
        if (panelTex != null) return;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < sekondat.Length; i++) sekondat[i] = i.ToString(ci);
        for (int i = 0; i < decimalet.Length; i++) decimalet[i] = (i / 10f).ToString("0.0", ci);

        // paneli: kënd i rrumbullakët, kafe i errët me buzë ari (9-slice)
        panelTex = Vizato(32, 32, (x, y) =>
        {
            float d = RectRrumbullak(x, y, 32, 32, 9f);
            float mbushja = Mathf.Clamp01(0.5f - d);
            float buza = Mathf.Clamp01(1f - Mathf.Abs(d + 1.5f) / 1.2f);
            Color c = new Color(0.08f, 0.06f, 0.03f, 0.82f * mbushja);
            return Color.Lerp(c, new Color(Ari.r, Ari.g, Ari.b, 0.85f * mbushja), buza);
        });
        rrethTex = Vizato(64, 64, (x, y) => new Color(1, 1, 1, Mathf.Clamp01(31.5f - Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)))));
        shkelqimTex = Vizato(64, 64, (x, y) => { float r = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) / 32f; return new Color(1, 1, 1, Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r)); });
        unazeTex = Vizato(128, 128, (x, y) =>
        {
            float r = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64));
            return new Color(1, 1, 1, Mathf.Clamp01(5.5f - Mathf.Abs(r - 57f)));   // unazë 11 px e trashë (r 51-63)
        });
        segmentTex = Vizato(24, 12, (x, y) => new Color(1, 1, 1, Mathf.Clamp01(0.5f - RectRrumbullak(x, y, 24, 12, 5.5f))));
        pxTex = new Texture2D(1, 1); pxTex.SetPixel(0, 0, Color.white); pxTex.Apply();
        ikonat = new[] { Ikona(BrendaMagnet), Ikona(BrendaMburoje), Ikona(BrendaRrufe) };

        panelSt = new GUIStyle { border = new RectOffset(10, 10, 10, 10) };
        panelSt.normal.background = panelTex;
        numriSt = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
        emriSt = new GUIStyle(numriSt);
        bigSt = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    static Texture2D Vizato(int w, int h, System.Func<float, float, Color> f)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x + 0.5f, y + 0.5f);
        t.SetPixels(px); t.Apply(false, true);
        return t;
    }

    // distanca me shenjë nga drejtkëndëshi me kënde të rrumbullakëta (negative brenda)
    static float RectRrumbullak(float x, float y, float w, float h, float r)
    {
        float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r), qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // ikonë 64x64 e bardhë (ngjyroset me GUI.color), me 4x4 mostra për skaje të lëmuara; y nga lart
    static Texture2D Ikona(System.Func<float, float, float> brenda)
    {
        return Vizato(64, 64, (x, yUp) =>
        {
            float y = 64f - yUp, a = 0f;
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) a += brenda(x - 0.5f + (i + 0.5f) / 4f, y - 0.5f + (j + 0.5f) / 4f);
            return new Color(1, 1, 1, Mathf.Clamp01(a / 16f));
        });
    }

    // magnet në formë U, me maja të ndara
    static float BrendaMagnet(float x, float y)
    {
        bool krahu = y >= 10f && y <= 32f && ((x >= 12f && x <= 24f) || (x >= 40f && x <= 52f));
        float r = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
        bool harku = y > 32f && r >= 8f && r <= 20f;
        bool cara = y > 17f && y < 20f;   // vija që ndan majat
        return (krahu || harku) && !cara ? 1f : 0f;
    }

    // mburojë: krye e sheshtë, anët poshtë dhe majë; me një dritare më të zbehtë brenda
    static float BrendaMburoje(float x, float y)
    {
        float hw(float yy) => yy <= 30f ? 21f : 21f * (1f - Mathf.Pow((yy - 30f) / 26f, 1.6f));
        if (y < 8f || y > 56f || Mathf.Abs(x - 32f) > hw(y)) return 0f;
        bool brenda = y > 15f && y < 48f && Mathf.Abs(x - 32f) < hw(y + 4f) - 7f;
        return brenda ? 0.45f : 1f;
    }

    // rrufe (shumëkëndësh)
    static readonly Vector2[] rrufe = { new Vector2(38, 4), new Vector2(15, 36), new Vector2(29, 36), new Vector2(22, 60), new Vector2(49, 25), new Vector2(34, 25), new Vector2(43, 4) };
    static float BrendaRrufe(float x, float y)
    {
        bool c = false;
        for (int i = 0, j = rrufe.Length - 1; i < rrufe.Length; j = i++)
            if ((rrufe[i].y > y) != (rrufe[j].y > y) && x < (rrufe[j].x - rrufe[i].x) * (y - rrufe[i].y) / (rrufe[j].y - rrufe[i].y) + rrufe[i].x) c = !c;
        return c ? 1f : 0f;
    }

    // ---------- vizatimi i kartave ----------
    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (player == null || GameManager.Instance == null || !GameManager.Instance.HudVisible) return;
        Teksturat();
        float W = Screen.width, H = Screen.height;
        float s = Mathf.Min(H / 720f, W / 1280f) * 1.1f;
        float sk = H > W ? s * 1.3f : s;   // telefon vertikal: kartat pak më të mëdha që të lexohen
        var oldM = GUI.matrix; var oldC = GUI.color;

        float kw = 172 * sk, kh = 64 * sk, gap = 8 * sk;
        float x0 = W - 16 * s - kw, y0 = 16 * s + 50 * s + 10 * s;   // poshtë kutisë së monedhave, e rreshtuar djathtas
        foreach (var k in kartat) Karte(k, x0, y0 + k.vendi * (kh + gap), kw, kh, sk);

        // njoftimi në mes të ekranit kur merret fuqia
        float kn = (Time.time - njoftimKoha) / 1.3f;
        if (kn >= 0f && kn < 1f)
        {
            float a = Mathf.Clamp01((1f - kn) * 3f);
            float pop = 1f + Mathf.Max(0f, 0.25f - kn) * 1.6f;
            bigSt.fontSize = Mathf.RoundToInt(44 * s * pop);
            bigSt.normal.textColor = Color.white;
            Color c = Fuqia.Ngjyrat[(int)njoftimi];
            var r = new Rect(0, H * 0.26f, W, 70 * s * pop);
            GUI.color = new Color(0, 0, 0, a * 0.7f);
            GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), Thirrjet[(int)njoftimi], bigSt);
            GUI.color = new Color(Mathf.Lerp(c.r, 1f, 0.35f), Mathf.Lerp(c.g, 1f, 0.35f), Mathf.Lerp(c.b, 1f, 0.35f), a);
            GUI.Label(r, Thirrjet[(int)njoftimi], bigSt);
        }
        GUI.matrix = oldM; GUI.color = oldC;
    }

    void Karte(Karta k, float x, float y, float w, float h, float s)
    {
        float mbetur = Mbetur(k.lloji);
        bool alarm = mbetur > 0f && mbetur <= Paralajmerim && !k.mbaron;
        float t = Time.unscaledTime;
        Color ngj = Fuqia.Ngjyrat[k.lloji];

        // hyrja nga e djathta + "pop"; mbarimi: zvogëlohet dhe zbehet; alarmi: pulson
        float e = 1f - Mathf.Pow(1f - k.hyrja, 3f);
        x += (1f - e) * (w + 40f * s);
        float shkalla = 1f + 0.12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((k.hyrja - 0.55f) / 0.45f));
        float pulsi = alarm ? 0.5f + 0.5f * Mathf.Sin(t * 10f) : 0f;
        shkalla *= 1f + 0.035f * pulsi;
        float alfa = Mathf.Clamp01(k.fundi) * Mathf.Clamp01(e * 1.5f);
        shkalla *= Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(Mathf.Clamp01(k.fundi)));

        var baza = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(shkalla, shkalla), new Vector2(x + w * 0.5f, y + h * 0.5f));

        // paneli (+ ndriçim ari kur merret/rimerret, + skuqje kur po mbaron)
        var rect = new Rect(x, y, w, h);
        GUI.color = new Color(1f, 1f, 1f, alfa);
        panelSt.Draw(rect, false, false, false, false);
        float drita = Mathf.Max(k.ndricim * 0.8f, pulsi * 0.35f);
        if (drita > 0.01f)
        {
            Color dc = alarm ? Kuqe : Ari;
            GUI.color = new Color(dc.r, dc.g, dc.b, alfa * drita);
            panelSt.Draw(rect, false, false, false, false);
        }

        // ikona brenda unazës
        float cx = x + h * 0.5f + 4f * s, cy = y + h * 0.5f, R = h * 0.5f - 5f * s;
        if (k.ndricim > 0.01f)
        {
            float g = 1.6f * R * (1f + 0.6f * (1f - k.ndricim));
            GUI.color = new Color(Mathf.Lerp(ngj.r, 1f, 0.5f), Mathf.Lerp(ngj.g, 1f, 0.5f), Mathf.Lerp(ngj.b, 1f, 0.5f), alfa * k.ndricim);
            GUI.DrawTexture(new Rect(cx - g, cy - g, g * 2f, g * 2f), shkelqimTex);
        }
        GUI.color = new Color(0f, 0f, 0f, alfa * 0.55f);
        GUI.DrawTexture(new Rect(cx - R, cy - R, R * 2f, R * 2f), rrethTex);
        GUI.color = new Color(1f, 1f, 1f, alfa * 0.14f);   // gjurma e unazës (e plotë, e zbehtë)
        GUI.DrawTexture(new Rect(cx - R, cy - R, R * 2f, R * 2f), unazeTex);

        // unaza që zbrazet: segmente të rrotulluara, nga lart në drejtim të akrepave
        Color uc = alarm ? Color.Lerp(Kuqe, Color.white, pulsi * 0.3f) : ngj;
        GUI.color = new Color(uc.r, uc.g, uc.b, alfa);
        float mbushur = Mathf.Clamp01(k.unaza) * Segmente;
        float rr = R * (57f / 64f), trash = R * (11f / 64f) * 1.05f, gjat = 2f * Mathf.PI * rr / Segmente * 1.25f;
        var mSc = GUI.matrix;
        for (int i = 0; i < Segmente && i < mbushur; i++)
        {
            float pjesa = Mathf.Min(1f, mbushur - i);
            float kendi = -90f + (i + 0.5f * pjesa) * 360f / Segmente;
            float rad = kendi * Mathf.Deg2Rad;
            var pos = new Vector2(cx + Mathf.Cos(rad) * rr, cy + Mathf.Sin(rad) * rr);
            GUI.matrix = mSc;
            GUIUtility.RotateAroundPivot(kendi + 90f, pos);
            float l = gjat * pjesa;
            GUI.DrawTexture(new Rect(pos.x - l * 0.5f, pos.y - trash * 0.5f, l, trash), segmentTex);
        }
        GUI.matrix = mSc;

        // ikona (me hije)
        float ik = R * 1.15f;
        var ir = new Rect(cx - ik * 0.5f, cy - ik * 0.5f, ik, ik);
        GUI.color = new Color(0f, 0f, 0f, alfa * 0.6f);
        GUI.DrawTexture(new Rect(ir.x + 1.5f * s, ir.y + 1.5f * s, ir.width, ir.height), ikonat[k.lloji]);
        GUI.color = new Color(Mathf.Lerp(ngj.r, 1f, 0.2f + k.ndricim * 0.6f), Mathf.Lerp(ngj.g, 1f, 0.2f + k.ndricim * 0.6f), Mathf.Lerp(ngj.b, 1f, 0.2f + k.ndricim * 0.6f), alfa);
        GUI.DrawTexture(ir, ikonat[k.lloji]);

        // emri (ari, i vogël) dhe sekondat (të mëdha)
        float tx = x + h + 8f * s;
        emriSt.fontSize = Mathf.RoundToInt(12 * s);
        emriSt.normal.textColor = Color.white;
        GUI.color = new Color(Ari.r, Ari.g, Ari.b, alfa);
        GUI.Label(new Rect(tx, y + 5f * s, w - h - 10f * s, 18f * s), Fuqia.Emrat[k.lloji], emriSt);
        string numri = mbetur < Paralajmerim
            ? decimalet[Mathf.Clamp(Mathf.FloorToInt(mbetur * 10f), 0, decimalet.Length - 1)]
            : sekondat[Mathf.Clamp(Mathf.CeilToInt(mbetur), 0, sekondat.Length - 1)];
        numriSt.fontSize = Mathf.RoundToInt(30 * s);
        numriSt.normal.textColor = Color.white;
        Color nc = alarm ? Color.Lerp(Color.white, Kuqe, 0.4f + 0.6f * pulsi) : Color.white;
        GUI.color = new Color(0f, 0f, 0f, alfa * 0.6f);
        GUI.Label(new Rect(tx + 2f * s, y + 22f * s + 2f * s, w - h - 10f * s, 36f * s), numri, numriSt);
        GUI.color = new Color(nc.r, nc.g, nc.b, alfa);
        GUI.Label(new Rect(tx, y + 22f * s, w - h - 10f * s, 36f * s), numri, numriSt);

        GUI.matrix = baza;
    }

    void OnDestroy() { if (flluskaMat != null) Destroy(flluskaMat); }
}
