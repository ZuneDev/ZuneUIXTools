using Microsoft.Iris.Asm.Models;
using Sprache;
using System.Collections.Generic;

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
            Operand[] operands = null;
            var endOfInstructionResult = StatementEnd(input);
            input = endOfInstructionResult.Remainder;

            if (!endOfInstructionResult.WasSuccessful)
            {
                input = ConsumeWhitespace(input);

                var opCode = InstructionSet.MnemonicToOpCode(identifier);
                var schema = InstructionSet.InstructionSchema[opCode];
                operands = new Operand[schema.Length];

                for (int operandIndex = 0; operandIndex < schema.Length; operandIndex++)
                {
                    Operand operand;
                    var operandType = schema[operandIndex];

                    var operandConstPrefixResult = Parse.Char('@')(input);
                    input = operandConstPrefixResult.Remainder;
                    if (operandConstPrefixResult.WasSuccessful)
                    {
                        if (!OperandLiteral.IsIndex(operandType))
                            return Result.Failure<IBodyItem>(input, "Invalid instruction", [$"Constant references are not allowed for this operand, expected a {operandType}"]);

                        var operandConstResult = Identifier(input);
                        input = operandConstResult.Remainder;
                        if (!operandConstResult.WasSuccessful)
                            return Result.Failure<IBodyItem>(input, "Invalid instruction", ["Invalid constant name"]);

                        operand = new OperandReference(operandConstResult.Value)
                        {
                            Line = line,
                        };
                    }
                    else
                    {
                        var operandContentResult = AlphanumericText(input);
                        input = operandContentResult.Remainder;
                        if (!operandContentResult.WasSuccessful)
                            return Result.Failure<IBodyItem>(input, "Invalid instruction", ["Invalid operand"]);

                        var operandContent = operandContentResult.Value;
                        object operandValue = OperandLiteral.ReduceDataType(operandType) switch
                        {
                            LiteralDataType.Byte => byte.Parse(operandContent),
                            LiteralDataType.UInt16 => ushort.Parse(operandContent),
                            LiteralDataType.UInt32 => uint.Parse(operandContent),
                            LiteralDataType.Int32 => int.Parse(operandContent),
                            LiteralDataType.Bytes or _ => operandContent,
                        };

                        operand = new OperandLiteral(operandValue, operandType, operandContent)
                        {
                            Line = line,
                        };
                    }

                    operands[operandIndex] = operand;

                    input = Parse.Char(',').Token()(input).Remainder;
                }
            }

            bodyItem = new Instruction(identifier, operands ?? [])
            {
                Line = line,
                Column = column,
            };
        }

        return Result.Success(bodyItem, input);
    }
}
