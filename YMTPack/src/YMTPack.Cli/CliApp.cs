using YMTPack.Cli.Commands;

namespace YMTPack.Cli;

public static class CliApp
{
    public const int ExitOk = 0;
    public const int ExitFailed = 1;
    public const int ExitUsage = 2;

    public const string Usage = """
        ymtpack - pack GTA V addon clothing into a FiveM resource with a fresh YMT

        Usage:
          ymtpack build <input-dir> --name <pack> [--out <dir>] [--gender male|female]
                        [--heels <file>] [--hats <file>] [--dry-run]
              Scan <input-dir> (any mix of addon packs), renumber everything per
              gender/component, rename to stream names and generate the .ymt files.
              --name     DLC/pack name: lowercase a-z, 0-9, _ (max 32)
              --out      resource folder to create (default: ./<name>); must be empty or missing
              --gender   gender for files whose prefix/folders don't say male or female
              --heels    high-heel rules (default: <input-dir>/heels.txt if present)
              --hats     hat hair-scale rules (default: <input-dir>/hats.txt if present)
              --dry-run  show the plan, write nothing

          ymtpack ymt2xml <file.ymt> [--out <file.ymt.xml>] [--force]
          ymtpack xml2ymt <file.ymt.xml> [--out <file.ymt>] [--force]
              Convert between binary .ymt (ped variation or creature metadata) and XML.

        Heels/hats rules: one "<glob> [value]" per line, matched against the source .ydd
        path inside <input-dir> ('*' one folder, '**' any depth, no '/' = file name only).
          heels.txt   feet only, value = heel height (default 1.0)
          hats.txt    p_head only, value = hair shrink 0-1 (default 0.5, 1 = hide)

        Gender detection: "mp_m_freemode_01..^" / "mp_f_freemode_01..^" prefix, else the
        closest folder named like male/men/m or female/women/f, else --gender.
        """;

    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        try
        {
            CliArgs parsed = CliArgs.Parse(args);
            if (parsed.Flag("help") || parsed.Command is "help")
            {
                stdout.WriteLine(Usage);
                return ExitOk;
            }

            return parsed.Command switch
            {
                "build" => BuildCommand.Run(parsed, stdout, stderr),
                "ymt2xml" => ConvertCommands.YmtToXml(parsed, stdout),
                "xml2ymt" => ConvertCommands.XmlToYmt(parsed, stdout),
                null => throw new UsageException("Missing command."),
                _ => throw new UsageException($"Unknown command '{parsed.Command}'."),
            };
        }
        catch (UsageException ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            stderr.WriteLine();
            stderr.WriteLine(Usage);
            return ExitUsage;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or System.Xml.XmlException)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return ExitFailed;
        }
        catch (Exception ex)
        {
            // Unexpected: keep the CLI output clean, full trace on request.
            stderr.WriteLine($"error: unexpected {ex.GetType().Name}: {ex.Message}");
            stderr.WriteLine(Environment.GetEnvironmentVariable("YMTPACK_DEBUG") is "1"
                ? ex.ToString()
                : "(set YMTPACK_DEBUG=1 for the full stack trace)");
            return ExitFailed;
        }
    }
}
