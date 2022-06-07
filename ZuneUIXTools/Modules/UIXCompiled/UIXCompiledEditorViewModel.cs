using Gemini.Framework.Threading;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ZuneUIXTools.Modules.UIXCompiled
{
    [Export(typeof(UIXCompiledEditorViewModel))]
#pragma warning disable 659
    public class UIXCompiledEditorViewModel : UIX.UIXEditorViewModelBase
#pragma warning restore 659
    {
        private UIXCompiledEditorView _view;
        private byte[] _originalBytes;

        protected override Task DoNew()
        {
            _originalBytes = Array.Empty<byte>();
            ApplyOriginalText();
            return TaskUtility.Completed;
        }

        protected override Task DoLoad(string filePath)
        {
            _originalBytes = File.ReadAllBytes(filePath);
            ApplyOriginalText();
            return TaskUtility.Completed;
        }

        protected override Task DoSave(string filePath)
        {
            _view.HexEditor.SaveCurrentState(filePath);
            _originalBytes = _view.HexEditor.GetAllBytes();
            return TaskUtility.Completed;
        }

        private void ApplyOriginalText()
        {
            _view.HexEditor.Stream?.Dispose();
            _view.HexEditor.Stream = new MemoryStream(_originalBytes);
            _view.HexEditor.UpdateVisual();

            _view.HexEditor.BytesModified += delegate
            {
                IsDirty = !_originalBytes.SequenceEqual(_view.HexEditor.GetAllBytes());
            };
        }

        protected override void OnViewLoaded(object view)
        {
            View = _view = (UIXCompiledEditorView)view;
        }
    }
}