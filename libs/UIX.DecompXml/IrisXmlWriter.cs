using Microsoft.Iris.Debug.Symbols;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Iris.DecompXml;

internal class IrisXmlWriter : IDisposable
{
    private readonly IrisTextWriter _textWriter;
    private readonly XmlWriter _xmlWriter;
    private readonly XDocument _xDoc;
    private readonly FileDebugSymbols _symbols;

    public IrisXmlWriter(XDocument xDoc, FileDebugSymbols symbols)
    {
        _xDoc = xDoc;
        _symbols = symbols;

        _textWriter = new();
        _xmlWriter = XmlWriter.Create(_textWriter, new XmlWriterSettings
        {
            Indent = true,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            Encoding = Encoding.UTF8,
        });
    }

    public void Dispose() => _xmlWriter.Dispose();

    public string WriteToString()
    {
        Write();
        return _textWriter.ToString();
    }

    private void Write()
    {
        if (_symbols is null)
        {
            // If we're not generating symbols, there's no reason to
            // use our custom implementation.
            _xDoc.WriteTo(_xmlWriter);
            _xmlWriter.Flush();
            return;
        }

        _xmlWriter.WriteStartDocument();

        foreach (var node in _xDoc.Nodes())
            WriteXObject(node);

        _xmlWriter.WriteEndDocument();
        _xmlWriter.Flush();
    }

    private void WriteXObject(XObject obj)
    {
        _xmlWriter.Flush();
        var startPos = _textWriter.Position;

        if (obj is XElement element)
        {
            _xmlWriter.WriteStartElement(element.Name.LocalName, element.Name.NamespaceName);

            foreach (var attribute in element.Attributes())
                WriteXObject(attribute);

            foreach (var childNode in element.Nodes())
                WriteXObject(childNode);

            _xmlWriter.WriteEndElement();
        }
        else if (obj is XIrisScriptContent script)
        {
            _xmlWriter.WriteCData(script.Value);
        }
        else if (obj is XCData cdata)
        {
            _xmlWriter.WriteCData(cdata.Value);
        }
        else if (obj is XAttribute attribute)
        {
            _xmlWriter.WriteAttributeString(attribute.Name.LocalName, attribute.Name.NamespaceName, attribute.Value);
        }


        var offset = obj.GetOffset();
        if (offset is not null)
        {
            _xmlWriter.Flush();
            var endPos = _textWriter.Position;

            var span = new SourceSpan(startPos, endPos);
            _symbols.SourceMap.Xml[offset.Value] = span;
        }
    }
}
