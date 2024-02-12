namespace Microsoft.Iris.Asm.Models;

public record NamespaceImport : ImportDirective
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

public record TypeImport : ImportDirective
{
    public TypeImport(QualifiedTypeName qualifiedName) : base("type")
    {
        QualifiedName = qualifiedName;
    }

    public QualifiedTypeName QualifiedName { get; init; }

    public override string ToString() => $"{base.ToString()} {QualifiedName}";
}
