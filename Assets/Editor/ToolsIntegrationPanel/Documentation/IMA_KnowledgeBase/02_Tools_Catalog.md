# Moshi 工具箱 · 工具目录详解（Catalog）

> 本文件按字母顺序罗列 Moshi 工具箱中**全部 24 个工具**的详细可调用知识。
> 每个工具固定 8 段结构：身份 / 一句话 / 能力 / 流程 / 参数 / 产出 / 关联 / 常见问题。
>
> 相关辅助文档：
> - `00_Moshi_Toolbox_Index.md` — 工具总索引与关键词检索表
> - `01_Framework_ToolsIntegrationPanel.md` — 主面板/注册/帮助按钮机制

---

# Moshi_AddParent · 层级工具集

## 身份
- **类名**：`Moshi_AddParent`（静态类）+ `ReplaceWithPrefabWindow : EditorWindow`
- **菜单**：`GameObject/Moshi/添加父物体`、`GameObject/Moshi/替换为Prefab`（Hierarchy 右键）
- **窗口标题**：弹窗 `"替换为Prefab"`
- **功能域**：层级工具
- **源码**：`AllToolsResources/Moshi_AddParent/Moshi_AddParent.cs`（单文件）

## 一句话
Hierarchy 右键扩展：为选中物体加父节点、或用 Prefab 替换选中物体。

## 能力
- **添加父物体**：在选中物体的父级插入新 GameObject，新父节点坐标归零
- **替换为 Prefab**：把选中物体替换为指定 Prefab 实例，保留位置/父级/层级索引
- 替换弹窗中可选：保留原物体子物体、沿用原名、保留额外组件（如 `JSBehaviour`）
- 基础组件（Transform / RectTransform / CanvasRenderer）自动跳过迁移
- 完整 Undo 支持，组名 `"替换为Prefab"`

## 流程
- **添加父物体**：Hierarchy 选中 → 右键 → `Moshi/添加父物体`
- **替换为 Prefab**：Hierarchy 选中 → 右键 → `Moshi/替换为Prefab` → 弹窗拖入 Prefab → 勾选迁移选项 → 确认

## 参数
| 参数 | 说明 |
|---|---|
| Prefab | 目标 Prefab 资产 |
| 保留子物体 | 把原物体的 children 迁移到新实例下 |
| 沿用原名 | 用原 GameObject 名字覆盖 Prefab 名 |
| 迁移组件列表 | 多选要从原物体复制到新实例的 Component |

## 产出
- 添加父物体：新建空 GameObject 插入层级
- 替换为 Prefab：销毁原物体，生成 PrefabInstance；支持 `Ctrl+Z`

## 关联
- 无配置 asset
- 无辅助文件

## 常见问题
- **Q**：替换后位置不对？**A**：本工具会记录原 worldPosition 再覆盖到新实例，如果 Prefab 自带偏移，编辑 Prefab 内部位置归零即可。
- **Q**：替换后 Missing Script？**A**：原物体上挂了 Prefab 没有的自定义脚本，勾选"迁移额外组件"把它们带过去。

---

# Moshi_AnimPath · 动画路径修复

## 身份
- **类名**：`Moshi_AnimPath : EditorWindow`
- **菜单**：`工具/Moshi/动画路径修复`
- **窗口标题**：`"动画路径修复"`
- **功能域**：动画编辑
- **源码**：`AllToolsResources/Moshi_AnimPath/Moshi_AnimPath.cs`（单文件）

## 一句话
批量修改 AnimationClip 和 Timeline 中绑定的 path 字段。

## 能力
- 三标签：扫描修复 / 操作日志 / 设置
- 扫描范围：选中 / 单文件夹 / 多文件夹（可含子目录）
- 资源类型：AnimationClip / Timeline / 两者
- 路径编辑模式：**替换、加前缀、加后缀、在索引处插入、移除片段**
- 支持路径过滤（只处理包含某段的路径）
- 支持 Clip 名过滤
- Shift 多选
- 自动备份目标文件到 `Assets/AnimationBackups/`（可配置开关和路径）

## 流程
1. 菜单打开工具 → 选 **扫描修复** 标签
2. 选扫描范围 + 资源类型 → 点扫描
3. 在结果列表中按 Shift 或 Ctrl 多选要改的条目
4. 选路径模式（替换/前后缀/插入/移除）填参数
5. 预览生效结果 → 点"应用"
6. 有问题在 `AnimationBackups/` 取备份回滚

## 参数
| 参数 | 说明 |
|---|---|
| 范围 | 选中 / 文件夹 / 多文件夹 |
| 递归子目录 | bool |
| 资源类型 | AnimationClip / Timeline / Both |
| 模式 | Replace / Prefix / Suffix / InsertAt / Remove |
| 查找词 | 在 path 中匹配的子串 |
| 替换词 | 新子串 |
| 插入位置 | 索引（段编号，用 `/` 分隔） |

## 产出
- 直接修改 `AnimationClip` 的 `EditorCurveBinding.path`
- 直接修改 Timeline Track 的 binding 信息
- 自动备份到 `Assets/AnimationBackups/`

## 关联
- 集成 `MoshiHelpButton`
- 依赖 `UnityEngine.Timeline` / `UnityEngine.Playables`
- 配合工具：`Moshi_BatchRename` 改完对象名后用它同步动画

## 常见问题
- **Q**：动画路径是否支持正则？**A**：目前是字符串查找替换，不是正则。
- **Q**：备份文件去哪了？**A**：默认 `Assets/AnimationBackups/时间戳/`，可在"设置"标签改。

---

# Moshi_AssetClean · 资源清理（五合一）

## 身份
- **类名**：`Moshi_AssetClean`（partial 分文件）
- **菜单**：`工具/Moshi/资源清理`
- **窗口标题**：`"资源清理"`
- **功能域**：资源管理
- **源码**：`AllToolsResources/Moshi_AssetClean/`（7 个 .cs 文件）

## 一句话
五个清理子模块合一：空材质、残留、缩放修复、空粒子、贴图检查。

## 能力
- **NullMat**：扫描引用为 `Missing` 或 `None` 的材质槽
- **Residual**：扫描孤立组件、Missing Script、Timeline 残留轨道/Clip
- **ScaleFix**：修复粒子系统被父节点非 1 缩放影响导致的错误大小
- **EmptyPS**：清理 Renderer+SubEmitters 全关的空粒子系统（支持自动清空引用/关闭）
- **TexCheck**：检查非 4 整除、>1024、>2MB 的贴图，一键修复或压缩
- **TexCompress**：贴图压缩设置内置弹窗
- 扫描范围统一：Selection / Scene / Prefab / 文件夹（可递归）

