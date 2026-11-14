using System.Collections.Generic;
using UnityEngine;

// Tempulli i Humbur - shtegu pa fund mbi re: urë guri e artë me harqe poshtë, bordura të gdhendura,
// porta me çati tjegullash, kulla anash, shkëmbinj-kulla në largësi dhe det resh poshtë.
// Pengesat: "Rrenja" (kërce), "Porta" (rrëshqit, edhe trungu i rënë), "Blloku" (anohu), gropa (kërce), kthesa.
// Pjesë të veçanta: gjysmë-shtegu (anohu në anën që ka dysheme), lumi (vrapo nëpër ujë), litari (kapet vetë).
public class TrackSpawner : MonoBehaviour
{
    public static TrackSpawner Instance { get; private set; }

    public class Junction
    {
        public Vector3 center;
        public Vector3 newDir;
        public int turn;      // -1 majtas, 1 djathtas
        public bool passed;
    }

    public class Zipline
    {
        public Vector3 a, b;     // pikat e litarit (bota)
        public float sag = 0.8f;
        public bool used;
    }

    public PlayerController player;
    // materialet e vjetra (mbeten për pajtueshmëri)
    public Material stone, stoneDark, moss, gold, wood, leaves, bark, glow;
    // materialet e reja me tekstura
    public Material deck, wall, carved, roof, rock, cloud, cloudPuff, cloudBase;
    // natyra nga Blender (tempulli_natyra.fbx): shkëmbinj, pemë, shkurre, fier
    public GameObject nature;
    // lumi, trungu, bari
    public Material water, riverbed, barkTex, grass;
    public Material flame;              // flaka e pishtarëve (grimca aditive)
    public float segmentLength = 24f;
    [Tooltip("Gjerësia e shtegut (m). Gjithçka tjetër (muret, lumi, kthesat, pengesat, korsitë) llogaritet prej saj.")]
    public float pathWidth = 6.6f;
    public float laneWidth = 2.0f;
    [Tooltip("Meshe të krijuara me ProBuilder (Tempulli > Ndërto skenën): harku i portës dhe kolona e rrumbullakët.")]
    public Mesh archMesh, columnMesh;
    public Material[] deckVariants;     // nuanca të ndryshme guri për çdo segment (nuk duken të gjitha njësoj)
    public float viewDistance = 200f;
    [Header("Botët dhe fuqitë")]
    [Tooltip("Pas sa metrash (në këtë botë) del portali për në botën tjetër.")]
    public float portalCdo = 500f;
    [Tooltip("Pishtar në çdo shtyllë të N-të të bordurës (BotaAplikuesi e ndryshon sipas temës).")]
    public int pishtarCdo = 3;
    public float pishtarDrita = 1f;
    [System.NonSerialized] public Material theks;   // theksi i pengesave (BotaAplikuesi, botët e errëta); null = pa theks
    // nga tema e botës (BotaAplikuesi): teposhte akulli, varr (brenda: mure e tavan) dhe vagon mbi shina
    [System.NonSerialized] public bool teposhte, brenda, vagon;
    // materialet e natyrës (Blender) që tema e botës i zëvendëson me kopje të ngjyrosura (BotaAplikuesi)
    [System.NonSerialized] public readonly Dictionary<Material, Material> remap = new Dictionary<Material, Material>();
    float nextPortal;
    int sincePower;
    // vështirësia vazhdon nga botët e mëparshme (distanca totale e vrapit)
    float Prog => trackDist + Udhetimi.DistancaPara;

    class Piece
    {
        public GameObject go; public float endDist; public List<Mesh> merged;
        public int run;                                                   // vija e drejtë (ndryshon pas çdo kthese)
        public readonly List<Korridor> korr = new List<Korridor>();
        public readonly List<Deko> deko = new List<Deko>();
        public readonly List<Renderer> mergedRends = new List<Renderer>();   // ato që Merge i fiku (për rindërtim)
    }

    // ---------------- korridoret e pamjes ----------------
    // Çdo pjesë e udhës regjistron një "korridor": kuti e orientuar me gjerësinë e udhës + 3 m anash
    // dhe lartësi 0-14 m mbi dysheme. Skenari dekorativ (shkëmbinj, male, pemë, rrënoja, kulla) nuk lejohet
    // brenda korridorit të një vije tjetër: as i ri në korridoret e vjetra, as i vjetër në korridorin e ri.
    // Kontrolli bëhet vetëm kur krijohet pjesa (jo në çdo frame), për ~12 pjesët e fundit.
    public struct Korridor { public Vector3 c; public Quaternion rot; public Vector3 half; }
    public class Deko { public Transform t; public Bounds b; }
    const float KorridorAnash = 3f, KorridorLart = 14f, KorridorPoshte = -0.5f;
    const int PjesetEKontrolluara = 12;
    readonly List<Transform> curDeko = new List<Transform>();
    int run;
    public int DekoTeFshehura { get; private set; }
    public int DekoTeShtyra { get; private set; }

    // grup dekorativ (gjithçka brenda tij lëviz/fshihet bashkë, p.sh. shkëmbi me pemën sipër)
    Transform Group(Transform t, string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(t, false);
        curDeko.Add(g);
        return g;
    }

    Korridor Kutia(Transform t, float x0, float x1, float y0, float y1, float z0, float z1)
    {
        var lc = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
        return new Korridor { c = t.TransformPoint(lc), rot = t.rotation, half = new Vector3((x1 - x0) * 0.5f, (y1 - y0) * 0.5f, (z1 - z0) * 0.5f) };
    }

    Korridor KutiaUdhes(Transform t, float z0, float z1, float y0 = KorridorPoshte)
    {
        float w = H + KorridorAnash;
        return Kutia(t, -w, w, y0, KorridorLart, z0, z1);
    }

