using UnityEngine;

// Zërat e lojës të krijuar me kod (pa skedarë audio): monedha, kërcim, rrëshqitje,
// kthesë, hapa, rënie dhe muzikë me daulle xhungle.
public class SoundFX : MonoBehaviour
{
    const int SR = 44100;
    AudioSource sfx, music, introSrc;
    float introFade;
    AudioClip coin, jump, slide, turn, step, die, lane, rumble, whoosh, boom, fuqi, tik;
    public static bool Muted;

    void Awake()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.volume = 0.35f;
        coin = Make("coin", 0.25f, (t, i) => Env(t, 0.004f, 0.2f) * (t < 0.07f ? Sin(1318, t) : Sin(1760, t)) * 0.5f);
        jump = Make("jump", 0.18f, (t, i) => Env(t, 0.005f, 0.16f) * Sin(260 + 900 * t, t) * 0.35f);
        lane = Make("lane", 0.12f, (t, i) => Env(t, 0.003f, 0.1f) * Noise() * 0.18f * (1 - t * 6));
        slide = Make("slide", 0.35f, (t, i) => Env(t, 0.01f, 0.3f) * LowNoise(0.15f) * 0.6f);
        turn = Make("turn", 0.25f, (t, i) => Env(t, 0.02f, 0.2f) * LowNoise(0.35f) * 0.5f);
        step = Make("step", 0.06f, (t, i) => Env(t, 0.002f, 0.05f) * (Sin(90, t) * 0.6f + LowNoise(0.2f) * 0.4f) * 0.35f);
        die = Make("die", 0.9f, (t, i) => Env(t, 0.005f, 0.85f) * (Sin(110 - 70 * t, t) * 0.7f + LowNoise(0.1f) * 0.5f) * 0.8f);
        // hyrja: gjëmimi i tokës kur zgjohet rojtari, fëshfërima e kamerës, daullja e "VRAPO!"
        rumble = Make("rumble", 2.0f, (t, i) => Env(t, 0.3f, 1.6f) * (LowNoise(0.03f) * 6f + Sin(36 + 4 * Mathf.Sin(9 * t), t) * 0.45f) * 0.8f);
        whoosh = Make("whoosh", 0.8f, (t, i) => Mathf.Sin(Mathf.PI * t / 0.8f) * LowNoise(0.05f + 0.4f * t) * 2.2f);
        boom = Make("boom", 0.7f, (t, i) => Env(t, 0.003f, 0.6f) * (Sin(75 - 35 * t, t) * 0.9f + LowNoise(0.3f) * 0.6f));
        // fuqia: arpezh që ngjitet (Do-Mi-Sol-Do)
        fuqi = Make("fuqi", 0.5f, (t, i) =>
        {
            float f = t < 0.08f ? 523.25f : t < 0.16f ? 659.25f : t < 0.24f ? 783.99f : 1046.5f;
            return Env(t, 0.005f, 0.45f) * (Sin(f, t) + 0.35f * Sin(f * 2f, t)) * 0.4f;
        });
        // fuqia po mbaron: "tik" i lehtë si ora (çdo sekondë në 3 të fundit)
        tik = Make("tik", 0.06f, (t, i) => Env(t, 0.001f, 0.035f) * (Sin(2400f, t) * 0.6f + Sin(3700f, t) * 0.25f + Noise() * 0.15f) * 0.32f);
        music.clip = MakeMusic();
        introSrc = gameObject.AddComponent<AudioSource>();
        introSrc.playOnAwake = false;
        introSrc.clip = MakeIntroMusic();
    }

    // muzika e hyrjes (IntroCinematic): ndalet butë kur kapërcehet
    public void PlayIntro()
    {
        if (Muted) return;
        introFade = 0f;
        introSrc.volume = 0.85f;
        introSrc.Play();
    }

    public void StopIntro(float fadeTime = 0.6f) { if (introSrc.isPlaying) introFade = Mathf.Max(0.01f, fadeTime); }

    void Update()
    {
        if (introFade <= 0f) return;
        introSrc.volume = Mathf.MoveTowards(introSrc.volume, 0f, Time.unscaledDeltaTime * 0.85f / introFade);
        if (introSrc.volume <= 0f) { introSrc.Stop(); introFade = 0f; }
    }

    // ~10.5 s: dron + akorde (La minor -> Fa -> Mi), kambana te titulli, rrahje zemre kur zgjohet rojtari, ngritje para "VRAPO!"
    static AudioClip MakeIntroMusic()
    {
        const float dur = 10.5f;
        int n = Mathf.CeilToInt(dur * SR);
        var data = new float[n];
        float[][] chords = { new[] { 220f, 261.63f, 329.63f }, new[] { 174.61f, 220f, 261.63f }, new[] { 207.65f, 246.94f, 329.63f } };
        float[] chordStart = { 0f, 5.3f, 7.5f, 99f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float swell = Mathf.SmoothStep(0f, 1f, t / 2.8f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 9.4f) / 1.1f));
            float v = (Sin(55f, t) * 0.55f + Sin(82.41f, t) * 0.25f + Sin(110f, t) * 0.15f) * swell * 0.5f;
            for (int c = 0; c < 3; c++)
            {
                float w = Mathf.Clamp01((t - chordStart[c] + 0.4f) / 0.8f) * Mathf.Clamp01((chordStart[c + 1] + 0.4f - t) / 0.8f);
                if (w <= 0f) continue;
                float pad = 0f;
                foreach (float f in chords[c])
                    pad += Sin(f, t) + Sin(f * 1.004f, t) * 0.8f + Sin(f * 2f, t) * 0.15f;
                v += pad * w * swell * 0.07f * (0.85f + 0.15f * Mathf.Sin(t * 3.1f));
            }
            data[i] = v;
        }
        // kambana kur shfaqet titulli
        float[] bell = { 880f, 1046.5f, 1318.5f, 1760f, 1318.5f, 1046.5f };
        for (int b = 0; b < 12; b++)
        {
            float f = bell[b % bell.Length];
            Add(data, 2.3f + b * 0.24f, 1.2f, (t) => (Sin(f, t) + 0.3f * Sin(f * 2.76f, t) * Mathf.Exp(-t * 8f)) * Env(t, 0.002f, 1.0f) * 0.09f);
        }
        // rrahjet e zemrës: gjithnjë e më shpejt
        float tb = 6.0f, gap = 0.8f;
        while (tb < 8.9f)
        {
            Add(data, tb, 0.4f, (t) => Sin(58f - 22f * t, t) * Env(t, 0.003f, 0.35f) * 0.75f);
            if (gap > 0.45f) Add(data, tb + 0.19f, 0.3f, (t) => Sin(52f - 18f * t, t) * Env(t, 0.003f, 0.25f) * 0.45f);
            tb += gap; gap = Mathf.Max(0.22f, gap * 0.84f);
        }
        // ngritja (zhurmë që hapet) para "VRAPO!"
        Add(data, 7.6f, 1.3f, (t) => LowNoise(0.03f + t * 0.35f) * Mathf.Pow(t / 1.3f, 2f) * 1.6f);
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.9f;
        var clip = AudioClip.Create("hyrja", n, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }

    public void Play(string what)
    {
        if (Muted) return;
        AudioClip c = what switch
        {
            "coin" => coin, "jump" => jump, "slide" => slide, "turn" => turn,
            "step" => step, "die" => die, "lane" => lane,
            "rumble" => rumble, "whoosh" => whoosh, "boom" => boom, "fuqi" => fuqi, "tik" => tik, _ => null
        };
        if (c != null) sfx.PlayOneShot(c, what == "step" ? 0.5f : 1f);
    }

    public void SetMusic(bool on)
    {
        if (on && !Muted) { if (!music.isPlaying) music.Play(); }
        else music.Stop();
    }

    // ---------- sinteza ----------
    static float lp;
    static float Sin(float f, float t) => Mathf.Sin(2 * Mathf.PI * f * t);
    static float Noise() => Random.value * 2f - 1f;
    static float LowNoise(float k) { lp += (Noise() - lp) * k; return lp; }
    static float Env(float t, float a, float d) => t < a ? t / a : Mathf.Exp(-(t - a) / (d * 0.35f));

    static AudioClip Make(string name, float dur, System.Func<float, int, float> f)
    {
        int n = Mathf.CeilToInt(dur * SR);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)SR, i), -1, 1);
        var c = AudioClip.Create(name, n, 1, SR, false);
        c.SetData(data, 0);
        return c;
    }

    static AudioClip MakeMusic()
    {
        float bpm = 104f, beat = 60f / bpm;
        int bars = 8;
        float dur = bars * 4 * beat;
        int n = Mathf.CeilToInt(dur * SR);
        var data = new float[n];
        var rnd = new System.Random(7);
        float[] scale = { 220f, 261.63f, 293.66f, 329.63f, 392f, 440f, 523.25f };
        int[] bassLine = { 0, 0, 3, 2 };

        for (int b = 0; b < bars * 4; b++)
        {
            float t0 = b * beat;
            // daulle e thellë në 1 dhe 3, tom në 2.5 dhe 4
            if (b % 2 == 0) Add(data, t0, 0.35f, (t) => Sin(70 - 30 * t, t) * Env(t, 0.003f, 0.3f) * 0.9f);
            if (b % 4 == 1) Add(data, t0 + beat * 0.5f, 0.25f, (t) => Sin(140 - 40 * t, t) * Env(t, 0.003f, 0.2f) * 0.5f);
            if (b % 4 == 3) { Add(data, t0, 0.25f, (t) => Sin(160 - 50 * t, t) * Env(t, 0.003f, 0.2f) * 0.5f);
                              Add(data, t0 + beat * 0.5f, 0.25f, (t) => Sin(120 - 40 * t, t) * Env(t, 0.003f, 0.2f) * 0.5f); }
            // shkundës në tetësha
            for (int h = 0; h < 2; h++)
                Add(data, t0 + h * beat * 0.5f, 0.06f, (t) => (Noise() - LowNoise(0.5f)) * Env(t, 0.002f, 0.05f) * (h == 1 ? 0.18f : 0.1f));
            // bas
            if (b % 4 == 0)
            {
                float f = scale[bassLine[(b / 4) % 4]] / 4f;
                Add(data, t0, beat * 3.8f, (t) => (Sin(f, t) + 0.3f * Sin(f * 2, t)) * Env(t, 0.02f, beat * 3.5f) * 0.35f);
            }
            // marimba (pentatonike), nga bari i tretë
            if (b >= 8)
                for (int h = 0; h < 2; h++)
                    if (rnd.NextDouble() < 0.55)
                    {
                        float f = scale[rnd.Next(scale.Length)];
                        Add(data, t0 + h * beat * 0.5f, 0.5f, (t) => (Sin(f, t) + 0.25f * Sin(f * 4, t) * Mathf.Exp(-t * 30)) * Env(t, 0.002f, 0.4f) * 0.22f);
                    }
        }
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i] * 0.7f, -1, 1);
        var c = AudioClip.Create("muzika", n, 1, SR, false);
        c.SetData(data, 0);
        return c;
    }

    static void Add(float[] data, float start, float dur, System.Func<float, float> f)
    {
        int s = Mathf.FloorToInt(start * SR), len = Mathf.CeilToInt(dur * SR);
        for (int i = 0; i < len && s + i < data.Length; i++) data[s + i] += f(i / (float)SR);
    }
}
