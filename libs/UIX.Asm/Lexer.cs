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
}
