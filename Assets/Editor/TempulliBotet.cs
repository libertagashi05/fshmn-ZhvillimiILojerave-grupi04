using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tempulli i Humbur - botët e portaleve.
// Menuja: Tempulli > Ndërto botët  ->  Bota_Nata.unity, Bota_Akulli.unity, Bota_Vullkani.unity (+ Build Settings)
// Secila ndërtohet me TempulliSetup.BuildScene dhe temën e vet (Assets/Botet/Tema_*.asset).
// Temat krijohen vetëm herën e parë: pastaj mund t'i ndryshosh në Inspector dhe ta ndërtosh sërish botën.
// Game.unity (tempulli mbi re) nuk preket.
[InitializeOnLoad]
public static class TempulliBotet
{
    const string Dosja = "Assets/Botet";
    static readonly string[] Skenat = { "Bota_Nata", "Bota_Akulli", "Bota_Vullkani", "Bota_Varri" };

    // Si Game.unity te TempulliSetup: nëse mungon ndonjë botë (p.sh. pas klonimit nga GitHub), ndërtohen vetë
    // një herë kur Unity hapet ose kompilon. Pa to, Udhetimi.ZgjidhTjetren() kthen -1 dhe portali nuk del fare.
    static TempulliBotet()
    {
        if (SessionState.GetBool("Tempulli_BotetAuto_Varri", false)) return;
        foreach (var s in Skenat)
            if (!System.IO.File.Exists("Assets/Scenes/" + s + ".unity"))
            {
                EditorApplication.delayCall += PritDheNderto;
                return;
            }
    }

