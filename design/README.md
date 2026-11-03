# design/ 公共设计层

四端共用的设计令牌、官方样式映射、例外清单、资产与页面规格。

| 文件 | 说明 |
|---|---|
| [`tokens.json`](tokens.json) | **唯一源**：语义令牌 → 各平台官方引用（或登记例外） |
| [`tokens.official.md`](tokens.official.md) | 人读映射表 + 规则；生成部分由 `gen-tokens` 维护 |
| [`EXCEPTIONS.md`](EXCEPTIONS.md) | 令牌例外（自动生成）+ 代码例外（人工登记，审计用） |
| [`windows.md`](windows.md) | 窗口 / 布局 / 断点 / 无障碍规格 |
| [`screens/`](screens/) | 每页的内容与行为规格（只允许布局差异） |
| [`icons/`](icons/) | 品牌矢量源（草案） |
| [`ASSETS.md`](ASSETS.md) / [`assets.json`](assets.json) | 资产来源/许可/导出清单 |
| [`build-config.json`](build-config.json) | 构建时配置：环境 → 注入值/环境变量；平台 → 输出路径 |
| [`generated/`](generated/) | 覆盖率报告、copy key 映射等确定性生成物 |

## 令牌模型（一句话）

```json
"color.text.primary": {
  "source": "official",
  "platforms": {
    "windows": { "ref": "TextFillColorPrimaryBrush" },
    "macos":   { "ref": ".primary" },
    "android": { "ref": "MaterialTheme.colorScheme.onSurface" },
    "linux":   { "ref": "@theme_fg_color" }
  }
}
```

- 叶子上的字段是四端默认值，`platforms.<platform>` 覆盖；每端必须解析出 `source`。
- `source: "official"`：`ref` 是官方资源键 / 样式表达式 / CSS 变量 / style class；
  `emit: "comment"` 表示官方只有机制、没有可寻址令牌。
- `source: "custom"`：必须 `value` + `reason` + `approvedBy` + `scope`，由生成器登记进 `EXCEPTIONS.md`。
- 校验失败（缺 source/ref/reason、缺平台、非法颜色/数值）→ 生成器退出 1。

## 命令

```powershell
python tools/gen-tokens/gen_tokens.py --check   # 令牌生成物是否最新
python tools/gen-copy/gen_copy.py --check       # 文案生成物是否最新 + key 覆盖率
python tools/audit-official-styles/audit.py     # 裸值/手写样式审计
python tools/export-assets.py --check           # 资产清单
```

改令牌/文案/资产后**先跑生成器再提交**；CI 会用 `--check` + `git diff` 检查是否漏提交。
