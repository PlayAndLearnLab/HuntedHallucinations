Shader "Custom/FogTest3"
{
    Properties
    {
        _MainTex         ("Sprite Sheet", 2D)                         = "white" {}
        _Color           ("Fog Color", Color)                         = (0.75, 0.8, 0.85, 1)
        _ParticleOpacity ("Particle Opacity Multiplier", Range(0, 1)) = 1.0
        _SoftEdge        ("Radial Edge Softness", Range(0, 1))        = 0.3
        _DepthFade       ("Depth Fade Distance", Range(0.1, 5))       = 1.5
        _InnerRadius     ("Clear Zone Inner Radius", Float)           = 5.0
        _OuterRadius     ("Clear Zone Outer Radius", Float)           = 15.0
        _MaskPower       ("Mask Curve Power (>1 = slower open)", Range(0.5, 4.0)) = 1.0
        // Extra softening applied on top of the particle's own size so the mask
        // starts dissolving the sprite before its centre reaches _InnerRadius.
        // Increase if large particles still pop on entry. Units: world-space metres.
        _SizeBleed       ("Size Bleed (world units)", Range(0, 10))   = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline"  = "UniversalPipeline"
            "PreviewType"     = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull   Off
        Lighting Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ── Textures ──────────────────────────────────────────────────────────
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ── Per-material uniforms ─────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _ParticleOpacity;
                half   _SoftEdge;
                half   _DepthFade;
                float  _InnerRadius;
                float  _OuterRadius;
                float  _MaskPower;
                float  _SizeBleed;
            CBUFFER_END

            // ── Global uniforms (set via Shader.SetGlobalXxx from PlayerFogFollower) ──
            // Keep outside CBUFFER so all materials share the same value each frame.
            float3 _PlayerPosition;
            // Velocity written by C# each frame:  _PlayerVelocity = rb.velocity (XZ)
            // Used to anticipate the hole so it opens ahead of the player.
            float3 _PlayerVelocity;

            // ─────────────────────────────────────────────────────────────────────

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                // Custom Vertex Stream slot — add "Size" (Size.x) in the Particle
                // System's Renderer > Custom Vertex Streams list, mapped to TEXCOORD1.x
                // If the stream is absent the value arrives as 0 and _SizeBleed takes over.
                float4 custom1    : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4  color       : COLOR;
                float4 screenPos   : TEXCOORD1;
                float3 worldPos    : TEXCOORD2;
                float  eyeDepth    : TEXCOORD3;
                // Effective inner radius for THIS particle = particleSize + _SizeBleed.
                // Baked in the vertex shader so the interpolator does the work.
                float  innerR      : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Vertex ───────────────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS  = vpi.positionCS;
                OUT.worldPos    = vpi.positionWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.screenPos   = ComputeScreenPos(OUT.positionCS);
                OUT.eyeDepth    = -TransformWorldToView(vpi.positionWS).z;

                // Guard zero-streamed vertex colors with a branchless lerp
                float  colorIsZero = step(length(IN.color), 0.001);
                float4 safeColor   = lerp(IN.color, float4(1, 1, 1, 1), colorIsZero);
                OUT.color = safeColor * _Color;

                // Particle size from custom stream (half the size value = radius).
                // Falls back gracefully to 0 if the stream is not wired up.
                float particleRadius = IN.custom1.x * 0.5;

                // Effective inner boundary for this specific particle:
                // push _InnerRadius outward by the particle's own radius plus a
                // constant bleed so dissolution begins before the centre enters the hole.
                OUT.innerR = _InnerRadius + particleRadius + _SizeBleed;

                return OUT;
            }

            // ── Fragment ─────────────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                // 1. Sprite sheet sample
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 2. Soft-particle depth fade
                float2 screenUV   = IN.screenPos.xy / IN.screenPos.w;
                float  rawDepth   = SampleSceneDepth(screenUV);
                float  sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float  softFade   = saturate((sceneDepth - IN.eyeDepth) / _DepthFade);

                // 3. Seamless player clear-zone mask
                //
                //    The key insight: measure distance from the player's *anticipated*
                //    position (current + one frame of velocity) rather than the raw
                //    position.  This keeps the hole open slightly ahead of movement so
                //    the leading edge of the player never re-enters opaque fog.
                //
                //    We then compare that distance against IN.innerR, which is already
                //    expanded by the particle's own radius + _SizeBleed, so the mask
                //    starts dissolving the sprite *before* the player centre touches it.
                //
                //    Result: 0 = fully transparent (inside clear zone)
                //            1 = full opacity  (outside outer radius)

                // Anticipation: ~1 frame of movement at 60 Hz.  Scale with speed so
                // fast movement gets more lead, slow movement gets none.
                float speed        = length(_PlayerVelocity.xz);
                float anticipation = speed * 0.016; // ~1 frame at 60 fps, world units
                float2 anticipatedXZ = _PlayerPosition.xz
                                     + normalize(_PlayerVelocity.xz + 0.0001) * anticipation;

                float playerDist = distance(IN.worldPos.xz, anticipatedXZ);

                // Effective band: inner edge expanded per-particle, outer edge fixed.
                float effectiveInner = IN.innerR;
                float effectiveOuter = max(effectiveInner + 0.001, _OuterRadius);
                float bandWidth      = effectiveOuter - effectiveInner;

                float t          = saturate((playerDist - effectiveInner) / bandWidth);
                float cosT       = 0.5 - 0.5 * cos(t * 3.14159265359); // S-curve, zero slope at ends
                float playerMask = pow(cosT, _MaskPower);

                // 4. Radial per-sprite edge fade
                float2 centeredUV = IN.uv * 2.0 - 1.0;
                float  radialFade = pow(
                    1.0 - saturate(dot(centeredUV, centeredUV)),
                    _SoftEdge * 3.0 + 0.5
                );

                // 5. Combine
                float pAlpha     = lerp(1.0, IN.color.a, step(0.001, IN.color.a));
                half  finalAlpha = texColor.a
                                 * pAlpha
                                 * _ParticleOpacity
                                 * radialFade
                                 * softFade
                                 * playerMask;

                return half4(texColor.rgb * IN.color.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}




// Shader "Custom/FogTest3"
// {
//     Properties
//     {
//         _MainTex ("Sprite Sheet", 2D) = "white" {}
//         _Color ("Fog Color", Color) = (0.75, 0.8, 0.85, 1)
//         _ParticleOpacity ("Particle Opacity Multiplier", Range(0, 1)) = 1.0
//         _SoftEdge ("Soft Edge Factor", Range(0, 1)) = 0.3
//         _DepthFade ("Depth Fade Distance", Range(0.1, 5)) = 1.5
//         _InnerRadius ("Clear Inner Radius", Float) = 5.0
//         _OuterRadius ("Fog Outer Radius", Float) = 15.0
//     }

//     SubShader
//     {
//         Tags
//         {
//             "Queue" = "Transparent"
//             "RenderType" = "Transparent"
//             "IgnoreProjector" = "True"
//             "RenderPipeline" = "UniversalPipeline"
//             "PreviewType" = "Plane"
//         }

//         Blend SrcAlpha OneMinusSrcAlpha
//         ZWrite Off
//         Cull Off
//         Lighting Off

//         Pass
//         {
//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #pragma multi_compile_particles
//             #pragma multi_compile_fog

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);

