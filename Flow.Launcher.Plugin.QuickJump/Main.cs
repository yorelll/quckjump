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
    private readonly PluginMessagePresenter _messagePresenter = new();

    public void Init(PluginInitContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _context = Context;
        _settings = Context.API.LoadSettingJsonStorage<Settings.Settings>();
        _customEntries = new CustomEntryRepository(_settings, () => Context.API.SaveSettingJsonStorage<Settings.Settings>());
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
        // HWND，后续操作不再仅依赖“当前前台窗口”。
        var capturedDialog = DialogWindowLocator.CaptureVisibleFileDialog();
        // Retain the original foreground standard dialog even when structural
        // qualification fails, so an elevated target can still be explained.
        var fallbackDialog = capturedDialog == HWND.Null
            ? DialogWindowLocator.CaptureForegroundStandardDialog()
            : default;
        var parsedQuery = QuickJumpQuery.Parse(query.Search);
        var results = _searchService.Search(
            parsedQuery,
            _customEntries.GetAll(),
            GetNativeEntries(),
            (search, value) => _context.API.FuzzySearch(search, value).Success);

        // Flow 以 Score 排序；间隔分数保留确定性顺序。
        const int step = 10_000;
        return results
            .Select((result, index) => ToFlowResult(result, int.MaxValue - index * step, capturedDialog, fallbackDialog))
            .ToList();
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
            Action = _ => JumpAction(entry.Path, entry.Type, context.CapturedDialog, context.FallbackDialog),
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

    private Result ToFlowResult(QuickEntryResult entry, int score, HWND capturedDialog, FallbackDialogCapture fallbackDialog)
    {
        string typeLabel = entry.IsCustom
            ? entry.Type == CustomEntryType.File ? QuickJumpText.CustomFile() : QuickJumpText.CustomFolder()
            : entry.IsLiteral ? QuickJumpText.LiteralPath() : QuickJumpText.OpenFolder();
        var resultContext = new QuickJumpResultContext(entry, capturedDialog, fallbackDialog);
        return new Result
        {
            Title = entry.Title,
            SubTitle = $"{typeLabel}: {entry.Path}",
            IcoPath = IconFor(entry),
            Action = ctx => ctx.SpecialKeyState.CtrlPressed
                ? CopyAction(entry.Path)
                : JumpAction(entry.Path, entry.Type, capturedDialog, fallbackDialog),
            ContextData = resultContext,
            Score = score,
        };
    }

    private static string IconFor(QuickEntryResult entry) => entry.IsCustom
        ? entry.Type == CustomEntryType.File ? "file.png" : "folder.png"
        : "icon.png";

    private bool ShowAction(string text)
    {
        _messagePresenter.Show(text);
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
            _messagePresenter.Show(error is null ? QuickJumpText.AddExistingOnly() : QuickJumpText.Error(error));
            return false;
        }

        // The context editor is a top-level, unowned modeless window. It is
        // scheduled on Flow's UI dispatcher, so it appears immediately even
        // after Flow hides its search window.
        _customEntryDialogPresenter.ShowForPath(_customEntries, path, () =>
            _messagePresenter.Show(QuickJumpText.AddSuccess()));
        return false;
    }

    private bool JumpAction(string path, CustomEntryType type, HWND capturedDialog, FallbackDialogCapture fallbackDialog)
    {
        var thread = new Thread(() =>
        {
            // UIPI must be checked before touching the dialog at all. An
            // elevated target may also refuse foreground activation, so this
            // check must not wait for readiness first.
            if (capturedDialog != HWND.Null && IsElevatedTarget(capturedDialog))
            {
                Fail(JumpFailure.ElevatedTarget);
                return;
            }

            var dialogHandle = DialogWindowLocator.WaitForReadyTarget(
                capturedDialog,
                isFlowForeground: IsFlowForeground);
            if (dialogHandle == HWND.Null)
            {
                var failure = JumpFailurePlan.DecideNoDialogFailure(
                    fallbackDialog.Found,
                    fallbackDialog.ProcessId,
                    (uint)Environment.ProcessId,
                    processId => UipiPlan.Decide(
                        ProcessIntegrityReader.GetCurrent(),
                        ProcessIntegrityReader.Get(processId)) == UipiDecision.BlockElevatedTarget);
                Fail(failure);
                return;
            }

            if (IsElevatedTarget(dialogHandle))
            {
                Fail(JumpFailure.ElevatedTarget);
                return;
            }

            var dialogKind = DialogWindowLocator.GetFileDialogKind(dialogHandle);
            var purpose = DialogWindowLocator.GetFileDialogPurpose(dialogHandle);
            var route = FileDialogJumpPlan.Select(dialogKind, purpose, type);
            switch (route)
            {
                case FileDialogJumpRoute.ModernFolder:
                    ModernFolderJump(path, dialogHandle);
                    break;
                case FileDialogJumpRoute.ModernFilePickerFile:
                    ModernFilePickerFileJump(path, dialogHandle);
                    break;
                case FileDialogJumpRoute.ModernFolderPickerFile:
                    Fail(JumpFailure.FolderPickerWithFile);
                    break;
                case FileDialogJumpRoute.LegacyFile:
                    FileJumpOnLegacyDialog(path, dialogHandle);
                    break;
                case FileDialogJumpRoute.LegacyFolder:
                    FolderJumpOnLegacyDialog(path, dialogHandle);
                    break;
                default:
                    Fail(JumpFailure.DialogStructureChanged);
                    break;
            }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return true;
    }

    private bool IsFlowForeground() => _context?.API.IsMainWindowVisible() == true;

    /// <summary>
    /// UIPI prevents a medium-integrity Flow Launcher from injecting input into
    /// an elevated target. Access denied while reading the target token is
    /// treated as likely elevated.
    /// </summary>
    private static unsafe bool IsElevatedTarget(HWND dialogHandle)
    {
        uint processId;
        _ = PInvoke.GetWindowThreadProcessId(dialogHandle, &processId);
        var decision = UipiPlan.Decide(ProcessIntegrityReader.GetCurrent(), ProcessIntegrityReader.Get(processId));
        return decision == UipiDecision.BlockElevatedTarget;
    }

    private void ModernFolderJump(string path, HWND dialogHandle)
    {
        var inputSimulator = new WindowsInput.InputSimulator();
        var editHandle = FocusAndVerifyModernAddressEdit(inputSimulator, dialogHandle);
        if (editHandle == HWND.Null)
        {
            Fail(JumpFailure.AddressBarFocusFailed);
            return;
        }

        SetDialogText(editHandle, path);
        Thread.Sleep(ModernAddressNavigationPlan.Default.NavigationDelayMilliseconds);
        inputSimulator.Keyboard.KeyPress(WindowsInput.VirtualKeyCode.RETURN);
    }

    /// <summary>
    /// File picker + file entry: navigate to the parent folder, then fill only
    /// the filename field. No final Enter is sent, so the shell never launches
    /// an "open with" flow for the file itself.
    /// </summary>
    private void ModernFilePickerFileJump(string path, HWND dialogHandle)
    {
        string? parentFolder = FileJumpNavigationPlan.GetParentFolder(path);
        string? fileName = FileJumpNavigationPlan.GetFileName(path);
        if (parentFolder is null || fileName is null)
        {
            Fail(JumpFailure.ParentFolderMissing);
            return;
        }

        var inputSimulator = new WindowsInput.InputSimulator();
        var addressEdit = FocusAndVerifyModernAddressEdit(inputSimulator, dialogHandle);
        if (addressEdit == HWND.Null)
        {
            Fail(JumpFailure.AddressBarFocusFailed);
            return;
        }

        SetDialogText(addressEdit, parentFolder);
        Thread.Sleep(ModernAddressNavigationPlan.Default.NavigationDelayMilliseconds);
        inputSimulator.Keyboard.KeyPress(WindowsInput.VirtualKeyCode.RETURN);

        var verification = WaitForParentNavigation(dialogHandle, parentFolder);
        if (verification == NavigationVerification.NotNavigated)
        {
            Fail(JumpFailure.NavigationFailed);
            return;
        }

        if (verification == NavigationVerification.Unverifiable)
        {
            // The address text could not be read cross-process. No second
            // Enter is sent and no full file path is written; only the bare
            // file name is filled so the user can verify the folder manually.
            LogWarn("无法读取文件对话框地址栏文本；将只填入文件名，请用户确认所在文件夹。");
        }

        var fileEdit = WaitForFileNameEdit(dialogHandle);
        if (fileEdit == HWND.Null)
        {
            Fail(JumpFailure.FileNameNotFound);
            return;
        }

        // Deliberately no Enter: the user reviews the path and confirms manually.
        SetDialogText(fileEdit, fileName);
    }

    private static NavigationVerification WaitForParentNavigation(HWND dialogHandle, string parentFolder)
    {
        bool unverifiable = false;
        for (int attempt = 0; FileJumpNavigationPlan.ShouldVerify(attempt); attempt++)
        {
            var address = CrossProcessTextReader.Read(FindModernAddressEdit(dialogHandle));
            string? title = ReadWindowTitle(dialogHandle);
            var state = FileJumpNavigationPlan.Verify(parentFolder, address, title);
            if (state == NavigationVerification.Confirmed)
            {
                return NavigationVerification.Confirmed;
            }
            if (state == NavigationVerification.Unverifiable)
            {
                unverifiable = true;
            }
            Thread.Sleep(FileJumpNavigationPlan.DefaultVerificationDelayMilliseconds);
        }

        return unverifiable
            ? NavigationVerification.Unverifiable
            : NavigationVerification.NotNavigated;
    }

    private static unsafe string? ReadWindowTitle(HWND dialogHandle)
    {
        // Window titles are process-owned top-level captions, so GetWindowText
        // is valid here; it is auxiliary evidence only.
        int length = PInvoke.GetWindowTextLength(dialogHandle);
        if (length <= 0)
        {
            return null;
        }

        fixed (char* buffer = new char[length + 1])
        {
            _ = PInvoke.GetWindowText(dialogHandle, buffer, length + 1);
            return new string(buffer);
        }
    }

    private static HWND WaitForFileNameEdit(HWND dialogHandle)
    {
        for (int attempt = 0; FileJumpNavigationPlan.ShouldVerify(attempt); attempt++)
        {
            var editHandle = DialogWindowLocator.FindFileNameEdit(dialogHandle);
            if (editHandle != HWND.Null && IsVisible(editHandle))
            {
                return editHandle;
            }
            Thread.Sleep(FileJumpNavigationPlan.DefaultVerificationDelayMilliseconds);
        }
        return HWND.Null;
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
            if (ready)
            {
                // The address shortcut can focus a different—but structurally
                // equivalent—address Edit than the first one enumerated. Only
                // use that actual focus if it carries the same strict proof.
                return focusedIsQualified ? focused : preselectedEdit;
            }

            // Some common-dialog hosts leave focus on an ordinary Edit after
            // the shortcut. Do not use that control. Instead, once per jump,
            // focus only the exact preselected address Edit whose structural
            // proof is already established above.
            if (attempt == 0 && dialogForeground && selectedEvidence.IsQualified() && visible)
            {
                if (QualifiedAddressFocus.TryFocus(dialogHandle, preselectedEdit))
                {
                    return preselectedEdit;
                }
            }
        }

        LogWarn("地址栏控件始终未获得可验证的焦点。");
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
        action();
    }

    private static void SetDialogText(HWND editHandle, string text) => _ = Utils.SetWindowText(editHandle, text);

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
            Fail(JumpFailure.LegacyFileNameFieldMissing);
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
            Fail(JumpFailure.LegacyFileNameFieldMissing);
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

    /// <summary>
    /// Every user-visible jump failure routes here. The message is shown in a
    /// dedicated top-level dialog because Flow's ShowMsg may be hidden behind
    /// the dialog, and an elevated target can cover the Flow window entirely.
    /// </summary>
    private void Fail(JumpFailure failure)
    {
        string message = QuickJumpText.Failure(failure);
        LogWarn(message);
        _messagePresenter.Show(message);
    }

    private void LogWarn(string message) => _context?.API.LogWarn(LogTag, message);

    private sealed record QuickJumpResultContext(QuickEntryResult Entry, HWND CapturedDialog, FallbackDialogCapture FallbackDialog);
}
