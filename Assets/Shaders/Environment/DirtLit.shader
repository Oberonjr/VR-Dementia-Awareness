// Dirt Lit — URP lit shader that adds ground dirt, corner grime, settled dust and
// hand-painted dirt (vertex color red) on top of a normal PBR material. The look of the dirt (color, height, breakup)
// is shared across the whole scene and driven by the DirtController component.
// Each material only decides how much dirt it accepts.

Shader "Custom/Dirt Lit"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Color", 2D) = "white" {}
        [MainColor] _BaseColor("Base Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Float) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.2
        _Metallic("Metallic", Range(0, 1)) = 0
        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1
        [Toggle(_ORMMAP)] _UseORM("Use Packed ORM Map (replaces Smoothness/Metallic/Occlusion)", Float) = 0
        [NoScaleOffset] _ORMMap("ORM Map (R=AO, G=Roughness, B=Metallic, sRGB off)", 2D) = "white" {}

        [Header(Dirt On This Material)]
        _DirtAmount("Dirt Amount", Range(0, 2)) = 1
        _HeightInfluence("Ground Band (dirt near ground level)", Range(0, 1)) = 1
        _CreviceInfluence("Corners (where surfaces meet)", Range(0, 1)) = 1
        _TopInfluence("Settled Dust (upward faces)", Range(0, 1)) = 1
        [Toggle(_PAINTED_DIRT)] _UsePaintedDirt("Use Painted Dirt (vertex color red)", Float) = 0
        _PaintedInfluence("Painted Dirt", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        // Per-material values (kept together so the SRP Batcher stays fast)
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Smoothness;
            half _Metallic;
            half _OcclusionStrength;
            half _DirtAmount;
            half _HeightInfluence;
            half _CreviceInfluence;
            half _TopInfluence;
            half _PaintedInfluence;
        CBUFFER_END

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);      SAMPLER(sampler_BumpMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_ORMMap);       SAMPLER(sampler_ORMMap);

        // Scene-wide values, set by DirtController.cs
        float  _DirtGroundHeight;
        float  _DirtHeight;
        float  _DirtFalloff;
        float  _DirtCrevice;
        float  _DirtTopDust;
        half4  _DirtColor;
        float  _DirtTiling;
        float  _DirtRoughness;
        float  _DirtNoiseScale;
        float  _DirtBreakup;
        float  _DirtStrength;
        float  _DirtDebug;
        TEXTURE2D(_DirtTex); SAMPLER(sampler_DirtTex);
        ENDHLSL

        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _ORMMAP
            #pragma shader_feature_local_fragment _PAINTED_DIRT

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                float2 lightmapUV : TEXCOORD4;
                float  fogCoord   : TEXCOORD5;
                half   painted    : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // --- Procedural noise (no texture needed) ---------------------
            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(Hash31(i + float3(0,0,0)), Hash31(i + float3(1,0,0)), f.x);
                float b = lerp(Hash31(i + float3(0,1,0)), Hash31(i + float3(1,1,0)), f.x);
                float c = lerp(Hash31(i + float3(0,0,1)), Hash31(i + float3(1,0,1)), f.x);
                float d = lerp(Hash31(i + float3(0,1,1)), Hash31(i + float3(1,1,1)), f.x);
                return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float v = 0.0, amp = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    v += amp * ValueNoise(p);
                    p *= 2.03;
                    amp *= 0.5;
                }
                return v / 0.875;
            }

            // --- Dirt texture projected from 3 sides (no UVs needed) ------
            half3 TriplanarDirt(float3 posWS, float3 nWS)
            {
                float3 w = pow(abs(nWS), 4.0);
                w /= (w.x + w.y + w.z);
                float3 p = posWS * _DirtTiling;
                half3 x = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, p.zy).rgb;
                half3 y = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, p.xz).rgb;
                half3 z = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, p.xy).rgb;
                return x * w.x + y * w.y + z * w.z;
            }

            // --- Where the dirt goes: 0 = clean, 1 = fully dirty ----------
            half DirtMask(float3 posWS, float3 nWS, half occlusion, half painted)
            {
                // 1. Ground band: strong at ground level, fading out upward
                float h = (posWS.y - _DirtGroundHeight) / max(_DirtHeight, 0.001);
                float ground = pow(saturate(1.0 - h), _DirtFalloff) * _HeightInfluence;

                // 2. Corners: anything occluded (wall/ground seams, window frames)
                float corner = saturate((1.0 - occlusion) * _DirtCrevice) * _CreviceInfluence;

                // 3. Dust settled on upward-facing ledges
                float top = smoothstep(0.6, 1.0, nWS.y) * _DirtTopDust * _TopInfluence;

                // 4. Hand-painted dirt (vertex color red), e.g. along wall bases
                float paint = 0.0;
                #if defined(_PAINTED_DIRT)
                    paint = painted * _PaintedInfluence;
                #endif

                float m = max(max(ground, paint), max(corner, top));

                // Break the clean gradient into organic patches
                float n = Fbm(posWS * _DirtNoiseScale);
                m = saturate(m * (1.0 + _DirtBreakup) - n * _DirtBreakup);

                return saturate(m * _DirtStrength * _DirtAmount);
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS, v.tangentOS);

                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = nrm.normalWS;
                o.tangentWS = float4(nrm.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.lightmapUV = v.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                o.fogCoord = ComputeFogFactor(pos.positionCS.z);
                o.painted = v.color.r;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Base material
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);

                #if defined(_ORMMAP)
                    half3 orm = SAMPLE_TEXTURE2D(_ORMMap, sampler_ORMMap, i.uv).rgb;
                    half occlusion = orm.r;
                    half smoothness = 1.0 - orm.g;
                    half metallic = orm.b;
                #else
                    half occlusion = lerp(1.0, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, i.uv).g, _OcclusionStrength);
                    half smoothness = _Smoothness;
                    half metallic = _Metallic;
                #endif

                // Corner detection: URP's Screen Space Ambient Occlusion + the texture AO
                float3 vertexNormal = normalize(i.normalWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half ssao = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    ssao = SampleAmbientOcclusion(screenUV);
                #endif

                half dirt = DirtMask(i.positionWS, vertexNormal, ssao * occlusion, i.painted);

                if (_DirtDebug > 0.5)
                    return half4(dirt.xxx, 1.0);

                // Blend the dirt layer over the material
                half3 dirtColor = _DirtColor.rgb * TriplanarDirt(i.positionWS, vertexNormal);
                half3 albedo = lerp(base.rgb, dirtColor, dirt);
                smoothness = lerp(smoothness, 1.0 - _DirtRoughness, dirt);
                metallic *= 1.0 - dirt;
                normalTS.xy *= 1.0 - dirt * 0.6; // dirt fills in surface detail

                // Lighting
                half3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                half3x3 tbn = half3x3(i.tangentWS.xyz, bitangent, i.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tbn));
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogCoord;
                inputData.normalizedScreenSpaceUV = screenUV;
                #if defined(LIGHTMAP_ON)
                    inputData.bakedGI = SampleLightmap(i.lightmapUV, inputData.normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(i.lightmapUV);
                #else
                    inputData.bakedGI = SampleSH(inputData.normalWS);
                    inputData.shadowMask = unity_ProbesOcclusion;
                #endif

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = metallic;
                surface.smoothness = smoothness;
                surface.normalTS = normalTS;
                surface.occlusion = occlusion;
                surface.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, i.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return posCS;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Needed so URP's SSAO can "see" these objects (that's what finds the corners)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = NormalizeNormalPerPixel(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
