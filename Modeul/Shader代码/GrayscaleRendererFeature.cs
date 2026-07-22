// ============================================================
// 灰阶 URP Renderer Feature · 诸子百家·口诛笔伐 Demo
// 版本: v1.1
// 引擎: Unity 2022.3 LTS + URP
// 用途: URP后处理Renderer Feature, 全屏灰阶滤镜
// 依赖: Grayscale.shader
// ============================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GrayscaleRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class GrayscaleSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Material grayscaleMaterial;
        public bool enableGrayscale = false;
        [Range(0f, 1f)]
        public float grayscaleAmount = 1.0f;
    }

    public GrayscaleSettings settings = new GrayscaleSettings();
    private GrayscaleRenderPass _renderPass;

    public override void Create()
    {
        _renderPass = new GrayscaleRenderPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.grayscaleMaterial == null)
            return;

        if (!settings.enableGrayscale)
            return;

        _renderPass.Setup(renderer.cameraColorTargetHandle);
        renderer.EnqueuePass(_renderPass);
    }

    /// <summary>
    /// 运行时切换灰阶开关
    /// </summary>
    public void SetGrayscaleEnabled(bool enabled)
    {
        settings.enableGrayscale = enabled;
    }

    protected override void Dispose(bool disposing)
    {
        _renderPass?.Dispose();
    }
}

public class GrayscaleRenderPass : ScriptableRenderPass
{
    private GrayscaleRendererFeature.GrayscaleSettings _settings;
    private RTHandle _cameraColorTarget;
    private RTHandle _tempTexture;
    private const string k_TagName = "Grayscale Pass";

    public GrayscaleRenderPass(GrayscaleRendererFeature.GrayscaleSettings settings)
    {
        _settings = settings;
        this.renderPassEvent = settings.renderPassEvent;
    }

    public void Setup(RTHandle cameraColorTarget)
    {
        _cameraColorTarget = cameraColorTarget;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        var desc = renderingData.cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;

        RenderingUtils.ReAllocateIfNeeded(ref _tempTexture, desc, name: "_GrayscaleTemp");
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (_settings.grayscaleMaterial == null)
            return;

        CommandBuffer cmd = CommandBufferPool.Get(k_TagName);

        _settings.grayscaleMaterial.SetFloat("_GrayscaleAmount", _settings.grayscaleAmount);

        // Blit: cameraColor -> temp -> cameraColor (灰阶处理)
        Blitter.BlitCameraTexture(cmd, _cameraColorTarget, _tempTexture, _settings.grayscaleMaterial, 0);
        Blitter.BlitCameraTexture(cmd, _tempTexture, _cameraColorTarget);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void Dispose()
    {
        _tempTexture?.Release();
    }
}
