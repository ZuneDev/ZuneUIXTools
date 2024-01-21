using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using Gemini.Modules.ErrorList;
using Gemini.Modules.Output;
using Microsoft.Iris;
using Microsoft.Iris.Debug.SystemNet;
using Microsoft.Iris.Markup;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZuneUIXTools.Modules.Shell.Commands;
using ZuneUIXTools.Modules.UIX;
using Application = Microsoft.Iris.Application;
using Command = Gemini.Framework.Commands.Command;

namespace ZuneUIXTools.Modules.UIXSource
{
    [Export(typeof(UIXSourceEditorViewModel))]
#pragma warning disable 659
    public class UIXSourceEditorViewModel : UIXEditorViewModelBase, ICommandHandler<BuildAndRunCommandDefinition>, ICommandHandler<BuildAndDebugCommandDefinition>,
        ICommandHandler<StepOverDebuggerCommandDefinition>, ICommandHandler<ContinueDebuggerCommandDefinition>
#pragma warning restore 659
    {
        private readonly IOutput _output = IoC.Get<IOutput>();
        private readonly IErrorList _errorList = IoC.Get<IErrorList>();
        private readonly DebuggerService _debuggerService = IoC.Get<DebuggerService>();

        private UIXSourceEditorView _view;
        private string _originalText;
        private string _debuggerConnectionUri = App.DEFAULT_DEBUG_URI;

        public bool CanBuild => !string.IsNullOrWhiteSpace(_view.CodeEditor.Text);

        protected override Task DoNew()
        {
            _originalText = string.Empty;
            ApplyOriginalText();
            return TaskUtility.Completed;
        }

        protected override Task DoLoad(string filePath)
        {
            _originalText = File.ReadAllText(filePath);
            ApplyOriginalText();
            return TaskUtility.Completed;
        }

        protected override Task DoSave(string filePath)
        {
            _view.CodeEditor.Save(filePath);
            _originalText = _view.CodeEditor.Text;
            return TaskUtility.Completed;
        }

        private void ApplyOriginalText()
        {
            _view.CodeEditor.Text = _originalText;

            _view.CodeEditor.TextChanged += delegate
            {
                IsDirty = string.Compare(_originalText, _view.CodeEditor.Text) != 0;
            };
        }

        protected override void OnViewLoaded(object view)
        {
            _view = (UIXSourceEditorView)view;
        }

        public override bool Equals(object obj)
        {
            var other = obj as UIXSourceEditorViewModel;
            return other != null
                && string.Equals(FilePath, other.FilePath, StringComparison.InvariantCultureIgnoreCase)
                && string.Equals(FileName, other.FileName, StringComparison.InvariantCultureIgnoreCase);
        }

        private void StartBuildAndRun(bool attachDebugger)
        {
            var buildThread = new Thread(BuildAndRun);
            buildThread.SetApartmentState(ApartmentState.STA);
            buildThread.IsBackground = true;
            buildThread.Start(attachDebugger);
        }

        private void BuildAndRun(object parameter)
        {
            string sourceFile = FilePath;
            string compiledFile = Path.ChangeExtension(sourceFile, "uib");

            bool attachDebugger = (bool)parameter;
            if (attachDebugger)
            {
                // Set up debugger client
                var debuggerService = IoC.Get<DebuggerService>();
                debuggerService.Start(_debuggerConnectionUri);

                debuggerService.Client.UpdateBreakpoint(new($"file://{compiledFile}", 1));
                debuggerService.Client.DebuggerCommand = Microsoft.Iris.Debug.Data.InterpreterCommand.Continue;
                debuggerService.Client.RequestLineNumberTable($"file://{sourceFile}", entries =>
                {
                    foreach (var entry in entries)
                        _output.AppendLine($"{entry.Offset} => {entry.Line}, {entry.Column}");
                });

                // Set up debugger server
                Application.DebugSettings.DebugConnectionUri = _debuggerConnectionUri;
                Application.Initialized += () =>
                {
                    // Server is ready, run the application
                    Run(sourceFile, compiledFile);
                };
            }
            _output.AppendLine($"Compiling '{sourceFile}'...");

            bool isSuccess = false;
            try
            {
                isSuccess = MarkupCompiler.Compile(
                    new[]
                    {
                        new CompilerInput()
                        {
                            SourceFileName = sourceFile,
                            OutputFileName = compiledFile
                        }
                    },
                    new CompilerInput()
                );
            }
            catch (Exception ex)
            {
                _errorList.AddItem(ErrorListItemType.Error, "Compiling failed: " + ex.Message, sourceFile, null, null);
            }

            if (!isSuccess)
            {
                var dialogResult = MessageBox.Show(
                    "There were build errors. Would you like to continue and run the last successful build?",
                    "Zune UIX Tools", MessageBoxButton.YesNo, MessageBoxImage.Information
                );
                if (dialogResult != MessageBoxResult.Yes)
                    return;
            }

            try
            {
                Application.Initialize();
            }
            catch { }

            // No need to wait for the debugger to start
            if (!attachDebugger)
                Run(sourceFile, compiledFile);
        }

        private void Run(string sourceFile, string compiledFile)
        {
            try
            {
                string uiRoot = string.IsNullOrEmpty(UIRoot) ? "Default" : UIRoot;
                _output.AppendLine($"Loading UI '{uiRoot}' from '{compiledFile}'...");

                Application.Window.SetBackgroundColor(new WindowColor(0xE6, 0xE6, 0xE6));
                Application.Window.RequestLoad($"file://{compiledFile}#{uiRoot}");
                Application.Window.CloseRequested += (object sender, WindowCloseRequestedEventArgs args) =>
                {
                    args.BlockCloseRequest();
                    Application.Window.Visible = false;

                    if (sender is Microsoft.Iris.Window window)
                        _output.AppendLine($"Window '{window.Handle}' closed");
                };

                _output.AppendLine($"Application is running...");
                Application.Run();
                _output.AppendLine("Application exited");
            }
            catch (Exception ex)
            {
                _errorList.AddItem(ErrorListItemType.Error, "Load failed: " + ex.Message, sourceFile, null, null);
            }
        }

        void ICommandHandler<BuildAndRunCommandDefinition>.Update(Command command) => command.Enabled = CanBuild;

        Task ICommandHandler<BuildAndRunCommandDefinition>.Run(Command command)
        {
            StartBuildAndRun(false);
            return TaskUtility.Completed;
        }

        void ICommandHandler<BuildAndDebugCommandDefinition>.Update(Command command) => command.Enabled = CanBuild;

        Task ICommandHandler<BuildAndDebugCommandDefinition>.Run(Command command)
        {
            StartBuildAndRun(true);
            return TaskUtility.Completed;
        }

        void ICommandHandler<StepOverDebuggerCommandDefinition>.Update(Command command)
        {
            command.Visible = _debuggerService.Client != null;
            command.Enabled = _debuggerService.Client != null
                && _debuggerService.Client.DebuggerCommand == Microsoft.Iris.Debug.Data.InterpreterCommand.Break;
        }

        Task ICommandHandler<StepOverDebuggerCommandDefinition>.Run(Command command)
        {
            _debuggerService.Client.DebuggerCommand = Microsoft.Iris.Debug.Data.InterpreterCommand.Step;
            return Task.CompletedTask;
        }

        void ICommandHandler<ContinueDebuggerCommandDefinition>.Update(Command command)
        {
            command.Visible = _debuggerService.Client != null;
            command.Enabled = _debuggerService.Client != null
                && _debuggerService.Client.DebuggerCommand != Microsoft.Iris.Debug.Data.InterpreterCommand.Continue;
        }

        Task ICommandHandler<ContinueDebuggerCommandDefinition>.Run(Command command)
        {
            _debuggerService.Client.DebuggerCommand = Microsoft.Iris.Debug.Data.InterpreterCommand.Continue;
            return Task.CompletedTask;
        }
    }
}