    static void PritDheNderto()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // në Play: ndërtohen sapo të dalësh nga Play
            EditorApplication.playModeStateChanged -= PasPlay;
            EditorApplication.playModeStateChanged += PasPlay;
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += PritDheNderto; return; }
        SessionState.SetBool("Tempulli_BotetAuto_Varri", true);   // vetëm një herë për seancë, edhe nëse anulohet
        Debug.Log("Tempulli: mungojnë botët e portaleve, po ndërtohen vetë (Tempulli > Ndërto botët)...");
        Nderto(true);   // vetëm ato që mungojnë
    }

    static void PasPlay(PlayModeStateChange s)
    {
        if (s != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= PasPlay;
        EditorApplication.delayCall += PritDheNderto;
    }

    [MenuItem("Tempulli/Ndërto botët")]
    public static void NdertoBotet() => Nderto(false);

    // vetemMungojne: ndërto vetëm skenat që s'ekzistojnë (ndërtimi automatik); menuja i rindërton të gjitha
    static void Nderto(bool vetemMungojne)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Tempulli: ndalo Play para se të ndërtosh botët."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder(Dosja)) AssetDatabase.CreateFolder("Assets", "Botet");

        var botet = new (string skena, string tema, System.Action<BotaTema> vlerat)[]
        {
            ("Bota_Nata", "Tema_Nata", Nata),
            ("Bota_Akulli", "Tema_Akulli", Akulli),
            ("Bota_Vullkani", "Tema_Vullkani", Vullkani),
            ("Bota_Varri", "Tema_Varri", Varri),
        };
        try
        {
            for (int i = 0; i < botet.Length; i++)
            {
                if (vetemMungojne && System.IO.File.Exists("Assets/Scenes/" + botet[i].skena + ".unity")) continue;
                EditorUtility.DisplayProgressBar("Tempulli: botët", "Po ndërtohet " + botet[i].skena + "...", i / (float)botet.Length);
                var tema = Tema(botet[i].tema, botet[i].vlerat);
                TempulliSetup.BuildScene(tema, "Assets/Scenes/" + botet[i].skena + ".unity");
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        // kthehu te skena kryesore
        if (System.IO.File.Exists("Assets/Scenes/Game.unity")) EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        Debug.Log("Tempulli: botët u ndërtuan dhe u shtuan te Build Settings. Hap Game.unity dhe luaj: pas ~500 m del portali.");
    }

    // tema ekzistuese ruhet (ndryshimet e tua në Inspector nuk humbin); e reja merr vlerat më poshtë
    static BotaTema Tema(string emri, System.Action<BotaTema> vlerat)
    {
        string p = Dosja + "/" + emri + ".asset";
        var t = AssetDatabase.LoadAssetAtPath<BotaTema>(p);
        if (t != null) return t;
        t = ScriptableObject.CreateInstance<BotaTema>();
        vlerat(t);
        AssetDatabase.CreateAsset(t, p);
        AssetDatabase.SaveAssets();
        return t;
    }

    // 2. Tempulli i natës: hënë, qiell vjollcë i errët, mjegull blu, pishtarë më të shumtë e më të fortë, xixëllonja
    static void Nata(BotaTema t)
    {
        t.pengesaTheks = new Color(2.6f, 1.9f, 0.8f);   // ari i ndritshëm mbi natën blu
        t.emri = "Tempulli i natës";
        t.mjegulla = new Color(0.10f, 0.11f, 0.24f); t.mjegullaFillim = 45f; t.mjegullaFund = 230f;
        t.ambientQielli = new Color(0.20f, 0.22f, 0.45f); t.ambientMesi = new Color(0.17f, 0.15f, 0.32f); t.ambientToka = new Color(0.07f, 0.06f, 0.12f);
        t.qielliNgjyra = new Color(0.16f, 0.08f, 0.32f); t.qielliToka = new Color(0.06f, 0.05f, 0.12f);
        t.qielliEkspozimi = 0.4f; t.atmosfera = 0.55f; t.madhesiaDiellit = 0.035f;   // "dielli" i qiellit = hëna
        t.dielliKendi = new Vector3(38f, 160f, 0f); t.dielliNgjyra = new Color(0.62f, 0.72f, 1f); t.dielliFuqia = 0.75f;
        t.hijet = 0.7f; t.shkelqimLente = false;
        t.bloom = 0.85f; t.bloomPragu = 0.9f; t.vinjeta = 0.38f; t.temperatura = -18f; t.kontrasti = 22f; t.ngopja = 0f;
        t.ekspozimi = 0.35f; t.filtri = new Color(0.88f, 0.92f, 1.1f);
        t.guri = new Color(0.72f, 0.74f, 0.98f); t.bimet = new Color(0.55f, 0.68f, 0.85f); t.ret = new Color(0.22f, 0.22f, 0.42f);
        t.uji = LlojiUjit.Uje;
        t.grimcat = LlojiGrimcave.Xixellonja; t.grimcatNgjyra = new Color(2.6f, 2.4f, 0.6f, 1f); t.grimcatSasia = 45f;
        t.pishtarCdo = 2; t.pishtarDrita = 1.8f;
    }

    // 3. Mali i akullit: gur i bardhë/blu i zbehtë, borë, mjegull e bardhë e dendur, uji akull, dritë e ftohtë
    static void Akulli(BotaTema t)
    {
        t.emri = "Mali i akullit";
        t.teposhteAkulli = true;   // rampa akulli ku Arta rrëshqet vetë
        t.mjegulla = new Color(0.86f, 0.91f, 0.97f); t.mjegullaFillim = 25f; t.mjegullaFund = 170f;
        t.ambientQielli = new Color(0.72f, 0.82f, 0.98f); t.ambientMesi = new Color(0.76f, 0.83f, 0.93f); t.ambientToka = new Color(0.58f, 0.64f, 0.76f);
        t.qielliNgjyra = new Color(0.6f, 0.74f, 0.95f); t.qielliToka = new Color(0.9f, 0.94f, 1f);
        t.qielliEkspozimi = 1.15f; t.atmosfera = 0.7f; t.madhesiaDiellit = 0.05f;
        t.dielliKendi = new Vector3(24f, 200f, 0f); t.dielliNgjyra = new Color(0.86f, 0.93f, 1f); t.dielliFuqia = 1.8f;
        t.hijet = 0.6f; t.shkelqimLente = true;
        t.bloom = 0.35f; t.bloomPragu = 1.1f; t.vinjeta = 0.2f; t.temperatura = -28f; t.kontrasti = 12f; t.ngopja = -25f;
        t.ekspozimi = 0.15f; t.filtri = new Color(0.93f, 0.98f, 1.06f);
        t.guri = new Color(0.92f, 0.98f, 1.1f); t.guriZbardhje = 0.35f; t.guriShkelqim = new Color(0.12f, 0.16f, 0.24f);
        t.bimet = new Color(0.85f, 0.92f, 1f); t.ret = new Color(1f, 1.05f, 1.15f);
        t.uji = LlojiUjit.Akull; t.ujiNgjyra = new Color(0.7f, 0.88f, 1f);
        t.grimcat = LlojiGrimcave.Bore; t.grimcatNgjyra = new Color(1f, 1f, 1f, 0.9f); t.grimcatSasia = 260f;
        t.pishtarCdo = 3; t.pishtarDrita = 0.8f;
    }

    // 5. Varri i faraonit: nën tokë, mure me hieroglife, pishtarë dhe ar; Arta udhëton me vagon mbi shina
    static void Varri(BotaTema t)
    {
        t.emri = "Varri i faraonit";
        t.brenda = true; t.vagon = true;
        t.mjegulla = new Color(0.07f, 0.05f, 0.03f); t.mjegullaFillim = 18f; t.mjegullaFund = 150f;
        t.ambientQielli = new Color(0.32f, 0.25f, 0.16f); t.ambientMesi = new Color(0.28f, 0.2f, 0.12f); t.ambientToka = new Color(0.12f, 0.09f, 0.05f);
        t.qielliNgjyra = new Color(0.04f, 0.03f, 0.02f); t.qielliToka = new Color(0.04f, 0.03f, 0.02f);
        t.qielliEkspozimi = 0.15f; t.atmosfera = 0.4f; t.madhesiaDiellit = 0f;
        t.dielliKendi = new Vector3(60f, 30f, 0f); t.dielliNgjyra = new Color(1f, 0.75f, 0.45f); t.dielliFuqia = 0.35f;
        t.hijet = 0.5f; t.shkelqimLente = false;
        t.bloom = 0.7f; t.bloomPragu = 0.95f; t.vinjeta = 0.42f; t.temperatura = 14f; t.kontrasti = 22f; t.ngopja = 8f;
        t.ekspozimi = 0.15f; t.filtri = new Color(1f, 0.95f, 0.85f);
        t.guri = new Color(1.08f, 0.9f, 0.66f); t.guriZbardhje = 0.06f; t.guriShkelqim = Color.black;
        t.bimet = new Color(0.55f, 0.48f, 0.3f); t.ret = new Color(0.2f, 0.15f, 0.1f);
        t.uji = LlojiUjit.Uje;
        t.grimcat = LlojiGrimcave.Bore; t.grimcatNgjyra = new Color(0.85f, 0.72f, 0.5f); t.grimcatSasia = 35f;   // pluhur rëre nga tavani
        t.pishtarCdo = 4; t.pishtarDrita = 2.2f;
        t.pengesaTheks = new Color(2.4f, 1.7f, 0.6f);
    }

    // 4. Vullkani: qiell i kuq-portokalli me tym, lavë në vend të ujit, shkëndija që ngrihen, gur i errët, dritë portokalli
    static void Vullkani(BotaTema t)
    {
        t.pengesaTheks = new Color(1.2f, 2.2f, 2.8f);   // e bardhë-kaltër, kundër portokallisë së lavës
        t.emri = "Vullkani";
        t.mjegulla = new Color(0.32f, 0.13f, 0.08f); t.mjegullaFillim = 40f; t.mjegullaFund = 220f;
        t.ambientQielli = new Color(0.55f, 0.24f, 0.15f); t.ambientMesi = new Color(0.48f, 0.2f, 0.12f); t.ambientToka = new Color(0.25f, 0.08f, 0.04f);
        t.qielliNgjyra = new Color(0.65f, 0.16f, 0.05f); t.qielliToka = new Color(0.22f, 0.07f, 0.04f);
        t.qielliEkspozimi = 0.85f; t.atmosfera = 3f; t.madhesiaDiellit = 0.09f;
        t.dielliKendi = new Vector3(18f, 220f, 0f); t.dielliNgjyra = new Color(1f, 0.52f, 0.26f); t.dielliFuqia = 1.7f;
        t.hijet = 0.85f; t.shkelqimLente = true;
        t.bloom = 0.75f; t.bloomPragu = 0.95f; t.vinjeta = 0.35f; t.temperatura = 22f; t.kontrasti = 26f; t.ngopja = 6f;
        t.ekspozimi = 0f; t.filtri = new Color(1.05f, 0.9f, 0.85f);
        t.guri = new Color(0.42f, 0.36f, 0.36f); t.guriZbardhje = 0.1f;
        t.bimet = new Color(0.32f, 0.22f, 0.16f); t.ret = new Color(0.55f, 0.3f, 0.26f);
        t.uji = LlojiUjit.Lave; t.ujiNgjyra = new Color(1f, 0.38f, 0.06f);
        t.grimcat = LlojiGrimcave.Shkendija; t.grimcatNgjyra = new Color(3f, 1.2f, 0.3f, 1f); t.grimcatSasia = 90f;
        t.pishtarCdo = 3; t.pishtarDrita = 1.2f;
    }
}
