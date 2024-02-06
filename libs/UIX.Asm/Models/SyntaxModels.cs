using Microsoft.Iris.Markup;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Microsoft.Iris.Asm.Models;

[DebuggerDisplay("{Name} " + DebuggerDisplay)]
public record Label(string Name) : BodyItem
{
    public override string ToString() => $"{Name}:";
}

public enum OperandDataType : byte
{
    Byte,
    UInt16,
    UInt32,
    Int32,
    Bytes,
}

public record Operand(object Value, OperandDataType DataType, string Content = null) : AsmItem
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
