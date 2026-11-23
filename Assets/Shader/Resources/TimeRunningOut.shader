// "Hurt in a shooter" full-screen effect for the timer running out.
//   - Radial (zoom) blur: centre of the screen stays sharp, the edges smear outwards.
//     The sharp area shrinks as intensity rises.
//   - Red grade over the whole image, plus a stronger red glow at the edges.
//
// Driven entirely by global shader values set from TimeRunningOutEffect.cs,
// and drawn by TimeRunningOutFeature.cs (URP renderer feature).
// Lives in a Resources folder so it is always included in builds.
Shader "Hidden/HuntedHallucinations/TimeRunningOut"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "TimeRunningOut"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define TRO_SAMPLES 14

            // Globals (set with Shader.SetGlobal* from TimeRunningOutEffect)
            float  _TRO_Intensity;    // 0..1 overall, 0 = pass does nothing
            float  _TRO_BlurStrength; // how far the edges smear, in UV units
            float  _TRO_ClearRadius;  // radius of the sharp centre
            float  _TRO_Falloff;      // distance from sharp to fully blurred
            float4 _TRO_TintColor;    // the red
            float  _TRO_TintAmount;   // red grade over the whole screen (0..1)
            float  _TRO_EdgeRed;      // red glow at the edges (0..1+, includes heartbeat)
            float  _TRO_Desaturate;   // pull colour out before tinting (0..1)
            float  _TRO_EdgeDarken;   // darken the edges (0..1)

            float Dither(float2 pixel)
            {
                // Interleaved gradient noise: breaks up sample banding in the blur.
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_TRO_Intensity <= 0.0001)
                    return src;

                // Distance from screen centre, aspect-corrected so the clear zone is round.
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 dir = uv - 0.5;
                float dist = length(float2(dir.x * aspect, dir.y));
                float edge = smoothstep(_TRO_ClearRadius, _TRO_ClearRadius + _TRO_Falloff, dist);

                // --- Radial blur (zoom towards centre + a little disc spread) ---
                float blurAmt = edge * _TRO_BlurStrength;
                float jitter = Dither(input.positionCS.xy);
                half3 acc = 0;
                [unroll]
                for (int i = 0; i < TRO_SAMPLES; i++)
                {
                    float t = (i + jitter) / TRO_SAMPLES;
                    float ang = (i + jitter) * 2.39996; // golden angle
                    float2 disc = float2(cos(ang), sin(ang)) * sqrt(t) * blurAmt * 0.3;
                    disc.x /= aspect;
                    float2 suv = uv - dir * t * blurAmt + disc;
                    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, suv).rgb;
                }
                half3 col = acc / TRO_SAMPLES;

                // --- Red grade ---
                half lum = dot(col, half3(0.299, 0.587, 0.114));
                col = lerp(col, lum.xxx, _TRO_Desaturate);
                half3 redMono = lum * _TRO_TintColor.rgb * 2.0;
                col = lerp(col, redMono, saturate(_TRO_TintAmount));

                // --- Red glow and darkening at the edges ---
                float edgeGlow = saturate(edge * edge * _TRO_EdgeRed);
                col = lerp(col, _TRO_TintColor.rgb * 0.55 + redMono * 0.45, edgeGlow);
                col *= 1.0 - edge * _TRO_EdgeDarken;

                return half4(col, src.a);
            }
            ENDHLSL
        }
    }
}
