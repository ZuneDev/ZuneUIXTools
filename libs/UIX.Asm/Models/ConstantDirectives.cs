using Microsoft.Iris.Markup;
using System;
using System.Linq;

namespace Microsoft.Iris.Asm.Models;

public record ConstantDirective : Directive
{
    public ConstantDirective(string name, QualifiedTypeName typeName, string constructor) : base("constant")
    {
        Name = name;
        TypeName = typeName;
        Constructor = constructor;
    }

    public string Name { get; }
    public QualifiedTypeName TypeName { get; }
    public string Constructor { get; }
    public MarkupConstantPersistMode PersistMode { get; init; }

    public override string ToString() => $"{base.ToString()} {Name} = {Constructor}";
}

public record StringEncodedConstantDirective : ConstantDirective
{
    public StringEncodedConstantDirective(string name, QualifiedTypeName typeName, string content)
        : base(name, typeName, $"{typeName}(\"{content.Escape()}\")")
    {
        Content = content;
        PersistMode = MarkupConstantPersistMode.FromString;
    }

    public string Content { get; }

    public override string ToString() => base.ToString();
}

public record BinaryEncodedConstantDirective : ConstantDirective
{
    public BinaryEncodedConstantDirective(string name, QualifiedTypeName typeName, byte[] content)
        : base(name, typeName, GetConstructor(typeName, content))
    {
        Content = content;
        PersistMode = MarkupConstantPersistMode.Binary;
    }

    public byte[] Content { get; }

    public override string ToString() => base.ToString();

    private static string GetConstructor(QualifiedTypeName typeName, byte[] content)
    {
        string contentStr;
        if (content.Length == 1)
            contentStr = $"0x{content[0]:X2}";
        else if (content.Length == 2)
            contentStr = $"0x{BitConverter.ToUInt16(content, 0):X4}";
        else if (content.Length == 4)
            contentStr = $"0x{BitConverter.ToUInt32(content, 0):X8}";
        else
            contentStr = string.Join(", ", content.Select(b => $"0x{b:X2}"));

        return $"{typeName}.bin({contentStr})";
    }
}

public record CanonicalInstanceConstantDirective : ConstantDirective
{
    public CanonicalInstanceConstantDirective(string name, QualifiedTypeName typeName, string canonicalName)
        : base(name, typeName, $"{typeName}.can({canonicalName})")
    {
        CanonicalName = canonicalName;
        PersistMode = MarkupConstantPersistMode.Canonical;
    }

    public string CanonicalName { get; }

    public override string ToString() => base.ToString();
}
