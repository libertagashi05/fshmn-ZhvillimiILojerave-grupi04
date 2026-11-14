using UnityEngine;

// Monedha që rrotullohet. Kur Arta e merr: fluturon lart, zvogëlohet dhe kthehet në pishinë (nuk fshihet).
// Krijohet nga TrackSpawner (pishina e monedhave); nuk ka nevojë të vendoset me dorë.
public class Coin : MonoBehaviour
{
    public System.Action<Coin> onFinished;   // TrackSpawner e kthen në pishinë
    bool collected;
    float t;
    Vector3 baseScale, startPos;
    Transform target;

    void Awake() { baseScale = transform.localScale; }

    void OnEnable() { collected = false; t = 0f; puller = null; if (baseScale != Vector3.zero) transform.localScale = baseScale; }

    // magneti (FuqiteLojtarit): monedha fluturon drejt Artës dhe merret kur arrin
    PlayerController puller;
    public void Pull(PlayerController p) { if (!collected) puller = p; }

    public bool Collect(Transform player)
    {
        if (collected) return false;
        collected = true;
        t = 0f;
        startPos = transform.position;
        target = player;
        var c = GetComponent<Collider>(); if (c) c.enabled = false;
        return true;
    }

    void Update()
    {
        if (!collected)
        {
            transform.Rotate(0, 180f * Time.deltaTime, 0, Space.World);
            if (puller != null)
            {
                Vector3 cak = puller.transform.position + Vector3.up * 1.1f;
                transform.position = Vector3.MoveTowards(transform.position, cak, 30f * Time.deltaTime);
                if ((cak - transform.position).sqrMagnitude < 1f) { var p = puller; puller = null; p.CollectCoin(this); }
            }
            return;
        }
        // animacion i shkurtër marrjeje (0.25 s): ngrihet drejt kokës së Artës dhe zhduket
        t += Time.deltaTime / 0.25f;
        Vector3 to = target != null ? target.position + Vector3.up * 2.4f : startPos + Vector3.up * 1.5f;
        transform.position = Vector3.Lerp(startPos, to, t * t);
        transform.localScale = baseScale * Mathf.Lerp(1.2f, 0.1f, t);
        transform.Rotate(0, 720f * Time.deltaTime, 0, Space.World);
        if (t >= 1f)
        {
            var c = GetComponent<Collider>(); if (c) c.enabled = true;
            if (onFinished != null) onFinished(this); else gameObject.SetActive(false);
        }
    }
}
