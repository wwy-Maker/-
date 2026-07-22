# Sprite 生成方案 · 诸子百家·口诛笔伐 Demo

**版本**: v1.0
**日期**: 2026-07-09
**作者**: 林绘澄（美术方向）
**依据**: 灰模资产规格清单 v1.0 · 主架构文档 v1.0 · ADR-001 数据驱动配置 · ADR-003 弹幕系统数据化
**引擎**: Unity 2022.3 LTS + URP
**状态**: 待工程实现

---

## 0. 方案概述

### 0.1 核心策略

灰模资产清单共 39 项，其中 35 项需要 Sprite/视觉资产，4 项为纯代码/粒子配置。本方案的核心策略：

| 策略 | 适用资产 | 数量 | 理由 |
|------|---------|------|------|
| **Runtime 代码生成** | 所有几何体 Sprite（圆形、方形、六边形、三角形、弧线段、扇形、圆环） | 31 项 | 形状固定、颜色从配置读取、无需美术制作，代码生成后缓存复用 |
| **手工制作** | 无 | 0 项 | 灰模阶段无任何手工 Sprite 需求 |
| **Unity 内置** | HUD 文本元素（TextMeshPro） | 4 项 | TMP 是 Unity 原生，不需要 Sprite |

**结论**：Demo 灰模阶段 **零手工 Sprite**——全部几何体由 `SpriteGenerator` 工具类在运行时生成，颜色从 JSON 配置注入。这与 ADR-001 数据驱动配置原则完全一致。

### 0.2 生成方式选型

| 方案 | API | 优点 | 缺点 | 采用 |
|------|-----|------|------|------|
| **A. Texture2D + Sprite.Create** | `new Texture2D()` → `SetPixel` → `Apply()` → `Sprite.Create()` | 完全控制像素、支持描边/空心/虚线 | 需手动管理纹理内存 | ✅ **主方案** |
| B. Graphics.Blit | `Graphics.Blit(src, dest, mat)` | GPU 加速 | 适合后处理，不适合生成单个 Sprite | ❌ |
| C. 预制 Sprite Atlas | 在 Editor 中预制 Sprite 打包 | 运行时零开销 | 需手工制作、颜色固定不可配置 | ❌ 违背数据驱动 |

**选 A 的理由**：
1. 灰模几何体形状简单（圆/方/六边形/三角/弧/扇形/圆环），像素操作量小（最大 1280×1280 的场地，其余均 <128px）
2. 颜色必须从 JSON 配置读取（ADR-001），`Texture2D.SetPixel` + `SpriteRenderer.color` 双重控制——纹理用白色，运行时通过 `SpriteRenderer.color` 注入学派色
3. 生成后缓存为静态 Sprite，整个游戏生命周期复用，无运行时开销

---

## 1. SpriteGenerator 工具类设计

### 1.1 类定位

```
Assets/_Project/Scripts/Foundation/SpriteGenerator/
├── SpriteGenerator.cs          # 核心生成工具类（静态）
├── SpriteShape.cs              # 形状枚举
└── GeneratedSpriteCache.cs     # 已生成 Sprite 的缓存管理
```

**架构归属**：Foundation Layer。被 Gameplay Layer（BulletSystem、EnemySystem 等）和 UI Layer 调用。

### 1.2 形状枚举

```csharp
public enum SpriteShape
{
    Circle,             // 实心圆——玩家、儒家弹幕、学识掉落
    Ring,               // 空心圆环——礼屏障、礼反弹圈、道宗师波纹
    Square,             // 实心方形——弟子
    SquareWithBorder,   // 带描边方形——精英弟子
    Hexagon,            // 六边形——Boss
    Triangle,           // 锐角三角形——法家弹幕
    Rectangle,          // 窄长矩形——射艺箭矢、御艺冲刺带
    Sector,             // 扇形——礼击推力波
    Arc,                // 弧线段（月牙形）——道家弹幕
    DashedRing          // 虚线圆环——气旋阵区域标记
}
```

### 1.3 核心 API

```csharp
/// <summary>
/// 灰模 Sprite 运行时生成工具。所有生成的 Sprite 纹理为纯白色，
/// 颜色通过 SpriteRenderer.color 在运行时注入（支持配置驱动）。
/// </summary>
public static class SpriteGenerator
{
    /// <summary>
    /// 生成指定形状的 Sprite，带可选描边。
    /// 生成的纹理为白色（fillColor=White, borderColor=White），
    /// 实际颜色由 SpriteRenderer.color 控制。
    /// </summary>
    /// <param name="shape">几何形状</param>
    /// <param name="pixelSize">主体尺寸（像素），圆形=直径，方形=边长，六边形=外接圆直径</param>
    /// <param name="borderWidth">描边宽度（像素），0=无描边</param>
    /// <param name="hollow">是否空心（圆环/虚线环用）</param>
    /// <param name="dashed">是否虚线描边（气旋阵用）</param>
    /// <param name="dashLength">虚线段长度（像素）</param>
    /// <param name="gapLength">虚线间隔长度（像素）</param>
    /// <returns>Sprite 对象（已缓存，重复调用返回同一实例）</returns>
    public static Sprite Generate(SpriteShape shape, int pixelSize, 
                                   int borderWidth = 0, bool hollow = false,
                                   bool dashed = false, int dashLength = 8, 
                                   int gapLength = 4)
    {
        // 缓存键：形状+尺寸+描边+空心+虚线参数
        string cacheKey = $"{shape}_{pixelSize}_{borderWidth}_{hollow}_{dashed}_{dashLength}_{gapLength}";
        
        if (GeneratedSpriteCache.TryGet(cacheKey, out var cached))
            return cached;
        
        // 计算 Texture2D 尺寸（含描边和抗锯齿余量）
        int padding = borderWidth + 2; // 2px 抗锯齿余量
        int textureSize = pixelSize + padding * 2;
        
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        
        // 初始化为透明
        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32[] pixels = new Color32[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = transparent;
        
        // 按形状绘制
        int center = textureSize / 2;
        switch (shape)
        {
            case SpriteShape.Circle:
                DrawCircle(pixels, textureSize, center, pixelSize / 2, 
                           borderWidth, hollow, dashed, dashLength, gapLength);
                break;
            case SpriteShape.Ring:
                DrawCircle(pixels, textureSize, center, pixelSize / 2, 
                           borderWidth, true, dashed, dashLength, gapLength);
                break;
            case SpriteShape.Square:
                DrawSquare(pixels, textureSize, pixelSize, 
                           borderWidth, hollow);
                break;
            case SpriteShape.SquareWithBorder:
                DrawSquare(pixels, textureSize, pixelSize, 
                           borderWidth, false);
                break;
            case SpriteShape.Hexagon:
                DrawPolygon(pixels, textureSize, center, pixelSize / 2, 
                            6, borderWidth, hollow);
                break;
            case SpriteShape.Triangle:
                DrawTriangle(pixels, textureSize, center, pixelSize, 
                             borderWidth);
                break;
            case SpriteShape.Rectangle:
                DrawRectangle(pixels, textureSize, pixelSize, 
                              borderWidth, hollow);
                break;
            case SpriteShape.Sector:
                DrawSector(pixels, textureSize, center, pixelSize / 2, 
                           90f, borderWidth);
                break;
            case SpriteShape.Arc:
                DrawArc(pixels, textureSize, center, pixelSize / 2, 
                        borderWidth);
                break;
            case SpriteShape.DashedRing:
                DrawCircle(pixels, textureSize, center, pixelSize / 2, 
                           borderWidth, true, true, dashLength, gapLength);
                break;
        }
        
        texture.SetPixels32(pixels);
        texture.Apply();
        
        // 创建 Sprite，以纹理中心为 pivot
        var sprite = Sprite.Create(texture, 
                                    new Rect(0, 0, textureSize, textureSize),
                                    new Vector2(0.5f, 0.5f), 
                                    pixelsPerUnit: 100f);
        
        GeneratedSpriteCache.Add(cacheKey, sprite);
        return sprite;
    }
}
```

