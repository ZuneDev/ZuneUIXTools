using Microsoft.CodeAnalysis;
using System;
using System.Xml.Linq;

namespace Microsoft.Iris.DecompXml;

internal class XIrisScriptContent : XCData
{
    public SyntaxNode Syntax { get; }

    public XIrisScriptContent(SyntaxNode syntax) : base("")
    {
        Syntax = syntax.NormalizeWhitespace();
        Value = $"{Environment.NewLine}{Syntax.GetText()}{Environment.NewLine}";
    }
}
