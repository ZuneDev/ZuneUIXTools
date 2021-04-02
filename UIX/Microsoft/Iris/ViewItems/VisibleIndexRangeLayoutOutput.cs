﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.ViewItems.VisibleIndexRangeLayoutOutput
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using Microsoft.Iris.Layout;
using Microsoft.Iris.Library;

namespace Microsoft.Iris.ViewItems
{
    public class VisibleIndexRangeLayoutOutput : ExtendedLayoutOutput
    {
        private static readonly DataCookie s_dataProperty = DataCookie.ReserveSlot();
        private int _beginVisible;
        private int _endVisible;
        private int _beginVisibleOffscreen;
        private int _endVisibleOffscreen;
        private int? _focusedItem;

        public void Initialize(
          int beginVisible,
          int endVisible,
          int beginVisibleOffscreen,
          int endVisibleOffscreen,
          int? focusedItem)
        {
            this._beginVisible = beginVisible;
            this._endVisible = endVisible;
            this._beginVisibleOffscreen = beginVisibleOffscreen;
            this._endVisibleOffscreen = endVisibleOffscreen;
            this._focusedItem = focusedItem;
        }

        public int? FocusedItem => this._focusedItem;

        public int BeginVisible => this._beginVisible;

        public int EndVisible => this._endVisible;

        public int BeginVisibleOffscreen => this._beginVisibleOffscreen;

        public int EndVisibleOffscreen => this._endVisibleOffscreen;

        public override DataCookie OutputID => DataCookie;

        public static DataCookie DataCookie => s_dataProperty;

        public override string ToString() => InvariantString.Format("{0} (BeginVisibleOffscreen={1}, EndVisibleOffscreen={2})", this.GetType().Name, _beginVisibleOffscreen, _endVisibleOffscreen);
    }
}
