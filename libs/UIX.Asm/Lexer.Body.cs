using Microsoft.Iris.Asm.Models;
using Sprache;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Iris.Asm;

partial class Lexer
{
    private static IResult<IBodyItem> ParseBodyItem(IInput input)
    {
        input = ConsumeWhitespace(input);
        var line = input.Line;
        var column = input.Column;

        var identifierResult = Parse.Letter.AtLeastOnce().Text().Invoke(input);
        if (!identifierResult.WasSuccessful)
            return Result.Failure<Instruction>(input, "Invalid code", []);

        input = identifierResult.Remainder;
        var identifier = identifierResult.Value;
        IBodyItem bodyItem;

        if (!input.AtEnd && input.Current == ':')
        {
            input = input.Advance();
            bodyItem = new Label(identifier)
            {
                Line = line, Column = column,
            };
            input = StatementEnd(input).Remainder;
        }
        else
        {
            List<Operand> operands = new();
            var endOfInstructionResult = StatementEnd(input);
            input = endOfInstructionResult.Remainder;

            if (!endOfInstructionResult.WasSuccessful)
            {
                input = ConsumeWhitespace(input);

                var operandsResult = Parse.Ref(() => AlphanumericText).DelimitedBy(Parse.Char(',').Token())(input);
                operands.AddRange(operandsResult.Value.Select(s => new Operand(s) { Line = line }));
                input = operandsResult.Remainder;
            }

            bodyItem = new Instruction(identifier, operands)
            {
                Line = line,
                Column = column,
            };
        }

        return Result.Success(bodyItem, input);
    }

}
