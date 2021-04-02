﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.RenderAPI.Drawing.PointF
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using Microsoft.Iris.Render;
using System;
using System.Globalization;
using System.Text;

namespace Microsoft.Iris.RenderAPI.Drawing
{
    public struct PointF
    {
        private float x;
        private float y;
        public static readonly PointF Zero = new PointF(0.0f, 0.0f);

        public PointF(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public bool IsZero => X == 0.0 && Y == 0.0;

        public float X
        {
            get => this.x;
            set => this.x = value;
        }

        public float Y
        {
            get => this.y;
            set => this.y = value;
        }

        public static PointF operator +(PointF pt, Size sz) => new PointF(pt.X + sz.Width, pt.Y + sz.Height);

        public static PointF operator +(PointF pt, SizeF sz) => new PointF(pt.X + sz.Width, pt.Y + sz.Height);

        public static PointF operator -(PointF pt, Size sz) => new PointF(pt.X - sz.Width, pt.Y - sz.Height);

        public static PointF operator -(PointF pt, SizeF sz) => new PointF(pt.X - sz.Width, pt.Y - sz.Height);

        public static SizeF operator -(PointF pt1, PointF pt2) => new SizeF(pt1.X - pt2.X, pt1.Y - pt2.Y);

        public static bool operator ==(PointF left, PointF right) => left.X == (double)right.X && left.Y == (double)right.Y;

        public static bool operator !=(PointF left, PointF right) => !(left == right);

        public Point ToPoint() => new Point((int)this.x, (int)this.y);

        public override bool Equals(object obj) => obj is PointF pointF && pointF.X == (double)this.X && pointF.Y == (double)this.Y;

        public override int GetHashCode() => this.x.GetHashCode() ^ this.y.GetHashCode();

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder(32);
            stringBuilder.Append("(X=");
            stringBuilder.Append(this.X.ToString(NumberFormatInfo.InvariantInfo));
            stringBuilder.Append(", Y=");
            stringBuilder.Append(this.Y.ToString(NumberFormatInfo.InvariantInfo));
            stringBuilder.Append(")");
            return stringBuilder.ToString();
        }
    }
}
