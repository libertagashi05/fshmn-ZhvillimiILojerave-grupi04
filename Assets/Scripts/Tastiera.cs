using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Rruga rezervë për tastierën: lexon tastet edhe nga ngjarjet IMGUI (Event.current te OnGUI i GameManager-it).
// Në disa kompjutera / versione të Editor-it, Input System nuk merr asnjë tast edhe kur dritarja e lojës ka fokusin.
// Atëherë loja përdor këtë. Sapo Input System jep qoftë edhe një tast, rruga rezervë fiket (që të mos numërohet dy herë).
public static class Tastiera
{
    static readonly Dictionary<KeyCode, int> shtypurNe = new Dictionary<KeyCode, int>();   // tasti -> frame-i i OnGUI ku u shtyp
    static readonly HashSet<KeyCode> mbajtur = new HashSet<KeyCode>();
    static bool inputSystemPunon, rezervaLogu;
    static int kontrolluarNe = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Fillimi() { shtypurNe.Clear(); mbajtur.Clear(); inputSystemPunon = false; rezervaLogu = false; kontrolluarNe = -1; }

    // thirret nga GameManager.OnGUI për çdo ngjarje
    public static void Ngjarje(Event e)
    {
        if (e == null || e.keyCode == KeyCode.None) return;
        if (e.type == EventType.KeyDown)
        {
            if (!mbajtur.Contains(e.keyCode)) shtypurNe[e.keyCode] = Time.frameCount;   // përsëritjet e tastit të mbajtur s'numërohen
            mbajtur.Add(e.keyCode);
            if (!inputSystemPunon && !rezervaLogu)
            {
                rezervaLogu = true;
                Debug.LogWarning("Tempulli: Input System s'po merr tastet - përdoret rruga rezervë (IMGUI). Tastiera punon.");
            }
        }
        else if (e.type == EventType.KeyUp) mbajtur.Remove(e.keyCode);
    }

    public static void Liro() => mbajtur.Clear();   // kur dritarja humbet fokusin

    // a duhet përdorur rruga rezervë? (vetëm nëse Input System s'ka dhënë asnjë tast deri tani)
    public static bool Rezerve
    {
        get
        {
            if (kontrolluarNe != Time.frameCount)
            {
                kontrolluarNe = Time.frameCount;
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (!inputSystemPunon && k != null && k.anyKey.wasPressedThisFrame)
                {
                    inputSystemPunon = true;
                    Debug.Log("Tempulli: tastiera po lexohet nga Input System (OK).");
                }
#endif
            }
            return !inputSystemPunon;
        }
    }

    // u shtyp në frame-in e kaluar (OnGUI vjen pas Update-it, ndaj lexohet në Update-in e radhës)
    public static bool Shtypur(KeyCode k) => Rezerve && shtypurNe.TryGetValue(k, out int f) && f >= Time.frameCount - 1 && f < Time.frameCount;
    public static bool Mbajtur(KeyCode k) => Rezerve && mbajtur.Contains(k);
}
