using Gemini.Framework;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZuneUIXTools.Modules.Shell.Commands;
using IrisApp = Microsoft.Iris.Application;

namespace ZuneUIXTools.Modules.UIXCompiled
{
    [Export(typeof(UIXCompiledEditorViewModel))]
#pragma warning disable 659
    public class UIXCompiledEditorViewModel : PersistedDocument, ICommandHandler<DecompileCommandDefinition>
#pragma warning restore 659
    {
        private UIXCompiledEditorView _view;
        private byte[] _originalBytes;
        private string _uiRoot;

        public string UIRoot
        {
            get => _uiRoot;
            set => Set(ref _uiRoot, value);
        }

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
            _view = (UIXCompiledEditorView)view;
        }

        public override bool Equals(object obj)
        {
            var other = obj as UIXCompiledEditorViewModel;
            return other != null
                && string.Equals(FilePath, other.FilePath, StringComparison.InvariantCultureIgnoreCase)
                && string.Equals(FileName, other.FileName, StringComparison.InvariantCultureIgnoreCase);
        }

        void ICommandHandler<DecompileCommandDefinition>.Update(Command command)
        {
            command.Enabled = !(IsNew || IsDirty);
        }

        Task ICommandHandler<DecompileCommandDefinition>.Run(Command command)
        {
            Thread _buildThread = new(new ThreadStart(Decompile));
            _buildThread.SetApartmentState(ApartmentState.STA);
            _buildThread.IsBackground = true;
            _buildThread.Start();

            return TaskUtility.Completed;
        }

        private void Decompile()
        {
            try
            {
                Microsoft.Iris.Debug.Trace.EnableAllCategories(true);
                IrisApp.Initialize();
            }
            catch { }

            try
            {
                //IrisApp.Window.RequestLoad("res://ZuneShellResources!Frame.uix#Frame");

                IrisApp.Window.RequestLoad("file://" + FilePath + (UIRoot == null ? string.Empty : "#" + UIRoot));
                IrisApp.DebugSettings.UseDecompiler = true;
                IrisApp.Run();
                IrisApp.DebugSettings.UseDecompiler = false;
            }
            catch (Exception ex)
            {
                _view.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Failed to decompile '{FilePath}'",
                        "Zune UIX Tools", MessageBoxButton.OK, MessageBoxImage.Error
                    );
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                });
            }
            finally
            {
                // Even if there were errors, save the decomp results.
                var results = IrisApp.DebugSettings.DecompileResults;
                if (results != null && results.Count > 0)
                {
                    for (int i = 0; i < results.Count; i++)
                    {
                        var result = results[i];
                        File.WriteAllText(Path.ChangeExtension(FilePath, $"{i}.decomp.uix"), result.InnerXml);
                    }
                }
            }
        }
    }
}