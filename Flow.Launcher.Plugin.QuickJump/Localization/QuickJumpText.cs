using System;
using Flow.Launcher.Plugin.QuickJump;
using Flow.Launcher.Plugin.QuickJump.CustomEntries;

namespace Flow.Launcher.Plugin.QuickJump.Localization;

/// <summary>Generated Flow localization accessors with test-only English fallbacks before Init.</summary>
internal static class QuickJumpText
{
    private static bool IsInitialized => global::Flow.Launcher.Plugin.QuickJump.QuickJump.Context is not null;

    public static string PluginName() => IsInitialized ? Localize.quickjump_plugin_name() : "QuickJump";
    public static string PluginDescription() => IsInitialized ? Localize.quickjump_plugin_description() : "Quickly jump to files and folders in open or save dialogs.";
    public static string CustomFile() => IsInitialized ? Localize.quickjump_result_custom_file() : "Custom file";
    public static string CustomFolder() => IsInitialized ? Localize.quickjump_result_custom_folder() : "Custom folder";
    public static string LiteralPath() => IsInitialized ? Localize.quickjump_result_literal_path() : "Entered path";
    public static string OpenFolder() => IsInitialized ? Localize.quickjump_result_open_folder() : "Open folder";
    public static string ContextJump() => IsInitialized ? Localize.quickjump_context_jump() : "Jump";
    public static string ContextShowPath() => IsInitialized ? Localize.quickjump_context_show_path() : "Show path";
    public static string ContextCopyPath() => IsInitialized ? Localize.quickjump_context_copy_path() : "Copy path";
    public static string ContextAddCustom() => IsInitialized ? Localize.quickjump_context_add_custom() : "Add to custom entries";
    public static string ContextAddCustomSubtitle() => IsInitialized ? Localize.quickjump_context_add_custom_subtitle() : "Create a persistent file or folder shortcut";
    public static string AddExistingOnly() => IsInitialized ? Localize.quickjump_add_existing_only() : "Only an existing file or folder can be added to custom entries.";
    public static string AddSuccess() => IsInitialized ? Localize.quickjump_add_success() : "Added to custom entries.";
    public static string UpdateEnabledFailed() => IsInitialized ? Localize.quickjump_update_enabled_failed() : "Unable to update entry state.";
    public static string SelectEdit() => IsInitialized ? Localize.quickjump_select_edit() : "Select a custom entry to edit.";
    public static string SelectDelete() => IsInitialized ? Localize.quickjump_select_delete() : "Select a custom entry to delete.";
    public static string EntryMissing() => IsInitialized ? Localize.quickjump_entry_missing() : "The selected custom entry no longer exists.";
    public static string DeleteConfirm(string alias) => IsInitialized ? Localize.quickjump_delete_confirm(alias) : $"Delete custom entry “{alias}”? The original file or folder will not be deleted.";
    public static string SaveFailed() => IsInitialized ? Localize.quickjump_save_failed() : "Unable to save custom entry.";
    public static string AliasFallback() => IsInitialized ? Localize.quickjump_alias_fallback() : "entry";
    public static string DetectedInvalid() => IsInitialized ? Localize.quickjump_detected_invalid() : "No valid file or folder found";
    public static string ChooseFileTitle() => IsInitialized ? Localize.quickjump_editor_choose_file_title() : "Choose a file to jump to";
    public static string ChooseFolderTitle() => IsInitialized ? Localize.quickjump_editor_choose_folder_title() : "Choose a folder to jump to";
    public static string AllFilesFilter() => IsInitialized ? Localize.quickjump_editor_all_files() : "All files|*.*";

    public static string Error(QuickJumpError? error) => error switch
    {
        null => string.Empty,
        { Code: QuickJumpErrorCode.AliasRequired } => IsInitialized ? Localize.quickjump_error_alias_required() : "Alias is required.",
        { Code: QuickJumpErrorCode.AliasLength } => IsInitialized ? Localize.quickjump_error_alias_length(error.Argument) : $"Alias must be {error.Argument} characters or fewer.",
        { Code: QuickJumpErrorCode.AliasInvalid } => IsInitialized ? Localize.quickjump_error_alias_invalid() : "Alias may contain only letters, numbers, hyphens, and underscores; spaces and special characters are not allowed.",
        { Code: QuickJumpErrorCode.PathRequired } => IsInitialized ? Localize.quickjump_error_path_required() : "Path is required.",
        { Code: QuickJumpErrorCode.PathInvalid } => IsInitialized ? Localize.quickjump_error_path_invalid() : "Path is not a valid Windows path.",
        { Code: QuickJumpErrorCode.PathMissing } => IsInitialized ? Localize.quickjump_error_path_missing() : "Path does not exist or cannot be accessed. Choose an existing file or folder.",
        { Code: QuickJumpErrorCode.PathTypeChanged } => IsInitialized ? Localize.quickjump_error_path_type_changed() : "The path type changed. Save again to detect its type automatically.",
        { Code: QuickJumpErrorCode.AliasExists } => IsInitialized ? Localize.quickjump_error_alias_exists(error.Argument) : $"An entry with alias “{error.Argument}” already exists.",
        { Code: QuickJumpErrorCode.EntryMissing } => IsInitialized ? Localize.quickjump_error_entry_missing(error.Argument) : $"Custom entry “{error.Argument}” no longer exists.",
        { Code: QuickJumpErrorCode.DetectPath } => IsInitialized ? Localize.quickjump_error_detect_path() : "Unable to detect the path type.",
        _ => SaveFailed(),
    };

    internal static Func<CustomEntryType, string>? TypeResolverForTests { get; set; }

    public static string Type(CustomEntryType type) => TypeResolverForTests?.Invoke(type) ?? (type == CustomEntryType.File
        ? IsInitialized ? Localize.quickjump_detected_file() : "File"
        : IsInitialized ? Localize.quickjump_detected_folder() : "Folder");
}
