# Modeul — 诸子百家·口诛笔伐 美术资源

> 本目录是技术美术(TechnicalArtist)为Demo Prototype v0.1生成的全部美术资产。

## 📁 目录结构

```
Modeul/
├── 玩家角色/             ← 4个Sprite (player_*)
├── 弟子/                 ← 6个Sprite (disciple_*)
├── Boss/                 ← 9个Sprite (boss_*_phase*)
├── 弹幕/                 ← 20个Sprite (bullet_* + vfx_*)
├── 场地与拾取物/         ← 6个Sprite (arena_* + pickup_*)
├── HUD元素/              ← 9个Sprite (hud_*)
├── Shader代码/           ← 3个Shader + 2个C#脚本
├── 技术美术文档/         ← 完整资产清单文档
├── generate_all_sprites.py  ← Sprite批量生成脚本
├── generate_preview.py      ← 总览图生成脚本
└── 美术资产总览预览.png     ← 全部资产的预览图
```

## 🎨 资产统计

| 类别 | 数量 |
|------|------|
| Sprite PNG | 50 |
| Shader文件 | 3 |
| C#脚本 | 2 |
| 技术文档 | 1 |
| 生成脚本 | 2 |
| 预览图 | 1 |
| **合计** | **59** |

## 🎯 核心设计原则

### 1. 灰模 + 半灰
- **几何体 + 命中闪烁 + 死亡碎裂 + 占位音效** = 验证核心手感
- 颜色从JSON配置读取(ADR-001数据驱动)
- 所有Sprite纹理为白色,运行时通过`SpriteRenderer.color`注入学派色

### 2. 灰阶可辨
- **硬约束**: 去掉所有颜色后,玩家仍能区分弹幕类型
- 形状编码: 圆形(玩家/儒弹) vs 方形(弟子) vs 六边形(Boss) vs 三角(法弹) vs 月牙(道弹)
- 灰阶Shader按G键切换,用于灰阶可辨测试

### 3. 弹道视觉识别
- 颜色编码: 儒金#D4A017 / 法黑#1A1A1A / 道青#2E8B8B / 素白#FFFFFF
- 形状编码作为形状可辨的硬约束(儒金vs道青灰阶值仅差48)

## 🚀 Unity使用

1. **导入Sprite**: 将`玩家角色/`、`弟子/`、`Boss/`、`弹幕/`、`场地与拾取物/`、`HUD元素/` 6个目录的PNG复制到Unity的`Assets/_Project/Art/Sprites/`对应子目录
2. **导入Shader**: 将`Shader代码/Grayscale.shader`、`ColorBlindness.shader`、`HitFlash.shader`复制到`Assets/_Project/Art/Shaders/`
3. **导入C#脚本**: 将`Shader代码/GrayscaleRendererFeature.cs`、`GrayscaleToggle.cs`复制到`Assets/_Project/Scripts/Rendering/`
4. **配置**: 参考`技术美术文档/技术美术资产清单.md` §10 Unity导入设置指南

## 🖼️ 预览

打开 `美术资产总览预览.png` 查看全部资产的可视化效果。

---

*生成日期: 2026-07-09 | 技术美术: TechnicalArtist | 引擎: Unity 2022.3 LTS + URP*
