using Microsoft.Iris.Asm.Models;
using Sprache;

namespace Microsoft.Iris.Asm;

partial class Lexer
{
    private static IResult<IImportDirective> ParseImport(IInput input)
    {
        input = ConsumeWhitespace(input);

        var importDirectiveResult = Parse.String(".import")(input);
        input = importDirectiveResult.Remainder;
        if (!importDirectiveResult.WasSuccessful)
            return Result.Failure<IImportDirective>(input, "Invalid import directive", ["Expected '.import'"]);

        return ParseImportAsDirective(input);
    }

    private static IResult<IImportDirective> ParseImportAsDirective(IInput input)
    {
        if (input.Current != '-' || input.AtEnd)
            return Result.Failure<IImportDirective>(input, "Invalid import type", ["An import type must be specified"]);
        input = input.Advance();

        var importTypeResult = WordText(input);
        input = importTypeResult.Remainder;
        if (!importTypeResult.WasSuccessful)
            return Result.Failure<IImportDirective>(input, "Invalid import type", ["Expected 'ns'"]);

        var line = input.Line;
        var col = input.Column;

        IImportDirective import;
        switch (importTypeResult.Value.ToUpperInvariant())
        {
            case "NS":
                var uriResult = Uri.Token()(input);
                input = uriResult.Remainder;
                if (!uriResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid URI", ["Expected a valid URI"]);

                input = Parse.String("as").Token()(input).Remainder;

                var nameResult = AlphanumericText(input);
                input = nameResult.Remainder;
                if (!nameResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid namespace alias", ["Expected a valid namespace alias"]);

                import = new NamespaceImport(uriResult.Value, nameResult.Value)
                {
                    Line = line,
                    Column = col,
                };
                break;

            case "TYPE":
                input = ConsumeWhitespace(input);

                var typeNameResult = ParseQualifiedTypeName(input);
                input = typeNameResult.Remainder;
                if (!typeNameResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid type import", ["Expected qualified type name"]);

                import = new TypeImport(typeNameResult.Value)
                {
                    Line = line,
                    Column = col,
                };
                break;

            case "MBRS":
                input = ConsumeWhitespace(input);

                var memberTypeNameResult = ParseQualifiedTypeName(input);
                input = memberTypeNameResult.Remainder;
                if (!memberTypeNameResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid member import", ["Expected qualified type name"]);

                input = Parse.Char('{')(input).Remainder;

                var memberNamesResult = Parse.Ref(() => Identifier).DelimitedBy(Parse.Char(',').Token())(input);
                input = memberNamesResult.Remainder;
                if (!memberNamesResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid member import", ["Expected list of members to import"]);

                input = Parse.Char('}')(input).Remainder;

                import = new NamedMemberImport(memberTypeNameResult.Value, memberNamesResult.Value)
                {
                    Line = line,
                    Column = col,
                };
                break;

            case "CTOR":
                input = ConsumeWhitespace(input);

                var ctorMemberTypeNameResult = ParseQualifiedTypeName(input);
                input = ctorMemberTypeNameResult.Remainder;
                if (!ctorMemberTypeNameResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid constructor import", ["Expected qualified type name"]);

                input = Parse.Char('(')(input).Remainder;

                var ctorParameterTypesResult = Parse.Ref(() => QualifiedTypeName).DelimitedBy(Parse.Char(',').Token())(input);
                input = ctorParameterTypesResult.Remainder;
                if (!ctorParameterTypesResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid constructor import", ["Expected constructor parameter types"]);

                input = Parse.Char(')')(input).Remainder;

                import = new ConstructorImport(ctorMemberTypeNameResult.Value, ctorParameterTypesResult.Value)
                {
                    Line = line,
                    Column = col,
                };
                break;

            case "MTHD":
                input = ConsumeWhitespace(input);

                var mthdMemberTypeNameResult = ParseQualifiedTypeName(input);
                input = mthdMemberTypeNameResult.Remainder;
                if (!mthdMemberTypeNameResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid method import", ["Expected qualified type name"]);

                input = Parse.Char('.')(input).Remainder;

                var mthdNameResult = Identifier(input);
                input = mthdNameResult.Remainder;
                if (!mthdNameResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid method import", ["Expected method name"]);

                input = Parse.Char('(')(input).Remainder;

                var mthdParameterTypesResult = Parse.Ref(() => QualifiedTypeName).DelimitedBy(Parse.Char(',').Token())(input);
                input = mthdParameterTypesResult.Remainder;
                if (!mthdParameterTypesResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid constructor import", ["Expected constructor parameter types"]);

                input = Parse.Char(')')(input).Remainder;

                import = new MethodImport(mthdMemberTypeNameResult.Value, mthdNameResult.Value, mthdParameterTypesResult.Value)
                {
                    Line = line,
                    Column = col,
                };
                break;

            default:
                return Result.Failure<IImportDirective>(input, $"Unknown import type '{importTypeResult.Value}'", ["Expected 'ns', 'type'"]);
        }

        return Result.Success(import, input);
    }
}
