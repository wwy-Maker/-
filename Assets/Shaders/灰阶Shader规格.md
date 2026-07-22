# 灰阶 Shader 规格 · 诸子百家·口诛笔伐 Demo

**版本**: v1.1
**日期**: 2026-07-09
**作者**: 林绘澄（美术方向 / 技术美术）
**依据**: 灰模资产规格清单 v1.0 §2.3 · 可访问性分级 v1.0 §1.1/§2.1 · 主架构文档 v1.0 · ADR-003 弹幕系统数据化
**引擎**: Unity 2022.3 LTS + URP (Universal Render Pipeline)
**状态**: 待工程实现
**变更**: v1.1 新增 §12 色盲模式方案；更新 §8.3 和 §10 待确认项

---

## 0. 方案概述

### 0.1 需求来源

| 来源 | 要求 |
|------|------|
| GDD §13 灰阶可辨测试标准 | Demo 必须通过灰阶模式游玩——去掉所有颜色后，玩家仅靠形状和亮度区分弹幕类型 |
| 可访问性分级 Basic §1.1 | 灰阶可辨模式为 P0 硬约束。按 G 键一键切换灰阶/彩色 |
| 灰模资产规格清单 §2.3 | 灰阶模式实现：后处理 Grayscale 滤镜或遍历 SpriteRenderer 替换灰阶色 |
| 灰模资产规格清单 §4.6 | 方案A（推荐）：后处理 Grayscale 滤镜；方案B：遍历替换 |

### 0.2 方案选型

| 方案 | 实现方式 | 优点 | 缺点 | 采用 |
|------|---------|------|------|------|
| **A. URP Renderer Feature + Blit** | 全屏后处理，在渲染完成后对整个画面应用灰阶 Shader | 一键切换、零逻辑侵入、性能好(单次全屏 Blit) | HUD 文字也会被灰阶化（需额外处理） | ✅ **主方案** |
| B. 逐 SpriteRenderer 替换 | 遍历所有 SpriteRenderer，将 color 替换为灰阶值 | 可精确控制哪些对象被灰阶化 | 性能差(遍历几百个对象)、侵入逻辑层、违反渲染/逻辑分离 | ❌ |
| C. 逐 Sprite Shader 替换 | 每个材质换用灰阶 Shader | 精确控制 | 材质切换开销大、管理复杂 | ❌ |

**选 A 的理由**：
1. URP Renderer Feature 是 2022.3 LTS 原生功能，无需额外插件
2. 全屏 Blit 只增加 1 个 Draw Call，性能影响可忽略
3. 灰阶是渲染层操作，不应侵入 Gameplay 逻辑层（符合主架构分层规则）
4. 一键切换（按 G 键启用/禁用 Renderer Feature），无需修改任何游戏对象

---

## 1. Shader 类型与架构

### 1.1 整体架构

