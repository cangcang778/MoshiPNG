# Moshi 工具箱框架机制 · ToolsIntegrationPanel

> 本文件描述 Moshi 工具箱的**主面板框架**。如需知道单个工具如何运作，请查 `Tools/Moshi_XXX.md`。

---

## 一、身份信息

- **面板类**：`ToolsIntegrationPanel : EditorWindow`
- **菜单入口**：`Tools > MoshiResourceTools`（`[MenuItem("Tools/MoshiResourceTools", priority = 0)]`）
- **窗口标题**：`"Moshi 工具箱"`
- **代码位置**：`Assets/Editor/ToolsIntegrationPanel/`
- **核心文件（5 个）**：
  - `ToolsIntegrationPanel.cs` — 主面板 UI
  - `ToolItem.cs` — 工具项数据结构
  - `ToolsConfig.cs` — 配置 ScriptableObject
  - `ToolsConfigEditor.cs` — 配置 Inspector
  - `InitToolsConfig.cs` — 启动时自动扫描/同步
  - `MoshiHelpButton.cs` — 帮助按钮组件
- **配置 asset**：`Assets/Editor/ToolsIntegrationPanel/MyToolsConfig.asset`

---

## 二、核心数据结构 `ToolItem`

```
toolName           string   面板显示名（4 字内，支持双击重命名）
toolDescription    string   多行描述（[TextArea]）
category           string   分类标签，用于分组渲染
showInQuickAccess  bool     是否显示在顶部快速入口网格
bindingKey         string   匹配 ToolBindingCatalog.ActionLookup 的唯一键
onExecute          Action   运行期注入的点击委托（[NonSerialized]）
```

---

## 三、工具注册机制（自动扫描 + 增量同步）

### 3.1 不走硬编码清单

框架**不在 `InitToolsConfig.cs` 里手工列工具**。启动流程：

1. `[InitializeOnLoad]` 触发 → `EditorApplication.delayCall += AutoSyncConfig`
2. `ToolBindingCatalog.CreateToolItemsSnapshot()` 反射扫描 `AllToolsResources/` 目录的所有 `.cs`
3. 匹配两类入口：
   - **显式**：带 `[Tool("bindingKey", "toolName", "desc", "category")]` 特性的方法
   - **兜底**：带 `[MenuItem(...)]` 的 `public static void()`
4. 合并到 `MyToolsConfig.asset`：新增 item 追加末尾、失效 `bindingKey` 清理、保留用户自定义的顺序和快速访问勾选

### 3.2 bindingKey 生成规则

- **显式 `[Tool]`**：用 `[Tool(...)]` 第一个参数
- **兜底 `[MenuItem]`**：菜单路径去除非字母数字后拼接，例如 `工具/Moshi/批量重命名` → `工具MoshiBatchRename`
- **category 生成**：菜单路径去掉 `Tools/` 后取中间段，例如 `工具/Moshi/粒子批量修改` → `工具 / Moshi`
- **toolName 生成**：菜单路径末段文本
- **description 生成**：`来自菜单项 "..." 的自动注册工具。`

### 3.3 手动入口

- `CreateOrResetToolsConfig()` — 完全覆盖重置
- `SyncAndPruneToolsConfig()` — 同步 + 剪枝失效项

---

## 四、工具名称优化 `ToolNameOptimizer`

- 手工映射表：`"图片转面片" → "图转面片"`、`"粒子批量修改" → "粒子批改"` 等
- 自动去后缀：`工具 / 助手 / 器 / 导入 / 导出 / 修改 / 清理 / 统计`
- 自动去前缀：`资源 / 文件 / 批量 / 物体 / 粒子`
- 最终截断到 **4 字**
- 目的：让快速入口网格按钮更整齐紧凑

---

## 五、主面板 UI 布局（`OnGUI`）

从上到下五段式：

### 5.1 顶部 Toolbar
按钮：`配置刷新 / 配置初始化 / 重新加载 / 定位配置 / 帮助文档`。

### 5.2 快速入口网格
- 只显示 `showInQuickAccess == true` 的工具
- 列数 = `position.width / 按钮最小宽度`
- 间距/Padding/最大列数从 `ToolsConfig` 读
- 可配置顶部标题栏显隐

