using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Iris.Asm;

internal static class StringBuilderExtensions
{
#if NETSTANDARD
    public static StringBuilder AppendJoin<T>(this StringBuilder sb, string? separator, IEnumerable<T> values)
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
