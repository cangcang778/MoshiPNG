# 上传到 IMA 知识库 · 操作手册

## 一、前置条件（都已完成）

- ✅ `ima-skills` 已安装到 `C:\Users\yejunli\.codebuddy\skills\ima-skills\`
- ✅ 环境变量已永久写入：`IMA_OPENAPI_CLIENTID` / `IMA_OPENAPI_APIKEY`
- ✅ 本目录下已生成三份可上传的知识库文档

## 二、要上传的文档（3 份）

所有文件都在：
```
g:\HLDDZ3D_Artist\HLDDZArtist\Assets\Editor\ToolsIntegrationPanel\Documentation\IMA_KnowledgeBase\
```

| # | 文件名 | 作用 | 大小约 |
|---|---|---|---|
| 1 | `00_Moshi_Toolbox_Index.md` | 总索引、工具矩阵、关键词检索表 | ~9 KB |
| 2 | `01_Framework_ToolsIntegrationPanel.md` | 框架机制、面板/注册/帮助按钮 | ~6 KB |
| 3 | `02_Tools_Catalog.md` | 24 个工具的完整详解目录 | ~48 KB |

---

## 三、重启 CodeBuddy 后的操作流程

### 第 0 步 · 重启 CodeBuddy
**完全关闭**（包括托盘图标）然后重新打开。这步是让环境变量被新进程读到。

### 第 1 步 · 触发 skill 列出可用知识库
在 CodeBuddy 对话框里直接复制发送：

```
调用 ima-skills，列出我所有的 IMA 知识库。
```

CodeBuddy 会自动加载 `ima-skills` 并调用 `search_knowledge_base`（query=""），返回您有权限添加内容的知识库列表。

### 第 2 步 · 创建或选择目标知识库
从列表里选一个现有的，或让 CodeBuddy 帮您建一个。

推荐的知识库名称示例：
- `Moshi 工具箱知识库`
- `HLDDZ Unity 工具文档`
- `Moshi VFX Toolkit`

发送：
```
把以下 3 个文档上传到【Moshi 工具箱知识库】：

1. G:\HLDDZ3D_Artist\HLDDZArtist\Assets\Editor\ToolsIntegrationPanel\Documentation\IMA_KnowledgeBase\00_Moshi_Toolbox_Index.md
2. G:\HLDDZ3D_Artist\HLDDZArtist\Assets\Editor\ToolsIntegrationPanel\Documentation\IMA_KnowledgeBase\01_Framework_ToolsIntegrationPanel.md
3. G:\HLDDZ3D_Artist\HLDDZArtist\Assets\Editor\ToolsIntegrationPanel\Documentation\IMA_KnowledgeBase\02_Tools_Catalog.md

注意：
- 标题保留原文件名不修改
- 如果重名，在末尾加时间戳
- 请按顺序完整跑完 preflight → 重名检查 → create_media → COS 上传 → add_knowledge
```

### 第 3 步 · 验证上传成功
发送：
```
在【Moshi 工具箱知识库】里搜索 "批量重命名"，确认知识库已生效。
```

预期返回：能命中 `00_Moshi_Toolbox_Index.md` 和 `02_Tools_Catalog.md` 两份文件的相关片段。

---

## 四、后续 · 让知识库生效

上传完成后，以后在 CodeBuddy 里问 Moshi 工具相关问题时，明确说：

```
请参考 IMA 知识库【Moshi 工具箱知识库】回答：xxx
```

CodeBuddy 会调用 `search_knowledge` 从您上传的 3 份文档中检索，再作答。

### 常见调用语句示例

- "从 Moshi 工具箱知识库找：动画路径修复工具支持哪些路径模式？"
- "查一下 Moshi 知识库：Moshi_RefCounter 的白名单机制怎么用？"
- "IMA 知识库里有没有 Moshi_VfxBrowser 的文件夹配置说明？"

---

## 五、手动命令行方式（备用）

如果 CodeBuddy 不在手边，也可以用 PowerShell 直接跑（需要 Node.js ≥ 18）。

### 5.1 列出知识库
```powershell
$skill = "$env:USERPROFILE\.codebuddy\skills\ima-skills"
node "$skill\ima_api.cjs" search_knowledge_base '{\"query\":\"\",\"limit\":20}'
```

### 5.2 单文件上传流程（示意，实际要拼 5 步）
```powershell
$file = "G:\HLDDZ3D_Artist\HLDDZArtist\Assets\Editor\ToolsIntegrationPanel\Documentation\IMA_KnowledgeBase\00_Moshi_Toolbox_Index.md"

# Step 1 preflight
node "$skill\knowledge-base\scripts\preflight-check.cjs" --file $file

# Step 2 check_repeated_names
# Step 3 create_media
# Step 4 cos-upload
# Step 5 add_knowledge
# ... 参见 SKILL.md
```

→ 手动拼太麻烦，**强烈推荐第三节的自然语言方式**。

---

## 六、文档更新策略

当你修改了工具或新增工具时：

1. 在 `Documentation/` 原目录改对应的 `Moshi_XXX.md`
2. 在 `Documentation/IMA_KnowledgeBase/02_Tools_Catalog.md` 同步相应段落
3. 在 CodeBuddy 发送：
   ```
   重新上传 02_Tools_Catalog.md 到【Moshi 工具箱知识库】，覆盖旧版本（旧版删除或加时间戳）。
   ```

---

## 七、故障排查

| 现象 | 原因 | 处理 |
|---|---|---|
| `code: -100` / `No credentials` | 环境变量未生效 | 重启 CodeBuddy；或 `[Environment]::GetEnvironmentVariable("IMA_OPENAPI_CLIENTID","User")` 验证 |
| `401 Unauthorized` | API Key 错误/过期 | 到 https://ima.qq.com/agent-interface 重置 |
| `is_repeated=true` | 文件名已存在 | 让 AI 给文件名加时间戳后缀重传 |
| `pass=false`（preflight 拒绝） | 文件类型不支持 | 本工具上传的都是 `.md`，属支持类型，如误拒按提示处理 |
| COS 上传超时 | 网络 | 重试；或切换网络后重跑 |

---

## 八、安全提示

- 您的 `API Key` 已在对话中出现过，如担心泄露建议到 IMA 后台重置
- 知识库文档里**不包含**敏感源码，只是工具说明，可放心上传
- 上传的是您自己的 IMA 账号下的**私有知识库**，除您和您分享的人外他人无法访问
