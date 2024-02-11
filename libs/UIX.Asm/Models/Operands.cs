namespace Microsoft.Iris.Asm.Models;

public enum LiteralDataType : byte
{
    Byte,
    UInt16,
    UInt32,
    Int32,
    Bytes,
}

public abstract record Operand(object Value, string Content = null) : AsmItem
{
    public override string ToString() => Content ?? Value.ToString();
}

public record OperandLiteral : Operand
{
    public OperandLiteral(object value, LiteralDataType dataType, string content = null) : base(value, content)
    {
        DataType = dataType;
    }

    public LiteralDataType DataType { get; init; }

    public override string ToString() => base.ToString();
}

public record OperandReference : Operand
{
    public OperandReference(string labelName) : base(labelName, labelName)
    {
    }

    public string LabelName => Content;

    public override string ToString() => base.ToString();
}
