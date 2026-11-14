using UnityEngine;

public enum LlojiUjit { Uje, Akull, Lave }
public enum LlojiGrimcave { Asnje, Xixellonja, Bore, Shkendija }

// Tema e një bote: mjegulla, ambienti, qielli, dielli/hëna, efektet e kamerës, ngjyrat e materialeve dhe grimcat.
// E përdorin të dy: Editor-i (TempulliSetup.BuildScene e "pjek" në skenë qiellin, dritën, mjegullën dhe Volume-in)
// dhe loja (BotaAplikuesi ngjyros materialet, ndërron ujin dhe shton grimcat kur nis skena).
// Vlerat e paracaktuara = Tempulli mbi re (Game.unity), saktësisht si më parë.
[CreateAssetMenu(menuName = "Tempulli/Tema e botës", fileName = "Tema_Bota")]
public class BotaTema : ScriptableObject
{
    public string emri = "Tempulli mbi re";

    [Header("Mjegulla dhe drita e ambientit")]
    public Color mjegulla = new Color(0.82f, 0.80f, 0.79f);
    public float mjegullaFillim = 90f, mjegullaFund = 320f;
    public Color ambientQielli = new Color(0.60f, 0.66f, 0.82f);
    public Color ambientMesi = new Color(0.86f, 0.74f, 0.62f);
    public Color ambientToka = new Color(0.46f, 0.42f, 0.42f);

    [Header("Qielli (Skybox/Procedural)")]
    public Color qielliNgjyra = new Color(0.42f, 0.55f, 0.80f);
    public Color qielliToka = new Color(0.95f, 0.86f, 0.78f);
    public float qielliEkspozimi = 1.0f;
    public float atmosfera = 1.05f;
    public float madhesiaDiellit = 0.06f;

    [Header("Dielli / hëna")]
    public Vector3 dielliKendi = new Vector3(32f, 205f, 0f);
    public Color dielliNgjyra = new Color(1f, 0.90f, 0.78f);
    public float dielliFuqia = 2.3f;
    public float hijet = 0.85f;
    public bool shkelqimLente = true;

    [Header("Efektet e kamerës (Volume)")]
    public float bloom = 0.3f;
    public float bloomPragu = 1.25f;
    public float vinjeta = 0.25f;
    public float temperatura = -8f;
    public float kontrasti = 18f;
    public float ngopja = 10f;
    public float ekspozimi = 0f;
    public Color filtri = Color.white;

    [Header("Materialet (gjatë lojës)")]
    [Tooltip("Ngjyra e gurit: shumëzohet me ngjyrën e materialit; 'zbardhja' e afron drejt kësaj ngjyre.")]
    public Color guri = Color.white;
    [Range(0, 1)] public float guriZbardhje = 0f;
    [Tooltip("Shkëlqim i lehtë i shtuar mbi gurin (p.sh. akulli i zbehtë).")]
    public Color guriShkelqim = Color.black;
    public Color bimet = Color.white;
    public Color ret = Color.white;
    public LlojiUjit uji = LlojiUjit.Uje;
    public Color ujiNgjyra = new Color(0.22f, 0.48f, 0.52f);

    [Header("Grimcat në ajër")]
    public LlojiGrimcave grimcat = LlojiGrimcave.Asnje;
    [ColorUsage(true, true)] public Color grimcatNgjyra = Color.white;
    public float grimcatSasia = 60f;

    [Header("Pishtarët")]
    [Tooltip("Pishtar në çdo shtyllë të N-të të bordurës (3 = si në tempullin e ditës).")]
    public int pishtarCdo = 3;
    public float pishtarDrita = 1f;

    [Header("Pengesat (botët e errëta)")]
    [Tooltip("Shirita që shkëlqejnë te pengesat: buza e traut, maja e bllokut, rrënja, buzët e gropës. E zezë = pa theks.")]
    [ColorUsage(false, true)] public Color pengesaTheks = Color.black;

    [Header("Shtegu")]
    [Tooltip("Teposhte akulli: rampa ku Arta rrëshqet vetë dhe më shpejt (bota e akullit).")]
    public bool teposhteAkulli = false;
    [Tooltip("Bota është brenda (varr): mure dhe tavan, pa re dhe pa male.")]
    public bool brenda = false;
    [Tooltip("Arta udhëton me vagon mbi shina: anohu (A/D) dhe ulu (S), pa kërcime.")]
    public bool vagon = false;
}
