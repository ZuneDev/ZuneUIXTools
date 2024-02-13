namespace Microsoft.Iris.Asm.Models;

public record SectionDirective : Directive
{
    public SectionDirective(string name) : base("section")
    {
        Name = name;
    }

    public string Name { get; init; }

    public override string ToString() => $"{base.ToString()} {Name}";
}

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

    public override string ToString() => $"{base.ToString()} {Name} = {Constructor}";
}

public record EncodedConstantDirective : ConstantDirective
{
    public EncodedConstantDirective(string name, QualifiedTypeName typeName, string content)
        : base(name, typeName, $"{typeName}({content})")
    {
        Content = content;
    }

    public string Content { get; }

    public override string ToString() => base.ToString();
}

public record ExportDirective : Directive
{
    public ExportDirective(string labelPrefix, uint listenerCount, string baseTypeName) : base("export")
    {
        LabelPrefix = labelPrefix;
        ListenerCount = listenerCount;
        BaseTypeName = baseTypeName;
    }

    public string LabelPrefix { get; init; }

    public uint ListenerCount { get; init; }

    public string BaseTypeName { get; init; }

    public override string ToString() => $"{base.ToString()} {LabelPrefix} {ListenerCount} {BaseTypeName}";

    public string InitializePropertiesLabel => GetInitializePropertiesLabel(LabelPrefix);
    public string InitializeLocalsInputLabel => GetInitializeLocalsInputLabel(LabelPrefix);
    public string InitializeContentLabel => GetInitializeContentLabel(LabelPrefix);

    public string InitialEvaluateOffsetsLabelPrefix => GetInitialEvaluateOffsetsLabelPrefix(LabelPrefix);
    public string FinalEvaluateOffsetsLabelPrefix => GetInitialEvaluateOffsetsLabelPrefix(LabelPrefix);
    public string RefreshGroupOffsetsLabelPrefix => GetRefreshGroupOffsetsLabelPrefix(LabelPrefix);

    public static string GetInitializePropertiesLabel(string prefix) => prefix + "_prop";
    public static string GetInitializeLocalsInputLabel(string prefix) => prefix + "_locl";
    public static string GetInitializeContentLabel(string prefix) => prefix + "_cont";
    public static string GetInitialEvaluateOffsetsLabelPrefix(string prefix) => prefix + "_evali_";
    public static string GetFinalEvaluateOffsetsLabelPrefix(string prefix) => prefix + "_evalf_";
    public static string GetRefreshGroupOffsetsLabelPrefix(string prefix) => prefix + "_rfsh_";
}