### 1.4 缓存管理

```csharp
/// <summary>
/// 已生成 Sprite 的全局缓存。按 cacheKey 索引，避免重复生成相同参数的 Sprite。
/// 生命周期：Bootstrapper 阶段预生成常用尺寸，游戏运行中按需补充。
/// </summary>
public static class GeneratedSpriteCache
{
    private static readonly Dictionary<string, Sprite> _cache = new();
    
    public static bool TryGet(string key, out Sprite sprite)
    {
        return _cache.TryGetValue(key, out sprite);
    }
    
    public static void Add(string key, Sprite sprite)
    {
        if (!_cache.ContainsKey(key))
            _cache[key] = sprite;
    }
    
    /// <summary>
    /// Bootstrapper 阶段调用：预生成所有 Demo 需要的 Sprite 尺寸。
    /// 避免运行中首次生成时的帧卡顿。
    /// </summary>
    public static void PreloadAll()
    {
        // 玩家（圆形，48px，无描边）
        SpriteGenerator.Generate(SpriteShape.Circle, 48);
        
        // 玩家描边环（圆环，52px，3px边）
        SpriteGenerator.Generate(SpriteShape.Ring, 52, borderWidth: 3);
        
        // 弟子普通（方形，36px）
        SpriteGenerator.Generate(SpriteShape.Square, 36);
        
        // 弟子精英（方形，44px，4px描边）
        SpriteGenerator.Generate(SpriteShape.SquareWithBorder, 44, borderWidth: 4);
        
        // Boss（六边形，80px / 90px / 100px）
        SpriteGenerator.Generate(SpriteShape.Hexagon, 80);
        SpriteGenerator.Generate(SpriteShape.Hexagon, 90);
        SpriteGenerator.Generate(SpriteShape.Hexagon, 100);
        
        // 弹幕——玩家
        SpriteGenerator.Generate(SpriteShape.Rectangle, 24, borderWidth: 0); // 箭矢(长) — 见说明
        SpriteGenerator.Generate(SpriteShape.Sector, 96, borderWidth: 2);    // 推力波
        SpriteGenerator.Generate(SpriteShape.Ring, 64, borderWidth: 4);      // 礼屏障
        SpriteGenerator.Generate(SpriteShape.Ring, 128, borderWidth: 3);     // 反弹圈
        
        // 弹幕——敌人
        SpriteGenerator.Generate(SpriteShape.Circle, 16);                    // 儒家扩散弹
        SpriteGenerator.Generate(SpriteShape.Triangle, 20, borderWidth: 1);  // 法家直线弹
        SpriteGenerator.Generate(SpriteShape.Arc, 32, borderWidth: 8);       // 道家弧线弹
        
        // 弹幕——Boss
        SpriteGenerator.Generate(SpriteShape.Circle, 24);                    // Boss儒家弹
        SpriteGenerator.Generate(SpriteShape.Triangle, 28, borderWidth: 2);  // Boss法家弹
        SpriteGenerator.Generate(SpriteShape.Ring, 128, borderWidth: 3);     // Boss道宗师波纹
        
        // 学识掉落
        SpriteGenerator.Generate(SpriteShape.Circle, 8);                     // 小圆点
        
        // 场地
        // arena_ground 用 SpriteRenderer.drawMode = Sliced + 原生白色方形即可
        
        // 气旋阵
        SpriteGenerator.Generate(SpriteShape.DashedRing, 256, borderWidth: 3, 
                                  dashed: true, dashLength: 8, gapLength: 4);
        
        // 碎裂粒子碎片
        SpriteGenerator.Generate(SpriteShape.Square, 8);                     // 4-8px 碎片
    }
    
    public static void Clear()
    {
        foreach (var kvp in _cache)
        {
            if (kvp.Value != null && kvp.Value.texture != null)
                Object.Destroy(kvp.Value.texture);
        }
        _cache.Clear();
    }
}
```

> **箭矢说明**：射艺箭矢为"窄长矩形"（长24px×宽6px）。`SpriteShape.Rectangle` 的 `pixelSize` 参数取最大维度，内部按纵横比绘制。详见 §2.4 矩形生成逻辑。

---

## 2. 几何体生成伪逻辑

所有绘制函数操作 `Color32[]` 像素数组，最终通过 `Texture2D.SetPixels32()` 一次性提交。纹理为白色（RGBA=255,255,255,255），实际颜色由 `SpriteRenderer.color` 控制。

### 2.1 圆形 / 圆环

```csharp
/// <summary>
/// 绘制圆形或圆环。支持实心、空心（环）、虚线环。
/// 
/// 实心圆：hollow=false → 整个圆区域填充白色
/// 圆环：hollow=true, dashed=false → 仅描边区域填充白色
/// 虚线环：hollow=true, dashed=true → 描边按 dashLength/gapLength 间断填充
/// 
/// 用途：
///   实心圆 → player_base, bullet_confucian_spread, pickup_knowledge
///   圆环  → player_ring_*, bullet_li_barrier, bullet_li_reflect_circle
///   虚线环 → arena_cyclone_zone
/// </summary>
static void DrawCircle(Color32[] pixels, int textureSize, int center, 
                        int radius, int borderWidth, bool hollow, 
                        bool dashed, int dashLength, int gapLength)
{
    int radiusSq = radius * radius;
    int innerRadiusSq = (radius - borderWidth) * (radius - borderWidth);
    
    for (int y = 0; y < textureSize; y++)
    {
        for (int x = 0; x < textureSize; x++)
        {
            int dx = x - center;
            int dy = y - center;
            int distSq = dx * dx + dy * dy;
            
            if (hollow)
            {
                // 圆环：只填充 [innerRadius, radius] 之间的环带
                if (distSq <= radiusSq && distSq >= innerRadiusSq)
                {
                    if (dashed)
                    {
                        // 虚线：按角度判断当前像素是否在 dash 段内
                        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (angle < 0) angle += 360f;
                        float period = dashLength + gapLength;
                        float arcLengthPerPixel = 360f / (2f * Mathf.PI * radius);
                        float pixelArc = angle / arcLengthPerPixel;
                        if (pixelArc % period < dashLength)
                        {
                            pixels[y * textureSize + x] = Color.white;
                        }
                    }
                    else
                    {
                        pixels[y * textureSize + x] = Color.white;
                    }
                }
            }
            else
            {
                // 实心圆
                if (distSq <= radiusSq)
                {
                    pixels[y * textureSize + x] = Color.white;
                }
            }
        }
    }
}
```

### 2.2 方形 / 带描边方形

```csharp
/// <summary>
/// 绘制方形。支持实心和带描边。
/// 
/// 实心方形：borderWidth=0 → 整个方形区域填充白色
/// 带描边方形：borderWidth>0 → 内部填充白色 + 描边区域用更高亮度标记
///   （因为纹理全白，描边在灰模阶段通过 SpriteRenderer.color 无法区分亮度。
///    精英弟子描边方案：用两个 SpriteRenderer 叠加——底层填色 Sprite + 上层描边 Sprite。
///    或者：描边区域 alpha=1.0，内部区域 alpha=0.85，通过透明度差异区分。）
/// 
/// 用途：
///   实心方形 → disciple_*_normal
///   带描边方形 → disciple_*_elite
/// </summary>
static void DrawSquare(Color32[] pixels, int textureSize, int size, 
                        int borderWidth, bool hollow)
{
    int center = textureSize / 2;
    int half = size / 2;
    int innerHalf = half - borderWidth;
    
    for (int y = 0; y < textureSize; y++)
    {
        for (int x = 0; x < textureSize; x++)
        {
            int dx = x - center;
            int dy = y - center;
            
            if (Mathf.Abs(dx) <= half && Mathf.Abs(dy) <= half)
            {
                if (hollow)
                {
                    // 空心方形（暂无使用场景，预留）
                    if (Mathf.Abs(dx) > innerHalf || Mathf.Abs(dy) > innerHalf)
                        pixels[y * textureSize + x] = Color.white;
                }
                else if (borderWidth > 0)
                {
                    // 带描边：描边 alpha=1.0，内部 alpha=0.85
                    if (Mathf.Abs(dx) > innerHalf || Mathf.Abs(dy) > innerHalf)
                        pixels[y * textureSize + x] = new Color32(255, 255, 255, 255);
                    else
                        pixels[y * textureSize + x] = new Color32(255, 255, 255, 217); // 0.85*255≈217
                }
                else
                {
                    // 纯实心
                    pixels[y * textureSize + x] = Color.white;
                }
            }
        }
    }
}
```

