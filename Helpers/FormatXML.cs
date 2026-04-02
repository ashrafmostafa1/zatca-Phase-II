using System;
using System.Text;
using System.Xml;

namespace Zatca_Phase_II.Helpers;

public static class FormatXML
{
    public static string ConvertXmlToBase64(XmlDocument xmlDoc)
    {
        using (var stringWriter = new System.IO.StringWriter())
        using (var xmlTextWriter = new XmlTextWriter(stringWriter))
        {
            xmlTextWriter.Formatting = Formatting.None; // Ensure compact format
            xmlDoc.WriteTo(xmlTextWriter);
            xmlTextWriter.Flush();
            string xmlString = stringWriter.GetStringBuilder().ToString();

            // Convert to Base64
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlString));
        }
    }

    public static XmlDocument GetFormattedXmlDocument(XmlDocument xmlDoc)
    {
        XmlWriterSettings settings = new XmlWriterSettings
        {
            Indent = true, // Enable indentation
            IndentChars = "    ", // Use 4 spaces for indentation
            NewLineOnAttributes = false,
            Encoding = new UTF8Encoding(false), // Prevent BOM in UTF-8
        };

        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (XmlWriter writer = XmlWriter.Create(memoryStream, settings))
            {
                xmlDoc.Save(writer);
            }

            // Rewind the stream and load it back into a new XmlDocument
            memoryStream.Position = 0;
            XmlDocument formattedXmlDoc = new XmlDocument();
            formattedXmlDoc.PreserveWhitespace = true;
            formattedXmlDoc.Load(memoryStream);
            LogsFile.MessageZatca($"Create Default Xml");
            return formattedXmlDoc;
        }
    }

    public static XmlDocument PrettyXml(XmlDocument inputXml)
    {
        XmlDocument formattedXml = new XmlDocument() { PreserveWhitespace = true };

        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (
                StreamWriter streamWriter = new StreamWriter(memoryStream, new UTF8Encoding(false))
            ) // false to exclude BOM
            {
                XmlWriterSettings settings = new XmlWriterSettings()
                {
                    Indent = true,
                    IndentChars = "    ",
                    OmitXmlDeclaration = false,
                    Encoding = Encoding.UTF8,
                };

                using (XmlWriter xmlWriter = XmlWriter.Create(streamWriter, settings))
                {
                    inputXml.Save(xmlWriter);
                }
            }

            // Get the UTF-8 encoded string from the MemoryStream
            string utf8Xml = Encoding.UTF8.GetString(memoryStream.ToArray()).Trim();

            // Load the UTF-8 XML string into the new XmlDocument
            formattedXml.LoadXml(utf8Xml);
        }

        return formattedXml;
    }
}
