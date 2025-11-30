using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Xml.Linq;

namespace Microsoft.Iris.DecompXml;

internal class XIrisScriptContent : XCData
{
    public SyntaxNode Syntax { get; }

    public SourceText Text { get; }

    public XIrisScriptContent(SyntaxNode syntax) : base("")
    {
        Syntax = syntax.NormalizeWhitespace();
        Text = Syntax.GetText();
        Value = $"{Environment.NewLine}{Text}{Environment.NewLine}";
    }
}
