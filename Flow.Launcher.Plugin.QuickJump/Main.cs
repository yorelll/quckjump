#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Globalization;
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
using Flow.Launcher.Plugin.QuickJump.Diagnostics;
using Flow.Launcher.Plugin.QuickJump.FileManager;
using Flow.Launcher.Plugin.QuickJump.Localization;
using Flow.Launcher.Plugin.QuickJump.Settings;

namespace Flow.Launcher.Plugin.QuickJump;

/// <summary>将已打开的文件夹或自定义条目填入活动 Windows 文件对话框。</summary>
public class QuickJump : IPlugin, IContextMenu, ISettingProvider, IPluginI18n
{
    private const string LogTag = "QuickJump";
    internal static PluginInitContext Context { get; private set; } = null!;
    private PluginInitContext? _context;
    private Settings.Settings? _settings;
    private CustomEntryRepository? _customEntries;
    private readonly QuickEntrySearchService _searchService = new();
    private readonly CustomEntryDialogPresenter _customEntryDialogPresenter = new();

    public void Init(PluginInitContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _context = Context;
        _settings = Context.API.LoadSettingJsonStorage<Settings.Settings>();
        _customEntries = new CustomEntryRepository(_settings, () => Context.API.SaveSettingJsonStorage<Settings.Settings>());
        QuickJumpDiagnostics.Initialize(Context, "1.0.1-diagnostic.3");
    }

    public string GetTranslatedPluginTitle() => QuickJumpText.PluginName();

    public string GetTranslatedPluginDescription() => QuickJumpText.PluginDescription();

    public void OnCultureInfoChanged(CultureInfo newCulture)
    {
        // Results/context menus resolve Localize values on construction. The
        // registry refreshes materialized type labels on existing WPF controls.
        LocalizedControlRegistry.RefreshAll();
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
        QuickJumpDiagnostics.Info($"query capturedDialog={(capturedDialog == HWND.Null ? "none" : "eligible")}");
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
            Title = QuickJumpText.ContextJump(),
            SubTitle = entry.Path,
            IcoPath = IconFor(entry),
            Action = _ => JumpAction(entry.Path, entry.Type, context.CapturedDialog),
        };
        var showAction = new Result
        {
            Title = QuickJumpText.ContextShowPath(),
            IcoPath = "icon.png",
            Action = _ => ShowAction(entry.Path),
        };
        var copyAction = new Result
        {
            Title = QuickJumpText.ContextCopyPath(),
            IcoPath = "icon.png",
            Action = _ => CopyAction(entry.Path),
        };

