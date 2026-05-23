# 层级工具集 (Moshi_AddParent)

## 功能概述
Hierarchy 右键菜单工具集，提供添加父物体和替换为 Prefab 两个功能。

## 打开方式
**右键菜单**: 在 Hierarchy 中右键物体 → `Moshi/添加父物体` 或 `Moshi/替换为Prefab`

---

## 功能一：添加父物体

### 功能说明
为选中物体创建一个父物体，子物体坐标归零，同时自动偏移 Animation 组件中所有 AnimationClip 的位置曲线。

### 使用流程
1. 在 Hierarchy 中选中一个或多个物体
2. 右键 → `Moshi/添加父物体`
3. 自动创建父物体，子物体 localPosition/localRotation 归零
4. 如有 Animation 组件，自动偏移位置曲线补偿

### 特殊处理
- 父物体继承子物体的原始 localPosition 和 localRotation
- Animation 位置曲线（localPosition.x/y/z）自动偏移
- 支持多选批量操作
- 支持 Undo（Ctrl+Z）

---

## 功能二：替换为 Prefab

### 功能说明
将选中的物体替换为指定的 Prefab 实例，保持原物体的位置和层级关系。

### 使用流程
1. 在 Hierarchy 中选中要替换的物体
2. 右键 → `Moshi/替换为Prefab`
3. 在弹窗中选择目标 Prefab
4. 点击"替换"

---

## 注意事项
1. 所有操作支持 Undo（Ctrl+Z）
2. 添加父物体时自动处理 Animation 曲线偏移
3. 替换为 Prefab 时原物体会被销毁
