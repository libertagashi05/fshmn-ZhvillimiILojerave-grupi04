using UnityEditor;

// Tempulli i Humbur - nis testin e animacioneve pa klikuar në menunë e lojës.
// Menuja: Tempulli > Testo animacionet (Play) -> hyn në Play me autopilot + "Testo animacionet"; shtyp Enter te menuja.
public static class TempulliTesti
{
    [MenuItem("Tempulli/Testo animacionet (Play)")]
    public static void TestPlay()
    {
        PlayerController.StressTest = true;
        PlayerController.Autopilot = true;
        EditorApplication.isPlaying = true;
    }

    // Testi i pamjes: Autopilot ~3 minuta në secilën botë (pa portale); çdo 0.2 s kontrollon që pengesa e ardhshme
    // të mos jetë e mbuluar nga mali/shkëmbinjtë/kullat. Rezultati: Console + Logs/tempulli_pamja_bllokuar.txt
    [MenuItem("Tempulli/Testo pamjen (Autopilot, 3 min në çdo botë)")]
    public static void TestoPamjen()
    {
        if (EditorApplication.isPlaying) { UnityEngine.Debug.LogWarning("Tempulli: ndalo Play para testit."); return; }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (System.IO.File.Exists("Assets/Scenes/Game.unity")) UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        UnityEngine.PlayerPrefs.SetInt(PamjaKontrolli.CelesiTestit, 1);
        UnityEngine.PlayerPrefs.Save();
        UnityEngine.Debug.Log("Tempulli: testi i pamjes nis (rreth " + (int)(PamjaKontrolli.KohaBota / 60f) + " min për çdo botë që ekziston).");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tempulli/Luaj normalisht (pa autopilot)")]
    public static void NormalPlay()
    {
        PlayerController.StressTest = false;
        PlayerController.Autopilot = false;
        EditorApplication.isPlaying = true;
    }
}