## 流程
1. 选模块标签（NullMat / Residual / ScaleFix / EmptyPS / TexCheck）
2. 选扫描范围（选中 / 场景 / Prefab 或文件夹）
3. 点扫描
4. 结果列表勾选要处理的条目
5. 点"修复"或"清理"按钮
6. 查看 Console 日志和弹窗报告

## 参数
| 子模块 | 关键参数 |
|---|---|
| NullMat | 扫描深度、材质槽位过滤 |
| Residual | 是否扫 Timeline、Missing Script、孤立组件 |
| ScaleFix | 根据父 scale 自动补偿粒子 size |
| EmptyPS | 是否自动关闭全空 SubEmitters |
| TexCheck | 非4倍数规则、最大边像素阈值、最大文件 KB |

## 产出
- 删除/修改 GameObject 组件
- 删除空粒子系统
- 重新导入贴图（改 TextureImporter 设置）
- 导出检查报告到 Console/剪贴板/txt

## 关联
- 辅助文件：`Moshi_AssetClean_NullMat.cs`、`_Residual.cs`、`_ScaleFix.cs`、`_EmptyPS.cs`、`_TexCheck.cs`、`_TexCompressWindow.cs`
- 依赖 `UnityEngine.Timeline` / `Playables`
- 集成 `MoshiHelpButton`

## 常见问题
- **Q**：TexCheck 修复后贴图变模糊？**A**：强制 4 倍数会缩放到最近的 4 的倍数。若贴图精度关键，改用"压缩窗口"而不是自动修复。
- **Q**：清空粒子系统会不会误杀？**A**：判定标准是 Renderer 关 + 所有 SubEmitter 引用为空，一般不会误杀可见粒子。保守起见先 Ctrl+Z 测试一次。

---

# Moshi_BatchRename · 批量重命名

## 身份
- **类名**：`Moshi_BatchRename : EditorWindow`
- **菜单**：`工具/Moshi/批量重命名`
- **窗口标题**：`"批量重命名"`
- **功能域**：资源管理
- **源码**：`AllToolsResources/Moshi_BatchRename.cs`（单文件）

## 一句话
批量重命名 Project 资源和 Hierarchy 对象，支持查找替换、前后缀、序号占位。

## 能力
- 双标签：Project（资源） / Hierarchy（场景对象）
- 两种模式：Modify（查找+替换+前后缀） / Direct（头-中-尾分段重写）
- 来源：选中 / 多文件夹（可递归）
- 排序：自然（1,2,..,10）/ 名称 / 层级
- 序号占位符 `{#}`：起始值 / 步长 / 位数
- 类型过滤（Texture / Prefab / Material …）
- 可选包含层级子物体
- 实时预览 + 操作日志

## 流程
1. 打开工具 → 选 Project 或 Hierarchy
2. 选来源（选中 or 拖入文件夹）
3. 选模式（Modify 或 Direct）
4. 填查找/替换/前后缀，或插 `{#}` 占位符
5. 对照预览 → 点应用

## 参数
| 参数 | 说明 |
|---|---|
| 来源 | 选中 / 文件夹 |
| 递归 | 是否含子目录 |
| 模式 | Modify / Direct |
| 查找、替换、前缀、后缀 | string |
| 序号参数 | 起始 / 步长 / 位数 |
| 排序 | 自然 / 名称 / 层级 |
| 类型过滤 | `t:TypeName` |

## 产出
- `AssetDatabase.RenameAsset` 保留 GUID
- GameObject.name 直接赋值
- 支持 Undo

## 关联
- 无辅助文件、无配置
- 集成 `MoshiHelpButton`

## 常见问题
- **Q**：对象改名后动画断裂？**A**：用 `Moshi_AnimPath` 同步修复动画路径。
- **Q**：序号位数怎么设？**A**：起始 1、步长 1、位数 3 → `001/002/003`。

---

# Moshi_ClipHelper · 动画片段助手

## 身份
- **类名**：`Moshi_ClipHelper : EditorWindow`
- **菜单**：`工具/Moshi/动画片段助手`
- **窗口标题**：`"动画片段助手"`
- **功能域**：动画编辑
- **源码**：`AllToolsResources/Moshi_ClipHelper/Moshi_ClipHelper.cs`（单文件 ~188KB）

## 一句话
AnimationClip 与粒子系统的关键帧/曲线综合辅助工具。

## 能力
- **关键帧操作**：批量插/删/偏移关键帧，三种偏移模式（递增/统一/均匀分布）
- **曲线优化**：**Douglas-Peucker 智能压缩**，保留特征点
- **水平翻转**：时间反向（动画倒放）
- **垂直翻转**：值取反（朝向/方向翻转）
- **曲线转换**：位移曲线 ↔ 速度曲线（累积速度/瞬时速度/平均速度三种算法）
- 曲线源：AnimationClip 或 ParticleSystem 均可
- 已有曲线策略：覆盖 / 合并 / 跳过
- 翻转范围：全曲线 / Animation 窗口选中 / 单属性
- 兼容 Humanoid 与 Generic，Unity 2021.3+

## 流程
1. 打开工具 → 选标签（关键帧 / 曲线优化 / 曲线转换）
2. **曲线优化**：拖入 AnimationClip → 调精度（ε） → 预览压缩前后对比 → 应用
3. **翻转**：拖入 Clip → 选水平/垂直 → 选范围 → 应用
4. **曲线转换**：选位移→速度，选算法（累积/瞬时/平均）→ 应用

## 参数
| 参数 | 说明 |
|---|---|
| AnimationClip | 目标动画 |
| 偏移模式 | 递增 / 统一 / 均匀分布 |
| DP epsilon | 压缩容差（越大压得越狠） |
| 翻转范围 | 全曲线 / 窗口选中 / 单属性 |
| 已有曲线处理 | 覆盖 / 合并 / 跳过 |
| 速度算法 | 累积 / 瞬时 / 平均 |

## 产出
- 修改 AnimationClip 的 EditorCurveBinding 和 AnimationCurve
- 修改 ParticleSystem 的 MinMaxCurve

## 关联
- 集成 `MoshiHelpButton`
- 依赖 `System.Reflection`（访问 Animation 窗口内部 API）

## 常见问题
- **Q**：优化后动画变卡？**A**：DP epsilon 太大，减小试试。
- **Q**：翻转后手脚反向了？**A**：Humanoid 的 Muscle 空间翻转语义不同，选"单属性"范围手动处理。

