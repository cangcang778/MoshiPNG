# Moshi特效生成器设计方案

## 文档状态

- **状态**：✅ 已完成
- **工具名称**：Moshi特效生成器
- **定位**：独立于 Moshi工具箱 的特效自动制作平台
- **层级关系**：与 `ToolsIntegrationPanel`（Moshi工具箱）平级，不再作为其子工具
- **主目录**：`Assets/Editor/MoshiVFXGenerator/`
- **菜单根路径**：`工具/Moshi特效生成器/`
- **主窗口建议类名**：`Moshi_VFXGen`
- **主窗口标题**：`Moshi特效生成器`

---

## 一、核心调整说明

原先方案把自动制作特效工具放在 Moshi工具箱体系下：

```text
Assets/Editor/ToolsIntegrationPanel/AllToolsResources/
└── Moshi_VFXCloner / Moshi_VFXFactory / Moshi_VFXBlueprint
```

现在调整为独立产品线：

```text
Assets/Editor/
├── ToolsIntegrationPanel/      # Moshi工具箱
└── MoshiVFXGenerator/          # Moshi特效生成器
```

也就是说：

- 不放入 `ToolsIntegrationPanel/AllToolsResources/`。
- 不注册到 `MyToolsConfig.asset`。
- 不作为 Moshi工具箱的一个按钮或子标签。
- 不使用 `工具/Moshi/xxx` 菜单路径。
- 拥有独立窗口、独立配置、独立文档、独立预设、独立生成历史。
- 可以复用 Moshi工具箱中已有工具的思想，但代码层不强依赖 Moshi工具箱。

---

## 二、产品定位

`Moshi特效生成器` 是一个专门用于“自动制作 / 一键生成 / 批量变体”特效的独立编辑器工具。

它和 Moshi工具箱的区别：

| 项目 | Moshi工具箱 | Moshi特效生成器 |
|---|---|---|
| 定位 | 多种小工具集合 | 特效生产专用平台 |
| 入口 | `工具/Moshi/` | `工具/Moshi特效生成器/` |
| 目录 | `Assets/Editor/ToolsIntegrationPanel/` | `Assets/Editor/MoshiVFXGenerator/` |
| 工具组织 | 多个独立工具 | 一个完整生产流程 |
| 数据 | 分散在各工具中 | 独立素材库、配方库、历史记录 |
| 目标 | 辅助编辑 | 批量生产特效 Prefab |

一句话：

> Moshi工具箱是“工具集合”，Moshi特效生成器是“特效生产线”。

---

## 三、总体目标

`Moshi特效生成器` 的目标是建立一套完整的特效自动生产流程：

1. 自动扫描项目内特效素材。
2. 从已有优秀特效克隆变体。
3. 根据配方一键生成标准特效。
4. 支持颜色、材质、贴图、粒子参数批量变化。
5. 自动创建规范资源目录。
6. 自动生成材质实例，避免污染源资源。
7. 自动执行质量检查。
8. 生成后直接预览、定位和管理。
9. 沉淀项目内可复用的配方库、素材库、生成历史。

---

## 四、独立目录结构

建议完整目录如下：

```text
Assets/Editor/MoshiVFXGenerator/
├── MoshiVFXGenerator.Editor.asmdef
├── Core/
│   ├── Moshi_VFXGenModels.cs
│   ├── Moshi_VFXAssetDB.cs
│   ├── Moshi_VFXShaderTags.cs
│   ├── Moshi_VFXTextureTags.cs
│   ├── Moshi_VFXPrefabUtil.cs
│   ├── Moshi_VFXQualityCheck.cs
│   └── Moshi_VFXHistory.cs
├── Window/
│   ├── Moshi_VFXGen.cs
│   ├── Moshi_VFXGenStyles.cs
│   ├── Moshi_VFXGenTabs.cs
│   └── Moshi_VFXGenPreview.cs
├── Modules/
│   ├── Cloner/
│   │   ├── Moshi_VFXCloner.cs
│   │   ├── VFXPrefabScanner.cs
│   │   ├── VFXVariantEngine.cs
│   │   ├── VFXColorRemapper.cs
│   │   └── VFXMaterialSwapper.cs
│   ├── Factory/
│   │   ├── Moshi_VFXFactory.cs
│   │   ├── VFXRecipeDB.cs
│   │   ├── VFXAssembler.cs
│   │   └── VFXPresetLibrary.cs
│   └── Blueprint/
│       ├── Moshi_VFXBlueprint.cs
│       ├── VFXBlueprintModels.cs
│       ├── VFXNodeRenderer.cs
│       ├── VFXBlueprintGenerator.cs
│       └── VFXBlueprintSerializer.cs
├── Presets/
│   ├── Recipes/
│   ├── Palettes/
│   └── Blueprints/
├── Settings/
│   ├── MoshiVFXGeneratorSettings.asset
│   └── MoshiVFXAssetDatabase.asset
├── Documentation/
│   └── Moshi特效生成器使用说明.md
└── DesignDocs/
    ├── README.md
    └── Moshi特效生成器设计方案.md
```

