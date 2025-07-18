using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Markup;
using System.Linq.Expressions;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml.Mock;

internal class IrisPropertyExpression : IrisExpression, IReturnValueProvider
{
    public IrisPropertyExpression(PropertySchema property, Expression? target)
    {
        Property = property;
        Target = target;
    }

    public new PropertySchema Property { get; }

    public sealed override ExpressionType NodeType => ExpressionType.MemberAccess;

    public Expression? Target { get; }

    public TypeSchema ReturnType => Property.PropertyType;

    public override ExpressionSyntax ToSyntax(DecompileContext context)
    {
        var targetExpression = Target switch
        {
            null => IdentifierName(context.GetQualifiedName(Property.Owner).ToString()),
            IrisExpression irisExpr => irisExpr.ToSyntax(context),
            _ => IdentifierName(Target.ToString())
        };

        var propertyAccessExpression = MemberAccessExpression(CodeAnalysis.CSharp.SyntaxKind.SimpleMemberAccessExpression,
            targetExpression, IdentifierName(Property.Name));

        return propertyAccessExpression;
    }
}
