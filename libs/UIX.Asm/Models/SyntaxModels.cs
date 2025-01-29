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
    }

    public IEnumerable<IBodyItem> Body { get; private set; }

    public IEnumerable<IDirective> Directives => Body.OfType<IDirective>();

    public SharedDataTableDirective DataTableDirective => Body.OfType<SharedDataTableDirective>().SingleOrDefault();

    public IEnumerable<IImportDirective> Imports => Directives.OfType<IImportDirective>();

    public IEnumerable<ExportDirective> Exports => Directives.OfType<ExportDirective>();

    public IEnumerable<ICodeItem> Code => Body.OfType<ICodeItem>();

    public void ImportSharedDataTable(Program dataTable)
    {
        List<IBodyItem> newBody = Exports.ToList<IBodyItem>();

        newBody.AddRange(dataTable.Directives.Where(d => d is not ExportDirective));
        newBody.AddRange(Code);

        Body = newBody.Cached();
    }

    public override string ToString()
    {
        const string lineEnding = "\r\n";
        const string indent = "    ";
        StringBuilder sb = new();

        if (DataTableDirective is not null)
        {
            sb.Append(DataTableDirective);
            sb.Append(lineEnding);
            sb.Append(lineEnding);
        }

        if (Exports.Any())
        {
            sb.AppendJoin(lineEnding, Exports.Select(i => i.ToString()));
            sb.Append(lineEnding);
            sb.Append(lineEnding);
        }

        if (Imports.Any())
        {
            var sortedImports = Imports
                .OrderBy(import => import.Type switch
                {
                    "ns" => 0,
                    "type" => 1,
                    "ctor" => 2,
                    "mthd" => 3,
                    "mbrs" => 4,
                    _ => int.MaxValue
                })
                .Select(i => i.ToString());

            sb.AppendJoin(lineEnding, sortedImports);
            sb.Append(lineEnding);
            sb.Append(lineEnding);
        }

        var otherDirectives = Directives.Where(d => d is not (IImportDirective or ExportDirective or SharedDataTableDirective));
        if (otherDirectives.Any())
        {
            sb.AppendJoin(lineEnding, otherDirectives.Select(i => i.ToString()));
            sb.Append(lineEnding);
            sb.Append(lineEnding);
        }

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