### 目录职责

| 目录 | 说明 |
|---|---|
| `Core/` | 共享核心能力，不直接画 UI |
| `Window/` | 主窗口、标签页、预览、样式 |
| `Modules/Cloner/` | 特效克隆与批量变体 |
| `Modules/Factory/` | 配方驱动的一键生成 |
| `Modules/Blueprint/` | 蓝图/图纸驱动生成 |
| `Presets/` | 内置配方、色板、蓝图模板 |
| `Settings/` | 独立配置和素材索引缓存 |
| `Documentation/` | 使用说明 |
| `DesignDocs/` | 设计文档 |

---

## 五、菜单与入口设计

### 5.1 菜单路径

独立工具不使用 `工具/Moshi/`，而是使用新的根路径：

```csharp
[MenuItem("工具/Moshi特效生成器/打开生成器")]
public static void ShowWindow()
{
    GetWindow<Moshi_VFXGen>("Moshi特效生成器");
}
```

可扩展菜单：

```text
工具/Moshi特效生成器/打开生成器
工具/Moshi特效生成器/刷新素材库
工具/Moshi特效生成器/打开输出目录
工具/Moshi特效生成器/清理生成缓存
```

### 5.2 主窗口定位

主窗口不是简单工具窗口，而是完整平台：

```text
[Moshi特效生成器]
├── 素材库
├── 克隆器
├── 配方生成
├── 蓝图
├── 质检
└── 历史
```

---

## 六、命名规范调整

为了和 Moshi工具箱区分，采用“独立产品线 + Moshi 前缀”的方式。

### 6.1 命名空间

建议使用独立命名空间：

```csharp
namespace MoshiVFXGenerator
{
}
```

原因：

- 避免和 `MoshiTools` 工具箱命名空间混在一起。
- 后续可以独立迁移、导出、禁用或打包。
- 体现它是和 Moshi工具箱平级的独立工具。

### 6.2 类名

主类仍保留 Moshi 前缀，便于项目识别：

| 类型 | 类名建议 | 说明 |
|---|---|---|
| 主窗口 | `Moshi_VFXGen` | Moshi特效生成器主窗口 |
| 素材库 | `Moshi_VFXAssetDB` | 素材扫描和索引 |
| 预览器 | `Moshi_VFXPreview` | 独立预览模块 |
| 克隆器 | `Moshi_VFXCloner` | 批量克隆变体 |
| 工厂 | `Moshi_VFXFactory` | 配方一键生成 |
| 蓝图 | `Moshi_VFXBlueprint` | 蓝图编辑和生成 |

---

## 七、整体功能架构

```text
Moshi特效生成器
├── 素材库系统
│   ├── Shader 扫描
│   ├── 材质扫描
│   ├── 贴图扫描
│   ├── Mesh 扫描
│   └── 标签分类
├── 克隆器系统
│   ├── 源 Prefab 扫描
│   ├── 批量复制
│   ├── 材质实例化
│   ├── 颜色重映射
│   ├── 贴图替换
│   └── 粒子参数扰动
├── 配方生成系统
│   ├── 配方库
│   ├── 内置预设
│   ├── 层级组装
│   ├── 材质匹配
│   └── Prefab 保存
├── 蓝图系统
│   ├── 节点画布
│   ├── 蓝图序列化
│   ├── 蓝图转配方
│   └── 蓝图生成 Prefab
├── 质检系统
│   ├── 粒子量检查
│   ├── 材质数量检查
│   ├── Missing 检查
│   ├── 性能评级
│   └── 自动修正建议
├── 预览系统
│   ├── 独立 PreviewRenderUtility
│   ├── 粒子模拟
│   ├── 相机控制
│   └── 生成结果预览
└── 历史系统
    ├── 生成记录
    ├── 配置复用
    ├── 输出定位
    └── 失败记录
```

