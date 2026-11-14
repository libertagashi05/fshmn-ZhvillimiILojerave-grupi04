using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tempulli i Humbur - animacionet e reja nga Mixamo (vagoni i varrit dhe teposhtja e akullit).
// Importon Mx_Vagon*.fbx dhe Mx_Akull.fbx dhe shton gjendjet "Vagoni", "Vagoni_Ulur" dhe "Akulli" te kontrolleri
// ekzistues i Artës, pa e rindërtuar skenën (kontrolleri mbetet i njëjti, skenat nuk prishen).
// Bëhet vetë një herë kur Unity kompilon, nëse mungojnë; ose nga menuja.
[InitializeOnLoad]
public static class TempulliAnimacionetReja
{
    const string Kontrolleri = "Assets/Animations/ArtaAnimator.controller";
    const string Celesi = "Tempulli_AnimacionetReja_1";

    static TempulliAnimacionetReja()
    {
        if (SessionState.GetBool(Celesi, false)) return;
        EditorApplication.delayCall += Prit;
    }

    static void Prit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;   // herën tjetër që kompilon
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Prit; return; }
        SessionState.SetBool(Celesi, true);
        if (!System.IO.File.Exists(TempulliMixamo.Dir + "Mx_VagonQendro.fbx") && !System.IO.File.Exists(TempulliMixamo.Dir + "Mx_Akull.fbx")) return;
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Kontrolleri);
        if (ctrl == null) return;
        bool mungon = true;
        foreach (var s in ctrl.layers[0].stateMachine.states)
            if (s.state.name == "Vagoni" || s.state.name == "Akulli") { mungon = false; break; }
        if (mungon) Shto();
    }

    [MenuItem("Tempulli/Animacionet e vagonit dhe akullit")]
    public static void Shto()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Tempulli: ndalo Play para se të shtosh animacionet."); return; }
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Kontrolleri);
        if (ctrl == null) { Debug.LogWarning("Tempulli: s'u gjet " + Kontrolleri + " (ndërto skenën e lojës më parë)."); return; }
        var klipet = TempulliMixamo.Import();
        bool ok = TempulliAnimacionet.ShtoGjendjetEReja(ctrl, klipet);
        AssetDatabase.SaveAssets();
        Debug.Log(ok ? "Tempulli: animacionet e vagonit dhe akullit u shtuan te Animator-i. Luaj: varri (vagoni) dhe mali i akullit (teposhtja)."
                     : "Tempulli: animacionet e vagonit/akullit ishin tashmë (ose mungojnë skedarët Mx_Vagon*/Mx_Akull te " + TempulliMixamo.Dir + ").");
    }
}
