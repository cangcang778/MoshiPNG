# Timeline视频 (Moshi_TLVideo)

## 功能概述
一键生成 Timeline 视频播放所需的全部资源，包括 RenderTexture、Prefab，支持自定义分辨率和 UI RawImage 创建。

## 打开方式
**菜单路径**: `工具 > Moshi > Timeline视频`

---

## 核心功能

### 自动生成资源
- **RenderTexture**: 按指定分辨率创建视频渲染目标
- **Prefab**: 创建包含 VideoPlayer 组件的预制体
- **UI RawImage**: 可选创建用于 UGUI 显示的 RawImage 对象

### 分辨率设置
- 支持自定义宽度和高度
- 默认 1920×1080

---

## 使用流程
1. 拖入 VideoClip 文件
2. 设置 RenderTexture 和 Prefab 的保存路径
3. 设置分辨率（宽×高）
4. 可选：勾选创建 UI RawImage
5. 点击生成
6. 将生成的 Prefab 拖入场景，配合 Timeline 使用

---

## 注意事项
1. 生成的 RenderTexture 分辨率应与视频源匹配
2. Prefab 包含已配置好的 VideoPlayer 组件
3. UI RawImage 需要 Canvas 环境
4. 路径设置会自动保存，下次打开时恢复
