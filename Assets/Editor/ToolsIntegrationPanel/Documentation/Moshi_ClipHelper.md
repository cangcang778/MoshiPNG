# 动画片段助手 (Moshi_ClipHelper)

## 功能概述
强大的动画曲线编辑工具，支持 AnimationClip 和粒子系统曲线的关键帧操作、曲线优化和曲线转换。

## 打开方式
**菜单路径**: `工具 > Moshi > 动画片段助手`

## 功能模块

### 1. 关键帧操作

#### 数据源选择
- **Animation Clip**: 操作 AnimationClip 文件
- **Particle System**: 操作粒子系统曲线

#### AnimationClip 模式功能
- **删除选中关键帧**: 删除 Animation 窗口中选中的关键帧
- **删除范围内关键帧**: 删除指定时间范围内的关键帧
- **插入关键帧**: 在指定位置插入关键帧
- **移动关键帧**: 将选中的关键帧移动到新位置
- **缩放时间**: 按比例缩放关键帧时间

#### Particle System 模式功能
- 支持批量选中多个粒子系统
- 在 Hierarchy 中选择对象后自动获取粒子系统
- 显示粒子系统列表，可单独移除

### 2. 曲线优化

#### 优化模式
- **智能优化**: 使用 Douglas-Peucker 算法减少关键帧数量，保持曲线形状
- **水平翻转**: 在时间轴上翻转曲线（倒放效果）
- **垂直翻转**: 沿中心值上下翻转曲线

#### 智能优化参数
- **误差容忍度**: 控制优化精度
  - 精确 (0.0001): 保留更多细节
  - 标准 (0.001): 平衡精度和压缩率
  - 宽松 (0.01): 更高压缩率
  - 激进 (0.05): 最大压缩

#### 翻转参数
- **翻转所有曲线**: 是否翻转所有可用曲线
- **自动计算中心值**: 自动计算曲线的中心点
- **翻转中心值**: 手动指定翻转中心

### 3. 曲线转换

#### 位置曲线转换
将世界坐标位置曲线转换为本地坐标或相对坐标。

#### 转换模式
- **World to Local**: 世界坐标转本地坐标
- **Local to World**: 本地坐标转世界坐标
- **Relative**: 相对于起始位置

## 粒子系统批量操作

### 支持的模块
- Main（Start Lifetime, Start Speed, Start Size 等）
- Emission（Rate over Time, Rate over Distance）
- Velocity over Lifetime
- Limit Velocity over Lifetime
- Force over Lifetime
- Size over Lifetime
- Size by Speed
- Rotation over Lifetime
- Rotation by Speed
- Noise

### 批量选择
1. 在 Hierarchy 中选择包含粒子系统的对象
2. 工具自动获取所有粒子系统（包括子对象）
3. 曲线列表显示格式：`[粒子系统名] 模块/属性`

## 使用技巧
1. 配合 Animation 窗口使用效果更佳
2. 优化前可先预览效果
3. 批量操作支持 Undo
4. 粒子系统模式下切换选择会自动刷新

## 注意事项
1. 操作前建议备份动画文件
2. 过度优化可能导致动画失真
3. 粒子系统曲线修改会立即生效
4. 支持 Undo 操作（Ctrl+Z）
