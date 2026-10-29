namespace YMTPack.Cli;

public sealed class UsageException(string message) : Exception(message);

/// <summary>
/// Minimal parser: first token is the command, "--key value" / "--key=value" are options,
/// listed boolean switches are flags, everything else is positional.
/// </summary>
public sealed class CliArgs
{
    private static readonly HashSet<string> KnownFlags = ["dry-run", "force", "help"];

    private readonly Dictionary<string, string> _options;
    private readonly HashSet<string> _flags;

    private CliArgs(string? command, IReadOnlyList<string> positionals, Dictionary<string, string> options, HashSet<string> flags)
    {
        Command = command;
        Positionals = positionals;
        _options = options;
        _flags = flags;
    }

    public string? Command { get; }

    public IReadOnlyList<string> Positionals { get; }

    public string? Option(string name) => _options.GetValueOrDefault(name);

    public bool Flag(string name) => _flags.Contains(name);

    public static CliArgs Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var positionals = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg is "-h")
            {
                flags.Add("help");
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                string key = arg[2..];
                int eq = key.IndexOf('=');
                if (eq >= 0)
                {
                    options[key[..eq]] = key[(eq + 1)..];
                }
                else if (KnownFlags.Contains(key))
                {
                    flags.Add(key);
                }
                else if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options[key] = args[++i];
                }
                else
                {
                    throw new UsageException($"Option --{key} needs a value.");
                }
            }
            else
            {
                positionals.Add(arg);
            }
        }

        string? command = positionals.Count > 0 ? positionals[0] : null;
        return new CliArgs(command, positionals.Skip(1).ToArray(), options, flags);
    }
}
