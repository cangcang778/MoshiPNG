# 粒子批量修改工具 (Moshi_ParticleBatch)

## 功能概述
用于批量修改选中对象及其子对象中所有粒子系统的常用参数，适合统一调整特效的播放行为。

## 打开方式
**菜单路径**: `工具 > Moshi > 粒子批量修改`

## 功能参数

### 基础参数
- **Duration = Start Lifetime**: 自动将 Duration 设置为与 Start Lifetime 相同
- **关闭 Prewarm**: 禁用预热功能
- **最大粒子数**: 设置 Max Particles 数量
- **Ring Buffer 模式**: 设置粒子缓冲区模式（PauseUntilReplaced 等）
- **清空发射率**: 将 Rate over Time 和 Rate over Distance 设为 0

### Start Lifetime 修改
- **修改 Start Lifetime**: 启用后可设置统一的 Start Lifetime 值

### Color over Lifetime 控制
- **启用 Color over Lifetime**: 启用后可设置颜色渐变曲线

### Burst 修改
- **修改 Bursts**: 启用后可设置 Burst 的发射数量

## 使用流程
1. 在 Hierarchy 中选择包含粒子系统的 GameObject（支持多选）
2. 配置要修改的参数
3. 点击"应用修改到选中对象"按钮
4. 查看修改结果统计

## 注意事项
1. 会递归处理选中对象的所有子对象中的粒子系统
2. 支持 Undo（Ctrl+Z）撤销操作
3. 只有勾选的参数才会被修改
