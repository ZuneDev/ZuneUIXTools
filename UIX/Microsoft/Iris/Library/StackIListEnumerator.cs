﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.Library.StackIListEnumerator
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using System.Collections;

namespace Microsoft.Iris.Library
{
    public struct StackIListEnumerator : IEnumerator
    {
        private const int START_INVALID_INDEX = -1;
        private IList _list;
        private int _currentIndex;

        public StackIListEnumerator(IList list)
        {
            this._list = list;
            this._currentIndex = -1;
        }

        public StackIListEnumerator GetEnumerator() => this;

        public bool MoveNext() => ++this._currentIndex < this._list.Count;

        public object Current => this._list[this._currentIndex];

        public void Reset() => this._currentIndex = -1;
    }
}
