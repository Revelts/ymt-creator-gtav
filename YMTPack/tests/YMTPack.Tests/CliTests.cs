using YMTPack.Cli;

namespace YMTPack.Tests;

public sealed class CliArgsTests
{
    [Fact]
    public void Parses_CommandPositionalsOptionsAndFlags()
    {
        CliArgs args = CliArgs.Parse(["build", "./in", "--name", "mypack", "--dry-run", "--out=./res"]);

        Assert.Equal("build", args.Command);
        Assert.Equal(["./in"], args.Positionals);
        Assert.Equal("mypack", args.Option("name"));
        Assert.Equal("./res", args.Option("out"));
        Assert.True(args.Flag("dry-run"));
        Assert.False(args.Flag("force"));
    }

    [Fact]
    public void MissingOptionValue_IsUsageError()
    {
        Assert.Throws<UsageException>(() => CliArgs.Parse(["build", "./in", "--name"]));
    }
}

public sealed class CliAppTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("ymtpack-cli-").FullName;
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public void Dispose() => Directory.Delete(_work, recursive: true);

    private int Run(params string[] args) => CliApp.Run(args, _out, _err);

    private string Put(string relativePath)
    {
        string path = Path.Combine(_work, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, relativePath);
        return path;
    }

    [Fact]
    public void NoArgs_PrintsUsage()
    {
        Assert.Equal(CliApp.ExitUsage, Run());
        Assert.Contains("ymtpack build", _err.ToString());
    }

    [Fact]
    public void Help_PrintsUsageToStdout()
    {
        Assert.Equal(CliApp.ExitOk, Run("--help"));
        Assert.Contains("ymt2xml", _out.ToString());
    }

    [Fact]
    public void Build_RequiresName()
    {
        Assert.Equal(CliApp.ExitUsage, Run("build", _work));
        Assert.Contains("--name", _err.ToString());
    }

    [Fact]
    public void Build_RejectsBadGender()
    {
        Assert.Equal(CliApp.ExitUsage, Run("build", _work, "--name", "x", "--gender", "other"));
    }

    [Fact]
    public void Build_WritesResourceAndSummary()
    {
        Put("in/male/jbib_000_u.ydd");
        Put("in/male/jbib_diff_000_a_uni.ytd");
        string output = Path.Combine(_work, "res");

        int code = Run("build", Path.Combine(_work, "in"), "--name", "mypack", "--out", output);

        Assert.Equal(CliApp.ExitOk, code);
        Assert.True(File.Exists(Path.Combine(output, "fxmanifest.lua")));
        Assert.Contains("mp_m_freemode_01_mypack.ymt", _out.ToString());
        Assert.Contains("jbib", _out.ToString());
    }

    [Fact]
    public void Build_ReportsErrorsAndFails()
    {
        Put("in/misc/jbib_000_u.ydd");

        int code = Run("build", Path.Combine(_work, "in"), "--name", "mypack", "--out", Path.Combine(_work, "res"));

        Assert.Equal(CliApp.ExitFailed, code);
        Assert.Contains("Error", _err.ToString());
        Assert.Contains("--gender", _err.ToString());
    }

    [Fact]
    public void Build_DryRun_WritesNothing()
    {
        Put("in/male/jbib_000_u.ydd");
        Put("in/male/jbib_diff_000_a_uni.ytd");
        string output = Path.Combine(_work, "res");

        Assert.Equal(CliApp.ExitOk, Run("build", Path.Combine(_work, "in"), "--name", "mypack", "--out", output, "--dry-run"));
        Assert.False(Directory.Exists(output));
        Assert.Contains("dry run", _out.ToString());
    }

    [Fact]
    public void Ymt2Xml_And_Xml2Ymt_RoundTrip()
    {
        Put("in/female/lowr_000_u.ydd");
        Put("in/female/lowr_diff_000_a_uni.ytd");
        string res = Path.Combine(_work, "res");
        Assert.Equal(CliApp.ExitOk, Run("build", Path.Combine(_work, "in"), "--name", "rt", "--out", res));
        string ymt = Path.Combine(res, "stream", "female", "mp_f_freemode_01_rt.ymt");

        Assert.Equal(CliApp.ExitOk, Run("ymt2xml", ymt));
        string xml = ymt + ".xml";
        Assert.Contains("<dlcName>rt</dlcName>", File.ReadAllText(xml));

        string rebuilt = Path.Combine(_work, "rebuilt.ymt");
        Assert.Equal(CliApp.ExitOk, Run("xml2ymt", xml, "--out", rebuilt));
        Assert.Equal("RSC7"u8.ToArray(), File.ReadAllBytes(rebuilt)[..4]);
    }

    [Fact]
    public void Convert_RefusesToOverwriteWithoutForce()
    {
        string existing = Put("a.ymt.xml");
        string target = Put("a.ymt");

        Assert.Equal(CliApp.ExitFailed, Run("xml2ymt", existing));
        Assert.Equal("a.ymt", File.ReadAllText(target));
    }

    [Fact]
    public void Xml2Ymt_DefaultsToYmtExtension_AndRejectsSamePath()
    {
        Put("in/male/jbib_000_u.ydd");
        Put("in/male/jbib_diff_000_a_uni.ytd");
        string res = Path.Combine(_work, "res");
        Run("build", Path.Combine(_work, "in"), "--name", "ext", "--out", res);
        string xml = Path.Combine(_work, "plain.xml");
        Assert.Equal(CliApp.ExitOk, Run("ymt2xml", Path.Combine(res, "stream", "male", "mp_m_freemode_01_ext.ymt"), "--out", xml));

        Assert.Equal(CliApp.ExitOk, Run("xml2ymt", xml));
        Assert.True(File.Exists(Path.Combine(_work, "plain.ymt")));
        Assert.Equal("<?xml"u8.ToArray(), File.ReadAllBytes(xml)[..5]);
        Assert.Equal(CliApp.ExitFailed, Run("xml2ymt", xml, "--out", xml, "--force"));
    }

    [Fact]
    public void Build_SummarizesWarnings()
    {
        Put("in/male/jbib_000_u.ydd");
        Put("in/male/jbib_diff_000_a_uni.ytd");
        Put("in/male/lowr_000_u.ydd");

        Assert.Equal(CliApp.ExitOk, Run("build", Path.Combine(_work, "in"), "--name", "w", "--out", Path.Combine(_work, "res")));
        Assert.Contains("1 warning", _err.ToString());
    }

    [Fact]
    public void Build_HeelsOption_WritesCreature_AndConvertersHandleIt()
    {
        Put("in/female/feet_000_u.ydd");
        Put("in/female/feet_diff_000_a_uni.ytd");
        string heels = Path.Combine(_work, "my-heels.txt");
        File.WriteAllText(heels, "feet_000_u.ydd 2\n");
        string res = Path.Combine(_work, "res");

        Assert.Equal(CliApp.ExitOk, Run("build", Path.Combine(_work, "in"), "--name", "hh", "--out", res, "--heels", heels));
        Assert.Contains("heels 1", _out.ToString());

        string creature = Path.Combine(res, "stream", "female", "mp_creaturemetadata_f_hh.ymt");
        Assert.Equal(CliApp.ExitOk, Run("ymt2xml", creature));
        Assert.Contains("CCreatureMetaData", File.ReadAllText(creature + ".xml"));
        string rebuilt = Path.Combine(_work, "creature2.ymt");
        Assert.Equal(CliApp.ExitOk, Run("xml2ymt", creature + ".xml", "--out", rebuilt));
        Assert.Equal("RBF0"u8.ToArray(), File.ReadAllBytes(rebuilt)[..4]);
    }

    [Fact]
    public void Ymt2Xml_FailsCleanlyOnGarbage()
    {
        string bad = Put("bad.ymt");

        Assert.Equal(CliApp.ExitFailed, Run("ymt2xml", bad));
        Assert.Contains("RSC7", _err.ToString());
    }

    [Fact]
    public void UnknownCommand_IsUsageError()
    {
        Assert.Equal(CliApp.ExitUsage, Run("frobnicate"));
    }
}
