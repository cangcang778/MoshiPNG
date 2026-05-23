# 骨骼动画烘焙 (Moshi_SpineBake)

## 功能概述
将 Spine 骨骼动画烘焙为 Unity AnimationClip，支持世界/本地坐标空间选择和新建/合并两种输出模式。

## 打开方式
**菜单路径**: `工具 > Moshi > 骨骼动画烘焙`

---

## 核心功能

### 坐标空间
| 模式 | 说明 |
|------|------|
| 世界坐标 | 烘焙结果使用世界空间坐标 |
| 本地坐标 | 烘焙结果使用骨骼本地空间坐标 |

### 输出模式
| 模式 | 说明 |
|------|------|
| 新建 Clip | 为每个 Spine 动画创建独立的 AnimationClip 文件 |
| 合并 Clip | 将多个 Spine 动画合并到一个 AnimationClip 中 |

---

## 使用流程
1. 在场景中选中包含 SkeletonAnimation 组件的物体
2. 打开骨骼动画烘焙窗口
3. 选择坐标空间（世界/本地）
4. 选择输出模式（新建/合并）
5. 选择要烘焙的 Spine 动画
6. 设置输出路径
7. 点击"烘焙"执行

---

## 支持的 Spine 组件
- SkeletonAnimation（MeshRenderer 渲染）
- SkeletonGraphic（UGUI 渲染）

## 兼容性
- 支持 Spine 3.8 和 4.x 运行时
- 核心 API（AnimationState/Skeleton/timeScale 等）两个版本完全兼容
- 4.x 新增属性（freeze/updateMode 等）通过反射安全设置

---

## 注意事项
1. 烘焙前确保 Spine 组件已正确初始化
2. 世界坐标模式适合独立播放的动画
3. 本地坐标模式适合需要与其他动画混合的情况
4. 合并模式会按时间顺序排列动画片段
5. 输出的 AnimationClip 可直接在 Animator 或 Timeline 中使用
