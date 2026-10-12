# QuickJump for Flow Launcher

[QuickJump](https://github.com/yorelll/quckjump) 是一个 Flow Launcher 插件，可在 Windows 的**打开**、**保存**或**选择文件夹**对话框中快速跳转到文件或文件夹。

它会读取 Explorer、Directory Opus、XYplorer 和 OneCommander 当前打开的文件夹，也支持维护持久化的文件/文件夹别名。

![QuickJump icon](Flow.Launcher.Plugin.QuickJump/icon.png)

## 安装

1. 从 [Releases](https://github.com/yorelll/quckjump/releases) 下载 `QuickJump-<version>.zip`。
2. 将 ZIP 内容直接解压到 Flow Launcher 的插件目录。
3. 在打开、保存或选择文件夹对话框中唤起 Flow Launcher，输入 `cd`。

## 使用

| 查询 | 结果 |
| --- | --- |
| `cd` | 支持的文件管理器中当前打开的文件夹。 |
| `cd work` | 优先显示匹配 `work` 的已启用自定义跳转，再显示当前打开的匹配文件夹和输入路径。精确别名始终置顶。 |
| `cd /f report` | 仅显示已启用的自定义**文件**条目。 |
| `cd /fd project` | 仅显示已启用的自定义**文件夹**条目。 |
| `cd /s project` | 仅显示文件管理器原生搜索结果；不包含自定义条目和输入路径兜底。 |

### 结果操作

- **Enter** — 文件夹会导航到目标位置，文件会填入并确认。已有的输入路径会自动识别为文件或文件夹；不存在的输入路径保留兼容的文件夹导航兜底行为。
- **Ctrl + Enter** — 复制路径。
- **右键 → 添加到自定义跳转** — 将搜索结果保存为持久化快捷方式。窗口会在 Flow 的 UI 线程中显示；当 Flow 当前窗口不可用时，会安全地以顶层居中窗口显示。

自定义文件和文件夹使用不同图标。

## 自定义跳转

在 **Flow Launcher 设置 → 插件 → QuickJump** 中管理自定义跳转。每项包括：

- **别名** — 在 `cd` 后输入的名称；支持中文、英文、数字、连字符（`-`）和下划线（`_`）。
- **路径** — 必须是存在的文件或文件夹。
- **自动识别类型** — 保存时根据当前路径自动识别为文件或文件夹，不能手动选择。
- **启用** — 关闭后保留配置，但不会出现在查询结果中。

新增或编辑时可分别点击 **选择文件…** 或 **选择文件夹…**，也可以手动编辑路径；保存时会重新检测路径。

自定义别名使用 Flow Launcher 的模糊搜索，精确匹配优先级最高。

从右键菜单新增条目时，默认别名会：

1. 对文件移除最终扩展名；
2. 将连续空白转换为连字符；
3. 移除其他特殊字符；
4. 保留中英文等 Unicode 字母、数字、连字符和下划线；
5. 与已有别名冲突时追加 `-1`、`-2` 等后缀。

例如，`My C# Notes.docx` 会生成 `My-C-Notes`，`测试 文件夹` 会生成 `测试-文件夹`。

## 地址栏导航方式

QuickJump 会先向 Windows 原生文件对话框发送快捷键，以聚焦地址栏，再插入所选路径：

- **Alt+D（默认）**：大多数 Windows 文件对话框的标准地址栏快捷键。
- **Ctrl+L（备用）**：当应用或对话框忽略 Alt+D 时，在插件设置中切换到此方式。

插件会在 Flow Launcher 取得焦点之前缓存原生文件对话框句柄，并在有界重试中优先恢复该目标；这改善了 VS Code 等宿主的原生打开对话框兼容性。实际 VS Code 对话框仍建议在发布前完成一次人工冒烟验证。

## 支持的文件管理器

- explorer.exe
- Directory Opus
- XYplorer
- OneCommander

## 开发

本项目通过 GitHub Actions 验证，因为本地开发环境不提供 C#/.NET 构建工具链。实现、review、CI 和发布流程见 [CLAUDE.md](CLAUDE.md)。
