using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Markup;
using System;
using System.Linq.Expressions;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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

    public override ExpressionSyntax ToSyntax(DecompileContext context)
    {
        return Value switch
        {
            Enum enumValue => MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(context.GetQualifiedName(TypeSchema).ToString()),
                IdentifierName(Value.ToString())
            ),

            _ => ToSyntax(Value, context)
        };
    }
}
