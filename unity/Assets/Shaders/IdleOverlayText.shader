Shader "Idle/OverlayText"
{
    // Texte (TextMesh) dessiné par-dessus tout le décor, avec son CONTOUR calculé dans le shader (un seul maillage par texte au lieu
    // de cinq copies décalées) : couleur du sommet pour le texte, _OutlineColor pour le contour, alpha de la texture de la police.
    Properties
    {
        _MainTex ("Police", 2D) = "white" {}
        _OutlineColor ("Contour", Color) = (0.055, 0.086, 0.125, 1)
        _OutlineWidth ("Épaisseur du contour (texels)", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+500" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "OverlayText"
            Tags { "LightMode" = "UniversalForward" }
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            half Alpha(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }
            // 12 directions : un contour rond, sans marches d'escalier
            static const float2 Ring[12] =
            {
                float2(1.0, 0.0), float2(0.866, 0.5), float2(0.5, 0.866), float2(0.0, 1.0), float2(-0.5, 0.866), float2(-0.866, 0.5),
                float2(-1.0, 0.0), float2(-0.866, -0.5), float2(-0.5, -0.866), float2(0.0, -1.0), float2(0.5, -0.866), float2(0.866, -0.5)
            };
            half4 frag(Varyings input) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * _OutlineWidth;
                half a0 = Alpha(input.uv);
                half outline = a0;
                for (int i = 0; i < 12; i++) outline = max(outline, Alpha(input.uv + Ring[i] * d));
                half3 rgb = lerp(_OutlineColor.rgb, input.color.rgb, saturate(a0 * 2));
                return half4(rgb, outline * input.color.a);
            }
            ENDHLSL
        }
    }
}
