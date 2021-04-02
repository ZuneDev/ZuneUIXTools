﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.Animations.SnapshotRelativeTo
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using Microsoft.Iris.RenderAPI.Drawing;

namespace Microsoft.Iris.Animations
{
    public class SnapshotRelativeTo : RelativeTo
    {
        private RectangleF _bounds;

        public SnapshotRelativeTo(RectangleF bounds)
          : base(SnapshotPolicy.Once)
          => this._bounds = bounds;

        public RectangleF Bounds => this._bounds;
    }
}
