﻿using Microsoft.Iris.Asm.Models;
using Sprache;
using System.Linq;

namespace Microsoft.Iris.Asm;

partial class Lexer
{
    private static IResult<IDirective> ParseDirective(IInput input)
    {
        input = ConsumeWhitespace(input);
        var line = input.Line;
        var column = input.Column;

        var directiveBeginResult = Parse.Char('.')(input);
        input = directiveBeginResult.Remainder;
        if (!directiveBeginResult.WasSuccessful)
            return Result.Failure<IDirective>(input, "Invalid directive", ["Expected '.', followed by an identifier"]);

        var directiveIdResult = WordText(input);
        input = directiveIdResult.Remainder;
        if (!directiveIdResult.WasSuccessful)
            return Result.Failure<IDirective>(input, "Invalid directive", ["Expected a valid directive name"]);

        IDirective directive;
        switch (directiveIdResult.Value.ToUpperInvariant())
        {
            case "IMPORT":
                return ParseImportAsDirective(input);

            case "SECTION":
                if (StatementEnd(input).WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid section directive", ["Expected a section name"]);

                var sectionNameResult = Parse.Letter.AtLeastOnce().Token().Text()(input);
                input = sectionNameResult.Remainder;
                if (!sectionNameResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid section name", ["Expected a section name containing only letters"]);

                directive = new SectionDirective(sectionNameResult.Value)
                {
                    Line = line,
                    Column = column,
                };
                break;

            case "CONSTANT":
                if (StatementEnd(input).WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected a consant declaration"]);

                var constNameResult = Identifier.Token()(input);
                input = constNameResult.Remainder;
                if (!constNameResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected a name for the constant"]);

                input = Parse.Char('=').Token()(input).Remainder;

                var typeNameResult = QualifiedTypeName.Token()(input);
                input = typeNameResult.Remainder;
                if (!typeNameResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected qualified name of type to construct"]);

                var openBracketResult = Parse.Char('(').Token()(input);
                input = openBracketResult.Remainder;
                if (!openBracketResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected '('"]);

                var contentResult = Parse.CharExcept(')').AtLeastOnce().Text().Token()(input);
                input = contentResult.Remainder;
                if (!contentResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected constant value"]);

                var closeBracketResult = Parse.Char(')').Token()(input);
                input = closeBracketResult.Remainder;
                if (!closeBracketResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected ')'"]);

                directive = new ConstantDirective(constNameResult.Value, typeNameResult.Value, contentResult.Value)
                {
                    Line = line,
                    Column = column,
                };
                break;

            case "EXPORT":
                if (StatementEnd(input).WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid export directive", ["Expected export information"]);

                var labelPrefixResult = Identifier.Token()(input);
                input = labelPrefixResult.Remainder;
                if (!labelPrefixResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid export directive", ["Expected prefix of labels to export"]);

                var listenerCountResult = WholeNumber.Token()(input);
                input = listenerCountResult.Remainder;
                if (!listenerCountResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid export directive", ["Expected listener count"]);

                if (!uint.TryParse(listenerCountResult.Value, out var listenerCount))
                    return Result.Failure<IDirective>(input, "Invalid export directive", ["Expected export listener count to be an unsigned integer"]);

                var baseTypeNameResult = AlphanumericText.Token()(input);
                input = baseTypeNameResult.Remainder;
                if (!baseTypeNameResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid export directive", ["Expected base type name"]);

                var labelPrefix = labelPrefixResult.Value;
                var baseTypeName = baseTypeNameResult.Value;
                directive = new ExportDirective(labelPrefix, listenerCount, baseTypeName)
                {
                    Line = line,
                    Column = column,
                };
                break;

            default:
                return Result.Failure<IDirective>(input, $"Unknown import type '{directiveIdResult.Value}'", ["Expected 'export', 'import', or 'section'"]);
        }

        return Result.Success(directive, input);
    }
}
