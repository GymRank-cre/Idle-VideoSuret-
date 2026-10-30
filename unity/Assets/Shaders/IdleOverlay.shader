Shader "Idle/Overlay"
{
    // Aplat sans éclairage dessiné PAR-DESSUS tout le décor (pas de test de profondeur) : plaques d'achat au sol, jamais cachées par une machine.
    Properties { _BaseColor ("Couleur", Color) = (1, 1, 1, 1) }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+500" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Overlay"
            Tags { "LightMode" = "UniversalForward" }
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
