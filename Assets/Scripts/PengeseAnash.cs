using UnityEngine;

// Tempulli i Humbur - vagoni (bota e varrit): guri që del nga muri në lartësinë e trupit.
// "ana" = ana ku është guri (-1 majtas, 1 djathtas). Krijohet nga TrackSpawner.Anash si trigger i gjerë;
// PlayerController.GuriAnash e kontrollon kur Arta hyn: shpëton vetëm nëse anohet në anën tjetër.
public class PengeseAnash : MonoBehaviour
{
    public int ana = -1;
}
