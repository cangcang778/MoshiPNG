# Moshi 工具箱 · 知识库总索引

> 本文件是 Moshi 工具箱的**可检索索引页**。IMA 知识库搜索时命中此文件即可得到：
> - 工具总览矩阵
> - 按功能域、菜单路径、关键词三种维度检索
> - 每个工具对应的详细知识条目定位

---

## 一、项目定位

- **名称**：Moshi 工具箱 / Moshi Toolbox
- **面向**：Unity 特效 / TA / 美术
- **形态**：Unity Editor 扩展，编辑器内可视化工具集
- **总入口**：菜单 `Tools > MoshiResourceTools`（主面板 `ToolsIntegrationPanel`）
- **单工具入口**：菜单 `工具 > Moshi > [工具名]`
- **Unity 版本**：2021.3+（部分工具要求 2019.4+）
- **代码根目录**：`Assets/Editor/ToolsIntegrationPanel/AllToolsResources/`
- **运行时脚本**：`Assets/Scripts/MoshiScripts/`
- **文档根目录**：`Assets/Editor/ToolsIntegrationPanel/Documentation/`

---

## 二、工具总览矩阵（22 个）

> 特效分析器、特效浏览器已迁移到平级独立工具 `Assets/Editor/MoshiVFXGenerator/`，不再计入 Moshi工具箱。

| # | 工具名（中文） | 类名 | 菜单路径 | 功能域 | 类型 |
|---|---|---|---|---|---|
| 1 | 批量重命名 | `Moshi_BatchRename` | `工具/Moshi/批量重命名` | 资源管理 | EditorWindow |
| 2 | 资源收藏夹 | `Moshi_Favorites` | `工具/Moshi/资源收藏夹` | 资源管理 | EditorWindow |
| 3 | 引用计数统计 | `Moshi_RefCounter` | `工具/Moshi/引用计数统计` | 资源管理 | EditorWindow |
| 4 | 资源清理 | `Moshi_AssetClean` | `工具/Moshi/资源清理` | 资源管理 | EditorWindow |
| 5 | 图片分类归档 | `Moshi_ImgClassifier` | `工具/Moshi/图片分类归档` | 资源管理 | EditorWindow |
| 6 | 粒子批量修改 | `Moshi_ParticleBatch` | `工具/Moshi/粒子批量修改` | 粒子系统 | EditorWindow |
| 7 | 粒子排序修改 | `Moshi_ParticleOrder` | `工具/Moshi/粒子排序修改` | 粒子系统 | EditorWindow |
| 8 | 粒子Mesh工具 | `Moshi_ParticleMesh` | `工具/Moshi/粒子Mesh工具` | 粒子系统 | EditorWindow |
| 9 | 反向排序 | `Moshi_ReverseOrder` | `工具/Moshi/反向排序` | 粒子系统 | EditorWindow |
| 10 | 动画片段助手 | `Moshi_ClipHelper` | `工具/Moshi/动画片段助手` | 动画编辑 | EditorWindow |
| 11 | 动画路径修复 | `Moshi_AnimPath` | `工具/Moshi/动画路径修复` | 动画编辑 | EditorWindow |
| 12 | 骨骼动画烘焙 | `Moshi_SpineBake` | `工具/Moshi/骨骼动画烘焙` | 动画编辑 | EditorWindow |
| 13 | 图片处理工具 | `Moshi_TextureTool` | `工具/Moshi/图片处理工具` | 图片处理 | EditorWindow |
| 14 | 图片转面片 | `Moshi_ImgToQuad` | `工具/Moshi/图片转面片` | 图片处理 | EditorWindow |
| 15 | SVG位置导入 | `Moshi_SvgImport` | `工具/Moshi/SVG位置导入` | 图片处理 | EditorWindow |
| 16 | 物体阵列工具 | `Moshi_GOArray` | `工具/Moshi/物体阵列工具` | 生成工具 | EditorWindow |
| 17 | 路径工具 | `Moshi_PathTool` | `工具/Moshi/路径工具` | 生成工具 | EditorWindow |
| 18 | 轴心对齐 | `Moshi_PivotAlign` | `工具/Moshi/轴心对齐` | 生成工具 | EditorWindow |
| 19 | Timeline增强 | `Moshi_TLEnhance` | `工具/Moshi/Timeline增强` | Timeline | EditorWindow |
| 20 | TL批量导入 | `Moshi_TLBatch` | `工具/Moshi/TL批量导入` | Timeline | EditorWindow |
| 21 | Timeline视频 | `Moshi_TLVideo` | `工具/Moshi/Timeline视频` | Timeline | EditorWindow |
| 22 | 层级工具集 | `Moshi_AddParent` | `GameObject/Moshi/添加父物体`、`GameObject/Moshi/替换为Prefab` | 层级工具 | 右键菜单 |
| - | 工具箱导出 | `Moshi_Exporter` | `工具/Moshi/工具箱导出` | 工具箱辅助 | EditorWindow |

