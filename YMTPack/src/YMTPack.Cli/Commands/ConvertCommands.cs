using System.Xml.Linq;
using YMTPack.Core;
using YMTPack.Core.Ymt;

namespace YMTPack.Cli.Commands;

public static class ConvertCommands
{
    // Creature metadata is stored as RBF; ped variation YMTs as RSC7 resources.
    private const string CreatureRootElement = "CCreatureMetaData";

    public static int YmtToXml(CliArgs args, TextWriter stdout)
    {
        string input = SingleInput(args, "ymt2xml", "<file.ymt>");
        string output = args.Option("out") ?? input + ".xml";
        EnsureCanWrite(input, output, args.Flag("force"));

        byte[] bytes = File.ReadAllBytes(input);
        XDocument xml = RbfCodec.IsRbf(bytes)
            ? RbfCodec.RbfToXml(bytes)
            : YmtCodec.YmtToXml(bytes, [YmtCodec.GuessDlcName(input)]);
        XmlFiles.SaveWithoutBom(xml, output);
        stdout.WriteLine($"Wrote {output}");
        return CliApp.ExitOk;
    }

    public static int XmlToYmt(CliArgs args, TextWriter stdout)
    {
        string input = SingleInput(args, "xml2ymt", "<file.ymt.xml>");
        string output = args.Option("out") ?? DefaultYmtPath(input);
        EnsureCanWrite(input, output, args.Flag("force"));

        XDocument xml = XDocument.Load(input);
        byte[] ymt = xml.Root?.Name.LocalName == CreatureRootElement
            ? RbfCodec.XmlToRbf(xml)
            : YmtCodec.XmlToYmt(xml);
        File.WriteAllBytes(output, ymt);
        stdout.WriteLine($"Wrote {output}");
        return CliApp.ExitOk;
    }

    private static string SingleInput(CliArgs args, string command, string what) =>
        args.Positionals.Count == 1
            ? args.Positionals[0]
            : throw new UsageException($"{command} needs exactly one {what}.");

    // a.ymt.xml → a.ymt, a.xml → a.ymt, a → a.ymt
    private static string DefaultYmtPath(string xmlPath)
    {
        string path = xmlPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? xmlPath[..^".xml".Length] : xmlPath;
        return path.EndsWith(".ymt", StringComparison.OrdinalIgnoreCase) ? path : path + ".ymt";
    }

    private static void EnsureCanWrite(string input, string output, bool force)
    {
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Output must be a different file from the input.");
        }

        if (File.Exists(output) && !force)
        {
            throw new IOException($"{output} already exists (use --force to overwrite).");
        }
    }
}
