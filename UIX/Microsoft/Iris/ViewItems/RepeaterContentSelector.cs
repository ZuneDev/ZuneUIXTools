﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.ViewItems.RepeaterContentSelector
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

using System.Collections;

namespace Microsoft.Iris.ViewItems
{
    public class RepeaterContentSelector
    {
        private Repeater _ownerRepeater;
        private IList _selectorsList;

        public RepeaterContentSelector(Repeater ownerRepeater)
        {
            this._ownerRepeater = ownerRepeater;
            this._selectorsList = new ArrayList();
        }

        public IList Selectors => this._selectorsList;

        public Repeater.ContentTypeHandler ContentTypeHandler => new Repeater.ContentTypeHandler(this.GetContentTypeForRepeatedItem);

        public void GetContentTypeForRepeatedItem(object itemObject, ref string contentName)
        {
            if (itemObject == null)
                return;
            foreach (TypeSelector selectors in _selectorsList)
            {
                if (selectors.IsMatch(itemObject, this._ownerRepeater))
                {
                    contentName = selectors.ContentName;
                    break;
                }
            }
        }
    }
}
