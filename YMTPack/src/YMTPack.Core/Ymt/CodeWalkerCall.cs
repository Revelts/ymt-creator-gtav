namespace YMTPack.Core.Ymt;

internal static class CodeWalkerCall
{
    // CodeWalker reports malformed input with whatever exception its parser hits first.
    public static T Run<T>(Func<T> action, string what)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is not (InvalidDataException or OutOfMemoryException))
        {
            throw new InvalidDataException($"CodeWalker failed to {what}: {ex.Message}", ex);
        }
    }
}
