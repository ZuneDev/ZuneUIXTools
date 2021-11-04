using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        public DocumentViewModelBase LoadDocument(string fileName, string fileFormat = null)
        {
            if (fileFormat == null)
                fileFormat = System.IO.Path.GetExtension(fileName);

            DocumentViewModelBase doc = UIXDocuments.FirstOrDefault(oldDoc => fileName == oldDoc.FileName);
            if (doc != null)
                return doc;

            if (fileFormat.EndsWith(".uix"))
                doc = new UIXDocumentViewModel(fileName);
            else if (fileFormat.EndsWith(".uib"))
                doc = new CompiledUIXDocumentViewModel(fileName);
            else
                throw new ArgumentException($"\"{fileName}\" is not a known format.");

            UIXDocuments.Add(doc);
            return doc;
        }

        public void UnloadDocument(string fileName)
        {
            DocumentViewModelBase doc = UIXDocuments.FirstOrDefault(oldDoc => fileName == oldDoc.FileName);
            if (doc != null)
                UIXDocuments.Remove(doc);
        }

        public void UnloadDocument(DocumentViewModelBase doc)
        {
            UIXDocuments.Remove(doc);
        }
    }
}
