﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.Animations.EffectColorKeyframe
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using Microsoft.Iris.Drawing;
using Microsoft.Iris.Render;

namespace Microsoft.Iris.Animations
{
    public class EffectColorKeyframe : BaseVector4Keyframe
    {
        public override void Apply(IAnimatableOwner animationTarget, Vector4 value)
        {
        }

        public override AnimationType Type => AnimationType.Vector4;

        public Color Color
        {
            get => new Color(this.Value.W, this.Value.X, this.Value.Y, this.Value.Z);
            set => this.Value = value.RenderConvert().ToVector4();
        }
    }
}
