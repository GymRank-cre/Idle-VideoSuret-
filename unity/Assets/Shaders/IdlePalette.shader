Shader "Idle/Palette"
{
    // Matériau unique du décor et des machines (UW-1) : la couleur vient des sommets (alpha = brillance), tout peut donc tenir dans un seul maillage,
    // donc un seul appel de dessin. Éclairage calculé par sommet : soleil adouci, ambiance ciel/sol, reflet doux, et pied des objets assombri (fausse occlusion).
    Properties
    {
        _SpecPower ("Finesse du reflet", Float) = 24
        _SpecStrength ("Force du reflet", Float) = 0.55
        _Wrap ("Adoucissement du soleil", Range(0, 1)) = 0.25
        _FootHeight ("Hauteur du pied assombri (m)", Float) = 0.45
        _FootDark ("Assombrissement du pied", Range(0, 1)) = 0.32
        _FootBase ("Niveau du sol (m)", Float) = 0
        _Flash ("Éclair blanc (changement d'aspect)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Palette"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _SpecPower;
                float _SpecStrength;
                float _Wrap;
                float _FootHeight;
                float _FootDark;
                float _FootBase;
                float _Flash;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                Light light = GetMainLight();

                half3 albedo = input.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    albedo = SRGBToLinear(albedo);       // les couleurs des sommets sont écrites en sRGB
                #endif

                half diffuse = saturate((dot(normalWS, light.direction) + _Wrap) / (1.0 + _Wrap));
                half3 lit = albedo * (SampleSH(normalWS) + light.color * diffuse);

                float3 view = normalize(GetWorldSpaceViewDir(position.positionWS));
                float3 halfway = normalize(light.direction + view);
                lit += light.color * pow(saturate(dot(normalWS, halfway)), _SpecPower) * _SpecStrength * input.color.a;

                // seules les faces verticales s'assombrissent vers le sol : le dessus de la dalle reste clair
                half foot = saturate((position.positionWS.y - _FootBase) / _FootHeight);
                half side = 1.0 - saturate(normalWS.y);
                lit *= 1.0 - _FootDark * (1.0 - foot) * side;

                output.positionCS = position.positionCS;
                output.color = lerp(lit, half3(1.0, 1.0, 1.0), _Flash);
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return half4(input.color, 1.0); }
            ENDHLSL
        }
    }
}
