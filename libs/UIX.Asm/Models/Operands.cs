namespace Microsoft.Iris.Asm.Models;

/// <summary>
/// The data type of a literal expression.
/// </summary>
/// <remarks>
/// If bit 4 is set, the literal is meant to be an index.
/// </remarks>
public enum LiteralDataType : byte
{
    Byte                = 0b0000,
    UInt16              = 0b0001,
    UInt32              = 0b0010,
    Int32               = 0b0011,

    Bytes               = 0b0100,

    TypeIndex           = 0b1000,
    PropertyIndex       = 0b1001,
    MethodIndex         = 0b1010,
    ConstantIndex       = 0b1011,
    SymbolRefIndex      = 0b1100,
    OpHostIndex         = 0b1101,
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

    public static LiteralDataType ReduceDataType(LiteralDataType dataType)
    {
        return IsIndex(dataType) ? LiteralDataType.UInt16 : dataType;
    }

    public static bool IsIndex(LiteralDataType dataType) => ((byte)dataType & (1 << 3)) != 0;
}

public record OperandReference : Operand
{
    public OperandReference(string constantName) : base(constantName, constantName)
    {
    }

    public string ConstantName => Content;

    public override string ToString() => $"@{base.ToString()}";
}