### 2.3 六边形

```csharp
/// <summary>
/// 绘制正六边形（顶点朝上）。支持实心和空心。
/// 
/// 算法：对每个像素，判断是否在六边形内部。
/// 六边形可分解为 3 个区域：上方梯形 + 中间矩形 + 下方梯形。
/// 或用通用多边形包含测试。
/// 
/// 用途：boss_confucian, boss_legalist, boss_daoist
/// 尺寸：80px / 90px / 100px（外接圆直径）
/// </summary>
static void DrawPolygon(Color32[] pixels, int textureSize, int center, 
                         int radius, int sides, int borderWidth, bool hollow)
{
    // 预计算多边形顶点
    Vector2[] vertices = new Vector2[sides];
    for (int i = 0; i < sides; i++)
    {
        float angle = (Mathf.PI * 2f / sides) * i + Mathf.PI / 2f; // 顶点朝上
        vertices[i] = new Vector2(
            center + radius * Mathf.Cos(angle),
            center + radius * Mathf.Sin(angle)
        );
    }
    
    for (int y = 0; y < textureSize; y++)
    {
        for (int x = 0; x < textureSize; x++)
        {
            Vector2 point = new Vector2(x, y);
            bool inside = IsPointInPolygon(point, vertices);
            
            if (hollow)
            {
                // 空心：判断是否在描边带内
                if (inside)
                {
                    // 缩小多边形判断内部
                    float scale = 1f - (float)borderWidth / radius;
                    Vector2[] innerVertices = new Vector2[sides];
                    for (int i = 0; i < sides; i++)
                    {
                        innerVertices[i] = new Vector2(
                            center + (vertices[i].x - center) * scale,
                            center + (vertices[i].y - center) * scale
                        );
                    }
                    bool innerInside = IsPointInPolygon(point, innerVertices);
                    if (!innerInside)
                        pixels[y * textureSize + x] = Color.white;
                }
            }
            else
            {
                if (inside)
                    pixels[y * textureSize + x] = Color.white;
            }
        }
    }
}

/// <summary>
/// 射线法判断点是否在多边形内。
/// </summary>
static bool IsPointInPolygon(Vector2 point, Vector2[] vertices)
{
    int n = vertices.Length;
    bool inside = false;
    for (int i = 0, j = n - 1; i < n; j = i++)
    {
        if (((vertices[i].y > point.y) != (vertices[j].y > point.y)) &&
            (point.x < (vertices[j].x - vertices[i].x) * (point.y - vertices[i].y) 
             / (vertices[j].y - vertices[i].y) + vertices[i].x))
        {
            inside = !inside;
        }
    }
    return inside;
}
```

### 2.4 矩形（窄长矩形 = 箭矢）

```csharp
/// <summary>
/// 绘制窄长矩形。pixelSize = 长边，宽边 = 长边 / aspectRatio。
/// 
/// 用途：
///   bullet_archery_arrow → 长24px×宽6px (aspectRatio=4)
///   bullet_archery_charge → 长32px×宽8px (aspectRatio=4)
///   bullet_yu_dash_trail → 宽48px×长=冲刺距离 (动态宽度，用 SpriteRenderer.drawMode=Tiled)
/// 
/// 注意：御艺冲刺带是动态长度的矩形，不适合预生成 Sprite。
/// 方案：冲刺带用 SpriteRenderer.drawMode = Sliced/Tiled + 原生白色方形 Sprite，
///       通过 transform.localScale 控制长度，通过 color 控制颜色和透明度。
/// </summary>
static void DrawRectangle(Color32[] pixels, int textureSize, int longSide, 
                           int borderWidth, bool hollow)
{
    // 箭矢默认宽长比 1:4
    int shortSide = longSide / 4;
    int centerX = textureSize / 2;
    int centerY = textureSize / 2;
    int halfLong = longSide / 2;
    int halfShort = shortSide / 2;
    
    for (int y = 0; y < textureSize; y++)
    {
        for (int x = 0; x < textureSize; x++)
        {
            int dx = x - centerX;
            int dy = y - centerY;
            
            if (Mathf.Abs(dx) <= halfLong && Mathf.Abs(dy) <= halfShort)
            {
                pixels[y * textureSize + x] = Color.white;
            }
        }
    }
}
```

### 2.5 三角形（锐角三角形 = 法家弹幕）

```csharp
/// <summary>
/// 绘制锐角三角形（箭头形），尖端朝右（默认朝向）。
/// 运行时通过 transform.rotation 旋转到飞行方向。
/// 
/// 形状：底边在左，尖端在右。底12px×高20px（弟子版）或 底18px×高28px（Boss版）。
/// 支持白描边（灰阶可辨需要——法黑弹幕需白描边在灰阶下可见）。
/// 
/// 用途：
///   bullet_legalist_line → 底12×高20，白描边1px
///   bullet_boss_legalist_track → 底18×高28，白描边2px
/// </summary>
static void DrawTriangle(Color32[] pixels, int textureSize, int center, 
                          int height, int borderWidth)
{
    // 三角形顶点：尖端在右，底边在左
    // 顶点A (尖端): (center + height/2, center)
    // 顶点B (底上): (center - height/2, center + baseWidth/2)
    // 顶点C (底下): (center - height/2, center - baseWidth/2)
    int baseWidth = height * 12 / 20; // 底宽 = 高 × (12/20)
    
    Vector2[] vertices = new Vector2[3];
    vertices[0] = new Vector2(center + height / 2, center);           // 尖端
    vertices[1] = new Vector2(center - height / 2, center + baseWidth / 2); // 底上
    vertices[2] = new Vector2(center - height / 2, center - baseWidth / 2); // 底下
    
    for (int y = 0; y < textureSize; y++)
    {
        for (int x = 0; x < textureSize; x++)
        {
            Vector2 point = new Vector2(x, y);
            if (IsPointInPolygon(point, vertices))
            {
                // 检查是否在描边区域（距边缘 borderWidth 像素以内）
                if (borderWidth > 0 && IsNearEdge(point, vertices, borderWidth))
                {
                    // 描边用纯白 alpha=1.0
                    pixels[y * textureSize + x] = new Color32(255, 255, 255, 255);
                }
                else
                {
                    // 内部用纯白 alpha=1.0（法黑弹幕的"黑色"由 SpriteRenderer.color=#1A1A1A 注入）
                    pixels[y * textureSize + x] = new Color32(255, 255, 255, 255);
                }
            }
        }
    }
    
    // 描边实现说明：
    // 法家弹幕需要"黑填色 + 白描边"。但 SpriteRenderer.color 只能整体着色。
    // 解决方案：法家弹幕用两个 SpriteRenderer 叠加：
    //   - 底层：三角形 Sprite，color=#1A1A1A（法黑填色），略大1px
    //   - 上层：三角形 Sprite，color=#FFFFFF（白描边），略小1px → 露出底层1px作为描边
    // 或者：三角形 Sprite 纹理中描边区域 alpha=1.0 内部 alpha=0.0（空心描边），
    //   底层放一个实心三角形 Sprite 作为填色。两层叠加实现黑填色+白描边。
    // 推荐方案：两层叠加（见 §4 Prefab 关联）
}

static bool IsNearEdge(Vector2 point, Vector2[] vertices, int threshold)
{
    for (int i = 0; i < vertices.Length; i++)
    {
        int j = (i + 1) % vertices.Length;
        float dist = DistanceToLineSegment(point, vertices[i], vertices[j]);
        if (dist <= threshold)
            return true;
    }
    return false;
}

static float DistanceToLineSegment(Vector2 p, Vector2 a, Vector2 b)
{
    Vector2 ab = b - a;
    float t = Vector2.Dot(p - a, ab) / ab.sqrMagnitude;
    t = Mathf.Clamp01(t);
    Vector2 projection = a + t * ab;
    return Vector2.Distance(p, projection);
}
```

