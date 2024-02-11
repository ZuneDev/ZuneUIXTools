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

        var directiveResult = ParseDirective(input);
        if (directiveResult.WasSuccessful)
        {
            input = directiveResult.Remainder;
            var directive = directiveResult.Value;
            if (directive is not IBodyItem directiveBodyItem)
                return Result.Failure<IBodyItem>(input, "Invalid code", [$"The {directive.Identifier} directive cannot be placed in the body of a program"]);

            directive.Line = line;
            directive.Column = column;
            return Result.Success(directiveBodyItem, input);
        }

        var identifierResult = Identifier.Invoke(input);
        if (!identifierResult.WasSuccessful)
            return Result.Failure<IBodyItem>(input, "Invalid code", []);

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
                input = operandsResult.Remainder;

                var opCode = InstructionSet.MnemonicToOpCode(identifier);
                var schema = InstructionSet.InstructionSchema[opCode];

                int schemaIndex = 0;
                foreach (var operandContent in operandsResult.Value)
                {
                    var operandType = schema[schemaIndex++];

                    object operandValue = operandType switch
                    {
                        OperandDataType.Byte => byte.Parse(operandContent),
                        OperandDataType.UInt16 => ushort.Parse(operandContent),
                        OperandDataType.UInt32 => uint.Parse(operandContent),
                        OperandDataType.Int32 => int.Parse(operandContent),
                        OperandDataType.Bytes or _ => operandContent,
                    };

                    operands.Add(new(operandValue, operandType, operandContent)
                    {
                        Line = line,
                    });
                }
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
