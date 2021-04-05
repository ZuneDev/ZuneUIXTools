using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Iris;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ZuneUIXTools.ViewModels;
using Application = Microsoft.Iris.Application;
using Window = System.Windows.Window;

namespace ZuneUIXTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string FILE_FORMAT_FILTER = "Microsoft Iris UI (*.uix)|*.uix|XML files (*.xml)|*.xml";
        private readonly FontFamily CODE_FONT = new FontFamily("JetBrains Mono");

        public IrisProjectViewModel IrisProject { get; } = new IrisProjectViewModel
        {
            Name = "IrisApp1",
            UIXDocuments = new System.Collections.ObjectModel.ObservableCollection<UIXDocumentViewModel>()
        };

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        private void RemoveDocument(string fileName, bool removeFromProject = true, bool removeFromUI = true)
        {
            if (removeFromProject)
                IrisProject.UIXDocuments.Remove(IrisProject.UIXDocuments.First(doc => doc.FileName == fileName));

            if (!removeFromUI)
                return;
            FrameworkElement elem = GetDocumentUI(fileName);
            if (elem != null)
                DockingManager.Children.Remove(elem);
        }

        private void AddDocument(string fileName, bool addToProject = true, bool addToUI = true)
        {
            if (addToProject && !IrisProject.UIXDocuments.Any(doc => doc.FileName == fileName))
                IrisProject.UIXDocuments.Add(new UIXDocumentViewModel(fileName));

            if (!addToUI)
                return;
            // <avalonEdit:TextEditor Name="textEditor" Grid.Row="1" Padding="4" ShowLineNumbers="True"
            //                        SyntaxHighlighting="XML" FontFamily="JetBrains Mono" FontSize="10pt" />
            var editor = new TextEditor()
            {
                Tag = fileName,
                SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML"),
                Padding = new Thickness(4),
                ShowLineNumbers = true,
                FontFamily = CODE_FONT,
                FontSize = 10
            };
            editor.Load(fileName);
            DockingManager.Children.Add(editor);
            editor.GotFocus += EditorGotFocus;
            Syncfusion.Windows.Tools.Controls.DockingManager.ChangeState(editor, Syncfusion.Windows.Tools.Controls.DockState.Document);
            Syncfusion.Windows.Tools.Controls.DockingManager.SetHeader(editor, System.IO.Path.GetFileName(fileName));
        }

        private FrameworkElement GetDocumentUI(string fileName)
        {
            FrameworkElement elem = null;
            foreach (FrameworkElement candidate in DockingManager.Children)
            {
                if (candidate.Tag?.ToString() == fileName)
                {
                    elem = candidate;
                    break;
                }
            }
            return elem;
        }

        private void SetSelectedDocument(string fileName)
        {
            IrisProject.SelectedDocument = IrisProject.UIXDocuments.First(doc => doc.FileName == fileName);
        }

        private void EditorGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem)
                SetSelectedDocument(elem.Tag.ToString());
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IrisProject.UIXDocuments.CollectionChanged += UIXDocuments_CollectionChanged;
        }

        private void UIXDocuments_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (UIXDocumentViewModel doc in e.OldItems)
                    RemoveDocument(doc.FileName, removeFromProject: false);

            if (e.NewItems != null)
                foreach (UIXDocumentViewModel doc in e.NewItems)
                    AddDocument(doc.FileName, addToProject: false);
        }

        private void BuildAndRun()
        {
            Dispatcher.Invoke(() => ErrorPanel.Children.Clear());

            MarkupSystem.Startup(true);
            ErrorManager.OnErrors += (IList errors) => {
                foreach (object obj in errors)
                {
                    if (!(obj is ErrorRecord err)) continue;

#if DEBUG
                    System.Diagnostics.Debug.WriteLine(err.Message);
                    //System.Diagnostics.Debugger.Break();
#else
                    Console.WriteLine(err.Message);
#endif

                    Dispatcher.Invoke(() =>
                    {
                        TextBlock errBlock = new TextBlock
                        {
                            Text = $"Ln {err.Line}, Col {err.Column}: {err.Message}",
                            Margin = new Thickness(0, 0, 0, 4)
                        };

                        if (err.Line >= 0 && err.Column >= 0)
                        {
                            errBlock.MouseDown += (object sender, MouseButtonEventArgs args) =>
                            {
                                try
                                {
                                    string fileName = new Uri(err.Context).LocalPath;
                                    var editor = (TextEditor)GetDocumentUI(fileName);
                                    // Highlight the error in the code editor
                                    int offset = editor.Document.GetOffset(err.Line, err.Column);
                                    editor.SelectionStart = offset;
                                    editor.SelectionLength = 1;
                                    editor.ScrollTo(err.Line, err.Column);
                                }
                                catch { }
                            };
                        }

                        ErrorPanel.Children.Add(errBlock);
                    });
                }
            };

            string sourceFile = IrisProject.SelectedDocument.FileName;
            string compiledFile = System.IO.Path.ChangeExtension(sourceFile, "uib");
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
                Dispatcher.Invoke(() =>
                {
                    ErrorPanel.Children.Add(new TextBlock
                    {
                        Text = $"Build failed: {ex.Message}",
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                });
            }

            if (!isSuccess)
            {
                var dialogResult = System.Windows.MessageBox.Show(
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
                string uiRoot = IrisProject.SelectedDocument.UIRoot;
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
                Dispatcher.Invoke(() =>
                {
                    ErrorPanel.Children.Add(new TextBlock
                    {
                        Text = $"Load failed: {ex.Message}",
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                });
            }
        }

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            var doc = IrisProject.SelectedDocument;
            if (doc == null)
                return;

            // Save changes before running
            ((TextEditor)GetDocumentUI(doc.FileName)).Save(doc.FileName);
            // Set UI root
            doc.UIRoot = UIRootBox.Text;

            var _buildThread = new Thread(new ThreadStart(BuildAndRun));
            _buildThread.SetApartmentState(ApartmentState.STA);
            _buildThread.IsBackground = true;
            _buildThread.Start();
        }

        private void BuildAndRun_Click(object sender, RoutedEventArgs e)
        {
            var doc = IrisProject.SelectedDocument;
            if (doc == null)
                return;

            // Save changes before running
            ((TextEditor)GetDocumentUI(doc.FileName)).Save(doc.FileName);
            // Set UI root
            doc.UIRoot = UIRootBox.Text;

            var _buildThread = new Thread(new ThreadStart(BuildAndRun));
            _buildThread.SetApartmentState(ApartmentState.STA);
            _buildThread.IsBackground = true;
            _buildThread.Start();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = FILE_FORMAT_FILTER
            };
            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            AddDocument(openFileDialog.FileName, addToUI: false);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var doc = IrisProject.SelectedDocument;
            if (doc == null)
                return;
            var editor = GetDocumentUI(doc.FileName) as TextEditor;
            editor.Save(doc.FileName);
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (IrisProject.SelectedDocument == null)
                return;

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = FILE_FORMAT_FILTER,
                FileName = IrisProject.SelectedDocument.FileName
            };
            if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var editor = GetDocumentUI(IrisProject.SelectedDocument.FileName) as TextEditor;
            editor.Save(saveFileDialog.FileName);
            AddDocument(saveFileDialog.FileName);
        }
    }
}