//             float3 _PlayerPosition;

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _MainTex_ST;
//                 half4 _Color;
//                 half _ParticleOpacity;
//                 half _SoftEdge;
//                 half _DepthFade;
//                 float _InnerRadius;
//                 float _OuterRadius;
//             CBUFFER_END

//             struct Attributes
//             {
//                 float4 positionOS   : POSITION;
//                 float2 uv           : TEXCOORD0;
//                 float4 color        : COLOR;
//                 UNITY_VERTEX_INPUT_INSTANCE_ID
//             };

//             struct Varyings
//             {
//                 float4 positionCS   : SV_POSITION;
//                 float2 uv           : TEXCOORD0;
//                 float4 color        : COLOR;
//                 float4 screenPos    : TEXCOORD1;
//                 float3 worldPos     : TEXCOORD2;
//                 float  eyeDepth     : TEXCOORD3;
//                 UNITY_VERTEX_OUTPUT_STEREO
//             };

//             Varyings vert(Attributes IN)
//             {
//                 UNITY_SETUP_INSTANCE_ID(IN);
//                 Varyings OUT;
//                 UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

//                 VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
//                 OUT.positionCS = vertexInput.positionCS;
//                 OUT.worldPos = vertexInput.positionWS;
//                 OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                
//                 // Fallback: If IN.color is zeroed out by Particle System, default to white
//                 float4 vertColor = IN.color;
//                 if (length(vertColor) < 0.001)
//                 {
//                     vertColor = float4(1, 1, 1, 1);
//                 }
                
//                 OUT.color = vertColor * _Color;
//                 OUT.screenPos = ComputeScreenPos(OUT.positionCS);
//                 OUT.eyeDepth = -TransformWorldToView(vertexInput.positionWS).z;
//                 return OUT;
//             }

//             half4 frag(Varyings IN) : SV_Target
//             {
//                 half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

//                 // 1. Soft-particle depth fade
//                 float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
//                 float sceneRawDepth = SampleSceneDepth(screenUV);
//                 float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
//                 float depthDiff = sceneEyeDepth - IN.eyeDepth;
//                 float softFade = saturate(depthDiff / _DepthFade);

//                 // 2. Clear zone hole around player
//                 float playerDist = distance(IN.worldPos.xz, _PlayerPosition.xz);
//                 // float playerMask = smoothstep(_InnerRadius, _OuterRadius, playerDist);

//                 // Smooth, quadratic falloff calculation:
//                 // float edgeDistance = saturate((playerDist - _InnerRadius) / max(0.001, (_OuterRadius - _InnerRadius)));
//                 // float playerMask = smoothstep(0.0, 1.0, edgeDistance);

//                 // // Curve the transition so it fades out very gently near the player edge:
//                 // playerMask = pow(playerMask, 2.0);

//                 // 2. Ultra-smooth Clear zone hole around player
//                 float normalizedDist = saturate((playerDist - _InnerRadius) / max(0.001, (_OuterRadius - _InnerRadius)));

//                 // Cosine falloff creates a zero-derivative curve at both ends (no sharp start/stop lines)
//                 float playerMask = 0.5 - 0.5 * cos(normalizedDist * 3.14159265);

//                 // Soften the lower end even further so low-density fog doesn't pop in abruptly
//                 playerMask = smoothstep(0.0, 1.0, playerMask);

//                 // 3. Radial edge softening
//                 float2 centeredUV = IN.uv * 2.0 - 1.0;
//                 float radialFade = 1.0 - saturate(dot(centeredUV, centeredUV));
//                 radialFade = pow(radialFade, _SoftEdge * 3.0 + 0.5);

//                 half3 finalColor = texColor.rgb * IN.color.rgb;

//                 // Safeguard particle lifetime alpha against 0 overrides
//                 float pAlpha = (IN.color.a < 0.001) ? 1.0 : IN.color.a;
//                 half finalAlpha = texColor.a * pAlpha * _ParticleOpacity * radialFade * softFade * playerMask;

//                 return half4(finalColor, finalAlpha);
//             }
//             ENDHLSL
//         }
//     }
// }
