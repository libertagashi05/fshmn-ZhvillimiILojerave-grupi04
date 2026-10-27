using UnityEngine;

// Zërat e lojës të krijuar me kod (pa skedarë audio): monedha, kërcim, rrëshqitje,
// kthesë, hapa, rënie dhe muzikë me daulle xhungle.
public class SoundFX : MonoBehaviour
{
    const int SR = 44100;
    AudioSource sfx, music;
    AudioClip coin, jump, slide, turn, step, die, lane;
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
        music.clip = MakeMusic();
    }

    public void Play(string what)
    {
        if (Muted) return;
        AudioClip c = what switch
        {
            "coin" => coin, "jump" => jump, "slide" => slide, "turn" => turn,
            "step" => step, "die" => die, "lane" => lane, _ => null
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
