// ============================================================
// 色盲模式后处理 Shader · 诸子百家·口诛笔伐 Demo
// 版本: v1.0
// 引擎: Unity 2022.3 LTS + URP
// 用途: 色盲模拟与色盲友好配色替换 (可访问性 Standard 级)
// 支持类型: 红色盲(Protanopia), 绿色盲(Deuteranopia), 蓝色盲(Tritanopia)
// ============================================================

Shader "Hidden/ColorBlindness"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Mode ("Color Blindness Mode", Float) = 0
        // 0 = Normal, 1 = Protanopia, 2 = Deuteranopia, 3 = Tritanopia
        // 4 = Protanopia Simulation (用于测试), 5 = Deuteranopia Simulation
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
            Name "ColorBlindnessPass"
            
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
            
            float _Mode;
            
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            
            // 色盲模拟矩阵 (基于 Brettel et al. 1997)
            // 红色盲 (Protanopia) — 缺失L锥
            static const half3x3 ProtanopiaMatrix = half3x3(
                0.567, 0.433, 0.000,
                0.558, 0.442, 0.000,
                0.000, 0.242, 0.758
            );
            
            // 绿色盲 (Deuteranopia) — 缺失M锥
            static const half3x3 DeuteranopiaMatrix = half3x3(
                0.625, 0.375, 0.000,
                0.700, 0.300, 0.000,
                0.000, 0.300, 0.700
            );
            
            // 蓝色盲 (Tritanopia) — 缺失S锥
            static const half3x3 TritanopiaMatrix = half3x3(
                0.950, 0.050, 0.000,
                0.000, 0.433, 0.567,
                0.000, 0.475, 0.525
            );
            
            half4 Frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                if (_Mode < 0.5)
                {
                    // Normal — 不变换
                    return col;
                }
                else if (_Mode < 1.5)
                {
                    // Protanopia
                    col.rgb = mul(ProtanopiaMatrix, col.rgb);
                }
                else if (_Mode < 2.5)
                {
                    // Deuteranopia
                    col.rgb = mul(DeuteranopiaMatrix, col.rgb);
                }
                else if (_Mode < 3.5)
                {
                    // Tritanopia
                    col.rgb = mul(TritanopiaMatrix, col.rgb);
                }
                
                return col;
            }
            
            ENDHLSL
        }
    }
}
