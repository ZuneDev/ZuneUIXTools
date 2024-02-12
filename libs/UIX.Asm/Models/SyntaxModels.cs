using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Microsoft.Iris.Asm.Models;

[DebuggerDisplay("{Name} " + DebuggerDisplay)]
public record Label(string Name) : CodeItem
{
    public override string ToString() => $"{Name}:";
}

public record QualifiedTypeName(string NamespacePrefix, string TypeName) : AsmItem
{
    public override string ToString()
    {
        if (NamespacePrefix is null)
            return TypeName;
        else
            return $"{NamespacePrefix}:{TypeName}";
    }
}

public record Program
{
    public Program(IEnumerable<IBodyItem> body)
    {
        Body = body.Cached();

        Directives = body.OfType<IDirective>().Cached();
        Imports = Directives.OfType<IImportDirective>().Cached();
        Exports = Directives.OfType<ExportDirective>().Cached();

        Code = body.OfType<ICodeItem>().Cached();
    }

    public IEnumerable<IBodyItem> Body { get; }

    public IEnumerable<IDirective> Directives { get; }
    public IEnumerable<IImportDirective> Imports { get; }
    public IEnumerable<ExportDirective> Exports { get; }

    public IEnumerable<ICodeItem> Code { get; }

    public override string ToString()
    {
        const string lineEnding = "\r\n";
        const string indent = "    ";
        StringBuilder sb = new();

        sb.AppendJoin(lineEnding, Exports.Select(i => i.ToString()));
        sb.Append(lineEnding);
        sb.Append(lineEnding);
        sb.AppendJoin(lineEnding, Imports.Select(i => i.ToString()));
        sb.Append(lineEnding);
        sb.Append(lineEnding);
        sb.AppendJoin(lineEnding, Directives.Where(d => d is not (IImportDirective or ExportDirective)).Select(i => i.ToString()));
        sb.Append(lineEnding);
        sb.Append(lineEnding);

        foreach (var bodyItem in Code)
        {
            if (bodyItem is Instruction)
                sb.Append(indent);

            sb.Append(bodyItem.ToString());
            sb.Append(lineEnding);
        }

        return sb.ToString();
    }
}
