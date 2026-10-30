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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half3 vertexColor = input.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    vertexColor = SRGBToLinear(vertexColor);   // les couleurs des sommets sont écrites en sRGB
                #endif
                return half4(_BaseColor.rgb * vertexColor, _BaseColor.a);   // l'alpha des sommets sert à la brillance ailleurs : ignoré ici
            }
            ENDHLSL
        }
    }
}
