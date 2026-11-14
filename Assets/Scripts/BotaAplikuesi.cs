using System.Collections.Generic;
using UnityEngine;

// Zbaton temën e botës kur nis skena (vendoset nga "Tempulli > Ndërto botët" te objekti "Loja" i çdo bote):
//  - ngjyros kopje të materialeve të shtegut, natyrës, reve dhe rojtarit (materialet origjinale nuk preken)
//  - uji bëhet akull (pa spërkatje) ose lavë që shkëlqen
//  - grimca në ajër rreth Artës: xixëllonja, borë ose shkëndija që ngrihen
//  - pishtarë më të shpeshtë / më të fortë
// Qielli, dielli, mjegulla dhe Volume janë "pjekur" në skenë nga Editor-i (TempulliSetup.BuildScene).
[DefaultExecutionOrder(-200)]   // para TrackSpawner-it dhe Rojtarit, që ata të marrin materialet e reja
public class BotaAplikuesi : MonoBehaviour
{
    public BotaTema tema;

    TrackSpawner spawner;
    PlayerController player;
    Transform grimcatT;
    readonly Dictionary<Material, Material> kopjet = new Dictionary<Material, Material>();
    readonly List<Material> teKrijuara = new List<Material>();

    void Awake()
    {
        if (tema == null) return;
        spawner = FindAnyObjectByType<TrackSpawner>();
        player = FindAnyObjectByType<PlayerController>();
        if (spawner != null) Materialet();
        Uji();
        Grimcat();
        // vagoni (bota e varrit): Arta udhëton mbi shina
        if (tema.vagon && player != null)
        {
            player.vagon = true;
            if (player.GetComponent<Vagoni>() == null) player.gameObject.AddComponent<Vagoni>().player = player;
        }
    }

