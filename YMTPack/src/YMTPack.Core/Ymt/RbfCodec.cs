using System.Xml;
using System.Xml.Linq;
using CodeWalker.GameFiles;

namespace YMTPack.Core.Ymt;

/// <summary>
/// RBF-format .ymt files (creature metadata). YMTEditor writes these with
/// XmlRbf.GetRbf + RbfFile.Save rather than as RSC7 resources.
/// </summary>
public static class RbfCodec
{
    private static readonly byte[] Magic = "RBF0"u8.ToArray();

    public static bool IsRbf(ReadOnlySpan<byte> bytes) => bytes.StartsWith(Magic);

    public static byte[] XmlToRbf(XDocument xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var xmlDocument = new XmlDocument();
        using (XmlReader reader = xml.CreateReader())
        {
            xmlDocument.Load(reader);
        }

        return CodeWalkerCall.Run(() => XmlRbf.GetRbf(xmlDocument).Save(), "build the RBF file");
    }

    public static XDocument RbfToXml(byte[] rbfBytes)
    {
        ArgumentNullException.ThrowIfNull(rbfBytes);
        if (!IsRbf(rbfBytes))
        {
            throw new InvalidDataException("Not an RBF (RBF0) file.");
        }

        var rbf = new RbfFile();
        string xml = CodeWalkerCall.Run(() => { rbf.Load(rbfBytes); return RbfXml.GetXml(rbf); }, "read the RBF file");
        return XDocument.Parse(xml);
    }
}
