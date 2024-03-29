﻿using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace ZuneUIXTools.ViewModels
{
    public class UIXDocumentViewModel : DocumentViewModelBase
    {
        public UIXDocumentViewModel(string fileName) : base(fileName)
        {
            Content = File.ReadAllText(fileName);
        }

        private string _content = null;
        public string Content
        {
            get
            {
                if (FileName != null)
                    _content = File.ReadAllText(FileName);
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
