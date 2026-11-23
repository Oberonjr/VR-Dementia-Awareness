// =============================================================================
//  Interior Mapping - URP, VR-ready (Single Pass Instanced / Multiview)
// -----------------------------------------------------------------------------
//  Fakes a 3D room behind a flat window quad. Each eye gets its own correct
//  parallax, so in a headset the room reads as real depth, not a picture.
//
//  HOW THE ROOM TEXTURE MUST LOOK ("pre-projected room"):
//    A straight-on perspective render of a box room, taken from in front of
//    the window. The image edges = the window opening; the back wall is a
//    centered rectangle. "Back Wall Size" = how much of the image width the
//    back wall takes (0.5 = back wall is half the image width).
//
//  CUBEMAP MODE (toggle "Use Cubemap Room"): instead of the 2D image, give it
//    a cubemap of a room captured from the room's centre (Unity reflection
//    probe bake, or a Blender equirect panorama imported as Texture Shape =
//    Cube). The +Z face is the back wall; use "Cubemap Rotation" if not.
//
//  No mesh tangents are needed and object scale doesn't matter - the shader
//  works out the window's size from its UVs, so any glass face with 0-1 UVs
//  works (one room per UV tile; use "Rooms Per UV Tile" for facades).
//
//  HOUSE LAYOUT MODE (toggle "Use House Layout"): the whole front facade's
//    glass is UV'd as ONE 0-1 square (U = left->right, V = bottom->top).
//    The shader splits it into 2 floors x 2 rooms of different widths:
//    "Floor Line" = where the floors meet, "Room Divider" per floor = where
//    the dividing wall sits. All rooms share the same depth (set by
//    Back Wall Size / Cubemap Room Depth, now relative to the facade width),
//    so a narrow hallway reads as a deep corridor. Each house gets its own
//    random lights/mirroring from its world position. Keep Rooms Per UV Tile = 1.
//    With BOTH house layout and cubemap mode on, each room uses its own
//    cubemap (Hallway / Living Room / Bedroom 1 / Bedroom 2) instead of
//    "Room Cubemap". Mirrored houses keep the right cubemap per room.
//
//  CURTAINS / BLINDS (toggle "Use Curtains/Blinds"): a flat layer sitting
//    "Curtain Distance Behind Glass" behind the glass, between reflection and
//    room. Texture: RGB = colour, A = coverage (0 = see through). It fills one
//    room's window opening (0-1). Two atlases (Open / Closed) share one layout;
//    each room's "Closed" checkbox picks which one. Use "Atlas Columns/Rows"
//    for a random style per room; "Windows With Curtains" leaves some bare.
//    With House Layout on, each room has a dropdown (Random / None / a style),
//    and the hallway can be split in two (door + window), each half its own style.
//    The door half can use its own tall "Door Curtains" texture (Door Style).
// =============================================================================
Shader "Custom/URP/InteriorMapping"
{
    Properties
    {
        [Header(Interior Rooms)]
        [Toggle(_INTERIOR_CUBEMAP)] _UseCubemap ("Use Cubemap Room (instead of 2D)", Float) = 0
        [NoScaleOffset] _RoomCube ("Room Cubemap", Cube) = "white" {}
        _CubeDepth ("Cubemap Room Depth (x width)", Range(0.1, 4)) = 1
        _CubeRotation ("Cubemap Rotation Y (deg)", Range(0, 360)) = 0
        [NoScaleOffset] _RoomTex ("Room Texture / Atlas (2D mode)", 2D) = "gray" {}
        _AtlasGrid ("Atlas Columns (X) Rows (Y)", Vector) = (1, 1, 0, 0)
        _RoomsPerWindow ("Rooms Per UV Tile (X, Y)", Vector) = (1, 1, 0, 0)
        _RoomDepth ("Back Wall Size", Range(0.05, 0.95)) = 0.5
        _InteriorBrightness ("Interior Brightness", Range(0, 10)) = 1

        [Header(House Layout 2 floors x 2 rooms)]
        [Toggle(_HOUSE_LAYOUT)] _HouseLayout ("Use House Layout", Float) = 0
        _FloorSplit ("Floor Line (UV height)", Range(0.05, 0.95)) = 0.5
        _GroundSplit ("Ground Floor Room Divider (UV width)", Range(0.05, 0.95)) = 0.25
        _UpperSplit ("Upper Floor Room Divider (UV width)", Range(0.05, 0.95)) = 0.5
        [NoScaleOffset] _HallwayCube  ("Hallway Cubemap (ground, left)",    Cube) = "white" {}
        [NoScaleOffset] _LivingCube   ("Living Room Cubemap (ground, right)", Cube) = "white" {}
        [NoScaleOffset] _Bedroom1Cube ("Bedroom 1 Cubemap (upper, left)",   Cube) = "white" {}
        [NoScaleOffset] _Bedroom2Cube ("Bedroom 2 Cubemap (upper, right)",  Cube) = "white" {}

        [Header(Variety)]
        _LitChance ("Rooms With Lights On", Range(0, 1)) = 1
        _DarkRoomBrightness ("Lights-Off Brightness", Range(0, 1)) = 0.15
        [Toggle] _RandomFlip ("Random Mirror Rooms", Float) = 1
        _Seed ("Random Seed", Float) = 0

        [Header(Curtains and Blinds optional)]
        [Toggle(_CURTAINS_ON)] _CurtainsOn ("Use Curtains/Blinds", Float) = 0
        // Two atlases with the SAME layout (bottom-left Venetian, bottom-right Wooden,
        // top-left Roller, top-right Curtains): one with everything closed, one open.
        [NoScaleOffset] _CurtainClosedTex ("Closed Atlas (RGB, Coverage A)", 2D) = "gray" {}
        [NoScaleOffset] _CurtainOpenTex ("Open Atlas (RGB, Coverage A)", 2D) = "gray" {}
        _CurtainGrid ("Atlas Columns (X) Rows (Y), both atlases", Vector) = (2, 2, 0, 0)
        _CurtainChance ("Windows With Curtains", Range(0, 1)) = 1
        _CurtainDepth ("Curtain Distance Behind Glass", Range(0, 0.2)) = 0.02
        _CurtainTint ("Curtain Tint", Color) = (1, 1, 1, 1)
        _CurtainBrightness ("Curtain Brightness", Range(0, 4)) = 1
        // Per-room pick (House Layout only). Style numbers follow the atlas cells:
        // bottom-left, bottom-right, top-left, top-right of the curtain texture.
        [Header(Curtains per Room House Layout)]
        [Enum(Random,0,None,1,Venetian Blinds,2,Wooden Blinds,3,Roller Blind,4,Curtains,5)] _HallwayCurtain ("Hallway Window Style", Float) = 0
        [ToggleUI] _HallwayCurtainClosed ("Hallway Closed", Float) = 0
        [Enum(Random,0,None,1,Venetian Blinds,2,Wooden Blinds,3,Roller Blind,4,Curtains,5)] _LivingCurtain ("Living Room Style", Float) = 0
        [ToggleUI] _LivingCurtainClosed ("Living Room Closed", Float) = 0
        [Enum(Random,0,None,1,Venetian Blinds,2,Wooden Blinds,3,Roller Blind,4,Curtains,5)] _Bedroom1Curtain ("Bedroom 1 Style", Float) = 0
        [ToggleUI] _Bedroom1CurtainClosed ("Bedroom 1 Closed", Float) = 0
        [Enum(Random,0,None,1,Venetian Blinds,2,Wooden Blinds,3,Roller Blind,4,Curtains,5)] _Bedroom2Curtain ("Bedroom 2 Style", Float) = 0
        [ToggleUI] _Bedroom2CurtainClosed ("Bedroom 2 Closed", Float) = 0

        [Header(Hallway Door and Window Split)]
        [Toggle] _HallwaySplit ("Split Hallway in 2 (door + window)", Float) = 0
        _HallwaySplitPos ("Hallway Split Position", Range(0.05, 0.95)) = 0.5
        [Enum(Left,0,Right,1)] _DoorSide ("Door Is On Hallway Half", Float) = 0
        // Door pane: its own tall texture (4 styles side by side). The other half
        // (the window) uses "Hallway Window Style" above.
        [NoScaleOffset] _DoorCurtainTex ("Door Curtains RGB, Coverage A (4 side by side)", 2D) = "gray" {}
        [Enum(Off use fallback below,0,Sash Curtain,1,Straight Curtain,2,Frosted Glass,3,Pleated Blind,4)] _DoorStyle ("Door Style", Float) = 0
        [Enum(Random,0,None,1,Venetian Blinds,2,Wooden Blinds,3,Roller Blind,4,Curtains,5)] _HallwayCurtainR ("Door Fallback (when Door Style Off)", Float) = 1

        [Header(Glass)]
        _GlassTint ("Glass Tint", Color) = (0.92, 0.96, 1, 1)
        _Reflectivity ("Reflection (head-on)", Range(0, 1)) = 0.1
        _GlassSmoothness ("Glass Smoothness", Range(0, 1)) = 0.95

        [Header(Frame Overlay optional)]
        [Toggle(_FRAME_ON)] _FrameOn ("Use Frame Overlay", Float) = 0
        [NoScaleOffset] _FrameTex ("Frame/Dirt RGB, Coverage A", 2D) = "white" {}
        _FrameColor ("Frame Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Everything in one constant buffer = SRP Batcher compatible.
        CBUFFER_START(UnityPerMaterial)
            float4 _AtlasGrid;
            float4 _RoomsPerWindow;
            half   _RoomDepth;
            half   _InteriorBrightness;
            half   _LitChance;
            half   _DarkRoomBrightness;
            half   _RandomFlip;
            float  _Seed;
            half4  _GlassTint;
            half   _Reflectivity;
            half   _GlassSmoothness;
            half4  _FrameColor;
            half   _CubeDepth;
            half   _CubeRotation;
            half4  _RoomCube_HDR;   // set by Unity; decodes HDR / RGBM cubemaps
            float  _FloorSplit;
            float  _GroundSplit;
            float  _UpperSplit;
            half4  _HallwayCube_HDR;
            half4  _LivingCube_HDR;
            half4  _Bedroom1Cube_HDR;
            half4  _Bedroom2Cube_HDR;
            float4 _CurtainGrid;
            half   _CurtainChance;
            half   _CurtainDepth;
            half4  _CurtainTint;
            half   _CurtainBrightness;
            half   _HallwayCurtain;
            half   _HallwayCurtainR;
            half   _HallwaySplit;
            float  _HallwaySplitPos;
            half   _LivingCurtain;
            half   _Bedroom1Curtain;
            half   _Bedroom2Curtain;
            half   _HallwayCurtainClosed;
            half   _LivingCurtainClosed;
            half   _Bedroom1CurtainClosed;
            half   _Bedroom2CurtainClosed;
            half   _DoorSide;
            half   _DoorStyle;
        CBUFFER_END
        ENDHLSL

        // ---------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _FRAME_ON
            #pragma shader_feature_local _INTERIOR_CUBEMAP
            #pragma shader_feature_local _HOUSE_LAYOUT
            #pragma shader_feature_local _CURTAINS_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            // Reflection probes (incl. Forward+ / cluster path used by the Quest build)
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_RoomTex);  SAMPLER(sampler_RoomTex);
            TEXTURE2D(_FrameTex); SAMPLER(sampler_FrameTex);
            TEXTURE2D(_CurtainClosedTex); SAMPLER(sampler_CurtainClosedTex);
            TEXTURE2D(_CurtainOpenTex);   SAMPLER(sampler_CurtainOpenTex);
            TEXTURE2D(_DoorCurtainTex); SAMPLER(sampler_DoorCurtainTex);
            TEXTURECUBE(_RoomCube); SAMPLER(sampler_RoomCube);
            TEXTURECUBE(_HallwayCube);  SAMPLER(sampler_HallwayCube);
            TEXTURECUBE(_LivingCube);   SAMPLER(sampler_LivingCube);
            TEXTURECUBE(_Bedroom1Cube); SAMPLER(sampler_Bedroom1Cube);
            TEXTURECUBE(_Bedroom2Cube); SAMPLER(sampler_Bedroom2Cube);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Cheap, stable 2D hash (Dave Hoskins) - random value per room.
            float2 Hash22(float2 p)
            {
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = IN.uv * _RoomsPerWindow.xy;
                OUT.fogFactor  = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN); // per-eye camera position in VR

                float3 N     = normalize(IN.normalWS);
                float3 ray   = IN.positionWS - GetCameraPositionWS(); // eye -> pixel
                float2 uv    = IN.uv;
                float2 duvdx = ddx(uv);
                float2 duvdy = ddy(uv);

                // --- 1. Express the view ray in "room space" ----------------------
                // Build the surface's UV directions from screen derivatives, so the
                // shader knows how big one room is in metres (no tangents needed).
                float3 dpdx = ddx(ray);
                float3 dpdy = ddy(ray);
                float3 dpdyPerp = cross(dpdy, N);
                float3 dpdxPerp = cross(N, dpdx);
                float3 gradU = dpdyPerp * duvdx.x + dpdxPerp * duvdy.x;
                float3 gradV = dpdyPerp * duvdx.y + dpdxPerp * duvdy.y;
                float  flip  = dot(cross(dpdx, dpdy), N) >= 0 ? 1.0 : -1.0; // mirrored UVs

                float3 rayTS = float3(dot(ray, gradU) * flip,
                                      dot(ray, gradV) * flip,
                                      dot(ray, N) * length(gradU));
                rayTS.z = min(rayTS.z, -1e-6);                      // always into the room
                float2 sgn = step(0.0, rayTS.xy) * 2.0 - 1.0;
                rayTS.xy = max(abs(rayTS.xy), 1e-6) * sgn;          // avoid divide-by-zero

                // --- 2. Pick a room for this tile -----------------------------------
                float2 roomUV    = frac(uv);
                float2 roomId    = floor(uv);
                float2 cellScale = 1.0;   // room size as a fraction of the UV tile
                int    roomSlot  = 0;     // house layout: 0 hall, 1 living, 2 bed 1, 3 bed 2

            #if defined(_HOUSE_LAYOUT)
                // Split the facade tile into 2 floors, each with its own divider.
                float  upper  = step(_FloorSplit, roomUV.y);            // 0 = ground, 1 = upper
                float  divide = upper > 0.5 ? _UpperSplit : _GroundSplit;
                float  right  = step(divide, roomUV.x);                 // 0 = left room, 1 = right room
                float2 cellMin = float2(right * divide, upper * _FloorSplit);
                cellScale = float2(right > 0.5 ? 1.0 - divide : divide,
                                   upper > 0.5 ? 1.0 - _FloorSplit : _FloorSplit);
                roomUV = (roomUV - cellMin) / cellScale;
                // Room slot is tied to the UV position, so mirrored houses keep
                // the hallway cubemap in the hallway automatically.
                roomSlot = (int)(right + upper * 2.0);
                // Narrow room = ray crosses its width faster; depth stays shared.
                rayTS.xy /= cellScale;
                // Unique id per room AND per house (house = object position).
                float3 housePos = GetObjectToWorldMatrix()._m03_m13_m23;
                roomId = roomId * 2.0 + float2(right, upper) + housePos.xz * 0.37 + housePos.y * 0.11;
            #endif

                float2 rndA   = Hash22(roomId + _Seed * 17.13);
                float2 rndB   = Hash22(roomId + _Seed * 17.13 + 41.7);
                float2 cell   = min(floor(rndA * _AtlasGrid.xy), _AtlasGrid.xy - 1.0);

                half roomLight = (rndB.y < _LitChance) ? 1.0h : _DarkRoomBrightness;
                half3 interior;

            #if defined(_CURTAINS_ON)
                // --- 2b. Curtains / blinds: a flat layer just behind the glass -----
                // Follow the view ray a short distance into the room, so the
                // curtain shifts slightly with your head (real depth in VR).
                // Done here, before step 3 changes rayTS in 2D mode.
                float2 curtainUV  = roomUV + rayTS.xy * (_CurtainDepth / -rayTS.z);
                // Outside the window opening there is no curtain (seen at steep angles).
                half   curtainMask = (half)(all(curtainUV >= 0.0) && all(curtainUV <= 1.0));

                // Which style this room asked for: 0 Random, 1 None, 2+ = atlas cell 0, 1, 2...
                half   style   = 0;     // 0 Random, 1 None, 2 Venetian, 3 Roller, 4 Curtains
                half   closed  = 0;     // 0 = open atlas, 1 = closed atlas
                float  halfId  = 0;     // hallway split: 0 = left half, 1 = right half
                float  stretch = 1.0;   // how much narrower the texture is drawn (for gradients)
                float  useDoor = 0;     // 1 = this pixel is the door pane with its own texture
            #if defined(_HOUSE_LAYOUT)
                style = roomSlot == 1 ? _LivingCurtain : (roomSlot == 2 ? _Bedroom1Curtain
                      : (roomSlot == 3 ? _Bedroom2Curtain : _HallwayCurtain));
                closed = roomSlot == 1 ? _LivingCurtainClosed : (roomSlot == 2 ? _Bedroom1CurtainClosed
                       : (roomSlot == 3 ? _Bedroom2CurtainClosed : _HallwayCurtainClosed));
                if (roomSlot == 0 && _HallwaySplit > 0.5)
                {
                    // Split is fixed to the geometry (door stays the door), so it is
                    // done before any random mirroring. Each half shows a whole style.
                    halfId     = step(_HallwaySplitPos, curtainUV.x);
                    float hMin = halfId * _HallwaySplitPos;
                    float hW   = halfId > 0.5 ? 1.0 - _HallwaySplitPos : _HallwaySplitPos;
                    curtainUV.x = (curtainUV.x - hMin) / hW;
                    stretch    = 1.0 / hW;
                    float isDoorHalf = (float)(abs(halfId - _DoorSide) < 0.5);
                    // Window half = "Hallway Window Style"; door half = door texture,
                    // or the fallback room style when Door Style is Off.
                    style      = isDoorHalf > 0.5 ? _HallwayCurtainR : _HallwayCurtain;
                    useDoor    = (float)(_DoorStyle > 0.5 && isDoorHalf > 0.5);
                }
            #endif
                float2 rndC = Hash22(roomId + _Seed * 17.13 + 83.1 + halfId * 19.7);  // random cell
                float2 rndD = Hash22(roomId + _Seed * 17.13 + 127.3);                 // curtains yes/no
                if (_RandomFlip > 0.5 && rndB.x < 0.5) curtainUV.x = 1.0 - curtainUV.x;

                // Both atlases: cell 0 bottom-left Venetian, 1 bottom-right Wooden,
                // 2 top-left Roller, 3 top-right Curtains. "Closed" picks which atlas.
                float2 curtainCell = 0;
                if (useDoor > 0.5)      // Door pane: always shown, style from the door dropdown
                {
                    curtainCell = float2(min(_DoorStyle - 1.0, 3.0), 0.0);
                }
                else
                {
                    float cells = _CurtainGrid.x * _CurtainGrid.y;
                    float k;
                    if (style < 0.5)    // Random type (respects "Windows With Curtains")
                    {
                        k = floor(rndC.x * cells);
                        curtainMask *= (half)(rndD.x < _CurtainChance);
                    }
                    else                // None, or a fixed type
                    {
                        k = style - 2.0;
                        curtainMask *= (half)(style > 1.5);
                    }
                    k = clamp(k, 0.0, cells - 1.0);
                    curtainCell = float2(fmod(k, _CurtainGrid.x), floor(k / _CurtainGrid.x));
                }
                // Door texture = 4 tall cells side by side; room texture = _CurtainGrid.
                float2 grid         = useDoor > 0.5 ? float2(4.0, 1.0) : _CurtainGrid.xy;
                float2 curtainAtlas = (curtainCell + clamp(curtainUV, 0.002, 0.998)) / grid;
                float2 cGradScale   = float2(stretch, 1.0) / (grid * cellScale);
                float2 cDx = duvdx * cGradScale;
                float2 cDy = duvdy * cGradScale;
                half4  curtain;
                // Only ONE of the three textures is read per pixel.
                [branch] if (useDoor > 0.5)
                    curtain = SAMPLE_TEXTURE2D_GRAD(_DoorCurtainTex, sampler_DoorCurtainTex, curtainAtlas, cDx, cDy);
                else if (closed > 0.5)
                    curtain = SAMPLE_TEXTURE2D_GRAD(_CurtainClosedTex, sampler_CurtainClosedTex, curtainAtlas, cDx, cDy);
                else
                    curtain = SAMPLE_TEXTURE2D_GRAD(_CurtainOpenTex, sampler_CurtainOpenTex, curtainAtlas, cDx, cDy);
            #endif

            #if defined(_INTERIOR_CUBEMAP)
                // --- 3a. CUBEMAP: trace a box room, look up the wall it hits ------
                // Box spans -1..1 on every axis; front face (the glass) is z = -1.
                // Dividing the ray's depth stretches the cube into a deeper/shallower room.
                float3 cubeRay = float3(rayTS.xy, -rayTS.z / _CubeDepth);
                float3 cubePos = float3(roomUV * 2.0 - 1.0, -1.0);
                float3 cubeInv = 1.0 / cubeRay;
                float3 cubeK   = abs(cubeInv) - cubePos * cubeInv;
                float3 hit     = cubePos + min(min(cubeK.x, cubeK.y), cubeK.z) * cubeRay;

                // Direction from room centre to the hit point = cubemap lookup.
                // (x = right, y = up, z = into the room -> Unity's +Z face is the back wall)
                if (_RandomFlip > 0.5 && rndB.x < 0.5) hit.x = -hit.x;
                float s, c;
                sincos(radians(_CubeRotation), s, c);
                hit.xz = float2(hit.x * c - hit.z * s, hit.x * s + hit.z * c);

              #if defined(_HOUSE_LAYOUT)
                // One cubemap per room. Gradients are taken here, outside the
                // branch, so each room only pays for ONE cubemap read (Quest-friendly)
                // and filtering matches the single-cubemap mode exactly.
                float3 hitDx = ddx(hit);
                float3 hitDy = ddy(hit);
                half4  cubeSample;
                half4  cubeHDR;
                [branch] if (roomSlot == 0)
                {
                    cubeSample = _HallwayCube.SampleGrad(sampler_HallwayCube, hit, hitDx, hitDy);
                    cubeHDR    = _HallwayCube_HDR;
                }
                else if (roomSlot == 1)
                {
                    cubeSample = _LivingCube.SampleGrad(sampler_LivingCube, hit, hitDx, hitDy);
                    cubeHDR    = _LivingCube_HDR;
                }
                else if (roomSlot == 2)
                {
                    cubeSample = _Bedroom1Cube.SampleGrad(sampler_Bedroom1Cube, hit, hitDx, hitDy);
                    cubeHDR    = _Bedroom1Cube_HDR;
                }
                else
                {
                    cubeSample = _Bedroom2Cube.SampleGrad(sampler_Bedroom2Cube, hit, hitDx, hitDy);
                    cubeHDR    = _Bedroom2Cube_HDR;
                }
              #else
                half4 cubeSample = SAMPLE_TEXTURECUBE(_RoomCube, sampler_RoomCube, hit);
                half4 cubeHDR    = _RoomCube_HDR;
              #endif
                half4 hdrDecode  = cubeHDR.x > 0 ? cubeHDR : half4(1, 1, 0, 0);
                interior = DecodeHDREnvironment(cubeSample, hdrDecode);
            #else
                // --- 3b. 2D: ray-box intersection (B. Golus' pre-projected method) --
                float farFrac    = _RoomDepth;
                float depthScale = 1.0 / (1.0 - farFrac) - 1.0;

                float3 pos = float3(roomUV * 2.0 - 1.0, -1.0);
                rayTS.z *= -depthScale;
                float3 invRay = 1.0 / rayTS;
                float3 k = abs(invRay) - pos * invRay;
                float  kMin = min(min(k.x, k.y), k.z);
                pos += kMin * rayTS;

                // Undo the perspective baked into the room image.
                float interp = pos.z * 0.5 + 0.5;
                float realZ  = saturate(interp) / depthScale + 1.0;
                interp = 1.0 - (1.0 / realZ);
                interp *= depthScale + 1.0;

                float2 interiorUV = pos.xy * lerp(1.0, farFrac, interp) * 0.5 + 0.5;
                if (_RandomFlip > 0.5 && rndB.x < 0.5) interiorUV.x = 1.0 - interiorUV.x;
                interiorUV = clamp(interiorUV, 0.002, 0.998); // keep inside atlas cell

                // Sample with the surface's gradients -> no mip seams, stable in VR.
                float2 atlasUV = (cell + interiorUV) / _AtlasGrid.xy;
                interior = SAMPLE_TEXTURE2D_GRAD(_RoomTex, sampler_RoomTex, atlasUV,
                                    duvdx / (_AtlasGrid.xy * cellScale), duvdy / (_AtlasGrid.xy * cellScale)).rgb;
            #endif

            #if defined(_CURTAINS_ON)
                // Curtain covers the room where its alpha says so. It is lit by the
                // room light below, so lights-off rooms get dark curtains.
                interior = lerp(interior, curtain.rgb * _CurtainTint.rgb * _CurtainBrightness,
                                curtain.a * _CurtainTint.a * curtainMask);
            #endif

                interior *= _InteriorBrightness * roomLight * _GlassTint.rgb;

                // --- 4. Glass reflection (reflection probe / skybox + Fresnel) ------
                float3 V = normalize(-ray);
                half NdotV   = saturate(dot(N, V));
                half fresnel = _Reflectivity + (1.0h - _Reflectivity) * pow(1.0h - NdotV, 5.0h);
                half3 refl   = GlossyEnvironmentReflection(reflect(-V, N), IN.positionWS,
                                    1.0h - _GlassSmoothness, 1.0h, GetNormalizedScreenSpaceUV(IN.positionCS));
                half3 color  = lerp(interior, refl, fresnel);

                // --- 5. Optional frame / dirt overlay, simply lit -------------------
                #if defined(_FRAME_ON)
                    half4 frame = SAMPLE_TEXTURE2D_GRAD(_FrameTex, sampler_FrameTex, roomUV, duvdx / cellScale, duvdy / cellScale) * _FrameColor;
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                    half3 lit = mainLight.color * saturate(dot(N, mainLight.direction))
                              * mainLight.shadowAttenuation * mainLight.distanceAttenuation
                              + SampleSH(N);
                    color = lerp(color, frame.rgb * lit, frame.a);
                #endif

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // Depth prepass support (needed when URP renders a depth texture first).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                DepthVaryings OUT = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(DepthVaryings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