    // ---------- materialet ----------
    void Materialet()
    {
        var s = spawner;
        Color g = tema.guri, b = tema.bimet, r = tema.ret, dru = Color.Lerp(Color.white, tema.bimet, 0.5f);
        float z = tema.guriZbardhje;
        Color e = tema.guriShkelqim;
        s.deck = Kopje(s.deck, g, z, e); s.wall = Kopje(s.wall, g, z, e); s.carved = Kopje(s.carved, g, z, e);
        s.rock = Kopje(s.rock, g, z, e); s.stone = Kopje(s.stone, g, z, e); s.stoneDark = Kopje(s.stoneDark, g, z, e);
        s.roof = Kopje(s.roof, g, z * 0.5f, e); s.riverbed = Kopje(s.riverbed, g, z, e);
        if (s.deckVariants != null)
        {
            var v = new Material[s.deckVariants.Length];
            for (int i = 0; i < v.Length; i++) v[i] = Kopje(s.deckVariants[i], g, z, e);
            s.deckVariants = v;
        }
        s.leaves = Kopje(s.leaves, b); s.moss = Kopje(s.moss, b); s.grass = Kopje(s.grass, b);
        s.bark = Kopje(s.bark, dru); s.barkTex = Kopje(s.barkTex, dru); s.wood = Kopje(s.wood, dru);
        s.cloud = Kopje(s.cloud, r); s.cloudPuff = Kopje(s.cloudPuff, r); s.cloudBase = Kopje(s.cloudBase, r);

        // natyra nga Blender (shkëmbinj, pemë, shkurre): materialet e saj ndërrohen kur vendosen (TrackSpawner.Place)
        if (s.nature != null)
            foreach (var rend in s.nature.GetComponentsInChildren<Renderer>(true))
                foreach (var m in rend.sharedMaterials)
                {
                    if (m == null || kopjet.ContainsKey(m)) continue;
                    string n = m.name;
                    if (n.Contains("Gjeth") || n.Contains("Myshk") || n.Contains("Bar")) Kopje(m, b);
                    else if (n.Contains("Levorj") || n.Contains("Lëvorj") || n.Contains("Dru")) Kopje(m, dru);
                    else Kopje(m, g, z, e);
                }
        foreach (var kv in kopjet) s.remap[kv.Key] = kv.Value;

        // rojtari prej guri merr të njëjtin gur
        var ch = FindAnyObjectByType<Chaser>();
        if (ch != null) { ch.stone = Kopje(ch.stone, g, z * 0.6f, e); ch.moss = Kopje(ch.moss, b); }

        s.pishtarCdo = Mathf.Max(1, tema.pishtarCdo);
        s.pishtarDrita = tema.pishtarDrita;

        // theksi i pengesave: material që shkëlqen (HDR + bloom), vetëm kur tema e kërkon
        if (tema.pengesaTheks.maxColorComponent > 0f)
        {
            var baza = s.glow != null ? s.glow : s.stone;
            if (baza != null)
            {
                var m = new Material(baza) { name = "Theksi_" + tema.name };
                Color c = tema.pengesaTheks;
                float max = Mathf.Max(1f, c.maxColorComponent);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(c.r / max, c.g / max, c.b / max, 1f));
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c);
                teKrijuara.Add(m);
                s.theks = m;
            }
        }
        s.teposhte = tema.teposhteAkulli; s.brenda = tema.brenda; s.vagon = tema.vagon;
    }

    // kopje e materialit me ngjyrë të re; alfa ruhet (te guri me myshk alfa = sasia e myshkut)
    Material Kopje(Material m, Color tint, float zbardhje = 0f, Color shkelqim = default)
    {
        if (m == null) return null;
        if (kopjet.TryGetValue(m, out var ekziston)) return ekziston;
        if (tint == Color.white && zbardhje <= 0f && shkelqim.maxColorComponent <= 0f) return m;
        var c = new Material(m) { name = m.name + "_" + tema.name };
        foreach (var prop in new[] { "_BaseColor", "_Color" })
        {
            if (!c.HasProperty(prop)) continue;
            Color a = c.GetColor(prop);
            Color nw = Color.Lerp(a * tint, tint, zbardhje);
            nw.a = a.a;
            c.SetColor(prop, nw);
        }
        if (c.HasProperty("_EmissionColor"))
        {
            Color em = c.IsKeywordEnabled("_EMISSION") ? c.GetColor("_EmissionColor") * tint : Color.black;
            em += shkelqim;
            if (em.maxColorComponent > 0f) { c.EnableKeyword("_EMISSION"); c.SetColor("_EmissionColor", em); }
        }
        kopjet[m] = c;
        teKrijuara.Add(c);
        return c;
    }

    // ---------- uji: akull ose lavë ----------
    void Uji()
    {
        if (spawner == null || tema.uji == LlojiUjit.Uje) return;
        var baza = spawner.stone != null ? spawner.stone : spawner.deck;
        if (baza == null) return;
        var tex = spawner.water != null && spawner.water.HasProperty("_BaseMap") ? spawner.water.GetTexture("_BaseMap") : null;
        var m = new Material(baza) { name = tema.uji == LlojiUjit.Akull ? "Akulli" : "Lava" };
        // akulli: i lëmuar, pa teksturë (tekstura e ujit lëviz dhe akulli do të dukej sikur rrjedh); lava: me valët e ujit
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tema.uji == LlojiUjit.Lave ? tex : null);
        if (tema.uji == LlojiUjit.Akull)
        {
            m.SetColor("_BaseColor", tema.ujiNgjyra);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.95f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", tema.ujiNgjyra * 0.15f);
            // akulli: pa spërkatje uji
            if (player != null) player.splash = null;
        }
        else
        {
            m.SetColor("_BaseColor", new Color(0.25f, 0.05f, 0.02f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            m.EnableKeyword("_EMISSION");
            if (tex != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", tema.ujiNgjyra * 3.2f);   // HDR: bloom-i e bën të shkëlqejë
            // "spërkatjet" bëhen shkëndija portokalli
            if (player != null && player.splash != null)
            {
                var main = player.splash.main;
                main.startColor = new Color(1f, 0.55f, 0.15f, 1f);
                if (spawner.flame != null) player.splash.GetComponent<ParticleSystemRenderer>().sharedMaterial = spawner.flame;
            }
        }
        teKrijuara.Add(m);
        spawner.water = m;
    }

    // ---------- grimcat në ajër (ndjekin Artën) ----------
    void Grimcat()
    {
        if (tema.grimcat == LlojiGrimcave.Asnje || player == null) return;
        Material mat = null;
        bool aditive = tema.grimcat != LlojiGrimcave.Bore;
        if (aditive && spawner != null) mat = spawner.flame;
        if (mat == null && player.sparkle != null) mat = player.sparkle.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        if (mat == null) return;

        grimcatT = new GameObject("Grimcat_" + tema.grimcat).transform;
        var ps = grimcatT.gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = tema.grimcatNgjyra;
        var em = ps.emission; em.rateOverTime = tema.grimcatSasia;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box;
        var noise = ps.noise; noise.enabled = true;
        var vel = ps.velocityOverLifetime;
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;

        switch (tema.grimcat)
        {
            case LlojiGrimcave.Xixellonja:   // pika të verdha që notojnë dhe ndizen e fiken
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
                main.maxParticles = 500;
                sh.scale = new Vector3(26f, 6f, 46f); sh.position = new Vector3(0, 2.5f, 16f);
                noise.strength = 0.9f; noise.frequency = 0.4f; noise.scrollSpeed = 0.3f;
                gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                           new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.2f, 0.35f),
                                   new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0.3f, 0.75f), new GradientAlphaKey(0f, 1f) });
                col.color = gr;
                break;
            case LlojiGrimcave.Bore:         // borë që bie
                main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
                main.gravityModifier = 0.06f;
                main.maxParticles = 3000;
                sh.scale = new Vector3(34f, 1f, 50f); sh.position = new Vector3(0, 13f, 18f);
                noise.strength = 0.6f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
                break;
            case LlojiGrimcave.Shkendija:    // shkëndija që ngrihen nga poshtë urës
                main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
                main.maxParticles = 800;
                sh.scale = new Vector3(34f, 2f, 50f); sh.position = new Vector3(0, -4f, 16f);
                vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                vel.y = new ParticleSystem.MinMaxCurve(1.5f, 3.8f);
                vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                noise.strength = 0.5f; noise.frequency = 0.6f; noise.scrollSpeed = 0.5f;
                break;
        }
        var rend = grimcatT.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        Ndiq();
        ps.Play();
    }

    void LateUpdate() { if (grimcatT != null) Ndiq(); }

    // kutia e grimcave qëndron rreth Artës dhe kthehet me shtegun (grimcat vetë mbeten në botë)
    void Ndiq()
    {
        if (player == null) return;
        grimcatT.SetPositionAndRotation(player.transform.position, Quaternion.LookRotation(player.Forward));
    }

    void OnDestroy()
    {
        foreach (var m in teKrijuara) if (m != null) Destroy(m);
    }
}
