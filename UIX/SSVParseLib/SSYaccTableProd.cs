﻿// Decompiled with JetBrains decompiler
// Type: SSVParseLib.SSYaccTableProd
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

namespace SSVParseLib
{
    public class SSYaccTableProd
    {
        private int m_size;
        private int m_leftside;

        public SSYaccTableProd(int q_size, int q_leftside)
        {
            this.m_size = q_size;
            this.m_leftside = q_leftside;
        }

        public int size() => this.m_size;

        public int leftside() => this.m_leftside;
    }
}
