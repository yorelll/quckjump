#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;
using Flow.Launcher.Plugin.QuickJump.DialogNavigation;
using Flow.Launcher.Plugin.QuickJump.FileManager;
using Flow.Launcher.Plugin.QuickJump.Settings;

namespace Flow.Launcher.Plugin.QuickJump;

/// <summary>将已打开的文件夹或自定义条目填入活动 Windows 文件对话框。</summary>
public class QuickJump : IPlugin, IContextMenu, ISettingProvider
{
    private const string LogTag = "QuickJump";
    private PluginInitContext? _context;
    private Settings.Settings? _settings;
    private CustomEntryRepository? _customEntries;
    private readonly QuickEntrySearchService _searchService = new();
    private readonly CustomEntryDialogPresenter _customEntryDialogPresenter = new();

    public void Init(PluginInitContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _settings = context.API.LoadSettingJsonStorage<Settings.Settings>();
        _customEntries = new CustomEntryRepository(_settings, () => context.API.SaveSettingJsonStorage<Settings.Settings>());
    }

    public List<Result> Query(Query query)
    {
        if (_context is null || _customEntries is null)
        {
            throw new UnreachableException(); // Init 后由 Flow 保证调用。
        }

        // Flow 取得焦点后，原生文件对话框通常仍可见但不在前台。尽早缓存其
        // HWND，后续操作不再仅依赖“当前前台窗口”，从而兼容 VS Code 等宿主。
        var capturedDialog = DialogWindowLocator.CaptureVisibleFileDialog();
        var parsedQuery = QuickJumpQuery.Parse(query.Search);
        var results = _searchService.Search(
            parsedQuery,
            _customEntries.GetAll(),
            GetNativeEntries(),
            (search, value) => _context.API.FuzzySearch(search, value).Success);

        // Flow 以 Score 排序；间隔分数保留确定性顺序。
        const int step = 10_000;
        return results.Select((result, index) => ToFlowResult(result, int.MaxValue - index * step, capturedDialog)).ToList();
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult.ContextData is not QuickJumpResultContext context)
        {
            return [];
        }

        var entry = context.Entry;
        var jumpAction = new Result
        {
            Title = "跳转",
            SubTitle = entry.Path,
            IcoPath = IconFor(entry),
            Action = _ => JumpAction(entry.Path, entry.Type, context.CapturedDialog),
        };
        var showAction = new Result
        {
            Title = "显示路径",
            IcoPath = "icon.png",
            Action = _ => ShowAction(entry.Path),
        };
        var copyAction = new Result
        {
            Title = "复制路径",
            IcoPath = "icon.png",
            Action = _ => CopyAction(entry.Path),
        };