---

## 八、与 Moshi工具箱的关系

### 8.1 不再作为工具箱子工具

`Moshi特效生成器` 不纳入：

- `ToolsIntegrationPanel/AllToolsResources/`
- `ToolsIntegrationPanel/MyToolsConfig.asset`
- `ToolsIntegrationPanel/Documentation/`
- `工具/Moshi/` 菜单

### 8.2 已迁移的相关工具

以下特效相关工具已从 Moshi工具箱迁移到 `Assets/Editor/MoshiVFXGenerator/Modules/`：

| 已迁移工具 | 新位置 | 新菜单 | 说明 |
|---|---|---|---|
| 特效分析器 | `Modules/Moshi_VFXAnalyzer/` | `工具/Moshi特效生成器/特效分析器` | Prefab 分析、性能评分、规则发现 |
| 特效浏览器 | `Modules/Moshi_VfxBrowser/` | `工具/Moshi特效生成器/特效浏览器` | 实时预览、搜索、分页、分组 |

主入口窗口：

```text
Assets/Editor/MoshiVFXGenerator/Window/Moshi_VFXGen.cs
菜单：工具/Moshi特效生成器/打开生成器
```

### 8.3 仍保留在 Moshi工具箱中的参考工具

以下工具暂不迁移，仅作为实现参考或后续可选桥接：

| Moshi工具箱工具 | 可参考内容 | 是否强依赖 |
|---|---|---|
| `Moshi_ParticleBatch` | 粒子参数批量修改逻辑 | 否 |
| `Moshi_ParticleMesh` | Mesh 查看/替换思路 | 否 |
| `Moshi_TextureTool` | 贴图处理思路 | 否 |

### 8.4 可选桥接层

为了避免强依赖，后续如果需要互通，采用桥接层：

```text
MoshiVFXGenerator/Core/Bridge/
├── ParticleBatchBridge.cs       # 可选复用粒子批量修改思路
└── TextureToolBridge.cs         # 可选复用贴图处理思路
```

桥接原则：

- 只读导入，不直接引用工具箱内部类。
- 通过 JSON、路径、AssetDatabase 交互。
- 即使 Moshi工具箱不存在，Moshi特效生成器也能独立运行。

---

## 九、主窗口标签页设计

### 9.1 标签页总览

| 标签 | 用途 | 阶段 |
|---|---|---|
| 素材库 | 扫描和管理 Shader / 材质 / 贴图 / Mesh | Phase 1 |
| 克隆器 | 从已有 Prefab 批量生成变体 | Phase 1 |
| 配方 | 选择或编辑生成配方 | Phase 2 |
| 生成 | 根据配方一键生成 Prefab | Phase 2 |
| 蓝图 | 可视化节点设计 | Phase 3 |
| 质检 | 性能评分、Missing 检查、优化建议 | Phase 1+ |
| 历史 | 生成记录、定位、复用配置 | Phase 1+ |

### 9.2 UI 规范

- 标签页按钮使用中文，尽量 2～4 字。
- 选择模式使用并排按钮组。
- 重要操作按钮使用四字中文，如 `扫描素材`、`批量生成`、`保存配方`。
- 不使用 `EnumPopup` 显示核心模式选择。
- 所有生成类操作必须显示输出目录和预估结果。
- 批量生成前显示确认摘要。

---

## 十、Phase 1：素材库 + 克隆器 MVP

### 10.1 目标

第一阶段目标是让工具快速可用：

```text
扫描素材 → 选择源特效 → 配置颜色/参数变体 → 批量生成 Prefab → 预览/质检
```

### 10.2 功能范围

#### 素材库

- 扫描指定目录。
- 识别项目内材质、贴图、Mesh、Shader。
- 对素材打标签。
- 缓存扫描结果到独立 `Settings/`。

#### 克隆器

- 选择源 Prefab。
- 扫描源 Prefab 层级。
- 批量复制 Prefab。
- 自动复制材质实例。
- 色相偏移改色。
- 粒子参数倍率调整。
- 保存新 Prefab。
- 记录生成历史。

#### 质检

