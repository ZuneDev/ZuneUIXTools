using Microsoft.Iris.Asm.Models;
using Sprache;
using System.Linq;

namespace Microsoft.Iris.Asm;

public static partial class Lexer
{
    public static readonly Parser<string> AlphanumericText = Parse.LetterOrDigit.AtLeastOnce().Text();

    public static readonly Parser<string> WordText = Parse.Letter.AtLeastOnce().Text();

    public static readonly Parser<string> WholeNumber = Parse.Digit.AtLeastOnce().Text();

    public static readonly Parser<string> Identifier = Parse.LetterOrDigit.Or(Parse.Chars('_', '-')).AtLeastOnce().Text();

    public static readonly Parser<string> Uri = Parse.LetterOrDigit.Or(Parse.Chars(":/!._-")).AtLeastOnce().Text();

    // https://github.com/apexsharp/apexparser/blob/4eb5983c657b0e6c49ed47bc42a0346e80f9e26d/ApexSharp.ApexParser/Parser/ApexGrammar.cs#L360C9-L367C64
    public static readonly Parser<string> StringLiteral =
        from leading in Parse.WhiteSpace.Many()
        from openQuote in Parse.Char('"')
        from fragments in Parse.Char('\\').Then(_ => Parse.AnyChar.Select(c => $"\\{c}"))
            .Or(Parse.CharExcept("\\\"").Many().Text()).Many()
        from closeQuote in Parse.Char('"')
        from trailing in Parse.WhiteSpace.Many()
        select $"\"{string.Join(string.Empty, fragments)}\"";

    public static readonly Parser<string> StatementEnd = Parse.Char(';').Return(";").Or(Parse.LineTerminator);

    public static readonly Parser<QualifiedTypeName> QualifiedTypeName = ParseQualifiedTypeName;

    public static readonly Parser<IImportDirective> Import = ParseImport;

    public static readonly Parser<IDirective> Directive = ParseDirective;

    public static readonly Parser<IBodyItem> BodyItem = ParseBodyItem;

    public static readonly Parser<Program> Program =
        from body in BodyItem.Many()
        select new Program(body);

    private static IInput ConsumeWhitespace(IInput input) => Parse.WhiteSpace.Many()(input).Remainder;

    private static IResult<QualifiedTypeName> ParseQualifiedTypeName(IInput input)
    {
        var line = input.Line;
        var col = input.Column;

        var typePrefixResult = Identifier(input);
        input = typePrefixResult.Remainder;
        if (!typePrefixResult.WasSuccessful)
            return Result.Failure<QualifiedTypeName>(input, "Invalid type name", ["Expected a valid namespace prefix"]);

        var typeNamespaceDelimitterResult = Parse.Char(':')(input);
        input = typeNamespaceDelimitterResult.Remainder;

        string typeName, typePrefix;
        if (typeNamespaceDelimitterResult.WasSuccessful)
        {
            var typeNameResult = Identifier(input);
            input = typeNameResult.Remainder;
            if (!typeNameResult.WasSuccessful)
                return Result.Failure<QualifiedTypeName>(input, "Invalid type name", ["Expected a valid type name"]);

            typePrefix = typePrefixResult.Value;
            typeName = typeNameResult.Value;
        }
        else
        {
            typePrefix = null;
            typeName = typePrefixResult.Value;
        }

        QualifiedTypeName qualifiedName = new(typePrefix, typeName)
        {
            Line = line,
            Column = col,
        };

        return Result.Success(qualifiedName, input);
    }

    private static Parser<string> ExpressionInBraces(Parser<string> parser, char open = '(', char close = ')') =>
        from openBrace in Parse.Char(open).Token()
        from expression in parser.Optional()
        from closeBrace in Parse.Char(close).Token()
        select expression.GetOrElse(string.Empty).Trim();
}
