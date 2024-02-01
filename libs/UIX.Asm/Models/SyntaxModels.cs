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

    public override string ToString() => Operands.Any()
        ? $"{Mnemonic} {string.Join(", ", Operands)}"
        : Mnemonic;
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
        const string lineEnding = "\r\n";
        const string indent = "    ";
        StringBuilder sb = new();

        sb.AppendJoin(lineEnding, Imports.Select(i => i.ToString()));
        sb.Append(lineEnding);
        sb.Append(lineEnding);

        foreach (var bodyItem in Body)
        {
            if (bodyItem is Instruction)
                sb.Append(indent);

            sb.Append(bodyItem.ToString());
            sb.Append(lineEnding);
        }

        return sb.ToString();
    }
}
