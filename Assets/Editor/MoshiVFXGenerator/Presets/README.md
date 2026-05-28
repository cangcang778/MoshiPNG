# Moshi特效生成器 Presets

本目录用于存放可共享的内置和项目级预设。

```text
Presets/
├── Recipes/     # 配方 JSON，主窗口启动时会自动扫描
├── Palettes/    # 色板或主题配置
├── Blueprints/  # 蓝图 JSON
└── Configs/     # Moshi_VFXGenPreset 配置资产
```

当前工具内置配方由 `Modules/Factory/VFXPresetLibrary.cs` 提供；`Recipes` 下的 JSON 配方会自动追加到配方列表。导出的 JSON 配方、蓝图和配置预设可以放到本目录便于版本管理。