- 粒子量统计。
- 材质数统计。
- Shader 数统计。
- Missing Material / Missing Mesh 检查。
- 生成后输出结果摘要。

### 10.3 克隆器文件结构

```text
Modules/Cloner/
├── Moshi_VFXCloner.cs
├── VFXClonerModels.cs
├── VFXPrefabScanner.cs
├── VFXVariantEngine.cs
├── VFXColorRemapper.cs
└── VFXMaterialSwapper.cs
```

### 10.4 克隆流程

1. 用户拖入源 Prefab。
2. 点击 `扫描结构`。
3. 工具记录层级、粒子系统、Renderer、材质、贴图。
4. 用户选择变体数量、输出目录、命名规则。
5. 用户选择颜色模式：`色相` / `调色` / `随机`。
6. 用户选择参数模式：`不调` / `倍率` / `随机`。
7. 点击 `预览列表` 生成待输出清单。
8. 点击 `批量生成`。
9. 工具复制源 Prefab。
10. 工具复制并替换材质实例。
11. 工具重映射颜色。
12. 工具调整粒子参数。
13. 工具保存 Prefab。
14. 工具执行质检。
15. 工具写入历史记录。

### 10.5 克隆器验收标准

- 能生成至少 3 个不同颜色变体。
- 源 Prefab 不被修改。
- 源材质不被修改。
- 每个变体拥有独立材质实例。
- 能定位生成结果。
- 能显示生成成功/失败原因。

---

## 十一、Phase 2：配方一键生成

### 11.1 目标

第二阶段实现真正“一键生成特效”：

```text
选择配方 → 选择素材风格 → 输入名称 → 自动组装 Prefab
```

### 11.2 配方系统

配方使用 JSON 或 ScriptableObject 双模式：

- JSON：适合版本管理和团队共享。
- ScriptableObject：适合在 Unity Inspector 中配置。

配方核心字段：

| 字段 | 说明 |
|---|---|
| `recipeName` | 配方名称 |
| `category` | 特效分类 |
| `description` | 描述 |
| `layers` | 层级列表 |
| `budget` | 性能预算 |
| `styleTags` | 风格标签 |

### 11.3 内置配方

第一批建议配方：

| 配方 | 结构 |
|---|---|
| 基础闪光 | 核心亮点 + 光晕 + 星点 |
| 圆环扩散 | 环形 Mesh + 扩散粒子 |
| 小爆炸 | 爆点 + 火花 + 烟雾 |
| 奖励弹出 | 背光 + 金币/星光 + 闪烁 |
| 按钮点击 | 缩放反馈 + 小粒子 + 光圈 |

### 11.4 生成流程

1. 选择配方。
2. 输入特效名称。
3. 选择输出目录。
4. 选择颜色风格。
5. 素材库自动匹配材质、贴图、Mesh。
6. 创建根 GameObject。
7. 按配方创建子层级。
8. 添加 ParticleSystem / MeshRenderer / TrailRenderer。
9. 创建材质实例。
10. 应用参数。
11. 保存 Prefab。
12. 执行质检。
13. 写入生成报告。

### 11.5 验收标准

- 至少内置 3 个可用配方。
- 能从零生成 Prefab。
- 能自动创建目录结构。
- 能自动创建材质实例。
- 能生成质量报告。
- 能在生成后立即预览。

---

## 十二、Phase 3：蓝图/图纸驱动生成

### 12.1 目标

第三阶段让设计师能用节点画布搭建复杂特效。

### 12.2 节点类型

| 节点 | 说明 |
|---|---|
| Root | 根节点 |
| ParticleLayer | 粒子层 |
| MeshLayer | Mesh 层 |
| TrailLayer | 拖尾层 |
| SubEmitter | 子发射器 |
| MaterialSlot | 材质槽 |

### 12.3 画布交互

- 右键添加节点。
- 左键拖拽节点。
- Alt + 左键连线。
- Delete 删除节点。
- Ctrl + D 复制节点。
- 中键平移画布。
- 滚轮缩放。
- 双击展开参数。

### 12.4 蓝图流程

```text
创建蓝图 → 添加节点 → 连接层级 → 配置参数 → 保存蓝图 → 生成 Prefab
```

### 12.5 验收标准

- 能创建和保存蓝图。
- 能导出/导入 JSON。
- 能从蓝图生成 Prefab。
- 能复用素材库和配方生成器。