---

# Moshi_Exporter · 工具箱导出

## 身份
- **类名**：`Moshi_Exporter : EditorWindow`
- **菜单**：`工具/Moshi/工具箱导出`
- **窗口标题**：`"工具箱导出"`
- **功能域**：工具箱辅助
- **源码**：`AllToolsResources/Moshi_Exporter/Moshi_Exporter.cs`

## 一句话
一键把 Moshi 工具箱打包成 ZIP 或 UnityPackage，方便跨项目迁移。

## 能力
- 两种格式：ZIP（`System.IO.Compression`） / UnityPackage
- 两种范围：全部 / 只导特定工具
- 可选 Editor / Runtime / .meta 是否包含
- 自动扫描 `Assets/Editor/ToolsIntegrationPanel` 和 `Assets/Scripts/MoshiScripts`
- 可剔除个人数据（配置 .asset）
- 文件预览：条目数 + 总大小

## 流程
1. 打开工具
2. 选格式（ZIP / UnityPackage）
3. 选范围（全部 / 勾选工具）
4. 勾 Editor / Runtime / meta 选项
5. 点导出 → 选保存路径

## 参数
| 参数 | 说明 |
|---|---|
| 格式 | ZIP / UnityPackage |
| 工具范围 | 全部 / 勾选列表 |
| 包含 Editor | bool |
| 包含 Runtime | bool |
| 包含 meta | bool |
| 剔除个人配置 | 不导 .asset |

## 产出
- `.zip` 文件（跨引擎可用）
- `.unitypackage` 文件（Unity 导入）

## 关联
- 依赖 `System.IO.Compression` / `AssetDatabase.ExportPackage`

## 常见问题
- **Q**：导出后在别的项目报 Missing Script？**A**：勾上 meta 文件、保证目标项目有相同依赖（Timeline / Spine 等）。

---

# Moshi_Favorites · 资源收藏夹

## 身份
- **类名**：`Moshi_Favorites : EditorWindow`
- **菜单**：`工具/Moshi/资源收藏夹`（`priority = 520`）
- **窗口标题**：`"资源收藏夹"`
- **功能域**：资源管理
- **源码**：`AllToolsResources/Moshi_Favorites.cs` + `Moshi_FavoritesConfig.cs`

## 一句话
收藏 Project 资源和 Hierarchy 对象，跨场景快速定位。

## 能力
- 左右分栏：Project 资源 / Hierarchy 对象
- 拖入添加、双击编辑显示名/备注、搜索过滤
- 18 种资源类型下拉过滤（AnimationClip / AudioClip / Material / Mesh / Prefab / Sprite / Texture …）
- 行拖拽排序、幽灵行视觉反馈、可拖出到 Project 窗口
- Hierarchy 条目记录 `scenePath + gameObjectPath`，跨场景回定位
- 图标/路径缓存、isDirty 延迟保存

## 流程
1. 打开工具
2. 拖入资产/对象 → 自动添加到对应列表
3. 双击条目改显示名/备注
4. 点击条目 → 自动在 Project 高亮 或 打开场景并定位 GameObject

## 参数
| 参数 | 说明 |
|---|---|
| 类型过滤 | 18 种 UnityEngine.Object 子类 |
| 搜索 | 按名/路径模糊匹配 |

## 产出
- 写入 `Moshi_FavoritesConfig.asset`
- 不修改任何原资产

## 关联
- 配置 asset：`AllToolsResources/Moshi_FavoritesConfig.asset`（首次自动创建）
- 辅助类：`FavoriteAssetEntry`、`HierarchyFavoriteEntry`
- 依赖 `UnityEditor.IMGUI.Controls.SearchField`、`UnityEditor.SceneManagement`
- 集成 `MoshiHelpButton`

## 常见问题
- **Q**：换电脑/项目路径变了，收藏失效？**A**：Hierarchy 收藏基于 scene/path，场景改动或对象改名后可能失效；Project 收藏基于 GUID 跟随不变。

---

# Moshi_GOArray · 物体阵列工具

## 身份
- **类名**：`Moshi_GOArray : EditorWindow`
- **菜单**：`工具/Moshi/物体阵列工具`
- **窗口标题**：`"物体阵列工具"`
- **功能域**：生成工具
- **源码**：`AllToolsResources/Moshi_GOArray/Moshi_GOArray.cs`

## 一句话
按 6 种布局批量阵列/复制物体，支持路径与随机扰动。

## 能力
- 6 种布局：**线性、网格 2D、3D 矩阵、环形、螺旋、路径**
- 2 种模式：**重排**（对现有子物体） / **复制**（基于源对象生成）
- 随机偏移（位置 / 旋转 / 缩放）
- 增量变换（递增旋转 / 递增缩放）
- 环形/螺旋可选朝向中心
- SceneView 实时预览

## 流程
1. 打开工具
2. 指定源物体（复制模式）或父物体（重排模式）
3. 选布局（线性/网格/环形/螺旋/路径）
4. 设置参数（数量、间距、半径、圈数、路径点列表）
5. 可选随机扰动 / 增量变换
6. SceneView 看预览 → 点"应用"

## 参数
| 布局 | 关键参数 |
|---|---|
| 线性 | 方向、间距、数量 |
| 网格 2D | 行列数、xz 间距 |
| 3D 矩阵 | xyz 各维数量和间距 |
| 环形 | 半径、数量、起始角、朝向中心 |
| 螺旋 | 半径、步长、圈数、朝向中心 |
| 路径 | Transform 点列表、采样密度 |

## 产出
- 复制模式：生成新 GameObject 群
- 重排模式：调整现有子物体的 `localPosition/Rotation/Scale`
- 支持 Undo

## 关联
- 集成 `MoshiHelpButton`
- 无配置 asset

## 常见问题
- **Q**：路径阵列要怎么指定路径？**A**：在 Hierarchy 里摆几个空物体作为路径锚点，按顺序拖入工具的点列表。

---

# Moshi_ImgClassifier · 图片分类归档

## 身份
- **类名**：`Moshi_ImgClassifier : EditorWindow`
- **菜单**：`工具/Moshi/图片分类归档`
- **窗口标题**：`"图片分类归档"`
- **功能域**：资源管理
- **源码**：`AllToolsResources/Moshi_ImgClassifier/Moshi_ImgClassifier.cs`（~340+ 行）

## 一句话
基于视觉指纹的图片自动分类归档 + 重复查清。

