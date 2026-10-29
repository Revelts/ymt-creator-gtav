using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace YMTPack.Core;

public static class XmlFiles
{
    /// <summary>Game data files ship as plain UTF-8; XDocument.Save would add a byte-order mark.</summary>
    public static void SaveWithoutBom(XDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), Indent = true };
        using XmlWriter writer = XmlWriter.Create(path, settings);
        document.Save(writer);
    }
}
