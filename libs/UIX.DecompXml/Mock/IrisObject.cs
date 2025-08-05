using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;

namespace Microsoft.Iris.DecompXml.Mock;

internal record IrisObject(object Object, TypeSchema Type)
{
    public static IrisObject Create(object objIn, TypeSchema type, DecompileContext context)
    {
        object obj = objIn;

        if (objIn is IrisObject irisObj)
        {
            return irisObj.Type is null
                ? (irisObj with { Type = type })
                : irisObj;
        }
        else if (objIn is null)
        {
            type ??= UIXTypes.MapIDToType(UIXTypeID.Null);
        }
        else if (objIn is ExpressionSyntax expr)
        {
            type ??= GuessExpressionReturnType(expr, context);
            return new(obj, type);
        }

        if (objIn is Disassembler.RawConstantInfo constantInfo)
        {
            obj = constantInfo.Value;
            type ??= constantInfo.Type;
        }

        type ??= Disassembler.GuessTypeSchema(obj.GetType(), context.LoadResult);

        return new(obj, type);
    }

    private static TypeSchema GuessExpressionReturnType(ExpressionSyntax expr, DecompileContext ctx)
    {
        if (expr is ParenthesizedExpressionSyntax parenExpr)
            expr = parenExpr.Expression;

        if (expr is MemberAccessExpressionSyntax memberAccessExpression)
        {
            // TODO: Handle member access on instance methods
            var sourceExpr = (IdentifierNameSyntax)memberAccessExpression.Expression;
            var sourceTypeName = QualifiedTypeName.Parse(sourceExpr.ToString());
            var sourceType = ctx.GetImportedType(sourceTypeName);

            var memberName = memberAccessExpression.TryGetInferredMemberName();

            var property = sourceType.FindPropertyDeep(memberName);
            if (property is not null)
                return property.PropertyType;
        }

        return null;
    }
}
