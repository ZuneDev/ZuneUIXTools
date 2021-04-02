﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.Markup.UIXConstructorSchema
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

namespace Microsoft.Iris.Markup
{
    public class UIXConstructorSchema : ConstructorSchema
    {
        private TypeSchema[] _parameterTypes;
        private ConstructHandler _construct;

        public UIXConstructorSchema(
          short ownerTypeID,
          short[] parameterTypeIDs,
          ConstructHandler construct)
          : base(UIXTypes.MapIDToType(ownerTypeID))
        {
            this._parameterTypes = UIXTypes.MapIDsToTypes(parameterTypeIDs);
            this._construct = construct;
        }

        public override TypeSchema[] ParameterTypes => this._parameterTypes;

        public override object Construct(object[] parameters) => this._construct(parameters);
    }
}
