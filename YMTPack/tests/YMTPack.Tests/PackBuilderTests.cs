using System.Text.Json;
using YMTPack.Core;
using YMTPack.Core.Model;
using YMTPack.Core.Ymt;

namespace YMTPack.Tests;

public sealed class PackBuilderTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("ymtpack-build-").FullName;
    private string Input => Path.Combine(_work, "input");
    private string Output => Path.Combine(_work, "out", "my_clothes");

    public void Dispose() => Directory.Delete(_work, recursive: true);

    private void Put(string relativePath, string content)
    {
        string path = Path.Combine(Input, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void PutSampleInput()
    {
        Put("[male] tops/jbib_004_u.ydd", "m-jbib4");
        Put("[male] tops/jbib_diff_004_a_uni.ytd", "m-jbib4-a");
        Put("[male] tops/jbib_004_u.yld", "m-jbib4-cloth");
        Put("female/mp_f_freemode_01_old^p_head_002.ydd", "f-hat2");
        Put("female/mp_f_freemode_01_old^p_head_diff_002_a.ytd", "f-hat2-a");
        Put("female/notes.txt", "ignored");
    }

    private BuildOptions Options(bool dryRun = false, Gender? gender = null) =>
        new(Input, Output, "mypack", gender, dryRun);

    [Fact]
    public void Build_WritesFiveMResource()
    {
        PutSampleInput();

        BuildResult result = PackBuilder.Build(Options());

        Assert.True(result.Written);
        string stream = Path.Combine(Output, "stream");
        Assert.Equal("m-jbib4", File.ReadAllText(Path.Combine(stream, "male", "mp_m_freemode_01_mypack^jbib_000_u.ydd")));
        Assert.Equal("m-jbib4-a", File.ReadAllText(Path.Combine(stream, "male", "mp_m_freemode_01_mypack^jbib_diff_000_a_uni.ytd")));
        Assert.Equal("m-jbib4-cloth", File.ReadAllText(Path.Combine(stream, "male", "mp_m_freemode_01_mypack^jbib_000_u.yld")));
        Assert.Equal("f-hat2", File.ReadAllText(Path.Combine(stream, "female", "mp_f_freemode_01_p_mypack^p_head_000.ydd")));

        byte[] maleYmt = File.ReadAllBytes(Path.Combine(stream, "male", "mp_m_freemode_01_mypack.ymt"));
        Assert.Equal("mypack", YmtCodec.YmtToXml(maleYmt).Root!.Element("dlcName")!.Value);
        Assert.True(File.Exists(Path.Combine(stream, "female", "mp_f_freemode_01_mypack.ymt")));

        string manifest = File.ReadAllText(Path.Combine(Output, "fxmanifest.lua"));
        Assert.Contains("'mp_m_freemode_01_mypack.meta'", manifest);
        Assert.Contains("'mp_f_freemode_01_mypack.meta'", manifest);
        Assert.True(File.Exists(Path.Combine(Output, "mp_f_freemode_01_mypack.meta")));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(Output)!, "*ymtpack-tmp*"));
    }

    [Fact]
    public void Build_WritesReportMappingSourcesToTargets()
    {
        PutSampleInput();

        PackBuilder.Build(Options());

        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(Path.Combine(Output, "ymtpack-report.json")));
        JsonElement files = report.RootElement.GetProperty("files");
        Assert.Contains(files.EnumerateArray(), f =>
            f.GetProperty("source").GetString() == Path.Combine("[male] tops", "jbib_004_u.ydd") &&
            f.GetProperty("target").GetString() == "stream/male/mp_m_freemode_01_mypack^jbib_000_u.ydd");
    }

    [Fact]
    public void Build_WritesCreatureMetadata_FromHeelsAndHatsFiles()
    {
        Put("female/feet_000_u.ydd", "heel");
        Put("female/feet_diff_000_a_uni.ytd", "heel-a");
        Put("female/mp_f_freemode_01_old^p_head_002.ydd", "f-hat2");
        Put("female/mp_f_freemode_01_old^p_head_diff_002_a.ytd", "f-hat2-a");
        Put("heels.txt", "female/feet_000_u.ydd 1.2\n");
        Put("hats.txt", "*p_head_002.ydd\n");

        BuildResult result = PackBuilder.Build(Options());

        Assert.True(result.Written);
        string creature = Path.Combine(Output, "stream", "female", "mp_creaturemetadata_f_mypack.ymt");
        // Stored as float32: 1.2 decodes as 1.20000005.
        float[] heelMods = YmtCodec.YmtToXml(File.ReadAllBytes(Path.Combine(Output, "stream", "female", "mp_f_freemode_01_mypack.ymt")))
            .Descendants("hash_07AE529D").Single().Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(1.2f, heelMods[4]);
        Assert.Equal("CCreatureMetaData", RbfCodec.RbfToXml(File.ReadAllBytes(creature)).Root!.Name.LocalName);
        Assert.Contains("<creatureMetaData>mp_creaturemetadata_f_mypack</creatureMetaData>",
            File.ReadAllText(Path.Combine(Output, "mp_f_freemode_01_mypack.meta")));
    }

    [Fact]
    public void BadEffectsFile_BlocksWriting()
    {
        PutSampleInput();
        Put("heels.txt", "*feet* -3\n");

        BuildResult result = PackBuilder.Build(Options());

        Assert.False(result.Written);
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Error && d.Path == "heels.txt:1");
    }

    [Fact]
    public void Build_WritesMetaWithoutByteOrderMark()
    {
        PutSampleInput();

        PackBuilder.Build(Options());

        byte[] meta = File.ReadAllBytes(Path.Combine(Output, "mp_m_freemode_01_mypack.meta"));
        Assert.Equal("<?xml"u8.ToArray(), meta[..5]);
    }

    [Fact]
    public void DryRun_WritesNothing()
    {
        PutSampleInput();

        BuildResult result = PackBuilder.Build(Options(dryRun: true));

        Assert.False(result.Written);
        Assert.Equal(2, result.Plan!.Variations.Count);
        Assert.False(Directory.Exists(Output));
    }

    [Fact]
    public void Errors_BlockWriting()
    {
        PutSampleInput();
        Put("misc/lowr_000_u.ydd", "no gender");

        BuildResult result = PackBuilder.Build(Options());

        Assert.False(result.Written);
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Error);
        Assert.False(Directory.Exists(Output));
    }

    [Fact]
    public void FallbackGender_ResolvesUnlabelledFolders()
    {
        Put("misc/lowr_000_u.ydd", "x");
        Put("misc/lowr_diff_000_a_uni.ytd", "x");

        BuildResult result = PackBuilder.Build(Options(gender: Gender.Female));

        Assert.True(result.Written);
        Assert.True(File.Exists(Path.Combine(Output, "stream", "female", "mp_f_freemode_01_mypack^lowr_000_u.ydd")));
    }

    [Fact]
    public void RefusesNonEmptyOutput()
    {
        PutSampleInput();
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "keep.txt"), "mine");

        Assert.Throws<IOException>(() => PackBuilder.Build(Options()));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(Output, "keep.txt")));
    }

    [Fact]
    public void AcceptsOutputWithTrailingSlash()
    {
        PutSampleInput();

        BuildResult result = PackBuilder.Build(Options() with { OutputDir = Output + Path.DirectorySeparatorChar });

        Assert.True(result.Written);
        Assert.True(File.Exists(Path.Combine(Output, "fxmanifest.lua")));
    }

    [Fact]
    public void RefusesSymlinkedOutput()
    {
        PutSampleInput();
        string real = Path.Combine(_work, "real");
        Directory.CreateDirectory(real);
        Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
        Directory.CreateSymbolicLink(Output, real);

        Assert.Throws<IOException>(() => PackBuilder.Build(Options()));
    }

    [Fact]
    public void RefusesOutputInsideInput()
    {
        PutSampleInput();

        Assert.Throws<ArgumentException>(() =>
            PackBuilder.Build(new BuildOptions(Input, Path.Combine(Input, "out"), "mypack", null, false)));
        if (OperatingSystem.IsMacOS())
        {
            Assert.Throws<ArgumentException>(() =>
                PackBuilder.Build(new BuildOptions(Input, Path.Combine(Input.ToUpperInvariant(), "out"), "mypack", null, false)));
        }
    }
}
