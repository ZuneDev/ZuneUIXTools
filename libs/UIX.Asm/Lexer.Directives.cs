using Microsoft.Iris.Asm.Models;
using Sprache;

namespace Microsoft.Iris.Asm;

partial class Lexer
{
    private static IResult<IDirective> ParseDirective(IInput input)
    {
        input = ConsumeWhitespace(input);
        var line = input.Line;
        var column = input.Column;

        var directiveBeginResult = Parse.String(".")(input);
        input = directiveBeginResult.Remainder;
        if (!directiveBeginResult.WasSuccessful)
            return Result.Failure<IDirective>(input, "Invalid directive", ["Expected '.', followed by an identifier"]);

        var directiveIdResult = Parse.Letter.AtLeastOnce().Text()(input);
        input = directiveIdResult.Remainder;
        if (!directiveIdResult.WasSuccessful)
            return Result.Failure<IDirective>(input, "Invalid directive", ["Expected an identifier"]);

        IDirective directive;
        switch (directiveIdResult.Value.ToUpperInvariant())
        {
            case "IMPORT":
                return ParseImportAsDirective(input);

            case "SECTION":
                if (StatementEnd(input).WasSuccessful)
                    return Result.Failure<IImport>(input, "Invalid section directive", ["Expected a section name"]);

                var sectionNameResult = Parse.Letter.AtLeastOnce().Token().Text()(input);
                input = sectionNameResult.Remainder;
                if (!sectionNameResult.WasSuccessful)
                    return Result.Failure<IImport>(input, "Invalid section name", ["Expected a section name containing only letters"]);

                directive = new SectionDirective(sectionNameResult.Value)
                {
                    Line = line,
                    Column = column,
                };
                break;

            default:
                return Result.Failure<IImport>(input, $"Unknown import type '{directiveIdResult.Value}'", ["Expected 'import' or 'section'"]);
        }

        return Result.Success(directive, input);
    }
}
