﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.ViewItems.TypeSelector
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using Microsoft.Iris.Markup;

namespace Microsoft.Iris.ViewItems
{
    public class TypeSelector
    {
        private TypeSchema _type;
        private string _contentName;

        public TypeSchema Type
        {
            get => this._type;
            set => this._type = value;
        }

        public string ContentName
        {
            get => this._contentName;
            set => this._contentName = value;
        }

        public bool IsMatch(object itemObject, Repeater ownerRepeater) => this._type.IsAssignableFrom(itemObject);
    }
}
