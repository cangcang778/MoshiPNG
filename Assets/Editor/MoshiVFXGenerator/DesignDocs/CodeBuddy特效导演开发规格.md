# CodeBuddy特效导演开发规格

## 文档状态

| 项目 | 内容 |
|---|---|
| 状态 | 🚧 Unity端 + Project Skill MVP已落地 |
| 所属工具 | Moshi特效生成器 |
| 模块定位 | AI / CodeBuddy 扩展模块 |
| 菜单路径 | `工具/Moshi特效生成器/CodeBuddy特效导演` |
| 主入口类 | `Moshi_VFXDirector` |
| 执行器类 | `Moshi_UnityBridge` |
| Unity端目录 | `Assets/Editor/MoshiVFXGenerator/CodeBuddyBridge/` |
| 对应方案 | `Assets/Editor/ToolsIntegrationPanel/DesignDocs/CodeBuddy控制Unity特效方案.md` |

## 一、模块定位

`CodeBuddy特效导演` 是 `Moshi特效生成器` 的 AI 扩展模块，用于接收 CodeBuddy / `moshi-vfx-director` Skill 生成的任务命令，并在 Unity Editor 内执行特效创建、修改、预览、结果回传。

它不是普通 `工具/Moshi` 杂项工具，而是 `Moshi特效生成器` 的后台执行与状态面板。

## 二、菜单与命名规格

### 2.1 菜单路径

```csharp
[MenuItem("工具/Moshi特效生成器/CodeBuddy特效导演")]
```

### 2.2 窗口标题

```csharp
private const string TOOL_NAME = "CodeBuddy特效导演";
```

### 2.3 命名规则

| 类型 | 名称 | 说明 |
|---|---|---|
| 主窗口 | `Moshi_VFXDirector` | 面向用户，显示监听状态、队列、日志 |
| 执行器 | `Moshi_UnityBridge` | 文件队列监听与命令执行入口 |
| 数据模型 | `UnityBridgeModels` | 命令、结果、错误、上下文数据结构 |
| 命令调度 | `UnityBridgeCommandRunner` | 状态机、命令分发、异常处理 |
| 特效命令 | `UnityBridgeVFXCommands` | 粒子创建、修改、预览 |
| 安全校验 | `UnityBridgeSafety` | 路径、权限、危险操作校验 |
| 上下文 | `UnityBridgeContext` | 最近任务、最近目标、最近输出 |
| 日志 | `UnityBridgeLogger` | 任务日志与诊断信息 |

## 三、目录结构

```text
Assets/Editor/MoshiVFXGenerator/CodeBuddyBridge/
├── Moshi_VFXDirector.cs
├── Moshi_UnityBridge.cs
├── UnityBridgeModels.cs
├── UnityBridgeCommandRunner.cs
├── UnityBridgeVFXCommands.cs
├── UnityBridgeSafety.cs
├── UnityBridgeContext.cs
├── UnityBridgeLogger.cs
└── Documentation/
```

命令队列目录：

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

生成资源目录：

```text
Assets/MoShi/GeneratedVFX/
├── Prefabs/
├── Materials/
├── Textures/
├── Meshes/
├── Preview/
└── Logs/
```

## 四、主窗口规格：`Moshi_VFXDirector`

### 4.1 职责

- 提供可视化监听开关。
- 显示命令队列统计。
- 显示最近执行任务。
- 提供打开队列目录、重新扫描、清理完成任务等按钮。
- 显示最近日志和错误摘要。
- 调用 `Moshi_UnityBridge` 执行后台逻辑。

### 4.2 UI 草图

```text
CodeBuddy特效导演

[监听状态]  ● 已开启    [开启监听] [停止监听]
[队列统计]  pending: 0 | processing: 0 | done: 12 | failed: 1 | timeout: 0

[最近任务]
cmd_20260527_000001  create_particle_prefab  done
cmd_20260527_000002  modify_selected_particle failed

[操作]
[打开命令目录] [重新扫描] [清理完成任务] [打开生成目录]

[日志]
14:10:01 Bridge started
14:10:05 Execute create_particle_prefab done
```

### 4.3 EditorWindow 模板要求

- 命名空间使用 `MoshiTools`。
- 类名使用 `Moshi_` 前缀。
- UI 文本使用中文。
- 事件注册在 `OnEnable`，注销在 `OnDisable`。
- 样式初始化在 `OnGUI` 延迟执行。

## 五、执行器规格：`Moshi_UnityBridge`

### 5.1 职责

- 创建并维护命令队列目录。
- 按固定间隔扫描 `pending/*.json`。
- 将任务移动到 `processing/`。
- 调用 `UnityBridgeCommandRunner` 执行命令。
- 根据结果移动到 `done/`、`failed/`、`timeout/`。
- Unity 启动时扫描残留 `processing/` 并标记疑似崩溃任务。