## 能力
- 计算每张图的**感知哈希 / 像素指纹**
- 按相似度聚类自动分组
- 重复/近似图检测（阈值可调）
- 批量按类别移动到目标子目录
- 支持 Texture / Sprite / 常见格式（png/jpg/tga/psd）
- 预览缩略图 + 组间对比

## 流程
1. 打开工具
2. 拖入要扫描的文件夹
3. 设置相似度阈值（0.0~1.0）
4. 点"扫描" → 等待指纹计算
5. 查看分组结果，勾选要归档的组
6. 指定目标根目录 → 点"执行归档"

## 参数
| 参数 | 说明 |
|---|---|
| 源目录 | 要扫描的图片根目录 |
| 相似阈值 | 聚类容差 |
| 包含格式 | png / jpg / tga / psd … |
| 目标根目录 | 归档输出位置 |

## 产出
- 批量 `AssetDatabase.MoveAsset` 把图片迁到分类子文件夹
- 生成归档报告

## 关联
- 集成 `MoshiHelpButton`
- 无依赖外部插件

## 常见问题
- **Q**：扫描很慢？**A**：图片数量×分辨率决定指纹耗时，先缩小扫描范围或降低采样精度。

---

# Moshi_ImgToQuad · 图片转面片

## 身份
- **类名**：`Moshi_ImgToQuad : EditorWindow`
- **菜单**：`工具/Moshi/图片转面片`
- **窗口标题**：`"图片转面片"`
- **功能域**：图片处理
- **源码**：`AllToolsResources/Moshi_ImgToQuad.cs`

## 一句话
图片/PSD 批量生成 Quad / SpriteRenderer / ParticleSystem，保留位置。

## 能力
- 解析 PSD 文件提取图层位置、尺寸、画布尺寸、导出图层贴图
- 三种坐标原点：Center / TopLeft / BottomLeft
- 三种尺寸基准：像素比例 / 宽 1 / 高 1
- 三种生成模式：**Quad / SpriteRenderer / ParticleSystem**（后者可套 Preset）
- 2D `RectTransform` 与 3D `Transform` 切换
- SortingLayer / Order 设置
- 批量生成材质球（自定义 Shader / 贴图属性名 / 材质输出目录）
- 拖拽导入 / 预览列表 / 全选反选

## 流程
1. Hierarchy 选一个物体作为父节点
2. 打开工具 → 拖入 PSD 或多张图片
3. 选坐标原点、尺寸基准、生成模式
4. （可选）指定粒子 Preset、Shader、材质输出目录
5. 勾选要生成的图层 → 点"生成"
6. 父节点下出现 `fx_quad_group` 子节点包含全部生成物体

## 参数
| 参数 | 说明 |
|---|---|
| targetObject | 父节点（Hierarchy 选中） |
| 输入 | PSD 文件 或 Texture 列表 |
| 坐标原点 | Center / TopLeft / BottomLeft |
| 尺寸基准 | 像素比例 / 宽 1 / 高 1 |
| 生成模式 | Quad / Sprite / Particle |
| 父组名 | 默认 `fx_quad_group` |
| Preset | ParticleSystem Preset |
| Shader / 贴图属性名 | 生成材质时使用 |

## 产出
- 父节点下 `fx_quad_group/` 子树
- 写出 `.mat` 到指定目录
- 若 PSD 有多图层，导出每层的单图贴图

## 关联
- 依赖 `UnityEditor.Presets`、`UnityEngine.UI`、`System.Xml`、`System.Reflection`
- 集成 `MoshiHelpButton`
- 姊妹工具：`Moshi_SvgImport`（SVG 版）

## 常见问题
- **Q**：PSD 解析失败？**A**：确认 PSD 不含 16bit 色深、不含智能对象嵌套。
- **Q**：生成的材质 Shader 找不到？**A**：默认尝试 `Pandavfx/Pandavfx_v2.3`，无则回退 `Unlit/Transparent`，可在工具里手动指定。

---

# Moshi_ParticleBatch · 粒子批量修改

## 身份
- **类名**：`Moshi_ParticleBatch : EditorWindow`
- **菜单**：`工具/Moshi/粒子批量修改`
- **窗口标题**：`"粒子批量修改"`
- **功能域**：粒子系统
- **源码**：`AllToolsResources/Moshi_ParticleBatch.cs`

## 一句话
一键批量设置所选粒子系统的核心参数。

## 能力
- **Duration = StartLifetime**：一键对齐
- **关 Prewarm**：批量关掉预热
- **设最大粒子数**：统一上限
- **设置 RingBufferMode**：循环队列模式
- **清空发射率**：Rate over Time 归零
- **修改 Burst 次数**
- **修改 StartLifetime 常量**
- **启用 Color over Lifetime（Gradient）**
- 递归处理选中对象的**所有子级粒子系统**
- 支持 Undo

## 流程
1. Hierarchy 选中一个或多个带粒子系统的对象
2. 打开工具
3. 勾选要执行的操作
4. 填对应参数
5. 点"应用"

## 参数
| 操作 | 参数 |
|---|---|
| 最大粒子数 | int |
| StartLifetime | float |
| Burst 次数 | int |
| Gradient | Gradient |
| RingBuffer 模式 | enum |

## 产出
- 直接修改选中对象及所有子级的 `ParticleSystem` 组件
- 完整 Undo

## 关联
- 集成 `MoshiHelpButton`
- 无辅助文件

## 常见问题
- **Q**：忘勾某项但已应用？**A**：`Ctrl+Z` 全量撤销后重来。
- **Q**：Burst 只改次数不改参数？**A**：是。改 Burst 数量（count）或其它 Burst 细节请直接在 ParticleSystem Inspector 操作。

---

# Moshi_ParticleMesh · 粒子Mesh工具

## 身份
- **类名**：`ParticleMeshToolWindow : EditorWindow`
- **菜单**：`工具/Moshi/粒子Mesh工具`
- **窗口标题**：见 `TOOL_NAME` 常量
- **功能域**：粒子系统
- **源码**：`AllToolsResources/Moshi_ParticleMesh/`（10 个 .cs）

## 一句话
粒子 Mesh 的分析、预览、替换、材质复制一体化。

## 能力
- 扫描选中/场景/文件夹的粒子系统 Mesh 使用情况
- Mesh 缩略图预览（自带渲染器）
- 批量替换 Mesh（同类型 Renderer 统一）
- 材质复制（拷贝整套材质到目标粒子）
- 分析异常（缺失 Mesh / SubMesh 索引越界）

