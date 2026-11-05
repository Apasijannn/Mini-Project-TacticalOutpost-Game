// Skybox khusus mode Procedural Dungeon.
// Lapisan 1: langit malam terang prosedural (gradasi zenith → cakrawala + cahaya di sisi bulan) + bintang dari
//            Stagit skyboxv1_starsonly.
// Lapisan 2: skyboxv1_moon1 (Stagit Earth & Planets Skyboxes) — bulan bertekstur kawah.
//   Bulan di skybox Stagit sangat besar (~55°). Shader "memperkecil" bulan itu dengan memetakan sudut dari arah
//   _MoonTarget (posisi bulan di langit) ke sudut x _MoonScale dari pusat bulan di cubemap Stagit (_MoonSource).
// Pemetaan 6-sided sama dengan Skybox/6 Sided bawaan Unity (Front=+Z, Back=-Z, Left=+X, Right=-X, Up=+Y, Down=-Y),
// rotasi langit juga sama (_Rotation diputar di vertex), jadi tampilan langit identik dengan material aslinya.
Shader "Outpost/Skybox Moonlit Dungeon"
{
    Properties
    {
        _ZenithColor ("Zenith Color", Color) = (0.06, 0.11, 0.26, 1)
        _HorizonColor ("Horizon Color", Color) = (0.22, 0.33, 0.56, 1)
        _GroundColor ("Below Horizon Color", Color) = (0.03, 0.045, 0.09, 1)
        _GradientPower ("Gradient Power", Range(0.1, 3)) = 0.55
        _SkyGlowColor ("Moonlit Sky Glow", Color) = (0.42, 0.55, 0.85, 1)
        _SkyGlowIntensity ("Moonlit Sky Glow Intensity", Range(0, 3)) = 0.7
        _SkyGlowPower ("Moonlit Sky Glow Falloff", Range(1, 32)) = 5
        _Tint ("Star Tint", Color) = (.5, .5, .5, .5)
        [Gamma] _Exposure ("Star Exposure", Range(0, 8)) = 1.0
        _StarIntensity ("Star Intensity", Range(0, 5)) = 1.6
        _Rotation ("Sky Rotation", Range(0, 360)) = 335
        [NoScaleOffset] _FrontTex ("Stars Front [+Z]", 2D) = "black" {}
        [NoScaleOffset] _BackTex ("Stars Back [-Z]", 2D) = "black" {}
        [NoScaleOffset] _LeftTex ("Stars Left [+X]", 2D) = "black" {}
        [NoScaleOffset] _RightTex ("Stars Right [-X]", 2D) = "black" {}
        [NoScaleOffset] _UpTex ("Stars Up [+Y]", 2D) = "black" {}
        [NoScaleOffset] _DownTex ("Stars Down [-Y]", 2D) = "black" {}

        [Header(Moon (Stagit skyboxv1_moon1))]
        [NoScaleOffset] _MoonFront ("Moon Front [+Z]", 2D) = "black" {}
        [NoScaleOffset] _MoonBack ("Moon Back [-Z]", 2D) = "black" {}
        [NoScaleOffset] _MoonLeft ("Moon Left [+X]", 2D) = "black" {}
        [NoScaleOffset] _MoonRight ("Moon Right [-X]", 2D) = "black" {}
        [NoScaleOffset] _MoonUp ("Moon Up [+Y]", 2D) = "black" {}
        [NoScaleOffset] _MoonDown ("Moon Down [-Y]", 2D) = "black" {}
        _MoonTarget ("Moon position in sky (sky space)", Vector) = (-0.341, 0.5876, 0.7338, 0)
        _MoonSource ("Moon disc centre in Stagit cubemap", Vector) = (0.8908, 0.4415, -0.1072, 0)
        _MoonRadius ("Moon disc radius in Stagit cubemap (deg)", Range(1, 90)) = 48.2
        _MoonScale ("Moon shrink factor", Range(1, 30)) = 7.5
        _MoonMaxAngle ("Max source angle (deg)", Range(10, 120)) = 52
        _MoonRoll ("Moon roll (deg)", Range(-180, 180)) = 0
        _MoonTint ("Moon Tint", Color) = (0.93, 0.96, 1, 1)
        _MoonIntensity ("Moon Intensity", Range(0, 10)) = 2.8
        _EarthShine ("Dark side earthshine", Range(0, 0.5)) = 0.12
        _MoonLod ("Moon mip level", Range(0, 6)) = 2.5
        _MoonOcclusion ("Moon hides sky behind it", Range(0, 1)) = 0.95
        _CloudOver ("Bright sky passes in front of moon", Range(0, 1)) = 0
        _GlowColor ("Halo Color", Color) = (0.55, 0.68, 1, 1)
        _GlowIntensity ("Halo Intensity", Range(0, 4)) = 0.55
        _GlowSize ("Halo Size (rad)", Range(0.01, 1)) = 0.22
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            half4 _Tint; half _Exposure; float _Rotation;
            half4 _ZenithColor, _HorizonColor, _GroundColor, _SkyGlowColor;
            float _GradientPower, _SkyGlowIntensity, _SkyGlowPower, _StarIntensity;
            sampler2D _FrontTex, _BackTex, _LeftTex, _RightTex, _UpTex, _DownTex;
            sampler2D _MoonFront, _MoonBack, _MoonLeft, _MoonRight, _MoonUp, _MoonDown;
            float4 _MoonTarget, _MoonSource;
            float _MoonRadius, _EarthShine, _MoonScale, _MoonMaxAngle, _MoonRoll, _MoonIntensity, _MoonLod, _MoonOcclusion, _CloudOver;
            half4 _MoonTint, _GlowColor; float _GlowIntensity, _GlowSize;

            float3 RotateAroundYInDegrees(float3 v, float degrees)
            {
                float a = degrees * UNITY_PI / 180.0;
                float s, c; sincos(a, s, c);
                float2x2 m = float2x2(c, -s, s, c);
                return float3(mul(m, v.xz), v.y).xzy;
            }

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(RotateAroundYInDegrees(v.vertex.xyz, _Rotation));
                o.dir = v.vertex.xyz;
                return o;
            }

            // Arah → face + UV, konvensi Unity Skybox/6 Sided.
            #define SAMPLE6(F, B, L, R, U, D, dir, lod, outc)                                   \
            {                                                                                    \
                float3 a = abs(dir); float2 uv; float4 col;                                      \
                if (a.x >= a.y && a.x >= a.z)                                                    \
                {                                                                                \
                    if (dir.x > 0) { uv = float2(-dir.z, dir.y) / a.x; uv = uv * 0.5 + 0.5; col = tex2Dlod(L, float4(saturate(uv), 0, lod)); } \
                    else           { uv = float2( dir.z, dir.y) / a.x; uv = uv * 0.5 + 0.5; col = tex2Dlod(R, float4(saturate(uv), 0, lod)); } \
                }                                                                                \
                else if (a.y >= a.z)                                                             \
                {                                                                                \
                    if (dir.y > 0) { uv = float2(dir.x, -dir.z) / a.y; uv = uv * 0.5 + 0.5; col = tex2Dlod(U, float4(saturate(uv), 0, lod)); } \
                    else           { uv = float2(dir.x,  dir.z) / a.y; uv = uv * 0.5 + 0.5; col = tex2Dlod(D, float4(saturate(uv), 0, lod)); } \
                }                                                                                \
                else                                                                             \
                {                                                                                \
                    if (dir.z > 0) { uv = float2( dir.x, dir.y) / a.z; uv = uv * 0.5 + 0.5; col = tex2Dlod(F, float4(saturate(uv), 0, lod)); } \
                    else           { uv = float2(-dir.x, dir.y) / a.z; uv = uv * 0.5 + 0.5; col = tex2Dlod(B, float4(saturate(uv), 0, lod)); } \
                }                                                                                \
                outc = col.rgb;                                                                  \
            }

            void Frame(float3 n, out float3 r, out float3 u)
            {
                float3 up = abs(n.y) > 0.98 ? float3(0, 0, 1) : float3(0, 1, 0);
                r = normalize(cross(up, n));
                u = cross(n, r);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 v = normalize(i.dir);

                // ---- langit malam terang: gradasi + pendar di sisi bulan + bintang Stagit
                float h = v.y;
                float3 sky = h >= 0 ? lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(h), _GradientPower))
                                    : lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-h * 5));
                float3 Tg = normalize(_MoonTarget.xyz);
                sky += _SkyGlowColor.rgb * _SkyGlowIntensity * pow(saturate(dot(v, Tg) * 0.5 + 0.5), _SkyGlowPower) * saturate(h * 3 + 0.4);
                float3 stars;
                SAMPLE6(_FrontTex, _BackTex, _LeftTex, _RightTex, _UpTex, _DownTex, v, 0, stars);
                float starFade = saturate(h * 6 + 0.1) * (1 - 0.8 * pow(saturate(dot(v, Tg)), 24));   // bintang redup di dekat bulan & cakrawala
                sky += stars * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure * _StarIntensity * starFade;

                // ---- bulan (Stagit), diperkecil di sekitar _MoonTarget
                float3 T = normalize(_MoonTarget.xyz), M = normalize(_MoonSource.xyz);
                float3 Tr, Tu, Mr, Mu; Frame(T, Tr, Tu); Frame(M, Mr, Mu);
                float c = clamp(dot(v, T), -1, 1);
                float th = acos(c);
                float3 perp = v - c * T; float pl = length(perp);
                float2 ab = pl > 1e-5 ? float2(dot(perp, Tr), dot(perp, Tu)) / pl : float2(0, 0);
                float rs, rc; sincos(radians(_MoonRoll), rs, rc);
                ab = float2(ab.x * rc - ab.y * rs, ab.x * rs + ab.y * rc);
                float sth = th * _MoonScale;
                float maxA = radians(_MoonMaxAngle);
                float inRange = 1 - smoothstep(maxA * 0.8, maxA, sth);
                float3 moon = 0;
                if (inRange > 0)
                {
                    float ss, sc; sincos(min(sth, UNITY_PI), ss, sc);
                    float3 src = sc * M + ss * (ab.x * Mr + ab.y * Mu);
                    SAMPLE6(_MoonFront, _MoonBack, _MoonLeft, _MoonRight, _MoonUp, _MoonDown, src, _MoonLod, moon);
                    moon *= inRange;
                }
                // Piringan bulan solid: sisi gelap menutupi langit (termasuk bulan kecil bawaan MoonlitNight) dengan sedikit earthshine.
                float Rv = radians(_MoonRadius) / _MoonScale;
                float disc = 1 - smoothstep(Rv * 0.97, Rv, th);
                moon *= disc;
                float skyLum = dot(sky, float3(0.299, 0.587, 0.114));
                float cloud = smoothstep(0.35, 0.75, skyLum) * _CloudOver * smoothstep(Rv * 1.1, Rv * 1.7, th);  // awan di sekitar bulan tetap di depan halo
                float3 halo = _GlowColor.rgb * _GlowIntensity * exp(-th / max(_GlowSize, 1e-3));
                float cover = disc * _MoonOcclusion * (1 - cloud);
                float3 moonCol = moon * _MoonTint.rgb * _MoonIntensity + sky * _EarthShine + float3(0.02, 0.025, 0.04);
                float3 col = sky * (1 - cover) + moonCol * cover + halo * (1 - disc * 0.7);
                return half4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
