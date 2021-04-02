// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.Markup.BooleanBoxes
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

namespace Microsoft.Iris.Markup
{
    public static class BooleanBoxes
    {
        public static object TrueBox = true;
        public static object FalseBox = false;

        public static object Box(bool value) => value ? TrueBox : FalseBox;
    }
}
