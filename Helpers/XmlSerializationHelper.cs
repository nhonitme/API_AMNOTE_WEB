using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Helpers
{
    internal static class XmlSerializationHelper
    {
        public const string Utf8Declaration = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";

        public static string Serialize(XDocument document)
        {
            if (document == null)
                return Utf8Declaration;

            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, CreateWriterSettings()))
            {
                document.Save(writer);
                writer.Flush();
            }

            return EntitizeWhitespace(Utf8Declaration + builder.ToString());
        }

        public static string Serialize(XElement element)
        {
            if (element == null)
                return Utf8Declaration;

            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, CreateWriterSettings()))
            {
                element.WriteTo(writer);
                writer.Flush();
            }

            return EntitizeWhitespace(Utf8Declaration + builder.ToString());
        }

        private static string EntitizeWhitespace(string xml)
            => xml
                .Replace("\r", "&#13;")
                .Replace("\n", "&#10;")
                .Replace("\t", "&#9;");

        private static XmlWriterSettings CreateWriterSettings()
        {
            return new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                Encoding = new UTF8Encoding(false)
            };
        }
    }
}
