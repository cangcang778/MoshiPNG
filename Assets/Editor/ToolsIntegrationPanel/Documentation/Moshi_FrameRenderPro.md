# 透明逐帧渲染

`Moshi_FrameRenderPro` 用于输出带 URP 后处理的透明 PNG 序列帧。

## 核心原理

默认使用“遮罩”专业模式：

1. 黑底相机开启 URP 后处理，渲染最终颜色。
2. 黑底/白底相机关闭后处理，单独渲染透明遮罩。
3. 用无后期遮罩重建 Alpha，再用后期颜色合成透明 PNG。
4. 根据后期亮度补充 Bloom 辉光区域 Alpha，减少黑边和灰边。

保留旧差分模式：

```text
black = color * alpha
white = color * alpha + (1 - alpha)
alpha = 1 - (white - black)
color = black / alpha
```

旧差分模式适合普通半透明，不适合强 Bloom / ACES / ColorAdjust 的复杂后期。

## 功能

- 专业遮罩合成透明重建
- HDR 渲染缓冲
- Bloom 辉光 Alpha 补偿
- 黑白差分透明重建
- 直接 Alpha 输出模式
- 粒子系统逐帧采样
- Animator / Animation / Timeline 采样
- 超采样 1x / 2x / 4x
- PNG 序列帧输出
- 可选 SpriteSheet 输出
- 可选透明边界裁切
- 可选目标根节点 Layer 隔离

## 使用流程

1. 先用 `逐帧渲染环境` 生成环境
2. 打开菜单：`工具/Moshi/透明逐帧渲染`
3. 点击 `自动` 或手动指定配置
4. 设置帧范围、FPS、分辨率和输出目录
5. 点击 `预览起始帧` 检查画面
6. 点击 `开始渲染`

## 注意

- 差分模式需要黑底和白底两台相机。
- 实体背景不会被差分自动去掉，建议启用 `隔离目标根节点`。
- 后处理需要相机上的 `UniversalAdditionalCameraData.renderPostProcessing` 开启。
- 如果要接近 Game 视图，请在 `逐帧渲染环境` 中选择后处理 `同步`，工具会复用当前场景中优先级最高的 Volume Profile。
- 默认 `遮罩` 模式比旧 `差分` 模式更适合 Bloom、ACES 和 Color Adjustments。
- 加法粒子在黑白差分里可能得不到有效 Alpha，工具会自动回退到后期亮度 Alpha；如果画面偏淡，提高 `辉光 Alpha`。
