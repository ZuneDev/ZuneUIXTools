using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Markup;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml.Mock;

internal class IrisMethodCallExpression : IrisExpression, IArgumentProvider, IReturnValueProvider
{
    private readonly Expression[] _arguments;

    public IrisMethodCallExpression(MethodSchema method, Expression? target, IEnumerable<Expression> arguments)
    {
        Method = method;
        Target = target;
        _arguments = arguments.ToArray();
    }

    public MethodSchema Method { get; }
    
    public sealed override ExpressionType NodeType => ExpressionType.Call;
    
    public Expression? Target { get; }

    public int ArgumentCount => _arguments.Length;

    public TypeSchema ReturnType => Method.ReturnType;

    public Expression GetArgument(int index) => _arguments[index];

    public override ExpressionSyntax ToSyntax(DecompileContext context)
    {
        var targetExpression = Target switch
        {
            null => IdentifierName(context.GetQualifiedName(Method.Owner).ToString()),
            IrisExpression irisExpr => irisExpr.ToSyntax(context),
            _ => IdentifierName(Target.ToString())
        };

        var methodExpression = MemberAccessExpression(CodeAnalysis.CSharp.SyntaxKind.SimpleMemberAccessExpression,
            targetExpression, IdentifierName(Method.Name));

        var argumentExpressions = _arguments
            .Select(expr => ToSyntax(expr, context))
            .Select(Argument);

        return InvocationExpression(methodExpression, ArgumentList([..argumentExpressions]));
    }
}