### 5.3 "全部工具"折叠区
- 按 `category` 分组
- 每组：分类标题 + 条目数 + 细分割线
- 每项是卡片按钮：▶ 工具名 + 描述 + 绑定状态
- 带 ScrollView

### 5.4 配置缺失 HelpBox
`DrawMissingConfigAlert()` 检测不到 `MyToolsConfig.asset` 时显示创建按钮。

### 5.5 Footer
居中标语文字。

---

## 六、帮助按钮 `MoshiHelpButton`

### 6.1 机制

- 文档根目录：`Assets/Editor/ToolsIntegrationPanel/Documentation/`
- 维护静态字典 `ToolDocMap`：`中文工具名 → Moshi_XXX.md`
- 两个绘制 API：
  - `DrawHelpButton(toolName)` — Toolbar 样式
  - `DrawHelpButtonMini(toolName)` — 普通样式（大部分工具用这个）
- 点击动作：
  1. 查表拿到 `.md` 路径
  2. `AssetDatabase.LoadAssetAtPath` 加载
  3. `EditorGUIUtility.PingObject` + `Selection.activeObject` 在 Project 高亮
  4. 失败则回退到定位 Documentation 文件夹或弹窗提示

### 6.2 接入方式

每个工具 `OnGUI` 里在标题行调用：

```
EditorGUILayout.BeginHorizontal();
GUILayout.Label(TOOL_NAME, EditorStyles.boldLabel);
GUILayout.FlexibleSpace();
MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
EditorGUILayout.EndHorizontal();
```

---

## 七、ToolsConfig 可配置项

| 字段 | 作用 |
|---|---|
| `items` | `List<ToolItem>` 工具清单 |
| `quickAccessSettings` | 快速入口网格的行距/列距/Padding/最大列数/按钮尺寸 |
| `showQuickAccessTitle` | 是否显示"快速入口"标题 |
| `windowMinWidth/MaxWidth/MinHeight` | 主窗口尺寸限制 |

Inspector 扩展（`ToolsConfigEditor`）支持：
- 快速入口尺寸实时预览
- 可用 `BindingKey` 列表（从 `ToolBindingCatalog` 取）
- 工具列表双击重命名（弹出 `RenamePopup`）

---

## 八、常见问题

### Q1：新加工具后面板里看不到？
A：面板启动时会扫描。可手动点 Toolbar 的"配置刷新"，或 `工具/Moshi/` 里跑一下任意工具让 InitializeOnLoad 重触发。

### Q2：工具名字太长，快速入口按钮挤变形？
A：框架会自动调用 `ToolNameOptimizer.GetShortName` 截断到 4 字。若自动优化不理想，可在 `ToolItem.toolName` 上双击重命名，手动设一个更合适的短名。

### Q3：配置文件丢失？
A：点"配置初始化"按钮或 HelpBox 上的一键创建按钮即可。

### Q4：category 都显示为 `工具 / Moshi`，无法细分？
A：这是 `[MenuItem]` 兜底路径的自然结果。若要细分，给方法上加 `[Tool("key","name","desc","具体分类")]` 特性。

### Q5：怎么让工具在快速入口出现？
A：打开 `MyToolsConfig.asset`，在 `items` 列表里勾选对应工具的 `showInQuickAccess`。

---

## 九、扩展新工具的最小步骤

1. 在 `AllToolsResources/Moshi_XXX/` 下新建主类文件 `Moshi_XXX.cs`
2. 使用 `namespace MoshiTools` + 类名 `Moshi_XXX : EditorWindow`
3. 添加 `const string TOOL_NAME = "中文名"`
4. 加 `[MenuItem("工具/Moshi/" + TOOL_NAME)]`
5. `GetWindow<Moshi_XXX>(TOOL_NAME)`
6. 在标题栏调用 `MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME)`
7. 在 `MoshiHelpButton.ToolDocMap` 里加一行映射：`{"中文名", "Moshi_XXX.md"}`
8. 在 `Documentation/` 下写 `Moshi_XXX.md` 使用说明
9. 重编译后自动并入面板