    static bool Prek(in Korridor k, Bounds b)
    {
        Quaternion inv = Quaternion.Inverse(k.rot);
        Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), mx = -mn;
        for (int i = 0; i < 8; i++)
        {
            var p = inv * (new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z) - k.c);
            mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
        }
        return mx.x > -k.half.x && mn.x < k.half.x && mx.y > -k.half.y && mn.y < k.half.y && mx.z > -k.half.z && mn.z < k.half.z;
    }

    static bool KaBounds(Transform t, out Bounds b)
    {
        b = default; bool any = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    // a prek kutia ndonjë korridor të një vije tjetër (pjesët e fundit që ekzistojnë ende)?
    bool PrekVijeTjeter(Bounds b, int vija, List<Korridor> shtese)
    {
        if (shtese != null) foreach (var k in shtese) if (Prek(k, b)) return true;
        for (int i = pieces.Count - 1, n = 0; i >= 0 && n < PjesetEKontrolluara; i--, n++)
        {
            if (pieces[i].run == vija) continue;
            foreach (var k in pieces[i].korr) if (Prek(k, b)) return true;
        }
        return false;
    }

    // mbyll pjesën e re: kontrollon skenarin (të riun dhe të vjetrin) me korridoret, pastaj bashkon mesh-et
    void AddPiece(Transform t, float endDist, params Korridor[] korridoret)
    {
        var p = new Piece { go = t.gameObject, endDist = endDist, run = run };
        p.korr.AddRange(korridoret);

        // 1. skenari i ri kundrejt udhës që ekziston (vijat e tjera)
        foreach (var g in curDeko)
        {
            if (g == null || !KaBounds(g, out var b)) continue;
            if (PrekVijeTjeter(b, run, null))
            {
                // shtyje larg vijës së vet, jashtë; nëse s'del dot nga korridori, fshihe
                Vector3 side = t.right * Mathf.Sign(Vector3.Dot(b.center - t.position, t.right) + 0.0001f);
                bool ok = false;
                for (int k = 1; k <= 6 && !ok; k++)
                {
                    var bb = b; bb.center += side * (3f * k);
                    if (!PrekVijeTjeter(bb, run, null)) { g.position += side * (3f * k); b = bb; ok = true; }
                }
                if (ok) DekoTeShtyra++;
                else { g.gameObject.SetActive(false); DekoTeFshehura++; continue; }
            }
            p.deko.Add(new Deko { t = g, b = b });
        }
        curDeko.Clear();

        // 2. skenari i vjetër (vija të tjera) që hyn në korridorin e ri: fshihet, dhe ajo pjesë rindërtohet
        for (int i = pieces.Count - 1, n = 0; i >= 0 && n < PjesetEKontrolluara; i--, n++)
        {
            var old = pieces[i];
            if (old.run == run) continue;
            bool ndryshoi = false;
            for (int d = old.deko.Count - 1; d >= 0; d--)
            {
                var dk = old.deko[d];
                if (dk.t == null) { old.deko.RemoveAt(d); continue; }
                foreach (var k in p.korr)
                    if (Prek(k, dk.b))
                    {
                        dk.t.gameObject.SetActive(false);
                        old.deko.RemoveAt(d);
                        DekoTeFshehura++;
                        ndryshoi = true;
                        break;
                    }
            }
            if (ndryshoi) Remerge(old);
        }

        p.merged = Merge(t, p.mergedRends);
        pieces.Add(p);
    }

    // rindërton mesh-et e bashkuara të një pjese (pas fshehjes së një dekori)
    void Remerge(Piece p)
    {
        var root = p.go.transform;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var ch = root.GetChild(i);
            if (ch.name.StartsWith("Bashkuar_")) { ch.gameObject.SetActive(false); Destroy(ch.gameObject); }
        }
        if (p.merged != null) foreach (var m in p.merged) Destroy(m);
        foreach (var r in p.mergedRends) if (r != null) r.enabled = true;
        p.mergedRends.Clear();
        p.merged = Merge(root, p.mergedRends);
    }

    // për kontrollin automatik të pamjes (PamjaKontrolli): dekori aktiv i pjesëve ekzistuese
    public void DekoAktive(List<Deko> list)
    {
        list.Clear();
        foreach (var p in pieces)
            foreach (var d in p.deko)
                if (d.t != null && d.t.gameObject.activeInHierarchy) list.Add(d);
    }
    readonly List<Piece> pieces = new List<Piece>();
    readonly List<Junction> junctions = new List<Junction>();
    readonly List<Zipline> ziplines = new List<Zipline>();
    int sinceSpecial;

    Vector3 cursor = new Vector3(0, 0, -20f);
    Vector3 dir = Vector3.forward;
    float trackDist = -20f;
    int count;
    int untilTurn = 7;
    bool afterTurn;
    // drejtimi i kthesës së ardhshme vendoset që më parë: segmenti para kthesës duhet ta dijë
    // se nga do të kalojë udha e re, që të mos vendosë shkëmbinj/male mbi të
    int nextTurn = 1;
    int keepOutSide;                    // 0 = pa kufizim; -1/1 = ana e kthesës në segmentin para saj
    float keepOutZ = float.MaxValue;    // asgjë e madhe në atë anë nuk kalon përtej kësaj z (lokale)
    readonly List<Transform> rocks = new List<Transform>(), trees = new List<Transform>();
    readonly List<Coin> coinBuffer = new List<Coin>();
    Transform bush, fern;
    Transform clouds;
    Material[] cloudLayers;

    float H => pathWidth * 0.5f;        // gjysma e gjerësisë
    float Wd => pathWidth;
    Material deckCur;                   // guri i dyshemesë për segmentin aktual
    Transform mountains;

    void Awake() { Instance = this; }

    void Start()
    {
        var preview = GameObject.Find("Shtegu_Paraprak");
        if (preview != null) Destroy(preview);
        if (deck == null) deck = stone;
        if (wall == null) wall = stoneDark;
        if (carved == null) carved = stoneDark;
        if (roof == null) roof = wood;
        if (rock == null) rock = stoneDark;
        if (cloudPuff == null) cloudPuff = stone;
        if (!brenda) BuildClouds();   // varri: pa re dhe pa male
        // korsitë dhe kufiri anësor i lojtarit ndjekin gjerësinë e shtegut
        laneWidth = Mathf.Min(laneWidth, pathWidth * 0.3f);
        if (vagon) laneWidth = 0.01f;   // vagoni: monedhat dhe fuqitë në mes të shinave
        if (player != null) { player.laneWidth = laneWidth; player.maxLateral = vagon ? 0.05f : H - 0.6f; }
        deckCur = deck;
        if (nature != null)
            foreach (Transform c in nature.transform)
            {
                if (c.name.StartsWith("Shkembi")) rocks.Add(c);
                else if (c.name.EndsWith("_Trungu")) trees.Add(c);
                else if (c.name == "Shkurre") bush = c;
                else if (c.name == "Fieri") fern = c;
            }
        nextTurn = Random.value < 0.5f ? -1 : 1;
        nextPortal = portalCdo;
        if (!brenda) BuildMountains();
        while (trackDist < viewDistance) SpawnNext();
    }

    void Update()
    {
        if (player == null) return;
        // një pjesë e re dhe një e vjetër për frame: pa ngrirje kur krijohen shumë objekte njëherësh
        if (trackDist < player.Distance + viewDistance) SpawnNext();
        if (pieces.Count > 0 && pieces[0].endDist < player.Distance - 30f)
        {
            using var _prof = mRecycle.Auto();
            pieces[0].go.GetComponentsInChildren(true, coinBuffer);   // lista e ripërdorur: pa array të ri
            foreach (var c in coinBuffer) ReturnCoin(c);
            if (pieces[0].merged != null) foreach (var m in pieces[0].merged) Destroy(m);
            Destroy(pieces[0].go);
            pieces.RemoveAt(0);
        }
        // deti i reve ndjek lojtarin dhe lëviz ngadalë
        if (clouds != null)
        {
            var p = player.transform.position;
            float cy = Mathf.Lerp(clouds.position.y, player.GroundY, 0.6f * Time.deltaTime);
            clouds.position = new Vector3(p.x, cy, p.z);
            if (mountains != null) mountains.position = clouds.position;   // malet e largëta lëvizin me Artën (si horizont)
            if (cloudLayers != null)
                for (int i = 0; i < cloudLayers.Length; i++)
                {
                    var off = new Vector2(p.x, p.z) / (i == 0 ? 160f : 240f) + new Vector2(Time.time * 0.004f * (i + 1), Time.time * 0.002f);
                    cloudLayers[i].SetTextureOffset("_BaseMap", off);
                }
            if (water != null)
            {
                water.SetTextureOffset("_BaseMap", new Vector2(Time.time * 0.03f, -Time.time * 0.25f));
            }
        }
    }

    public Junction NextJunction()
    {
        foreach (var j in junctions) if (!j.passed) return j;
        return null;
    }

    public Zipline NextZipline()
    {
        foreach (var z in ziplines) if (!z.used) return z;
        return null;
    }

    public void Passed(Junction j)
    {
        j.passed = true;
        junctions.RemoveAll(x => x.passed);
    }

    static readonly Unity.Profiling.ProfilerMarker mSpawn = new Unity.Profiling.ProfilerMarker("Tempulli.SpawnNext"),
        mMerge = new Unity.Profiling.ProfilerMarker("Tempulli.Merge"), mRecycle = new Unity.Profiling.ProfilerMarker("Tempulli.RecyclePiece");

    void SpawnNext()
    {
        using var _prof = mSpawn.Auto();   // duket te Profiler-i si "Tempulli.SpawnNext"
        untilTurn--;
        sinceSpecial++;
        sincePower++;
        if (untilTurn <= 0 && count > 4) { SpawnJunction(); untilTurn = Random.Range(5, 9); }
        // portali: vetëm në pjesë të drejtë, jo pas kthese, jo para kthese dhe jo menjëherë pas lumit/litarit/gjysmë-shtegut
        else if (!PamjaKontrolli.TestAktiv && trackDist >= nextPortal && count > 5 && !afterTurn && untilTurn > 2 && sinceSpecial > 1)
        {
            int bota = Udhetimi.ZgjidhTjetren();
            nextPortal = trackDist + portalCdo;
            if (bota >= 0) { SpawnPortal(bota); sinceSpecial = 0; }
            else SpawnStraight(untilTurn == 1);
        }
        else if (!vagon && count > 7 && sinceSpecial > 3 && !afterTurn && untilTurn > 2 && Random.value < (teposhte ? 0.45f : 0.35f))
        {
            float r = Random.value;
            if (teposhte && r < 0.5f) SpawnTeposhte();
            else if (r < 0.25f && Prog > 350f) SpawnZipline();
            else if (r < 0.6f) SpawnRiver();
            else SpawnHalf();
            sinceSpecial = 0;
        }
        else SpawnStraight(untilTurn == 1);
        count++;
    }

    Transform NewRoot(string name)
    {
        deckCur = (deckVariants != null && deckVariants.Length > 0) ? deckVariants[Random.Range(0, deckVariants.Length)] : deck;
        var root = new GameObject(name).transform;
        root.SetPositionAndRotation(cursor, Quaternion.LookRotation(dir));
        curDeko.Clear();
        return root;
    }

    // ---------------- pjesët e shtegut ----------------
    static readonly Unity.Profiling.ProfilerMarker mSpawnStraight = new Unity.Profiling.ProfilerMarker("Tempulli.SpawnStraight");
    void SpawnStraight(bool beforeTurn)
    {
        using var _prof = mSpawnStraight.Auto();
        var t = NewRoot("Segmenti_" + count);
        float L = segmentLength;
        bool safe = count < 4;

        var rows = new List<(float z, int type)>();
        // pengesat: zakonisht një për segment (24 m), disa segmente bosh; pas 700 m ndonjëherë dy
        if (!safe && !afterTurn)
        {
            float empty = Prog < 300f ? 0.35f : (Prog < 900f ? 0.22f : 0.12f);
            if (Random.value > empty)
            {
                float z = Random.Range(L * 0.3f, L * 0.55f);
                rows.Add((z, Lloji(5)));
                if (!beforeTurn && Prog > 700f && Random.value < 0.3f)
                    rows.Add((z + Random.Range(10f, 12f), Lloji(3)));
            }
        }
        afterTurn = false;
        float gapZ = -1;
        foreach (var r in rows) if (r.type == 3) gapZ = r.z;
        if (gapZ > 0)
        {
            Deck(t, 0, gapZ - 1.6f);
            Deck(t, gapZ + 1.6f, L);
            // buzët e gropës shkëlqejnë (kërce mbi të)
            Theks(t, new Vector3(0, 0.015f, gapZ - 1.66f), new Vector3(Wd - 0.2f, 0.03f, 0.12f));
            Theks(t, new Vector3(0, 0.015f, gapZ + 1.66f), new Vector3(Wd - 0.2f, 0.03f, 0.12f));
            Coins(t, 0, gapZ - 3f, 4, 1.4f, 1.5f, true);
        }
        else Deck(t, 0, L);

        foreach (var r in rows)
        {
            float z = r.z;
            switch (r.type)
            {
                case 0: Root(t, z); Coins(t, Random.Range(-1, 2) * laneWidth, z - 4.5f, 4, 2.0f, 1.5f, false); break;
                case 1: Gate(t, z); Coins(t, Random.Range(-1, 2) * laneWidth, z - 4f, 3, 0.7f, 1.5f, false); break;
                case 4: FallenTree(t, z); Coins(t, Random.Range(-1, 2) * laneWidth, z - 4f, 3, 0.7f, 1.5f, false); break;
                case 5: { int ana = Random.value < 0.5f ? -1 : 1; Anash(t, z, ana); Coins(t, 0, z - 5f, 3, 0.7f, 1.4f, false); break; }
                case 2:
                    int free = Random.Range(-1, 2);
                    for (int lane = -1; lane <= 1; lane++)
                    {
                        if (lane == free) Coins(t, lane * laneWidth, z - 4.5f, 4, 0.8f, 1.5f, false);
                        else StoneBlock(t, lane * laneWidth, z);
                    }
                    break;
            }
        }

        Underside(t, L, gapZ);
        // segmenti i fundit para kthesës: udha e re fillon menjëherë pas tij, në anën e kthesës
        if (beforeTurn) { keepOutSide = nextTurn; keepOutZ = L - 3f; }
        Scenery(t, L);
        keepOutSide = 0; keepOutZ = float.MaxValue;
        Advance(t.gameObject, L);

        // fuqi në një segment pa pengesa (pas bashkimit të mesh-eve, që të rrotullohet)
        if (rows.Count == 0 && !safe && sincePower >= 6 && Prog > 120f && Random.value < 0.6f)
        {
            sincePower = 0;
            var lloji = (LlojiFuqise)Random.Range(0, 3);
            Fuqia.Krijo(t, new Vector3(Random.Range(-1, 2) * laneWidth, 1.3f, L * 0.5f), lloji, gold);
        }
    }

    // lloji i pengesës: në vagon vetëm portë (ulu) ose gur anash (anohu)
    int Lloji(int n) => vagon ? (Random.value < 0.45f ? 1 : 5) : Random.Range(0, n);

    // ---------------- vagoni (bota e varrit) ----------------
    // gur i gdhendur që del nga muri në lartësinë e trupit (ana = -1 majtas, 1 djathtas). Nuk ka collider të fortë:
    // PlayerController.GuriAnash e kontrollon kur Arta hyn në trigger dhe e shpëton vetëm nëse anohet në anën tjetër.
    void Anash(Transform t, float z, int ana)
    {
        float x0 = ana * (H + 0.7f), x1 = -ana * 0.15f, w = Mathf.Abs(x1 - x0), cx = (x0 + x1) * 0.5f;
        Block(t, "Anash_Guri", new Vector3(cx, 1.8f, z), new Vector3(w, 1.2f, 0.9f), carved, false);
        Block(t, "Anash_Kapak", new Vector3(cx, 2.46f, z), new Vector3(w + 0.1f, 0.12f, 1.0f), gold, false);
        Block(t, "Anash_Koka", new Vector3(x1 + ana * 0.25f, 1.8f, z), new Vector3(0.5f, 0.7f, 0.95f), gold, false);
        Theks(t, new Vector3(cx, 1.17f, z - 0.47f), new Vector3(w, 0.06f, 0.05f));
        Theks(t, new Vector3(x1 + ana * 0.03f, 1.8f, z - 0.47f), new Vector3(0.06f, 1.2f, 0.05f));
        var k = new GameObject("Anash_Kontrolli");
        k.transform.SetParent(t, false);
        k.transform.localPosition = new Vector3(0, 1.2f, z - 0.3f);
        var bc = k.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(Wd, 2.2f, 0.4f);
        k.AddComponent<PengeseAnash>().ana = ana;
    }

    // shinat e vagonit: dy shina hekuri mbi traversa druri
    void Shinat(Transform t, float z0, float z1)
    {
        float len = z1 - z0;
        if (len < 0.1f) return;
        float zc = (z0 + z1) * 0.5f;
        var dru = wood != null ? wood : carved;
        for (int s = -1; s <= 1; s += 2)
            Block(t, "Shina", new Vector3(s * 0.5f, 0.07f, zc), new Vector3(0.09f, 0.1f, len), stoneDark, false);
        for (float z = z0 + 0.45f; z < z1; z += 1.1f)
            Block(t, "Traversa", new Vector3(0, 0.025f, z), new Vector3(1.45f, 0.05f, 0.26f), dru, false);
    }

    // ---------------- varri: korridor nën tokë me mure, tavan, kolona, pishtarë dhe statuja ----------------
    const float VarrLart = 9f;
    void Varr(Transform t, float L)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (H + 1.3f), xi = s * (H + 0.68f);   // qendra e murit dhe faqja e brendshme
            Block(t, "Varri_Muri", new Vector3(x, VarrLart * 0.5f - 0.5f, L * 0.5f), new Vector3(1.2f, VarrLart + 1f, L), wall, false);
            Block(t, "Varri_Brezi", new Vector3(xi, 3.6f, L * 0.5f), new Vector3(0.1f, 1.1f, L), carved, false);
            Block(t, "Varri_Vija_Ari", new Vector3(xi, 2.95f, L * 0.5f), new Vector3(0.12f, 0.12f, L), gold, false);
            Block(t, "Varri_Vija_Ari", new Vector3(xi, 4.25f, L * 0.5f), new Vector3(0.12f, 0.12f, L), gold, false);
            for (float z = 4f; z < L; z += 8f)
            {
                Block(t, "Varri_Kolona", new Vector3(xi - s * 0.15f, VarrLart * 0.5f - 0.5f, z), new Vector3(0.5f, VarrLart + 1f, 0.9f), carved, false);
                Block(t, "Varri_Kapiteli", new Vector3(xi - s * 0.2f, VarrLart - 1.2f, z), new Vector3(0.7f, 0.5f, 1.2f), gold, false);
            }
            // pishtar në mur, mes kolonave (anët alternohen)
            if (flame != null)
                for (float z = (s < 0 ? 8f : 16f); z < L; z += 16f)
                {
                    Block(t, "Varri_Mbajtesja", new Vector3(xi - s * 0.18f, 2.95f, z), new Vector3(0.36f, 0.12f, 0.14f), stoneDark, false);
                    Pishtari.Krijo(t, new Vector3(xi - s * 0.35f, 3.05f, z), flame, gold, pishtarDrita);
                }
        }
        Block(t, "Varri_Tavani", new Vector3(0, VarrLart + 0.5f, L * 0.5f), new Vector3(Wd + 4.2f, 1f, L), stoneDark, false);
        for (float z = 4f; z < L; z += 8f)
            Block(t, "Varri_Trau", new Vector3(0, VarrLart - 0.3f, z), new Vector3(Wd + 2.6f, 0.6f, 0.8f), carved, false);
        // statujë faraoni mbi bordurë, herë pas here
        if (Random.value < 0.45f)
        {
            int s = Random.value < 0.5f ? -1 : 1;
            float z = Random.Range(5f, L - 5f), x = s * (H + 0.3f);
            Block(t, "Varri_Statuja", new Vector3(x, 2.05f, z), new Vector3(0.6f, 2.6f, 0.7f), carved, false);
            Block(t, "Varri_Statuja_Koka", new Vector3(x, 3.6f, z), new Vector3(0.5f, 0.55f, 0.55f), gold, false);
            Pyramid(t, new Vector3(x, 3.87f, z), 0.75f, 0.45f);
        }
    }

    // ---------------- teposhtja e akullit (bota e akullit) ----------------
    // Rampë akulli 72 m që zbret 10 m: Arta rrëshqet vetë dhe më shpejt (PlayerController e njeh nga emri "Teposhtja"),
    // anohet rreth blloqeve të akullit dhe kalon poshtë portës. Pas saj udha vazhdon 10 m më poshtë.
    void SpawnTeposhte()
    {
        var t0 = NewRoot("Teposhtja_" + count);
        const float L = 72f, D = 10f;
        float len = Mathf.Sqrt(L * L + D * D);
        var t = new GameObject("Rampa").transform;
        t.SetParent(t0, false);
        t.localRotation = Quaternion.Euler(Mathf.Atan2(D, L) * Mathf.Rad2Deg, 0f, 0f);
        Material akull = water != null ? water : deckCur;
        Block(t, "Teposhtja", new Vector3(0, -0.7f, len * 0.5f), new Vector3(Wd, 1.4f, len), akull, true);
        Block(t, "Brezi", new Vector3(0, -1.75f, len * 0.5f), new Vector3(Wd + 0.7f, 0.7f, len), carved, false);
        for (int s = -1; s <= 1; s += 2)
        {
            Block(t, "Bordura", new Vector3(s * (H + 0.25f), 0.32f, len * 0.5f), new Vector3(0.7f, 0.64f, len), carved, true);
            Block(t, "Bordura_Kapak", new Vector3(s * (H + 0.25f), 0.7f, len * 0.5f), new Vector3(0.86f, 0.14f, len), akull, false);
        }
        // monedha në mes në fillim, dy rreshta blloqesh akulli (njëra korsi e lirë) dhe një portë poshtë
        Coins(t, 0, 4f, 6, 0.6f, 2f, false);
        foreach (float zr in new[] { 24f, 46f })
        {
            int free = Random.Range(-1, 2);
            for (int lane = -1; lane <= 1; lane++)
            {
                if (lane == free) Coins(t, lane * laneWidth, zr - 7f, 4, 0.6f, 1.6f, false);
                else StoneBlock(t, lane * laneWidth, zr);
            }
        }
        Coins(t, 0, 56f, 6, 0.9f, 1.6f, false);   // pa portë: Arta zbret më këmbë (snowboard), jo shtrirë
        // këmbët e rampës (vertikale)
        for (float z = 8f; z < L; z += 24f)
        {
            float top = -z * D / L - 2.1f;
            Block(t0, "Kemba", new Vector3(0, top - 25f, z), new Vector3(Wd - 3.0f, 50f, 2.4f), wall, false);
        }
        // male akulli larg anash
        for (int i = 0; i < 2; i++)
        {
            int s = Random.value < 0.5f ? -1 : 1;
            Spire(Group(t0, "Deko_Mali"), new Vector3(s * Random.Range(30f, 90f), -D * 0.5f, Random.Range(0f, L)));
        }
        var korr = Kutia(t, -(H + KorridorAnash), H + KorridorAnash, KorridorPoshte, KorridorLart, 0f, len);
        trackDist += L;
        cursor += dir * L + Vector3.down * D;
        AddPiece(t0, trackDist, korr);
    }

    // ---------------- portali për në botën tjetër ----------------
    // pjesë e drejtë 56 m pa pengesa; portali në mes (28+ m bosh para dhe pas), monedhat të çojnë drejt tij
    void SpawnPortal(int bota)
    {
        var t = NewRoot("Portali_" + count);
        float L = 56f, z = 30f, th = 4.2f;
        Deck(t, 0, L);
        Underside(t, L, -1);
        if (brenda) Varr(t, L);
        // harku prej guri: dy kulla dhe harku (ose arkitrau) sipër, si porta e tempullit
        Tower(t, new Vector3(-(H + 1.1f), 0, z), 1.5f, th, false);
        Tower(t, new Vector3(H + 1.1f, 0, z), 1.5f, th, false);
        float hapja = 2f * (H + 1.1f) - 1.5f;   // gjerësia mes kullave
        float lart;
        if (archMesh != null)
        {
            MeshPiece(t, "Portali_Harku", archMesh, new Vector3(0, th + 0.3f, z), Vector3.one, wall);
            float top = th + 0.3f + archMesh.bounds.size.y;
            Block(t, "Portali_Guri_Kyç", new Vector3(0, top - 0.45f, z), new Vector3(0.9f, 1.1f, 2.0f), carved, false);
            lart = th + 0.3f + Mathf.Max(1f, archMesh.bounds.size.y - 1.5f);
        }
        else
        {
            Block(t, "Portali_Arkitrau", new Vector3(0, 7.7f, z), new Vector3(Wd + 4.0f, 1.2f, 1.8f), carved, false);
            lart = 7.1f;
        }
        // dy rreshta monedhash nga anët që bashkohen në mes, para portalit
        for (int i = 0; i < 9; i++)
        {
            float k = i / 8f, zz = 6f + i * 2.4f;
            GetCoin(t, new Vector3(-laneWidth * (1f - k), 1.2f, zz));
            if (i < 8) GetCoin(t, new Vector3(laneWidth * (1f - k), 1.2f, zz + 1.2f));
        }
        Nature(t, L);
        Advance(t.gameObject, L);
        Portali.Krijo(t, new Vector3(0, 0, z), bota, hapja, lart, flame);
        Debug.Log("Tempulli: portali për në " + Udhetimi.Botet[bota].emri + " te " + (int)Prog + " m");
    }

    static readonly Unity.Profiling.ProfilerMarker mSpawnJunction = new Unity.Profiling.ProfilerMarker("Tempulli.SpawnJunction");
    void SpawnJunction()
    {
        using var _prof = mSpawnJunction.Auto();
        var t = NewRoot("Kthesa_" + count);
        int turn = nextTurn;
        nextTurn = Random.value < 0.5f ? -1 : 1;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float J = Wd + 0.6f, c = J / 2f - 0.4f, wallZ = c + J / 2f + 0.35f;
        Vector3 center = cursor + dir * c;

        // sheshi i kryqëzimit
        Block(t, "Dyshemeja", new Vector3(0, -0.7f, c), new Vector3(J, 1.4f, J), deckCur, true);
        Block(t, "Brezi", new Vector3(0, -1.9f, c), new Vector3(J + 0.6f, 1.0f, J + 0.6f), carved, false);
        Block(t, "Kembet", new Vector3(0, -27f, c), new Vector3(J - 2.3f, 50f, J - 2.3f), wall, false);
        // muri përballë (vret nëse nuk kthehesh) - mur tempulli me brez të gdhendur
        var front = Block(t, "Muri_Perballe", new Vector3(0, 2.2f, wallZ), new Vector3(Wd + 1.4f, 4.4f, 0.9f), wall, true);
        front.AddComponent<Obstacle>();
        Block(t, "Muri_Brezi", new Vector3(0, 3.6f, wallZ - 0.5f), new Vector3(Wd + 1.5f, 0.9f, 0.15f), carved, false);
        Gable(t, new Vector3(0, 4.4f, wallZ), Wd + 2.2f, 2.2f, 1.6f);
        // bordura në anën e mbyllur
        Curb(t, -turn * (H + 0.25f), 0f, J);
        Tower(Group(t, "Deko_Kulla"), new Vector3(-turn * (H + 0.8f), 0, wallZ - 0.05f), 1.5f, 7.5f, true);
        Tower(Group(t, "Deko_Kulla"), new Vector3(turn * (H + 0.8f), 0, wallZ - 0.05f), 1.5f, 7.5f, true);
        // shigjetat e arta në dysheme
        for (int i = 0; i < 3; i++)
            Block(t, "Shigjeta", new Vector3(turn * (i * 0.9f - 0.4f), 0.02f, 4f), new Vector3(0.5f, 0.04f, 1.6f - i * 0.45f), glow, false);
        Coins(t, 0, 0.5f, 3, 0.8f, 1.4f, false);
        if (vagon)
        {
            // shinat hyjnë deri në mes të sheshit dhe dalin nga ana e kthesës
            Shinat(t, 0f, c);
            var shK = new GameObject("Shinat_Kthesa").transform;
            shK.SetParent(t, false);
            shK.localPosition = new Vector3(0, 0, c);
            shK.localRotation = Quaternion.Euler(0, 90f * turn, 0);
            Shinat(shK, 0f, J * 0.5f);
        }
        if (brenda)
        {
            float big = J + 2.4f;
            Block(t, "Varri_Muri", new Vector3(-turn * (J * 0.5f + 0.6f), VarrLart * 0.5f - 0.5f, c), new Vector3(1.2f, VarrLart + 1f, big), wall, false);
            Block(t, "Varri_Muri", new Vector3(0, VarrLart * 0.5f + 2.5f, wallZ + 0.2f), new Vector3(big, VarrLart - 5f, 1f), wall, false);
            Block(t, "Varri_Tavani", new Vector3(0, VarrLart + 0.5f, c), new Vector3(big, 1f, big), stoneDark, false);
        }

        junctions.Add(new Junction { center = center, newDir = side * turn, turn = turn });
        float endDist = trackDist + J - 0.8f;
        run++;   // vija e re fillon te kthesa
        float kw = J * 0.5f + KorridorAnash;
        AddPiece(t, endDist + 40f, Kutia(t, -kw, kw, KorridorPoshte, KorridorLart, c - kw, c + kw));

        dir = side * turn;
        cursor = center + dir * (J / 2f);
        afterTurn = true;
        trackDist = endDist;
    }

    // ---------------- gjysmë-shtegu: njëra anë është shembur, anohu në anën tjetër ----------------
    static readonly Unity.Profiling.ProfilerMarker mSpawnHalf = new Unity.Profiling.ProfilerMarker("Tempulli.SpawnHalf");
    void SpawnHalf()
    {
        using var _prof = mSpawnHalf.Auto();
        var t = NewRoot("Gjysme_" + count);
        float L = segmentLength + 6f;
        int keep = Random.value < 0.5f ? -1 : 1;    // ana që mbetet
        float z0 = 6f, z1 = L - 5f;
        Deck(t, 0, z0);
        Deck(t, z1, L);
        // gjysma që mbetet (pak më e gjerë se gjysma, që të ketë vend)
        float w = Wd * 0.55f, cx = keep * (H - w / 2f);
        Block(t, "Shtegu", new Vector3(cx, -0.7f, (z0 + z1) / 2), new Vector3(w, 1.4f, z1 - z0), deckCur, true);
        Block(t, "Brezi", new Vector3(cx + keep * 0.35f, -1.75f, (z0 + z1) / 2), new Vector3(w + 0.7f, 0.7f, z1 - z0), carved, false);
        Curb(t, keep * (H + 0.25f), z0, z1);
        // skaji i thyer: gurë të shkëputur dhe copa të varura
        float edge = cx - keep * w / 2f;
        for (float z = z0; z < z1; z += Random.Range(1.2f, 2.2f))
        {
            var r = Block(t, "Thyerje", new Vector3(edge - keep * Random.Range(0.1f, 0.5f), Random.Range(-1.6f, -0.5f), z),
                          new Vector3(Random.Range(0.5f, 1.1f), Random.Range(0.6f, 1.4f), Random.Range(0.8f, 1.6f)), deckCur, false);
            r.transform.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(-20f, 20f), Random.Range(-30f, 30f));
        }
        // kolona e mbetur dhe harku i thyer në anën e shembur
        Block(t, "Kemba", new Vector3(cx, -27f, (z0 + z1) / 2), new Vector3(3.2f, 50f, 2.4f), wall, false);
        Coins(t, keep * laneWidth, z0 + 1f, 6, 0.8f, 2.4f, false);
        Underside(t, z0, -1);
        Scenery(t, L);
        Advance(t.gameObject, L);
    }

    // ---------------- lumi: vrapon nëpër ujë të cekët, me gurë për t'u shmangur ----------------
    static readonly Unity.Profiling.ProfilerMarker mSpawnRiver = new Unity.Profiling.ProfilerMarker("Tempulli.SpawnRiver");
    void SpawnRiver()
    {
        using var _prof = mSpawnRiver.Auto();
        var t = NewRoot("Lumi_" + count);
        float L = 40f;
        Material bed = riverbed != null ? riverbed : rock;
        // shtrati i lumit (në nivel me shtegun) dhe uji i cekët sipër tij
        Block(t, "Shtrati", new Vector3(0, -0.7f, L / 2), new Vector3(Wd, 1.4f, L), bed, true);
        if (water != null)
        {
            var wq = Block(t, "Uji", new Vector3(0, 0.2f, L / 2), new Vector3(Wd + 0.4f, 0.02f, L + 0.4f), water, false);
            wq.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var zone = new GameObject("Uji_Zona");
        zone.transform.SetParent(t, false);
        zone.transform.localPosition = new Vector3(0, 0.8f, L / 2);
        var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(Wd, 2f, L - 1f);
        zone.AddComponent<WaterZone>();
        // brigjet: mure guri me bar
        for (int s = -1; s <= 1; s += 2)
        {
            Block(t, "Brigu", new Vector3(s * (H + 0.45f), 0.25f, L / 2), new Vector3(0.9f, 1.3f, L), wall, true);
            Block(t, "Brigu_Kapak", new Vector3(s * (H + 0.45f), 0.95f, L / 2), new Vector3(1.05f, 0.14f, L), deckCur, false);
            for (float z = 1.5f; z < L; z += Random.Range(2f, 4f)) Grass(t, new Vector3(s * (H + 0.3f), 1.0f, z), Random.Range(0.8f, 1.4f));
        }
        Block(t, "Brezi", new Vector3(0, -1.9f, L / 2), new Vector3(Wd + 1.2f, 0.8f, L), carved, false);
        // kolona të arta në hyrje dhe dalje
        foreach (float z in new[] { 0.6f, L - 0.6f })
            for (int s = -1; s <= 1; s += 2)
            {
                Block(t, "Kolona_Ari", new Vector3(s * (H + 0.45f), 1.6f, z), new Vector3(1.1f, 3.2f, 1.1f), carved, false);
                Block(t, "Kolona_Ari_Kapak", new Vector3(s * (H + 0.45f), 3.3f, z), new Vector3(1.4f, 0.25f, 1.4f), deckCur, false);
            }
        // gurë në lumë (anohu rreth tyre) dhe monedha në rrugën e lirë
        for (float z = 10f; z < L - 6f; z += Random.Range(9f, 12f))
        {
            int free = Random.Range(-1, 2);
            for (int lane = -1; lane <= 1; lane++)
            {
                if (lane == free) { Coins(t, lane * laneWidth, z - 4f, 3, 0.8f, 1.5f, false); continue; }
                if (Random.value < 0.35f) continue;
                var b = Block(t, "Blloku_Lumi", new Vector3(lane * laneWidth, 0.6f, z), new Vector3(laneWidth * 0.75f, 1.5f, 1.4f), null, true);
                b.AddComponent<Obstacle>();
                b.GetComponent<MeshRenderer>().enabled = false;
                if (rocks.Count > 4) Place(rocks[4], b.transform, new Vector3(0, -0.2f, 0), Random.Range(0f, 360f), 0.36f);
                else Block(b.transform, "Guri", Vector3.zero, new Vector3(laneWidth * 0.75f, 1.5f, 1.4f), rock, false);
            }
        }
        Underside(t, L, -1);
        Scenery(t, L);
        Advance(t.gameObject, L);
    }

    // ---------------- litari: kapet vetë dhe rrëshqet mbi humnerë deri në platformën tjetër ----------------
    static readonly Unity.Profiling.ProfilerMarker mSpawnZipline = new Unity.Profiling.ProfilerMarker("Tempulli.SpawnZipline");
    void SpawnZipline()
    {
        using var _prof = mSpawnZipline.Auto();
        var t = NewRoot("Litari_" + count);
        float start = 10f, span = Random.Range(34f, 44f), drop = Random.Range(4f, 7f), endLen = 12f;
        float L = start + span + endLen;
        Deck(t, 0, start);
        Underside(t, start, -1);
        // shtyllat e litarit
        Vector3 aLocal = new Vector3(0, 4.7f, start - 0.6f);
        Vector3 bLocal = new Vector3(0, 4.7f - drop, start + span + 1.5f);
        foreach (var (p, h) in new[] { (aLocal, 0f), (bLocal, -drop) })
        {
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Shtylla_Litarit", new Vector3(s * (H - 1.1f), h + 2.7f, p.z), new Vector3(0.45f, 5.4f, 0.45f), barkTex != null ? barkTex : bark, false);
            Block(t, "Trau_Litarit", new Vector3(0, h + 5.2f, p.z), new Vector3(Wd - 1.6f, 0.4f, 0.4f), barkTex != null ? barkTex : bark, false);
        }
        // litari (pak i varur në mes)
        int segs = 12; Vector3 prev = aLocal;
        for (int i = 1; i <= segs; i++)
        {
            float u = i / (float)segs;
            Vector3 q = Vector3.Lerp(aLocal, bLocal, u) - Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.8f;
            var rope = Prim(PrimitiveType.Cylinder, t, "Litari", (prev + q) / 2f, new Vector3(0.07f, (q - prev).magnitude / 2f, 0.07f), bark, false);
            rope.transform.localRotation = Quaternion.FromToRotation(Vector3.up, q - prev);
            prev = q;
        }
        // platforma tjetër, më poshtë
        var t2 = new GameObject("Platforma").transform;
        t2.SetParent(t, false);
        t2.localPosition = new Vector3(0, -drop, start + span);
        Deck(t2, 0, endLen);
        Underside(t2, endLen, -1);
        // monedha përgjatë litarit, në lartësinë e trupit
        for (int i = 0; i < 9; i++)
        {
            float u = 0.12f + i * 0.09f;
            Vector3 q = Vector3.Lerp(aLocal, bLocal, u) - Vector3.up * (Mathf.Sin(u * Mathf.PI) * 0.8f + 1.4f);
            Coins(t, 0, q.z, 1, q.y, 1f, false);
        }
        // shkëmbinj dhe pamje anash humnerës
        Scenery(t, L);
        ziplines.Add(new Zipline { a = t.TransformPoint(aLocal), b = t.TransformPoint(bLocal), sag = 0.8f });
        trackDist += L;
        cursor += dir * L + Vector3.down * drop;
        // korridori i litarit përfshin edhe zbritjen deri te platforma më poshtë
        AddPiece(t, trackDist, KutiaUdhes(t, 0f, L, KorridorPoshte - drop));
    }

    // trung i madh peme i rënë mbi shtegun: rrëshqit poshtë tij
    void FallenTree(Transform t, float z)
    {
        var o = Block(t, "Porta_Trungu", new Vector3(0, 2.05f, z), new Vector3(Wd - 0.6f, 1.9f, 1.8f), null, true);
        o.AddComponent<Obstacle>();
        o.GetComponent<MeshRenderer>().enabled = false;
        var mat = barkTex != null ? barkTex : bark;
        float yaw = Random.Range(-8f, 8f);
        var trunk = Prim(PrimitiveType.Cylinder, t, "Trungu_Rene", new Vector3(0, 2.15f, z), new Vector3(2.0f, 8f, 2.0f), mat, false);
        trunk.transform.localRotation = Quaternion.Euler(0, yaw, 90f);
        // vija që shkëlqen poshtë trungut (rrëshqit poshtë saj), e kthyer si trungu
        var thT = Theks(t, new Vector3(0, 1.1f, z), new Vector3(Wd - 0.6f, 0.08f, 0.5f));
        if (thT != null) thT.localRotation = Quaternion.Euler(0, yaw, 0);
        // rrënjët në njërin skaj, degë e gjethe në tjetrin
        int s = Random.value < 0.5f ? -1 : 1;
        var roots = Prim(PrimitiveType.Cylinder, t, "Rrenjet", new Vector3(s * (H + 4.9f), 2.1f, z + s * Mathf.Sin(yaw * Mathf.Deg2Rad) * 8f), new Vector3(3.6f, 0.35f, 3.6f), mat, false);
        roots.transform.localRotation = Quaternion.Euler(0, yaw, 90f);
        for (int i = 0; i < 3; i++)
        {
            var br = Prim(PrimitiveType.Cylinder, t, "Dega", new Vector3(-s * Random.Range(3f, 7f), 3.2f, z + Random.Range(-0.6f, 0.6f)), new Vector3(0.35f, 1.6f, 0.35f), mat, false);
            br.transform.localRotation = Quaternion.Euler(Random.Range(-40f, 40f), 0, Random.Range(-35f, 35f));
            if (leaves != null)
                Prim(PrimitiveType.Sphere, t, "Gjethet", br.transform.localPosition + new Vector3(0, 1.6f, 0), Vector3.one * Random.Range(2.2f, 3.2f), leaves, false);
        }
        if (moss != null) Prim(PrimitiveType.Sphere, t, "Myshk", new Vector3(Random.Range(-2f, 2f), 3.1f, z), new Vector3(2.5f, 0.35f, 1.4f), moss, false);
        for (int i = 0; i < 3; i++) Grass(t, new Vector3(Random.Range(-H + 0.3f, H - 0.3f), 3.15f, z + Random.Range(-0.4f, 0.4f)), Random.Range(0.7f, 1.1f));
    }

    // tufë bari (dy plane të kryqëzuara me teksturë me alfa)
    void Grass(Transform t, Vector3 p, float size)
    {
        if (grass == null) return;
        var g = new GameObject("Bari");
        g.transform.SetParent(t, false);
        g.transform.localPosition = p;
        g.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 180f), 0);
        g.transform.localScale = Vector3.one * size;
        g.AddComponent<MeshFilter>().sharedMesh = GrassMesh();
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = grass;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Mesh grassMesh;
    static Mesh GrassMesh()
    {
        if (grassMesh != null) return grassMesh;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        for (int k = 0; k < 2; k++)
        {
            Vector3 d = k == 0 ? Vector3.right : Vector3.forward;
            int i0 = v.Count;
            v.Add(-d * 0.6f); v.Add(d * 0.6f); v.Add(d * 0.6f + Vector3.up); v.Add(-d * 0.6f + Vector3.up);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            tr.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
        }
        grassMesh = new Mesh { name = "Bari" };
        grassMesh.SetVertices(v); grassMesh.SetUVs(0, uv); grassMesh.SetTriangles(tr, 0);
        grassMesh.RecalculateNormals(); grassMesh.RecalculateBounds();
        return grassMesh;
    }

    void Advance(GameObject go, float L)
    {
        var korr = KutiaUdhes(go.transform, 0f, L);   // para se të lëvizë kursori (kutia është në hapësirën e pjesës)
        trackDist += L;
        cursor += dir * L;
        AddPiece(go.transform, trackDist, korr);
    }

    // dyshemeja e artë + bordurat e gdhendura + brezi anësor
    void Deck(Transform t, float z0, float z1)
    {
        float len = z1 - z0;
        if (len < 0.1f) return;
        float zc = (z0 + z1) / 2;
        Block(t, "Shtegu", new Vector3(0, -0.7f, zc), new Vector3(Wd, 1.4f, len), deckCur, true);
        Block(t, "Brezi", new Vector3(0, -1.75f, zc), new Vector3(Wd + 0.7f, 0.7f, len), carved, false);
        Curb(t, -(H + 0.25f), z0, z1);
        Curb(t, H + 0.25f, z0, z1);
        if (vagon) Shinat(t, z0, z1);
    }

    void Curb(Transform t, float x, float z0, float z1)
    {
        float len = z1 - z0, zc = (z0 + z1) / 2;
        Block(t, "Bordura", new Vector3(x, 0.32f, zc), new Vector3(0.7f, 0.64f, len), carved, true);
        Block(t, "Bordura_Kapak", new Vector3(x, 0.7f, zc), new Vector3(0.86f, 0.14f, len), deckCur, false);
        // shtylla të vogla mbi bordurë çdo ~6 m: ritëm dhe ndjesi shpejtësie, pa penguar pamjen
        for (float z = Mathf.Ceil((z0 + 1f) / 6f) * 6f; z < z1 - 0.8f; z += 6f)
        {
            Block(t, "Bordura_Shtylle", new Vector3(x, 1.12f, z), new Vector3(0.62f, 0.7f, 0.62f), carved, false);
            Block(t, "Bordura_Shtylle_Kapak", new Vector3(x, 1.52f, z), new Vector3(0.78f, 0.12f, 0.78f), deckCur, false);
            // pishtar çdo shtyllë të tretë (anët alternohen): dritë e ngrohtë që dridhet mbi shteg
            int k = Mathf.RoundToInt(z / 6f) + count * 4;
            if (flame != null && (k + (x < 0 ? 0 : 1)) % Mathf.Max(1, pishtarCdo) == 0) Pishtari.Krijo(t, new Vector3(x, 1.58f, z), flame, gold, pishtarDrita);
        }
    }

    // këmbët e urës dhe harqet poshtë shtegut
    void Underside(Transform t, float L, float gapZ)
    {
        if (brenda) return;   // varri: poshtë është toka, s'ka urë
        for (float z = 0; z < L; z += 10f)
        {
            Block(t, "Kemba", new Vector3(0, -27f, z + 1.2f), new Vector3(Wd - 3.0f, 50f, 2.4f), wall, false);
            Block(t, "Kemba_Kapiteli", new Vector3(0, -2.5f, z + 1.2f), new Vector3(Wd - 1.8f, 0.8f, 3.0f), carved, false);
            // harku midis dy këmbëve
            float zc = z + 6.2f, R = 3.8f;
            Vector3 c = new Vector3(0, -6.0f, zc);
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.PI * i / 8f;
                var p = c + new Vector3(0, Mathf.Sin(a) * R, Mathf.Cos(a) * R);
                var b = Block(t, "Harku", p, new Vector3(Wd - 3.4f, 0.9f, 1.55f), carved, false);
                b.transform.localRotation = Quaternion.Euler(-Mathf.Rad2Deg * a + 90f, 0, 0);
            }
            Block(t, "Muri_Mbi_Hark", new Vector3(0, -2.4f, zc), new Vector3(Wd - 3.6f, 1.6f, 7.4f), wall, false);
        }
    }

    // ---------------- pengesat ----------------
    void Root(Transform t, float z)
    {
        var o = Block(t, "Rrenja", new Vector3(0, 0.35f, z), new Vector3(Wd - 0.6f, 0.7f, 0.7f), null, true);
        o.AddComponent<Obstacle>();
        o.GetComponent<MeshRenderer>().enabled = false;
        o.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-3f, 3f));
        // vija mbi rrënjë (kërce mbi të) dhe vija në dysheme para saj
        Theks(t, new Vector3(0, 0.70f, z), new Vector3(Wd - 0.9f, 0.05f, 0.14f));
        Theks(t, new Vector3(0, 0.015f, z - 0.9f), new Vector3(Wd - 0.9f, 0.03f, 0.12f));
        // rrënjë e trashë peme që kalon shtegun, me degëzime
        var log = Prim(PrimitiveType.Cylinder, t, "Rrenja_Trungu", new Vector3(0, 0.36f, z), new Vector3(0.62f, H - 0.12f, 0.62f), barkTex != null ? barkTex : bark, false);
        // pa anim të madh anash: më parë skajet e trungut dilnin përmes murit anësor
        log.transform.localRotation = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-2f, 2f), 90f);
        for (int i = 0; i < 4; i++)
        {
            float x = Random.Range(-H + 0.7f, H - 0.7f);
            var k = Prim(PrimitiveType.Cylinder, t, "Rrenja_Dege", new Vector3(x, 0.25f, z + Random.Range(-0.5f, 0.5f)), new Vector3(0.28f, 0.6f, 0.28f), bark, false);
            k.transform.localRotation = Quaternion.Euler(Random.Range(50f, 80f) * (Random.value < 0.5f ? 1 : -1), Random.Range(0f, 360f), 0);
        }
        if (moss != null && Random.value < 0.7f)
            Prim(PrimitiveType.Sphere, t, "Rrenja_Myshk", new Vector3(Random.Range(-2f, 2f), 0.62f, z), new Vector3(1.4f, 0.25f, 0.6f), moss, false);
    }

    // shirit që shkëlqen mbi një pengesë (vetëm në botët e errëta): pa collider dhe pa hije
    Transform Theks(Transform t, Vector3 pos, Vector3 size)
    {
        if (theks == null) return null;
        var g = Block(t, "Theksi", pos, size, theks, false);
        g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    void Gate(Transform t, float z)
    {
        // trau i gdhendur (rrëshqit poshtë tij) mbi dy shtylla guri
        var beam = Block(t, "Porta", new Vector3(0, 2.0f, z), new Vector3(Wd - 0.6f, 1.8f, 0.7f), carved, true);
        beam.AddComponent<Obstacle>();
        // buza e poshtme e traut (rrëshqit poshtë saj) dhe buza e sipërme
        Theks(t, new Vector3(0, 1.16f, z - 0.37f), new Vector3(Wd - 0.6f, 0.12f, 0.05f));
        Theks(t, new Vector3(0, 2.84f, z - 0.37f), new Vector3(Wd - 0.6f, 0.06f, 0.05f));
        Block(t, "Porta_Kapak", new Vector3(0, 2.98f, z), new Vector3(Wd + 0.2f, 0.16f, 0.95f), deckCur, false);
        for (int s = -1; s <= 1; s += 2)
        {
            var p = Block(t, "Porta_Shtylla", new Vector3(s * (H - 0.35f), 1.45f, z), new Vector3(0.6f, 2.9f, 0.8f), wall, true);
            p.AddComponent<Obstacle>();
        }
    }

    void StoneBlock(Transform t, float x, float z)
    {
        float bw = laneWidth * 0.85f;
        var b = Block(t, "Blloku", new Vector3(x, 1.2f, z), new Vector3(bw, 2.4f, 1.2f), wall, true);
        b.AddComponent<Obstacle>();
        // korniza që shkëlqen në faqen përpara: buza e sipërme dhe anët
        Theks(t, new Vector3(x, 2.34f, z - 0.66f), new Vector3(bw, 0.08f, 0.05f));
        Theks(t, new Vector3(x - bw * 0.5f + 0.04f, 1.17f, z - 0.66f), new Vector3(0.08f, 2.34f, 0.05f));
        Theks(t, new Vector3(x + bw * 0.5f - 0.04f, 1.17f, z - 0.66f), new Vector3(0.08f, 2.34f, 0.05f));
        Block(t, "Blloku_Brezi", new Vector3(x, 1.9f, z - 0.62f), new Vector3(bw + 0.02f, 0.5f, 0.06f), carved, false);
        Block(t, "Blloku_Kapak", new Vector3(x, 2.47f, z), new Vector3(bw + 0.15f, 0.14f, 1.35f), deckCur, false);
    }

    // ---------------- pamja përreth ----------------
    void Scenery(Transform t, float L)
    {
        if (brenda) { Varr(t, L); return; }
        // portë e madhe tempulli me çati, çdo segment të tretë
        if (count % 3 == 0 && archMesh != null && (count / 3) % 2 == 1)
        {
            // portë me hark guri (ProBuilder): dy kulla të ulëta dhe harku mbi to
            float z = L * 0.5f, th = 4.2f;
            var g = Group(t, "Deko_Porta");
            Tower(g, new Vector3(-(H + 1.1f), 0, z), 1.5f, th, false);
            Tower(g, new Vector3(H + 1.1f, 0, z), 1.5f, th, false);
            MeshPiece(g, "Harku", archMesh, new Vector3(0, th + 0.3f, z), Vector3.one, wall);
            float top = th + 0.3f + archMesh.bounds.size.y;
            Block(g, "Harku_Guri_Kyç", new Vector3(0, top - 0.45f, z), new Vector3(0.9f, 1.1f, 2.0f), carved, false);
        }
        else if (count % 3 == 0)
        {
            float z = L * 0.5f;
            var g = Group(t, "Deko_Porta");
            Tower(g, new Vector3(-(H + 1.1f), 0, z), 1.5f, 7.2f, false);
            Tower(g, new Vector3(H + 1.1f, 0, z), 1.5f, 7.2f, false);
            Block(g, "Arkitrau", new Vector3(0, 7.7f, z), new Vector3(Wd + 4.0f, 1.2f, 1.8f), carved, false);
            Block(g, "Arkitrau_Kapak", new Vector3(0, 8.4f, z), new Vector3(Wd + 4.4f, 0.2f, 2.0f), deckCur, false);
            Gable(g, new Vector3(0, 8.5f, z), Wd + 4.6f, 2.6f, 2.4f);
        }
        else
        {
            for (int s = -1; s <= 1; s += 2)
                if (Random.value < 0.25f)
                    Tower(Group(t, "Deko_Kulla"), new Vector3(s * Random.Range(H + 2.1f, H + 4.9f), 0, Random.Range(3f, L - 3f)), Random.Range(1.4f, 2.4f), Random.Range(2.5f, 9f), Random.value < 0.5f);
        }
        Nature(t, L);
        // shkëmbinj-kulla në largësi me tempuj të vegjël në majë (grupi është në origjinën e pjesës: koordinatat s'ndryshojnë)
        if (Random.value < 0.7f)
        {
            int s = Random.value < 0.5f ? -1 : 1;
            Spire(Group(t, "Deko_Mali"), new Vector3(s * Random.Range(28f, 95f), 0, Random.Range(0f, L)));
        }
        // re të fryra poshtë urës
        for (int i = 0; i < 2; i++)
        {
            int s = Random.value < 0.5f ? -1 : 1;
            var c = Prim(PrimitiveType.Sphere, t, "Re", new Vector3(s * Random.Range(8f, 45f), Random.Range(-16f, -9f), Random.Range(0f, L)),
                         new Vector3(Random.Range(14f, 30f), Random.Range(4f, 7f), Random.Range(10f, 22f)), cloudPuff, false);
            c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // shkëmbinj organikë afër shtegut, pemë mbi ta, shkurre e fier
    void Nature(Transform t, float L)
    {
        if (brenda) return;
        if (rocks.Count == 0) return;
        for (int s = -1; s <= 1; s += 2)
        {
            if (Random.value < 0.55f)
            {
                // grupi (shkëmbi me pemën/rrënojën/shkurren sipër) është në origjinën e pjesës: koordinatat lokale s'ndryshojnë
                var g = Group(t, "Deko_Shkembi");
                var r = Place(rocks[Random.Range(0, Mathf.Min(4, rocks.Count))], g,
                              new Vector3(s * Random.Range(H + 4.9f, H + 9.9f), Random.Range(-14f, -4f), Random.Range(2f, L - 2f)),
                              Random.Range(0f, 360f), Random.Range(0.9f, 1.5f));
                KeepAway(g, r, s, H + 2.4f);
                KeepBeforeTurn(g, r, s);
                // pemë dhe shkurre mbi shkëmb
                var b = r.GetComponent<Renderer>().bounds;
                Vector3 top = g.InverseTransformPoint(new Vector3(b.center.x, b.max.y - 0.6f, b.center.z));
                float pick = Random.value;
                if (pick < 0.3f) KeepBeforeTurn(g, Ruin(g, top), s);
                else if (trees.Count > 0 && pick < 0.85f)
                    KeepBeforeTurn(g, Place(trees[Random.Range(0, trees.Count)], g, top, Random.Range(0f, 360f), Random.Range(0.8f, 1.3f)), s);
                if (bush != null && Random.value < 0.7f)
                    KeepBeforeTurn(g, Place(bush, g, top + new Vector3(Random.Range(-2f, 2f), -0.3f, Random.Range(-2f, 2f)), Random.Range(0f, 360f), Random.Range(1.2f, 2.2f)), s);
            }
            // bar i verdhë buzë shtegut
            for (int i = 0; i < 3; i++)
                if (Random.value < 0.5f) Grass(t, new Vector3(s * Random.Range(H - 0.35f, H - 0.15f), 0f, Random.Range(1f, L - 1f)), Random.Range(0.5f, 0.85f));
            // fier mbi bordurat e gdhendura
            if (fern != null)
                for (int i = 0; i < 2; i++)
                    if (Random.value < 0.45f)
                        Place(fern, t, new Vector3(s * (H + 0.35f), 0.76f, Random.Range(1f, L - 1f)), Random.Range(0f, 360f), Random.Range(0.6f, 0.95f));
        }
        // gur i vogël i rënë
        if (rocks.Count > 4 && Random.value < 0.4f)
        {
            int s = Random.value < 0.5f ? -1 : 1;
            var grp = Group(t, "Deko_Guri");
            var g = Place(rocks[4], grp, new Vector3(s * Random.Range(H + 2.9f, H + 4.9f), -2.2f, Random.Range(2f, L - 2f)), Random.Range(0f, 360f), Random.Range(0.6f, 1.2f));
            KeepAway(grp, g, s, H + 1.1f);
            KeepBeforeTurn(grp, g, s);
        }
    }

    // rrënoja mbi shkëmb: kolona të thyera dhe një tra i rrëzuar
    Transform Ruin(Transform t, Vector3 p)
    {
        var r = new GameObject("Rrenoja").transform;
        r.SetParent(t, false); r.localPosition = p; r.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        int n = Random.Range(2, 4);
        for (int i = 0; i < n; i++)
        {
            float h = Random.Range(1.5f, 4.5f);
            var c = columnMesh != null
                ? MeshPiece(r, "Kolona_Thyer", columnMesh, new Vector3(i * 2.2f - n, -0.3f, Random.Range(-0.4f, 0.4f)), new Vector3(0.9f, h / 3f, 0.9f), wall)
                : Block(r, "Kolona_Thyer", new Vector3(i * 2.2f - n, h / 2f - 0.3f, Random.Range(-0.4f, 0.4f)), new Vector3(0.8f, h, 0.8f), wall, false);
            c.transform.localRotation = Quaternion.Euler(Random.Range(-6f, 6f), 0, Random.Range(-6f, 6f));
            Block(r, "Kolona_Kapitel", new Vector3(i * 2.2f - n, h - 0.2f, 0), new Vector3(1.05f, 0.25f, 1.05f), carved, false);
        }
        var lintel = Block(r, "Tra_Rene", new Vector3(0.3f, 0.2f, 1.6f), new Vector3(3.2f, 0.6f, 0.8f), carved, false);
        lintel.transform.localRotation = Quaternion.Euler(0, Random.Range(-30f, 30f), Random.Range(8f, 20f));
        if (bush != null) Place(bush, r, new Vector3(-1f, -0.2f, 1f), Random.Range(0f, 360f), 1.3f);
        return r;
    }

    // malet e largëta: unazë shkëmbinjsh gjigantë në mjegull, rreth ~230 m, që lëviz bashkë me Artën.
    // Japin thellësi (shtresa e tretë pas shtegut dhe kullave) pa u arritur kurrë.
    void BuildMountains()
    {
        if (rocks.Count < 3) return;
        mountains = new GameObject("Malet_Larg").transform;
        for (int i = 0; i < 18; i++)
        {
            float a = i / 18f * 360f + Random.Range(-7f, 7f);
            float r = Random.Range(205f, 245f);
            var src = rocks[Random.value < 0.5f ? 0 : 2];
            var g = Instantiate(src.gameObject, mountains);
            g.name = "Mali";
            g.transform.localPosition = Quaternion.Euler(0, a, 0) * Vector3.forward * r + Vector3.up * Random.Range(-70f, -40f);
            g.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * src.localRotation;
            g.transform.localScale = src.localScale * Random.Range(4.5f, 7f);
            foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var rr in g.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Remap(g);
            // skaji i brendshëm i malit duhet të jetë përtej udhës së dukshme (viewDistance),
            // përndryshe udha në horizont duket sikur hyn brenda malit
            // të gjithë renderer-at e malit (jo vetëm i pari): skaji i brendshëm i vërtetë
            if (KaBounds(g.transform, out var b))
            {
                Vector2 c2 = new Vector2(b.center.x - mountains.position.x, b.center.z - mountains.position.z);
                float inner = c2.magnitude - Mathf.Max(b.extents.x, b.extents.z);
                float need = viewDistance + 15f;
                if (inner < need && c2.sqrMagnitude > 1f)
                {
                    Vector2 push = c2.normalized * (need - inner);
                    g.transform.localPosition += new Vector3(push.x, 0f, push.y);
                }
            }
        }
    }

    // shtyn objektin anash që të mos hyjë në shteg (skaji i brendshëm të paktën minX nga qendra)
    static void KeepAway(Transform t, Transform o, int side, float minX)
    {
        var r = o.GetComponent<Renderer>(); if (r == null) return;
        var b = r.bounds; float inner = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            inner = Mathf.Min(inner, t.InverseTransformPoint(c).x * side);
        }
        if (inner < minX) o.localPosition += new Vector3(side * (minX - inner), 0, 0);
    }

    Transform Place(Transform prefab, Transform parent, Vector3 localPos, float yaw, float scale)
    {
        var g = Instantiate(prefab.gameObject, parent);
        g.name = prefab.name;
        g.transform.localPosition = localPos;
        g.transform.localRotation = Quaternion.Euler(0, yaw, 0) * prefab.localRotation;
        g.transform.localScale = prefab.localScale * scale;
        foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);
        Remap(g);
        return g.transform;
    }

    // tema e botës: materialet e natyrës zëvendësohen me kopjet e ngjyrosura
    void Remap(GameObject g)
    {
        if (remap.Count == 0) return;
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < ms.Length; i++)
                if (ms[i] != null && remap.TryGetValue(ms[i], out var nm)) { ms[i] = nm; changed = true; }
            if (changed) r.sharedMaterials = ms;
        }
    }

    // kullë katrore guri që ngrihet nga retë, me brez të gdhendur dhe kapitel / çati
    void Tower(Transform t, Vector3 p, float w, float h, bool roofed)
    {
        Block(t, "Kulla", p + new Vector3(0, (h - 40f) / 2f, 0), new Vector3(w, h + 40f, w), wall, false);
        Block(t, "Kulla_Brezi", p + new Vector3(0, h - 0.9f, 0), new Vector3(w + 0.12f, 0.9f, w + 0.12f), carved, false);
        Block(t, "Kulla_Kapiteli", p + new Vector3(0, h + 0.15f, 0), new Vector3(w + 0.5f, 0.3f, w + 0.5f), deck, false);
        if (roofed) Pyramid(t, p + new Vector3(0, h + 0.3f, 0), w + 0.7f, w * 0.9f);
        else if (moss != null && Random.value < 0.5f)
            Block(t, "Kulla_Myshk", p + new Vector3(0, h * Random.Range(0.2f, 0.7f), 0), new Vector3(w + 0.06f, 0.6f, w + 0.06f), moss, false);
    }

    void Spire(Transform t, Vector3 p)
    {
        if (rocks.Count >= 4 && Random.value < 0.6f)
        {
            var big = Place(rocks[Random.value < 0.5f ? 0 : 2], t, p + new Vector3(0, Random.Range(-35f, -15f), 0), Random.Range(0f, 360f), Random.Range(2.5f, 4f));
            KeepAway(t, big, p.x < 0 ? -1 : 1, 16f);
            KeepBeforeTurn(t, big, p.x < 0 ? -1 : 1);   // para tempullit në majë, që ai të ndjekë shkëmbin
            var bb = big.GetComponent<Renderer>().bounds;
            Vector3 peak = t.InverseTransformPoint(new Vector3(bb.center.x, bb.max.y - 1.5f, bb.center.z));
            var tr = new GameObject("Tempulli_Maje").transform; tr.SetParent(t, false); tr.localPosition = peak;
            float w2 = Random.Range(5f, 8f);
            Block(tr, "Tempulli_Baza", new Vector3(0, 0.4f, 0), new Vector3(w2 + 1f, 0.8f, w2 + 1f), deck, false);
            Block(tr, "Tempulli_Muret", new Vector3(0, 0.8f + w2 * 0.35f, 0), new Vector3(w2, w2 * 0.7f, w2), wall, false);
            Block(tr, "Tempulli_Brezi", new Vector3(0, 0.8f + w2 * 0.62f, 0), new Vector3(w2 + 0.2f, w2 * 0.14f, w2 + 0.2f), carved, false);
            Pyramid(tr, new Vector3(0, 0.8f + w2 * 0.7f, 0), w2 + 0.8f, w2 * 0.6f);
            if (trees.Count > 0) Place(trees[Random.Range(0, trees.Count)], t, peak + new Vector3(w2 * 0.7f, 0, w2 * 0.3f), Random.Range(0f, 360f), 1.3f);
            return;
        }
        float top = Random.Range(4f, 30f);
        float w = Random.Range(9f, 18f);
        var root = new GameObject("Shkembi").transform;
        root.SetParent(t, false);
        root.localPosition = p;
        root.localRotation = Quaternion.Euler(0, Random.Range(0f, 90f), 0);
        float y = -70f;
        int n = Random.Range(3, 5);
        for (int i = 0; i < n; i++)
        {
            float hh = (top - y) / (n - i) * Random.Range(0.8f, 1.1f);
            if (i == n - 1) hh = top - y;
            float ww = w * (1f - 0.12f * i);
            var b = Block(root, "Shkembi_Pjese", new Vector3(Random.Range(-1f, 1f), y + hh / 2, Random.Range(-1f, 1f)), new Vector3(ww, hh, ww * Random.Range(0.8f, 1.1f)), rock, false);
            b.transform.localRotation = Quaternion.Euler(0, Random.Range(-12f, 12f), 0);
            y += hh;
        }
        // tempull i vogël në majë
        float tw = Mathf.Min(w * 0.55f, 8f);
        Block(root, "Tempulli_Baza", new Vector3(0, top + 0.4f, 0), new Vector3(tw + 1f, 0.8f, tw + 1f), deck, false);
        Block(root, "Tempulli_Muret", new Vector3(0, top + 0.8f + tw * 0.35f, 0), new Vector3(tw, tw * 0.7f, tw), wall, false);
        Block(root, "Tempulli_Brezi", new Vector3(0, top + 0.8f + tw * 0.62f, 0), new Vector3(tw + 0.2f, tw * 0.14f, tw + 0.2f), carved, false);
        Pyramid(root, new Vector3(0, top + 0.8f + tw * 0.7f, 0), tw + 0.8f, tw * 0.6f);
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        KeepBeforeTurn(t, root, p.x < 0 ? -1 : 1);
    }

    // Në segmentin e fundit para kthesës, udha e re kalon tërthor menjëherë pas fundit të segmentit (z ≈ L),
    // në anën e kthesës. Një shkëmb/mal i vendosur atje do të dilte mes udhës së re: e shtyjmë mbrapa
    // (vetëm në z, që të mbetet larg shtegut aktual) derisa i gjithë të jetë para keepOutZ.
    void KeepBeforeTurn(Transform t, Transform o, int side)
    {
        if (o == null || keepOutSide == 0 || side != keepOutSide) return;
        float maxZ = float.MinValue;
        foreach (var r in o.GetComponentsInChildren<Renderer>())
        {
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                maxZ = Mathf.Max(maxZ, t.InverseTransformPoint(c).z);
            }
        }
        if (maxZ > keepOutZ) o.localPosition -= new Vector3(0, 0, maxZ - keepOutZ);
    }

    // çati me dy ujëra (tjegulla)
    void Gable(Transform t, Vector3 basePos, float length, float height, float depth)
    {
        float half = depth / 2f + 0.3f;
        float slope = Mathf.Sqrt(half * half + height * height);
        float ang = Mathf.Atan2(height, half) * Mathf.Rad2Deg;
        for (int s = -1; s <= 1; s += 2)
        {
            var r = Block(t, "Cati", basePos + new Vector3(0, height / 2f, s * half / 2f), new Vector3(length, 0.18f, slope), roof, false);
            r.transform.localRotation = Quaternion.Euler(s * ang, 0, 0);
        }
        Block(t, "Cati_Kreshta", basePos + new Vector3(0, height, 0), new Vector3(length + 0.1f, 0.25f, 0.3f), carved, false);
    }

    void Pyramid(Transform t, Vector3 basePos, float w, float h)
    {
        var g = new GameObject("Cati_Piramide");
        g.transform.SetParent(t, false);
        g.transform.localPosition = basePos;
        g.transform.localScale = new Vector3(w, h, w);
        g.AddComponent<MeshFilter>().sharedMesh = PyramidMesh();
        g.AddComponent<MeshRenderer>().sharedMaterial = roof;
    }

    // ---------------- deti i reve ----------------
    void BuildClouds()
    {
        clouds = new GameObject("Deti_i_Reve").transform;
        var layers = new List<Material>();
        float[] ys = { -12f, -22f };
        for (int i = 0; i < ys.Length; i++)
        {
            if (cloud == null) break;
            var q = GameObject.CreatePrimitive(PrimitiveType.Plane);
            q.name = "Ret_" + i;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(clouds, false);
            q.transform.localPosition = new Vector3(0, ys[i], 0);
            q.transform.localScale = new Vector3(70, 1, 70);
            var m = new Material(cloud);
            m.SetTextureScale("_BaseMap", new Vector2(i == 0 ? 4.5f : 3f, i == 0 ? 4.5f : 3f));
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            layers.Add(m);
        }
        cloudLayers = layers.ToArray();
        if (cloudBase != null)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Plane);
            b.name = "Ret_Baza";
            Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(clouds, false);
            b.transform.localPosition = new Vector3(0, -34f, 0);
            b.transform.localScale = new Vector3(90, 1, 90);
            b.GetComponent<Renderer>().sharedMaterial = cloudBase;
            b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // ---------------- monedhat ----------------
    void Coins(Transform parent, float x, float z, int n, float y, float step, bool arc)
    {
        for (int i = 0; i < n; i++)
        {
            float yy = arc ? y + Mathf.Sin((i + 0.5f) / n * Mathf.PI) * 1.2f : y;
            GetCoin(parent, new Vector3(x, yy, z + i * step));
        }
    }

    // Bashkon të gjitha pjesët statike të një segmenti në pak mesh-e (një për material).
    // Kjo ul "draw calls" nga ~1700 në ~200: CPU-ja e lojës ishte pengesa kryesore e FPS-ve.
    // Collider-ët mbeten në objektet origjinale; monedhat (lëvizin) nuk bashkohen.
    readonly Dictionary<(Material, UnityEngine.Rendering.ShadowCastingMode), List<CombineInstance>> mergeGroups =
        new Dictionary<(Material, UnityEngine.Rendering.ShadowCastingMode), List<CombineInstance>>();
    readonly List<MeshFilter> mergeFilters = new List<MeshFilter>();

    List<Mesh> Merge(Transform root, List<Renderer> disabled = null)
    {
        using var _prof = mMerge.Auto();
        var result = new List<Mesh>();
        foreach (var l in mergeGroups.Values) l.Clear();
        root.GetComponentsInChildren(mergeFilters);
        Matrix4x4 w2l = root.worldToLocalMatrix;
        foreach (var mf in mergeFilters)
        {
            var mesh = mf.sharedMesh;
            if (mesh == null || mesh.subMeshCount != 1 || !mesh.isReadable) continue;
            var r = mf.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled || r.sharedMaterial == null) continue;
            if (mf.GetComponent<Coin>() != null) continue;
            // fuqitë dhe portali lëvizin (rrotullohen): nuk bashkohen as kur pjesa rindërtohet
            if (mf.GetComponentInParent<Fuqia>() != null || mf.GetComponentInParent<Portali>() != null) continue;
            var key = (r.sharedMaterial, r.shadowCastingMode);
            if (!mergeGroups.TryGetValue(key, out var list)) { list = new List<CombineInstance>(); mergeGroups[key] = list; }
            list.Add(new CombineInstance { mesh = mesh, transform = w2l * mf.transform.localToWorldMatrix });
            r.enabled = false;
            disabled?.Add(r);
        }
        foreach (var kv in mergeGroups)
        {
            if (kv.Value.Count == 0) continue;
            var m = new Mesh { name = "Bashkuar_" + kv.Key.Item1.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(kv.Value.ToArray(), true, true);
            m.UploadMeshData(true);   // vetëm për vizatim: liro kopjen në RAM (kolajderët janë te objektet origjinale)
            var g = new GameObject(m.name);
            g.transform.SetParent(root, false);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = kv.Key.Item1;
            mr.shadowCastingMode = kv.Key.Item2;
            result.Add(m);
        }
        return result;
    }

    // pishina e monedhave: monedhat nuk krijohen/fshihen vazhdimisht, por ripërdoren (më pak GC, pa ngrirje)
    readonly Stack<Coin> coinPool = new Stack<Coin>();
    Transform coinPoolRoot;

    void GetCoin(Transform parent, Vector3 localPos)
    {
        Coin c;
        if (coinPool.Count > 0) c = coinPool.Pop();
        else
        {
            var g = Prim(PrimitiveType.Cylinder, null, "Monedha", Vector3.zero, new Vector3(0.6f, 0.05f, 0.6f), gold, false);
            var sc = g.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.9f;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c = g.AddComponent<Coin>();
            c.onFinished = ReturnCoin;
        }
        c.transform.SetParent(parent, false);
        c.transform.localPosition = localPos;
        c.transform.localRotation = Quaternion.Euler(90, 0, 0);
        c.gameObject.SetActive(true);
    }

    void ReturnCoin(Coin c)
    {
        if (c == null || !c.gameObject.activeSelf) return;
        if (coinPoolRoot == null) coinPoolRoot = new GameObject("Pishina_Monedhave").transform;
        c.gameObject.SetActive(false);
        c.transform.SetParent(coinPoolRoot, false);
        coinPool.Push(c);
    }

    // ---------------- ndihmës: kuti me UV në metra (tekstura nuk shtrihet) ----------------
    static readonly Dictionary<Vector3, Mesh> boxCache = new Dictionary<Vector3, Mesh>();
    static Mesh pyramid;

    static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        if (collider) g.AddComponent<BoxCollider>().size = size;
        return g;
    }

    // copë me mesh të gatshëm (p.sh. nga ProBuilder); pivoti i meshit është poshtë në mes
    static GameObject MeshPiece(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 scale, Material mat)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    static Mesh BoxMesh(Vector3 s)
    {
        var key = new Vector3(Mathf.Round(s.x * 100f) / 100f, Mathf.Round(s.y * 100f) / 100f, Mathf.Round(s.z * 100f) / 100f);
        if (boxCache.TryGetValue(key, out var cached) && cached != null) return cached;
        Vector3 h = s * 0.5f;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        const float T = 4f; // 1 teksturë = 4 m
        void Face(Vector3 normal, Vector3 u, Vector3 w, float su, float sw)
        {
            int i0 = v.Count;
            Vector3 c = Vector3.Scale(normal, h);
            Vector3 hu = u * (su / 2f), hw = w * (sw / 2f);
            v.Add(c - hu - hw); v.Add(c + hu - hw); v.Add(c + hu + hw); v.Add(c - hu + hw);
            for (int k = 0; k < 4; k++) n.Add(normal);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(su / T, 0)); uv.Add(new Vector2(su / T, sw / T)); uv.Add(new Vector2(0, sw / T));
            tr.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
        }
        Face(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z);
        Face(Vector3.down, Vector3.right, Vector3.back, s.x, s.z);
        Face(Vector3.forward, Vector3.left, Vector3.up, s.x, s.y);
        Face(Vector3.back, Vector3.right, Vector3.up, s.x, s.y);
        Face(Vector3.right, Vector3.forward, Vector3.up, s.z, s.y);
        Face(Vector3.left, Vector3.back, Vector3.up, s.z, s.y);
        var m = new Mesh { name = "Kuti" };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
        m.RecalculateTangents(); m.RecalculateBounds();
        boxCache[key] = m;
        return m;
    }

    static Mesh PyramidMesh()
    {
        if (pyramid != null) return pyramid;
        Vector3 top = new Vector3(0, 1, 0);
        Vector3[] b = { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            int i0 = v.Count;
            v.Add(b[i]); v.Add(b[(i + 1) % 4]); v.Add(top);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0.5f, 1));
            tr.AddRange(new[] { i0, i0 + 2, i0 + 1 });
        }
        pyramid = new Mesh { name = "Piramida" };
        pyramid.SetVertices(v); pyramid.SetUVs(0, uv); pyramid.SetTriangles(tr, 0);
        pyramid.RecalculateNormals(); pyramid.RecalculateTangents(); pyramid.RecalculateBounds();
        return pyramid;
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool collider)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Destroy(g.GetComponent<Collider>());
        return g;
    }
}