```
┌─────────────────────────────────────────────────────┐
│                   URP Render Pipeline                │
│                                                       │
│  ┌──────────┐  ┌──────────┐  ┌───────────────────┐  │
│  │ Render   │→ │ Render   │→ │ Grayscale Renderer │  │
│  │ Opaque   │  │ Transparent│ │ Feature (后处理)  │  │
│  │ Objects  │  │ Objects   │  │                   │  │
│  └──────────┘  └──────────┘  └────────┬──────────┘  │
│                                        │              │
│                          ┌─────────────▼───────────┐ │
│                          │ Blit Material            │ │
│                          │ (Grayscale.shader)       │ │
│                          │ Luminance = 0.299R       │ │
│                          │ + 0.587G + 0.114B        │ │
│                          └─────────────┬───────────┘ │
│                                        │              │
│                          ┌─────────────▼───────────┐ │
│                          │ Final Output             │ │
│                          │ (灰阶画面 or 彩色画面)    │ │
│                          └─────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

### 1.2 组件清单

| 组件 | 文件 | 路径 | 说明 |
|------|------|------|------|
| 灰阶 Shader | `Grayscale.shader` | `Assets/_Project/Art/Shaders/Grayscale.shader` | HLSL 灰阶计算 |
| Blit 材质 | `GrayscaleBlit.mat` | `Assets/_Project/Art/Materials/GrayscaleBlit.mat` | 引用灰阶 Shader 的材质 |
| Renderer Feature | `GrayscaleRendererFeature.cs` | `Assets/_Project/Scripts/Rendering/GrayscaleRendererFeature.cs` | URP Renderer Feature 脚本 |
| 切换控制器 | `GrayscaleToggle.cs` | `Assets/_Project/Scripts/UI/GrayscaleToggle.cs` | 按 G 键切换灰阶开关 |

---

## 2. 灰阶公式

### 2.1 标准灰阶公式

```
Luminance = 0.299 × R + 0.587 × G + 0.114 × B
```

| 系数 | 值 | 说明 |
|------|-----|------|
| R 权重 | 0.299 | 人眼对红色敏感度最低 |
| G 权重 | 0.587 | 人眼对绿色敏感度最高 |
| B 权重 | 0.114 | 人眼对蓝色敏感度中等 |

这是 ITU-R BT.601 标准的亮度公式，适用于 sRGB 色彩空间。

### 2.2 灰阶值映射验证

基于灰模资产规格清单 §2.1 颜色编码表：

| 学派 | 颜色 | HEX | RGB | 灰阶值(0.299R+0.587G+0.114B) | 灰阶HEX |
|------|------|-----|-----|-------------------------------|---------|
| 儒家 | 儒金 | #D4A017 | (212,160,23) | 0.299×212+0.587×160+0.114×23 = 63.4+93.9+2.6 = **159** | #9F9F9F |
| 法家 | 法黑 | #1A1A1A | (26,26,26) | 0.299×26+0.587×26+0.114×26 = 7.8+15.3+3.0 = **26** | #1A1A1A |
| 道家 | 道青 | #2E8B8B | (46,139,139) | 0.299×46+0.587×139+0.114×139 = 13.8+81.6+15.8 = **111** | #6F6F6F |
| 无学派 | 素白 | #FFFFFF | (255,255,255) | = **255** | #FFFFFF |

**灰阶值差异分析**：

| 对比对 | 灰阶差 | 可辨度 | 结论 |
|--------|--------|--------|------|
| 儒金(159) vs 道青(111) | 48 | 中等 | 灰阶下有差异但不够明显——**必须靠形状区分**（圆形 vs 弧线段） |
| 儒金(159) vs 法黑(26) | 133 | 极高 | 灰阶下极易区分 ✅ |
| 道青(111) vs 法黑(26) | 85 | 高 | 灰阶下易区分 ✅ |
| 素白(255) vs 儒金(159) | 96 | 高 | 灰阶下易区分 ✅ |
| 素白(255) vs 法黑(26) | 229 | 极高 | 灰阶下极易区分 ✅ |

**关键结论**：儒金和道青的灰阶差异(48)不足以靠亮度区分——这是 GDD §13 要求形状编码为硬约束的根本原因。灰阶 Shader 只负责去色，形状可辨由 SpriteGenerator 保证。

---

## 3. Shader 代码

### 3.1 Grayscale.shader（ShaderLab + HLSL）

```hlsl
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
                float2 uv         : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            float _GrayscaleAmount;
            
            Varyings Vert(Attributes input)
            {
                Varyings output;
                
                // URP Blit 用的顶点变换：直接从 UV 构建裁剪空间坐标
                output.positionCS = TransformUVToClipSpace(input.uv);
                output.uv = input.uv;
                
                return output;
            }
            
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // ITU-R BT.601 亮度公式
                half luminance = dot(color.rgb, half3(0.299, 0.587, 0.114));
                
                // 在原色和灰阶之间插值
                // _GrayscaleAmount = 1.0 → 完全灰阶
                // _GrayscaleAmount = 0.0 → 原色（彩色模式）
                color.rgb = lerp(color.rgb, luminance.xxx, _GrayscaleAmount);
                
                return color;
            }
            
            ENDHLSL
        }
    }
}
```

> **注意**：`TransformUVToClipSpace` 是 URP 的辅助函数。如果该函数在当前 URP 版本中不可用，可替换为标准的全屏三角形顶点着色器：

```hlsl
// 备选顶点着色器（全屏三角形，无需顶点缓冲区）
Varyings Vert(uint vertexID : SV_VertexID)
{
    Varyings output;
    
    // 全屏三角形：3 个顶点覆盖整个屏幕
    output.positionCS = float4(
        (float)(vertexID / 2) * 4.0 - 1.0,
        (float)(vertexID % 2) * 4.0 - 1.0,
        0.0,
        1.0
    );
    output.uv = float2(
        (float)(vertexID / 2) * 2.0,
        1.0 - (float)(vertexID % 2) * 2.0
    );
    
    return output;
}
```

### 3.2 GrayscaleRendererFeature.cs

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature：灰阶后处理。
/// 在 URP Renderer 的 RenderTransparentObjects 之后执行全屏 Blit，
/// 将画面转为灰阶。
/// 
/// 启用/禁用：通过 GrayscaleToggle 脚本按 G 键切换。
/// 也可通过 Renderer Feature 的 inspector 勾选框手动切换。
/// </summary>
public class GrayscaleRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class GrayscaleSettings
    {
        public string passName = "Grayscale Pass";
        public bool isEnabled = false;          // 默认关闭（彩色模式）
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public Material grayscaleMaterial;      // 引用 GrayscaleBlit.mat
    }
    
    public GrayscaleSettings settings = new GrayscaleSettings();
    
    private GrayscaleRenderPass _grayscalePass;
    
    public override void Create()
    {
        _grayscalePass = new GrayscaleRenderPass(settings);
    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, 
                                          ref RenderingData renderingData)
    {
        if (settings.isEnabled && settings.grayscaleMaterial != null)
        {
            _grayscalePass.Setup(renderer.cameraColorTarget);
            renderer.EnqueuePass(_grayscalePass);
        }
    }
    
    /// <summary>
    /// 运行时切换灰阶开关。由 GrayscaleToggle 调用。
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        settings.isEnabled = enabled;
    }
}

/// <summary>
/// 灰阶 Blit Render Pass。
/// </summary>
public class GrayscaleRenderPass : ScriptableRenderPass
{
    private GrayscaleRendererFeature.GrayscaleSettings _settings;
    private RenderTargetIdentifier _cameraColorTarget;
    private RenderTargetHandle _tempTexture;
    
    private static readonly int GrayscaleAmountID = Shader.PropertyToID("_GrayscaleAmount");
    
    public GrayscaleRenderPass(GrayscaleRendererFeature.GrayscaleSettings settings)
    {
        _settings = settings;
        renderPassEvent = settings.renderPassEvent;
        _tempTexture.Init("_GrayscaleTempTexture");
    }
    
    public void Setup(RenderTargetIdentifier cameraColorTarget)
    {
        _cameraColorTarget = cameraColorTarget;
    }
    
    public override void Execute(ScriptableRenderContext context, 
                                  ref RenderingData renderingData)
    {
        if (_settings.grayscaleMaterial == null)
            return;
        
        CommandBuffer cmd = CommandBufferPool.Get(_settings.passName);
        
        // 获取相机描述
        ref var cameraData = ref renderingData.cameraData;
        var descriptor = cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 0; // 不需要深度缓冲
        
        // 创建临时 RenderTexture
        cmd.GetTemporaryRT(_tempTexture.id, descriptor);
        
        // 设置灰阶强度（1.0 = 完全灰阶）
        _settings.grayscaleMaterial.SetFloat(GrayscaleAmountID, 1.0f);
        
        // Blit: 相机颜色 → 临时纹理（应用灰阶 Shader）→ 相机颜色
        cmd.Blit(_cameraColorTarget, _tempTexture.Identifier(), 
                  _settings.grayscaleMaterial, 0);
        cmd.Blit(_tempTexture.Identifier(), _cameraColorTarget);
        
        // 释放临时纹理
        cmd.ReleaseTemporaryRT(_tempTexture.id);
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
```

### 3.3 GrayscaleToggle.cs（G 键切换控制器）

```csharp
using UnityEngine;

/// <summary>
/// 灰阶测试模式切换器。
/// 按 G 键切换灰阶/彩色模式。
/// 
/// 注意：GDD §13 和可访问性分级 §1.1 规定快捷键为 G。
/// 但灰模资产规格清单 §4.6 中提到 G 键。
/// 任务描述中提到 F 键——以任务描述为准，使用 F 键切换。
/// 
/// 最终确认：使用 G 键（与可访问性分级文档一致）。
/// 如果主理人确认改用 F，只需修改 KeyCode.G → KeyCode.F。
/// </summary>
public class GrayscaleToggle : MonoBehaviour
{
    [SerializeField] private GrayscaleRendererFeature _grayscaleFeature;
    
    private bool _isGrayscale = false;
    
    private void Update()
    {
        // 按 G 键切换灰阶模式
        if (Input.GetKeyDown(KeyCode.G))
        {
            _isGrayscale = !_isGrayscale;
            _grayscaleFeature.SetEnabled(_isGrayscale);
            
            Debug.Log($"[Grayscale] 灰阶模式: {(_isGrayscale ? "开启" : "关闭")}");
        }
    }
    
    /// <summary>
    /// 程序化切换灰阶（设置菜单调用）。
    /// </summary>
    public void SetGrayscale(bool enabled)
    {
        _isGrayscale = enabled;
        _grayscaleFeature.SetEnabled(enabled);
    }
    
    public bool IsGrayscale => _isGrayscale;
}
```

> **快捷键说明**：可访问性分级文档 §5.3 明确写"灰阶测试流程：测试快捷键 G"。灰模资产规格清单 §4.6 也写 G 键。任务描述中提到 F 键——此处以已有文档为准使用 **G 键**。如需改为 F 键，只需修改一行代码（`KeyCode.G` → `KeyCode.F`）。

### 3.4 Blit 材质配置

| 属性 | 值 |
|------|-----|
| 文件名 | `GrayscaleBlit.mat` |
| Shader | `Hidden/Grayscale` |
| `_GrayscaleAmount` | 1.0（运行时由 RenderPass 控制） |
| 路径 | `Assets/_Project/Art/Materials/GrayscaleBlit.mat` |

### 3.5 URP Renderer 配置步骤

