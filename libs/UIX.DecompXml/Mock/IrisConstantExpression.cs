using Microsoft.Iris.Markup;
using System.Linq.Expressions;

namespace Microsoft.Iris.DecompXml.Mock;

internal class IrisConstantExpression : IrisExpression, IReturnValueProvider
{
    public IrisConstantExpression(object value, TypeSchema typeSchema)
    {
        Value = value;
        TypeSchema = typeSchema;
    }

    public sealed override ExpressionType NodeType => ExpressionType.Constant;
    
    public object Value { get; }

    public TypeSchema TypeSchema { get; }

    public TypeSchema ReturnType => TypeSchema;

    public override string Decompile(DecompileContext context)
    {
        if (TypeSchema.IsEnum)
            return $"{context.GetQualifiedName(TypeSchema)}.{Value}";

        return Value switch
        {
            string str => str,
            IStringEncodable strEnc => strEnc.EncodeString(),
            _ => Value?.ToString() ?? "null",
        };
    }
}
