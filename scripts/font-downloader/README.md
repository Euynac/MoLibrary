# Google Fonts 下载器

通用的 Google Fonts 字体文件下载工具，支持从任意 Google Fonts CSS URL 下载字体文件并自动重命名。

## 功能特性

- ✅ 支持任意 Google Fonts CSS URL
- ✅ 自动解析字体文件并下载 woff2 格式
- ✅ 智能重命名字体文件（按字体家族+字重格式）
- ✅ 支持批量下载项目所需字体
- ✅ 支持字重过滤
- ✅ 跳过已存在文件，避免重复下载
- ✅ 完整的下载进度和统计信息

## 环境要求

- Python 3.6+
- requests 库：`pip install requests`

## 使用方法

### 1. 下载单个字体

```bash
python font_downloader.py "https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&display=swap"
```

### 2. 下载项目所需的所有字体

```bash
python font_downloader.py --download-all
```

### 3. 指定输出目录

```bash
python font_downloader.py "CSS_URL" -o /path/to/fonts
```

### 4. 过滤特定字重

```bash
python font_downloader.py "CSS_URL" --weights 400,500,600
```

## 项目集成

本工具专为 MoLibrary 项目设计，项目中的 UI 主题字体已配置为离线模式。当需要更新或添加新字体时：

1. 运行 `python font_downloader.py --download-all` 下载所有项目字体
2. 字体文件会自动下载到当前目录
3. 手动将字体文件复制到 `MoLibrary.UI/wwwroot/fonts/` 目录

## 支持的字体

### 当前项目使用的字体：

- **Noto Serif SC** - 墨韵山水主题
- **Comfortaa** - 马卡龙甜心主题  
- **Nunito** - 马卡龙甜心主题
- **Source Sans Pro** - 深海静谧主题
- **Open Sans** - 深海静谧主题
- **Courier Prime** - 复古印刷主题
- **Playfair Display** - 复古印刷主题
- **IBM Plex Mono** - 复古印刷主题
- **Roboto** - 基础字体

## 文件命名规则

下载的字体文件按以下规则命名：

```
{FontFamily}-{Weight}.woff2
```

示例：
- `Roboto-Regular.woff2` (font-weight: 400)
- `Roboto-Medium.woff2` (font-weight: 500)
- `NotoSerifSC-SemiBold.woff2` (font-weight: 600)

## 字重映射

- 100 → Thin
- 200 → ExtraLight
- 300 → Light
- 400 → Regular
- 500 → Medium
- 600 → SemiBold
- 700 → Bold
- 800 → ExtraBold
- 900 → Black

## 故障排除

### 1. 下载失败
- 检查网络连接
- 确认 Google Fonts URL 有效
- 检查防火墙设置

### 2. 编码问题 (Windows)
- 脚本已自动处理 Windows 中文环境的编码问题
- 如遇问题，请使用 PowerShell 或 WSL

### 3. 权限问题
- 确保对输出目录有写入权限
- Windows 下可能需要管理员权限

## 开发说明

工具使用正则表达式解析 CSS 中的 `@font-face` 规则：
- 提取字体家族名称
- 提取字重和样式
- 提取 woff2 文件 URL
- 自动重命名并下载

## 许可证

本工具用于 MoLibrary 项目内部使用。使用时请遵守 Google Fonts 的使用条款。