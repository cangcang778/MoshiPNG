# Moshi 工具箱使用说明

## 概述
Moshi 工具箱是一套专为 Unity 特效制作和资源管理设计的编辑器工具集，涵盖粒子系统、动画编辑、图片处理、资源管理、Timeline 增强等多个领域。

## 打开方式
- **主面板**: `Tools > MoshiResourceTools`
- **单独工具**: `工具 > Moshi > [工具名称]`

---

## 工具列表

### 资源管理类

| 工具名称 | 文件名 | 功能简介 |
|---------|--------|---------|
| 资源收藏夹 | Moshi_Favorites | 快速收藏和定位 Project/Hierarchy 中的资源 |
| 批量重命名 | Moshi_BatchRename | 批量重命名文件和 GameObject |
| 引用计数统计 | Moshi_RefCounter | 统计资源引用、检测零引用、依赖整合 |
| 资源清理 | Moshi_AssetClean | 空材质检测 / 残留清理 / 缩放修复 / 空粒子清理 |
| 图片分类归档 | Moshi_ImgClassifier | 基于视觉指纹的图片自动分类归档 + 重复查清 |

### 粒子系统类

| 工具名称 | 文件名 | 功能简介 |
|---------|--------|---------|
| 粒子批量修改 | Moshi_ParticleBatch | 批量修改粒子系统参数 |
| 粒子排序修改 | Moshi_ParticleOrder | 增量修改 Order/Fudge/Delay/Lifetime |
| 粒子Mesh工具 | Moshi_ParticleMesh | Mesh 分析、预览、替换、材质复制 |

### 动画编辑类

| 工具名称 | 文件名 | 功能简介 |
|---------|--------|---------|
| 动画片段助手 | Moshi_ClipHelper | AnimationClip 关键帧操作、曲线优化 |
| 动画路径修复 | Moshi_AnimPath | 批量修改 AnimationClip/Timeline 中的路径 |
| 骨骼动画烘焙 | Moshi_SpineBake | 将 Spine 骨骼动画烘焙为 Unity AnimationClip |

### 图片处理类

| 工具名称 | 文件名 | 功能简介 |
|---------|--------|---------|
| 图片处理工具 | Moshi_TextureTool | 拆分/合并/通道/去黑/加黑/UnMult/尺寸/色条/材质渲染/极坐标 |
| 图片转面片 | Moshi_ImgToQuad | 图片批量生成 Quad/Sprite/粒子，支持 PSD |
| SVG位置导入 | Moshi_SvgImport | 从 SVG 解析位置生成对象 |

### 生成工具类

| 工具名称 | 文件名 | 功能简介 |
|---------|--------|---------|
| 物体阵列工具 | Moshi_GOArray | 线性/网格/环形/螺旋/路径阵列 |
| 路径工具 | Moshi_PathTool | 3ds Max 路径导入、路径动画烘焙 |

### Timeline 类

| 工具名称 | 文件名 | 功能简介 |
|---------|--------|---------|
| Timeline增强 | Moshi_TLEnhance | 类 AE 快捷键（Clip移动/裁剪）+ 轨道快速选择 |
| TL批量导入 | Moshi_TLBatch | Timeline Clip 批量导入，4种排列模式 |
| Timeline视频 | Moshi_TLVideo | 一键生成视频播放所需资源 |

### 层级工具类

| 工具名称 | 文件名 | 功能简介 |
|---------|--------|---------|
| 层级工具集 | Moshi_AddParent | 添加父物体（坐标归零+动画偏移）/ 替换为 Prefab |

---

## 快速入门

### 1. 打开工具箱主面板
菜单 `Tools > MoshiResourceTools`，可以看到所有工具的快捷入口。

### 2. 使用单独工具
菜单 `工具 > Moshi`，选择需要的工具直接打开。

### 3. 查看帮助文档
每个工具窗口右上角都有 `?` 帮助按钮，点击可定位到对应的使用说明文档。

---

## 通用操作

### Undo 支持
所有工具的修改操作都支持 `Ctrl+Z` 撤销。

### 帮助文档
点击工具窗口的 `?` 按钮查看详细说明。

### 文档位置
`Assets/Editor/ToolsIntegrationPanel/Documentation/`

---

## 版本信息
- 作者: Moshi
- 适用版本: Unity 2019.4+
