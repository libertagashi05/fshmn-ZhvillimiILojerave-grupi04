using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

// Hyrja kinematike (~9.4 s) që luhet kur shtyp "LUAJ" te menuja:
//  0. e zezë: "Diku, thellë në xhunglat e lashta..."
//  1. kamera shikon qiellin mbi xhungël dhe ulet butë te shtegu i tempullit
//  2. titulli "TEMPULLI I HUMBUR" (shkronjat dalin një nga një, me shkëlqim ari)
//  3. afrim te Arta (sfondi turbullohet, Arta shikon kamerën)
//  4. fokusi kalon te rojtari prej guri që ngrihet nga toka - sytë ndizen, toka dridhet, Arta kthen kokën
//  5. kamera zbret pas Artës, saktësisht te pozicioni i vrapit - "VRAPO!" dhe loja fillon
// Me muzikë të vetën, pluhur ari në ajër dhe efekte kinemaje (thellësi fushe, vinjetë, kokërr filmi, ngjyra).
// Kalohet me Space / Enter / Esc / prekje. Shtohet vetë nga GameManager.
// Me Cinemachine: Brain-i fiket gjatë hyrjes dhe ndizet në fund (kamera përfundon aty ku është CM_Vrapi, pa kërcim).
[DefaultExecutionOrder(-50)]   // pas CameraFollow (-100), që pozicioni i vrapit të jetë i freskët, dhe para CinemachineBrain (0)
public class IntroCinematic : MonoBehaviour
{
    // pikat kyçe të kamerës (sekonda) dhe FOV-i i secilës (i fundit = FOV-i i vrapit)
    static readonly float[] K = { 0f, 3.0f, 5.4f, 7.6f, 9.4f };
    static readonly float[] Fov = { 58f, 50f, 36f, 44f, 0f };
    const float BlackHold = 1.3f, FadeIn = 1.4f;        // e zeza në fillim
    const float TitleIn = 2.3f, TitleOut = 5.2f;         // titulli
    const float RiseStart = 6.0f, RiseEnd = 7.5f;        // rojtari ngrihet nga toka
    const float RackFocus = 5.9f;                        // fokusi kalon nga Arta te rojtari
    const float TailTime = 1.1f;                         // "VRAPO!" shuhet pasi nis loja
    static float T => K[K.Length - 1];

    public bool Running { get; private set; }

    PlayerController player;
    CameraFollow follow;
    SoundFX sound;
    Camera cam;
    CinemachineCamera introCm;
    Chaser chaserC;
    Transform chaser;
    Vector3 chaserBase;
    Light glow;
    Material eyeMat;
    readonly List<Renderer> eyeRends = new List<Renderer>();
    System.Action onDone;
    float t, tail, shakeSeed;
    bool rumbled, whooshed, boomed;
    readonly Vector3[] pos = new Vector3[5], look = new Vector3[5];

    // efektet
    GameObject volGo;
    Volume vol;
    VolumeProfile profile;
    DepthOfField dof;
    ColorAdjustments col;
    ParticleSystem motes;

    public void Play(PlayerController p, CameraFollow f, SoundFX s, System.Action done)
    {
        player = p; follow = f; sound = s; onDone = done;
        cam = f.GetComponent<Camera>();
        // Cinemachine: Brain-i NUK fiket (rindezja e tij e linte kamerën të ngrirë);
        // hyrja ka kamerën e vet CM me përparësi më të lartë, që e lëvizim vetë.
        if (f.GetComponent<CinemachineBrain>() != null)
        {
            introCm = new GameObject("CM_Hyrja").AddComponent<CinemachineCamera>();
            if (f.cmRun != null) introCm.Lens = f.cmRun.Lens;
            introCm.Priority = 1000;
            introCm.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
        }
        follow.Hold = true;
        Debug.Log("Tempulli: hyrja nis" + (introCm != null ? " (Cinemachine)" : ""));

        chaserC = FindAnyObjectByType<Chaser>();
        if (chaserC != null)
        {
            chaser = chaserC.transform;
            chaserBase = chaser.position;
            chaser.position = chaserBase + Vector3.down * 2.0f;   // fillimisht i zhytur në tokë
            glow = new GameObject("Hyrja_DritaESyve").AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.32f, 0.08f);
            glow.range = 7f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;
            // sytë: kopje e materialit me shkëlqim (emission) që ndizet; në fund rikthehet origjinali
            if (chaserC.eyes != null)
            {
                eyeMat = new Material(chaserC.eyes);
                eyeMat.EnableKeyword("_EMISSION");
                eyeRends.Clear();
                foreach (var r in chaser.GetComponentsInChildren<Renderer>())
                    if (r.sharedMaterial == chaserC.eyes) { r.sharedMaterial = eyeMat; eyeRends.Add(r); }
            }
        }