1. 在 Project Settings → Graphics → URP Renderer Asset 中，添加 Renderer Feature
2. 选择 "Grayscale Renderer Feature"（从脚本注册）
3. 将 `GrayscaleBlit.mat` 拖入 `grayscaleMaterial` 字段
4. `isEnabled` 默认为 false（彩色模式）
5. 将 `GrayscaleToggle` 组件挂载到场景中的持久 GameObject（如 GameManager）

---

## 4. 灰阶可辨测试模式

### 4.1 测试流程

| 步骤 | 操作 | 预期结果 |
|------|------|---------|
| 1 | 游戏运行中按 G 键 | 画面变为灰阶 |
| 2 | 观察三种敌人弹幕 | 能区分直线型(三角箭头) vs 扩散型(圆形) vs 弧线型(月牙) |
| 3 | 观察弟子弹幕 vs Boss弹幕 | 能通过尺寸差异区分（Boss弹幕大50%+） |
| 4 | 道宗师Boss战中观察波纹 vs 礼反弹圈 | 能通过透明度+描边粗细区分 |
| 5 | 观察玩家弹幕(白色直线) vs 敌人弹幕 | 能区分（白色 vs 灰色 + 形状差异） |
| 6 | 完成一局灰阶模式游戏 | 全程能辨识弹幕类型，不因灰阶导致误判 |
| 7 | 再次按 G 键 | 画面恢复彩色 |

### 4.2 测试通过标准（来自灰模资产规格清单 §2.3）

| 测试项 | 通过标准 |
|--------|---------|
| 弹幕类型识别 | 灰阶下能区分三角(直线型) vs 圆形(扩散型) vs 弧线(飘忽型) |
| 威胁等级识别 | 灰阶下能通过尺寸区分弟子弹幕 vs Boss弹幕 |
| 波纹 vs 反弹圈 | 灰阶下能通过透明度(α=0.7 vs 1.0) + 描边粗细(3px vs 4px)区分 |
| 玩家弹幕 vs 敌人弹幕 | 灰阶下能区分（白色高亮 vs 灰色 + 形状差异） |

### 4.3 失败处理

如果灰阶测试失败，按灰模资产规格清单 §2.3 的失败处理列执行：

| 失败项 | 处理方案 |
|--------|---------|
| 弹幕类型不可辨 | 增加轮廓差异（形状修改），不依赖颜色 |
| 威胁等级不可辨 | 增大 Boss 弹幕尺寸差（从50%提升到80%+） |
| 波纹vs反弹圈不可辨 | 增加透明度差（波纹α 0.7→0.5）或增加形状标记（波纹加虚线效果） |

---

## 5. 不受灰阶影响的元素

### 5.1 排除列表

以下元素**不应该**被灰阶后处理影响——它们在灰阶模式下必须保持原有视觉表现：

| 元素 | 理由 | 处理方案 |
|------|------|---------|
| **HUD 文字**（HP/体力/学识/波次/Boss阶段） | 文字可读性依赖颜色对比（如 HP 条绿→黄→红）。灰阶后文字仍可读但颜色信息丢失 | **方案1**：HUD 使用 Screen Space - Overlay Canvas，渲染在 URP 后处理之后，天然不受灰阶影响。**推荐此方案** |
| **命中闪烁**（白→红50ms） | 命中闪烁是手感反馈核心。灰阶下需通过亮度差异保持可见 | 见 §6 兼容方案 |
| **设置菜单 UI** | 玩家需要在灰阶模式下操作设置菜单 | 同 HUD，用 Screen Space - Overlay Canvas |

### 5.2 HUD 渲染模式

```
Canvas 渲染模式选择：
┌─────────────────────────────────────────┐
│  Screen Space - Overlay  ← ✅ 推荐方案   │
│  （渲染在所有后处理之后，不受灰阶影响）    │
├─────────────────────────────────────────┤
│  Screen Space - Camera  ← ❌ 不推荐      │
│  （受后处理影响，HUD 会被灰阶化）         │
├─────────────────────────────────────────┤
│  World Space            ← ❌ 不推荐      │
│  （受后处理影响，且 HUD 会随相机移动）     │
└─────────────────────────────────────────┘
```

**重要**：Demo 所有 HUD 和 UI Canvas 必须设为 **Screen Space - Overlay** 模式。这样灰阶 Renderer Feature 的 Blit 操作发生在 UI 渲染之前，HUD 不受灰阶影响。

**URP 渲染顺序**：
```
1. Render Opaque Objects (场地、弟子、Boss 等)
2. Render Transparent Objects (弹幕、粒子、半透明效果)
3. Grayscale Renderer Feature (灰阶 Blit) ← 灰阶在此执行
4. Render Overlay UI (HUD、TextMeshPro)    ← 不受灰阶影响
5. Final Output
```

### 5.3 例外处理

如果某些 UI 元素必须用 Screen Space - Camera 模式（如需要后处理效果的 UI），可以：
- 在灰阶 Shader 中添加 Stencil 测试，跳过特定标记的像素
- 或在 Renderer Feature 中添加对特定 Layer 的排除

Demo 阶段不推荐此复杂方案——全部 UI 用 Overlay 模式即可。

---

## 6. 命中闪烁兼容方案

### 6.1 问题分析

| 要素 | 实现方式 | 层级 |
|------|---------|------|
| 灰阶 | URP Renderer Feature 全屏 Blit | 渲染层（后处理） |
| 命中闪烁 | `SpriteRenderer.color` 协程覆盖 | 渲染层（逐对象着色） |

**两者不冲突**——灰阶是后处理，命中闪烁是 SpriteRenderer 着色。灰阶处理的是已经渲染好的画面（包含命中闪烁的颜色），两者在渲染管线上是串行的：

```
SpriteRenderer.color = 白色 (命中闪烁阶段1)
    → 渲染到帧缓冲
    → 灰阶 Blit 处理帧缓冲
    → 白色(255,255,255) → 灰阶值255 (仍为白色)
    → 红色(255,48,48) → 灰阶值0.299×255+0.587×48+0.114×48 = 76+28+5 = 109 (中灰)
```

### 6.2 灰阶下命中闪烁的可见性

| 闪烁阶段 | 原色 | 灰阶值 | 与原色灰阶值差异 | 可见性 |
|---------|------|--------|----------------|--------|
| 阶段1: 纯白 #FFFFFF | (255,255,255) | 255 | — | 极亮，与任何背景都有高对比 |
| 阶段2: 纯红 #FF3030 | (255,48,48) | 109 | 与白色差146 | 中灰，与白色阶段有明显亮度差 |
| 恢复: 原色 | 取决于学派 | 取决于学派 | — | 恢复正常 |

**结论**：灰阶下命中闪烁仍然可见——阶段1(亮度255)→阶段2(亮度109)的亮度变化足够明显。玩家在灰阶模式下仍能感知命中反馈。

### 6.3 命中闪烁协程（重复确认）

此协程已在灰模资产规格清单 §4.5 中给出，此处确认其与灰阶 Shader 的兼容性：

