using Microsoft.Iris.Markup;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Iris.Asm.Models;

[DebuggerDisplay("{ToString()} " + DebuggerDisplay)]
public record Instruction(string Mnemonic, IEnumerable<Operand> Operands, uint Offset) : CodeItem
{
    public Instruction(OpCode opCode, OperationType? operationType, IEnumerable<Operand> operands, uint offset)
        : this(InstructionSet.GetMnemonic(opCode, operationType), operands, offset)
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

    public static Instruction Create(OpCode opCode, IEnumerable<Operand> operands, uint offset)
    {
        OperationType? operationType = null;

        if (opCode is OpCode.Operation)
        {
            var operandsList = operands.ToList();
            if (operandsList.Count != 2)
                throw new System.ArgumentException("Operation instructions must have exactly two operands: the operation host index and the operation type.", nameof(operands));

            var operandOperationValue = operandsList[1].Value;
            if (operandOperationValue is byte or Markup.OperationType)
                operationType = (OperationType?)(int?)(byte?)operandOperationValue;

            operandsList.RemoveAt(1);
            operands = operandsList;
        }

        return new(opCode, operationType, operands, offset);
    }
}
