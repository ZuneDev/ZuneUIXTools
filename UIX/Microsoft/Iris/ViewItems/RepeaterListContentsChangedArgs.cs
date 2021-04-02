﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.ViewItems.RepeaterListContentsChangedArgs
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using Microsoft.Iris.Data;

namespace Microsoft.Iris.ViewItems
{
    public class RepeaterListContentsChangedArgs
    {
        private UIListContentsChangedArgs _changedArgs;
        private Repeater _target;
        private int _generation;

        public RepeaterListContentsChangedArgs(
          UIListContentsChangedArgs args,
          Repeater target,
          int generation)
        {
            this._changedArgs = args;
            this._target = target;
            this._generation = generation;
        }

        public UIListContentsChangedArgs ChangedArgs => this._changedArgs;

        public Repeater Target => this._target;

        public int Generation => this._generation;
    }
}