```csharp
/// <summary>
/// 命中闪烁协程。通过 SpriteRenderer.color 覆盖实现。
/// 灰阶 Shader 在后处理层操作，与此协程无冲突。
/// 
/// 灰阶下表现：
///   阶段1 纯白 → 灰阶值255（极亮）
///   阶段2 纯红 → 灰阶值109（中灰）
///   亮度差异 = 255-109 = 146，足够可见
/// </summary>
public class HitFlashController : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Coroutine _flashCoroutine;
    
    private static readonly Color HitFlashWhite = new Color(1f, 1f, 1f);      // #FFFFFF
    private static readonly Color HitFlashRed = new Color(1f, 0.188f, 0.188f); // #FF3030
    
    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }
    
    /// <summary>
    /// 触发命中闪烁。多次受击时重置协程。
    /// </summary>
    /// <param name="originalColor">受击前的原始颜色（用于恢复）</param>
    public void TriggerHitFlash(Color originalColor)
    {
        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(HitFlashRoutine(originalColor));
    }
    
    private IEnumerator HitFlashRoutine(Color originalColor)
    {
        // 阶段1: 纯白，持续 25ms
        _renderer.color = HitFlashWhite;
        yield return new WaitForSeconds(0.025f);
        
        // 阶段2: 纯红，持续 25ms
        _renderer.color = HitFlashRed;
        yield return new WaitForSeconds(0.025f);
        
        // 恢复原色
        _renderer.color = originalColor;
        
        _flashCoroutine = null;
    }
}
```

### 6.4 简化视觉模式兼容

可访问性 Standard §2.3 规定简化视觉模式下命中闪烁**不可关闭**。灰阶模式和简化视觉模式可同时启用，互不冲突：

| 模式组合 | 命中闪烁 | 死亡碎裂 | 画面颜色 |
|---------|---------|---------|---------|
| 正常 | 白→红 50ms | 3-5片粒子 | 彩色 |
| 灰阶 | 白→红 50ms（灰阶下为亮→暗） | 3-5片粒子 | 灰阶 |
| 简化视觉 | 白→红 50ms（不可关） | 关闭，直接淡出 | 彩色 |
| 灰阶 + 简化视觉 | 白→红 50ms（灰阶下为亮→暗） | 关闭，直接淡出 | 灰阶 |

---

## 7. 波纹圈 vs 反弹圈的灰阶区分

### 7.1 问题

灰模资产规格清单 §2.2 指出：道宗师波纹和礼反弹圈都是"扩散空心圆"——灰阶下形状完全相同，**只能靠非颜色参数区分**。

### 7.2 区分方案

| 参数 | 道宗师波纹 | 礼反弹圈 | 灰阶下差异 |
|------|-----------|---------|-----------|
| 透明度 (α) | 0.7 | 1.0 | 波纹更淡，反弹圈更实 |
| 描边宽度 (borderWidth) | 3px | 4px | 波纹更细，反弹圈更粗 |
| 扩散来源 | Boss 位置 | 屏障位置 | 位置不同（但形状相同） |
| 扩散速度 | 较慢 (ringExpandSpeed=3.0) | 较快 (ringExpandSpeed=5.0) | 运动速度差异 |

### 7.3 实现方式

两者都使用 `SpriteShape.Ring` 生成的空心圆环 Sprite，区别在 `SpriteRenderer.color` 的 alpha 值和 Sprite 的 borderWidth 参数：

```csharp
// 道宗师波纹
var rippleSprite = SpriteGenerator.Generate(SpriteShape.Ring, 128, borderWidth: 3);
rippleRenderer.sprite = rippleSprite;
Color rippleColor = ColorInjector.GetSchoolColor("Taoist");
rippleColor.a = 0.7f;  // 半透明
rippleRenderer.color = rippleColor;

// 礼反弹圈
var bounceSprite = SpriteGenerator.Generate(SpriteShape.Ring, 128, borderWidth: 4);
bounceRenderer.sprite = bounceSprite;
Color bounceColor = ColorInjector.GetSchoolColor("Neutral"); // 白色
bounceColor.a = 1.0f;  // 不透明
bounceRenderer.color = bounceColor;
```

### 7.4 灰阶测试验证

| 测试场景 | 灰阶表现 | 通过标准 |
|---------|---------|---------|
| 波纹单独出现 | 半透明(α=0.7)灰色细环(3px) | 可见但不突出 |
| 反弹圈单独出现 | 不透明(α=1.0)灰色粗环(4px) | 清晰可见 |
| 两者同时出现 | 波纹(淡细) vs 反弹圈(实粗) | 玩家能区分"较淡的扩散圈"和"较实的扩散圈" |

### 7.5 失败预案

如果灰阶下波纹和反弹圈仍无法区分：

| 预案 | 方案 | 影响 |
|------|------|------|
| 预案A | 波纹改为虚线环（DashedRing），反弹圈保持实线环 | 形状差异，灰阶下极易区分 |
| 预案B | 反弹圈添加箭头标记（指向扩散方向） | 增加视觉信息量 |
| 预案C | 波纹 α 从 0.7 降至 0.5，增大透明度差 | 可能影响波纹可见性 |

**推荐**：预案A（波纹改虚线环）——形状差异是最可靠的灰阶区分手段。但需程基岩确认虚线环在快速扩散动画下的视觉效果。

---

## 8. 文件命名规范与存放路径

### 8.1 Shader 相关文件

| 文件 | 命名 | 路径 |
|------|------|------|
| 灰阶 Shader | `Grayscale.shader` | `Assets/_Project/Art/Shaders/Grayscale.shader` |
| Blit 材质 | `GrayscaleBlit.mat` | `Assets/_Project/Art/Materials/GrayscaleBlit.mat` |
| Renderer Feature 脚本 | `GrayscaleRendererFeature.cs` | `Assets/_Project/Scripts/Rendering/GrayscaleRendererFeature.cs` |
| 切换控制器 | `GrayscaleToggle.cs` | `Assets/_Project/Scripts/UI/GrayscaleToggle.cs` |

### 8.2 命名规范对齐

| 规范来源 | 要求 | 本方案对齐 |
|---------|------|-----------|
| 主架构 §5.2 | Shader 用 PascalCase.shader | ✅ `Grayscale.shader` |
| 主架构 §5.1 | C# 脚本用 PascalCase.cs | ✅ `GrayscaleRendererFeature.cs` |
| 主架构 §4 目录 | Shaders 在 `Art/Shaders/` | ✅ `Assets/_Project/Art/Shaders/` |
| 主架构 §4 目录 | Materials 在 `Art/Materials/` | ✅ `Assets/_Project/Art/Materials/` |
| 主架构 §4 目录 | 脚本按架构分层 | ✅ Rendering 脚本归 Rendering/，UI 脚本归 UI/ |

### 8.3 未来扩展预留

正式版可能需要的 Shader（当前 Demo 不实现，预留命名规范）：

| Shader | 用途 | 命名 | 预计路径 |
|--------|------|------|---------|
| 水墨渲染 | 水墨风格正式版 | `InkPaint.shader` | `Art/Shaders/InkPaint.shader` |
| 墨染标记 | 墨池地形墨染效果 | `InkStain.shader` | `Art/Shaders/InkStain.shader` |
| 屏幕震屏 | 命中/爆炸震屏后处理 | `ScreenShake.shader` | `Art/Shaders/ScreenShake.shader` |
| 色盲模拟 | 色盲模式测试/切换 | `ColorblindSimulate.shader` | `Art/Shaders/ColorblindSimulate.shader` |（→ v1.1 已在 §12 中正式定义，不再是预留）

---

## 9. 性能分析

### 9.1 灰阶 Blit 开销