## 流程
1. 打开工具
2. 选扫描范围 → 扫描
3. 结果列表查看每个粒子使用的 Mesh + 缩略图
4. 选中条目 → 拖入新 Mesh → 点"替换"
5. 或右键条目 → 复制材质组

## 参数
| 参数 | 说明 |
|---|---|
| 扫描范围 | 选中 / 场景 / 文件夹 |
| 过滤 | 有无 Mesh / 特定 Mesh |

## 产出
- 修改 `ParticleSystemRenderer.mesh`
- 修改 `Renderer.sharedMaterials`
- 支持 Undo

## 关联
- 10 个辅助 .cs 文件（UI/预览/批处理/数据）
- 集成 `MoshiHelpButton`

## 常见问题
- **Q**：缩略图加载不出来？**A**：Unity 的 PreviewRenderUtility 在某些 Unity 版本首次调用有 GC，等待 1~2 秒再看。

---

# Moshi_ParticleOrder · 粒子排序修改

## 身份
- **类名**：`Moshi_ParticleOrder : EditorWindow`
- **菜单**：`工具/Moshi/粒子排序修改`
- **窗口标题**：`"粒子排序修改"`
- **功能域**：粒子系统
- **源码**：`AllToolsResources/Moshi_ParticleOrder.cs`

## 一句话
增量修改选中粒子的渲染排序/时间延迟/生命周期。

## 能力
- 四个增量参数：**Order in Layer、Sorting Fudge、Start Delay、Start Lifetime**
- 三种模式：
  - **统一偏移**：所有对象加同一个值
  - **递增排序**：按顺序 +1/+2/+3... （步长可调）
  - **递减排序**：按顺序 -1/-2/-3...
- 预览列表显示每个对象 current → new 值对比
- 基于**原值增量**（不是覆盖）

## 流程
1. 选中一批粒子对象
2. 打开工具
3. 选要改的参数（Order / Fudge / Delay / Lifetime）
4. 选模式（统一/递增/递减）+ 步长
5. 查看预览 → 点"应用"

## 参数
| 参数 | 说明 |
|---|---|
| 目标参数 | Order / Fudge / Delay / Lifetime |
| 模式 | 统一 / 递增 / 递减 |
| 偏移量 / 步长 | int 或 float |

## 产出
- 修改 `ParticleSystemRenderer.sortingOrder` / `sortingFudge`
- 修改 `ParticleSystem.main.startDelay` / `startLifetime`
- 支持 Undo

## 关联
- 集成 `MoshiHelpButton`

## 常见问题
- **Q**：Order 和 Fudge 什么区别？**A**：Order 是整数层级，Fudge 是浮点微调（同 Order 下细分先后）。
- **Q**：递增是按选中顺序还是按层级？**A**：按选中的 Selection 顺序。

---

# Moshi_PathTool · 路径工具

## 身份
- **类名**：`PathImporterWindow` + `PathAnimationBaker`（两个 EditorWindow）
- **菜单**：
  - `工具/Moshi/路径工具/路径导入`
  - `工具/Moshi/路径工具/路径烘焙`
- **功能域**：生成工具
- **源码**：`AllToolsResources/Moshi_PathTool/`（多文件）

## 一句话
3ds Max 路径导入 + 路径动画烘焙，两个子工具组合。

## 能力

### 路径导入
- 从 3ds Max 导出的路径文件读取点列表
- 在场景中还原为一串 Transform 锚点
- 可指定父节点和命名前缀

### 路径烘焙
- 选定一条路径（Transform 序列）
- 让目标物体沿路径运动
- 烘焙为 AnimationClip 关键帧
- 支持循环/一次、速度、朝向设置

## 流程
1. 3ds Max 导出路径 → 拿到数据文件
2. 菜单 `路径导入` → 拖入文件 → 指定父节点 → 生成锚点
3. 菜单 `路径烘焙` → 指定目标物体 + 路径锚点根 → 设置参数 → 烘焙
4. 得到 AnimationClip，可直接用于 Animator/Timeline

## 参数
| 参数 | 说明 |
|---|---|
| 路径文件 | Max 导出文件 |
| 父节点 | 锚点生成到哪里 |
| 烘焙目标 | 沿路径运动的对象 |
| 速度 / 循环 | 烘焙动画属性 |
| 朝向 | 是否对齐切线方向 |

## 产出
- 生成 Transform 锚点层级
- 生成 `.anim` 文件

## 关联
- 辅助文件：多个（Importer / Baker / BezierCurve 等）
- 关联运行时：`Scripts/MoshiScripts/Moshi_PathTool/`（PathCreator / PathFollower / BezierCurve）
- 集成 `MoshiHelpButton`

## 常见问题
- **Q**：Max 路径数据格式？**A**：项目约定格式（见 `Documentation/Moshi_PathTool.md` 详细说明）。
- **Q**：烘焙动画会不会抖动？**A**：采样密度过高时容易抖，降低采样率或用 `Moshi_ClipHelper` DP 优化。

---

# Moshi_PivotAlign · 轴心对齐

## 身份
- **类名**：`Moshi_PivotAlign`（静态类）
- **菜单（7 个右键子项）**：
  - `GameObject/Moshi/原点对齐/移到底部` `/顶部` `/中心` `/左侧` `/右侧` `/前方` `/后方`
- **窗口标题**：无 EditorWindow
- **功能域**：生成工具
- **源码**：`AllToolsResources/Moshi_PivotAlign/Moshi_PivotAlign.cs`

## 一句话
Hierarchy 右键一键把物体轴心对齐到 Mesh 包围盒的某个边/面。

## 能力
- 7 个子菜单：底部、顶部、中心、左侧、右侧、前方、后方
- 根据 MeshRenderer / SkinnedMeshRenderer 的 Bounds 计算
- 修改物体位置使 Mesh 顶点相对位置偏移，等效移轴心
- 支持选中多物体批处理
- 完整 Undo

## 流程
1. Hierarchy 选中物体 → 右键
2. `Moshi/原点对齐/移到[底部/顶部/...]`
3. 即时生效，可 Ctrl+Z

## 参数
- 无可调参数（固定语义）

## 产出
- 修改 Transform 位置，同时把子物体反向偏移补偿，实现"视觉不变、轴心换位"的效果
- 支持 Undo

## 关联
- 集成到 Hierarchy 右键菜单（不在工具箱面板里）
- 无配置

## 常见问题
- **Q**：没 MeshRenderer 的物体怎么办？**A**：本工具依赖 Bounds，没有 Renderer 会弹提示。可考虑手动调 Transform。

