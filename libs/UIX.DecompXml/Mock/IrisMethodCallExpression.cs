using Microsoft.Iris.Markup;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

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

    public override string Decompile(DecompileContext context)
    {
        StringBuilder sb = new();

        if (Target is null)
        {
            var qfn = context.GetQualifiedName(Method.Owner);
            sb.Append(qfn);
        }
        else
        {
            sb.Append(Decompile(Target, context));
        }

        sb.Append('.');
        sb.Append(Method.Name);

        sb.Append('(');
        sb.Append(string.Join(", ", _arguments.Select(x => Decompile(x, context))));
        sb.Append(')');

        return sb.ToString();
    }
}
