using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Iris.Asm;

public static class Extensions
{
    public static void FullLoad(this LoadResult result)
    {
        result.Load(LoadPass.DeclareTypes);
        result.Load(LoadPass.PopulatePublicModel);
        result.Load(LoadPass.Full);
        result.Load(LoadPass.Done);
    }

#if NETSTANDARD
    internal static StringBuilder AppendJoin<T>(this StringBuilder sb, string? separator, IEnumerable<T> values)
    {
        _ = values ?? throw new ArgumentNullException(nameof(values));

        separator ??= string.Empty;
        using (IEnumerator<T> en = values.GetEnumerator())
        {
            if (!en.MoveNext())
            {
                return sb;
            }

            T value = en.Current;
            if (value != null)
            {
                sb.Append(value.ToString());
            }

            while (en.MoveNext())
            {
                sb.Append(separator);
                value = en.Current;
                if (value != null)
                {
                    sb.Append(value.ToString());
                }
            }
        }
        return sb;
    }
#endif
}
