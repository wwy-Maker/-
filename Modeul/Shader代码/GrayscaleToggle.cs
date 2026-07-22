// ============================================================
// 灰阶切换控制器 · 诸子百家·口诛笔伐 Demo
// 版本: v1.0
// 引擎: Unity 2022.3 LTS + URP
// 用途: 按G键切换灰阶/彩色模式, 用于灰阶可辨测试
// 依赖: GrayscaleRendererFeature (挂在URP Renderer Data上)
// ============================================================

using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 灰阶测试切换器。
/// 挂在场景中的任意GameObject上。
/// 按G键切换灰阶模式, 同时在Console输出当前状态。
/// 
/// 可访问性: P0 硬约束 (GDD §13 灰阶可辨测试标准)
/// </summary>
public class GrayscaleToggle : MonoBehaviour
{
    [Header("灰阶Renderer Feature引用")]
    [Tooltip("URP Renderer Data上的GrayscaleRendererFeature组件")]
    public GrayscaleRendererFeature grayscaleFeature;

    [Header("快捷键")]
    [Tooltip("切换灰阶模式的快捷键")]
    public KeyCode toggleKey = KeyCode.G;

    [Header("调试")]
    [Tooltip("是否在启动时输出当前状态")]
    public bool logOnStart = true;

    private bool _isGrayscale = false;

    private void Start()
    {
        if (grayscaleFeature != null)
        {
            grayscaleFeature.SetGrayscaleEnabled(_isGrayscale);
        }
        else
        {
            Debug.LogWarning("[GrayscaleToggle] 未指定 GrayscaleRendererFeature 引用! 请在Inspector中拖入。");
        }

        if (logOnStart)
        {
            Debug.Log($"[GrayscaleToggle] 初始化完成. 当前模式: {(_isGrayscale ? "灰阶" : "彩色")}. 按 {toggleKey} 键切换.");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleGrayscale();
        }
    }

    /// <summary>
    /// 切换灰阶模式
    /// </summary>
    public void ToggleGrayscale()
    {
        _isGrayscale = !_isGrayscale;

        if (grayscaleFeature != null)
        {
            grayscaleFeature.SetGrayscaleEnabled(_isGrayscale);
        }

        Debug.Log($"[GrayscaleToggle] 模式切换: {(_isGrayscale ? "灰阶 (测试用)" : "彩色 (正常)")}");
    }

    /// <summary>
    /// 直接设置灰阶模式
    /// </summary>
    public void SetGrayscale(bool enabled)
    {
        _isGrayscale = enabled;

        if (grayscaleFeature != null)
        {
            grayscaleFeature.SetGrayscaleEnabled(_isGrayscale);
        }
    }
}
