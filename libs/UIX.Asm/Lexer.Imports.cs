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

        var importTypeResult = WordText(input);
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

            case "TYPE":
                var typePrefixResult = Identifier.Token()(input);
                input = typePrefixResult.Remainder;
                if (!typePrefixResult.WasSuccessful)
                    return Result.Failure<IImport>(input, "Invalid type import", ["Expected a valid namespace prefix"]);

                var typeNamespaceDelimitterResult = Parse.Char(':')(input);
                input = typeNamespaceDelimitterResult.Remainder;

                string typeName, typePrefix;
                if (typeNamespaceDelimitterResult.WasSuccessful)
                {
                    var typeNameResult = Identifier(input);
                    input = typeNameResult.Remainder;
                    if (!typeNameResult.WasSuccessful)
                        return Result.Failure<IImport>(input, "Invalid type import", ["Expected a valid type name"]);

                    typePrefix = typePrefixResult.Value;
                    typeName = typeNameResult.Value;
                }
                else
                {
                    typePrefix = null;
                    typeName = typePrefixResult.Value;
                }

                import = new TypeImport(typePrefix, typeName);
                break;

            default:
                return Result.Failure<IImport>(input, $"Unknown import type '{importTypeResult.Value}'", ["Expected 'ns'"]);
        }

        return Result.Success(import, input);
    }
}
