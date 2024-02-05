using Microsoft.Iris.Asm.Models;
using Sprache;

namespace Microsoft.Iris.Asm;

partial class Lexer
{
    private static IResult<IImport> ParseImport(IInput input)
    {
        input = ConsumeWhitespace(input);

        var importDirectiveResult = Parse.String(".import")(input);
        input = importDirectiveResult.Remainder;
        if (!importDirectiveResult.WasSuccessful)
            return Result.Failure<IImport>(input, "Invalid import directive", ["Expected '.import'"]);

        return ParseImportAsDirective(input);
    }

    private static IResult<IImport> ParseImportAsDirective(IInput input)
    {
        if (input.Current != '-' || input.AtEnd)
            return Result.Failure<IImport>(input, "Invalid import type", ["An import type must be specified"]);
        input = input.Advance();

        var importTypeResult = Parse.Letter.AtLeastOnce().Text()(input);
        input = importTypeResult.Remainder;
        if (!importTypeResult.WasSuccessful)
            return Result.Failure<IImport>(input, "Invalid import type", ["Expected 'ns'"]);

        IImport import;
        switch (importTypeResult.Value.ToUpperInvariant())
        {
            case "NS":
                var uriResult = Uri.Token()(input);
                input = uriResult.Remainder;
                if (!uriResult.WasSuccessful)
                    return Result.Failure<IImport>(input, "Invalid URI", ["Expected a valid URI"]);

                input = Parse.String("as").Token()(input).Remainder;

                var nameResult = AlphanumericText(input);
                input = nameResult.Remainder;
                if (!nameResult.WasSuccessful)
                    return Result.Failure<IImport>(input, "Invalid namespace alias", ["Expected a valid namespace alias"]);

                import = new NamespaceImport(uriResult.Value, nameResult.Value);
                break;

            default:
                return Result.Failure<IImport>(input, $"Unknown import type '{importTypeResult.Value}'", ["Expected 'ns'"]);
        }

        return Result.Success(import, input);
    }
}
