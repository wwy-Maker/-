// ============================================================
// 灰阶后处理 Shader · 诸子百家·口诛笔伐 Demo
// 版本: v1.1
// 引擎: Unity 2022.3 LTS + URP
// 依据: 灰阶Shader规格 v1.1
// 用途: 全屏灰阶滤镜, 按G键切换, 用于灰阶可辨测试
// ============================================================

Shader "Hidden/Grayscale"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _GrayscaleAmount ("Grayscale Amount", Range(0, 1)) = 1.0
    }
    
    SubShader
    {
        Tags
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        
        LOD 100
        
        Pass
        {
            Name "GrayscalePass"
            
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            float _GrayscaleAmount;
            
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            
            half4 Frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // ITU-R BT.601 标准亮度公式
                // Luminance = 0.299R + 0.587G + 0.114B
                half gray = dot(col.rgb, half3(0.299, 0.587, 0.114));
                
                // 在原色和灰阶之间插值
                col.rgb = lerp(col.rgb, gray.xxx, _GrayscaleAmount);
                
                return col;
            }
            
            ENDHLSL
        }
    }
}
