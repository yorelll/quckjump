namespace Flow.Launcher.Plugin.QuickJump.DialogNavigation;

/// <summary>描述在 Flow 关闭后应优先使用哪个原生文件对话框句柄。</summary>
internal enum DialogTargetSource
{
    None,
    Captured,
    Foreground,
}

/// <summary>
/// 纯逻辑选择器：优先使用在 Flow 取得焦点前缓存的文件对话框，避免 VS Code
/// 等宿主在隐藏 Flow 后尚未立刻恢复前台时丢失目标。
/// </summary>
internal static class DialogTargetSelection
{
    public static DialogTargetSource Select(bool capturedIsUsable, bool foregroundIsUsable) =>
        capturedIsUsable ? DialogTargetSource.Captured :
        foregroundIsUsable ? DialogTargetSource.Foreground :
        DialogTargetSource.None;
}

/// <summary>有界等待文件对话框恢复可用状态的重试参数。</summary>
internal sealed record DialogTargetRetryPlan(int Attempts, int DelayMilliseconds)
{
    public static DialogTargetRetryPlan Default { get; } = new(8, 125);

    public bool ShouldTry(int attempt) => attempt >= 0 && attempt < Attempts;
}
