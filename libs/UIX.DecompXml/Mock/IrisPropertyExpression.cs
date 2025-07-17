using Microsoft.Iris.Markup;
using System.Linq.Expressions;
using System.Text;

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


    public override string Decompile(DecompileContext context)
    {
        StringBuilder sb = new();

        if (Target is null)
        {
            var qfn = context.GetQualifiedName(Property.Owner);
            sb.Append(qfn);
        }
        else
        {
            sb.Append(Decompile(Target, context));
        }

        sb.Append('.');
        sb.Append(Property.Name);

        return sb.ToString();
    }
}