        MakeEffects();
        MakeMotes();
        sound.PlayIntro();

        t = 0f; tail = 0f;
        rumbled = whooshed = boomed = false;
        shakeSeed = Random.value * 100f;
        Running = true;
    }

    public void Skip()
    {
        if (!Running || t >= T) return;
        t = T;
        sound.StopIntro(0.5f);
    }

    // ---------- kamera dhe skena ----------
    void LateUpdate()
    {
        if (!Running)
        {
            if (tail > 0f) tail -= Time.deltaTime;
            return;
        }
        // mbrojtje: nëse diçka dështon, hyrja mbaron menjëherë dhe loja nis (loja nuk ngec kurrë këtu)
        try { Step(); }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Finish();
        }
    }

    void Step()
    {
        t += Time.deltaTime;

        Vector3 p = player.transform.position;
        Vector3 f = player.Forward; f.y = 0; f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f);
        Vector3 guard = chaser != null ? chaserBase : p - f * 5f;
        Vector3 artaHead = p + Vector3.up * 1.55f;
        Vector3 guardHead = guard + Vector3.up * 2.3f;

        pos[0] = p + f * 46f + Vector3.up * 22f + r * 10f;  look[0] = p + f * 140f + Vector3.up * 30f;   // qielli mbi xhungël
        pos[1] = p + f * 22f + Vector3.up * 9f + r * 4.5f;  look[1] = p + f * 1f + Vector3.up * 1f;      // ulet te tempulli
        pos[2] = p + f * 4.2f + Vector3.up * 1.7f + r * 1.2f; look[2] = artaHead;                         // afër Artës
        pos[3] = p + r * 2.0f + Vector3.up * 1.6f + f * 0.5f; look[3] = guardHead;                       // rojtari
        pos[4] = follow.RunPos;                               look[4] = follow.RunLook;                  // pas Artës

        // koha: lehtësim i butë në fillim dhe në fund, pastaj segmentet sipas K[]
        float tn = Mathf.Clamp01(t / T);
        float eased = Mathf.Lerp(tn, tn - Mathf.Sin(2f * Mathf.PI * tn) / (2f * Mathf.PI), 0.7f);
        float tt = eased * T;
        int last = K.Length - 1;
        int i = 0;
        while (i < last - 1 && tt >= K[i + 1]) i++;
        float u = Mathf.Clamp01((tt - K[i]) / (K[i + 1] - K[i]));

        Vector3 camPos = Spline(pos, i, u);
        Vector3 camLook = Spline(look, i, u);
        float f0 = Fov[i], f1 = i + 1 == last ? follow.RunFov : Fov[i + 1];
        float fov = Mathf.Lerp(f0, f1, Mathf.SmoothStep(0f, 1f, u));

        // rojtari ngrihet nga toka, sytë ndizen
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RiseStart, RiseEnd, t));
        float eyes = Mathf.InverseLerp(RiseStart + 0.3f, RiseEnd, t) * (1f - Mathf.InverseLerp(T - 1.0f, T, t));
        if (chaser != null)
        {
            float jitter = rise > 0f && rise < 1f ? (Mathf.PerlinNoise(shakeSeed, t * 30f) - 0.5f) * 0.1f : 0f;
            chaser.position = chaserBase + Vector3.down * (2.0f * (1f - rise)) + r * jitter;
            float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(shakeSeed + 3f, t * 8f);
            glow.intensity = 6f * eyes * flicker;
            glow.transform.position = chaser.position + Vector3.up * 2.35f + chaser.forward * 0.6f;
            if (eyeMat != null) eyeMat.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.08f) * (eyes * flicker * 12f));
        }

        // Arta: shikon kamerën kur afrohemi, pastaj kthen kokën nga rojtari
        if (t > 4.3f && t < 6.0f) { ArtaRig.LookOverride = true; ArtaRig.LookOverridePos = camPos; }
        else if (t >= 6.3f && t < 8.6f) { ArtaRig.LookOverride = true; ArtaRig.LookOverridePos = guardHead + r * 3.5f; }
        else ArtaRig.LookOverride = false;

        // dridhja e tokës
        float shakeAmt = t > RiseStart && t < RiseEnd + 0.5f
            ? Mathf.Sin(Mathf.PI * Mathf.InverseLerp(RiseStart, RiseEnd + 0.5f, t)) * 0.12f : 0f;
        if (shakeAmt > 0f)
            camPos += new Vector3(Mathf.PerlinNoise(shakeSeed, t * 22f) - 0.5f, Mathf.PerlinNoise(shakeSeed + 9f, t * 22f) - 0.5f, 0f) * (shakeAmt * 2f);

        if (!rumbled && t >= RiseStart) { rumbled = true; sound.Play("rumble"); }
        if (!whooshed && t >= K[3] + 0.1f) { whooshed = true; sound.Play("whoosh"); }
        if (!boomed && t >= T - 0.55f) { boomed = true; sound.Play("boom"); }

        Vector3 dir = camLook - camPos;
        if (dir.sqrMagnitude > 0.0001f) SetCam(camPos, Quaternion.LookRotation(dir), fov);

        UpdateEffects(camPos, artaHead, guardHead, p, f);

        if (t >= T) Finish();
    }

    // me Cinemachine lëvizet kamera e hyrjes (CM_Hyrja); pa të, vetë kamera
    void SetCam(Vector3 position, Quaternion rotation, float fov)
    {
        if (introCm != null)
        {
            introCm.transform.SetPositionAndRotation(position, rotation);
            introCm.Lens.FieldOfView = fov;
        }
        else
        {
            cam.transform.SetPositionAndRotation(position, rotation);
            cam.fieldOfView = fov;
        }
    }

    void Finish()
    {
        if (!Running) return;
        Running = false;
        tail = TailTime;
        try { Cleanup(); }
        catch (System.Exception e) { Debug.LogException(e); }
        finally
        {
            follow.Hold = false;
            Debug.Log("Tempulli: hyrja mbaroi, loja nis.");
            onDone?.Invoke();   // loja nis gjithmonë, edhe nëse diçka dështoi më lart
        }
    }

    void Cleanup()
    {
        ArtaRig.LookOverride = false;
        if (chaser != null) chaser.position = chaserBase;
        if (glow != null) Destroy(glow.gameObject);
        if (eyeMat != null)
        {
            foreach (var r in eyeRends) if (r != null) r.sharedMaterial = chaserC.eyes;
            Destroy(eyeMat);
            eyeMat = null;
        }
        if (volGo != null) Destroy(volGo);
        if (profile != null)
        {
            foreach (var c in profile.components) Destroy(c);
            Destroy(profile);
        }
        if (motes != null)
        {
            motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(motes.gameObject, 4f);
            motes = null;
        }
        // kamera tani është saktësisht në pozicionin e vrapit: ia kthejmë kontrollin CameraFollow / Cinemachine
        Vector3 d = follow.RunLook - follow.RunPos;
        if (d.sqrMagnitude > 0.0001f) SetCam(follow.RunPos, Quaternion.LookRotation(d), follow.RunFov);
        if (introCm != null)
        {
            // Brain-i kalon butë te CM_Vrapi (janë në të njëjtin vend, pra s'ka kërcim)
            introCm.Priority = -1000;
            Destroy(introCm.gameObject, 2f);
            introCm = null;
        }
    }

    void OnDisable() => ArtaRig.LookOverride = false;

    // Catmull-Rom nëpër pikat kyçe (skajet zgjaten vetë)
    static Vector3 Spline(Vector3[] k, int i, float u)
    {
        Vector3 p1 = k[i], p2 = k[i + 1];
        Vector3 p0 = i > 0 ? k[i - 1] : 2f * p1 - p2;
        Vector3 p3 = i + 2 < k.Length ? k[i + 2] : 2f * p2 - p1;
        float u2 = u * u, u3 = u2 * u;
        return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
    }

    // ---------- efektet kinematike (Volume i përkohshëm mbi atë të lojës) ----------
    void MakeEffects()
    {
        volGo = new GameObject("Hyrja_Efektet");
        vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100f;
        vol.weight = 1f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();

        dof = profile.Add<DepthOfField>(true);
        if (Application.isMobilePlatform)
        {
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(1000f); dof.gaussianEnd.Override(1001f);
            dof.gaussianMaxRadius.Override(1.2f);
        }
        else
        {
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(10f);
            dof.aperture.Override(2.2f);
            dof.focalLength.Override(1f);
        }
        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.42f); vig.smoothness.Override(0.5f); vig.color.Override(Color.black);
        col = profile.Add<ColorAdjustments>(true);
        col.postExposure.Override(-0.7f); col.saturation.Override(-30f); col.contrast.Override(24f);
        col.colorFilter.Override(Color.white);
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.6f); bloom.threshold.Override(1.0f); bloom.scatter.Override(0.65f);
        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(0.2f); grain.response.Override(0.8f);
        vol.sharedProfile = profile;
    }

    void UpdateEffects(Vector3 camPos, Vector3 artaHead, Vector3 guardHead, Vector3 p, Vector3 f)
    {
        if (vol == null) return;
        // ngjyrat: nga e errët dhe pa ngjyra te e ngrohta; me rojtarin pak e kuqërremtë
        float warm = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BlackHold, TitleIn + 1.5f, t));
        float danger = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RiseStart - 0.2f, RiseEnd, t)) * (1f - Mathf.InverseLerp(T - 1.2f, T, t));
        col.postExposure.value = Mathf.Lerp(-0.7f, 0.1f, warm) - danger * 0.15f;
        col.saturation.value = Mathf.Lerp(-30f, 12f, warm) - danger * 8f;
        col.colorFilter.value = Color.Lerp(Color.Lerp(Color.white, new Color(1f, 0.95f, 0.86f), warm), new Color(1f, 0.86f, 0.8f), danger);
        // thellësia e fushës: vetëm në pamjet e afërta; fokusi kalon nga Arta te rojtari
        float dofAmt = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.6f, 4.8f, t)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(T - 1.4f, T - 0.4f, t)));
        float rack = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RackFocus, RackFocus + 0.9f, t));
        float focus = Mathf.Lerp(Vector3.Distance(camPos, artaHead), Vector3.Distance(camPos, guardHead), rack);
        if (dof.mode.value == DepthOfFieldMode.Bokeh)
        {
            dof.focusDistance.value = focus;
            dof.focalLength.value = Mathf.Lerp(1f, 70f, dofAmt);
        }
        else
        {
            dof.gaussianStart.value = Mathf.Lerp(1000f, focus + 1.5f, dofAmt);
            dof.gaussianEnd.value = Mathf.Lerp(1001f, focus + 12f, dofAmt);
        }
        // në fund efektet shuhen butë te pamja normale e lojës
        vol.weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(T - 0.9f, T, t));
        if (motes != null) motes.transform.position = p + f * 2f + Vector3.up * 2.2f;
    }

    // pluhur ari që noton në ajër (përdor materialin e shkëndijave të monedhave)
    void MakeMotes()
    {
        Material mat = null;
        if (player.sparkle != null) mat = player.sparkle.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        if (mat == null && player.dust != null) mat = player.dust.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        if (mat == null) return;

        var go = new GameObject("Hyrja_PluhuriIArte");
        go.transform.position = player.transform.position + Vector3.up * 2.2f;
        motes = go.AddComponent<ParticleSystem>();
        motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = motes.main;
        main.loop = true; main.prewarm = true; main.playOnAwake = false;
        main.duration = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.5f, 0.9f), new Color(1f, 0.7f, 0.3f, 0.6f));
        main.gravityModifier = -0.004f;
        main.maxParticles = 400;
        var em = motes.emission; em.rateOverTime = 55f;
        var sh = motes.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(10f, 4.5f, 16f);
        var noise = motes.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.3f; noise.scrollSpeed = 0.2f;
        var colt = motes.colorOverLifetime; colt.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colt.color = g;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        motes.Play();
    }

    // ---------- tekstet, titulli dhe shiritat ----------
    static Font serif;
    GUIStyle titleSt, textSt, bigSt, hintSt;
    Texture2D white;
    const string Title = "TEMPULLI I HUMBUR";
    GUIContent[] letters;
    float[] letterW;
    int letterSize = -1;
    static readonly Color Gold = new Color(1f, 0.82f, 0.42f), Shine = new Color(1f, 0.98f, 0.9f), Cream = new Color(0.95f, 0.92f, 0.85f);

    static float Window(float t, float a, float b, float fade) =>
        Mathf.Clamp01(Mathf.Min((t - a) / fade, (b - t) / fade));

    void InitGUI(float s)
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (serif == null)
            serif = Font.CreateDynamicFontFromOSFont(new[] { "Palatino Linotype", "Book Antiqua", "Georgia", "Cambria", "Times New Roman", "Noto Serif", "Droid Serif", "serif" }, 64);
        if (titleSt == null)
        {
            titleSt = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, font = serif };
            textSt = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, wordWrap = true, font = serif };
            bigSt = new GUIStyle(titleSt);
            hintSt = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight };
        }
        titleSt.fontSize = Mathf.RoundToInt(66 * s);
        textSt.fontSize = Mathf.RoundToInt(27 * s);
        hintSt.fontSize = Mathf.RoundToInt(15 * s);
        if (letterSize != titleSt.fontSize)
        {
            letterSize = titleSt.fontSize;
            letters = new GUIContent[Title.Length];
            letterW = new float[Title.Length];
            for (int i = 0; i < Title.Length; i++)
            {
                letters[i] = new GUIContent(Title[i].ToString());
                letterW[i] = Title[i] == ' ' ? letterSize * 0.32f : titleSt.CalcSize(letters[i]).x;
            }
        }
    }

    void OnGUI()
    {
        if (!Running && tail <= 0f) return;
        GUI.depth = -10;
        float W = Screen.width, H = Screen.height;
        float s = Mathf.Min(H / 720f, W / 1280f) * 1.1f;
        InitGUI(s);
        Color old = GUI.color;

        if (Running)
        {
            // shiritat e kinemasë (largohen në fund)
            float bar = H * 0.1f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(T - 0.9f, T, t)));
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, W, bar), white);
            GUI.DrawTexture(new Rect(0, H - bar, W, bar), white);

            DrawTitle(W * 0.5f, H * 0.2f, s);
            float sub = Window(t, TitleIn + 1.0f, TitleOut, 0.7f);
            Shadowed(new Rect(W * 0.12f, H * 0.2f + 105 * s - (1f - sub) * 8f * s, W * 0.76f, 50 * s),
                "Thellë në xhungël fle ari i një tempulli të harruar...", textSt, Cream, sub);

            float warn = Window(t, RiseStart + 0.1f, 8.3f, 0.5f);
            Vector2 jit = t < RiseEnd ? new Vector2(Mathf.PerlinNoise(shakeSeed, t * 25f) - 0.5f, Mathf.PerlinNoise(shakeSeed + 5f, t * 25f) - 0.5f) * 4f * s : Vector2.zero;
            Shadowed(new Rect(W * 0.12f + jit.x, H * 0.7f + jit.y, W * 0.76f, 50 * s),
                "...por rojtari prej guri është zgjuar!", textSt, new Color(1f, 0.58f, 0.42f), warn);

            DrawRun(Mathf.InverseLerp(T - 0.55f, T - 0.2f, t), 0f, s, W, H);

            if (t > 1.0f)
            {
                GUI.color = new Color(1, 1, 1, 0.5f * Mathf.Clamp01(t - 1f));
                GUI.Label(new Rect(0, 0, W - 18 * s, H - bar - 8 * s), "Space / Esc = kapërce", hintSt);
            }

            // e zeza në fillim, me fjalinë e parë
            float black = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BlackHold, BlackHold + FadeIn, t));
            if (black > 0f)
            {
                GUI.color = new Color(0, 0, 0, black);
                GUI.DrawTexture(new Rect(0, 0, W, H), white);
                Shadowed(new Rect(0, H * 0.45f, W, 60 * s), "Diku, thellë në xhunglat e lashta...", textSt, Cream, Window(t, 0.15f, BlackHold + 0.5f, 0.5f));
            }
        }
        else
        {
            float a = tail / TailTime;
            // blic i shkurtër ari kur nis loja
            float flash = Mathf.Clamp01((a - 0.75f) / 0.25f) * 0.35f;
            if (flash > 0f) { GUI.color = new Color(1f, 0.9f, 0.6f, flash); GUI.DrawTexture(new Rect(0, 0, W, H), white); }
            DrawRun(1f, 1f - a, s, W, H);
        }
        GUI.color = old;
    }

    // titulli: shkronjat dalin një nga një dhe afrohen, pastaj një shkëlqim ari kalon mbi to
    void DrawTitle(float cx, float y, float s)
    {
        float fadeOut = 1f - Mathf.InverseLerp(TitleOut - 0.6f, TitleOut, t);
        if (t < TitleIn || fadeOut <= 0f) return;
        float k = Mathf.InverseLerp(TitleIn, TitleIn + 2.2f, t);
        float spread = Mathf.Lerp(0.5f, 0.16f, 1f - (1f - k) * (1f - k)) * letterSize;
        float total = 0f;
        for (int i = 0; i < letterW.Length; i++) total += letterW[i];
        total += spread * (letterW.Length - 1);
        float x = cx - total * 0.5f;
        float shineX = Mathf.Lerp(x - letterSize * 2f, x + total + letterSize * 2f, Mathf.InverseLerp(TitleIn + 1.5f, TitleIn + 2.6f, t));
        float h = letterSize * 1.4f;
        for (int i = 0; i < letters.Length; i++)
        {
            float la = Mathf.SmoothStep(0f, 1f, (t - TitleIn - i * 0.06f) / 0.55f) * fadeOut;
            if (la > 0f && Title[i] != ' ')
            {
                float mid = x + letterW[i] * 0.5f;
                float sh = Mathf.Clamp01(1f - Mathf.Abs(mid - shineX) / (letterSize * 1.3f));
                float dy = (1f - la) * 14f * s;
                Shadowed(new Rect(x - 4f, y + dy, letterW[i] + 8f, h), letters[i], titleSt, Color.Lerp(Gold, Shine, sh), la);
            }
            x += letterW[i] + spread;
        }
        // zbukurimi: dy vija ari dhe një romb në mes
        float lineA = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TitleIn + 0.8f, TitleIn + 2.0f, t)) * fadeOut;
        if (lineA > 0f)
        {
            float ly = y + h + 6f * s, half = total * 0.42f * lineA, gapW = 16f * s;
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, lineA * 0.85f);
            GUI.DrawTexture(new Rect(cx - gapW - half, ly, half, 2f * s), white);
            GUI.DrawTexture(new Rect(cx + gapW, ly, half, 2f * s), white);
            var m = GUI.matrix;
            float d = 9f * s;
            GUIUtility.RotateAroundPivot(45f, new Vector2(cx, ly + s));
            GUI.DrawTexture(new Rect(cx - d * 0.5f, ly + s - d * 0.5f, d, d), white);
            GUI.matrix = m;
        }
    }

    // "VRAPO!": del me një "goditje" (zmadhim i shkurtër), pastaj shuhet
    void DrawRun(float appear, float fade, float s, float W, float H)
    {
        if (appear <= 0f) return;
        float a = Mathf.Clamp01(appear) * (1f - fade);
        if (a <= 0f) return;
        float pop = 1f + (1f - Mathf.Clamp01(appear)) * 0.35f + fade * 0.15f;
        bigSt.fontSize = Mathf.RoundToInt(104 * s * pop);
        Shadowed(new Rect(0, H * 0.3f, W, 150 * s * pop), new GUIContent("VRAPO!"), bigSt, Gold, a);
    }

    static void Shadowed(Rect r, string text, GUIStyle st, Color c, float a) => Shadowed(r, new GUIContent(text), st, c, a);

    static void Shadowed(Rect r, GUIContent text, GUIStyle st, Color c, float a)
    {
        if (a <= 0f) return;
        st.normal.textColor = Color.white;
        float o = Mathf.Max(2f, st.fontSize * 0.05f);
        GUI.color = new Color(0, 0, 0, a * 0.7f);
        GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, st);
        GUI.color = new Color(c.r, c.g, c.b, a);
        GUI.Label(r, text, st);
    }
}
