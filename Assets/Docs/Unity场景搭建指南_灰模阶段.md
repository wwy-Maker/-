# 《诸子百家·口诛笔伐》— Unity 场景搭建指南（灰模验证阶段）

> **核心原则**：不导入任何外部图片资源，所有视觉元素全部使用 Unity 内置组件实现。

---

## 一、推荐的 Scripts 子目录结构

```
Assets/
├── Scripts/
│   ├── Core/               ← 全局核心系统
│   │   ├── GameManager.cs      (已创建)
│   │   └── CameraController.cs (已创建)
│   ├── Player/             ← 玩家相关
│   │   └── PlayerController.cs (已创建)
│   ├── Enemy/              ← 敌人基类 & 具体敌人
│   ├── Combat/             ← 战斗系统（弹幕、伤害、Buff）
│   ├── Flow/               ← 关卡流程（波次生成、房间管理）
│   ├── UI/                 ← UI 面板（血条、技能栏、暂停菜单）
│   ├── Economy/            ← 经济系统（经验、升级、流派选择）
│   └── Render/             ← 渲染相关（Shader、特效）
├── Scenes/                 ← 场景文件（.unity）
├── Prefabs/                ← 预制体（Player、Enemy 模板）
├── Art/
│   ├── Materials/          ← 灰模材质
│   └── Particles/          ← 粒子特效参数（后续阶段）
├── Audio/                  ← 音效（后续阶段）
└── Configs/                ← ScriptableObject 配置表（后续阶段）
```

---

## 二、在 Unity 编辑器中搭建"灰模玩家"

### 第一步：创建玩家 GameObject

1. 在 Hierarchy 窗口，右键 → **Create Empty**
2. 将新建的 GameObject 重命名为 **Player**
3. 设置 Position 为 **(0, 0, 0)**

### 第二步：设为 Prefab（预制体）

1. 将 Hierarchy 中的 Player 拖入 `Assets/Prefabs/` 文件夹
2. 弹出窗口选择 **Original Prefab**
3. 此后所有对 Player 的修改都会自动同步到预制体

### 第三步：添加 SpriteRenderer（白色圆圈）

1. 选中 Player，Inspector 中点击 **Add Component** → 搜索 **SpriteRenderer**
2. 将 SpriteRenderer 的 **Sprite** 字段设为 Unity 内置的圆形：
   - 点击 Sprite 字段右侧的 ◎ 圆点
   - 搜索 `Circle` → 选择 **Knob** 或 **Circle**（Unity 内置 UGUI 精灵）
   - 如果没有内置精灵，也可以选中 SpriteRenderer，在 **Sprite** 下拉中选择 `Built-in Extra/Knob`
3. 将 **Color** 设为纯白色 `(255, 255, 255, 255)`
4. 调整 **Scale** 让圆的大小合适，例如设置 Transform → Scale 为 **(0.5, 0.5, 1)**

### 第四步：添加 Rigidbody2D（物理组件）

1. **Add Component** → 搜索 **Rigidbody2D**
2. 配置如下：
   - **Body Type**: Dynamic
   - **Gravity Scale**: `0`（俯视角，无重力）
   - **Linear Drag**: `8`（有阻尼，松开按键后不会一直滑）
   - **Constraints** → Freeze Rotation: ✅ 勾选 Z（防止碰撞导致旋转）

### 第五步：添加 CircleCollider2D（碰撞体）

1. **Add Component** → 搜索 **CircleCollider2D**
2. Radius 调为 `0.5`（与 SpriteRenderer 的视觉大小匹配）
3. **Is Trigger**: ✅ 勾选（弹幕类游戏通常用 Trigger 判定命中）

### 第六步：挂载 PlayerController.cs 脚本

1. **Add Component** → 搜索 **PlayerController**
2. 参数保持默认即可（moveSpeed = 6, damping = 0.3）

### 第七步：设置 Tag 为 Player

1. 在 Inspector 最顶部，点击 **Tag** 下拉框
2. 选择 **Player**（如果没有，点击 **Add Tag...** 手动创建一个）

---

## 三、搭建 Main Camera（相机跟随）

1. 选中 Hierarchy 中的 **Main Camera**
2. 设置 Position 为 **(0, 0, -10)**（2D 游戏相机 Z 轴通常为 -10）
3. 设置 **Projection** 为 **Orthographic**（正交投影，2D 游戏标准）
4. 设置 **Size** 为 `7`（控制视野范围，值越大看到越多）
5. **Add Component** → 搜索 **CameraController**
6. 将 Hierarchy 中的 Player 拖入 CameraController 脚本的 **Target** 字段

---

## 四、搭建 GameManager 全局管理器

1. Hierarchy 中右键 → **Create Empty**，命名为 **GameManager**
2. **Add Component** → 搜索 **GameManager**
3. Room Size 设为 `(20, 14)`（战斗房间大小）

---

## 五、搭建战斗房间（灰模地板+墙壁）

### 地板

1. Hierarchy 中右键 → **2D Object** → **Sprites** → **Square**
2. 重命名为 **Floor**
3. Scale 设为 **(20, 14, 1)**（与 GameManager 的 roomSize 一致）
4. SpriteRenderer → **Color** 设为深灰色 `(40, 40, 40, 255)`

### 墙壁（四面）

创建4个细长的 Cube/Square 围住地板即可：

| 墙壁 | Position | Scale |
|------|----------|-------|
| 上墙 | (0, 7, 0) | (20, 0.3, 1) |
| 下墙 | (0, -7, 0) | (20, 0.3, 1) |
| 左墙 | (-10, 0, 0) | (0.3, 14, 1) |
| 右墙 | (10, 0, 0) | (0.3, 14, 1) |

每面墙的 SpriteRenderer Color 设为浅灰 `(120, 120, 120, 255)`，并添加 BoxCollider2D。

---

## 六、最终场景 Hierarchy 结构预览

```
SampleScene
├── Main Camera          ← 挂载 CameraController.cs
├── GameManager          ← 挂载 GameManager.cs
├── Floor                ← 深灰方块，做地板
├── Walls (空父节点)
│   ├── Wall_Top
│   ├── Wall_Bottom
│   ├── Wall_Left
│   └── Wall_Right
└── Player               ← Tag=Player, 挂载 PlayerController.cs
    ├── SpriteRenderer   (Circle, 白色)
    ├── Rigidbody2D      (Gravity=0, FreezeZ)
    └── CircleCollider2D (IsTrigger)
```

---

## 七、验证步骤（确认一切正常）

1. 按 **Play** 运行游戏
2. 用 **WASD** 移动 Player — 应该能看到白色圆圈平滑移动并朝向鼠标
3. 镜头应跟随 Player 平滑移动
4. Player 应该在墙壁围成的区域内活动，无法穿墙

如果以上全部正常，灰模阶段的基础架构就搭建完成了。