---

## 三、按功能域分组

### 3.1 资源管理（5 个）
解决资产命名、收藏、引用统计、清理、分类归档。
→ `Moshi_BatchRename`、`Moshi_Favorites`、`Moshi_RefCounter`、`Moshi_AssetClean`、`Moshi_ImgClassifier`

### 3.2 粒子系统（4 个）
处理 ParticleSystem 的批量参数、排序、Mesh、反序。
→ `Moshi_ParticleBatch`、`Moshi_ParticleOrder`、`Moshi_ParticleMesh`、`Moshi_ReverseOrder`

> 特效分析/浏览能力已迁移到平级独立工具 `Moshi特效生成器`。

### 3.3 动画编辑（3 个）
AnimationClip 曲线辅助、动画路径批量修复、Spine 骨骼烘焙。
→ `Moshi_ClipHelper`、`Moshi_AnimPath`、`Moshi_SpineBake`

### 3.4 图片处理（3 个）
通用图像处理、图片转面片、SVG 坐标导入。
→ `Moshi_TextureTool`、`Moshi_ImgToQuad`、`Moshi_SvgImport`

### 3.5 生成工具（3 个）
阵列、路径、轴心。
→ `Moshi_GOArray`、`Moshi_PathTool`、`Moshi_PivotAlign`

### 3.6 Timeline（3 个）
快捷键增强、批量导入、视频资源一键生成。
→ `Moshi_TLEnhance`、`Moshi_TLBatch`、`Moshi_TLVideo`

### 3.7 层级工具（1 个）
Hierarchy 右键扩展。
→ `Moshi_AddParent`

### 3.8 工具箱辅助（1 个）
打包导出自身。
→ `Moshi_Exporter`

---

## 四、关键词检索表

| 我想做... | 选这个工具 |
|---|---|
| 批量改资源/对象名字 | `Moshi_BatchRename` |
| 加序号、前缀、后缀 | `Moshi_BatchRename` |
| 收藏常用资源/对象 | `Moshi_Favorites` |
| 查找资源被谁引用 | `Moshi_RefCounter` |
| 找零引用资源（可删） | `Moshi_RefCounter` |
| 资源依赖整合到一个目录 | `Moshi_RefCounter` |
| 检查空材质/Missing 组件 | `Moshi_AssetClean` |
| 清理空粒子系统 | `Moshi_AssetClean` |
| 粒子缩放修复 | `Moshi_AssetClean` |
| 贴图非 4 整除/超大检查 | `Moshi_AssetClean` |
| 贴图压缩批量设置 | `Moshi_AssetClean`（内含压缩窗口） |
| 相似/重复图片分类 | `Moshi_ImgClassifier` |
| 一键改所有粒子 Duration | `Moshi_ParticleBatch` |
| 粒子 SortingOrder 增量 | `Moshi_ParticleOrder` |
| 粒子 StartDelay 递增/递减 | `Moshi_ParticleOrder` |
| 粒子 Mesh 查看/替换 | `Moshi_ParticleMesh` |
| 反向某些对象 | `Moshi_ReverseOrder` |
| 分析特效性能 | `Moshi_VFXAnalyzer` |
| 像资源管理器一样看特效 | `Moshi_VfxBrowser` |
| 粒子 Mesh 预览缩略图 | `Moshi_VfxBrowser` |
| 动画曲线优化 / DP 压缩 | `Moshi_ClipHelper` |
| 动画曲线水平/垂直翻转 | `Moshi_ClipHelper` |
| 位移曲线↔速度曲线 | `Moshi_ClipHelper` |
| AnimationClip 改路径 | `Moshi_AnimPath` |
| Timeline 动画路径改 | `Moshi_AnimPath` |
| Spine 烘焙为 AnimationClip | `Moshi_SpineBake` |
| 贴图拆分/合并 RGBA 通道 | `Moshi_TextureTool` |
| UnMult 去预乘 | `Moshi_TextureTool` |
| PSD/图片转 Quad/Sprite | `Moshi_ImgToQuad` |
| SVG 解析生成对象 | `Moshi_SvgImport` |
| 线性/网格/环形/螺旋阵列 | `Moshi_GOArray` |
| 路径动画烘焙 | `Moshi_PathTool` |
| 3ds Max 导入路径 | `Moshi_PathTool` |
| 对齐轴心到 Mesh 中心 | `Moshi_PivotAlign` |
| Timeline AE 式快捷键 | `Moshi_TLEnhance` |
| Timeline 批量导入 Clip | `Moshi_TLBatch` |
| Timeline 播视频 | `Moshi_TLVideo` |
| 添加父物体归零坐标 | `Moshi_AddParent` |
| 用 Prefab 替换 GameObject | `Moshi_AddParent`（替换为Prefab） |

