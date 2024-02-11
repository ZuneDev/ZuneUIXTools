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
    public TypeImport(string namespacePrefix, string name) : base("type")
    {
        NamespacePrefix = namespacePrefix;
        Name = name;
    }

    public string NamespacePrefix { get; init; }
    public string Name { get; init; }

    public override string ToString()
    {
        if (NamespacePrefix is null)
            return $"{base.ToString()} {Name}";
        else
            return $"{base.ToString()} {NamespacePrefix}:{Name}";
    }
}
