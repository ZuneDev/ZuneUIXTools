using System;
using System.Linq;

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

public record StringEncodedConstantDirective : ConstantDirective
{
    public StringEncodedConstantDirective(string name, QualifiedTypeName typeName, string content)
        : base(name, typeName, $"{typeName}({content})")
    {
        Content = content;
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
