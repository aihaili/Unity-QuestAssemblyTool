# 🧩 Quest Assembly Tool — 零件收集任务编辑器

一个轻量级的 Unity 编辑器工具，用于快速创建**收集零件→组装成品**的任务系统。  
地编在场景中放好零件，选中它们点个按钮，任务就建好了。

---

## 📦 快速开始

1. **导入包**：`Assets → Import Package → Custom Package`，选中 `QuestAssemblyTool.unitypackage`
2. **打开工具**：菜单栏 `Tools → Quest Assembly Tool`
3. **放零件**：在场景中放置多个 3D 物体（Cube、Sphere、Cylinder 等，或任何预制体）
4. **选中零件**：在 Hierarchy 中选中它们
5. **输入任务名**：如"组装电台"
6. **点击「从选中物体创建任务」**
7. **运行游戏**：角色走近零件即可收集

---

## 🎮 核心功能

### 零件收集
- 每个零件挂载 `CollectiblePart` 组件（自动添加），带有触发器碰撞体
- 玩家（Tag = Player 或挂有 CharacterController）碰触即拾取
- 同种零件多份摆放时，**只需收集 1 次**即计入任务进度
- 多余零件仍可拾取（消失），但不重复计任务（RPG 友好）

### 多任务管理
- 统一 `QuestManager`（单例），一个管理器管理 N 个任务
- 每个任务独立零件列表、独立进度追踪
- 左上角 UI 实时显示所有任务的进度条

### 任务链（前置任务）
- 可设前置任务：A 完成后 B 的零件才可收集
- 前置未完成时碰触零件 → 显示 `🔒 需要先完成「A」`，零件不消失
- UI 进度列表中显示锁定状态

### 两种管理模式
| 模式 | 说明 |
|------|------|
| 统一管理器 | 所有任务共用一个 `QuestManager`（推荐）|
| 独立管理器 | 每个任务创建独立的 `QuestManager_{任务名}` |

---

## 🛠️ 编辑器工具窗口

菜单 **Tools → Quest Assembly Tool**

### 创建任务
1. 输入 **任务名称**
2. 可选：选择 **前置任务**（已有任务列表自动加载）
3. 在 Hierarchy 中选中零件物体 → 窗口显示**分组预览**（同种零件自动合并）
4. 点击 **「从选中物体创建任务」**

### 管理现有任务
- **刷新任务列表**：同步场景中的零件到 QuestManager
- **定位管理器**：一键选中并 Ping 场景中的 QuestManager

### 保存/加载
- **保存为配置**：将当前任务导出为 `.asset` 文件（ScriptableObject），方便复用
- **从配置生成**：读取 `.asset` 在场景中重新生成所有零件（随机散布）

---

## 📂 项目结构

```
Assets/Scripts/QuestAssembly/
├── CollectiblePart.cs            # 零件组件（挂到零件上）
├── QuestManager.cs               # 任务管理器（单例）
├── QuestUIController.cs          # 屏幕 UI（自动创建 Canvas）
├── QuestAssemblyConfig.cs        # 任务配置（ScriptableObject）
└── Editor/
    └── QuestAssemblyWindow.cs    # 编辑器工具窗口
```

### 组件说明

| 组件 | 挂载到 | 作用 |
|------|--------|------|
| `CollectiblePart` | 每个零件物体 | 触发器检测、通知 QuestManager、拾取反馈 |
| `QuestManager` | 场景空物体 | 管理所有任务进度、前置依赖检查 |
| `QuestUIController` | 场景空物体 | 自动创建 Canvas 显示进度和反馈 |

---

## ⚙️ 依赖

- **Unity 2021.3+**（含 UGUI）
- **Universal Render Pipeline**（材质使用 URP/Lit Shader，非必需但效果最好）
- 无其他第三方包依赖

---

## 🔄 从零创建完整任务的步骤示例

```
1. 场景中放 5 个零件：天线、电路板、喇叭、电池、外壳
2. 选中 5 个零件
3. 打开 Tools → Quest Assembly Tool
4. 输入任务名"组装电台"
5. 点击「从选中物体创建任务」
6. 再放 5 个零件：引擎、轮胎、方向盘、电池组、排气管
7. 选中它们，输入任务名"修理车辆"
8. 前置任务选"组装电台"
9. 点击创建

运行游戏效果：
  - 收集电台 5 个零件 → 任务完成 ✅
  - 解锁"修理车辆"零件收集
  - 第 2 块电池可拾取但不计分 📦
```

---

## 👨‍💻 作者

**乂.海狸**

Reasonix 生成 · 适合独立开发和小团队的轻量任务工具
