# 用户界面转换器 - HTML/CSS 转 Unity 用户界面工具包
此工具可将 HTML/CSS 转换为 Unity UI 工具包（UXML/USS）格式，从而使人工智能能够利用熟悉的网络技术来生成用户界面。

## 支持的元素
| HTML | UXML ||------|------|
| `<div>`， `<span>` | `<ui:VisualElement>` |
| `<button>` | `<ui:Button>` |
| `<input type="text">` | `<ui:TextField>` |
| `<input type="password">` | `<ui:TextField password="true">` |
| `<input type="checkbox">` | `<ui:Toggle>` |
| `<input type="radio">` | `<ui:RadioButton>` |
| `<select>` | `<ui:DropdownField>` |
| `<label>`， `<p>`， `<h1> - <h6>` | `<ui:Label>` |
| `<img>` | `<ui:Image>` |
| `<progress>` | `<ui:ProgressBar>` |

### 直接支持的属性
- 尺寸：`宽度`、`高度`、`最小值-*`、`最大值-*`
- 盒模型：`内边距`、`外边距`、`边框-*`、`圆角半径`
- 背景：`背景颜色`
- 文本：`颜色`、`字体大小`、`换行方式`
- 布局：`布局方向`、`换行方式`、`内容对齐方式`、`轴向对齐方式` 等
- 定位：`定位方式：绝对|相对`、`左`、`上`、`右`、`下`
- `text-align` → `-unity-text-align`
- `font-weight: bold` → `-unity-font-style: bold`

## 不支持
### CSS
- **网格布局**：使用 Flexbox 并设置 `flex-wrap: wrap`
- **间距**：在子元素上使用 `margin`
- **固定/粘性定位**：使用 `absolute` 或 `relative`
- **阴影**：使用分层元素
- **渐变**：使用纯色或图像
- **伪元素**：`::before`、`::after`
### HTML
- `<table>`：使用 Flexbox 网格布局
- `<canvas>`、`<video>`、`<audio>`：没有直接对应的元素
- `<svg>`：转换为图像

## 文件结构
资产/编辑器/用户界面转换器/
├── UIConverterWindow.cs        # 编辑器窗口
├── 解析器/
│   ├── HtmlParser.cs           # HTML 解析
│   └── CssParser.cs            # CSS 解析
├── 转换器/
│   ├── UxmlGenerator.cs        # UXML 生成
│   └── UssGenerator.cs         # USS 生成
├── 验证器/
│   └── FeatureValidator.cs     # 功能验证
├── 配置/
│   ├── element-mapping.json    # 元素映射配置
│   └── css-to-uss-mapping.json # CSS 属性配置
└── 文档/
├── unity-uitoolkit-guide.md    # AI 参考指南
├── html-css-constraints.md      # 约束文档
└── 示例/                        # 示例模板```

## 人工智能提示模板
当人工智能生成用于转化的用户界面时：

根据以下限制条件，生成 HTML/CSS 代码：
- 仅使用 Flexbox 布局（不使用 CSS 网格）
- 避免使用 gap 属性（使用 margin 替代）
- 仅使用受支持的元素：div、span、button、input、label、img、select、textarea、progress
- 使用简单的类选择器
- 避免使用 ：：before/:：after 伪元素
- 使用像素或百分比单位