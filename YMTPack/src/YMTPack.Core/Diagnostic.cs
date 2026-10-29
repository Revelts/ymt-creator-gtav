namespace YMTPack.Core;

public enum Severity
{
    Info,
    Warning,
    Error,
}

/// <param name="Path">Input-relative file path the message is about, when there is one.</param>
public sealed record Diagnostic(Severity Severity, string Message, string? Path = null)
{
    public override string ToString() =>
        Path is null ? $"{Severity}: {Message}" : $"{Severity}: {Path}: {Message}";
}
