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
using Flow.Launcher.Plugin.QuickJump.FileManager;
using Flow.Launcher.Plugin.QuickJump.Settings;

namespace Flow.Launcher.Plugin.QuickJump;

/// <summary>Fills active Windows file dialogs from open folders or user-managed quick entries.</summary>
public class QuickJump : IPlugin, IContextMenu, ISettingProvider
{
    private const string LogTag = "QuickJump";
    private PluginInitContext? _context;
    private Settings.Settings? _settings;
    private CustomEntryRepository? _customEntries;
    private readonly QuickEntrySearchService _searchService = new();

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
            throw new UnreachableException(); // Guaranteed by the caller after Init.
        }

        var parsedQuery = QuickJumpQuery.Parse(query.Search);
        var results = _searchService.Search(
            parsedQuery,
            _customEntries.GetAll(),
            GetNativeEntries(),
            (search, value) => _context.API.FuzzySearch(search, value).Success);

        // Flow currently orders by Score; spaced scores retain a deterministic
        // order even where multiple host results share an internal rank.
        const int step = 10_000;
        return results.Select((result, index) => ToFlowResult(result, int.MaxValue - index * step)).ToList();
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult.ContextData is not QuickEntryResult entry)
        {
            return [];
        }

        var jumpAction = new Result
        {
            Title = "Jump",
            SubTitle = entry.Path,
            IcoPath = IconFor(entry),
            Action = _ => JumpAction(entry.Path, entry.Type),
        };
        var showAction = new Result
        {
            Title = "Show the path",
            IcoPath = "icon.png",
            Action = _ => ShowAction(entry.Path),
        };
        var copyAction = new Result
        {
            Title = "Copy the path",
            IcoPath = "icon.png",
            Action = _ => CopyAction(entry.Path),
        };

        var actions = new List<Result> { jumpAction, showAction, copyAction };
        if (!entry.IsCustom)
        {
            actions.Add(new Result
            {
                Title = "Add to custom entries",
                SubTitle = "Create a persistent file or folder shortcut",
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

    private Result ToFlowResult(QuickEntryResult entry, int score)
    {
        string typeLabel = entry.IsCustom ? $"Custom {entry.Type}" : entry.IsLiteral ? "Path" : "Open folder";
        return new Result
        {
            Title = entry.Title,
            SubTitle = $"{typeLabel}: {entry.Path}",
            IcoPath = IconFor(entry),
            Action = ctx => ctx.SpecialKeyState.CtrlPressed
                ? CopyAction(entry.Path)
                : JumpAction(entry.Path, entry.Type),
            ContextData = entry,
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

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            _context?.API.ShowMsg("Only an existing file or folder can be added to custom entries.");
            return false;
        }

        var dialog = new CustomEntryEditorDialog(_customEntries, initialPath: path)
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive),
        };
        dialog.ShowDialog();
        return false;
    }

    private bool JumpAction(string path, CustomEntryType type)
    {
        _context?.API.LogInfo(LogTag, $"Jumping to {path}");
        var thread = new Thread(() =>
        {
            // Jump after Flow Launcher has hidden and the file dialog is foreground.
            bool timedOut = !SpinWait.SpinUntil(() => GetForegroundWindowClassName() == "#32770", 1000);
            if (timedOut)
            {
                _context?.API.LogWarn(LogTag, "Dialog window not found");
                return;
            }

            var dialogHandle = PInvoke.GetForegroundWindow();
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

        static string? GetForegroundWindowClassName() => Utils.GetClassName(PInvoke.GetForegroundWindow());
    }

    private void FolderJump(string path, HWND dialogHandle)
    {
        var inputSimulator = new WindowsInput.InputSimulator();
        FocusAddressBar(inputSimulator);
        var editHandle = FindModernAddressEdit(dialogHandle);
        if (editHandle == HWND.Null)
        {
            _context?.API.LogWarn(LogTag, "Modern address edit control not found. Trying legacy dialog.");
            FolderJumpOnLegacyDialog(path, dialogHandle);
            return;
        }

        if (!WaitForVisible(editHandle))
        {
            _context?.API.LogWarn(LogTag, "Address bar was not visible after navigation shortcut.");
            return;
        }

        Utils.SetWindowText(editHandle, path);
        inputSimulator.Keyboard.KeyPress(WindowsInput.VirtualKeyCode.RETURN);
    }

    private void FileJump(string path, HWND dialogHandle)
    {
        // Modern IFileDialog offers a filename Edit control after Ctrl+L/Alt+D.
        // Setting the full path then Enter selects and confirms the file. If that
        // control tree is unavailable, legacy FileName entry is handled below.
        var inputSimulator = new WindowsInput.InputSimulator();
        FocusAddressBar(inputSimulator);
        var editHandle = FindModernAddressEdit(dialogHandle);
        if (editHandle != HWND.Null && WaitForVisible(editHandle))
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

    private static HWND FindModernAddressEdit(HWND dialogHandle)
    {
        var controlHandle = PInvoke.FindWindowEx(dialogHandle, HWND.Null, "WorkerW", null);
        controlHandle = PInvoke.FindWindowEx(controlHandle, HWND.Null, "ReBarWindow32", null);
        controlHandle = PInvoke.FindWindowEx(controlHandle, HWND.Null, "Address Band Root", null);
        controlHandle = PInvoke.FindWindowEx(controlHandle, HWND.Null, "msctls_progress32", null);
        controlHandle = PInvoke.FindWindowEx(controlHandle, HWND.Null, "ComboBoxEx32", null);
        if (controlHandle == HWND.Null)
        {
            return HWND.Null;
        }

        controlHandle = PInvoke.FindWindowEx(controlHandle, HWND.Null, "ComboBox", null);
        return PInvoke.FindWindowEx(controlHandle, HWND.Null, "Edit", null);
    }

    private static bool WaitForVisible(HWND controlHandle) => SpinWait.SpinUntil(() =>
    {
        int style = PInvoke.GetWindowLong(controlHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        return (style & (int)WINDOW_STYLE.WS_VISIBLE) != 0;
    }, 1000);

    private void FolderJumpOnLegacyDialog(string path, HWND dialogHandle)
    {
        var editHandle = FindLegacyEdit(dialogHandle);
        if (editHandle == HWND.Null)
        {
            _context?.API.LogWarn(LogTag, "Filename edit control not found on legacy dialog.");
            return;
        }

        Utils.SetWindowText(editHandle, path);
        var inputSimulator = new WindowsInput.InputSimulator();
        // A first Alt-O may only enter the current folder; a second confirms.
        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.VirtualKeyCode.LMENU, WindowsInput.VirtualKeyCode.VK_O);
        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.VirtualKeyCode.LMENU, WindowsInput.VirtualKeyCode.VK_O);
    }

    private void FileJumpOnLegacyDialog(string path, HWND dialogHandle)
    {
        var editHandle = FindLegacyEdit(dialogHandle);
        if (editHandle == HWND.Null)
        {
            _context?.API.LogWarn(LogTag, "Filename edit control not found on legacy dialog.");
            return;
        }

        Utils.SetWindowText(editHandle, path);
        var inputSimulator = new WindowsInput.InputSimulator();
        // A file path in the filename input requires one confirmation, unlike a
        // folder navigation where the first confirmation can change directory.
        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.VirtualKeyCode.LMENU, WindowsInput.VirtualKeyCode.VK_O);
    }

    private static HWND FindLegacyEdit(HWND dialogHandle)
    {
        var controlHandle = PInvoke.FindWindowEx(dialogHandle, HWND.Null, "ComboBoxEx32", null);
        controlHandle = PInvoke.FindWindowEx(controlHandle, HWND.Null, "ComboBox", null);
        return PInvoke.FindWindowEx(controlHandle, HWND.Null, "Edit", null);
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
}
