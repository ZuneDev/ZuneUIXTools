using Microsoft.Iris.Debug.Symbols;
using System;
using System.IO;

namespace Microsoft.Iris.DecompXml;

internal class IrisTextWriter : StringWriter
{
    private int _currentLineIndex, _currentColumnIndex;

    public SourcePosition Position => new(_currentLineIndex + 1, _currentColumnIndex + 1);

    public override void Write(char[] buffer, int index, int count)
    {
        UpdatePosition(buffer.AsSpan(index, count));
        base.Write(buffer, index, count);
    }

    public override void Write(string value)
    {
        if (value is not null)
            UpdatePosition(value.AsSpan());

        base.Write(value);
    }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    public override void Write(ReadOnlySpan<char> buffer)
    {
        UpdatePosition(buffer);
        base.Write(buffer);
    }
#endif

    private void UpdatePosition(ReadOnlySpan<char> buffer)
    {
        int lastNewLineIndex = 0;
        for (int c = 0; c < buffer.Length;)
        {
            if (!IsAtNewLine(buffer, c))
            {
                ++c;
                continue;
            }

            c += NewLine.Length;
            IncrementLine();
            lastNewLineIndex = c;
        }

        _currentColumnIndex += buffer.Length - lastNewLineIndex;
    }

    private bool IsAtNewLine(ReadOnlySpan<char> buffer, int index)
    {
        for (int n = 0; n < NewLine.Length; ++n)
        {
            var b = n + index;
            if (b >= buffer.Length)
                return false;

            var nch = NewLine[n];
            var bch = buffer[b];
            if (nch != bch)
                return false;
        }

        return true;
    }

    private void IncrementLine()
    {
        ++_currentLineIndex;
        _currentColumnIndex = 0;
    }
}
