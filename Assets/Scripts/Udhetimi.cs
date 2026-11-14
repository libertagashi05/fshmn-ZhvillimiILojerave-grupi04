using UnityEngine;
using UnityEngine.SceneManagement;

// Udhëtimi mes botëve (portalet): ruan distancën, monedhat dhe shpejtësinë kur ngarkohet skena e re,
// që loja të vazhdojë pa u ndalur. Bota aktuale njihet nga emri i skenës (Game.unity nuk ka nevojë për ndryshime).
public static class Udhetimi
{
    public struct Bota
    {
        public string emri, skena;
        public Color ngjyra;   // ngjyra e vorbullës së portalit që të çon atje
    }

    public static readonly Bota[] Botet =
    {
        new Bota { emri = "Tempulli mbi re",  skena = "Game",          ngjyra = new Color(1f, 0.78f, 0.25f) },
        new Bota { emri = "Tempulli i natës", skena = "Bota_Nata",     ngjyra = new Color(0.62f, 0.3f, 1f) },
        new Bota { emri = "Mali i akullit",   skena = "Bota_Akulli",   ngjyra = new Color(0.35f, 0.85f, 1f) },
        new Bota { emri = "Vullkani",         skena = "Bota_Vullkani", ngjyra = new Color(1f, 0.45f, 0.1f) },
        new Bota { emri = "Varri i faraonit", skena = "Bota_Varri",    ngjyra = new Color(0.3f, 1f, 0.45f) },
    };

    public static bool NeUdhetim;       // skena u ngarkua nga një portal: loja vazhdon menjëherë (pa menu)
    public static float DistancaPara;   // metrat e vrapuar në botët e mëparshme
    public static int Monedhat;
    public static float Shpejtesia;
    public static int Portale;          // sa portale janë kaluar në këtë vrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Fillimi() => Reset();

    public static void Reset()
    {
        NeUdhetim = false;
        DistancaPara = 0f;
        Monedhat = 0;
        Shpejtesia = 0f;
        Portale = 0;
    }

    public static int BotaAktuale
    {
        get
        {
            string n = SceneManager.GetActiveScene().name;
            for (int i = 0; i < Botet.Length; i++) if (Botet[i].skena == n) return i;
            return 0;
        }
    }

    // vetëm skenat që janë në Build Settings (menuja "Tempulli > Ndërto botët" i shton)
    public static bool Ekziston(int i) => i >= 0 && i < Botet.Length && Application.CanStreamedLevelBeLoaded(Botet[i].skena);

    // bota e radhës: rastësisht, por kurrë e njëjta me atë ku je; -1 nëse s'ka asnjë tjetër
    public static int ZgjidhTjetren()
    {
        int cur = BotaAktuale, n = 0;
        var mundesite = new int[Botet.Length];
        for (int i = 0; i < Botet.Length; i++) if (i != cur && Ekziston(i)) mundesite[n++] = i;
        return n == 0 ? -1 : mundesite[Random.Range(0, n)];
    }
}
