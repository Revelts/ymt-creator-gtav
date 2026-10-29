using System.Buffers.Binary;
using System.Xml;
using System.Xml.Linq;
using CodeWalker.GameFiles;

namespace YMTPack.Core.Ymt;

/// <summary>
/// Converts between CodeWalker-style YMT XML and binary RSC7 .ymt resources.
/// Mirrors YMTEditor's MainWindow save/open paths, which delegate to CodeWalker.Core.
/// </summary>
public static class YmtCodec
{
    private const uint Rsc7Magic = 0x37435352; // "RSC7" little-endian
    private const int MetaResourceVersion = 2;
    private const int Rsc7HeaderSize = 16;

    public static byte[] XmlToYmt(XDocument xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var xmlDocument = new XmlDocument();
        using (XmlReader reader = xml.CreateReader())
        {
            xmlDocument.Load(reader);
        }

        Meta meta = CodeWalkerCall.Run(() => XmlMeta.GetMeta(xmlDocument), "build Meta from the YMT XML")
            ?? throw new InvalidDataException("CodeWalker could not build Meta from the YMT XML.");
        return CodeWalkerCall.Run(() => ResourceBuilder.Build(meta, MetaResourceVersion), "build the YMT resource");
    }

    /// <param name="knownStrings">
    /// Strings to make resolvable in the output (e.g. the DLC name). Hash fields such as
    /// dlcName otherwise print as hash_XXXXXXXX unless this process has seen the string.
    /// </param>
    public static XDocument YmtToXml(byte[] ymtBytes, IEnumerable<string>? knownStrings = null)
    {
        ArgumentNullException.ThrowIfNull(ymtBytes);
        foreach (string s in knownStrings ?? [])
        {
            JenkIndex.Ensure(s);
        }

        if (ymtBytes.Length < Rsc7HeaderSize || BinaryPrimitives.ReadUInt32LittleEndian(ymtBytes) != Rsc7Magic)
        {
            throw new InvalidDataException("Not an RSC7 resource (.ymt) file.");
        }

        var pedFile = new PedFile();
        CodeWalkerCall.Run(() => { RpfFile.LoadResourceFile(pedFile, ymtBytes, MetaResourceVersion); return true; }, "read the YMT resource");
        if (pedFile.Meta is null)
        {
            throw new InvalidDataException("Resource did not contain ped variation Meta data.");
        }

        return XDocument.Parse(CodeWalkerCall.Run(() => MetaXml.GetXml(pedFile.Meta), "convert YMT Meta to XML"));
    }

    /// <summary>"mp_f_freemode_01_mypack.ymt" → "mypack"; other names → the bare file name.</summary>
    public static string GuessDlcName(string ymtPath)
    {
        ArgumentNullException.ThrowIfNull(ymtPath);
        string name = Path.GetFileNameWithoutExtension(ymtPath).ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.Replace(name, "^mp_[mf]_freemode_01_", "");
    }
}
