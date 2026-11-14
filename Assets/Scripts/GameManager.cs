using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Menuja, pikët, pauza, Game Over dhe zërat.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public PlayerController player;
    public CameraFollow cam;
    public bool Playing => state == State.Playing;
    public bool HudVisible => state == State.Playing || state == State.Paused;

    enum State { Menu, Intro, Playing, Paused, Over }
    State state = State.Menu;
    static bool autoStart;
    int best;
    float overTime, stepTimer;
    SoundFX sound;
    IntroCinematic intro;
    GUIStyle title, big, label, hud, button, panelStyle, small, caption, hudMes, hudMesVogel;
    Texture2D panelTex, btnTex, btnHover, goldTex, paperTex, shadowTex, tapeTex;
    int hudScore = -1, hudDist = -1, hudCoins = -1;
    string hudScoreText = "", hudDistText = "", hudCoinsText = "";

    void Awake()
    {
        Instance = this;
        best = PlayerPrefs.GetInt("Rekordi", 0);
        SoundFX.Muted = PlayerPrefs.GetInt("PaZe", 0) == 1;
        sound = gameObject.AddComponent<SoundFX>();
        intro = gameObject.AddComponent<IntroCinematic>();
        // kontrolli i pamjes (punon vetëm me Autopilot; në lojën e zakonshme nuk bën asgjë)
        if (Application.isEditor || Debug.isDebugBuild) gameObject.AddComponent<PamjaKontrolli>();
        // Telefoni: 60 FPS (parazgjedhja atje është 30).
        // PC: pa kufi me "gjumë" - ritmin e jep VSync-u i cilësisë PC (i sinkronizuar me monitorin).
        // targetFrameRate = 60 pa VSync në Windows jepte frame të parregullta 17/33 ms (~52 FPS me dridhje),
        // ndërsa loja vetë ka nevojë vetëm për ~8 ms për frame (u mat: ~117 FPS pa kufi).
        Application.targetFrameRate = Application.isMobilePlatform ? 60 : -1;
    }

    void Start()
    {
        if (player != null) player.OnEvent += e =>
        {
            sound.Play(e == "stumble" ? "lane" : e == "smash" ? "boom" : e == "portal" ? "whoosh" : e);
            if (cam == null) return;
            if (e == "die") cam.Shake(0.35f);
            else if (e == "stumble") cam.Shake(0.2f);
            else if (e == "smash") cam.Shake(0.25f);
            else if (e == "land") cam.Land();
        };
        // "Luaj sërish" ose erdhi nga një portal: pa menu dhe pa hyrje, vrapi vazhdon menjëherë
        if (autoStart || Udhetimi.NeUdhetim)
        {
            if (Udhetimi.NeUdhetim) Debug.Log("Tempulli: arriti në " + Udhetimi.Botet[Udhetimi.BotaAktuale].emri + " (" + (int)Udhetimi.DistancaPara + " m deri tani)");
            autoStart = false;
            Udhetimi.NeUdhetim = false;
            StartGame();
        }
        DeathPhoto.Get();   // fotoja e vdekjes vizatohet që tani (jo në momentin e vdekjes: pa ngecje)
    }

    // "LUAJ" nga menuja: së pari hyrja kinematike (jo te "Luaj sërish" dhe jo me autopilot/teste)
    void BeginIntro()
    {
        if (state != State.Menu) return;
        if (PlayerController.Autopilot || player == null || cam == null) { StartGame(); return; }
        state = State.Intro;
        try { intro.Play(player, cam, sound, StartGame); }
        catch (System.Exception e) { Debug.LogException(e); IntroFailed(); }
    }

    // nëse hyrja s'mund të luhet, loja nis menjëherë (asnjëherë s'ngec në hyrje)
    void IntroFailed()
    {
        if (cam != null) cam.Hold = false;
        ArtaRig.LookOverride = false;
        sound.StopIntro(0.2f);
        StartGame();
    }

    void StartGame()
    {
        state = State.Playing;
        sound.SetMusic(true);
        if (PlayerController.Autopilot) Debug.Log("Tempulli: loja filloi me Autopilot (test)");
        if (PlayerController.StressTest && player != null && player.animator != null && player.animator.GetComponent<ArtaAnimProbe>() == null)
            player.animator.gameObject.AddComponent<ArtaAnimProbe>().player = player;
    }

    void Update()
    {
        bool confirm = false, pause = false, mute = false;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            confirm = k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame;
            pause = k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame;
            mute = k.mKey.wasPressedThisFrame;
        }
        var ts = Touchscreen.current;
        if ((state == State.Menu || state == State.Intro) && ts != null && ts.primaryTouch.press.wasPressedThisFrame) confirm = true;
#else
        confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        pause = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        mute = Input.GetKeyDown(KeyCode.M);
