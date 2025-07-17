using Microsoft.Iris.Markup;

namespace Microsoft.Iris.DecompXml.Mock;

internal interface IReturnValueProvider
{
    TypeSchema ReturnType { get; }
}