---

## 十三、独立预览系统

因为 `Moshi特效生成器` 不依赖 `Moshi_VfxBrowser`，需要内置轻量预览系统。

### 13.1 预览能力

- 使用 `PreviewRenderUtility`。
- 支持 ParticleSystem 模拟。
- 支持 MeshRenderer 预览。
- 支持正交/透视切换。
- 支持播放/暂停/重置。
- 支持旋转视角。
- 支持生成结果快速预览。

### 13.2 与浏览器关系

`Moshi_VfxBrowser` 仍可作为可选外部查看器，但不是必要依赖。

---

## 十四、独立数据与配置

### 14.1 配置路径

```text
Assets/Editor/MoshiVFXGenerator/Settings/
├── MoshiVFXGeneratorSettings.asset
├── MoshiVFXAssetDatabase.asset
└── MoshiVFXHistory.asset
```

### 14.2 用户数据路径

```text
UserSettings/MoshiVFXGenerator/
├── History.json
├── LastSession.json
├── AssetScanCache.json
└── Reports/
```

### 14.3 输出资源路径

默认输出：

```text
Assets/MoShi/GeneratedVFX/
```

单个特效结构：

```text
Assets/MoShi/GeneratedVFX/[特效名称]/
├── Prefab/
├── Material/
├── Texture/
├── Mesh/
└── Report/
```

---

## 十五、质量检查规则

| 检查项 | 建议阈值 | 级别 |
|---|---:|---|
| 总最大粒子数 | > 3000 | 警告 |
| 总最大粒子数 | > 8000 | 严重 |
| 粒子系统数量 | > 12 | 警告 |
| 材质数量 | > 6 | 警告 |
| Shader 数量 | > 4 | 警告 |
| 循环粒子数量 | > 5 | 警告 |
| Missing Material | 任意 | 严重 |
| Missing Mesh | 任意 | 严重 |
| scale 为 0 | 任意 | 严重 |
| 资源引用丢失 | 任意 | 严重 |

质检结果显示在 `质检` 标签页，并写入生成报告。

---

## 十六、问题预判与解决方案

本节用于在正式开发前提前识别风险，避免做到一半才发现架构、资源、性能或工作流问题。

### 16.1 总体风险分级

| 级别 | 含义 | 处理原则 |
|---|---|---|
| P0 | 会导致资源损坏、源 Prefab 被污染、GUID 丢失 | 必须在开发前设计防线，不允许带病上线 |
| P1 | 会导致生成失败、结果不可用、工具卡死 | MVP 阶段必须解决 |
| P2 | 会导致体验差、生成效果不稳定、维护困难 | 可分阶段优化，但要预留接口 |
| P3 | 锦上添花类问题 | 放到后续迭代 |

### 16.2 独立工具迁移风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| 工具迁移后仍残留在 Moshi工具箱菜单 | 用户入口混乱，工具箱自动注册重复 | 所有特效相关菜单统一使用 `工具/Moshi特效生成器/`；从 `MyToolsConfig.asset` 和工具箱索引中移除旧入口 |
| 命名空间仍使用 `MoshiTools` | 与 Moshi工具箱代码混淆，后续打包困难 | 独立工具统一使用 `MoshiVFXGenerator`、`MoshiVFXGenerator.Analyzer`、`MoshiVFXGenerator.VfxBrowser` |
| 迁移文件导致 `.meta` 丢失 | ScriptableObject / asmdef / 配置引用断裂 | 移动目录时必须同时移动 `.meta`，禁止删除重建 |
| 旧文档还指向 `AllToolsResources` | AI 或开发者继续按旧路径开发 | 旧文档只保留迁移提示，新文档以 `MoshiVFXGenerator/DesignDocs/` 为准 |
| asmdef 改名后引用丢失 | 编译失败或命名空间无法访问 | asmdef 文件名、`name`、`rootNamespace` 同步改为 `MoshiVFXGenerator.*` |

