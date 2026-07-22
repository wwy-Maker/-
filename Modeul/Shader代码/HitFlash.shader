// ============================================================
// 命中闪烁 Shader · 诸子百家·口诛笔伐 Demo
// 版本: v1.0
// 引擎: Unity 2022.3 LTS + URP
// 用途: 受击对象颜色覆盖 (白→红→原色, 50ms)
// 说明: 此Shader为备选方案, 优先使用SpriteRenderer.color协程方案
//       当需要更精细的视觉控制(如局部闪烁)时使用此Shader
// ============================================================

Shader "Custom/HitFlash"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        // 0 = 显示原色, 1 = 完全显示闪烁色
    }
    
    SubShader
    {
        Tags
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Name "HitFlashPass"
            
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            half4 _BaseColor;
            half4 _FlashColor;
            half _FlashAmount;
            
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            
            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                baseCol *= _BaseColor * input.color;
                
                // 在原色和闪烁色之间插值
                half4 finalCol = lerp(baseCol, _FlashColor, _FlashAmount);
                
                return finalCol;
            }
            
            ENDHLSL
        }
    }
}
