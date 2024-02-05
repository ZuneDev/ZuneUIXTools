namespace Microsoft.Iris.Asm.Models;

public record NamespaceImport : Import
{
    public NamespaceImport(string uri, string name) : base("ns")
    {
        Uri = uri;
        Name = name;
    }

    public string Uri { get; init; }
    public string Name { get; init; }

    public override string ToString() => $"{base.ToString()} {Uri} as {Name}";
}
