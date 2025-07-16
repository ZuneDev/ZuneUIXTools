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
    public Instruction(OpCode opCode, IEnumerable<Operand> operands, uint offset)
        : this(opCode, null, operands, offset)
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
}