### 5.2 扫描策略

| 项目 | 建议值 |
|---|---|
| 默认扫描间隔 | `0.5s` |
| 单任务默认超时 | `30s` |
| 单帧最多处理 | `1` 个任务 |
| 启动时处理旧任务 | 是 |
| 重复 `commandId` 去重 | 是 |

## 六、命令模型规格

### 6.1 基础命令结构

```json
{
  "version": "1.0",
  "commandId": "cmd_20260527_000001",
  "commandType": "create_particle_prefab",
  "targetMode": "none",
  "targetPath": "",
  "outputFolder": "Assets/MoShi/GeneratedVFX/",
  "createdAt": "2026-05-27T14:00:00",
  "payload": {},
  "options": {
    "previewAfterExecute": true,
    "savePrefab": true,
    "backupBeforeModify": true
  }
}
```

### 6.2 MVP 命令类型

| 命令 | 必须实现 | 说明 |
|---|---|---|
| `ping_unity` | 是 | 检查 Bridge 是否在线 |
| `get_selection_info` | 是 | 获取当前 Unity 选中对象 |
| `create_particle_prefab` | 是 | 创建基础粒子 Prefab |
| `modify_selected_particle` | 是 | 修改当前选中粒子 |
| `preview_vfx` | 可选 | 刷新或播放粒子预览 |

## 七、`payload` Schema

### 7.1 `ping_unity`

```json
{
  "payload": {
    "message": "hello_unity"
  }
}
```

### 7.2 `get_selection_info`

```json
{
  "payload": {
    "includeComponents": true,
    "includeAssetPath": true,
    "includeHierarchyPath": true
  }
}
```

### 7.3 `create_particle_prefab`

```json
{
  "payload": {
    "vfxName": "FX_BlueBurst",
    "displayName": "蓝色爆点",
    "vfxType": "burst",
    "style": "tech",
    "duration": 0.6,
    "loop": false,
    "colorPalette": ["#4AA8FF", "#7B3DFF"],
    "scale": 1.0,
    "sortingOrder": 0,
    "layers": [
      {
        "name": "Flash",
        "role": "flash",
        "rendererType": "billboard",
        "particleCount": 24,
        "lifetime": 0.18,
        "startSpeed": 0.2,
        "startSize": 1.2,
        "emissionRate": 0,
        "burstCount": 24,
        "color": "#7FD8FF",
        "materialHint": "additive"
      }
    ]
  }
}
```

### 7.4 `modify_selected_particle`

```json
{
  "payload": {
    "modifyMode": "relative",
    "target": "selected",
    "adjustments": {
      "brightnessMultiplier": 1.25,
      "particleCountMultiplier": 0.8,
      "durationMultiplier": 0.75,
      "sizeMultiplier": 1.1,
      "speedMultiplier": 0.9
    }
  }
}
```

## 八、结果模型规格

### 8.1 成功结果

```json
{
  "version": "1.0",
  "commandId": "cmd_20260527_000001",
  "status": "done",
  "success": true,
  "message": "已生成蓝色爆点特效。",
  "result": {
    "prefabPath": "Assets/MoShi/GeneratedVFX/Prefabs/FX_BlueBurst.prefab",
    "layerCount": 2,
    "materialCount": 1,
    "maxParticles": 72,
    "performanceLevel": "Normal"
  }
}
```

### 8.2 失败结果

```json
{
  "version": "1.0",
  "commandId": "cmd_20260527_000002",
  "status": "failed",
  "success": false,
  "error": {
    "errorCode": "TARGET_NOT_FOUND",
    "errorMessage": "没有找到当前选中的粒子对象。",
    "stackTrace": "",
    "recoverable": true,
    "suggestion": "请先在 Unity 中选中一个粒子对象。"
  }
}
```

## 九、安全规格

### 9.1 路径白名单

默认允许：

```text
Assets/Editor/ToolsIntegrationPanel/AgentCommands/
Assets/MoShi/GeneratedVFX/
Assets/Editor/MoshiVFXGenerator/CodeBuddyBridge/
```

默认禁止：

- 删除用户资源。
- 覆盖正式 Prefab。
- 修改未授权目录。
- 自动播放带业务 JS 回调的 UI 动画。
- 接入 Puerts 执行链路。

### 9.2 高风险对象

如果目标包含以下内容，Bridge 应提示风险并跳过自动播放：

- `JSBehaviour`。
- Puerts 相关组件。
- 高风险 `Animation Event`。
- 原生插件驱动对象。
- 复杂 UI 动画根节点。

## 十、MVP 开发任务拆分