| 指标 | 值 | 说明 |
|------|-----|------|
| Draw Call | +1 | 灰阶 Blit 是一个全屏 Draw Call |
| GPU 负载 | 极低 | 单次纹理采样 + 点积运算 |
| 内存 | ~4MB | 一个临时 RenderTexture（1920×1080×RGBA32） |
| 帧率影响 | <0.1ms | 在 GTX 1060 级别 GPU 上几乎无感 |

### 9.2 与性能预算的对齐

主架构文档 §10 性能预算要求 60 FPS、Draw Call <100。灰阶 Blit 增加 1 个 Draw Call，总计仍远低于预算。

| 预算指标 | 预算 | 灰模预估 | 含灰阶 Blit | 结论 |
|---------|------|---------|------------|------|
| 帧率 | 60 FPS | ~60 FPS | ~60 FPS | ✅ |
| Draw Call | <100 | <50 | <51 | ✅ |
| 内存 | <500MB | ~600KB Sprite + 引擎 | +4MB 临时 RT | ✅ |

---

## 10. 待确认项与风险

| 项目 | 说明 | 需要确认方 |
|------|------|-----------|
| G 键 vs F 键 | ~~可访问性分级文档写 G 键，任务描述提到 F 键~~ → **已确认：G 键**（主理人决策 v1.1） | ~~主理人决策~~ → ✅ 已确认 |
| URP Blit API 版本兼容 | `ScriptableRenderPass` + `Blit` 在 URP 12.x (2022.3 LTS) 中的 API 可能有细微差异。需程基岩确认具体 URP 版本的 Blit 调用方式 | 程基岩（工程） |
| Screen Space - Overlay Canvas 与 TextMeshPro | 需确认 TMP 在 Overlay 模式下渲染正常（通常无问题，但需验证） | 程基岩（工程） |
| 波纹改虚线环预案 | 如果灰阶下波纹/反弹圈不可辨，预案A(波纹改DashedRing)需程基岩确认虚线环在扩散动画下的视觉效果 | 程基岩 + 灰阶测试 |
| 灰阶 Shader 中 TransformUVToClipSpace 函数 | 该函数可能不在所有 URP 版本中可用。已提供全屏三角形备选方案 | 程基岩（工程） |

---

## 11. 与可访问性分级文档的接口

可访问性分级 Basic §1.1 对灰阶可辨模式的要求与本方案的对应关系：

| 可访问性分级要求 | 本方案实现 |
|----------------|-----------|
| 实现方式：后处理 Grayscale 滤镜 | ✅ URP Renderer Feature + Blit Material |
| 按 G 键一键切换灰阶/彩色 | ✅ GrayscaleToggle.cs，KeyCode.G |
| 验证标准：GDD §13 灰阶可辨测试标准 | ✅ §4 测试流程与通过标准 |
| 依赖架构：无（渲染层） | ✅ 纯渲染层，不侵入 Gameplay/Input/Config |
| 备选方案：遍历 SpriteRenderer 替换灰阶色 | ⚠️ 未采用（性能差、侵入逻辑层）。如 URP 方案不可行可降级 |

---

*本规格定义了 URP 兼容的灰阶后处理方案。灰阶通过 Renderer Feature + Blit Material 实现，与命中闪烁(SpriteRenderer.color)互不冲突。HUD 使用 Overlay Canvas 天然不受灰阶影响。波纹vs反弹圈通过透明度和描边粗细在灰阶下区分，并有虚线环预案兜底。*

---

## 12. 色盲模式方案（可访问性 Standard §2.1 补充）

> **v1.1 新增章节**。主理人确认 Demo 做基础色盲模式。本章节定义色盲模式的实现方案，包含后处理 Shader 和配置驱动配色替换双轨方案。

### 12.1 需求来源与定位

| 来源 | 要求 |
|------|------|
| 可访问性分级 v1.0 §2.1 | 色盲友好模式为 Standard 级 P1。弹幕形状不依赖颜色区分（灰阶可辨已覆盖），色盲模式是**颜色层**的增强 |
| 可访问性分级 v1.0 §2.1 | 预设3种色盲友好配色：红绿色盲(Protanopia)、蓝黄色盲(Tritanopia)、全色弱(Achromatomaly) |
| 主理人决策 | Demo 做基础色盲模式 |

**核心原则**：色盲模式与灰阶模式是互补关系，不是替代关系。

| 模式 | 解决的问题 | 实现层 |
|------|-----------|--------|
| 灰阶模式 (Basic §1.1) | 去掉所有颜色，验证形状可辨 | 后处理 Shader（全屏去色） |
| 色盲模式 (Standard §2.1) | 保留颜色但替换为色觉差异更大的配色 | 配置驱动配色替换 + 后处理 Shader（可选模拟） |

### 12.2 双轨方案

Demo 色盲模式采用双轨方案——配置替换为主、后处理 Shader 为辅：

| 轨道 | 方案 | 用途 | 优先级 |
|------|------|------|--------|
| **轨道A（主）：配置驱动配色替换** | 从 JSON 读取色盲友好配色，运行时替换 SpriteRenderer.color | 让色觉差异玩家在彩色模式下也能快速识别弹幕 | P1 |
| **轨道B（辅）：色盲模拟后处理** | 后处理 Shader 模拟色盲视觉，供开发者测试配色是否有效 | 开发者工具——验证配色在色盲视角下的效果 | P2 |

### 12.3 轨道A：配置驱动配色替换

#### 12.3.1 色盲友好配色方案

基于灰模资产规格清单 §2.1 的颜色编码表，针对3种色盲类型设计替代配色：

**原配色回顾**：

| 学派 | 原色 | HEX | RGB | 灰阶值 |
|------|------|-----|-----|--------|
| 儒家 | 儒金 | #D4A017 | (212,160,23) | 159 |
| 法家 | 法黑 | #1A1A1A | (26,26,26) | 26 |
| 道家 | 道青 | #2E8B8B | (46,139,139) | 111 |
| 无学派 | 素白 | #FFFFFF | (255,255,255) | 255 |

**色盲友好配色——红绿色盲（Protanopia / Deuteranopia）**：

红绿色盲无法区分红绿色调。儒金(偏黄红)和道青(偏青绿)在红绿色盲视角下会趋近——需替换为色相差异大的配色。

| 学派 | 原色 | 色盲友好色 | HEX | RGB | 替代理由 |
|------|------|-----------|-----|-----|---------|
| 儒家 | 儒金 #D4A017 | **儒蓝** | #2196F3 | (33,150,243) | 蓝色与橙/绿色对比度高，红绿色盲可辨 |
| 法家 | 法黑 #1A1A1A | 法黑（不变） | #1A1A1A | (26,26,26) | 黑色不受色盲影响 |
| 道家 | 道青 #2E8B8B | **道橙** | #FF9800 | (255,152,0) | 橙色在红绿色盲下偏黄，与蓝色对比明显 |
| 无学派 | 素白 #FFFFFF | 素白（不变） | #FFFFFF | (255,255,255) | 白色不受色盲影响 |

> 儒蓝(蓝) vs 道橙(橙) 在红绿色盲视角下仍有明显色相差——蓝色感知正常，橙色偏黄，两者可辨。

**色盲友好配色——蓝黄色盲（Tritanopia）**：

蓝黄色盲无法区分蓝黄色调。道青(偏蓝)需替换为非蓝色。

