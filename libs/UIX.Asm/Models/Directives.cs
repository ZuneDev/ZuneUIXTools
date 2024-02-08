namespace Microsoft.Iris.Asm.Models;

public record SectionDirective : Directive, IBodyItem
{
    public SectionDirective(string name) : base("section")
    {
        Name = name;
    }

    public string Name { get; init; }

    public override string ToString() => $"{base.ToString()} {Name}";
}

public record ExportDirective : Directive
{
    public ExportDirective(string labelPrefix) : base("export")
    {
        LabelPrefix = labelPrefix;
    }

    public string LabelPrefix { get; init; }

    public override string ToString() => $"{base.ToString()} {LabelPrefix}";

    public string InitializePropertiesLabel => LabelPrefix + "_prop";
    public string InitializeLocalInputLabel => LabelPrefix + "_locl";
    public string InitializeContentLabel => LabelPrefix + "_cont";

    public string InitialEvaluateOffsetsLabelPrefix => LabelPrefix + "_evali_";
    public string FinalEvaluateOffsetsLabelPrefix => LabelPrefix + "_evalf_";
    public string RefreshGroupOffsetsLabelPrefix => LabelPrefix + "_rfsh_";
}
