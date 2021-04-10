using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace ZuneUIXTools.ViewModels
{
    public abstract class DocumentViewModelBase : ObservableObject
    {
        public DocumentViewModelBase(string fileName)
        {
            FileName = fileName;
        }

        private string _FileName;
        public string FileName
        {
            get => _FileName;
            set => SetProperty(ref _FileName, value);
        }
    }
}
