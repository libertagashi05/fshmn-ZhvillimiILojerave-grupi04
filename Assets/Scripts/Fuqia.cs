using UnityEngine;

public enum LlojiFuqise { Magnet, Mburoje, Vrap }

// Fuqia që shkëlqen mbi shteg (krijohet nga TrackSpawner në segmente pa pengesa). Kur Arta e prek, aktivizohet:
//  Magnet  - monedhat afër vijnë vetë te Arta
//  Mburojë - të shpëton një herë nga përplasja
//  Vrap    - vrap shumë i shpejtë që thyen pengesat dhe merr vetë kthesat
public class Fuqia : MonoBehaviour
{
    public LlojiFuqise lloji;
    Transform modeli;
    float fara;
    static Material[] mats;

    public static readonly Color[] Ngjyrat =
    {
        new Color(1f, 0.25f, 0.2f),    // magneti: i kuq
        new Color(0.3f, 0.75f, 1f),    // mburoja: e kaltër
        new Color(0.75f, 1f, 0.2f),    // vrapi: e verdhë-gjelbër
    };
    public static readonly string[] Emrat = { "MAGNET", "MBUROJË", "VRAP I SHPEJTË" };

    public static Fuqia Krijo(Transform parent, Vector3 localPos, LlojiFuqise lloji, Material baza)
    {
        var go = new GameObject("Fuqia_" + lloji);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var f = go.AddComponent<Fuqia>();
        f.lloji = lloji;
        f.Nderto(baza);
        return f;
    }

    static Material Mat(int i, Material baza)
    {
        if (mats == null) mats = new Material[4];
        if (mats[i] != null) return mats[i];
        var m = baza != null ? new Material(baza) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Color c = i < 3 ? Ngjyrat[i] : new Color(0.85f, 0.88f, 0.92f);
        m.name = "Fuqia_" + i;
        m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", i == 3 ? 1f : 0.2f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.8f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * (i == 3 ? 0.3f : 1.8f));   // HDR: shkëlqen me bloom
        mats[i] = m;
        return m;
    }

    void Nderto(Material baza)
    {
        fara = Random.value * 10f;
        var sc = gameObject.AddComponent<SphereCollider>();
        sc.isTrigger = true; sc.radius = 1.3f;
        modeli = new GameObject("Modeli").transform;
        modeli.SetParent(transform, false);
        int i = (int)lloji;
        var m = Mat(i, baza);
        var argjend = Mat(3, baza);
        switch (lloji)
        {
            case LlojiFuqise.Magnet:   // patkua: dy krahë dhe hark poshtë, me maja argjendi
                Pjese(PrimitiveType.Cube, new Vector3(-0.28f, 0.12f, 0), new Vector3(0.2f, 0.62f, 0.2f), m);
                Pjese(PrimitiveType.Cube, new Vector3(0.28f, 0.12f, 0), new Vector3(0.2f, 0.62f, 0.2f), m);
                Pjese(PrimitiveType.Cube, new Vector3(0, -0.24f, 0), new Vector3(0.76f, 0.2f, 0.2f), m);
                Pjese(PrimitiveType.Cube, new Vector3(-0.28f, 0.5f, 0), new Vector3(0.21f, 0.16f, 0.21f), argjend);
                Pjese(PrimitiveType.Cube, new Vector3(0.28f, 0.5f, 0), new Vector3(0.21f, 0.16f, 0.21f), argjend);
                break;
            case LlojiFuqise.Mburoje:  // mburojë e rrumbullakët me buzë argjendi dhe kokë në mes
                Pjese(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.95f, 0.05f, 0.95f), argjend, new Vector3(90, 0, 0));
                Pjese(PrimitiveType.Cylinder, new Vector3(0, 0, -0.04f), new Vector3(0.8f, 0.05f, 0.8f), m, new Vector3(90, 0, 0));
                Pjese(PrimitiveType.Sphere, new Vector3(0, 0, -0.1f), new Vector3(0.28f, 0.28f, 0.16f), argjend);
                break;
            case LlojiFuqise.Vrap:     // dy shigjeta lart (^^)
                for (int k = 0; k < 2; k++)
                {
                    float y = -0.2f + k * 0.38f;
                    Pjese(PrimitiveType.Cube, new Vector3(-0.17f, y, 0), new Vector3(0.48f, 0.15f, 0.15f), m, new Vector3(0, 0, 40));
                    Pjese(PrimitiveType.Cube, new Vector3(0.17f, y, 0), new Vector3(0.48f, 0.15f, 0.15f), m, new Vector3(0, 0, -40));
                }
                break;
        }
        // drita e vogël rreth fuqisë
        var l = new GameObject("Drita").AddComponent<Light>();
        l.transform.SetParent(transform, false);
        l.type = LightType.Point; l.color = Ngjyrat[i]; l.range = 4.5f; l.intensity = 2.5f; l.shadows = LightShadows.None;
    }

    void Pjese(PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3 rot = default)
    {
        var g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(modeli, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(rot);
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        float t = Time.time + fara;
        modeli.localRotation = Quaternion.Euler(0, t * 140f, 0);
        modeli.localPosition = new Vector3(0, Mathf.Sin(t * 3f) * 0.15f, 0);
    }

    public void Merre(PlayerController p)
    {
        if (p == null || p.IsDead || p.Fuqite == null) return;
        p.Fuqite.Aktivizo(lloji);
        if (p.sparkle) { p.sparkle.transform.position = transform.position; p.sparkle.Emit(25); }
        Destroy(gameObject);
    }
}