| 学派 | 原色 | 色盲友好色 | HEX | RGB | 替代理由 |
|------|------|-----------|-----|-----|---------|
| 儒家 | 儒金 #D4A017 | 儒金（不变） | #D4A017 | (212,160,23) | 金黄色在蓝黄色盲下感知正常 |
| 法家 | 法黑 #1A1A1A | 法黑（不变） | #1A1A1A | (26,26,26) | 不受影响 |
| 道家 | 道青 #2E8B8B | **道品红** | #E91E63 | (233,30,99) | 品红色不含蓝色成分，蓝黄色盲可辨 |
| 无学派 | 素白 #FFFFFF | 素白（不变） | #FFFFFF | (255,255,255) | 不受影响 |

> 儒金(黄) vs 道品红(红) 在蓝黄色盲视角下——黄色偏绿、品红偏红，两者可辨。

**色盲友好配色——全色弱（Achromatomaly）**：

全色弱玩家对颜色饱和度感知降低。方案：提高所有颜色的饱和度和明度对比。

| 学派 | 原色 | 色盲友好色 | HEX | RGB | 替代理由 |
|------|------|-----------|-----|-----|---------|
| 儒家 | 儒金 #D4A017 | **高饱和金** | #FFD700 | (255,215,0) | 提高饱和度和明度 |
| 法家 | 法黑 #1A1A1A | 法黑（不变） | #1A1A1A | (26,26,26) | 不受影响 |
| 道家 | 道青 #2E8B8B | **高饱和青** | #00CED1 | (0,206,209) | 提高饱和度和明度 |
| 无学派 | 素白 #FFFFFF | 素白（不变） | #FFFFFF | (255,255,255) | 不受影响 |

> 全色弱模式本质是增强原配色的对比度——保持色相不变，提高饱和度+明度。

#### 12.3.2 配色配置 JSON

色盲友好配色从 `schools.json` 读取（ADR-001 数据驱动），与原配色并列：

```json
// schools.json 中的色盲配色段
{
  "schoolColors": {
    "confucian": { "hex": "#D4A017", "r": 212, "g": 160, "b": 23 },
    "legalist":  { "hex": "#1A1A1A", "r": 26,  "g": 26,  "b": 26 },
    "daoist":    { "hex": "#2E8B8B", "r": 46,  "g": 139, "b": 139 },
    "neutral":   { "hex": "#FFFFFF", "r": 255, "g": 255, "b": 255 }
  },
  "colorblindPalettes": {
    "protanopia": {
      "confucian": { "hex": "#2196F3", "r": 33,  "g": 150, "b": 243 },
      "legalist":  { "hex": "#1A1A1A", "r": 26,  "g": 26,  "b": 26 },
      "daoist":    { "hex": "#FF9800", "r": 255, "g": 152, "b": 0 },
      "neutral":   { "hex": "#FFFFFF", "r": 255, "g": 255, "b": 255 }
    },
    "tritanopia": {
      "confucian": { "hex": "#D4A017", "r": 212, "g": 160, "b": 23 },
      "legalist":  { "hex": "#1A1A1A", "r": 26,  "g": 26,  "b": 26 },
      "daoist":    { "hex": "#E91E63", "r": 233, "g": 30,  "b": 99 },
      "neutral":   { "hex": "#FFFFFF", "r": 255, "g": 255, "b": 255 }
    },
    "achromatomaly": {
      "confucian": { "hex": "#FFD700", "r": 255, "g": 215, "b": 0 },
      "legalist":  { "hex": "#1A1A1A", "r": 26,  "g": 26,  "b": 26 },
      "daoist":    { "hex": "#00CED1", "r": 0,   "g": 206, "b": 209 },
      "neutral":   { "hex": "#FFFFFF", "r": 255, "g": 255, "b": 255 }
    }
  }
}
```

#### 12.3.3 C# 配置绑定

```csharp
public enum ColorblindMode
{
    None,           // 正常配色（默认）
    Protanopia,     // 红绿色盲
    Tritanopia,     // 蓝黄色盲
    Achromatomaly   // 全色弱
}

[System.Serializable]
public class SchoolColorsConfig
{
    public SchoolColorConfig confucian;
    public SchoolColorConfig legalist;
    public SchoolColorConfig daoist;
    public SchoolColorConfig neutral;
}

[System.Serializable]
public class ColorblindPalettesConfig
{
    public SchoolColorsConfig protanopia;
    public SchoolColorsConfig tritanopia;
    public SchoolColorsConfig achromatomaly;
}

[System.Serializable]
public class ArtColorConfig
{
    public SchoolColorsConfig schoolColors;           // 原配色
    public ColorblindPalettesConfig colorblindPalettes; // 色盲友好配色
    // ... 其他颜色段（vfxColors, hudColors, arenaColors）...
}
```

#### 12.3.4 ColorInjector 扩展

在 Sprite 生成方案的 `ColorInjector` 基础上扩展色盲配色支持：

```csharp
/// <summary>
/// 颜色注入器扩展——支持色盲配色替换。
/// 当色盲模式启用时，从 colorblindPalettes 读取替代配色。
/// </summary>
public static class ColorInjector
{
    private static ArtColorConfig _config;
    private static ColorblindMode _colorblindMode = ColorblindMode.None;
    
    public static void Initialize(ArtColorConfig config)
    {
        _config = config;
    }
    
    /// <summary>
    /// 设置色盲模式。设置后所有 GetSchoolColor 调用返回色盲友好配色。
    /// </summary>
    public static void SetColorblindMode(ColorblindMode mode)
    {
        _colorblindMode = mode;
    }
    
    /// <summary>
    /// 按学派获取颜色。如果色盲模式启用，返回色盲友好配色。
    /// </summary>
    public static Color GetSchoolColor(string school)
    {
        SchoolColorsConfig palette = GetActivePalette();
        
        return school switch
        {
            "Confucian" => palette.confucian.ToColor(),
            "Legalist"  => palette.legalist.ToColor(),
            "Taoist"    => palette.daoist.ToColor(),
            _           => palette.neutral.ToColor()
        };
    }
    
    private static SchoolColorsConfig GetActivePalette()
    {
        return _colorblindMode switch
        {
            ColorblindMode.Protanopia     => _config.colorblindPalettes.protanopia,
            ColorblindMode.Tritanopia     => _config.colorblindPalettes.tritanopia,
            ColorblindMode.Achromatomaly  => _config.colorblindPalettes.achromatomaly,
            _                              => _config.schoolColors // None = 原配色
        };
    }
    
    // ... ApplyColor, ApplyColorWithAlpha 等方法调用 GetSchoolColor，自动适配色盲模式 ...
}
```

#### 12.3.5 运行时切换

色盲模式切换不需要重新加载场景——只需更新 `ColorInjector` 的色盲模式标志，然后重新注入所有活跃 SpriteRenderer 的颜色：

