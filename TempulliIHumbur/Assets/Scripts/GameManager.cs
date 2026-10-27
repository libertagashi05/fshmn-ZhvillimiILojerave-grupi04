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

    enum State { Menu, Playing, Paused, Over }
    State state = State.Menu;
    static bool autoStart;
    int best;
    float overTime, stepTimer;
    SoundFX sound;
    GUIStyle title, big, label, hud, button, panelStyle, small;
    Texture2D panelTex, btnTex, btnHover, goldTex;

    void Awake()
    {
        Instance = this;
        best = PlayerPrefs.GetInt("Rekordi", 0);
        SoundFX.Muted = PlayerPrefs.GetInt("PaZe", 0) == 1;
        sound = gameObject.AddComponent<SoundFX>();
        // 60 FPS të qëndrueshme (sidomos në telefon, ku parazgjedhja është 30)
        Application.targetFrameRate = 60;
    }

    void Start()
    {
        if (player != null) player.OnEvent += e =>
        {
            sound.Play(e == "stumble" ? "lane" : e);
            if (cam == null) return;
            if (e == "die") cam.Shake(0.35f);
            else if (e == "stumble") cam.Shake(0.2f);
            else if (e == "land") cam.Land();
        };
        if (autoStart) { autoStart = false; StartGame(); }
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
        if (state == State.Menu && ts != null && ts.primaryTouch.press.wasPressedThisFrame) confirm = true;
#else
        confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        pause = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        mute = Input.GetKeyDown(KeyCode.M);
#endif
        if (mute) ToggleMute();
        switch (state)
        {
            case State.Menu: if (confirm) StartGame(); break;
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
        if (player == null || player.IsDead) return;
        var cc = player.GetComponent<CharacterController>();
        if (!cc.isGrounded) return;
        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0) { sound.Play("step"); stepTimer = 3.2f / Mathf.Max(player.Speed, 1f); }
    }

    void Resume() { state = State.Playing; Time.timeScale = 1; sound.SetMusic(true); }

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
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public int Score => player == null ? 0 : Mathf.FloorToInt(player.Distance) + player.Coins * 10;

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
            button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            button.normal.background = btnTex; button.hover.background = btnHover; button.active.background = btnHover;
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTex;
        }
        title.fontSize = Mathf.RoundToInt(50 * s);
        title.wordWrap = false;
        big.fontSize = Mathf.RoundToInt(40 * s);
        label.fontSize = Mathf.RoundToInt(22 * s);
        small.fontSize = Mathf.RoundToInt(15 * s);
        small.normal.textColor = new Color(1f, 0.7f, 0.6f);
        hud.fontSize = Mathf.RoundToInt(26 * s);
        button.fontSize = Mathf.RoundToInt(26 * s);
        Color gold = new Color(1f, 0.85f, 0.45f);
        title.normal.textColor = gold; big.normal.textColor = gold;
        label.normal.textColor = new Color(0.95f, 0.92f, 0.85f);
        hud.normal.textColor = Color.white;
        button.normal.textColor = button.hover.textColor = button.active.textColor = Color.white;
    }

    bool Button(Rect r, string text) => GUI.Button(r, text, button);

    void OnGUI()
    {
        float s = Mathf.Min(Screen.height / 720f, Screen.width / 1280f) * 1.1f;
        Styles(s);
        float W = Screen.width, H = Screen.height;

        if (state == State.Playing || state == State.Paused)
        {
            GUI.Box(new Rect(16 * s, 16 * s, 230 * s, 86 * s), GUIContent.none, panelStyle);
            GUI.Label(new Rect(30 * s, 22 * s, 220 * s, 40 * s), "Pikët  " + Score, hud);
            GUI.Label(new Rect(30 * s, 58 * s, 220 * s, 40 * s), Mathf.FloorToInt(player.Distance) + " m", hud);
            GUI.Box(new Rect(W - 176 * s, 16 * s, 160 * s, 50 * s), GUIContent.none, panelStyle);
            GUI.DrawTexture(new Rect(W - 162 * s, 29 * s, 24 * s, 24 * s), goldTex);
            GUI.Label(new Rect(W - 128 * s, 22 * s, 110 * s, 40 * s), player.Coins.ToString(), hud);
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
            if (Button(new Rect(bx, panel.y + 245 * s, bw, bh), "LUAJ")) StartGame();
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
                "Distanca: " + Mathf.FloorToInt(player.Distance) + " m     Monedha: " + player.Coins + "\nRekordi: " + best, label);
            GUI.Label(new Rect(panel.x + 10 * s, panel.y + 222 * s, panel.width - 20 * s, 30 * s), player.DeathReason, small);
            if (Button(new Rect(bx, panel.y + 255 * s, bw, bh), "LUAJ SËRISH")) Restart(true);
            if (Button(new Rect(bx, panel.y + 325 * s, bw, bh), "MENUJA")) Restart(false);
        }
        else if (state == State.Playing && player.Distance < 25f)
        {
            GUI.Label(new Rect(0, H * 0.62f, W, 60 * s), "Esc = pauzë     M = zëri", label);
        }
    }
}