        var actions = new List<Result> { jumpAction, showAction, copyAction };
        if (!entry.IsCustom)
        {
            actions.Add(new Result
            {
                Title = "添加到自定义跳转",
                SubTitle = "创建一个持久化的文件或文件夹快捷方式",
                IcoPath = "icon.png",
                Action = _ => AddToCustomEntries(entry.Path),
            });
        }
        return actions;
    }

    private IEnumerable<Entry> GetNativeEntries()
    {
        List<IFileManager> fileManagers = [
            new Explorer(),
            new DirectoryOpus(),
            new XYplorer(),
            new OneCommander(),
        ];
        return fileManagers.SelectMany(manager => manager.GetEntries());
    }

    private Result ToFlowResult(QuickEntryResult entry, int score, HWND capturedDialog)
    {
        string typeLabel = entry.IsCustom
            ? entry.Type == CustomEntryType.File ? "自定义文件" : "自定义文件夹"
            : entry.IsLiteral ? "输入路径" : "已打开的文件夹";
        var resultContext = new QuickJumpResultContext(entry, capturedDialog);
        return new Result
        {
            Title = entry.Title,
            SubTitle = $"{typeLabel}：{entry.Path}",
            IcoPath = IconFor(entry),
            Action = ctx => ctx.SpecialKeyState.CtrlPressed
                ? CopyAction(entry.Path)
                : JumpAction(entry.Path, entry.Type, capturedDialog),
            ContextData = resultContext,
            Score = score,
        };
    }

    private static string IconFor(QuickEntryResult entry) => entry.IsCustom
        ? entry.Type == CustomEntryType.File ? "file.png" : "folder.png"
        : "icon.png";

    private bool ShowAction(string text)
    {
        _context?.API.ShowMsg(text);
        return false;
    }

    private bool CopyAction(string text)
    {
        _context?.API.CopyToClipboard(text);
        return true;
    }

    private bool AddToCustomEntries(string path)
    {
        if (_customEntries is null)
        {
            return false;
        }

        if (!PathTypeDetector.TryDetect(path, out _, out var error))
        {
            _context?.API.ShowMsg(error ?? "只有现有文件或文件夹可以添加到自定义跳转。");
            return false;
        }

        // Context menus can be invoked while Flow owns a hidden or disabled
        // window. The presenter creates and shows the dialog via WPF's UI
        // dispatcher and avoids assigning such an invalid owner.
        _customEntryDialogPresenter.ShowForPath(_customEntries, path, () =>
            _context?.API.ShowMsg("已添加到自定义跳转。"));
        return false;
    }

    private bool JumpAction(string path, CustomEntryType type, HWND capturedDialog)
    {
        _context?.API.LogInfo(LogTag, $"准备跳转到 {path}");
        var thread = new Thread(() =>
        {
            var dialogHandle = DialogWindowLocator.WaitForTarget(capturedDialog);
            if (dialogHandle == HWND.Null)
            {
                _context?.API.LogWarn(LogTag, "未找到可用的 Windows 文件对话框；已完成有界重试。");
                return;
            }

            if (type == CustomEntryType.File)
            {
                FileJump(path, dialogHandle);
            }
            else
            {
                FolderJump(path, dialogHandle);
            }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return true;
    }

    private void FolderJump(string path, HWND dialogHandle)
    {
        var inputSimulator = new WindowsInput.InputSimulator();
        FocusAddressBar(inputSimulator);
        var editHandle = WaitForModernAddressEdit(dialogHandle);
        if (editHandle == HWND.Null)
        {
            _context?.API.LogWarn(LogTag, "未找到现代地址栏编辑框，尝试旧版文件对话框。");
            FolderJumpOnLegacyDialog(path, dialogHandle);
            return;
        }

        Utils.SetWindowText(editHandle, path);
        inputSimulator.Keyboard.KeyPress(WindowsInput.VirtualKeyCode.RETURN);
    }

    private void FileJump(string path, HWND dialogHandle)
    {
        // 现代 IFileDialog 在地址栏获得焦点后可接受完整路径；对于不提供该
        // 地址栏的旧对话框，单独使用“文件名”输入框并只确认一次。
        var inputSimulator = new WindowsInput.InputSimulator();
        FocusAddressBar(inputSimulator);
        var editHandle = WaitForModernAddressEdit(dialogHandle);
        if (editHandle != HWND.Null)
        {
            Utils.SetWindowText(editHandle, path);
            inputSimulator.Keyboard.KeyPress(WindowsInput.VirtualKeyCode.RETURN);
            return;
        }

        FileJumpOnLegacyDialog(path, dialogHandle);
    }

    private void FocusAddressBar(WindowsInput.InputSimulator inputSimulator)
    {
        Action action = _settings?.Strategy switch
        {
            Strategy.AltD => () => inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.VirtualKeyCode.LMENU, WindowsInput.VirtualKeyCode.VK_D),
            Strategy.CtrlL => () => inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.VirtualKeyCode.LCONTROL, WindowsInput.VirtualKeyCode.VK_L),
            _ => () => throw new UnreachableException(),
        };
        action();
    }

    private static HWND WaitForModernAddressEdit(HWND dialogHandle)
    {
        var retryPlan = new DialogTargetRetryPlan(Attempts: 6, DelayMilliseconds: 100);
        for (int attempt = 0; retryPlan.ShouldTry(attempt); attempt++)
        {
            var editHandle = FindModernAddressEdit(dialogHandle);
            if (editHandle != HWND.Null && IsVisible(editHandle))
            {
                return editHandle;
            }
            if (attempt + 1 < retryPlan.Attempts)
            {
                Thread.Sleep(retryPlan.DelayMilliseconds);
            }
        }
        return HWND.Null;
    }

    private static HWND FindModernAddressEdit(HWND dialogHandle)
    {
        var comboExControls = FindDescendantControls(dialogHandle, "ComboBoxEx32");
        var addressControls = comboExControls.Where(HasAddressBarAncestor).ToArray();
        foreach (var control in addressControls)
        {
            var edit = FindAddressEditUnder(control);
            if (edit != HWND.Null)
            {
                return edit;
            }
        }

        // Some host applications build the standard dialog hierarchy without
        // the usual Address Band Root. Fall back only if exactly one qualified
        // ComboBoxEx/Edit path exists, never to an arbitrary Edit control.
        if (addressControls.Length == 0 && comboExControls.Count == 1)
        {
            return FindAddressEditUnder(comboExControls[0]);
        }
        return HWND.Null;
    }

    private static List<HWND> FindDescendantControls(HWND dialogHandle, string className)
    {
        var controls = new List<HWND>();
        PInvoke.EnumChildWindows(dialogHandle, EnumProc, 0);
        return controls;

        BOOL EnumProc(HWND window, LPARAM _)
        {
            if (string.Equals(Utils.GetClassName(window), className, StringComparison.Ordinal))
            {
                controls.Add(window);
            }
            return true;
        }
    }

    private static bool HasAddressBarAncestor(HWND control)
    {
        var current = PInvoke.GetParent(control);
        for (int depth = 0; depth < 12 && current != HWND.Null; depth++)
        {
            string? className = Utils.GetClassName(current);
            if (string.Equals(className, "Address Band Root", StringComparison.Ordinal))
            {
                return true;
            }
            current = PInvoke.GetParent(current);
        }
        return false;
    }

    private static HWND FindAddressEditUnder(HWND comboExHandle)
    {
        var comboHandle = PInvoke.FindWindowEx(comboExHandle, HWND.Null, "ComboBox", null);
        if (comboHandle == HWND.Null)
        {
            return HWND.Null;
        }

        var editHandle = PInvoke.FindWindowEx(comboHandle, HWND.Null, "Edit", null);
        return editHandle != HWND.Null && AddressEditDiscovery.IsAddressEditPath(["ComboBoxEx32", "ComboBox", "Edit"])
            ? editHandle
            : HWND.Null;
    }

    private static bool IsVisible(HWND controlHandle)
    {
        if (!PInvoke.IsWindow(controlHandle))
        {
            return false;
        }
        int style = PInvoke.GetWindowLong(controlHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        return (style & (int)WINDOW_STYLE.WS_VISIBLE) != 0;
    }

    private void FolderJumpOnLegacyDialog(string path, HWND dialogHandle)
    {
        var editHandle = FindLegacyEdit(dialogHandle);
        if (editHandle == HWND.Null)
        {
            _context?.API.LogWarn(LogTag, "旧版对话框中未找到文件名输入框。");
            return;
        }

        Utils.SetWindowText(editHandle, path);
        var inputSimulator = new WindowsInput.InputSimulator();
        // 首次 Alt+O 可能只是进入文件夹，第二次才确认。
        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.VirtualKeyCode.LMENU, WindowsInput.VirtualKeyCode.VK_O);
        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.VirtualKeyCode.LMENU, WindowsInput.VirtualKeyCode.VK_O);
    }

    private void FileJumpOnLegacyDialog(string path, HWND dialogHandle)
    {
        var editHandle = FindLegacyEdit(dialogHandle);
        if (editHandle == HWND.Null)
        {
            _context?.API.LogWarn(LogTag, "旧版对话框中未找到文件名输入框。");
            return;
        }

        Utils.SetWindowText(editHandle, path);
        var inputSimulator = new WindowsInput.InputSimulator();
        // 文件路径只需确认一次；文件夹路径则可能需要先进入目录。
        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.VirtualKeyCode.LMENU, WindowsInput.VirtualKeyCode.VK_O);
    }

    private static HWND FindLegacyEdit(HWND dialogHandle)
    {
        var controlHandle = PInvoke.FindWindowEx(dialogHandle, HWND.Null, "ComboBoxEx32", null);
        if (controlHandle == HWND.Null)
        {
            return HWND.Null;
        }
        controlHandle = PInvoke.FindWindowEx(controlHandle, HWND.Null, "ComboBox", null);
        return controlHandle == HWND.Null
            ? HWND.Null
            : PInvoke.FindWindowEx(controlHandle, HWND.Null, "Edit", null);
    }

    public Control CreateSettingPanel()
    {
        if (_settings is null || _customEntries is null || _context is null)
        {
            throw new UnreachableException();
        }

        return new QuickJumpSettingsPanel(
            _settings,
            _customEntries,
            () => _context.API.SaveSettingJsonStorage<Settings.Settings>());
    }

    private sealed record QuickJumpResultContext(QuickEntryResult Entry, HWND CapturedDialog);
}