---

# Moshi_RefCounter · 引用计数统计

## 身份
- **类名**：`Moshi_RefCounter : EditorWindow`
- **菜单**：`工具/Moshi/引用计数统计`
- **窗口标题**：`"引用计数统计"`
- **功能域**：资源管理
- **源码**：`AllToolsResources/Moshi_RefCounter.cs`

## 一句话
资源依赖整合 + 零引用检测 + 清理的一体化流水线。

## 能力
- 6 大资源分组开关（动画 / 贴图材质 / 模型 / 音视频 / 预制体 / 其他）
- 白名单文件夹（作为引用来源但不参与零引用清理），持久化到 `EditorPrefs`
- 扫描 GUID 引用（正则 `guid:([a-f0-9]{32})`）
- 识别 `.prefab / .unity / .mat / .asset / .controller / .anim / .overrideController / .playable`
- **依赖整合**：把外部依赖复制到目标文件夹，按类型分文件夹
- 自动收集二级文件夹、内外白名单依赖分类展示
- **🚀 一键资源整理流水线**：扫描 → 整合 → 检测 → 报告
- 零引用资源列表勾选清理
- 生成 `OrganizationReport`

## 流程
1. 打开工具
2. 设置目标文件夹 + 白名单文件夹
3. 勾 6 大资源分组
4. 点 🚀 一键整理（或单步：扫描 → 整合 → 清理）
5. 查看报告 → 勾选零引用条目 → 删除

## 参数
| 参数 | 说明 |
|---|---|
| 目标文件夹 | DefaultAsset 根目录 |
| 白名单 | 多个 DefaultAsset 文件夹 |
| 资源分组开关 | 6 个类别 |

## 产出
- 移动/复制资产到整合目录
- 删除零引用资源
- 日志列表（上限 200 条）
- 清理报告

## 关联
- EditorPrefs 键：
  - `TextureMaterialReferenceCounter_WhitelistFolders`
  - `TextureMaterialReferenceCounter_CustomFolderNames`
- 依赖：`AssetDatabase`、`UNITY_TIMELINE`（可选）、U2D `SpriteAtlas`、Video
- 集成 `MoshiHelpButton`
- 姊妹设计文档：`Moshi_RefCounter_图片查重设计方案.md`

## 常见问题
- **Q**：零引用判定会不会误杀 Resources 里的资源？**A**：`Resources/` 下资源通过运行时字符串加载，GUID 扫描会认为它们零引用。务必把 `Resources/` 加白名单。
- **Q**：Addressable 的资源算引用吗？**A**：Addressable group asset (`.asset`) 会被扫描为引用源。但如果代码里用 string key 加载，那也漏不了（GUID 在 group asset 里）。

---

# Moshi_ReverseOrder · 反向排序

## 身份
- **类名**：`Moshi_ReverseOrder`（静态类）
- **菜单（3 个右键子项）**：
  - `GameObject/Moshi/反转排序/反转子物体`
  - `GameObject/Moshi/反转排序/反转选中同级`
  - `GameObject/Moshi/反转排序/智能反转`
- **窗口标题**：无 EditorWindow
- **功能域**：生成工具 / 层级工具
- **源码**：`AllToolsResources/Moshi_ReverseOrder/Moshi_ReverseOrder.cs`

## 一句话
Hierarchy 右键一键反转子物体或同级物体的排列顺序。

## 能力
- **反转子物体**：反转当前选中物体的所有子物体顺序
- **反转选中同级**：只反转多选物体的相对顺序（不影响未选中兄弟）
- **智能反转**：自动判断（单选→反转子物体，多选→反转同级）
- 帧内去重：`[MenuItem("GameObject/...")]` 会被 Unity 对每个选中物体逐一调用，本工具用 `EditorApplication.delayCall + 帧内去重标记` 保证只执行一次
- 完整 Undo，组名 `Moshi 反转子物体`

## 流程
1. Hierarchy 选中物体
2. 右键 `Moshi/反转排序/[模式]`
3. 即时生效

## 参数
- 无可调参数

## 产出
- 修改 Hierarchy 中物体的 `SiblingIndex`
- 支持 Undo

## 关联
- 集成到 Hierarchy 右键（不在工具箱面板里）
- 无配置

## 常见问题
- **Q**：多个对象选中但只有一个被反转？**A**：Unity 机制问题，本工具已用 `delayCall` 去重处理；如仍异常请报 bug。

---

# Moshi_SpineBake · 骨骼动画烘焙

## 身份
- **类名**：`Moshi_SpineBake : EditorWindow`
- **菜单**：`工具/Moshi/骨骼动画烘焙`
- **窗口标题**：见 `TOOL_NAME`
- **功能域**：动画编辑
- **源码**：`AllToolsResources/Moshi_SpineBake/Moshi_SpineBake.cs`

## 一句话
把 Spine 骨骼动画烘焙为 Unity AnimationClip，方便 Animator/Timeline 使用。

## 能力
- 扫描 Spine SkeletonDataAsset 里的所有动画
- 选择要烘焙的动画列表
- 配置采样率（FPS）
- 选择生成 Clip 的输出路径
- 保留循环设置
- 烘焙 Bone 曲线 + Slot 状态 + 颜色属性

## 流程
1. 打开工具
2. 拖入 Spine SkeletonDataAsset
3. 勾选要烘焙的动画
4. 设置采样率（30 / 60）
5. 设置输出目录
6. 点烘焙 → 生成 `.anim` 文件

## 参数
| 参数 | 说明 |
|---|---|
| SkeletonDataAsset | Spine 数据资产 |
| 动画列表 | 多选勾选 |
| 采样率 | int FPS |
| 输出目录 | 文件夹路径 |
| Loop | 循环标志 |

## 产出
- 生成 `.anim` 文件到指定目录
- 每个 Spine 动画一份 Clip

## 关联
- 依赖 Spine Runtime（`Plugins/Spine/`）
- 集成 `MoshiHelpButton`

## 常见问题
- **Q**：烘焙后角色变形异常？**A**：Spine 的 IK/MeshDeform 在 Unity AnimationClip 中难完美还原。如果需要 IK 必须用 Spine 自己的 SkeletonAnimation。
- **Q**：采样率设多少合适？**A**：30 通常够用，高帧率 Spine 动画可选 60；过高导致 Clip 文件很大。

---

# Moshi_SvgImport · SVG位置导入

