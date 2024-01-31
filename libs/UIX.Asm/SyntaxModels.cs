using Microsoft.Iris.Markup;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Iris.Asm;

public interface IBodyItem { }
public interface IImport { }

public record Instruction(string Mnemonic, IEnumerable<Operand> Operands) : IBodyItem
{
    public OpCode OpCode => LexerMaps.MnemonicToOpCode(Mnemonic);
    public OperationType? OperationType => LexerMaps.TryOperationMnemonicToType(Mnemonic);

    public override string ToString() => $"{Mnemonic} {string.Join(", ", Operands)}";
}

public record Label(string Name) : IBodyItem
{
    public override string ToString() => $"{Name}:";
}

public record Operand(string Content)
{
    public override string ToString() => Content;
}

public record NamespaceImport(string Uri, string Name) : IImport
{
    public override string ToString() => $".import-ns {Uri} as {Name}";
}

public record Program(IEnumerable<IImport> Imports, IEnumerable<IBodyItem> Body)
{
    public override string ToString() => string.Join("\r\n", Imports.Select(i => i.ToString()).Concat(Body.Select(b => b.ToString())));
}