#endif
        // rruga rezervë (IMGUI), nëse Input System s'po merr tastet në këtë kompjuter
        confirm |= Tastiera.Shtypur(KeyCode.Return) || Tastiera.Shtypur(KeyCode.KeypadEnter) || Tastiera.Shtypur(KeyCode.Space);
        pause |= Tastiera.Shtypur(KeyCode.Escape) || Tastiera.Shtypur(KeyCode.P);
        mute |= Tastiera.Shtypur(KeyCode.M);
        if (mute) ToggleMute();
        switch (state)
        {
            case State.Menu: if (confirm) BeginIntro(); break;
            case State.Intro:
                if (!intro.Running) { IntroFailed(); break; }
                if (confirm || pause) intro.Skip();
                break;
            case State.Playing:
                if (pause) { state = State.Paused; Time.timeScale = 0; sound.SetMusic(false); }
                Steps();
                break;
            case State.Paused: if (pause || confirm) Resume(); break;
            case State.Over: if (confirm && Time.unscaledTime - overTime > 0.8f) Restart(true); break;
        }
    }

    void Steps()
    {
        if (player == null || player.IsDead || player.vagon || player.NeAkull) return;   // në vagon s'ka hapa
        if (!player.IsGrounded) return;   // pa GetComponent në çdo frame
        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0) { sound.Play("step"); stepTimer = 3.2f / Mathf.Max(player.Speed, 1f); }
    }

    void Resume() { state = State.Playing; Time.timeScale = 1; sound.SetMusic(true); ndalurNgaFokusi = false; }

    // Kur dritarja e lojës humbet fokusin (p.sh. kliko te VS Code ose te një dritare tjetër e Unity-t),
    // tastet nuk arrijnë më te loja dhe Arta do të vraponte pa kontroll. Prandaj loja ndalet vetë.
    // (Jo me Autopilot / testin e pamjes, që duhet të vazhdojnë edhe kur punon në një dritare tjetër.)
    bool ndalurNgaFokusi;
    void OnApplicationFocus(bool fokus)
    {
        if (!fokus) Tastiera.Liro();
        if (fokus || state != State.Playing || PlayerController.Autopilot || PamjaKontrolli.TestAktiv) return;
        state = State.Paused; Time.timeScale = 0; sound.SetMusic(false);
        ndalurNgaFokusi = true;
        Debug.Log("Tempulli: dritarja e lojës humbi fokusin - loja u ndal (tastet s'arrijnë te loja pa fokus).");
    }

    void ToggleMute()
    {
        SoundFX.Muted = !SoundFX.Muted;
        PlayerPrefs.SetInt("PaZe", SoundFX.Muted ? 1 : 0);
        sound.SetMusic(state == State.Playing);
    }

    public void Restart(bool play)
    {
        Time.timeScale = 1;
        autoStart = play;
        // vrap i ri: gjithmonë nga tempulli mbi re (bota e parë), me distancë dhe monedha nga zero
        Udhetimi.Reset();
        string skena = Udhetimi.Botet[0].skena;
        if (Application.CanStreamedLevelBeLoaded(skena)) SceneManager.LoadScene(skena);
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public int Score => player == null ? 0 : Mathf.FloorToInt(player.TotalDistance) + player.Coins * 10;

    public void GameOver()
    {
        state = State.Over;
        overTime = Time.unscaledTime;
        sound.SetMusic(false);
        if (Score > best) { best = Score; PlayerPrefs.SetInt("Rekordi", best); PlayerPrefs.Save(); }
    }

    // ---------- UI ----------
    static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

    void Styles(float s)
    {
        if (title == null)
        {
            panelTex = Tex(new Color(0.08f, 0.06f, 0.03f, 0.78f));
            btnTex = Tex(new Color(0.55f, 0.38f, 0.12f, 0.95f));
            btnHover = Tex(new Color(0.75f, 0.55f, 0.18f, 1f));
            goldTex = Tex(new Color(1f, 0.78f, 0.2f));
            title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            big = new GUIStyle(title);
            label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            small = new GUIStyle(label);
            hud = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            hudMes = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            hudMesVogel = new GUIStyle(hudMes);
            button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            button.normal.background = btnTex; button.hover.background = btnHover; button.active.background = btnHover;
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTex;
            paperTex = Tex(new Color(0.97f, 0.95f, 0.9f));
            shadowTex = Tex(new Color(0f, 0f, 0f, 0.45f));
            tapeTex = Tex(new Color(0.95f, 0.92f, 0.75f, 0.7f));
            caption = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.BoldAndItalic, wordWrap = false };
            caption.normal.textColor = new Color(0.2f, 0.14f, 0.1f);
        }
        caption.fontSize = Mathf.RoundToInt(19 * s);
        title.fontSize = Mathf.RoundToInt(50 * s);
        title.wordWrap = false;
        big.fontSize = Mathf.RoundToInt(40 * s);
        label.fontSize = Mathf.RoundToInt(22 * s);
        small.fontSize = Mathf.RoundToInt(15 * s);
        small.normal.textColor = new Color(1f, 0.7f, 0.6f);
        hud.fontSize = Mathf.RoundToInt(26 * s);
        hudMes.fontSize = Mathf.RoundToInt(32 * s);
        hudMesVogel.fontSize = Mathf.RoundToInt(19 * s);
        hudMes.normal.textColor = Color.white;
        hudMesVogel.normal.textColor = new Color(1f, 0.88f, 0.6f);
        button.fontSize = Mathf.RoundToInt(26 * s);
        Color gold = new Color(1f, 0.85f, 0.45f);
        title.normal.textColor = gold; big.normal.textColor = gold;
        label.normal.textColor = new Color(0.95f, 0.92f, 0.85f);
        hud.normal.textColor = Color.white;
        button.normal.textColor = button.hover.textColor = button.active.textColor = Color.white;
    }

    bool Button(Rect r, string text) => GUI.Button(r, text, button);

    // sfond gjysmë i tejdukshëm me cepa të rrumbullakët dhe vijë e hollë ari
    static void Sfond(Rect r, float s)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.32f), 0f, 14f * s);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 0.82f, 0.4f, 0.35f), 1.5f * s, 14f * s);
    }

    // tekst me hije, që lexohet mbi çdo sfond
    static void Hije(Rect r, string t, GUIStyle st, float s)
    {
        Color c = st.normal.textColor;
        st.normal.textColor = new Color(0f, 0f, 0f, 0.65f);
        GUI.Label(new Rect(r.x + 2f * s, r.y + 2f * s, r.width, r.height), t, st);
        st.normal.textColor = c;
        GUI.Label(r, t, st);
    }

    // fotoja polaroid e Artës (sytë X, gjuha jashtë) që bie butë mbi anën e majtë të panelit "TË KAPI!"
    void DrawDeathPhoto(Rect panel, float s)
    {
        float k = Mathf.Clamp01((Time.unscaledTime - overTime - 0.35f) / 0.55f);
        if (k <= 0f) return;
        float e = 1f - (1f - k) * (1f - k) * (1f - k);
        var photo = DeathPhoto.Get();
        float pw = 210 * s, inner = pw - 24 * s;
        float ih = inner * photo.height / Mathf.Max(1f, photo.width);
        float ph = 12 * s + ih + 46 * s;
        float x = Mathf.Max(8 * s, panel.x - pw * 0.72f);
        float y = panel.y + 20 * s - (1f - e) * 80 * s;
        var oldM = GUI.matrix; var oldC = GUI.color;
        GUIUtility.RotateAroundPivot(Mathf.Lerp(-30f, -8f, e), new Vector2(x + pw * 0.5f, y + ph * 0.5f));
        GUI.color = new Color(1f, 1f, 1f, e);
        GUI.DrawTexture(new Rect(x + 6 * s, y + 8 * s, pw, ph), shadowTex);
        GUI.DrawTexture(new Rect(x, y, pw, ph), paperTex);
        GUI.DrawTexture(new Rect(x + 12 * s, y + 12 * s, inner, ih), photo, ScaleMode.ScaleAndCrop);
        GUI.Label(new Rect(x, y + 12 * s + ih + 2 * s, pw, 42 * s), "Arta  †  " + Mathf.FloorToInt(player.TotalDistance) + " m", caption);
        GUI.DrawTexture(new Rect(x + pw * 0.5f - 34 * s, y - 10 * s, 68 * s, 22 * s), tapeTex);   // shiriti ngjitës
        GUI.matrix = oldM; GUI.color = oldC;
    }

    void OnGUI()
    {
        Tastiera.Ngjarje(Event.current);   // tastet nga IMGUI (rruga rezervë e tastierës)
        float s = Mathf.Min(Screen.height / 720f, Screen.width / 1280f) * 1.1f;
        Styles(s);
        float W = Screen.width, H = Screen.height;

        if (state == State.Playing || state == State.Paused)
        {
            // tekstet e HUD-it krijohen vetëm kur ndryshon vlera (OnGUI thirret disa herë në frame: pa mbeturina për GC)
            int sc = Score, dist = Mathf.FloorToInt(player.TotalDistance), coins = player.Coins;
            if (sc != hudScore) { hudScore = sc; hudScoreText = "Pikët  " + sc; }
            if (dist != hudDist) { hudDist = dist; hudDistText = dist + " m"; }
            if (coins != hudCoins) { hudCoins = coins; hudCoinsText = coins.ToString(); }
            // pikët në mes lart, mbi sfond gjysmë të tejdukshëm me cepa të rrumbullakët (udha shihet përmes tij)
            var pr = new Rect(W / 2 - 150 * s, 10 * s, 300 * s, 76 * s);
            Sfond(pr, s);
            Hije(new Rect(pr.x, pr.y + 4 * s, pr.width, 40 * s), hudScoreText, hudMes, s);
            Hije(new Rect(pr.x, pr.y + 44 * s, pr.width, 26 * s), hudDistText, hudMesVogel, s);
            // monedhat djathtas, me të njëjtin sfond
            Sfond(new Rect(W - 176 * s, 16 * s, 160 * s, 50 * s), s);
            GUI.DrawTexture(new Rect(W - 162 * s, 29 * s, 24 * s, 24 * s), goldTex);
            Hije(new Rect(W - 128 * s, 22 * s, 110 * s, 40 * s), hudCoinsText, hud, s);
        }

        Rect panel = new Rect(W / 2 - 300 * s, H / 2 - 230 * s, 600 * s, 460 * s);
        float bx = W / 2 - 150 * s, bw = 300 * s, bh = 56 * s;

        if (state == State.Menu)
        {
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panel.x, panel.y + 20 * s, panel.width, 80 * s), "TEMPULLI I HUMBUR", title);
            GUI.Label(new Rect(panel.x + 30 * s, panel.y + 105 * s, panel.width - 60 * s, 90 * s),
                "Vrapo nga rojtari prej guri dhe mblidh arin e tempullit!\n← →  anohu / kthehu     ↑  kërce     ↓  rrëshqit", label);
            GUI.Label(new Rect(panel.x, panel.y + 195 * s, panel.width, 40 * s), "Rekordi: " + best, label);
            if (Button(new Rect(bx, panel.y + 245 * s, bw, bh), "LUAJ")) BeginIntro();
            if (Button(new Rect(bx, panel.y + 310 * s, bw, bh), SoundFX.Muted ? "Zëri: JO" : "Zëri: PO")) ToggleMute();
            if (Button(new Rect(bx, panel.y + 375 * s, bw, bh), "DIL")) Application.Quit();
            PlayerController.Autopilot = GUI.Toggle(new Rect(panel.x + 20 * s, panel.y + panel.height - 30 * s, 260 * s, 26 * s), PlayerController.Autopilot, " Autopilot (test)");
            bool st = GUI.Toggle(new Rect(panel.x + 280 * s, panel.y + panel.height - 30 * s, 260 * s, 26 * s), PlayerController.StressTest, " Testo animacionet");
            if (st != PlayerController.StressTest) { PlayerController.StressTest = st; if (st) PlayerController.Autopilot = true; }
        }
        else if (state == State.Paused)
        {
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panel.x, panel.y + 40 * s, panel.width, 80 * s), "PAUZË", title);
            if (ndalurNgaFokusi)
                GUI.Label(new Rect(panel.x + 20 * s, panel.y + 112 * s, panel.width - 40 * s, 60 * s),
                    "Dritarja e lojës humbi fokusin, ndaj tastet shkonin diku tjetër.\nKliko VAZHDO (ose brenda lojës dhe shtyp Space).", small);
            if (Button(new Rect(bx, panel.y + 180 * s, bw, bh), "VAZHDO")) Resume();
            if (Button(new Rect(bx, panel.y + 250 * s, bw, bh), "FILLO SËRISH")) Restart(true);
            if (Button(new Rect(bx, panel.y + 320 * s, bw, bh), "MENUJA")) Restart(false);
        }
        else if (state == State.Over)
        {
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panel.x, panel.y + 20 * s, panel.width, 80 * s), "TË KAPI!", title);
            bool record = Score >= best && Score > 0;
            GUI.Label(new Rect(panel.x, panel.y + 105 * s, panel.width, 50 * s), "Pikët: " + Score + (record ? "   ★ REKORD I RI" : ""), big);
            GUI.Label(new Rect(panel.x, panel.y + 160 * s, panel.width, 70 * s),
                "Distanca: " + Mathf.FloorToInt(player.TotalDistance) + " m     Monedha: " + player.Coins + "\nRekordi: " + best, label);
            GUI.Label(new Rect(panel.x + 10 * s, panel.y + 222 * s, panel.width - 20 * s, 30 * s), player.DeathReason, small);
            if (Button(new Rect(bx, panel.y + 255 * s, bw, bh), "LUAJ SËRISH")) Restart(true);
            if (Button(new Rect(bx, panel.y + 325 * s, bw, bh), "MENUJA")) Restart(false);
            DrawDeathPhoto(panel, s);
        }
        else if (state == State.Playing && player.TotalDistance < 25f)
        {
            GUI.Label(new Rect(0, H * 0.62f, W, 60 * s), "Esc = pauzë     M = zëri", label);
        }
    }
}
