# Moshi特效生成器使用说明

## 入口

在 Unity 菜单中打开：

```text
工具/Moshi特效生成器/打开生成器
```

## 推荐流程

1. 在 `素材库` 页配置扫描目录，点击 `扫描素材`。
2. 在 `克隆器` 页选择源 Prefab，配置颜色、贴图和参数变体后执行 `生成预检`。
3. 预检无严重问题后点击 `批量生成`。
4. 在 `生成` 页选择或编辑配方，点击 `一键生成` 创建新特效。
5. 在 `蓝图` 页通过轻量列表或 `特效蓝图` 画布搭建层级，再执行生成。
6. 在 `总览` 页使用内嵌预览查看生成结果。
7. 在 `质检` 页检查 Prefab 的粒子量、Renderer、材质和 Missing 问题。
8. 在 `历史` 页定位输出、打开报告或删除生成结果。

## 内嵌预览操作

- `播放/暂停`：控制粒子模拟。
- `重播`：从头播放当前 Prefab。
- `重置`：恢复相机角度。
- 左键拖拽：旋转视角。
- 右键拖拽：平移视角。
- 鼠标滚轮：缩放。

## 素材库筛选

素材库支持按以下条件筛选：

- 关键词：匹配名称、路径和 Shader 名称。
- 标签：匹配主标签和标签列表。
- 类型：全部、材质、贴图、网格、预制、着色。
- 仅收藏：只显示已点亮星标的素材。
- 批量标签：对当前筛选结果追加标签或设置主标签。

重新扫描素材会保留已有手动标签和收藏状态。

## 预设目录

- `Presets/Recipes`：配方 JSON，主窗口启动和点击 `刷新预设` 时自动加载。
- `Presets/Blueprints`：蓝图 JSON，蓝图窗口保存和加载默认使用该目录。
- `Presets/Configs`：主窗口保存的 `Moshi_VFXGenPreset` 配置资产。

## 配方高级粒子参数

粒子层支持：

- 循环、延迟、形状、半径。
- 生命周期颜色曲线。
- 生命周期大小曲线和倍率。
- Texture Sheet Animation 序列帧格数和帧进度。
- Renderer 排序、材质渲染队列和 SubEmitter 触发类型。

## 质检与自动修复

`质检` 页支持配置预算阈值，并可对选中 Prefab 执行自动修复。当前自动修复范围：

- 按预算压缩 `maxParticles`。
- 限制单个粒子系统粒子上限。
- 修复 0 缩放。
- 限制异常高的 `simulationSpeed`。

## CodeBuddy特效导演

### 入口

```text
工具/Moshi特效生成器/CodeBuddy特效导演
```

### 用途

`CodeBuddy特效导演` 是 Moshi特效生成器的 AI 扩展模块，用于接收 CodeBuddy / `moshi-vfx-director` Skill 写入的任务命令，并在 Unity Editor 内执行基础特效创建、修改、预览和结果回传。

### MVP 验收流程

1. 打开 `工具/Moshi特效生成器/CodeBuddy特效导演`。
2. 点击 `开启监听`。
3. 点击 `写入Ping`，等待队列执行后确认最近任务显示 `ping_unity done`。
4. 点击 `写入蓝爆`，确认生成：

```text
Assets/MoShi/GeneratedVFX/Prefabs/FX_BlueBurst.prefab
```

5. 将生成的 Prefab 拖到 Hierarchy，选中它。
6. 点击 `写入修改`，确认粒子亮度、数量或时长发生变化。
7. 如出现失败，点击 `打开队列` 查看 `failed` 下的结果 JSON。

### CodeBuddy Skill

项目级 Skill 已落地：

```text
Assets/.codebuddy/skills/moshi-vfx-director/
├── SKILL.md
├── scripts/
│   ├── command_builder.py
│   ├── send_unity_command.py
│   └── read_unity_result.py
└── references/
    ├── command_protocol.md
    ├── unity_bridge_workflow.md
    ├── vfx_payload_examples.md
    └── safety_rules.md
```

可在 CodeBuddy 中用自然语言触发该 Skill，例如：

```text
创建一个蓝色爆点特效，命名 FX_TestBlue
当前选中特效更亮一点
查询 Unity 当前选中对象
```

### 队列目录

```text
Assets/Editor/ToolsIntegrationPanel/AgentCommands/
├── pending/
├── processing/
├── done/
├── failed/
├── cancelled/
├── timeout/
└── logs/
```

### 安全限制

- MVP 阶段不直接覆盖正式 Prefab。
- 不删除用户资源。
- 不自动播放带 `JSBehaviour` / Puerts 回调的业务 UI 动画。
- 默认只输出到 `Assets/MoShi/GeneratedVFX/`。

## 安全约定

- 克隆和生成流程默认创建独立材质实例，不修改源材质。
- 材质为空时不自动补材质，除非配方生成明确创建新材质。
- 历史删除优先删除本次生成目录，不影响源资源。
- CodeBuddy特效导演 MVP 阶段默认只处理基础粒子 Prefab，不直接修改正式资源。
