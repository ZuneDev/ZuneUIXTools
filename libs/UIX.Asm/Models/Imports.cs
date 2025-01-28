using System.Collections.Generic;
using System.Linq;

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

/// <summary>
/// Imports many members that can be specified by solely their name.
/// </summary>
public record NamedMemberImport : ImportDirective
{
    public NamedMemberImport(QualifiedTypeName qualifiedName, IEnumerable<string> memberNames) : base("mbrs")
    {
        QualifiedName = qualifiedName;
        MemberNames = memberNames.ToList();
    }

    public QualifiedTypeName QualifiedName { get; init; }

    public List<string> MemberNames { get; init; }

    public override string ToString() => $"{base.ToString()} {QualifiedName}{{{string.Join(", ", MemberNames)}}}";
}

public record ConstructorImport : ImportDirective
{
    public ConstructorImport(QualifiedTypeName qualifiedName, IEnumerable<QualifiedTypeName> parameterTypes) : base("ctor")
    {
        QualifiedName = qualifiedName;
        ParameterTypes = parameterTypes.ToList();
    }

    public QualifiedTypeName QualifiedName { get; init; }

    public IEnumerable<QualifiedTypeName> ParameterTypes { get; init; }

    public override string ToString() => $"{base.ToString()} {QualifiedName}({string.Join(", ", ParameterTypes)})";
}

public record MethodImport : ImportDirective
{
    public MethodImport(QualifiedTypeName qualifiedName, string methodName, IEnumerable<QualifiedTypeName> parameterTypes) : base("mthd")
    {
        QualifiedName = qualifiedName;
        MethodName = methodName;
        ParameterTypes = parameterTypes.ToList();
    }

    public QualifiedTypeName QualifiedName { get; init; }

    public string MethodName { get; init; }

    public IEnumerable<QualifiedTypeName> ParameterTypes { get; init; }

    public override string ToString() => $"{base.ToString()} {QualifiedName}.{MethodName}({string.Join(", ", ParameterTypes)})";
}