### 16.3 资源安全风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| 克隆时直接修改源材质 | 源特效被污染，所有引用该材质的 Prefab 变色 | 生成前建立 `sourceMaterial -> clonedMaterial` 映射，所有写入只写 clonedMaterial |
| 直接修改源 Prefab 实例 | 源 Prefab 被覆盖，难以恢复 | 永远先 `CopyAsset` 到输出目录，再加载复制体处理 |
| 输出路径覆盖已有资源 | 用户已有资源被替换 | 生成前预检同名路径；默认自动追加 `_01/_02`；覆盖必须二次确认 |
| 生成失败留下半成品 | Project 目录污染 | 每次生成建立事务列表，失败时删除本次创建的 Prefab / Material / Report |
| 移动或复制贴图导致 GUID 变化 | 原引用断裂 | 默认不复制贴图，只引用源贴图；需要复制时连同 `.meta` 或用 `AssetDatabase.CopyAsset` |
| 生成材质没有保存成资产 | 重启 Unity 后材质丢失 | 所有材质实例必须 `AssetDatabase.CreateAsset` 到 `Material/` 目录 |

### 16.4 Prefab 处理风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| Prefab 是 Variant | 直接复制后修改可能影响继承关系判断 | MVP 先把复制体当普通 Prefab 处理；后续增加“保留 Variant / 断开 Variant”选项 |
| Prefab 内有嵌套 Prefab | 修改子对象可能变成 override，结构复杂 | 使用 `PrefabUtility.LoadPrefabContents` 处理复制体；保存前记录 override 摘要 |
| Prefab 内有 Missing Script | 生成后不可用 | 预检阶段标记 P1 警告，允许继续但报告中列出路径 |
| Prefab 内引用外部材质数组 | 多 Renderer 共用材质，替换不完整 | 按 `Renderer.sharedMaterials` 全数组复制和替换，不只处理 `sharedMaterial` |
| SubEmitter 引用子粒子系统 | 复制/改名后引用可能失效 | 克隆时不销毁层级；调整参数不重建对象；后续专门扫描 SubEmitters 关系 |
| TrailRenderer / LineRenderer 没有被处理 | 克隆颜色不完整 | 颜色重映射统一处理 Renderer、ParticleSystem、TrailRenderer、LineRenderer |

### 16.5 材质与 Shader 风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| 不同 Shader 颜色属性名不同 | 改色无效 | 建立属性优先级：`_Color`、`_BaseColor`、`_TintColor`、`_MainColor`、`_EmissionColor` |
| Shader 使用 HDR 颜色 | 普通 RGB 改色导致亮度丢失 | HSV 改色时保留 V 值和 alpha；HDR 颜色保留强度倍数 |
| 材质使用多个贴图槽 | 只替换 `_MainTex` 不完整 | 识别 `_MainTex`、`_BaseMap`、`_MaskTex`、`_NoiseTex`、`_DissolveTex` 等常见槽 |
| 材质启用关键字 | 复制后关键字/渲染队列丢失 | 使用 `Object.Instantiate(material)` 复制，保留 keyword、renderQueue、enableInstancing |
| 使用 URP / Built-in 不同 Shader | 预览或生成显示异常 | 素材库扫描时记录 Shader 名称和管线标签，生成时不跨管线强行替换 |
| 材质为 None | 自动补材质可能改变用户意图 | 保持 None，不自动安排材质；只在配方生成明确需要材质时创建 |

### 16.6 粒子参数风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| 直接改 `main.startSize` 丢失曲线模式 | 曲线/随机双常量被破坏 | 使用 `ParticleSystem.MinMaxCurve` 原模式按倍率缩放，不强转常量 |
| 直接改 `startColor` 丢失渐变模式 | 渐变色失真 | 保留 `MinMaxGradient` 模式，对 Color / Gradient / TwoColors 分别处理 |
| emission rate 可能是 curve | 倍率处理不完整 | 对 `rateOverTime`、`rateOverDistance` 的 curveMultiplier 和 constant 分别处理 |
| maxParticles 被调太低 | 特效看不见 | 设置最小阈值，例如不低于 8 或原值的 10% |
| startLifetime 被调为 0 | 粒子瞬间消失 | 所有时间类参数设置最小值，例如 `0.03f` |
| simulationSpeed 过高 | 预览和运行异常 | 限制范围，例如 `0.1f ~ 5f`，超过写入警告 |
| 随机参数不可复现 | 同配置无法再生成同结果 | 所有随机使用显式 seed，历史记录保存 seed |

