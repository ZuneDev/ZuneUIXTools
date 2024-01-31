namespace Microsoft.Iris.Asm.Models;

public record NamespaceImport(string Uri, string Name) : IImport
{
    public override string ToString() => $".import-ns {Uri} as {Name}";
}
