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

public record SharedDataTableDirective : Directive
{
    public SharedDataTableDirective(string dataTableUri) : base("dataTable")
    {
        Uri = dataTableUri;
    }

    public string Uri { get; init; }

    public override string ToString() => $"{base.ToString()} {Uri}";
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