        var actions = new List<Result> { jumpAction, showAction, copyAction };
        if (!entry.IsCustom)
        {
            actions.Add(new Result
            {
                Title = QuickJumpText.ContextAddCustom(),
                SubTitle = QuickJumpText.ContextAddCustomSubtitle(),
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
            ? entry.Type == CustomEntryType.File ? QuickJumpText.CustomFile() : QuickJumpText.CustomFolder()
            : entry.IsLiteral ? QuickJumpText.LiteralPath() : QuickJumpText.OpenFolder();
        var resultContext = new QuickJumpResultContext(entry, capturedDialog);
        return new Result
        {
            Title = entry.Title,
            SubTitle = $"{typeLabel}: {entry.Path}",
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
            _context?.API.ShowMsg(error is null ? QuickJumpText.AddExistingOnly() : QuickJumpText.Error(error));
            return false;
        }

        // Context menus can be invoked while Flow owns a hidden or disabled
        // window. The presenter creates and shows the dialog via WPF's UI
        // dispatcher and avoids assigning such an invalid owner.
        _customEntryDialogPresenter.ShowForPath(_customEntries, path, () =>
            _context?.API.ShowMsg(QuickJumpText.AddSuccess()));
        return false;
    }

    private bool JumpAction(string path, CustomEntryType type, HWND capturedDialog)
    {
        _context?.API.LogInfo(LogTag, $"准备跳转到 {path}");
        QuickJumpDiagnostics.Info($"jump action path={path} type={type} captured={(capturedDialog == HWND.Null ? "none" : "present")}");
        var thread = new Thread(() =>
        {
            var dialogHandle = DialogWindowLocator.WaitForReadyTarget(
                capturedDialog,
                isFlowForeground: IsFlowForeground);
            if (dialogHandle == HWND.Null)
            {
                _context?.API.LogWarn(LogTag, "原生文件对话框未在有界重试内成为前台窗口；为避免向 Flow 注入快捷键，已取消跳转。");
                return;
            }

            var dialogKind = DialogWindowLocator.GetFileDialogKind(dialogHandle);
            var route = FileDialogJumpPlan.Select(dialogKind, type);
            QuickJumpDiagnostics.Info($"jump route kind={dialogKind} entryType={type} route={route}");
            switch (route)
            {
                case FileDialogJumpRoute.ModernFile:
                    ModernFileJump(path, dialogHandle);
                    break;
                case FileDialogJumpRoute.ModernFolder:
                    ModernFolderJump(path, dialogHandle);
                    break;
                case FileDialogJumpRoute.LegacyFile:
                    FileJumpOnLegacyDialog(path, dialogHandle);
                    break;
                case FileDialogJumpRoute.LegacyFolder:
                    FolderJumpOnLegacyDialog(path, dialogHandle);
                    break;
                default:
                    _context?.API.LogWarn(LogTag, "已验证的原生文件对话框结构在跳转前发生变化；已取消跳转。");
                    break;
            }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return true;
    }

    private bool IsFlowForeground() => _context?.API.IsMainWindowVisible() == true;

    private void ModernFolderJump(string path, HWND dialogHandle)
    {
        var inputSimulator = new WindowsInput.InputSimulator();
        var editHandle = FocusAndVerifyModernAddressEdit(inputSimulator, dialogHandle);
        if (editHandle == HWND.Null)
        {
            _context?.API.LogWarn(LogTag, "现代地址栏未在快捷键后获得焦点；已取消跳转。");
            return;
        }

        SetDialogText(editHandle, path);
        Thread.Sleep(ModernAddressNavigationPlan.Default.NavigationDelayMilliseconds);
        QuickJumpDiagnostics.Info("modern folder inject key=Enter after verified address focus");
        inputSimulator.Keyboard.KeyPress(WindowsInput.VirtualKeyCode.RETURN);
    }

    private void ModernFileJump(string path, HWND dialogHandle)
    {
        // Modern IFileDialog accepts the full file path through its verified
        // address-bar path. Legacy filename controls are routed before this
        // method and therefore never receive the modern one-Enter behavior.
        var inputSimulator = new WindowsInput.InputSimulator();
        var editHandle = FocusAndVerifyModernAddressEdit(inputSimulator, dialogHandle);
        if (editHandle == HWND.Null)
        {
            _context?.API.LogWarn(LogTag, "现代地址栏未在快捷键后获得焦点；已取消跳转。");
            return;
        }

        SetDialogText(editHandle, path);
        Thread.Sleep(ModernAddressNavigationPlan.Default.NavigationDelayMilliseconds);
        QuickJumpDiagnostics.Info("modern file inject key=Enter after verified address focus");
        inputSimulator.Keyboard.KeyPress(WindowsInput.VirtualKeyCode.RETURN);
    }

    private HWND FocusAndVerifyModernAddressEdit(WindowsInput.InputSimulator inputSimulator, HWND dialogHandle)
    {
        var plan = ModernAddressNavigationPlan.Default;
        for (int attempt = 0; plan.ShouldTry(attempt); attempt++)
        {
            FocusAddressBar(inputSimulator);
            Thread.Sleep(plan.FocusDelayMilliseconds);

            var preselectedEdit = FindModernAddressEdit(dialogHandle);
            bool exists = preselectedEdit != HWND.Null;
            bool visible = exists && IsVisible(preselectedEdit);
            // GetFocus only reports focus for this background plugin thread;
            // inspect the target dialog's GUI thread instead.
            var focused = GetDialogFocusedWindow(dialogHandle);
            var selectedEvidence = DescribeModernAddressEdit(preselectedEdit, dialogHandle);
            var focusedEvidence = DescribeModernAddressEdit(focused, dialogHandle);
            bool selectedHasFocus = visible && focused == preselectedEdit;
            bool focusedIsQualified = focusedEvidence.IsQualified();
            bool dialogForeground = PInvoke.GetForegroundWindow() == dialogHandle;
            bool ready = ModernAddressFocusPlan.IsReady(
                dialogForeground,
                exists || focused != HWND.Null,
                visible || IsVisible(focused),
                selectedHasFocus || focusedIsQualified);
            QuickJumpDiagnostics.Info($"address focus retry={attempt + 1}/{plan.FocusAttempts} dialogForeground={dialogForeground} preselected={DescribeHandle(preselectedEdit)} selectedPath={DescribePath(selectedEvidence)} focused={DescribeHandle(focused)} focusedPath={DescribePath(focusedEvidence)} selectedHasFocus={selectedHasFocus} focusedQualified={focusedIsQualified} ready={ready}");
            if (ready)
            {
                // The address shortcut can focus a different—but structurally
                // equivalent—address Edit than the first one enumerated. Only
                // use that actual focus if it carries the same strict proof.
                return focusedIsQualified ? focused : preselectedEdit;
            }
        }

        QuickJumpDiagnostics.Warn("address focus abort: qualified edit never gained focus after shortcut");
        return HWND.Null;
    }

    private static ModernAddressEditEvidence DescribeModernAddressEdit(HWND editHandle, HWND dialogHandle)
    {
        var classes = GetBoundedAncestorClasses(editHandle, maxDepth: 12);
        bool isEdit = string.Equals(Utils.GetClassName(editHandle), "Edit", StringComparison.Ordinal);
        bool belongsToDialog = editHandle != HWND.Null && PInvoke.GetAncestor(editHandle, GET_ANCESTOR_FLAGS.GA_ROOT) == dialogHandle;
        return new ModernAddressEditEvidence(isEdit, classes, belongsToDialog);
    }

    private static List<string> GetBoundedAncestorClasses(HWND handle, int maxDepth)
    {
        var classes = new List<string>();
        var current = handle;
        for (int depth = 0; depth < maxDepth && current != HWND.Null; depth++)
        {
            classes.Add(Utils.GetClassName(current) ?? "<unknown>");
            current = PInvoke.GetParent(current);
        }
        return classes;
    }

    private static string DescribePath(ModernAddressEditEvidence evidence) =>
        $"isEdit={evidence.IsEdit};owned={evidence.IsOwnedByTargetDialog};classes=[{string.Join(">", evidence.AncestorClasses)}];qualified={evidence.IsQualified()}";

    private static string DescribeHandle(HWND handle) => handle == HWND.Null ? "none" : handle.ToString();

    private static unsafe HWND GetDialogFocusedWindow(HWND dialogHandle)
    {
        uint processId;
        uint threadId = PInvoke.GetWindowThreadProcessId(dialogHandle, &processId);
        GUITHREADINFO info = new() { cbSize = (uint)sizeof(GUITHREADINFO) };
        return PInvoke.GetGUIThreadInfo(threadId, ref info) ? info.hwndFocus : HWND.Null;
    }

    private void FocusAddressBar(WindowsInput.InputSimulator inputSimulator)
    {
        Action action = _settings?.Strategy switch
        {
            Strategy.AltD => () => inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.VirtualKeyCode.LMENU, WindowsInput.VirtualKeyCode.VK_D),
            Strategy.CtrlL => () => inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.VirtualKeyCode.LCONTROL, WindowsInput.VirtualKeyCode.VK_L),
            _ => () => throw new UnreachableException(),
        };
        QuickJumpDiagnostics.Info($"address focus inject strategy={_settings?.Strategy}");
        action();
    }

    private static void SetDialogText(HWND editHandle, string path)
    {
        var result = Utils.SetWindowText(editHandle, path);
        string currentText = GetWindowText(editHandle);
        QuickJumpDiagnostics.Info($"set-window-text result={result} editClass={Utils.GetClassName(editHandle)} textMatches={string.Equals(currentText, path, StringComparison.Ordinal)}");
    }

    private static unsafe string GetWindowText(HWND handle)
    {
        int length = PInvoke.GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        fixed (char* buffer = new char[length + 1])
        {
            _ = PInvoke.GetWindowText(handle, buffer, length + 1);
            return new string(buffer);
        }
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

        // A ComboBoxEx/Edit path without Address Band Root is the verified
        // legacy filename structure. It must never be accepted as a modern
        // address bar because folder navigation then needs two Alt+O confirms.
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

        SetDialogText(editHandle, path);
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

        SetDialogText(editHandle, path);
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