### 2.6 扇形（礼击推力波）

```csharp
/// <summary>
/// 绘制扇形。默认朝右（0度），扇角90度。
/// 运行时通过 transform.rotation 旋转到释放方向。
/// 
/// 用途：bullet_li_push_wave → 半径96px，扇角90°
/// 
/// 算法：对每个像素，判断是否在扇形半径内 且 角度在 [-sectorAngle/2, +sectorAngle/2] 范围内。
/// </summary>
static void DrawSector(Color32[] pixels, int textureSize, int center, 
                        int radius, float sectorAngle, int borderWidth)
{
    float halfAngle = sectorAngle / 2f;
    int radiusSq = radius * radius;
    
    for (int y = 0; y < textureSize; y++)
    {
        for (int x = 0; x < textureSize; x++)
        {
            int dx = x - center;
            int dy = y - center;
            int distSq = dx * dx + dy * dy;
            
            if (distSq <= radiusSq)
            {
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                // 归一化到 [-180, 180]
                if (angle < -180f) angle += 360f;
                if (angle > 180f) angle -= 360f;
                
                if (angle >= -halfAngle && angle <= halfAngle)
                {
                    pixels[y * textureSize + x] = Color.white;
                }
            }
        }
    }
}
```

### 2.7 弧线段（月牙形 = 道家弹幕）

```csharp
/// <summary>
/// 绘制弧线段（月牙形）。道家弹幕专用。
/// 
/// 形状：一段弯曲的弧线，像月牙。弧长32px，弧宽8px。
/// 算法：绘制两个不同半径的圆弧，取差集。
///   外弧半径 R，内弧半径 R - arcWidth，弧长对应的圆心角 = arcLength / R。
/// 
/// 用途：bullet_daoist_arc → 弧长32px，弧宽8px
/// </summary>
static void DrawArc(Color32[] pixels, int textureSize, int center, 
                     int radius, int arcWidth)
{
    int outerRadiusSq = radius * radius;
    int innerRadiusSq = (radius - arcWidth) * (radius - arcWidth);
    
    // 弧线对应的圆心角（弧度）= 弧长 / 半径
    float arcAngle = 32f / radius; // 32px 弧长
    
    for (int y = 0; y < textureSize; y++)
    {
        for (int x = 0; x < textureSize; x++)
        {
            int dx = x - center;
            int dy = y - center;
            int distSq = dx * dx + dy * dy;
            
            // 在外弧和内弧之间的环带
            if (distSq <= outerRadiusSq && distSq >= innerRadiusSq)
            {
                float angle = Mathf.Atan2(dy, dx);
                // 限制弧线段范围：[-arcAngle/2, +arcAngle/2]
                if (Mathf.Abs(angle) <= arcAngle / 2f)
                {
                    pixels[y * textureSize + x] = Color.white;
                }
            }
        }
    }
}
```

---

## 3. 颜色编码方案

### 3.1 颜色配置 JSON

颜色从 `schools.json`（ADR-001 配置层）读取，符合数据驱动架构。美术侧不硬编码任何颜色。

```json
// schools.json 中的颜色配置段
{
  "schoolColors": {
    "confucian": { "hex": "#D4A017", "r": 212, "g": 160, "b": 23 },
    "legalist":  { "hex": "#1A1A1A", "r": 26,  "g": 26,  "b": 26 },
    "daoist":    { "hex": "#2E8B8B", "r": 46,  "g": 139, "b": 139 },
    "neutral":   { "hex": "#FFFFFF", "r": 255, "g": 255, "b": 255 }
  },
  "vfxColors": {
    "hitFlashWhite": { "hex": "#FFFFFF", "r": 255, "g": 255, "b": 255 },
    "hitFlashRed":   { "hex": "#FF3030", "r": 255, "g": 48,  "b": 48 }
  },
  "hudColors": {
    "hpGreen":  { "hex": "#4CAF50", "r": 76,  "g": 175, "b": 80 },
    "hpYellow": { "hex": "#FFC107", "r": 255, "g": 193, "b": 7 },
    "hpRed":    { "hex": "#F44336", "r": 244, "g": 67,  "b": 54 },
    "staminaBlue": { "hex": "#2196F3", "r": 33, "g": 150, "b": 243 },
    "hudBg":    { "hex": "#2A2A2A", "r": 42,  "g": 42,  "b": 42 }
  },
  "arenaColors": {
    "ground":     { "hex": "#3A3A3A", "r": 58,  "g": 58,  "b": 58 },
    "water":      { "hex": "#2A3A3A", "r": 42,  "g": 58,  "b": 58 },
    "cycloneZone":{ "hex": "#FFFFFF", "r": 255, "g": 255, "b": 255, "a": 51 }
  }
}
```

### 3.2 C# 颜色绑定

```csharp
[System.Serializable]
public class SchoolColorConfig
{
    public string hex;
    public int r;
    public int g;
    public int b;
    public int a = 255; // 默认不透明
    
    public Color ToColor()
    {
        return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }
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
public class ArtColorConfig
{
    public SchoolColorsConfig schoolColors;
    public Dictionary<string, SchoolColorConfig> vfxColors;
    public Dictionary<string, SchoolColorConfig> hudColors;
    public Dictionary<string, SchoolColorConfig> arenaColors;
}
```

### 3.3 运行时颜色注入

```csharp
/// <summary>
/// 颜色注入器。从配置读取颜色，注入到 SpriteRenderer。
/// 所有 Sprite 纹理为白色，颜色 100% 由 SpriteRenderer.color 控制。
/// </summary>
public static class ColorInjector
{
    private static ArtColorConfig _config;
    
    public static void Initialize(ArtColorConfig config)
    {
        _config = config;
    }
    
    /// <summary>
    /// 按学派获取颜色。
    /// </summary>
    public static Color GetSchoolColor(string school)
    {
        return school switch
        {
            "Confucian" => _config.schoolColors.confucian.ToColor(),
            "Legalist"  => _config.schoolColors.legalist.ToColor(),
            "Taoist"    => _config.schoolColors.daoist.ToColor(),
            _           => _config.schoolColors.neutral.ToColor()
        };
    }
    
    /// <summary>
    /// 将颜色注入到 SpriteRenderer。
    /// </summary>
    public static void ApplyColor(SpriteRenderer renderer, string school)
    {
        renderer.color = GetSchoolColor(school);
    }
    
    /// <summary>
    /// 带透明度的颜色注入（波纹圈、冲刺带等需要半透明）。
    /// </summary>
    public static void ApplyColorWithAlpha(SpriteRenderer renderer, 
                                            string school, float alpha)
    {
        Color c = GetSchoolColor(school);
        c.a = alpha;
        renderer.color = c;
    }
}
```

### 3.4 灰阶测试配色替换

灰阶测试模式（Basic §1.1）下，颜色配置可一键替换为灰阶配色：