```csharp
/// <summary>
/// 色盲模式切换控制器。
/// 由设置菜单调用（UX 规格由文策渊产出，须引用可访问性分级 §2.1）。
/// </summary>
public class ColorblindModeController : MonoBehaviour
{
    /// <summary>
    /// 切换色盲模式并刷新所有活跃 SpriteRenderer 的颜色。
    /// </summary>
    public void SetColorblindMode(ColorblindMode mode)
    {
        ColorInjector.SetColorblindMode(mode);
        RefreshAllSpriteRenderers();
    }
    
    /// <summary>
    /// 遍历所有活跃的 SpriteRenderer，重新注入颜色。
    /// 注意：此方法有性能开销（遍历所有 SpriteRenderer），仅在切换模式时调用。
    /// </summary>
    private void RefreshAllSpriteRenderers()
    {
        var allRenderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var renderer in allRenderers)
        {
            var initializer = renderer.GetComponent<SpriteInitializer>();
            if (initializer != null)
            {
                initializer.RefreshColor();
            }
        }
    }
}
```

> `SpriteInitializer` 需新增 `RefreshColor()` 方法：
> ```csharp
> public void RefreshColor()
> {
>     if (_renderer != null)
>         ColorInjector.ApplyColorWithAlpha(_renderer, _colorSource, _alpha);
> }
> ```

#### 12.3.6 命中闪烁与色盲模式的兼容

命中闪烁的颜色（纯白 #FFFFFF → 纯红 #FF3030）在色盲模式下的可见性分析：

| 色盲类型 | 白色 #FFFFFF 感知 | 红色 #FF3030 感知 | 差异 | 可见性 |
|---------|-----------------|-----------------|------|--------|
| 红绿色盲 | 白色（正常） | 暗黄褐色（红绿色盲将红色感知为暗黄） | 亮度差大 | ✅ 可见 |
| 蓝黄色盲 | 白色（正常） | 粉红色（蓝黄色盲对红色感知正常） | 色相+亮度差 | ✅ 可见 |
| 全色弱 | 白色（正常） | 灰粉色（饱和度降低） | 亮度差大 | ✅ 可见 |

**结论**：命中闪烁在所有色盲模式下均可见——白色与任何色觉感知都有高对比度。无需为色盲模式调整命中闪烁颜色。

#### 12.3.7 HUD 颜色与色盲模式的兼容

HUD HP 条颜色（绿→黄→红）在色盲模式下的问题：

| 色盲类型 | 绿色感知 | 黄色感知 | 红色感知 | 问题 |
|---------|---------|---------|---------|------|
| 红绿色盲 | 暗黄 | 黄 | 暗黄 | 绿和红都变暗黄 → **HP 条颜色不可辨** |

**解决方案**：HUD 使用 Screen Space - Overlay Canvas（§5.2），**不受后处理 Shader 影响**。但配色替换（轨道A）会影响 HUD。

**HUD 色盲友好方案**：

| HP 状态 | 原色 | 红绿色盲友好色 | 说明 |
|---------|------|--------------|------|
| 高 HP (>60%) | 绿 #4CAF50 | **蓝 #2196F3** | 蓝色在红绿色盲下可辨 |
| 中 HP (30-60%) | 黄 #FFC107 | 黄 #FFC107（不变） | 黄色在红绿色盲下可辨 |
| 低 HP (<30%) | 红 #F44336 | **橙 #FF9800** | 橙色在红绿色盲下偏黄，与蓝色可区分 |

> **简化方案**：Demo 阶段 HUD HP 条色盲友好配色可以从 `colorblindPalettes` 中统一读取。或者更简单——HP 条在色盲模式下用**蓝→黄→橙**替代**绿→黄→红**。体力条（蓝色）在色盲模式下可改为**紫色 #9C27B0**，与 HP 条的蓝/橙不冲突。

### 12.4 轨道B：色盲模拟后处理 Shader（开发者工具）

#### 12.4.1 定位

色盲模拟 Shader 是**开发者工具**，不是玩家功能。用途：让开发者在不安装色盲模拟软件的情况下，直接在游戏内预览色盲视角下的画面效果，验证配色方案是否有效。

**优先级**：P2（Demo 时间允许则做，否则用外部工具如 Color Oracle 代替）

#### 12.4.2 Shader 方案

色盲模拟 Shader 与灰阶 Shader 使用相同的 URP Renderer Feature 架构，只是 Blit Material 不同。可以复用 `GrayscaleRendererFeature` 的框架，替换 Material 即可。

```hlsl
Shader "Hidden/ColorblindSimulate"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Mode ("Colorblind Mode", Float) = 0  // 0=Normal, 1=Protanopia, 2=Tritanopia, 3=Achromatomaly
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        
        Pass
        {
            Name "ColorblindSimulatePass"
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                uint vertexID     : SV_VertexID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            float _Mode;
            
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                // 全屏三角形
                output.positionCS = float4(
                    (float)(vertexID / 2) * 4.0 - 1.0,
                    (float)(vertexID % 2) * 4.0 - 1.0,
                    0.0, 1.0
                );
                output.uv = float2(
                    (float)(vertexID / 2) * 2.0,
                    1.0 - (float)(vertexID % 2) * 2.0
                );
                return output;
            }
            
            // === 色盲模拟矩阵 ===
            // 基于 Machado et al. 2009 的色盲模拟模型
            // 这些矩阵将 RGB 颜色空间映射到色盲感知的颜色空间
            
            // 红绿色盲 (Protanopia) 模拟矩阵
            static const float3x3 ProtanopiaMatrix = {
                0.152286, 1.052583, -0.204868,
                0.114503, 0.786281,  0.099216,
               -0.003882, -0.048116, 1.051998
            };
            
            // 蓝黄色盲 (Tritanopia) 模拟矩阵
            static const float3x3 TritanopiaMatrix = {
                1.012673, 0.135749, -0.148422,
               -0.012416, 0.868121,  0.144295,
                0.075893, 0.805353,  0.118754
            };
            
            // 全色弱 (Achromatomaly) 模拟——降低饱和度
            static const float3x3 AchromatomalyMatrix = {
                0.618, 0.320, 0.062,
                0.163, 0.775, 0.062,
                0.163, 0.320, 0.516
            };
            
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                if (_Mode < 0.5)
                {
                    // Mode 0: Normal（不处理）
                    return color;
                }
                else if (_Mode < 1.5)
                {
                    // Mode 1: Protanopia
                    color.rgb = mul(ProtanopiaMatrix, color.rgb);
                }
                else if (_Mode < 2.5)
                {
                    // Mode 2: Tritanopia
                    color.rgb = mul(TritanopiaMatrix, color.rgb);
                }
                else
                {
                    // Mode 3: Achromatomaly
                    color.rgb = mul(AchromatomalyMatrix, color.rgb);
                }
                
                return color;
            }
            
            ENDHLSL
        }
    }
}
```

> **矩阵来源**：色盲模拟矩阵基于 Machado, Oliveira & Fernandes (2009) 的研究成果，是业界标准的色盲模拟方法。这些矩阵适用于 sRGB 色彩空间的近似模拟。精确模拟需要线性空间转换，但 Demo 阶段近似模拟足够。

#### 12.4.3 色盲模拟切换控制器

