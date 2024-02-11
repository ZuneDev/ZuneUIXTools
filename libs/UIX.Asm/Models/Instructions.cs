using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Iris.Asm.Models;

[DebuggerDisplay("{ToString()} " + DebuggerDisplay)]
public record Instruction(string Mnemonic, IEnumerable<Operand> Operands) : CodeItem
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
