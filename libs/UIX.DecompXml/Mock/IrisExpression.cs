using Microsoft.Iris.Asm;
using System;
using System.Linq.Expressions;

namespace Microsoft.Iris.DecompXml.Mock;

internal class IrisExpression : Expression
{
    public virtual string Decompile(DecompileContext context) => ToString();

    public static Expression ToExpression(object p)
    {
        return p switch
        {
            null => Constant(null),
            Expression expr => expr,
            Disassembler.RawConstantInfo constantInfo => new IrisConstantExpression(constantInfo.Value, constantInfo.Type),

            _ => throw new NotImplementedException()
        };
    }
}
