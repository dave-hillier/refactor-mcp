/// <summary>
/// Outcome of a tool invocation. Errors are values rather than exceptions so
/// the CLI and the daemon can both report them as text without unwinding.
/// </summary>
internal readonly struct ToolResult
{
    private ToolResult(bool isError, string text)
    {
        IsError = isError;
        Text = text;
    }

    public bool IsError { get; }

    public string Text { get; }

    public static ToolResult Ok(string text) => new(false, text);

    public static ToolResult Error(string text) => new(true, text);
}