```json
// schools_grayscale.json（灰阶测试用配置）
{
  "schoolColors": {
    "confucian": { "hex": "#8B8B8B", "r": 139, "g": 139, "b": 139 },  // 灰阶值~139
    "legalist":  { "hex": "#1A1A1A", "r": 26,  "g": 26,  "b": 26 },   // 灰阶值~26
    "daoist":    { "hex": "#707070", "r": 112, "g": 112, "b": 112 },  // 灰阶值~112
    "neutral":   { "hex": "#FFFFFF", "r": 255, "g": 255, "b": 255 }   // 灰阶值~255
  }
}
```

> 灰阶测试有两种实现方式：① 后处理 Shader 去色（推荐，见灰阶 Shader 规格文档）；② 配置替换为灰阶配色。两种方式可并用——后处理用于快速切换测试，配置替换用于精确控制灰阶值差异。

---

## 4. Prefab 关联方案

### 4.1 Prefab 结构规范

每个需要 Sprite 的实体对应一个 Prefab，Prefab 中的 SpriteRenderer 在 `Awake()` 或 `Start()` 时从 `SpriteGenerator` 获取 Sprite 并从 `ColorInjector` 获取颜色。

```
Assets/_Project/Prefabs/
├── Player/
│   └── PlayerBase.prefab
│       ├── PlayerBody (SpriteRenderer)     ← Circle(48px), color=学派色
│       └── PlayerRing (SpriteRenderer)     ← Ring(52px,3px), color=学派色
├── Enemies/
│   ├── DiscipleNormal.prefab
│   │   └── Body (SpriteRenderer)           ← Square(36px), color=学派色
│   ├── DiscipleElite.prefab
│   │   ├── Body (SpriteRenderer)           ← SquareWithBorder(44px,4px), color=学派色
│   │   └── BorderHighlight (SpriteRenderer)← 可选：叠加描边高亮层
│   └── ...
├── Bosses/
│   └── BossBase.prefab
│       └── Body (SpriteRenderer)           ← Hexagon(80/90/100px), color=阶段色
├── Bullets/
│   ├── BulletArrow.prefab
│   │   └── Body (SpriteRenderer)           ← Rectangle(24px), color=#FFFFFF
│   ├── BulletCircle.prefab
│   │   └── Body (SpriteRenderer)           ← Circle(16px), color=儒金
│   ├── BulletTriangle.prefab
│   │   ├── Fill (SpriteRenderer)           ← Triangle(20px), color=法黑
│   │   └── Border (SpriteRenderer)         ← Triangle(20px+1px), color=#FFFFFF (白描边)
│   ├── BulletArc.prefab
│   │   └── Body (SpriteRenderer)           ← Arc(32px,8px), color=道青
│   ├── BulletRing.prefab
│   │   └── Body (SpriteRenderer)           ← Ring(可变,3-4px), color=学派色
│   └── BulletSector.prefab
│       └── Body (SpriteRenderer)           ← Sector(96px,90°), color=#FFFFFF
├── Effects/
│   ├── DeathShatter.prefab                 ← ParticleSystem
│   └── HitFlashTarget.prefab               ← 空Prefab，HitFlash通过代码控制
└── UI/
    └── (HUD 用 UGUI/TextMeshPro，不需 Prefab 中的 SpriteRenderer)
```

### 4.2 Sprite 注入组件

每个需要 Sprite 的 Prefab 挂载一个 `SpriteInitializer` 组件，在 `Awake()` 中完成 Sprite 和颜色注入：

```csharp
/// <summary>
/// 挂载在需要运行时生成 Sprite 的 Prefab 上。
/// 在 Awake() 中从 SpriteGenerator 获取 Sprite，从 ColorInjector 获取颜色。
/// </summary>
public class SpriteInitializer : MonoBehaviour
{
    [Header("Sprite 配置")]
    [SerializeField] private SpriteShape _shape;
    [SerializeField] private int _pixelSize;
    [SerializeField] private int _borderWidth;
    [SerializeField] private bool _hollow;
    [SerializeField] private bool _dashed;
    [SerializeField] private int _dashLength = 8;
    [SerializeField] private int _gapLength = 4;
    
    [Header("颜色配置")]
    [SerializeField] private string _colorSource = "Neutral"; // Confucian/Legalist/Taoist/Neutral/Custom
    [SerializeField] private float _alpha = 1.0f;
    
    private SpriteRenderer _renderer;
    
    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        
        // 生成/获取缓存的 Sprite
        _renderer.sprite = SpriteGenerator.Generate(
            _shape, _pixelSize, _borderWidth, _hollow, 
            _dashed, _dashLength, _gapLength);
        
        // 注入颜色
        if (_colorSource == "Custom")
        {
            // 自定义颜色（如场地灰色），从 arenaColors 配置读取
            // 具体实现根据配置结构调整
        }
        else
        {
            ColorInjector.ApplyColorWithAlpha(_renderer, _colorSource, _alpha);
        }
    }
    
    /// <summary>
    /// 运行时动态切换颜色（如 Boss 阶段转换时改变亮度）。
    /// </summary>
    public void SetColor(Color color)
    {
        if (_renderer != null)
            _renderer.color = color;
    }
    
    /// <summary>
    /// 运行时动态切换 Sprite（如 Boss 阶段转换时增大尺寸）。
    /// </summary>
    public void SetSprite(SpriteShape shape, int pixelSize, int borderWidth = 0)
    {
        if (_renderer != null)
            _renderer.sprite = SpriteGenerator.Generate(shape, pixelSize, borderWidth);
    }
}
```

### 4.3 法家弹幕描边方案（双层 SpriteRenderer）

法家弹幕需要"黑填色 + 白描边"以通过灰阶可辨测试。方案：Prefab 中用两层 SpriteRenderer 叠加：

```
BulletTriangle.prefab
├── Fill (SpriteRenderer)     ← Triangle(20px), sortingOrder=0, color=#1A1A1A(法黑)
│     └── SpriteInitializer: shape=Triangle, pixelSize=20, colorSource=Legalist
└── Border (SpriteRenderer)   ← Triangle(22px), sortingOrder=1, color=#FFFFFF(白)
      └── SpriteInitializer: shape=Triangle, pixelSize=22, borderWidth=1, colorSource=Neutral
```

- Fill 层：20px 三角形，法黑色，sortingOrder=0（底层）
- Border 层：22px 三角形（比 Fill 大 2px），白色，sortingOrder=1（上层）
- 效果：白色大三角形露出底层 1px 边缘 = 白描边效果
- Boss 版法家弹幕：Fill=28px, Border=30px（描边 2px）

### 4.4 御艺冲刺带方案（动态尺寸）

冲刺带是动态长度的矩形，不适合预生成固定尺寸 Sprite。方案：

```csharp
/// <summary>
/// 御艺冲刺带。用 Unity 内置白色方形 Sprite + drawMode=Sliced + transform.localScale 控制长度。
/// 颜色 = 玩家学派色，透明度 = 0.4，渐变淡出通过协程控制 alpha。
/// </summary>
public class DashTrailController : MonoBehaviour
{
    private SpriteRenderer _renderer;
    
    // 使用 Unity 内置 1x1 白色方形（通过 SpriteGenerator.Generate(SpriteShape.Square, 1) 获取）
    
    public void Initialize(Vector3 startPos, Vector3 endPos, Color schoolColor)
    {
        _renderer = GetComponent<SpriteRenderer>();
        _renderer.sprite = SpriteGenerator.Generate(SpriteShape.Square, 1);
        _renderer.drawMode = SpriteDrawMode.Sliced;
        
        // 计算位置、旋转、尺寸
        Vector3 midPoint = (startPos + endPos) / 2f;
        float length = Vector3.Distance(startPos, endPos);
        
        transform.position = midPoint;
        transform.rotation = Quaternion.LookRotation(
            Vector3.forward, 
            (endPos - startPos).normalized);
        
        _renderer.size = new Vector2(length, 0.48f); // 宽48px=0.48单位
        
        // 颜色 + 半透明
        Color c = schoolColor;
        c.a = 0.4f;
        _renderer.color = c;
        
        // 启动淡出协程
        StartCoroutine(FadeOut(0.8f));
    }
    
    IEnumerator FadeOut(float duration)
    {
        float elapsed = 0f;
        Color startColor = _renderer.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Color c = startColor;
            c.a = Mathf.Lerp(0.4f, 0f, elapsed / duration);
            _renderer.color = c;
            yield return null;
        }
        // 回收到对象池
    }
}
```

