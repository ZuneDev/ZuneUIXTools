using Gemini.Framework;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZuneUIXTools.Modules.Shell.Commands;
using IrisApp = Microsoft.Iris.Application;

namespace ZuneUIXTools.Modules.UIX
{
#pragma warning disable CS0659
    public abstract class UIXEditorViewModelBase : PersistedDocument, ICommandHandler<DecompileCommandDefinition>
#pragma warning restore CS0659
    {
        private string _uiRoot;

        public string UIRoot
        {
            get => _uiRoot;
            set => Set(ref _uiRoot, value);
        }

        protected UIElement View { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as PersistedDocument;
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
                Microsoft.Iris.Debug.Bridge bridge = new();
                bridge.InterpreterStep += (ctx, entry) => System.Diagnostics.Debug.WriteLine($"[UIXT] [Interpreter] [{ctx}] {entry}");

                //IrisApp.Window.RequestLoad("res://ZuneShellResources!Frame.uix#Frame");

                AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
                {
                    if (e.Name.StartsWith("Zune"))
                    {
                        // Load recompiled binaries instead
                        System.Reflection.AssemblyName asmName = new(e.Name);
                        return System.Reflection.Assembly.LoadFrom(Path.Combine(@"C:\Program Files\Zune", asmName.Name + ".dll"));
                    }

                    return null;
                };
                IrisApp.AddImportRedirect("res://ZuneShellResources!", "file://D:/Documents/Ghidra/Zune/ZuneShellResouces/");
                IrisApp.AddImportRedirect("res://ZuneMarketplaceResources!", "file://D:/Documents/Ghidra/Zune/ZuneMarketplaceResources/");
                //IrisApp.AddImportRedirect("assembly://ZuneShell", "file://D:/Repos/ZuneDev/ZuneShell.dll/ZuneShell/bin/x64/Debug/net48/ZuneShell.dll");

                IrisApp.Window.RequestLoad("file://" + FilePath + (UIRoot == null ? string.Empty : "#" + UIRoot));
                IrisApp.DebugSettings.UseDecompiler = true;
                IrisApp.Run();
                IrisApp.DebugSettings.UseDecompiler = false;
            }
            catch (Exception ex)
            {
                View.Dispatcher.Invoke(() =>
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
                        File.WriteAllText(Path.ChangeExtension(FilePath, $"{i}.decomp.uix"), result.Doc.InnerXml);
                    }
                }
            }
        }
    }
}
