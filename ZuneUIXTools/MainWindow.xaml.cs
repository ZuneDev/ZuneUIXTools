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
using Window = System.Windows.Window;

namespace ZuneUIXTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string FILE_FORMAT_FILTER = "Microsoft Iris UI (*.uix)|*.uix|XML files (*.xml)|*.xml";

        public string DocumentPath { get; set; }
        public string UIRoot { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            
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
                                // Highlight the error in the code editor
                                int offset = textEditor.Document.GetOffset(err.Line, err.Column);
                                textEditor.SelectionStart = offset + 1;
                                textEditor.SelectionLength = 1;
                                textEditor.ScrollTo(err.Line, err.Column);
                            };
                        }

                        ErrorPanel.Children.Add(errBlock);
                    });
                }
            };

            string compiledFile = System.IO.Path.ChangeExtension(DocumentPath, "uib");
            bool isSuccess = false;
            try
            {
                isSuccess = MarkupCompiler.Compile(
                    new[]
                    {
                        new CompilerInput()
                        {
                            SourceFileName = DocumentPath,
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
                Microsoft.Iris.Application.Initialize();
            }
            catch { }

            try
            {
                Microsoft.Iris.Application.Window.SetBackgroundColor(new WindowColor(0xE6, 0xE6, 0xE6));
                Microsoft.Iris.Application.Window.RequestLoad("file://" + compiledFile + (string.IsNullOrEmpty(UIRoot) ? string.Empty : "#" + UIRoot));
                Microsoft.Iris.Application.Window.CloseRequested += (object sender, WindowCloseRequestedEventArgs args) =>
                {
                    args.BlockCloseRequest();
                    Microsoft.Iris.Application.Window.Visible = false;
                };
                Microsoft.Iris.Application.Run();
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
            if (string.IsNullOrEmpty(DocumentPath))
                return;

            // Save changes before running
            textEditor.Save(DocumentPath);
            // Set UI root
            UIRoot = UIRootBox.Text;

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

            DocumentPath = openFileDialog.FileName;
            textEditor.Load(DocumentPath);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            textEditor.Save(DocumentPath);
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = FILE_FORMAT_FILTER,
                FileName = DocumentPath
            };
            if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            DocumentPath = saveFileDialog.FileName;
            textEditor.Save(DocumentPath);
        }
    }
}
