using Gemini.Framework;
using Gemini.Framework.Services;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace ZuneUIXTools.Modules.UIXSource
{
    [Export(typeof(IEditorProvider))]
    public class UIXEditorProvider : IEditorProvider
    {
        private static readonly List<string> _extensions = new()
        {
            ".uix",
            ".xml"
        };

        public IEnumerable<EditorFileType> FileTypes
        {
            get { yield return new EditorFileType("Microsoft Iris UI", ".uix"); }
        }

        public bool CanCreateNew => true;

        public bool Handles(string path)
        {
            var extension = Path.GetExtension(path);
            return _extensions.Contains(extension.ToLowerInvariant());
        }

        public IDocument Create() => new UIXSourceEditorViewModel();

        public async Task New(IDocument document, string name) => await ((UIXSourceEditorViewModel)document).New(name);

        public async Task Open(IDocument document, string path) => await ((UIXSourceEditorViewModel)document).Load(path);
    }
}
