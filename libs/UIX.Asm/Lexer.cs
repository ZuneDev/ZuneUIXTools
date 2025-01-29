using Microsoft.Iris.Asm.Models;
using Sprache;
using System.Collections.Generic;
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

    public static readonly Parser<Program> Program = ParseProgram;

    private static IResult<Program> ParseProgram(IInput input)
    {
        List<IBodyItem> body = [];

        while (!input.AtEnd)
        {
            var bodyItemResult = BodyItem(input);
            input = bodyItemResult.Remainder;
            if (!bodyItemResult.WasSuccessful && !ConsumeWhitespace(input).AtEnd)
                return bodyItemResult.ForType<Program>();

            body.Add(bodyItemResult.Value);
        }

        Program program = new(body);
        return Result.Success(program, input);
    }

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

        // Capture generic types
        var genericTypeStartPosition = input.Position;
        var genericTypeMarkerResult = Parse.Char('`')(input);
        input = genericTypeMarkerResult.Remainder;
        if (genericTypeMarkerResult.WasSuccessful)
        {
            var genericTypeParameterCountResult = Parse.Number(input);
            input = genericTypeParameterCountResult.Remainder;
            if (!genericTypeParameterCountResult.WasSuccessful)
                return Result.Failure<QualifiedTypeName>(input, "Invalid generic type", ["Expected a type parameter count"]);

            var genericTypeParametersOpenBrace = Parse.Char('[')(input);
            input = genericTypeParametersOpenBrace.Remainder;
            if (genericTypeParametersOpenBrace.WasSuccessful)
            {
                var genericTypeParametersResult = Parse.AnyChar.Except(Parse.Chars('.', ']')).AtLeastOnce().Text()
                    .DelimitedBy(Parse.Char('.'))
                    .Until(Parse.Char(']'))(input);
                input = genericTypeParametersResult.Remainder;
                if (!genericTypeParametersResult.WasSuccessful)
                    return Result.Failure<QualifiedTypeName>(input, "Invalid generic type", ["Expected type parameters"]);

                var genericTypeEndPosition = input.Position;
                typeName += input.Source[genericTypeStartPosition..genericTypeEndPosition];
            }
        }

        // Capture arrays
        var arrayMarkerResult = Parse.String("[]")(input);
        input = arrayMarkerResult.Remainder;
        if (arrayMarkerResult.WasSuccessful)
            typeName += "[]";

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
