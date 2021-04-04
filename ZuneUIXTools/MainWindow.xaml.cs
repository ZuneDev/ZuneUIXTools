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
                        ErrorPanel.Children.Add(new TextBlock
                        {
                            Text = $"Ln {err.Line}, Col {err.Column}: {err.Message}",
                            Margin = new Thickness(0, 0, 0, 4)
                        });

                        // Highlight the error in the code editor
                        int offset = textEditor.Document.GetOffset(err.Line, err.Column);
                        textEditor.SelectionStart = offset;
                        //textEditor.SelectionLength = err.
                        //var anchor = textEditor.Document.CreateAnchor(offset);
                        //textEditor.Document.GetLineByOffset(offset)
                    });
                }
            };

            string compiledFile = System.IO.Path.ChangeExtension(DocumentPath, "uib");
            bool isSuccess = MarkupCompiler.Compile(
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

            try
            {
                Microsoft.Iris.Application.Initialize();
            }
            catch { }

            try
            {
                Microsoft.Iris.Application.Window.SetBackgroundColor(new WindowColor(0xE6, 0xE6, 0xE6));
                Microsoft.Iris.Application.Window.RequestLoad("file://" + compiledFile + (string.IsNullOrEmpty(UIRoot) ? string.Empty : "#" + UIRoot));
                Microsoft.Iris.Application.Run();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    ErrorPanel.Children.Add(new TextBlock
                    {
                        Text = $"Load Failed: {ex.Message}",
                        Margin = new Thickness(0, 0, 0, 4)
                    })
                );
            }

            //Microsoft.Iris.Application.Window.
            //Microsoft.Iris.Application.Shutdown();

            // This code is run AFTER the Iris window is closed
        }

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(DocumentPath))
                return;

            // Save changes before running
            textEditor.Save(DocumentPath);
            // Set UI root
            UIRoot = UIRootBox.Text;

            Thread newWindowThread = new Thread(new ThreadStart(BuildAndRun));
            newWindowThread.SetApartmentState(ApartmentState.STA);
            newWindowThread.IsBackground = true;
            newWindowThread.Start();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Microsoft Iris UI (*.uix)|*.uix|XML files (*.xml)|*.xml"
            };
            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            DocumentPath = openFileDialog.FileName;
            textEditor.Load(DocumentPath);
        }
    }
}
