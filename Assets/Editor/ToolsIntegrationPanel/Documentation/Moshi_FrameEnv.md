# 逐帧渲染环境

`Moshi_FrameEnv` 用于生成透明逐帧渲染专用环境。

## 功能

- 生成黑底/白底差分双相机
- 创建 `RenderTargetRoot`
- 创建全局 URP 后处理 Volume
- 可选三点灯光
- 生成 `Moshi_FrameRenderConfig`，供 `Moshi_FrameRenderPro` 读取

## 使用流程

1. 打开菜单：`工具/Moshi/逐帧渲染环境`
2. 选择环境模式：特效 / UI / 角色 / 自定
3. 指定参考相机和目标根节点
4. 点击 `生成环境`
5. 打开 `透明逐帧渲染` 输出 PNG 序列帧

## 说明

该环境默认不创建背景板和地面，避免实体背景进入透明 PNG。透明输出由黑白差分重建 Alpha 完成。
