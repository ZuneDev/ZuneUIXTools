using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZuneUIXTools.ViewModels
{
    public class CompiledUIXDocumentViewModel : DocumentViewModelBase
    {
        public CompiledUIXDocumentViewModel(string fileName) : base(fileName)
        {
            Content = File.ReadAllBytes(fileName);
        }

        private byte[] _content = null;
        public byte[] Content
        {
            get
            {
                if (FileName != null)
                    _content = File.ReadAllBytes(FileName);
                return _content;
            }
            set => SetProperty(ref _content, value);
        }

        private string _uiRoot;
        public string UIRoot
        {
            get => _uiRoot;
            set => SetProperty(ref _uiRoot, value);
        }
    }
}
