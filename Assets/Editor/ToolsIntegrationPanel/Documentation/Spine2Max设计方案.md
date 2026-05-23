# Spine → 3ds Max 导入工具 — 完整设计方案

## 一、功能概述

开发一个 3ds Max 端的 Python + pymxs 工具 **Spine2Max**，实现：
1. 解析 Spine 导出的 `.json` + `.atlas` + `.png` 文件
2. 在 3ds Max 中还原骨骼层级、网格附件、材质贴图
3. 导入动画关键帧并精确映射贝塞尔曲线
4. 兼容 Spine 3.8 / 4.x 两个版本
5. 适配 3ds Max 2024 / 2025（Python + pymxs）

## 二、版本兼容性策略

### 2.1 3ds Max 版本

| 版本 | 技术方案 | 状态 |
|------|---------|------|
| 3ds Max 2024/2025 | Python + pymxs | ✅ 主力开发 |
| 3ds Max 2014 | 纯 MaxScript | 🔜 后续适配 |

### 2.2 Spine 版本差异

| 差异项 | Spine 3.8 | Spine 4.x | 处理方式 |
|-------|-----------|-----------|---------|
| `skins` 格式 | Object `{ "default": {...} }` | Array `[{ "name": "default", ... }]` | 检测类型自动适配 |
| `bones.transform` | `inheritRotation`/`inheritScale` 布尔值 | `transform` 枚举值 | 版本判断分支 |
| `sequence` 附件 | 不存在 | 新增 | 4.x 特有，可选支持 |
| atlas 格式 | xy/size/orig 分行 | bounds 合并行 | 两种都解析 |

## 三、文件结构

```
Spine2Max/
├── Spine2Max_Launcher.ms           -- MaxScript 启动器（入口）
├── spine2max_main.py               -- Python 主入口：UI面板 + 调度逻辑
├── spine2max_json_parser.py        -- JSON 解析器
├── spine2max_atlas_parser.py       -- Atlas 文本解析（UV坐标计算）
├── spine2max_bone_builder.py       -- 骨骼层级构建
├── spine2max_mesh_builder.py       -- 网格+UV+共享材质构建
├── spine2max_skin_weights.py       -- Skin修改器+权重映射
├── spine2max_anim_importer.py      -- 动画关键帧导入+贝塞尔映射
├── spine2max_utils.py              -- 公共工具函数
└── README.md                       -- 使用说明
```

## 四、核心设计要点

### 4.1 Atlas UV 映射方案（非裁切）

**不裁切 Atlas 大图**，所有 mesh 共享一个材质球，通过 UV 坐标映射到 Atlas 大图的对应区域：

- Region 附件：从 atlas 文件解析像素坐标 → 归一化 UV
- Mesh 附件：Spine 的 `mesh.uvs` 已是归一化坐标，直接写入（仅翻转 V 轴）
- 支持 Atlas `rotate: true`（旋转90°打包）的 UV 特殊处理
- 多 Atlas 页：每张大图一个材质球

### 4.2 坐标映射

| Spine 2D | 3ds Max 3D | 说明 |
|----------|-----------|------|
| X | X | 水平方向一致 |
| Y | Z | Spine Y轴向上 → Max Z轴向上 |
| — | Y = 0 | 2D 平面，Y 用于 slot 深度排序 |
| rotation | Z 轴旋转 | Spine 旋转 → Max 绕 Z 轴旋转 |
| scaleX/Y | scaleX/Z | 对应映射 |

### 4.3 Slot 绘制顺序 → Z 深度

Slot 索引 × 0.01 → Max Y 轴偏移，保证渲染遮挡关系正确。

### 4.4 贝塞尔曲线精确映射

Spine: `curve: [cx1, cy1, cx2, cy2]` 归一化三次贝塞尔
Max: Bezier Float Controller 切线

映射公式：
```
outSlope = cy1 / cx1
inSlope  = (1-cy2) / (1-cx2)
Max outTangent = outSlope × ΔValue / 3
Max inTangent  = inSlope  × ΔValue / 3
```

支持三种插值模式：
| Spine | Max | 说明 |
|-------|-----|------|
| `"linear"` | `#linear` | 线性 |
| `"stepped"` | `#step` | 阶梯 |
| `[cx1,cy1,cx2,cy2]` | `#custom` Bezier | 贝塞尔精确映射 |

### 4.5 Mesh 顶点权重格式

无权重：`vertices: [x1, y1, x2, y2, ...]` (vertices.Count == uvs.Count)
有权重：`vertices: [boneCount, boneIdx, x, y, weight, ...]` (vertices.Count != uvs.Count)

### 4.6 Deform 变形 → Morph 目标

逐关键帧创建变形目标 mesh，通过 Morpher 修改器权重动画驱动。

### 4.7 Slot 可见性切换

attachment 切换 → visibility 关键帧（step 插值），color 动画 → 材质 opacity。

## 五、UI 面板设计

```
┌─────────────────────────────────────────────┐
│          Spine → 3ds Max Importer           │
├─────────────────────────────────────────────┤
│  Spine JSON:  [________________________] [..] │
│  Atlas: (自动检测)   贴图: (自动检测)        │
│                                             │
│  ─── Spine 信息 ───                         │
│  版本: 3.8.75   骨骼: 24   动画: 5         │
│  图集: 1024×1024   区域: 36                │
│                                             │
│  ─── 导入选项 ───                           │
│  缩放比例: [ 1.0 ]                         │
│                                             │
│  ─── 导入内容 ───                           │
│  [✓] 骨骼  [✓] Region附件  [✓] Mesh附件    │
│  [✓] 动画  [✓] Deform变形                  │
│                                             │
│  ─── 动画设置 ───                           │
│  帧率:  (●) 跟随Spine  ( ) 自定义 [30]     │
│  曲线:  (●) 贝塞尔精确  ( ) 线性近似       │
│                                             │
│        [ 预览信息 ]  [ 导入 ]               │
│                                             │
│  ─── 日志 ───                               │
│  ✓ 已创建 24 根骨骼                        │
│  ✓ 已创建 36 个网格附件                    │
│  ✓ 已导入动画 (共 420 个关键帧)            │
└─────────────────────────────────────────────┘
```

## 六、安装与使用

1. 将 `Spine2Max/` 文件夹放到 Max 的 `scripts/` 目录下
2. Max 菜单 → MAXScript → Run Script → 选择 `Spine2Max_Launcher.ms`
3. 弹出面板 → 选择 `.json` 文件 → 自动检测 atlas 和贴图
4. 确认选项 → 点击「导入」

快速命令行导入（无 UI）：
```python
import spine2max_main
spine2max_main.quick_import(r"C:/path/to/spine.json", scale=0.1)
```

## 七、风险与应对

| 风险 | 应对方案 |
|------|---------|
| 大文件性能（10MB+ JSON） | Python json 模块性能足够 |
| Skin 修改器需要 select+modPanel | 已在代码中处理 |
| 贝塞尔精度微小误差 | < 0.1%，视觉不可察觉 |
| Spine 4.x 新特性 | 跳过不影响基础骨骼动画 |
| 多 Atlas 页 | 每张大图一个材质球 |

## 八、状态

🚧 开发中

---

*创建日期：2026-03-27*
*最后更新：2026-03-27*
