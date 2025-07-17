using Microsoft.Iris.Asm;
using System;
using System.Linq.Expressions;

namespace Microsoft.Iris.DecompXml.Mock;

internal class IrisExpression : Expression
{
    public virtual string Decompile(DecompileContext context) => ToString();

    public static Expression Wrap(object p)
    {
        return p switch
        {
            null => Constant(null),
            Expression expr => expr,
            Disassembler.RawConstantInfo constantInfo => new IrisConstantExpression(constantInfo.Value, constantInfo.Type),

            _ => throw new NotImplementedException($"Unable to wrap '{p}' in an expression")
        };
    }

    public static string Decompile(Expression expr, DecompileContext context)
    {
        return expr is IrisExpression irisExpr
            ? irisExpr.Decompile(context)
            : expr.ToString();
    }
}
