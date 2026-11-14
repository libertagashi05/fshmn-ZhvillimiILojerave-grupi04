using UnityEngine;

// Tempulli i Humbur - pishtar mbi shtyllat e bordurës: flakë (grimca) + dritë që dridhet.
// Krijohet nga TrackSpawner (Pishtari.Krijo). Drita fiket kur është larg kamerës (më pak kosto).
public class Pishtari : MonoBehaviour
{
    public Light drita;
    float baza, fara;
    static Transform kamera;

    // fuqia: shumëzues i dritës (p.sh. tempulli i natës ka pishtarë më të fortë)
    public static void Krijo(Transform parent, Vector3 localPos, Material flame, Material bowl, float fuqia = 1f)
    {
        var root = new GameObject("Pishtari").transform;
        root.SetParent(parent, false);
        root.localPosition = localPos;

        // tasi prej guri/bronzi
        var tas = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tas.name = "Pishtari_Tasi";
        Destroy(tas.GetComponent<Collider>());
        tas.transform.SetParent(root, false);
        tas.transform.localPosition = new Vector3(0, 0.08f, 0);
        tas.transform.localScale = new Vector3(0.42f, 0.08f, 0.42f);
        if (bowl != null) tas.GetComponent<MeshRenderer>().sharedMaterial = bowl;

        // flaka
        var fg = new GameObject("Flaka");
        fg.transform.SetParent(root, false);
        fg.transform.localPosition = new Vector3(0, 0.18f, 0);
        fg.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
        var ps = fg.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.35f, 1f), new Color(1f, 0.42f, 0.12f, 1f));
        main.gravityModifier = -0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var em = ps.emission; em.rateOverTime = 45f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 4f; sh.radius = 0.14f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.15f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f), new GradientColorKey(new Color(0.6f, 0.15f, 0.05f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.18f; noise.frequency = 1.5f; noise.scrollSpeed = 1.2f;
        var pr = fg.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = flame;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        ps.Play();

        // drita
        var lg = new GameObject("Drita");
        lg.transform.SetParent(root, false);
        lg.transform.localPosition = new Vector3(0, 0.6f, 0);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = 7.5f * Mathf.Lerp(1f, fuqia, 0.5f); l.intensity = 3.2f * fuqia;
        l.color = new Color(1f, 0.62f, 0.28f);
        l.shadows = LightShadows.None;
        var p = root.gameObject.AddComponent<Pishtari>();
        p.drita = l;
    }

    void Start()
    {
        baza = drita != null ? drita.intensity : 0f;
        fara = Random.value * 100f;
        if (kamera == null && Camera.main != null) kamera = Camera.main.transform;
    }

    void Update()
    {
        if (drita == null) return;
        if (kamera == null) { if (Camera.main != null) kamera = Camera.main.transform; else return; }
        // larg kamerës drita nuk shihet: fike
        bool afer = (kamera.position - transform.position).sqrMagnitude < 70f * 70f;
        if (drita.enabled != afer) drita.enabled = afer;
        if (!afer) return;
        float t = Time.time * 7f + fara;
        drita.intensity = baza * (0.78f + 0.3f * Mathf.PerlinNoise(t, fara) + 0.08f * Mathf.Sin(t * 3.1f));
    }
}
