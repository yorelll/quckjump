# QuickJump for Flow Launcher

QuickJump is a [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) plugin for quickly filling an active Windows **Open**, **Save**, or **Folder picker** dialog with a file or folder path.

It reads folders currently open in Explorer, Directory Opus, XYplorer, and OneCommander, and also lets you maintain persistent file/folder shortcuts with memorable aliases.

![QuickJump icon](Flow.Launcher.Plugin.QuickJump/icon.png)

## Install

1. Download `QuickJump-<version>.zip` from [Releases](https://github.com/yorelll/quckjump/releases).
2. Extract the archive contents directly into a Flow Launcher plugin directory.
3. In an open/save/folder-selection dialog, invoke Flow Launcher and type `cd`.

## Use

| Query | Results |
| --- | --- |
| `cd` | Folders currently open in supported file managers. |
| `cd work` | Enabled custom entries matching `work` first, then matching currently open folders and a literal path. An exact custom alias is ranked first. |
| `cd /f report` | Enabled custom **file** entries only. |
| `cd /fd project` | Enabled custom **folder** entries only. |
| `cd /s project` | Current file-manager search results only; excludes custom entries. |

### Result actions

- **Enter** — fill and navigate to the selected path.
- **Ctrl + Enter** — copy the selected path.
- **Right-click → Add to custom entries** — add a search result to persistent custom entries. The dialog pre-fills a safe alias; press Enter to accept it or edit it first.

File and folder custom entries use different icons in the result list.

## Custom entries

Open **Flow Launcher Settings → Plugins → QuickJump** to manage custom entries. Each entry has:

- **Alias** — the name you type after `cd`; aliases support Chinese and English text, numbers, hyphens (`-`), and underscores (`_`).
- **Path** — an existing file or folder.
- **Type** — File or Folder.
- **Enabled** — disabled entries are retained but never returned by queries.

A custom alias is matched with Flow Launcher's fuzzy search. Exact matches take precedence over all other results.

When adding an entry from a search result, the default alias:

1. removes a file extension for files;
2. converts whitespace to hyphens;
3. removes other special characters;
4. retains Chinese/English characters, numbers, hyphens, and underscores;
5. adds `-1`, `-2`, etc. if it conflicts with an existing alias.

For example, `My C# Notes.docx` becomes `My-C-Notes`, and `测试 文件夹` becomes `测试-文件夹`.

## Supported file managers

- explorer.exe
- Directory Opus
- XYplorer
- OneCommander

## Development

This project is verified by GitHub Actions because the local development environment intentionally has no C#/.NET build toolchain. See [CLAUDE.md](CLAUDE.md) for the required implementation, review, CI, and release process.
