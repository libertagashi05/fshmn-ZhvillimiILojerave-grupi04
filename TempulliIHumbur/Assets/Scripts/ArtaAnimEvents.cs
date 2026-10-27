using UnityEngine;

// Merr ngjarjet nga klipet e animacionit (vendosen në import nga TempulliAnimacionet) dhe i çon te loja.
// Lëvizja e lojës drejtohet nga kodi (përgjigje e menjëhershme); ngjarjet sinkronizojnë efektet me trupin:
//  Hapi -> zë hapi + pluhur i vogël / spërkatje në ujë, Shtytja -> pluhur nga këmba, Ulja -> pluhur, Rreshqitja -> pluhur.
public class ArtaAnimEvents : MonoBehaviour
{
    public PlayerController player;
    public void Hapi() { if (player) player.AnimEvent("step"); }
    public void Shtytja() { if (player) player.AnimEvent("takeoff"); }
    public void Ulja() { if (player) player.AnimEvent("touchdown"); }
    public void Rreshqitja() { if (player) player.AnimEvent("slidedust"); }
}