---

## 五、跨工具共性特征

| 特征 | 涉及工具 |
|---|---|
| 命名空间 `MoshiTools` | 全部 24 个 |
| 类前缀 `Moshi_` | 全部 24 个 |
| 菜单前缀 `工具/Moshi/` | 22 个（除 `Moshi_AddParent` 走 `GameObject/Moshi/`） |
| 集成 `MoshiHelpButton` 帮助按钮 | 全部 EditorWindow 工具 |
| 支持 `Undo.RecordObject` | 全部涉及修改的工具 |
| 使用 `EditorPrefs` 持久化用户设置 | `Moshi_VfxBrowser`、`Moshi_RefCounter`、`Moshi_ClipHelper` 等 |
| 使用 ScriptableObject 配置 | `Moshi_Favorites`（Moshi_FavoritesConfig）、主面板（`MyToolsConfig`） |
| 使用 Preset | `Moshi_ImgToQuad`、`Moshi_SvgImport` |
| 使用 ComputeShader | `Moshi_TextureTool`（Compute 目录下 `.compute` 文件） |
| 依赖 Timeline/Playables | `Moshi_AnimPath`、`Moshi_AssetClean`（残留模块）、`Moshi_TLEnhance`、`Moshi_TLBatch`、`Moshi_TLVideo` |
| 依赖 Spine Runtime | `Moshi_SpineBake` |
| 依赖 U2D SpriteAtlas | `Moshi_RefCounter` |
| 依赖 Video | `Moshi_RefCounter`、`Moshi_TLVideo` |


---

## 六、调用知识条目的定位方式

详细知识条目位于 `IMA_KnowledgeBase/Tools/` 目录，一个工具一份 Markdown 文件：

```
IMA_KnowledgeBase/
├── 00_Moshi_Toolbox_Index.md        ← 本文件（总索引）
├── 01_Framework_ToolsIntegrationPanel.md   ← 面板机制/注册/配置
└── Tools/
    ├── Moshi_AddParent.md
    ├── Moshi_AnimPath.md
    ├── Moshi_AssetClean.md
    ├── Moshi_BatchRename.md
    ├── Moshi_ClipHelper.md
    ├── Moshi_Exporter.md
    ├── Moshi_Favorites.md
    ├── Moshi_GOArray.md
    ├── Moshi_ImgClassifier.md
    ├── Moshi_ImgToQuad.md
    ├── Moshi_ParticleBatch.md
    ├── Moshi_ParticleMesh.md
    ├── Moshi_ParticleOrder.md
    ├── Moshi_PathTool.md
    ├── Moshi_PivotAlign.md
    ├── Moshi_RefCounter.md
    ├── Moshi_ReverseOrder.md
    ├── Moshi_SpineBake.md
    ├── Moshi_SvgImport.md
    ├── Moshi_TextureTool.md
    ├── Moshi_TLBatch.md
    ├── Moshi_TLEnhance.md
    └── Moshi_TLVideo.md
```

每份工具卡片固定 8 段结构，便于 AI 召回：

1. **身份信息** — 类名、菜单、窗口标题、功能域
2. **一句话定义** — 20 字内核心功能
3. **能力清单** — 逐条可执行动作
4. **使用流程** — 最小可行调用步骤
5. **参数与输入** — 用户要提供什么
6. **产出与副作用** — 修改/生成什么
7. **关联文件** — 源码结构、配置 asset、依赖
8. **常见问题** — 易踩的坑

---

## 七、版本与作者

- 作者：Moshi
- 适用 Unity：2021.3+
- 编码规范参考：`MoshiTools` 命名空间、`Moshi_` 类前缀、菜单路径 `工具/Moshi/`
- 文档产出日期：见文件头