| 编号 | 任务 | 产出 | 验收 |
|---|---|---|---|
| MVP-01 | 创建 `CodeBuddyBridge` 目录 | `Assets/Editor/MoshiVFXGenerator/CodeBuddyBridge/` | Unity 编译通过 |
| MVP-02 | 创建主窗口 | `Moshi_VFXDirector.cs` | 菜单可打开窗口 |
| MVP-03 | 创建队列目录 | `AgentCommands/*` | 首次启动自动创建 |
| MVP-04 | 定义命令/结果模型 | `UnityBridgeModels.cs` | JSON 可读写 |
| MVP-05 | 实现队列扫描 | `UnityBridgeCommandRunner.cs` | 能发现 pending 命令 |
| MVP-06 | 实现状态机 | pending → processing → done/failed | 文件能移动 |
| MVP-07 | 实现 `ping_unity` | 在线检测 | 返回 Bridge 版本 |
| MVP-08 | 实现 `get_selection_info` | 选中对象查询 | 返回对象信息 |
| MVP-09 | 实现 `create_particle_prefab` | 基础粒子生成 | Prefab 存在 |
| MVP-10 | 实现 `modify_selected_particle` | 修改粒子参数 | 参数变化可见 |
| MVP-11 | 实现错误回传 | failed 结果 | 有错误码和建议 |
| MVP-12 | 实现路径安全 | `UnityBridgeSafety.cs` | 非授权路径被拒绝 |
| MVP-13 | 实现崩溃恢复 | 处理旧 processing | 标记疑似崩溃 |
| MVP-14 | 实现日志 | `logs/` | 每个任务有记录 |
| MVP-15 | CodeBuddy 发送脚本 | `send_unity_command.py` | 能写入 pending |
| MVP-16 | CodeBuddy 读取脚本 | `read_unity_result.py` | 能读取结果 |
| MVP-17 | 跑通一句话生成 | 自然语言 → Prefab | 完成闭环 |

## 十一、测试验收标准

### 11.1 基础连接

| 用例 | 输入 | 预期 |
|---|---|---|
| Unity 在线检测 | `ping_unity` | 返回 `success = true` |
| Bridge 未启动 | 写入命令但无监听 | CodeBuddy 超时提示 |
| 重复命令 ID | 两个相同 `commandId` | 只执行一次 |

### 11.2 命令队列

| 用例 | 输入 | 预期 |
|---|---|---|
| 合法命令 | pending 中放入 JSON | 执行后进入 done |
| 非法 JSON | 格式错误 | 进入 failed |
| 未知命令 | `commandType = unknown` | 返回 `INVALID_COMMAND` |
| 残留 processing | 重启 Unity | 标记 `UNITY_CRASH_SUSPECTED` |

### 11.3 资源生成

| 用例 | 输入 | 预期 |
|---|---|---|
| 创建蓝色爆点 | `create_particle_prefab` | 生成 Prefab |
| 单层粒子 | `layers` 1 层 | 生成 1 个 ParticleSystem |
| 多层粒子 | `layers` 2 到 5 层 | 生成层级结构 |
| 非法输出目录 | 未授权路径 | 返回 `PERMISSION_DENIED` |

### 11.4 修改粒子

| 用例 | 输入 | 预期 |
|---|---|---|
| 未选中对象 | `modify_selected_particle` | 返回 `TARGET_NOT_FOUND` |
| 提高亮度 | `brightnessMultiplier = 1.25` | 亮度提高 |
| 降低数量 | `particleCountMultiplier = 0.8` | 粒子数量降低 |
| 缩短时长 | `durationMultiplier = 0.75` | 时长缩短 |

### 11.5 MVP 完成判定

满足以下条件才算完成：

- `ping_unity`、`get_selection_info`、`create_particle_prefab`、`modify_selected_particle` 稳定执行。
- 成功和失败结果都能被 CodeBuddy 读取。
- 连续 10 次创建基础粒子 Prefab 不崩溃。
- Unity 重启后不会重复执行旧任务。
- 非法路径、非法命令、未选中对象都有明确错误码。
- 自动生成资源全部进入 `Assets/MoShi/GeneratedVFX/`。

## 十二、第一版禁止事项

第一版明确不做：

- 不接 Puerts。
- 不自动播放 UI 业务动画。
- 不直接修改正式 Prefab。
- 不做 HTTP / WebSocket。
- 不做复杂蓝图。
- 不做大规模批量生成。
- 不做自动截图质量判断。
- 不做删除资源命令。

## 十三、实施结论

第一版只追求稳定闭环：

```text
CodeBuddy 自然语言
→ moshi-vfx-director Skill
→ JSON 命令文件
→ Moshi_VFXDirector / Moshi_UnityBridge
→ Unity 创建或修改基础粒子 Prefab
→ JSON 结果回传
→ CodeBuddy 用自然语言反馈
```

不要在 MVP 阶段追求复杂特效生成。先把 `ping_unity`、`create_particle_prefab`、结果回传跑通，再逐步扩展。