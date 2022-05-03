using Gemini.Framework;
using Gemini.Framework.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace ZuneUIXTools.Modules.UIXCompiled
{
    [Export(typeof(IEditorProvider))]
    public class UIBEditorProvider : IEditorProvider
    {
        private static readonly List<string> _extensions = new()
        {
            ".uib",
        };

        public IEnumerable<EditorFileType> FileTypes
        {
            get { yield return new EditorFileType("Compiled Microsoft Iris UI", ".uib"); }
        }

        public bool CanCreateNew => false;

        public bool Handles(string path)
        {
            var extension = Path.GetExtension(path);
            return _extensions.Contains(extension.ToLowerInvariant());
        }

        public IDocument Create() => new UIXCompiledEditorViewModel();

        public async Task New(IDocument document, string name) => await ((UIXCompiledEditorViewModel)document).New(name);

        public async Task Open(IDocument document, string path) => await ((UIXCompiledEditorViewModel)document).Load(path);
    }
}
