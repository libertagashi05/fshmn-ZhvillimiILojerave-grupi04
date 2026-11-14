using UnityEngine;

// Fotoja që del pas vdekjes (te ekrani "TË KAPI!"): Arta si vizatim me sytë X, gjuhën jashtë dhe aureolë mbi kokë.
// Vizatohet me kod (pa skedar). Për të vënë foton tënde në vend të saj: ruaje si Assets/Resources/VdekjaFoto.png
public static class DeathPhoto
{
    const int W = 256, H = 288;
    static Texture2D tex;

    public static Texture2D Get()
    {
        if (tex != null) return tex;
        var custom = Resources.Load<Texture2D>("VdekjaFoto");
        tex = custom != null ? custom : Draw();
        return tex;
    }

    // ---------- vizatimi ----------
    static readonly Color Skin = new Color(0.94f, 0.75f, 0.6f), SkinDark = new Color(0.8f, 0.58f, 0.45f);
    static readonly Color Hair = new Color(0.25f, 0.15f, 0.09f), HairLight = new Color(0.38f, 0.24f, 0.14f);
    static readonly Color Ink = new Color(0.16f, 0.1f, 0.07f), Shirt = new Color(0.93f, 0.9f, 0.8f);
    static readonly Color Tongue = new Color(0.95f, 0.42f, 0.5f), Mouth = new Color(0.45f, 0.08f, 0.1f);
    static readonly Color Gold = new Color(1f, 0.84f, 0.3f);

    static Texture2D Draw()
    {
        var px = new Color[W * H];
        for (int row = 0; row < H; row++)
            for (int x = 0; x < W; x++)
            {
                var p = new Vector2(x + 0.5f, row + 0.5f);   // koordinata nga lart-majtas
                px[(H - 1 - row) * W + x] = Pixel(p);
            }
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "VdekjaFoto_Vizatim" };
        t.SetPixels(px);
        t.Apply(false, true);
        return t;
    }

    static Color Pixel(Vector2 p)
    {
        // sfondi: qiell i ngrohtë me rreze dielli
        float g = p.y / H;
        Color c = Color.Lerp(new Color(1f, 0.88f, 0.6f), new Color(0.88f, 0.6f, 0.42f), g);
        float ang = Mathf.Atan2(p.y - 130f, p.x - 128f);
        if (Mathf.Repeat(ang * 12f / Mathf.PI, 2f) < 1f) c = Color.Lerp(c, new Color(1f, 0.95f, 0.75f), 0.25f);

        // flokët prapa + gërsheti
        c = Shape(c, Ink, Ellipse(p, new Vector2(128, 132), 92, 98) - 3f);
        c = Shape(c, Hair, Ellipse(p, new Vector2(128, 132), 92, 98));
        for (int i = 0; i < 6; i++)
        {
            var bc = new Vector2(206 + i * 3f, 180 + i * 19f);
            float r = 16f - i * 1.3f;
            c = Shape(c, Ink, Circle(p, bc, r + 2.5f));
            c = Shape(c, i % 2 == 0 ? Hair : HairLight, Circle(p, bc, r));
        }
        // qafa dhe bluza
        c = Shape(c, Ink, Ellipse(p, new Vector2(128, 300), 112, 72) - 3f);
        c = Shape(c, Shirt, Ellipse(p, new Vector2(128, 300), 112, 72));
        c = Shape(c, SkinDark, Ellipse(p, new Vector2(128, 228), 26, 34));
        // fytyra
        var fc = new Vector2(128, 144);
        c = Shape(c, Ink, Ellipse(p, fc, 66, 78) - 3f);
        c = Shape(c, Skin, Ellipse(p, fc, 66, 78));
        // balluket (flokët mbi ballë)
        float bangs = Mathf.Min(Ellipse(p, new Vector2(94, 92), 56, 36), Ellipse(p, new Vector2(164, 90), 54, 34));
        c = Shape(c, Hair, bangs);
        c = Shape(c, HairLight, Mathf.Max(Segment(p, new Vector2(78, 84), new Vector2(112, 76)) - 2f, -bangs - 4f));   // shkëlqim
        c = Shape(c, HairLight, Mathf.Max(Segment(p, new Vector2(146, 74), new Vector2(178, 82)) - 2f, -bangs - 4f));
        // faqet e skuqura
        c = Shape(c, new Color(1f, 0.55f, 0.55f, 0.45f), Ellipse(p, new Vector2(90, 172), 13, 7));
        c = Shape(c, new Color(1f, 0.55f, 0.55f, 0.45f), Ellipse(p, new Vector2(166, 172), 13, 7));
        // sytë X
        foreach (float ex in new[] { 102f, 154f })
        {
            var e = new Vector2(ex, 146);
            c = Shape(c, Ink, Segment(p, e + new Vector2(-11, -11), e + new Vector2(11, 11)) - 3.2f);
            c = Shape(c, Ink, Segment(p, e + new Vector2(-11, 11), e + new Vector2(11, -11)) - 3.2f);
        }
        // goja e hapur dhe gjuha jashtë
        c = Shape(c, Ink, Ellipse(p, new Vector2(128, 194), 24, 11) - 2f);
        c = Shape(c, Mouth, Ellipse(p, new Vector2(128, 194), 24, 11));
        c = Shape(c, Ink, Ellipse(p, new Vector2(138, 211), 14, 20) - 2.5f);
        c = Shape(c, Tongue, Ellipse(p, new Vector2(138, 211), 14, 20));
        c = Shape(c, new Color(0.8f, 0.25f, 0.33f), Mathf.Max(Segment(p, new Vector2(138, 200), new Vector2(138, 220)) - 1.2f, -1f));
        // aureola
        float halo = Mathf.Abs(Ellipse(p, new Vector2(128, 30), 62, 14)) - 4f;
        c = Shape(c, Ink, halo - 1.5f);
        c = Shape(c, Gold, halo);

        // tone fotoje: pak sepia, vinjetë dhe kokërr
        float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
        c = Color.Lerp(c, new Color(lum * 1.08f, lum * 0.95f, lum * 0.78f), 0.18f);
        float v = Vector2.Distance(p, new Vector2(W * 0.5f, H * 0.5f)) / (H * 0.62f);
        c *= 1f - 0.35f * v * v;
        c += Color.white * ((Hash(p) - 0.5f) * 0.05f);
        c.a = 1f;
        return c;
    }

    // ---------- forma (distanca me shenjë, në piksel) ----------
    static Color Shape(Color under, Color col, float d)
    {
        float a = Mathf.Clamp01(0.5f - d) * col.a;
        return Color.Lerp(under, new Color(col.r, col.g, col.b, 1f), a);
    }

    static float Circle(Vector2 p, Vector2 c, float r) => Vector2.Distance(p, c) - r;

    static float Ellipse(Vector2 p, Vector2 c, float rx, float ry)
    {
        var q = new Vector2((p.x - c.x) / rx, (p.y - c.y) / ry);
        return (q.magnitude - 1f) * Mathf.Min(rx, ry);
    }

    static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude;
    }

    static float Hash(Vector2 p) => Mathf.Repeat(Mathf.Sin(p.x * 12.9898f + p.y * 78.233f) * 43758.5453f, 1f);
}
