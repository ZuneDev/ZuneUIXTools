using Microsoft.Iris.Asm.Models;
using Sprache;
using System.Linq;

namespace Microsoft.Iris.Asm;

public static partial class Lexer
{
    public static readonly Parser<string> AlphanumericText = Parse.LetterOrDigit.AtLeastOnce().Text();

    public static readonly Parser<string> Uri = Parse.LetterOrDigit.Or(Parse.Chars(":/!._-")).AtLeastOnce().Text();

    public static readonly Parser<string> StatementEnd = Parse.Char(';').Return(";").Or(Parse.LineTerminator);

    public static readonly Parser<string> SectionDirective =
        from _ in Parse.String(".section").Token()
        from sectionId in Parse.Letter.AtLeastOnce().Text()
        select sectionId;

    public static readonly Parser<IImport> Import = ParseImport;

    public static readonly Parser<IBodyItem> BodyItem = ParseBodyItem;

    public static readonly Parser<Program> Program =
        from imports in Import.Token().Many()
        from body in BodyItem.Many()
        select new Program(imports, body);

    private static IInput ConsumeWhitespace(IInput input) => Parse.WhiteSpace.Many()(input).Remainder;
}
