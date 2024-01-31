using Microsoft.Iris.Markup;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Iris.Asm.Models;

public interface IBodyItem { }
public interface IImport { }

public record Instruction(string Mnemonic, IEnumerable<Operand> Operands) : IBodyItem
{
    public Instruction(OpCode opCode, OperationType? operationType, IEnumerable<Operand> Operands)
        : this(LexerMaps.GetMnemonic(opCode, operationType), Operands)
    {
    }
    public Instruction(OpCode opCode, IEnumerable<Operand> Operands)
        : this(opCode, null, Operands)
    {
    }

    public OpCode OpCode => LexerMaps.MnemonicToOpCode(Mnemonic);
    public OperationType? OperationType => LexerMaps.TryOperationMnemonicToType(Mnemonic);

    public override string ToString() => $"{Mnemonic} {string.Join(", ", Operands)}";
}

public record Label(string Name) : IBodyItem
{
    public override string ToString() => $"{Name}:";
}

public record Operand(object Value, string Content = null)
{
    public override string ToString() => Content ?? Value.ToString();
}

public record Program(IEnumerable<IImport> Imports, IEnumerable<IBodyItem> Body)
{
    public override string ToString()
    {
        StringBuilder sb = new();

        sb.AppendJoin("\r\n", Imports.Select(i => i.ToString()));
        sb.AppendJoin("\r\n", Body.Select(b => b.ToString()));

        return sb.ToString();
    }
}