### 16.7 颜色重映射风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| 白色/灰色被改色后变脏 | 高光、烟雾、遮罩不自然 | 对低饱和度颜色设置跳过阈值，例如 `S < 0.08` 不偏移 H |
| 黑色透明边缘被改色 | 贴图边缘脏色 | 颜色 alpha 低于阈值时不处理 |
| 渐变多个 key 改色后层次丢失 | 视觉单调 | 只偏移 Hue，保留 Saturation / Value / Alpha 的相对关系 |
| 材质颜色和粒子颜色重复叠加 | 结果过饱和 | 生成报告标记“双重改色”；必要时提供“只改粒子 / 只改材质 / 两者都改”按钮组 |
| 随机调色不符合项目风格 | 生成结果不可控 | 支持项目色板 `Palettes/`，优先使用色板而不是纯随机 |

### 16.8 素材库扫描风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| 全项目扫描太慢 | 编辑器卡死 | 默认只扫描 `Assets/Effect/`、`Assets/MoShi/` 和用户配置目录；提供手动全项目扫描 |
| AssetDatabase 查询频繁 | UI 卡顿 | 扫描结果缓存到 `MoshiVFXAssetDatabase.asset` 或 `UserSettings` JSON |
| 素材标签误判 | 自动匹配错误 | 标签分为“自动标签”和“人工标签”，人工标签优先 |
| 贴图类型难以判断 | 噪声/遮罩/渐变混淆 | 先按文件名关键词分类，再允许用户手动修正 |
| 素材被删除后缓存过期 | 生成引用 Missing | 每次生成前验证 GUID/path 是否仍有效，无效则提示刷新素材库 |
| 同名素材太多 | 选择困难 | 素材库 UI 支持路径、类型、标签、最近使用过滤 |

### 16.9 预览系统风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| `PreviewRenderUtility` 未释放 | 内存/GPU 资源泄漏 | 在 `OnDisable`、窗口关闭、切换 Prefab 时统一 Cleanup |
| 同时预览太多 Prefab | GPU 占用过高 | 主生成器内置预览只显示当前选中项；批量浏览仍使用独立浏览器模块 |
| 粒子模拟和真实场景不一致 | 预览误判 | 明确预览只做编辑器近似；报告中保留真实 Prefab 输出路径供场景测试 |
| UGUI 特效相机距离异常 | 预览看不到 | 检测 Canvas / RectTransform，自动切 WorldSpace 并使用正交相机 |
| URP 下深度/透明异常 | 预览结果和 GameView 不一致 | 复用浏览器里已有 URP 处理逻辑，并提供背景色/相机距离调节 |

### 16.10 历史记录与配置风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| 历史 JSON 损坏 | 工具打不开或历史丢失 | 加载失败时备份坏文件为 `.broken`，自动创建新历史 |
| 历史记录过多 | UI 卡顿 | 默认保留最近 200 条，旧记录归档或清理 |
| 配置和代码版本不兼容 | 字段变更导致反序列化异常 | 配置增加 `version` 字段，加载时做迁移 |
| 用户切项目后路径失效 | 历史定位失败 | 历史记录保存 GUID + path，优先 GUID 恢复，失败再用 path |
| 随机生成无法复现 | 难以回滚问题 | 历史保存完整配置快照和 random seed |

### 16.11 一键生成风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| 配方过于抽象 | 生成效果不可控 | 第一阶段只做固定模板，不做自由文本生成 |
| 自动匹配素材不合适 | 生成效果差 | 生成前显示素材匹配清单，允许手动替换 |
| 生成层级过复杂 | 后续维护困难 | 内置配方限制层数，超出预算给警告 |
| 输出效果没有可调性 | 用户无法微调 | 每个配方暴露少量核心参数：颜色、大小、持续时间、强度 |
| 生成目录太深 | 使用不方便 | 保持 `Prefab/Material/Texture/Mesh/Report` 五类目录，禁止无限嵌套 |

### 16.12 蓝图系统风险

| 可能问题 | 风险 | 解决方案 |
|---|---|---|
| 节点编辑器开发周期过长 | 拖慢核心功能 | 蓝图放到 Phase 3，先不影响克隆器和配方生成 |
| 节点自由度太高 | 生成器无法稳定处理 | 节点类型固定，先支持 Root / Particle / Mesh / Trail / Material |
| 连线形成循环 | 生成层级死循环 | 蓝图保存前做拓扑检查，禁止环 |
| 蓝图 JSON 手改出错 | 加载异常 | 反序列化后做 schema 校验，错误显示具体节点 ID |
| 节点参数和 Unity API 不一致 | 生成失败 | 蓝图先转标准配方，再由 Factory 生成 Prefab |

