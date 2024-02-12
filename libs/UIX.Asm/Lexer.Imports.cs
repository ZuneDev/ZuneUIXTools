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

                import = new NamespaceImport(uriResult.Value, nameResult.Value);
                break;

            case "TYPE":
                input = ConsumeWhitespace(input);

                var typeNameResult = ParseQualifiedTypeName(input);
                input = typeNameResult.Remainder;
                if (!typeNameResult.WasSuccessful)
                    return Result.Failure<IImportDirective>(input, "Invalid type import", ["Expected qualified type name"]);

                import = new TypeImport(typeNameResult.Value);
                break;

            default:
                return Result.Failure<IImportDirective>(input, $"Unknown import type '{importTypeResult.Value}'", ["Expected 'ns', 'type'"]);
        }

        return Result.Success(import, input);
    }
}
