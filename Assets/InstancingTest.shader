// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/InstancingTest"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white"{}
        _ShadowAmount("ShadowAmount", Float) = 0.5
        _ShadowStrength("ShadowStrength", Float) = 1
        _NormalStrength("HorizontalRoundness", Float) = 2
        _Roundness("VerticalRoundness", Float) = 1
    }
    SubShader
    {
        Tags{ 
            "RenderType"="Opaque"
            "RenderPipeline" = "UniversalPipeline" 
            "LightMode" = "UniversalForwardOnly"
            "Queue"="Geometry"
        }
        LOD 100
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode" = "UniversalForwardOnly"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma shader_feature _ALPHATEST_ON

            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 4.5

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"


            struct v2f
            {
                float4 color : COLOR0;
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _ShadowAmount;
                float _ShadowStrength;
                float _NormalStrength;
                float _Roundness;
                float4 _MainTex_ST;
            CBUFFER_END


            uniform float4x4 _ObjectToWorld;
            uniform StructuredBuffer<float3> _GrassPositions;

            float3x3 rotation(float3 forward, float3 up)
            {
                float3 right = cross(up, forward);
                
                return float3x3(right, up, forward);
            }

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal: NORMAL;
            };

            float4x4 InverseRotation(float4x4 mat)
            {
                float3x3 rotation = (float3x3)mat;
                float3x3 inverseRotation = transpose(rotation);
                float4x4 result;
                    result[0] = float4(inverseRotation[0], 0);
                    result[1] = float4(inverseRotation[1], 0);
                    result[2] = float4(inverseRotation[2], 0);
                    result[3] = float4(0, 0, 0, 1);
                return result;
            }

            v2f vert(VertexInput v, uint svInstanceID : SV_InstanceID)
            {
                v2f o;
                float3 grassPosition = _GrassPositions[svInstanceID];

                //rotate object to always look at camera
                float baseDistance = v.vertex.y;
                float3 objectPos = (mul(_ObjectToWorld,float4(grassPosition, 1)));
                float3 objectDir = objectPos - _WorldSpaceCameraPos;
                objectDir.y = 0;
                objectDir = normalize( objectDir);
                float3x3 vRotation = rotation(objectDir, float3(0,1,0));
                v.vertex =  float4(mul(v.vertex.xyz, vRotation), v.vertex.w); 
                // object to worldspace
                float4 wpos = mul(_ObjectToWorld,  v.vertex + float4( grassPosition, 0.0));
                //o.worldNormal = mul(_ObjectToWorld,float4(v.normal,0));
                float3 lightDir = - GetMainLight().direction;
                lightDir.y = 0;
                float3 additiveNormal = v.vertex;
                additiveNormal.y = 0;
                o.worldNormal = normalize( normalize(-lightDir)-objectDir + (additiveNormal*_NormalStrength) + (v.vertex.y/(v.vertex.y+_Roundness))); //vertex dir should be normalized/2 but if it works skipping it better
                //o.worldNormal = objectDir;// mul(vRotation, v.normal);//mul((float3x3)_ObjectToWorld,mul(vRotation, v.normal));
                //o.worldNormal = normalize(wpos - _WorldSpaceCameraPos);
                //Wind should be changed to a better algorithm
                wpos += sin((_Time*20)+(length(wpos)*0.2)) * baseDistance*baseDistance* 0.2 * float4(1,0,0,0);//*baseDistance;
                
                o.uv = v.uv;
                o.pos = mul(UNITY_MATRIX_VP, wpos);
                o.color = lerp(  float4(0.3, 0.3, 0.3, 1),float4(1, 1, 1,1), baseDistance*baseDistance*baseDistance*_ShadowAmount);
                o.shadowCoord = TransformWorldToShadowCoord(wpos);

                return o;
            }

            float MultiplyByShadowStrength(float shadow)
            {
                return 1-((1-shadow)*_ShadowStrength);
            }

            float4 frag(v2f i) : SV_Target
            {
                Light light = GetMainLight();
                float4 lightColor = float4(LightingLambert(light.color, light.direction, i.worldNormal),1);

                half shadowAttenuation = MainLightRealtimeShadow(i.shadowCoord);;

                float4 texColor = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                

                float4 finalColor = texColor * MultiplyByShadowStrength(lightColor) * MultiplyByShadowStrength(shadowAttenuation);
                //finalColor = float4(LightingLambert(light.color, light.direction, i.worldNormal),1);
                //finalColor = float4(i.worldNormal/2+0.5,1);
                return finalColor;
            }
            

            ENDHLSL
        }
    }
}