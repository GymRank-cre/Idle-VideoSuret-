Shader "Idle/ContactShadow"
{
    // Ombres de contact (UW-1) : maillage plat, l'opacité vient des sommets (bords à 0 : ombre douce). Dessinées après le décor opaque, sous tout ce qui est plus haut.
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-200" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ContactShadow"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            CBUFFER_START(UnityPerMaterial)
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
                half4 color = input.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    color.rgb = SRGBToLinear(color.rgb);   // les couleurs des sommets sont écrites en sRGB
                #endif
                return color;
            }
            ENDHLSL
        }
    }
}
