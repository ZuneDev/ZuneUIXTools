﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.Markup.SSLexUnicodeBufferConsumer
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using Microsoft.Iris.OS;
using SSVParseLib;
using System;

namespace Microsoft.Iris.Markup
{
    public class SSLexUnicodeBufferConsumer : SSLexConsumer
    {
        private char[] _buffer;
        private uint _length;
        private string _prefix;

        public SSLexUnicodeBufferConsumer(char[] buffer, uint length)
          : this(buffer, length, "")
        {
        }

        public unsafe SSLexUnicodeBufferConsumer(char[] buffer, uint length, string prefix)
        {
            this._buffer = buffer;
            this._length = length;
            this._prefix = prefix;
        }

        public override bool getNext() => this.GetCharAt(this.m_index, out this.m_current);

        public override unsafe string getSubstring(int start, int length)
        {
            if (start + length <= this._prefix.Length)
            {
                return this._prefix.Substring(start, length);
            }
            else
            {
                fixed (char* ptr = _buffer)
                {
                    return NativeApi.PtrToStringUni(new IntPtr(ptr + start - this._prefix.Length), length);
                }
            }
        }

        public unsafe bool GetCharAt(int position, out char ch)
        {
            if (position < this._prefix.Length)
            {
                ch = this._prefix[this.m_index];
                return true;
            }
            int index = position - this._prefix.Length;
            if (index < _length)
            {
                ch = this._buffer[index];
                return true;
            }
            ch = char.MinValue;
            return false;
        }
    }
}
