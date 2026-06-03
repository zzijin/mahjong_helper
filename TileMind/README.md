# TileMind — 日麻 AI 辅助工具

基于 .NET 10 + ONNX Runtime 的实时日麻对局分析工具，支持屏幕捕获、YOLOv8 牌识别、对局状态追踪与 WPF 叠加层显示。

## 架构概览

```
┌─────────────────────────────────────────────────────┐
│                    TileMind.UI                       │
│              WPF 桌面应用 / Overlay 叠加层            │
├─────────────────────────────────────────────────────┤
│                   TileMind.Core                      │
│              DI 注册 / 对局状态追踪 / 动作分类          │
├──────────────┬──────────────────┬───────────────────┤
│ TileMind.AI  │ TileMind.Vision  │ TileMind.Common    │
│  AI 决策     │  屏幕捕获 DXGI    │  共享模型 / 配置     │
│  (占位)      │  YOLOv8 推理     │  日志 / 工具类      │
│              │  多帧融合         │                    │
└──────────────┴──────────────────┴───────────────────┘
```

## 模块说明

| 模块 | 功能 |
|------|------|
| **TileMind.Common** | 共享数据模型（`TileType`, `DetectionResult`, `GameState` 等）、配置选项、日志扩展 |
| **TileMind.Core** | 依赖注入胶水层、**对局状态追踪**（跨帧 tile 匹配、手牌/副露分离、动作分类） |
| **TileMind.Vision** | DXGI 桌面复制 API 屏幕捕获、YOLOv8 ONNX 推理（支持 CUDA FP32/FP16）、多帧融合 |
| **TileMind.AI** | AI 决策模块占位（后续实现牌效分析、防守判断等） |
| **TileMind.UI** | WPF-UI 桌面应用、透明 Overlay 叠加层绘制检测框、导航/设置页面 |
| **TileMind.Console** | 控制台测试入口，用于快速验证 Vision 推理结果 |

## 核心流程

```
屏幕捕获 (DXGI)
    → YOLOv8 推理 (ONNX Runtime, GPU)
    → 多帧融合 (加权投票)
    → 区域路由 (按 ScreenCaptureOptions 派生区域将检测结果分发至各玩家/区域)
    → 对局状态追踪 (帧间 IoU 匹配, 手牌/副露分离, 动作分类)
    → UI 叠加层显示 / 动作日志记录
```

## 支持的牌型

34 种日麻牌（含赤牌）：

- **万子**: 1m–9m, 0m（赤五万）
- **筒子**: 1p–9p, 0p（赤五筒）
- **索子**: 1s–9s, 0s（赤五索）
- **字牌**: 1z–7z（东南西北白发中）

## 对局状态追踪

`GameStateTracker` 通过连续帧比对自动检测以下动作：

| 动作 | 判定条件 |
|------|---------|
| 摸牌 | 手牌 +1，无其他变化 |
| 出牌 | 手牌 -1，牌河 +1 |
| 吃 | 手牌 -2，副露 +1 组（同花色顺子） |
| 碰 | 手牌 -2，副露 +1 组（同牌 3 枚） |
| 明杠 | 手牌 -3，副露 +1 组（4 枚） |
| 暗杠 | 手牌 -4，副露 +1 组（4 枚） |
| 加杠 | 已有碰升级为 4 枚 |

### 追踪算法

1. **帧间 Tile 匹配** — 贪心 IoU 匹配赋予持久化 TrackId
2. **手牌/副露分离** — 空间 gap analysis 从合并 Hand+Meld 区域分离副露组
3. **动作分类** — 两趟模式匹配：逐玩家增量 → 跨玩家关联吃碰杠来源

## 快速开始

### 环境要求

- Windows 10+（DXGI 桌面复制需要）
- .NET 10 SDK
- NVIDIA GPU + CUDA 12+（可选，CPU 回退可用）
- ONNX Runtime GPU 依赖（`Dependency/` 目录下）

### 构建

```bash
dotnet build
```

### 运行控制台测试

```bash
dotnet run --project TileMind.Console
```

默认从 `testdatas/` 读取测试图片，输出标注结果到同目录。

### 运行 WPF 应用

```bash
dotnet run --project TileMind.UI
```

### 配置

所有配置项通过 JSON 文件管理（`settings/` 目录）：

| 文件 | 内容 |
|------|------|
| `yolosettings.json` | 模型路径、置信度/IoU 阈值、GPU 设备 ID、输入尺寸 |
| `screencapturesettings.json` | 适配器/显示器索引、牌桌各区域四边形坐标。8 个玩家分区（手牌+副露区、弃牌区各 4 个）由 4 个基础区域自动计算，无需手动配置 |
| `framefusionsettings.json` | 融合帧数、变化阈值、融合置信度 |
| `gamestatetrackersettings.json` | 追踪 IoU 阈值、miss 容限、手牌/副露分离参数 |

## 项目依赖

- **OpenCvSharp4** — 图像处理和可视化
- **Microsoft.ML.OnnxRuntime** — YOLOv8 推理引擎
- **SharpDX** — 高性能屏幕捕获
- **WPF-UI** — Fluent Design 桌面界面
- **CommunityToolkit.Mvvm** — MVVM 工具包
- **ZLogger** — 结构化日志

## 目录结构

```
TileMind/
├── TileMind.Common/        # 共享层
│   ├── Config/             #   配置选项类
│   ├── Helpers/            #   扩展方法 / 几何计算 / 配置加载
│   ├── Logging/            #   日志配置
│   └── Models/             #   数据模型
├── TileMind.Core/          # 核心层
│   └── Services/           #   DI 注册 / 状态追踪 / 动作分类
├── TileMind.Vision/        # 视觉层
│   ├── Detection/          #   YOLOv8 检测器 / 对象池
│   ├── ScreenCapture/      #   DXGI 捕获 / 帧融合
│   └── Tools/              #   颜色生成等工具
├── TileMind.AI/            # AI 决策 (占位)
├── TileMind.UI/            # WPF 桌面应用
│   ├── Overlay/            #   叠加层绘制系统
│   ├── ViewModels/         #   视图模型
│   ├── Views/              #   页面/窗口
│   └── Services/           #   应用托管
├── TileMind.Console/       # 控制台测试
└── Dependency/             # 原生依赖 (cuDNN 等)
```

## 待完成

- [ ] AI 决策模块（牌效分析、防守判断）
- [ ] InfoArea 解析（结合TextRecognizer项目）
- [ ] 对局记录导出（牌谱格式）
- [ ] 立直检测的视觉判定