# 诸子百家·口诛笔伐 — Demo 开发工作区

> 本目录存放所有代码、美术资产、配置文件、测试代码和制作过程文档。
> 设计文档（GDD、架构文档、ADR 等）在桌面 `诸子百家_项目文档` 文件夹。

## 引擎与版本

- Unity 2022.3.62f3c1
- 渲染管线：URP
- 目标平台：PC 优先 + 移动端留口

## 目录结构

```
D:/诸子百家_口诛笔伐/
│
├── UnityProject/              ← Unity 工程根目录（用 Unity Hub 打开这里）
│
├── Assets/                    ← 所有游戏资产
│   ├── Scripts/               ← C# 脚本（六模块架构）
│   │   ├── Core/              ← 基础层：配置加载、状态机、事件总线、输入抽象
│   │   ├── Combat/            ← 战斗层：武器、弹幕、闪避、冲刺、碰撞
│   │   ├── Enemy/             ← 敌人层：弟子AI、Boss阶段机、弹幕模式调度
│   │   ├── Economy/           ← 经济层：学识掉落、升级、器物商店
│   │   ├── Flow/              ← 流程层：波次管理、局间操作、死亡/通关
│   │   ├── Render/            ← 渲染层：灰阶Shader、命中闪烁、粒子碎裂
│   │   └── UI/                ← UI层：HUD、菜单、结算面板
│   │
│   ├── Configs/               ← JSON 配置文件（数据驱动，ADR-001）
│   │   ├── weapons.json       ← 武器参数（射艺/御艺/礼击）
│   │   ├── enemies.json       ← 敌人参数（弟子/Boss）
│   │   ├── bullets.json       ← 弹幕配置（ADR-003 弹幕DSL）
│   │   ├── waves.json         ← 波次配置
│   │   ├── items.json         ← 器物配置
│   │   ├── upgrades.json      ← 升级路径配置
│   │   └── input.json         ← 输入映射配置（ADR-002）
│   │
│   ├── Art/                   ← 美术资产
│   │   ├── Sprites/           ← 灰模几何体 Sprite（代码生成或手工）
│   │   ├── Particles/         ← 粒子特效（命中碎裂等）
│   │   └── Materials/         ← 材质（灰阶Shader等）
│   │
│   ├── Audio/                 ← 占位音效（CC0来源）
│   │   ├── shoot.wav          ← 射击音效
│   │   ├── hit.wav            ← 命中音效
│   │   ├── dodge.wav          ← 闪避音效
│   │   └── boss_phase.wav     ← Boss阶段转换音效
│   │
│   ├── Prefabs/               ← 预制体（玩家、弟子、Boss、弹幕等）
│   ├── Scenes/                ← 场景文件（主场景、测试场景）
│   └── Shaders/               ← 自定义Shader（灰阶后处理等）
│
├── Tests/                     ← 测试代码与测试结果
│   └── results/               ← 测试证据（按Story ID归档）
│
└── Production/                ← 制作过程文档
    ├── epics/                 ← Epic/Story 拆分（Phase 4产出）
    └── sprints/               ← 冲刺计划（Phase 4产出）
```

## 四根地基钢筋（对应4条ADR）

| ADR | 钢筋 | Configs对应文件 |
|-----|------|----------------|
| ADR-001 | 数据驱动配置 | 所有 .json 文件 |
| ADR-002 | 输入映射抽象层 | input.json |
| ADR-003 | 弹幕系统数据化 | bullets.json |
| ADR-004 | 游戏状态机 | Core/GameStateMachine.cs |

## 开发批次（来自系统依赖排序图）

- **批次0**：引擎骨架（Unity工程初始化、URP配置、基础框架）
- **批次1**：P0核心（12个P0系统——最小可验证单元）
- **批次2**：P1完整体验（10个P1系统——Boss战完整）
- **批次3**：P2增强（3个P2系统——锦上添花）

## 灰模标准：半灰

- 视觉：几何体 + 命中闪烁（白→红50ms）+ 死亡碎裂（粒子3-5片）
- 听觉：射击/命中/闪避/Boss阶段转换（各1个占位音效）
- 操作：WASD + 左键 + 右键 + Shift
- 最小验证单元：儒家 + 射艺 + 教学波 + 儒宗师阶段1-2

---

> 架构详情见桌面 `诸子百家_项目文档/docs/architecture/` 目录。
> GDD 见桌面 `诸子百家_项目文档/design/gdd/` 目录。