---

## 5. 生成时机

### 5.1 预生成（Bootstrapper 阶段）

**时机**：`Bootstrapper.cs` 的 `Start()` 中，在配置加载完成后、游戏状态进入 MainMenu 之前。

```csharp
// Bootstrapper.cs 片段
void Start()
{
    // 1. 加载所有 JSON 配置
    ConfigLoader.LoadAll();
    ConfigValidator.Validate();
    
    // 2. 初始化颜色注入器
    var artConfig = ConfigLoader.GetConfig<ArtColorConfig>();
    ColorInjector.Initialize(artConfig);
    
    // 3. 预生成所有 Sprite（~20种尺寸，总计 <500KB 纹理内存）
    GeneratedSpriteCache.PreloadAll();
    
    // 4. 进入主菜单
    GameStateMachine.TransitionTo(GameState.MainMenu);
}
```

**预生成性能估算**：
- 最大纹理：1280×1280（arena_ground，但用内置方形不需要生成）
- 实际预生成纹理：最大 256×256（气旋阵虚线环），大部分 <128×128
- 总纹理内存：~20 个 Sprite × 平均 64×64 × 4 bytes = ~320KB
- 预生成耗时：~10-20ms（一次性，在启动画面之后）

### 5.2 按需生成（运行中）

极少数情况下，运行中可能需要动态生成新尺寸的 Sprite（如 Boss 阶段转换时尺寸变化）。`SpriteGenerator.Generate()` 内部有缓存检查，已生成的尺寸直接返回缓存，未生成的才创建新纹理。

Boss 阶段转换示例：
```csharp
// BossSystem.EnterPhase() 中
public void EnterPhase(int phaseIndex)
{
    var phase = _config.phases[phaseIndex - 1];
    
    // 更新 Boss Sprite 尺寸和颜色
    int newSize = phaseIndex switch
    {
        1 => 80,
        2 => 90,
        3 => 100,
        _ => 80
    };
    
    var spriteInit = _bossEntity.GetComponent<SpriteInitializer>();
    spriteInit.SetSprite(SpriteShape.Hexagon, newSize);
    
    // 阶段转换脉冲动画
    StartCoroutine(PhaseTransitionPulse(_bossEntity.transform));
}

IEnumerator PhaseTransitionPulse(Transform target)
{
    Vector3 originalScale = target.localScale;
    Vector3 pulsedScale = originalScale * 1.2f;
    
    float elapsed = 0f;
    float duration = 0.2f; // 200ms
    
    // 放大
    while (elapsed < duration / 2f)
    {
        elapsed += Time.deltaTime;
        target.localScale = Vector3.Lerp(originalScale, pulsedScale, elapsed / (duration / 2f));
        yield return null;
    }
    
    // 恢复
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        target.localScale = Vector3.Lerp(pulsedScale, originalScale, (elapsed - duration / 2f) / (duration / 2f));
        yield return null;
    }
    
    target.localScale = originalScale;
}
```

---

## 6. 35 项资产逐项生成方案表

> 以下表格覆盖所有 35 项需 Sprite/视觉资产的条目。4 项非 Sprite 资产（vfx_hit_flash、vfx_death_shatter、hud_knowledge_counter、hud_wave_indicator、hud_boss_phase_indicator 中的文本类）标注为"非 Sprite"。

### 6.1 玩家角色（4 项）

| # | 资产名 | 形状 | 尺寸(px) | 颜色HEX | 生成方式 | Prefab 路径 | 备注 |
|---|--------|------|---------|---------|---------|------------|------|
| 1 | `player_base` | Circle | 48 | #FFFFFF | SpriteGenerator.Generate(Circle, 48) | Prefabs/Player/PlayerBase.prefab → Body | 纹理白色，运行时 color=学派色（但玩家基底为白色，学派色通过 Ring 体现） |
| 2 | `player_ring_confucian` | Ring | 52, border=3 | #D4A017 | SpriteGenerator.Generate(Ring, 52, 3) | Prefabs/Player/PlayerBase.prefab → Ring | color=儒金，从配置注入 |
| 3 | `player_ring_legalist` | Ring | 52, border=3 | #1A1A1A | SpriteGenerator.Generate(Ring, 52, 3) | 同上 Prefab → Ring | color=法黑。注意：法黑描边在深色背景上不可见，需背景为中灰(#3A3A3A)保证对比度 |
| 4 | `player_ring_daoist` | Ring | 52, border=3 | #2E8B8B | SpriteGenerator.Generate(Ring, 52, 3) | 同上 Prefab → Ring | color=道青 |

**Prefab 设计**：`PlayerBase.prefab` 包含两个子物体——Body(圆形) + Ring(圆环)。Ring 的颜色根据玩家选择的学派在角色选择后注入。三个学派的 Ring 用同一个 Ring Sprite（52px, 3px边），仅颜色不同。

### 6.2 弟子（6 项）

| # | 资产名 | 形状 | 尺寸(px) | 颜色HEX | 生成方式 | Prefab 路径 | 备注 |
|---|--------|------|---------|---------|---------|------------|------|
| 5 | `disciple_confucian_normal` | Square | 36 | #D4A017 | Generate(Square, 36) | Prefabs/Enemies/DiscipleNormal.prefab | color=儒金 |
| 6 | `disciple_confucian_elite` | SquareWithBorder | 44, border=4 | #D4A017 填 + #8B6914 描边 | Generate(SquareWithBorder, 44, 4) | Prefabs/Enemies/DiscipleElite.prefab | 描边通过 alpha 差异(1.0 vs 0.85)+ 深金描边色叠加。方案：两层 SpriteRenderer，底层填色(alpha=0.85)，上层描边环(alpha=1.0, color=深金) |
| 7 | `disciple_legalist_normal` | Square | 36 | #1A1A1A | Generate(Square, 36) | DiscipleNormal.prefab | color=法黑。需中灰背景保证可见 |
| 8 | `disciple_legalist_elite` | SquareWithBorder | 44, border=4 | #1A1A1A 填 + #4A4A4A 描边 | Generate(SquareWithBorder, 44, 4) | DiscipleElite.prefab | 同 #6 方案，描边色=灰白 |
| 9 | `disciple_daoist_normal` | Square | 36 | #2E8B8B | Generate(Square, 36) | DiscipleNormal.prefab | color=道青 |
| 10 | `disciple_daoist_elite` | SquareWithBorder | 44, border=4 | #2E8B8B 填 + #1A5C5C 描边 | Generate(SquareWithBorder, 44, 4) | DiscipleElite.prefab | 同 #6 方案，描边色=深青 |

**Prefab 复用**：`DiscipleNormal.prefab` 和 `DiscipleElite.prefab` 各一个，通过 `SpriteInitializer` 的 `_colorSource` 参数区分学派。6 种弟子 = 2 个 Prefab × 3 种颜色配置。

### 6.3 Boss（3 项）

