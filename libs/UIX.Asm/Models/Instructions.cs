using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Iris.Asm.Models;

[DebuggerDisplay("{ToString()} " + DebuggerDisplay)]
public record Instruction(string Mnemonic, IEnumerable<Operand> Operands) : BodyItem
{
    public Instruction(OpCode opCode, OperationType? operationType, IEnumerable<Operand> Operands)
        : this(InstructionSet.GetMnemonic(opCode, operationType), Operands)
    {
    }
    public Instruction(OpCode opCode, IEnumerable<Operand> Operands)
        : this(opCode, null, Operands)
    {
    }

    public OpCode OpCode => InstructionSet.MnemonicToOpCode(Mnemonic);
    public OperationType? OperationType => InstructionSet.TryOperationMnemonicToType(Mnemonic);

    public override string ToString() => ToString(true);

    public string ToString(bool uppercase)
    {
        var mnemonic = uppercase ? Mnemonic.ToUpperInvariant() : Mnemonic.ToLowerInvariant();
        return Operands.Any()
            ? $"{mnemonic} {string.Join(", ", Operands)}"
            : mnemonic;
    }

    public static Instruction CreateParamless(OpCode opCode) => new(opCode, Array.Empty<Operand>());

    public static Instruction CreateUInt16(OpCode opCode, ushort operand1)
        => new(opCode, [new(operand1, OperandDataType.UInt16)]);

    public static Instruction CreateUInt32(OpCode opCode, uint operand1)
        => new(opCode, [new(operand1, OperandDataType.UInt32)]);

    public static Instruction CreateInt32(OpCode opCode, int operand1)
        => new(opCode, [new(operand1, OperandDataType.Int32)]);

    public static Instruction CreateUInt16UInt16(OpCode opCode, ushort operand1, ushort operand2)
        => new(opCode, [new(operand1, OperandDataType.UInt16), new(operand2, OperandDataType.UInt16)]);

    public static Instruction CreateWithSchema(OpCode opCode, params object[] operands)
    {
        var schema = InstructionSet.InstructionSchema[opCode];
        if (operands.Length != schema.Length)
            throw new ArgumentException($"{opCode} requires {schema.Length} operands, got {operands.Length}");

        var operandModels = new Operand[operands.Length];
        for (int i = 0; i < schema.Length; i++)
        {
            var operandValue = operands[i];
            // Should we verify types?

            operandModels[i] = new(operandValue, schema[i]);
        }

        var operationType = operands.Length > 0 ? operands[0] as OperationType? : null;

        return new Instruction(opCode, operationType, operandModels);
    }
}
