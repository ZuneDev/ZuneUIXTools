﻿using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Gemini.Framework;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using Microsoft.Iris;
using Microsoft.Iris.Markup;
using ZuneUIXTools.Modules.Shell.Commands;
using Application = Microsoft.Iris.Application;
using Command = Gemini.Framework.Commands.Command;

namespace ZuneUIXTools.Modules.UIXSource
{
    [Export(typeof(UIXSourceEditorViewModel))]
#pragma warning disable 659
    public class UIXSourceEditorViewModel : PersistedDocument, ICommandHandler<BuildAndRunCommandDefinition>
#pragma warning restore 659
    {
        private UIXSourceEditorView _view;
        private string _originalText;

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

        void ICommandHandler<BuildAndRunCommandDefinition>.Update(Command command)
        {
            command.Enabled = !string.IsNullOrWhiteSpace(_view.CodeEditor.Text);
        }

        Task ICommandHandler<BuildAndRunCommandDefinition>.Run(Command command)
        {
            var _buildThread = new Thread(new ThreadStart(BuildAndRun));
            _buildThread.SetApartmentState(ApartmentState.STA);
            _buildThread.IsBackground = true;
            _buildThread.Start();

            return TaskUtility.Completed;
        }

        private void BuildAndRun()
        {
            string sourceFile = FilePath;
            string compiledFile = Path.ChangeExtension(sourceFile, "uib");
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
                //Dispatcher.Invoke(() =>
                //{
                //    ErrorPanel.Children.Add(new TextBlock
                //    {
                //        Text = $"Build failed: {ex.Message}",
                //        Margin = new Thickness(0, 0, 0, 4)
                //    });
                //});
            }

            if (!isSuccess)
            {
                var dialogResult = MessageBox.Show(
                    "There were build errors. Would you like to contine and run the last successful build?",
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

            try
            {
                string uiRoot = null;// (IrisProject.SelectedDocument as UIXDocumentViewModel)?.UIRoot;
                Application.Window.SetBackgroundColor(new WindowColor(0xE6, 0xE6, 0xE6));
                Application.Window.RequestLoad("file://" + compiledFile + (string.IsNullOrEmpty(uiRoot) ? string.Empty : "#" + uiRoot));
                Application.Window.CloseRequested += (object sender, WindowCloseRequestedEventArgs args) =>
                {
                    args.BlockCloseRequest();
                    Application.Window.Visible = false;
                };
                Application.Run();
            }
            catch (Exception ex)
            {
                //Dispatcher.Invoke(() =>
                //{
                //    ErrorPanel.Children.Add(new TextBlock
                //    {
                //        Text = $"Load failed: {ex.Message}",
                //        Margin = new Thickness(0, 0, 0, 4)
                //    });
                //});
            }
        }
    }
}