```csharp
/// <summary>
/// 色盲模拟模式切换器（开发者工具）。
/// 按 B 键循环切换：Normal → Protanopia → Tritanopia → Achromatomaly → Normal
/// 
/// 注意：这是开发者工具，不是玩家功能。
/// 玩家色盲模式通过设置菜单的配色替换实现（轨道A）。
/// </summary>
public class ColorblindSimulateToggle : MonoBehaviour
{
    [SerializeField] private GrayscaleRendererFeature _simulateFeature;
    [SerializeField] private Material _colorblindSimulateMaterial;
    
    private int _currentMode = 0; // 0=Normal, 1=Protanopia, 2=Tritanopia, 3=Achromatomaly
    private static readonly int ModeID = Shader.PropertyToID("_Mode");
    
    private void Update()
    {
        // 按 B 键循环切换色盲模拟
        if (Input.GetKeyDown(KeyCode.B))
        {
            _currentMode = (_currentMode + 1) % 4;
            
            if (_currentMode == 0)
            {
                // Normal：关闭模拟
                _simulateFeature.SetEnabled(false);
                Debug.Log("[Colorblind Sim] Normal (off)");
            }
            else
            {
                // 切换模拟模式
                _colorblindSimulateMaterial.SetFloat(ModeID, _currentMode);
                _simulateFeature.settings.grayscaleMaterial = _colorblindSimulateMaterial;
                _simulateFeature.SetEnabled(true);
                
                string modeName = _currentMode switch
                {
                    1 => "Protanopia (红绿色盲)",
                    2 => "Tritanopia (蓝黄色盲)",
                    3 => "Achromatomaly (全色弱)",
                    _ => "Unknown"
                };
                Debug.Log($"[Colorblind Sim] {modeName}");
            }
        }
    }
}
```

> **复用说明**：色盲模拟复用 `GrayscaleRendererFeature` 的框架——同一个 Renderer Feature，切换 Material 即可在灰阶和色盲模拟之间切换。不需要新建第二个 Renderer Feature。

### 12.5 形状辅助标记方案

除了颜色替换，还可以通过**形状辅助标记**增强色盲可辨度。这作为色盲模式的第三层保障：

| 弹幕类型 | 原形状 | 色盲辅助标记（可选） | 说明 |
|---------|--------|-------------------|------|
| 儒家扩散弹 | 实心圆形 | 无需额外标记 | 圆形轮廓本身已足够可辨 |
| 法家直线弹 | 锐角三角形 | 白描边（已实现） | 白描边在所有色盲模式下均可见 |
| 道家弧线弹 | 弧线段（月牙） | 无需额外标记 | 月牙轮廓本身已足够可辨 |
| 玩家弹幕 | 窄长矩形 | 无需额外标记 | 白色 + 细长形状在所有色盲模式下可辨 |

**结论**：Demo 的形状编码系统（灰模资产规格清单 §2.2）本身已经是色盲友好的——3种敌人弹幕的轮廓完全不同（圆形 vs 三角形 vs 月牙）。色盲模式只需替换颜色作为辅助层，形状不需要额外标记。

### 12.6 色盲模式验证标准

来自可访问性分级 §2.1：

| 验证项 | 验证方法 | 通过标准 |
|--------|---------|---------|
| 红绿色盲配色 | 切换 Protanopia 配色，观察3种敌人弹幕 | 能通过颜色+形状组合快速区分弹幕类型 |
| 蓝黄色盲配色 | 切换 Tritanopia 配色，观察3种敌人弹幕 | 同上 |
| 全色弱配色 | 切换 Achromatomaly 配色，观察3种敌人弹幕 | 同上 |
| 色盲模拟验证 | 用色盲模拟 Shader (B键) 预览原配色在色盲视角下的效果 | 确认原配色在色盲视角下确实不够可辨（验证色盲模式的必要性） |
| HUD 颜色 | 切换色盲配色后观察 HP 条 | HP 条颜色在色盲模式下仍可区分（蓝→黄→橙） |

### 12.7 快捷键汇总

| 快捷键 | 功能 | 优先级 | 来源 |
|--------|------|--------|------|
| **G** | 切换灰阶/彩色模式 | P0 | 可访问性 Basic §1.1 |
| **B** | 循环切换色盲模拟（开发者工具） | P2 | 本章节新增 |

> **注意**：G 键（灰阶）是玩家可访问性功能。B 键（色盲模拟）是开发者工具，正式版应移除或隐藏。玩家色盲模式通过设置菜单切换配色（轨道A），不通过快捷键。

### 12.8 文件清单

| 文件 | 命名 | 路径 | 优先级 |
|------|------|------|--------|
| 色盲模拟 Shader | `ColorblindSimulate.shader` | `Assets/_Project/Art/Shaders/ColorblindSimulate.shader` | P2 |
| 色盲模拟 Blit 材质 | `ColorblindSimulateBlit.mat` | `Assets/_Project/Art/Materials/ColorblindSimulateBlit.mat` | P2 |
| 色盲模拟切换控制器 | `ColorblindSimulateToggle.cs` | `Assets/_Project/Scripts/Rendering/ColorblindSimulateToggle.cs` | P2 |
| 色盲模式控制器（玩家） | `ColorblindModeController.cs` | `Assets/_Project/Scripts/UI/ColorblindModeController.cs` | P1 |
| 色盲配色配置 | `schools.json` 中的 `colorblindPalettes` 段 | `Assets/_Project/Configs/schools.json` | P1 |

### 12.9 与灰阶模式的关系

| 维度 | 灰阶模式 (Basic §1.1) | 色盲模式 (Standard §2.1) |
|------|----------------------|------------------------|
| 解决问题 | 去掉所有颜色，验证形状可辨 | 替换为色觉差异更大的配色 |
| 实现层 | 后处理 Shader（全屏去色） | 配置驱动配色替换 + 后处理 Shader（模拟，开发工具） |
| 快捷键 | G（玩家功能） | B（开发者工具）；玩家通过设置菜单切换 |
| 同时启用 | 可以同时启用——灰阶 + 色盲配色。但灰阶会覆盖色盲配色（去色后颜色信息丢失） | 不建议同时启用。灰阶是最终验证手段，色盲是日常使用方案 |
| 优先级 | P0（硬约束） | P1（Standard 级） |

> **建议**：玩家不需要同时启用灰阶和色盲模式。灰阶是"终极测试"（验证形状是否足够可辨），色盲是"日常使用"（在彩色模式下为色觉差异玩家提供更好的配色）。设置菜单中两者应互斥——启用灰阶时色盲配色无意义（颜色被去色）；启用色盲配色时不需要灰阶（配色已优化）。

### 12.10 性能分析

| 方案 | Draw Call | GPU 负载 | 内存 | 说明 |
|------|----------|---------|------|------|
| 轨道A：配置替换 | 0 | 0 | 0 | 纯配置层，无渲染开销。切换时一次性遍历 SpriteRenderer 刷新颜色 |
| 轨道B：色盲模拟 Shader | +1 | 极低（矩阵乘法） | +4MB（临时 RT） | 与灰阶 Blit 相同的开销 |
| 同时启用轨道A+B | +1 | 极低 | +4MB | 配置替换 + 模拟预览叠加 |

**结论**：色盲模式对性能无显著影响。轨道A零渲染开销，轨道B与灰阶 Blit 等效。

---

## 13. 版本变更日志

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0 | 2026-07-09 | 首版：URP 灰阶后处理方案、Shader 代码、命中闪烁兼容、波纹vs反弹圈区分 |
| v1.1 | 2026-07-09 | 新增 §12 色盲模式方案（双轨：配置替换 + 模拟 Shader）；更新 §8.3 色盲 Shader 从预留改为正式定义；更新 §10 灰阶快捷键确认为 G 键 |
