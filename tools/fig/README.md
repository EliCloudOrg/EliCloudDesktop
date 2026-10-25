# `.fig` 反解工具

`docs/design-spec.md` 里的每个数字（字号、行高、内边距、圆角、颜色、文案）
都是从设计稿的原始节点属性里读出来的，不是目测。这里就是读它的工具。

## 为什么需要工具

`.fig` 是 zip，但里面的 `canvas.fig` 是 **fig-kiwi** 二进制（Figma 自研的 schema 驱动格式），
不是图片、也不是 JSON：

```text
canvas.fig
├── "fig-kiwi"                 magic（8 字节）
├── uint32 LE version          = 101
├── uint32 LE size + chunk 0   kiwi schema，  raw deflate 压缩
├── uint32 LE size + chunk 1   kiwi message， zstd 压缩
└── ...
```

chunk 0 是 schema，chunk 1 是用 schema 解码出来的节点树。两者的压缩算法**不一样**
（schema 是 raw deflate，message 是 zstd）—— 这一点是从字节里试出来的。

## 用法

```powershell
# 1) 解压 .fig 容器
Copy-Item 'E:\Download\EliCloudDesktopUI.fig' .\design.zip
Expand-Archive .\design.zip -DestinationPath .\unpacked

# 2) 安装解码依赖
npm install kiwi-schema pako

# 3) 解码 canvas.fig → canvas.json（约 42 MB，别直接打开）
node decode.mjs .\unpacked\canvas.fig .\canvas.json

# 4) 还原节点树 → 可读的层级文本（约 1.4 MB 行文本）
node outline.mjs .\canvas.json .\outline.txt
```

| 脚本 | 作用 |
|---|---|
| `decode.mjs` | `.fig` 容器 → `canvas.json`（`{ version, schema, message }`） |
| `outline.mjs` | 节点树 → 缩进文本：类型、名字、尺寸、位置、排版、填充、描边、效果、文本内容 |
| `images.mjs` | 列出所有图片填充的 sha1 与「用在哪」（按父链） —— 用来把 `images/` 里的哈希还原成语义化的文件名 |
| `inst.mjs` | 列出某个组件实例的全部文案（含实例覆盖与组件属性赋值） |
| `comp.mjs` | 按 guid 打印某一棵子树（组件定义或实例） |

已知的两个坑，都写在脚本注释里：

- kiwi 的 `Optional`/`guid` 字段读出来是对象，**不能直接当字符串用**；
- 节点属性是**内联在 nodeChange 上**的，不是包在 `.node` 里（第一版按 `.node` 取，全树 8914 个节点都取不到）。
