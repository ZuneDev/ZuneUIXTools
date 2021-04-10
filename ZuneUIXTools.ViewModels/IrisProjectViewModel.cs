using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ZuneUIXTools.ViewModels
{
    public class IrisProjectViewModel : ObservableObject
    {
        private ObservableCollection<DocumentViewModelBase> _uixDocuments;
        public ObservableCollection<DocumentViewModelBase> UIXDocuments
        {
            get => _uixDocuments;
            set => SetProperty(ref _uixDocuments, value);
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private DocumentViewModelBase _selectedDocument;
        public DocumentViewModelBase SelectedDocument
        {
            get => _selectedDocument;
            set => SetProperty(ref _selectedDocument, value);
        }
    }
}
