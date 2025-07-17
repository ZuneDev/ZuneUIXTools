using System.Linq.Expressions;

namespace Microsoft.Iris.DecompXml.Mock;

internal class IrisExpression : Expression
{
    public virtual string Decompile(Decompiler decompiler) => ToString();
}
