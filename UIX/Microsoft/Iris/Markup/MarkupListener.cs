﻿// Decompiled with JetBrains decompiler
// Type: Microsoft.Iris.Markup.MarkupListener
// Assembly: UIX, Version=4.8.0.0, Culture=neutral, PublicKeyToken=ddd0da4d3e678217
// MVID: A56C6C9D-B7F6-46A9-8BDE-B3D9B8D60B11
// Assembly location: C:\Program Files\Zune\UIX.dll

namespace Microsoft.Iris.Markup
{
    public class MarkupListener : Listener
    {
        protected IMarkupTypeBase _scriptHost;
        protected uint _scriptId = uint.MaxValue;

        public MarkupListener()
        {
        }

        public void Reset(
          INotifyObject notifier,
          string watch,
          IMarkupTypeBase scriptHost,
          uint scriptId)
        {
            this._watch = watch;
            this._scriptHost = scriptHost;
            this._scriptId = scriptId;
            this._watch = NotifyService.CanonicalizeString(this._watch);
            notifier.AddListener(this);
        }

        public override void Dispose()
        {
            this._scriptHost = null;
            this._scriptId = uint.MaxValue;
            base.Dispose();
        }

        public IMarkupTypeBase ScriptHost => this._scriptHost;

        public override void OnNotify()
        {
            base.OnNotify();
            if (this._scriptId == uint.MaxValue)
                return;
            this._scriptHost.ScheduleScriptRun(this._scriptId, false);
        }

        public override string ToString() => string.Format("{0}{4}: {1}->0x{2:X8} on '{3}'", this.GetType().Name, _watch, _scriptId, _scriptHost, "");
    }
}
