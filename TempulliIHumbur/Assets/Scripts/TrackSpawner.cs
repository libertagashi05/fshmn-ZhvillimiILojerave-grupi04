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
    public float segmentLength = 24f;
    [Tooltip("Gjerësia e shtegut (m). Gjithçka tjetër (muret, lumi, kthesat, pengesat, korsitë) llogaritet prej saj.")]
    public float pathWidth = 6.6f;
    public float laneWidth = 2.0f;
    [Tooltip("Meshe të krijuara me ProBuilder (Tempulli > Ndërto skenën): harku i portës dhe kolona e rrumbullakët.")]
    public Mesh archMesh, columnMesh;
    public Material[] deckVariants;     // nuanca të ndryshme guri për çdo segment (nuk duken të gjitha njësoj)
    public float viewDistance = 200f;

    class Piece { public GameObject go; public float endDist; public List<Mesh> merged; }
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
    readonly List<Transform> rocks = new List<Transform>(), trees = new List<Transform>();
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
        BuildClouds();
        // korsitë dhe kufiri anësor i lojtarit ndjekin gjerësinë e shtegut
        laneWidth = Mathf.Min(laneWidth, pathWidth * 0.3f);
        if (player != null) { player.laneWidth = laneWidth; player.maxLateral = H - 0.6f; }
        deckCur = deck;
        if (nature != null)
            foreach (Transform c in nature.transform)
            {
                if (c.name.StartsWith("Shkembi")) rocks.Add(c);
                else if (c.name.EndsWith("_Trungu")) trees.Add(c);
                else if (c.name == "Shkurre") bush = c;
                else if (c.name == "Fieri") fern = c;
            }
        BuildMountains();
        while (trackDist < viewDistance) SpawnNext();
    }

    void Update()
    {
        if (player == null) return;
        // një pjesë e re dhe një e vjetër për frame: pa ngrirje kur krijohen shumë objekte njëherësh
        if (trackDist < player.Distance + viewDistance) SpawnNext();
        if (pieces.Count > 0 && pieces[0].endDist < player.Distance - 30f)
        {
            foreach (var c in pieces[0].go.GetComponentsInChildren<Coin>(true)) ReturnCoin(c);
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

    void SpawnNext()
    {
        untilTurn--;
        sinceSpecial++;
        if (untilTurn <= 0 && count > 4) { SpawnJunction(); untilTurn = Random.Range(5, 9); }
        else if (count > 7 && sinceSpecial > 3 && !afterTurn && untilTurn > 2 && Random.value < 0.35f)
        {
            float r = Random.value;
            if (r < 0.25f && trackDist > 350f) SpawnZipline();
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
        return root;
    }

    // ---------------- pjesët e shtegut ----------------
    void SpawnStraight(bool beforeTurn)
    {
        var t = NewRoot("Segmenti_" + count);
        float L = segmentLength;
        bool safe = count < 4;

        var rows = new List<(float z, int type)>();
        // pengesat: zakonisht një për segment (24 m), disa segmente bosh; pas 700 m ndonjëherë dy
        if (!safe && !afterTurn)
        {
            float empty = trackDist < 300f ? 0.35f : (trackDist < 900f ? 0.22f : 0.12f);
            if (Random.value > empty)
            {
                float z = Random.Range(L * 0.3f, L * 0.55f);
                rows.Add((z, Random.Range(0, 5)));
                if (!beforeTurn && trackDist > 700f && Random.value < 0.3f)
                    rows.Add((z + Random.Range(10f, 12f), Random.Range(0, 3)));
            }
        }
        afterTurn = false;
        float gapZ = -1;
        foreach (var r in rows) if (r.type == 3) gapZ = r.z;
        if (gapZ > 0)
        {
            Deck(t, 0, gapZ - 1.6f);
            Deck(t, gapZ + 1.6f, L);
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
        Scenery(t, L);
        Advance(t.gameObject, L);
    }

    void SpawnJunction()
    {
        var t = NewRoot("Kthesa_" + count);
        int turn = Random.value < 0.5f ? -1 : 1;
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
        Tower(t, new Vector3(-turn * (H + 0.8f), 0, wallZ - 0.05f), 1.5f, 7.5f, true);
        Tower(t, new Vector3(turn * (H + 0.8f), 0, wallZ - 0.05f), 1.5f, 7.5f, true);
        // shigjetat e arta në dysheme
        for (int i = 0; i < 3; i++)
            Block(t, "Shigjeta", new Vector3(turn * (i * 0.9f - 0.4f), 0.02f, 4f), new Vector3(0.5f, 0.04f, 1.6f - i * 0.45f), glow, false);
        Coins(t, 0, 0.5f, 3, 0.8f, 1.4f, false);

        junctions.Add(new Junction { center = center, newDir = side * turn, turn = turn });
        float endDist = trackDist + J - 0.8f;
        pieces.Add(new Piece { go = t.gameObject, endDist = endDist + 40f, merged = Merge(t) });

        dir = side * turn;
        cursor = center + dir * (J / 2f);
        afterTurn = true;
        trackDist = endDist;
    }

    // ---------------- gjysmë-shtegu: njëra anë është shembur, anohu në anën tjetër ----------------
    void SpawnHalf()
    {
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
    void SpawnRiver()
    {
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
    void SpawnZipline()
    {
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
        pieces.Add(new Piece { go = t.gameObject, endDist = trackDist, merged = Merge(t) });
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
        trackDist += L;
        cursor += dir * L;
        pieces.Add(new Piece { go = go, endDist = trackDist, merged = Merge(go.transform) });
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
        }
    }

    // këmbët e urës dhe harqet poshtë shtegut
    void Underside(Transform t, float L, float gapZ)
    {
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
        // rrënjë e trashë peme që kalon shtegun, me degëzime
        var log = Prim(PrimitiveType.Cylinder, t, "Rrenja_Trungu", new Vector3(0, 0.36f, z), new Vector3(0.62f, H + 0.2f, 0.62f), barkTex != null ? barkTex : bark, false);
        log.transform.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-6f, 6f), 90f);
        for (int i = 0; i < 4; i++)
        {
            float x = Random.Range(-H + 0.7f, H - 0.7f);
            var k = Prim(PrimitiveType.Cylinder, t, "Rrenja_Dege", new Vector3(x, 0.25f, z + Random.Range(-0.5f, 0.5f)), new Vector3(0.28f, 0.6f, 0.28f), bark, false);
            k.transform.localRotation = Quaternion.Euler(Random.Range(50f, 80f) * (Random.value < 0.5f ? 1 : -1), Random.Range(0f, 360f), 0);
        }
        if (moss != null && Random.value < 0.7f)
            Prim(PrimitiveType.Sphere, t, "Rrenja_Myshk", new Vector3(Random.Range(-2f, 2f), 0.62f, z), new Vector3(1.4f, 0.25f, 0.6f), moss, false);
    }

    void Gate(Transform t, float z)
    {
        // trau i gdhendur (rrëshqit poshtë tij) mbi dy shtylla guri
        var beam = Block(t, "Porta", new Vector3(0, 2.0f, z), new Vector3(Wd - 0.6f, 1.8f, 0.7f), carved, true);
        beam.AddComponent<Obstacle>();
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
        Block(t, "Blloku_Brezi", new Vector3(x, 1.9f, z - 0.62f), new Vector3(bw + 0.02f, 0.5f, 0.06f), carved, false);
        Block(t, "Blloku_Kapak", new Vector3(x, 2.47f, z), new Vector3(bw + 0.15f, 0.14f, 1.35f), deckCur, false);
    }

    // ---------------- pamja përreth ----------------
    void Scenery(Transform t, float L)
    {
        // portë e madhe tempulli me çati, çdo segment të tretë
        if (count % 3 == 0 && archMesh != null && (count / 3) % 2 == 1)
        {
            // portë me hark guri (ProBuilder): dy kulla të ulëta dhe harku mbi to
            float z = L * 0.5f, th = 4.2f;
            Tower(t, new Vector3(-(H + 1.1f), 0, z), 1.5f, th, false);
            Tower(t, new Vector3(H + 1.1f, 0, z), 1.5f, th, false);
            MeshPiece(t, "Harku", archMesh, new Vector3(0, th + 0.3f, z), Vector3.one, wall);
            float top = th + 0.3f + archMesh.bounds.size.y;
            Block(t, "Harku_Guri_Kyç", new Vector3(0, top - 0.45f, z), new Vector3(0.9f, 1.1f, 2.0f), carved, false);
        }
        else if (count % 3 == 0)
        {
            float z = L * 0.5f;
            Tower(t, new Vector3(-(H + 1.1f), 0, z), 1.5f, 7.2f, false);
            Tower(t, new Vector3(H + 1.1f, 0, z), 1.5f, 7.2f, false);
            Block(t, "Arkitrau", new Vector3(0, 7.7f, z), new Vector3(Wd + 4.0f, 1.2f, 1.8f), carved, false);
            Block(t, "Arkitrau_Kapak", new Vector3(0, 8.4f, z), new Vector3(Wd + 4.4f, 0.2f, 2.0f), deckCur, false);
            Gable(t, new Vector3(0, 8.5f, z), Wd + 4.6f, 2.6f, 2.4f);
        }
        else
        {
            for (int s = -1; s <= 1; s += 2)
                if (Random.value < 0.25f)
                    Tower(t, new Vector3(s * Random.Range(H + 2.1f, H + 4.9f), 0, Random.Range(3f, L - 3f)), Random.Range(1.4f, 2.4f), Random.Range(2.5f, 9f), Random.value < 0.5f);
        }
        Nature(t, L);
        // shkëmbinj-kulla në largësi me tempuj të vegjël në majë
        if (Random.value < 0.7f)
        {
            int s = Random.value < 0.5f ? -1 : 1;
            Spire(t, new Vector3(s * Random.Range(28f, 95f), 0, Random.Range(0f, L)));
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
        if (rocks.Count == 0) return;
        for (int s = -1; s <= 1; s += 2)
        {
            if (Random.value < 0.55f)
            {
                var r = Place(rocks[Random.Range(0, Mathf.Min(4, rocks.Count))], t,
                              new Vector3(s * Random.Range(H + 4.9f, H + 9.9f), Random.Range(-14f, -4f), Random.Range(2f, L - 2f)),
                              Random.Range(0f, 360f), Random.Range(0.9f, 1.5f));
                KeepAway(t, r, s, H + 2.4f);
                // pemë dhe shkurre mbi shkëmb
                var b = r.GetComponent<Renderer>().bounds;
                Vector3 top = t.InverseTransformPoint(new Vector3(b.center.x, b.max.y - 0.6f, b.center.z));
                float pick = Random.value;
                if (pick < 0.3f) Ruin(t, top);
                else if (trees.Count > 0 && pick < 0.85f)
                    Place(trees[Random.Range(0, trees.Count)], t, top, Random.Range(0f, 360f), Random.Range(0.8f, 1.3f));
                if (bush != null && Random.value < 0.7f)
                    Place(bush, t, top + new Vector3(Random.Range(-2f, 2f), -0.3f, Random.Range(-2f, 2f)), Random.Range(0f, 360f), Random.Range(1.2f, 2.2f));
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
            var g = Place(rocks[4], t, new Vector3(s * Random.Range(H + 2.9f, H + 4.9f), -2.2f, Random.Range(2f, L - 2f)), Random.Range(0f, 360f), Random.Range(0.6f, 1.2f));
            KeepAway(t, g, s, H + 1.1f);
        }
    }

    // rrënoja mbi shkëmb: kolona të thyera dhe një tra i rrëzuar
    void Ruin(Transform t, Vector3 p)
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
        return g.transform;
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

    List<Mesh> Merge(Transform root)
    {
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
            var key = (r.sharedMaterial, r.shadowCastingMode);
            if (!mergeGroups.TryGetValue(key, out var list)) { list = new List<CombineInstance>(); mergeGroups[key] = list; }
            list.Add(new CombineInstance { mesh = mesh, transform = w2l * mf.transform.localToWorldMatrix });
            r.enabled = false;
        }
        foreach (var kv in mergeGroups)
        {
            if (kv.Value.Count == 0) continue;
            var m = new Mesh { name = "Bashkuar_" + kv.Key.Item1.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(kv.Value.ToArray(), true, true);
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
