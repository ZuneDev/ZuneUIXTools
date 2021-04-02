﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.Layouts.StackLayoutInput
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using Microsoft.Iris.Layout;
using Microsoft.Iris.Library;
using Microsoft.Iris.Render;

namespace Microsoft.Iris.Layouts
{
    public class StackLayoutInput : ILayoutInput
    {
        private StackPriority _priority;
        private Size _minimumSize;

        public StackLayoutInput() => this.Priority = StackPriority.Low;

        public StackPriority Priority
        {
            get => this._priority;
            set => this._priority = value;
        }

        public Size MinimumSize
        {
            get => this._minimumSize;
            set => this._minimumSize = value;
        }

        DataCookie ILayoutInput.Data => StackLayout.Data;
    }
}
