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

    public static string Unescape(this string input)
    {
        // https://stackoverflow.com/a/6736653/6232957

        if (input.Length <= 1) return input;

        // The input string can only get shorter,
        // so init the buffer so we won't have to reallocate later
        char[] buffer = new char[input.Length];
        int outIdx = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '\\')
            {
                if (i < input.Length - 1)
                {
                    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/lexical-structure#unicode-character-escape-sequences
                    var escapedChar = input[i + 1];
                    var unescapedChar = escapedChar switch
                    {
                        '0' => '\0',
                        'b' => '\b',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => escapedChar
                    };

                    buffer[outIdx++] = unescapedChar;
                    i++;
                    continue;
                }
            }

            buffer[outIdx++] = c;
        }

        return new string(buffer, 0, outIdx);
    }

    public static string Escape(this string input)
    {
        StringBuilder sb = new();

        var inputSpan = input.AsSpan();
        for (int i = 0; i < input.Length; i++)
        {
            var ch = inputSpan[i];
            var escapedCh = ch switch
            {
                '\0' => @"\0",
                '\b' => @"\b",
                '\n' => @"\n",
                '\r' => @"\r",
                '\t' => @"\t",
                '\'' => @"\'",
                '"' => "\"",
                _ => null
            };

            if (escapedCh is not null)
                sb.Append(escapedCh);
            else
                sb.Append(ch);
        }

        return sb.ToString();
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