| # | 资产名 | 形状 | 尺寸(px) | 颜色HEX | 生成方式 | Prefab 路径 | 备注 |
|---|--------|------|---------|---------|---------|------------|------|
| 11 | `boss_confucian` | Hexagon | 80/90/100 | P1:#D4A017 P2:#FFD700 P3:#FFEC8B | Generate(Hexagon, 80/90/100) | Prefabs/Bosses/BossConfucian.prefab | 阶段转换时 SetSprite() 更换尺寸 + SetColor() 更换亮度色 |
| 12 | `boss_legalist` | Hexagon | 80/90/100 | P1:#1A1A1A P2:#2A2A2A P3:#0A0A0A | Generate(Hexagon, 80/90/100) | Prefabs/Bosses/BossLegalist.prefab | 同上。法宗师阶段3(#0A0A0A)在灰背景上极暗，需白描边。方案：Boss 六边形也用双层 SpriteRenderer，底层填色 + 上层白描边环(1px) |
| 13 | `boss_daoist` | Hexagon | 80/85/95 | P1:#2E8B8B P2:#40E0D0 P3:#00CED1 | Generate(Hexagon, 80/85/95) | Prefabs/Bosses/BossDaoist.prefab | 道宗师阶段2有瞬移，瞬移时 alpha 淡出100ms→淡入100ms |

### 6.4 玩家弹幕（7 项）

| # | 资产名 | 形状 | 尺寸(px) | 颜色HEX | 生成方式 | Prefab 路径 | 备注 |
|---|--------|------|---------|---------|---------|------------|------|
| 14 | `bullet_archery_arrow` | Rectangle | 24×6 | #FFFFFF | Generate(Rectangle, 24) | Prefabs/Bullets/BulletArrow.prefab | 纹理白色，color=白色。朝飞行方向旋转 |
| 15 | `bullet_archery_charge` | Rectangle | 32×8 | #FFFFFF (亮) | Generate(Rectangle, 32) | BulletArrow.prefab (共用，尺寸不同) | 蓄力箭。比普通箭更长。可通过 SetSprite() 动态切换。或用独立 Prefab BulletArrowCharged.prefab |
| 16 | `bullet_yu_dash_trail` | Rectangle(动态) | 宽48×动态长 | 学派色 α=0.4 | 内置方形 + drawMode=Sliced | Prefabs/Effects/DashTrail.prefab | 动态长度，见 §4.4 方案 |
| 17 | `bullet_li_push_wave` | Sector | 96, 90° | #FFFFFF | Generate(Sector, 96, borderWidth:2) | Prefabs/Bullets/BulletSector.prefab | 扇形，朝释放方向旋转。扩散动画(0→96px, 150ms) |
| 18 | `bullet_li_barrier` | Ring | 64, border=4 | #FFFFFF | Generate(Ring, 64, 4) | Prefabs/Bullets/BulletBarrier.prefab | 持续5s，呼吸效果(α 0.6↔0.9, 1s周期) |
| 19 | `bullet_li_barrier_thorn` | Rectangle(短) | 8×2 | #FFFFFF | Generate(Rectangle, 8) | BulletBarrier.prefab 子物体 | 屏障内8根短线刺，每0.2s刷新位置 |
| 20 | `bullet_li_reflect_circle` | Ring | 128, border=4 | #FFFFFF | Generate(Ring, 128, 4) | Prefabs/Bullets/BulletReflectRing.prefab | 扩散动画(0→128px, 300ms)。不透明 α=1.0 |

### 6.5 敌人弹幕（3 项）

| # | 资产名 | 形状 | 尺寸(px) | 颜色HEX | 生成方式 | Prefab 路径 | 备注 |
|---|--------|------|---------|---------|---------|------------|------|
| 21 | `bullet_confucian_spread` | Circle | 16 | #D4A017 | Generate(Circle, 16) | Prefabs/Bullets/BulletCircle.prefab | 命中后溅射圈(64px)用 Ring Sprite 临时显示 |
| 22 | `bullet_legalist_line` | Triangle | 底12×高20 | #1A1A1A 填 + #FFFFFF 描边1px | Generate(Triangle, 20, 1) | Prefabs/Bullets/BulletTriangle.prefab | 双层 SpriteRenderer：Fill(法黑)+Border(白)。见 §4.3 |
| 23 | `bullet_daoist_arc` | Arc | 弧长32, 弧宽8 | #2E8B8B | Generate(Arc, 32, 8) | Prefabs/Bullets/BulletArc.prefab | 月牙形，沿弧线轨迹飘忽飞行 |

### 6.6 Boss 弹幕（3 项）

| # | 资产名 | 形状 | 尺寸(px) | 颜色HEX | 生成方式 | Prefab 路径 | 备注 |
|---|--------|------|---------|---------|---------|------------|------|
| 24 | `bullet_boss_confucian_spread` | Circle | 24 | #D4A017(更亮) | Generate(Circle, 24) | BulletCircle.prefab (共用) | 比弟子弹大50%。溅射圈96px。阶段3溅射×2 |
| 25 | `bullet_boss_legalist_track` | Triangle | 底18×高28 | #1A1A1A 填 + #FFFFFF 描边2px | Generate(Triangle, 28, 2) | BulletTriangle.prefab (共用) | 双层：Fill(28px,法黑)+Border(30px,白,2px描边) |
| 26 | `bullet_boss_daoist_ripple` | Ring | 128+, border=3 | #2E8B8B α=0.7 | Generate(Ring, 128, 3) | Prefabs/Bullets/BulletRipple.prefab | 半透明扩散圆环。与反弹圈区分：α=0.7(波纹) vs α=1.0(反弹圈)，border=3px(波纹) vs 4px(反弹圈) |

### 6.7 学识掉落（1 项）

| # | 资产名 | 形状 | 尺寸(px) | 颜色HEX | 生成方式 | Prefab 路径 | 备注 |
|---|--------|------|---------|---------|---------|------------|------|
| 27 | `pickup_knowledge` | Circle | 8 | #FFFFFF | Generate(Circle, 8) | Prefabs/Effects/KnowledgePickup.prefab | 微弱呼吸(α 0.6↔1.0, 0.8s周期)。拾取时缩放消失(1.0→0, 100ms) |

### 6.8 场地（3 项）

| # | 资产名 | 形状 | 尺寸(px) | 颜色HEX | 生成方式 | Prefab 路径 | 备注 |
|---|--------|------|---------|---------|---------|------------|------|
| 28 | `arena_ground` | Square(超大) | 1280×1280 | #3A3A3A | 内置白色方形 + drawMode=Sliced + color=#3A3A3A | Prefabs/Arena/ArenaGround.prefab | 20×20单位。用 SpriteRenderer.size 控制尺寸，不需要生成 1280px 纹理 |
| 29 | `arena_water` | Square(超大) | 1280×1280 | #2A3A3A α=0.5 | 同上 + color=#2A3A3A, α=0.5 | Prefabs/Arena/ArenaWater.prefab | 道宗师Boss战场地。半透明叠加在 ground 之上 |
| 30 | `arena_cyclone_zone` | DashedRing | 256, border=3 | #FFFFFF α=0.2 | Generate(DashedRing, 256, 3, dashed:true, dashLength:8, gapLength:4) | Prefabs/Arena/CycloneZone.prefab | 半透明虚线圆环。缓慢旋转(2s/圈) |

### 6.9 命中闪烁（1 项 · 非 Sprite）

| # | 资产名 | 类型 | 生成方式 | 备注 |
|---|--------|------|---------|------|
| 31 | `vfx_hit_flash` | SpriteRenderer.color 协程 | 代码控制，无独立 Sprite 资产 | 白→红→原色，50ms。见粒子特效参数表文档中的协程伪代码 |

### 6.10 死亡碎裂粒子（1 项 · Particle System 配置）

| # | 资产名 | 类型 | 生成方式 | 备注 |
|---|--------|------|---------|------|
| 32 | `vfx_death_shatter` | Particle System | 粒子 Sprite = Generate(Square, 8)。ParticleSystem 参数见粒子特效参数表文档 | 继承死亡对象颜色 |