## 身份
- **类名**：`Moshi_SvgImport : EditorWindow`
- **菜单**：`工具/Moshi/SVG位置导入`
- **窗口标题**：`"SVG位置导入"`
- **功能域**：图片处理
- **源码**：`AllToolsResources/Moshi_SvgImport.cs`

## 一句话
解析 SVG 文件批量生成带坐标的 Quad / Sprite / ParticleSystem。

## 能力
- XML 解析 SVG，提取元素 id / tag / x / y / w / h / fill / stroke / path.d
- 三贴图匹配模式：按 id 匹配 / 按目录查找 / 手动指定
- 三生成模式：**Quad / SpriteRenderer / ParticleSystem**（可套 Preset）
- Transform3D / RectTransform2D 切换
- 三种坐标原点（Center / TopLeft / BottomLeft） + 三种尺寸基准
- 可将 SVG 元素导出为位图（默认 `Assets/Textures/SVG_Images`）
- 多 SVG 文件并列管理 / 元素清单勾选 / 拖拽导入
- 默认 Shader 尝试 `Pandavfx/Pandavfx_v2.3`，回退 `Unlit/Transparent`

## 流程
1. Hierarchy 选父节点
2. 打开工具 → 拖入 SVG 文件
3. 选坐标原点、尺寸基准、生成模式
4. 选贴图匹配模式（自动/目录/手动）
5. 勾选要生成的元素 → 点"生成"
6. 父节点下出现 `fx_svg_group` 子树

## 参数
| 参数 | 说明 |
|---|---|
| targetObject | 父节点 |
| SVG 文件列表 | 多文件 |
| 坐标原点 / 尺寸基准 | 同 `Moshi_ImgToQuad` |
| 生成模式 | Quad / Sprite / Particle |
| 贴图匹配 | id / 目录 / 手动 |
| 输出位图目录 | string |

## 产出
- 父节点下 `fx_svg_group/` 子树
- 可选导出 SVG 元素为 PNG
- 生成材质 `.mat`

## 关联
- 依赖 `UnityEditor.Presets`、`System.Xml`、`System.Reflection`、`System.Text.RegularExpressions`
- 集成 `MoshiHelpButton`
- 姊妹工具：`Moshi_ImgToQuad`

## 常见问题
- **Q**：SVG 贴图找不到怎么办？**A**：切到"手动"模式每个元素指定贴图，或启用"导出位图"由工具自己光栅化。
- **Q**：复杂 path 元素（`<path d="..."/>`）能精确还原吗？**A**：本工具只取 bounding box 生成面片，path 内部路径不能完美还原；需要精确矢量请用 UnityVectorGraphics 包。

---

# Moshi_TextureTool · 图片处理工具

## 身份
- **类名**：`Moshi_TextureTool : EditorWindow`
- **菜单**：`工具/Moshi/图片处理工具`
- **窗口标题**：`"图片处理工具"`
- **功能域**：图片处理
- **源码**：`AllToolsResources/Moshi_TextureTool/`（12 .cs + 1 .compute）

## 一句话
通用图像处理集：通道/去黑/UnMult/尺寸/色条/材质渲染/极坐标。

## 能力
- **拆分/合并**：RGBA 通道拆分/合并
- **通道转换**：RGBA 各通道重映射
- **去黑**（Remove Black）：去除黑底
- **加黑**（Add Black）：反向
- **UnMult**：去预乘 Alpha
- **尺寸调整**：批量 Resize
- **色条生成**：调色板贴图
- **材质渲染**：把材质烘到贴图
- **极坐标变换**：笛卡尔 ↔ 极坐标
- 使用 ComputeShader 加速

## 流程
1. 打开工具 → 选子功能标签
2. 拖入输入贴图
3. 设置对应参数
4. 点"处理" → 查看预览
5. 点"保存"输出到指定路径

## 参数
- 每个子功能独立参数面板，参见 `Documentation/Moshi_TextureTool.md`

## 产出
- 新贴图 `.png` / `.tga`
- 可覆盖原文件或另存

## 关联
- 12 个辅助 .cs（每个子功能一份）
- ComputeShader：`.compute` 文件
- 集成 `MoshiHelpButton`
- 大多数子功能依赖 GPU ComputeShader

## 常见问题
- **Q**：ComputeShader 在某平台报错？**A**：确认目标平台支持 Compute（移动端 GLES2 不支持）。编辑器预览在 DX11/Metal 下稳定。

---

# Moshi_TLBatch · TL批量导入

## 身份
- **类名**：`Moshi_TLBatch : EditorWindow`
- **菜单**：`工具/Moshi/TL批量导入`
- **窗口标题**：`"TL批量导入"` 或 `TOOL_NAME`
- **功能域**：Timeline
- **源码**：`AllToolsResources/Moshi_TLBatch/Moshi_TLBatch.cs`

## 一句话
Timeline Clip 批量导入，4 种排列模式。

## 能力
- 从 Project 拖入一批 AnimationClip / AudioClip / 其他 Track 支持类型
- 4 种排列模式：
  - **顺序连接**（首尾相接）
  - **对齐起点**（全部从 t=0 开始）
  - **等间距**（自定义间隔）
  - **自定义偏移**
- 自动创建对应 Track
- 可指定起始时间、缩放系数

## 流程
1. 打开 Timeline 窗口，确保有 PlayableDirector
2. 打开本工具 → 指定目标 Timeline
3. 拖入 Clip 列表
4. 选排列模式 + 参数
5. 点"导入"

## 参数
| 参数 | 说明 |
|---|---|
| 目标 Timeline | TimelineAsset 引用 |
| Clip 列表 | 多个 Clip 资产 |
| 排列模式 | 顺序/对齐/等间距/自定义 |
| 起始时间 | double |
| 间隔 | double（等间距模式） |

## 产出
- 在 Timeline 中自动创建 Track 和 Clip

## 关联
- 依赖 `UnityEngine.Timeline`
- 集成 `MoshiHelpButton`

## 常见问题
- **Q**：Clip 类型不匹配 Track？**A**：工具按 Clip 类型自动选对应 TrackAsset，如果仍报错检查 Timeline 版本。

---

# Moshi_TLEnhance · Timeline增强

## 身份
- **类名**：`Moshi_TLEnhance`（静态类 + `[InitializeOnLoad]`）
- **菜单**：无
- **快捷键（4 个 `[Shortcut]`）**：
  - `[` — Timeline Clip 入点移到播放头
  - `]` — Timeline Clip 出点移到播放头
  - `Alt+[` — 裁剪入点到播放头
  - `Alt+]` — 裁剪出点到播放头