### 16.13 开发实现防线

正式写功能前建议建立以下防线：

1. **预检层**：所有生成前检查路径、覆盖、源资源、素材库缓存、Missing 引用。
2. **事务层**：所有新建资源加入 createdAssets 列表，失败时自动删除。
3. **隔离层**：源 Prefab、源材质、源贴图只读；生成只写输出目录。
4. **日志层**：每次生成产出 `Report/[name]_生成报告.json`。
5. **历史层**：保存配置快照、随机种子、成功/失败列表。
6. **质检层**：生成后自动跑质量检查，严重问题用红色提示。
7. **回滚层**：批量生成失败时允许“一键清理本次生成”。

### 16.14 MVP 阶段明确不做的内容

为了控制风险，以下内容不进入第一版 MVP：

- 不做自由文本生成特效。
- 不做 AI 自动审美判断。
- 不做复杂蓝图编辑器。
- 不做跨渲染管线 Shader 自动转换。
- 不做源素材自动重命名或移动。
- 不做直接修改源 Prefab 的功能。
- 不做自动合批或 Mesh 烘焙优化。

MVP 只保证：安全复制、独立材质、基础改色、基础粒子倍率、质检报告、历史记录。

---

## 十七、开发里程碑

### Milestone 0：独立工程骨架

预计 0.5 天。

- 创建 `Assets/Editor/MoshiVFXGenerator/`。
- 创建 asmdef。
- 创建主窗口 `Moshi_VFXGen`。
- 注册菜单 `工具/Moshi特效生成器/打开生成器`。
- 创建 `Settings/`、`Presets/`、`DesignDocs/`、`Documentation/`。

### Milestone 1：素材库 + 克隆器 MVP

预计 2～3 天。

- 素材扫描。
- 源 Prefab 扫描。
- 批量复制 Prefab。
- 材质实例复制。
- 色相偏移。
- 粒子参数倍率。
- 生成历史。

### Milestone 2：内置预览 + 质检

预计 1～2 天。

- 内置 PreviewRenderUtility 预览。
- 粒子播放控制。
- 质量检查。
- 生成报告。

### Milestone 3：配方一键生成

预计 3～4 天。

- 配方数据结构。
- 内置配方。
- 自动组装 Prefab。
- 自动匹配素材。

### Milestone 4：蓝图生成

预计 4～5 天。

- 节点画布。
- 蓝图保存加载。
- 蓝图转配方。
- 蓝图生成 Prefab。

---

## 十七、实施顺序建议

最推荐的落地顺序：

1. **先建独立骨架**：菜单、主窗口、目录、设置。
2. **先做克隆器**：最快产生可用效果。
3. **再做独立预览**：让生成结果能马上看。
4. **再做素材库**：支持更智能的替换和生成。
5. **再做配方一键生成**：开始从零创建特效。
6. **最后做蓝图**：提高上限，但不影响前期收益。

---

## 十八、最终形态

最终 `Moshi特效生成器` 应该是一个和 `Moshi工具箱` 平级的独立工具：

```text
Assets/Editor/
├── ToolsIntegrationPanel/      # Moshi工具箱：通用编辑器工具集合
└── MoshiVFXGenerator/          # Moshi特效生成器：特效生产专用平台
```

菜单上也保持平级区分：

```text
工具/Moshi/...
工具/Moshi特效生成器/打开生成器
```

工作流：

```text
素材库扫描 → 克隆变体 / 配方生成 / 蓝图生成 → 质检 → 预览 → 输出 Prefab
```

---

## 二十、下一步执行项

下一步应先创建独立工具骨架：

1. 创建 `Assets/Editor/MoshiVFXGenerator/`。
2. 创建 `MoshiVFXGenerator.Editor.asmdef`。
3. 创建 `Window/Moshi_VFXGen.cs`。
4. 注册菜单 `工具/Moshi特效生成器/打开生成器`。
5. 创建基础标签页：`素材库`、`克隆器`、`生成`、`质检`、`历史`。
6. 创建 `Settings/` 和默认配置 asset。
7. 再进入 `Moshi_VFXCloner` MVP 开发。