### 6.11 HUD（7 项）

| # | 资产名 | 类型 | 尺寸(px) | 颜色HEX | 生成方式 | 备注 |
|---|--------|------|---------|---------|---------|------|
| 33 | `hud_hp_bar_bg` | UGUI Image | 200×16 | #2A2A2A | Unity 内置 UGUI Image，color 从配置注入 | 不需要 Sprite 生成 |
| 34 | `hud_hp_bar_fill` | UGUI Image | 0-200×16 | #4CAF50→#FFC107→#F44336 | 同上，颜色按 HP 分段动态切换 | Image.type=Filled, fillMethod=Horizontal |
| 35 | `hud_stamina_bar_bg` | UGUI Image | 150×10 | #2A2A2A | 同上 | |
| 36 | `hud_stamina_bar_fill` | UGUI Image | 0-150×10 | #2196F3 | 同上，fillMethod=Horizontal | Standard 级：体力不足变红闪烁 |
| 37 | `hud_knowledge_counter` | TextMeshPro | 字号20 | #FFFFFF | TMP 组件，非 Sprite | 数字变化时短暂放大(1.0→1.2→1.0, 150ms) |
| 38 | `hud_wave_indicator` | TextMeshPro | 字号16 | #FFFFFF | TMP 组件 | |
| 39 | `hud_boss_phase_indicator` | TextMeshPro | 字号14 | #FFFFFF | TMP 组件 | 阶段转换闪烁(白→红→白, 300ms) |

### 6.12 汇总统计

| 生成方式 | 资产数 | 说明 |
|---------|--------|------|
| SpriteGenerator.Generate() 代码生成 | 27 项 | 所有几何体 Sprite |
| 内置方形 + drawMode=Sliced | 4 项 | 场地(2) + 冲刺带(1) + HUD条(1, 填充条) |
| UGUI Image（不需 Sprite 生成） | 3 项 | HUD 背景条(2) + HP填充条(1) |
| TextMeshPro（非 Sprite） | 3 项 | HUD 文本(3) |
| 代码控制（无 Sprite 资产） | 1 项 | 命中闪烁 |
| Particle System 配置 | 1 项 | 死亡碎裂 |
| **合计** | **39 项** | 其中 35 项需 Sprite/视觉资产 |

**实际需 SpriteGenerator 生成的唯一 Sprite 形状/尺寸组合**：约 20 种（预生成列表见 §1.4 PreloadAll）

---

## 7. 性能考量

### 7.1 纹理内存

| Sprite | 尺寸 | 内存(RGBA32) | 数量 |
|--------|------|-------------|------|
| Circle 8px | 12×12 | 576 B | 1 |
| Circle 16px | 20×20 | 1.6 KB | 1 |
| Circle 24px | 28×28 | 3.1 KB | 1 |
| Circle 48px | 52×52 | 10.8 KB | 1 |
| Square 36px | 40×40 | 6.4 KB | 1 |
| SquareWithBorder 44px | 52×52 | 10.8 KB | 1 |
| Hexagon 80px | 84×84 | 28.2 KB | 1 |
| Hexagon 90px | 94×94 | 35.3 KB | 1 |
| Hexagon 100px | 104×104 | 43.3 KB | 1 |
| Rectangle 24px | 28×12 | 1.3 KB | 1 |
| Rectangle 32px | 36×14 | 2.0 KB | 1 |
| Triangle 20px | 24×24 | 2.3 KB | 1 |
| Triangle 22px | 26×26 | 2.7 KB | 1 |
| Triangle 28px | 32×32 | 4.1 KB | 1 |
| Triangle 30px | 34×34 | 4.6 KB | 1 |
| Arc 32px | 40×40 | 6.4 KB | 1 |
| Ring 52px | 58×58 | 13.5 KB | 1 |
| Ring 64px | 70×70 | 19.6 KB | 1 |
| Ring 128px | 134×134 | 71.8 KB | 1 |
| Sector 96px | 100×100 | 40.0 KB | 1 |
| DashedRing 256px | 262×262 | 274.9 KB | 1 |
| Square 1px (冲刺带/场地) | 5×5 | 100 B | 1 |
| **总计** | | | **~580 KB** |

**结论**：全部预生成 Sprite 的纹理内存 <600KB，远低于 500MB 预算（主架构文档 §10）。

### 7.2 Draw Call 优化

- 灰模阶段所有 Sprite 使用同一白色纹理变体，可通过 Sprite Atlas 合批
- 但由于 `SpriteRenderer.color` 各异（学派色不同），相同颜色的 Sprite 才能合批
- 预期 Draw Call：<50（远低于 100 预算）
- 建议：创建 Sprite Atlas 将所有生成的 Sprite 打包

```csharp
// 可选：运行时创建 Sprite Atlas（或在 Editor 中预打包）
// Demo 阶段可省略，灰模 Draw Call 数量低
```

### 7.3 对象池兼容

`SpriteGenerator` 生成的 Sprite 是静态资产，对象池中的对象（弹幕、弟子等）在 `Get()` 时只需将 Sprite 赋值给 SpriteRenderer（如果尚未赋值），在 `Return()` 时不需要清除 Sprite。颜色注入在 `Initialize()` 时完成。

---

## 8. 待确认项与风险

| 项目 | 说明 | 需要确认方 |
|------|------|-----------|
| SpriteInitializer 序列化字段 | `SpriteShape` 枚举和 `string colorSource` 需要在 Inspector 中可编辑。需确认 Unity 2022.3 LTS 的 Enum 字段序列化无问题 | 程基岩（工程） |
| 法黑弹幕在深色背景上的可见性 | 法家弹幕(#1A1A1A)在中灰背景(#3A3A3A)上对比度较低。白描边(1-2px)是硬约束。需灰阶实测确认可见度 | 灰阶测试 |
| 冲刺带 drawMode=Sliced 兼容性 | `SpriteDrawMode.Sliced` 要求 Sprite 有 border（九宫格）。1×1 白色方形 Sprite 需确认是否支持 Sliced 模式，或改用 `SpriteDrawMode.Tiled` | 程基岩（工程） |
| 精英弟子描边方案 | 双层 SpriteRenderer 方案增加 Draw Call。6 种弟子中 3 种精英同时出现最多 3 个，Draw Call 影响 <3，可接受 | — |
| Boss 六边形白描边 | 法宗师阶段3(#0A0A0A)在灰背景上极暗，需白描边。但六边形描边的双层方案比三角形复杂（六边形不能简单用大尺寸+小尺寸差值）。备选：六边形纹理生成时直接内嵌白描边 | 程基岩（工程） |

---

## 9. 与架构的对齐

| 架构要求 | 本方案对齐方式 |
|---------|--------------|
| ADR-001 数据驱动配置 | 颜色从 schools.json 读取，不硬编码。Sprite 形状/尺寸从 SpriteInitializer 序列化字段配置 |
| ADR-003 弹幕 shape/pattern 分离 | SpriteGenerator 只关心 shape（视觉），BulletEntity 只关心 pattern（行为），互不干扰 |
| 主架构 §4 目录结构 | SpriteGenerator 在 `Scripts/Foundation/`，Prefabs 在 `Prefabs/`，Art 在 `Art/Sprites/` |
| 主架构 §10 性能预算 | 预生成 <600KB，Draw Call <50，均远低于预算 |
| 主架构 §5 命名规范 | Prefab 用 PascalCase（PlayerBase.prefab），Sprite 形状用 PascalCase 枚举（SpriteShape.Circle） |

---

*本方案将 39 项灰模资产中的 35 项视觉资产全部用 runtime 代码生成实现，零手工 Sprite。颜色从 JSON 配置注入，灰阶测试可一键切换。与 ADR-001 数据驱动配置和 ADR-003 弹幕系统数据化完全对齐。*
