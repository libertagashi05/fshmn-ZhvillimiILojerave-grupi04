// Funksione HLSL për Shader Graph-et e Tempullit (përdoren nga nyja "Custom Function").
// SG_GuriMyshk.shadergraph -> Guri   : gur me myshk sipër dhe në të çarat (sasia = alfa e ngjyrës)
// SG_Uji.shadergraph       -> Uji    : ujë që rrjedh, dy shtresa valësh, shkumë që lëviz dhe më e fortë te brigjet
#ifndef TEMPULLI_SG_INCLUDED
#define TEMPULLI_SG_INCLUDED

float TSG_Hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float TSG_Noise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = TSG_Hash(i), b = TSG_Hash(i + float2(1, 0)), c = TSG_Hash(i + float2(0, 1)), d = TSG_Hash(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float TSG_Fbm(float2 p)
{
    float s = 0.0, a = 0.5;
    for (int k = 0; k < 4; k++) { s += a * TSG_Noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
    return s;
}

// UV në metra/4 (1 UV = 4 m), NormalWS = normalja në botë
void Guri_float(float2 UV, float3 NormalWS, UnityTexture2D BaseMap, UnityTexture2D NormalMap, float4 Tint, float Smooth, float NormalScale,
                out float3 Albedo, out float3 NormalTS, out float Smoothness, out float Alpha)
{
    float2 uv = UV * BaseMap.scaleTranslate.xy + BaseMap.scaleTranslate.zw;
    float4 c = SAMPLE_TEXTURE2D(BaseMap.tex, BaseMap.samplerstate, uv);
    float3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(NormalMap.tex, NormalMap.samplerstate, uv), NormalScale);
    float3 baseCol = c.rgb * Tint.rgb;
    float lum = dot(c.rgb, float3(0.3, 0.59, 0.11));
    float big = TSG_Fbm(UV * 1.7);
    float fine = TSG_Noise(UV * 14.0);
    float top = saturate((NormalWS.y - 0.35) * 2.2);
    float amount = Tint.a;                        // sasia e myshkut: alfa e ngjyrës (0 = pa myshk)
    float m = top * smoothstep(0.80 - amount * 0.45, 0.92 - amount * 0.40, big + fine * 0.12);
    m = max(m, (1.0 - top) * amount * smoothstep(0.55, 0.75, big) * saturate((0.42 - lum) * 4.0));
    m = saturate(m) * step(0.001, amount);
    float3 mossCol = lerp(float3(0.16, 0.25, 0.06), float3(0.36, 0.45, 0.13), fine) * (0.75 + 0.5 * lum);
    baseCol *= lerp(0.86, 1.10, big);             // ndryshim i lehtë ngjyre: gurët nuk duken të kopjuar
    Albedo = lerp(baseCol, mossCol, m);
    NormalTS = normalize(lerp(n, float3(0, 0, 1), m * 0.7));
    Smoothness = lerp(Smooth, 0.05, m);
    Alpha = 1.0;
}

void Uji_float(float2 UV, float3 NormalWS, UnityTexture2D BaseMap, UnityTexture2D NormalMap, float4 Tint, float Smooth, float NormalScale,
               out float3 Albedo, out float3 NormalTS, out float Smoothness, out float Alpha)
{
    float t = _TimeParameters.x;
    float2 uv = UV * BaseMap.scaleTranslate.xy;
    float2 flow = float2(0.0, -0.35);             // rrjedha drejt Artës (përgjatë lumit)
    float3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(NormalMap.tex, NormalMap.samplerstate, uv * 1.3 + flow * t), NormalScale);
    float3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(NormalMap.tex, NormalMap.samplerstate, uv * 2.1 + float2(0.07, -0.22) * t + 0.37), NormalScale * 0.7);
    NormalTS = normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
    float3 c = SAMPLE_TEXTURE2D(BaseMap.tex, BaseMap.samplerstate, uv + flow * t * 0.6).rgb;
    float f = TSG_Fbm(float2(UV.x * 3.0, UV.y * 1.2 - t * 0.45));
    float foam = smoothstep(0.60, 0.78, f + n1.x * 0.15);
    float edge = 1.0 - saturate(min(UV.x, max(0.0, 1.75 - UV.x)) * 6.0);   // brigjet (lumi ~7 m = 1.75 UV)
    foam = saturate(foam + edge * 0.6 * TSG_Noise(float2(UV.x * 8.0, UV.y * 6.0 - t * 1.2)));
    float3 deep = Tint.rgb * (0.75 + 0.4 * c);
    Albedo = lerp(deep, float3(0.92, 0.96, 0.95), foam * 0.85);
    Smoothness = lerp(Smooth, 0.45, foam);
    Alpha = saturate(Tint.a + foam * 0.4);
}

// versionet "half" (nëse grafiku kalon në saktësi të ulët)
void Guri_half(float2 UV, float3 NormalWS, UnityTexture2D BaseMap, UnityTexture2D NormalMap, float4 Tint, float Smooth, float NormalScale,
               out float3 Albedo, out float3 NormalTS, out float Smoothness, out float Alpha)
{ Guri_float(UV, NormalWS, BaseMap, NormalMap, Tint, Smooth, NormalScale, Albedo, NormalTS, Smoothness, Alpha); }
void Uji_half(float2 UV, float3 NormalWS, UnityTexture2D BaseMap, UnityTexture2D NormalMap, float4 Tint, float Smooth, float NormalScale,
              out float3 Albedo, out float3 NormalTS, out float Smoothness, out float Alpha)
{ Uji_float(UV, NormalWS, BaseMap, NormalMap, Tint, Smooth, NormalScale, Albedo, NormalTS, Smoothness, Alpha); }

#endif