- **功能域**：Timeline
- **源码**：`AllToolsResources/Moshi_TLEnhance/Moshi_TLEnhance.cs`

## 一句话
编辑器加载即生效的 Timeline AE 式快捷键（无窗口）。

## 能力
- 4 个快捷键注入 Timeline 编辑器
- 类似 After Effects 的工作流
- 选中 Clip + 按快捷键即可操作

## 流程
- 无需打开窗口
- 编辑器启动自动生效
- 在 Timeline 选 Clip → 移动播放头 → 按快捷键

## 参数
- 无

## 产出
- 修改 Timeline Clip 的 start / duration

## 关联
- 依赖 `UnityEditor.ShortcutManagement`
- `[InitializeOnLoad]` 自加载

## 常见问题
- **Q**：快捷键失效？**A**：Timeline 窗口没聚焦时快捷键不响应。点一下 Timeline 窗口激活。
- **Q**：想改快捷键？**A**：Unity 菜单 `Edit > Shortcuts` 里搜 `Timeline Clip` 找到本工具注册的 4 个。

---

# Moshi_TLVideo · Timeline视频

## 身份
- **类名**：`Moshi_TLVideo : EditorWindow`
- **菜单**：`工具/Moshi/Timeline视频`
- **窗口标题**：`"Timeline视频"`
- **功能域**：Timeline
- **源码**：`AllToolsResources/Moshi_TLVideo/Moshi_TLVideo.cs`

## 一句话
一键为 VideoClip 生成 Timeline 播视频所需的全部资源。

## 能力
- 输入 VideoClip → 自动生成：
  - Timeline Asset
  - VideoTrack + VideoPlayableAsset
  - RenderTexture（如需显示到 UI）
  - 预设 PlayableDirector 配置
- 可选目标：世界空间 / UGUI
- 可设置 Loop / AspectRatio

## 流程
1. 打开工具
2. 拖入 VideoClip 列表
3. 选目标类型（World / UGUI）
4. 设置输出目录
5. 点"生成"

## 参数
| 参数 | 说明 |
|---|---|
| VideoClip 列表 | 多文件 |
| 目标 | World / UGUI |
| Loop | bool |
| AspectRatio | enum |
| 输出目录 | string |

## 产出
- `.playable` Timeline 资产
- `.renderTexture` 资产
- 场景内挂好 PlayableDirector 的 GameObject

## 关联
- 依赖 `UnityEngine.Video` / `Timeline`
- 集成 `MoshiHelpButton`

## 常见问题
- **Q**：UI 上视频不显示？**A**：确认 RenderTexture 贴到 RawImage，且 VideoPlayer 的 targetTexture 已指向它。

---

# 特效分析器 / 特效浏览器已迁移

特效分析器与特效浏览器已从 Moshi工具箱独立迁移到 **Moshi特效生成器**，不再作为 `ToolsIntegrationPanel/AllToolsResources/` 下的工具维护。

## 新位置

| 工具 | 新源码位置 | 新菜单 |
|---|---|---|
| 特效分析器 | `Assets/Editor/MoshiVFXGenerator/Modules/Moshi_VFXAnalyzer/` | `工具/Moshi特效生成器/特效分析器` |
| 特效浏览器 | `Assets/Editor/MoshiVFXGenerator/Modules/Moshi_VfxBrowser/` | `工具/Moshi特效生成器/特效浏览器` |
| Moshi特效生成器主入口 | `Assets/Editor/MoshiVFXGenerator/Window/Moshi_VFXGen.cs` | `工具/Moshi特效生成器/打开生成器` |

后续特效自动制作、克隆变体、一键生成、蓝图生成等能力都归属 `Assets/Editor/MoshiVFXGenerator/`。

---

## 尾部：类名与菜单规范对照表

> 这是供 IMA 召回时交叉验证用的索引卡。

| 工具 | 类名 | 菜单路径 |
|---|---|---|
| 批量重命名 | `Moshi_BatchRename` | `工具/Moshi/批量重命名` |
| 资源收藏夹 | `Moshi_Favorites` | `工具/Moshi/资源收藏夹` |
| 引用计数统计 | `Moshi_RefCounter` | `工具/Moshi/引用计数统计` |
| 资源清理 | `Moshi_AssetClean` | `工具/Moshi/资源清理` |
| 图片分类归档 | `Moshi_ImgClassifier` | `工具/Moshi/图片分类归档` |
| 粒子批量修改 | `Moshi_ParticleBatch` | `工具/Moshi/粒子批量修改` |
| 粒子排序修改 | `Moshi_ParticleOrder` | `工具/Moshi/粒子排序修改` |
| 粒子Mesh工具 | `ParticleMeshToolWindow` | `工具/Moshi/粒子Mesh工具` |
| 反向排序 | `Moshi_ReverseOrder` | `GameObject/Moshi/反转排序/*` |
| 动画片段助手 | `Moshi_ClipHelper` | `工具/Moshi/动画片段助手` |
| 动画路径修复 | `Moshi_AnimPath` | `工具/Moshi/动画路径修复` |
| 骨骼动画烘焙 | `Moshi_SpineBake` | `工具/Moshi/骨骼动画烘焙` |
| 图片处理工具 | `Moshi_TextureTool` | `工具/Moshi/图片处理工具` |
| 图片转面片 | `Moshi_ImgToQuad` | `工具/Moshi/图片转面片` |
| SVG位置导入 | `Moshi_SvgImport` | `工具/Moshi/SVG位置导入` |
| 物体阵列工具 | `Moshi_GOArray` | `工具/Moshi/物体阵列工具` |
| 路径工具 | `PathImporterWindow` + `PathAnimationBaker` | `工具/Moshi/路径工具/路径导入`、`/路径烘焙` |
| 轴心对齐 | `Moshi_PivotAlign` | `GameObject/Moshi/原点对齐/*` |
| Timeline增强 | `Moshi_TLEnhance` | 无菜单（`[InitializeOnLoad]` + 快捷键 `[`、`]`、`Alt+[`、`Alt+]`） |
| TL批量导入 | `Moshi_TLBatch` | `工具/Moshi/TL批量导入` |
| Timeline视频 | `Moshi_TLVideo` | `工具/Moshi/Timeline视频` |
| 层级工具集 | `Moshi_AddParent` | `GameObject/Moshi/添加父物体`、`GameObject/Moshi/替换为Prefab` |
| 工具箱导出 | `Moshi_Exporter` | `工具/Moshi/工具箱导出` |

⚠️ 标记项为规范迁移 TODO。
