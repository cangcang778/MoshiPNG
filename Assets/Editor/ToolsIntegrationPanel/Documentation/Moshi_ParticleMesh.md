# 粒子Mesh工具 (Moshi_ParticleMesh)

## 功能概述
一套用于分析和管理粒子系统 Mesh 资源的工具集，包括 Mesh 分析、预览、替换和材质复制等功能。

## 打开方式
**菜单路径**: 通过工具集成面板访问

## 工具组件

### 1. ParticleMeshToolWindow（主窗口）
粒子 Mesh 工具的主界面，集成了所有功能模块。

### 2. ParticleMeshAnalyzer（Mesh 分析器）
分析粒子系统中使用的 Mesh 资源。

**功能**:
- 扫描选中对象中的所有粒子系统
- 列出每个粒子系统使用的 Mesh
- 显示 Mesh 的顶点数、三角形数等信息
- 检测重复使用的 Mesh

### 3. MeshPreviewAnalyzer（Mesh 预览分析器）
提供 Mesh 的可视化预览功能。

**功能**:
- 3D 预览 Mesh 模型
- 显示 Mesh 详细信息
- 支持旋转、缩放查看

### 4. MaterialPreviewAnalyzer（材质预览分析器）
分析和预览粒子系统使用的材质。

**功能**:
- 列出所有使用的材质
- 预览材质效果
- 显示材质属性

### 5. MeshReplacementWindow（Mesh 替换窗口）
批量替换粒子系统中的 Mesh。

**功能**:
- 选择源 Mesh 和目标 Mesh
- 批量替换所有匹配的 Mesh
- 支持预览替换结果

### 6. ParticleMeshReplacer（Mesh 替换器）
执行实际的 Mesh 替换操作。

### 7. MaterialCopyUtility（材质复制工具）
复制和管理粒子系统材质。

**功能**:
- 复制材质到新位置
- 批量更新材质引用
- 创建材质变体

### 8. ParticleMeshVisualizationHelper（可视化辅助）
提供 Mesh 可视化相关的辅助功能。

## 数据类

### ParticleMeshData
存储粒子系统 Mesh 的相关数据。

### MeshPreviewData
存储 Mesh 预览所需的数据。

## 使用流程

### Mesh 分析流程
1. 在 Hierarchy 中选择包含粒子系统的对象
2. 打开粒子 Mesh 工具窗口
3. 点击"分析"按钮扫描 Mesh
4. 查看分析结果

### Mesh 替换流程
1. 分析当前使用的 Mesh
2. 选择要替换的源 Mesh
3. 指定新的目标 Mesh
4. 预览替换效果
5. 执行替换

### 材质复制流程
1. 选择要复制的材质
2. 指定目标路径
3. 执行复制
4. 更新粒子系统引用

## 注意事项
1. 替换操作支持 Undo
2. 建议在操作前备份项目
3. 大型项目分析可能需要较长时间
4. 材质复制会创建独立副本
