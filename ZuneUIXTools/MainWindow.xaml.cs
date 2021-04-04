using Microsoft.Iris;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
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
        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Thread newWindowThread = new Thread(new ThreadStart(TestCompile));
            newWindowThread.SetApartmentState(ApartmentState.STA);
            newWindowThread.IsBackground = true;
            newWindowThread.Start();
        }

        private void TestCompile()
        {
            MarkupSystem.Startup(true);
            ErrorManager.OnErrors += (IList errors) => {
                foreach (object obj in errors)
                {
                    var err = obj as ErrorRecord;
                    if (err == null) continue;

#if DEBUG
                    System.Diagnostics.Debug.WriteLine(err.Message);
                    System.Diagnostics.Debugger.Break();
#else
                    Console.WriteLine(err.Message);
#endif
                }
            };

            string testDir = @"D:\Repos\yoshiask\ZuneUIXTools\test\";
            string testName = "text";
            string sourceFile = System.IO.Path.Combine(testDir, testName) + ".uix";
            string compiledFile = System.IO.Path.Combine(testDir, testName) + ".uib";

            bool isSuccess = MarkupCompiler.Compile(
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

            Microsoft.Iris.Application.Initialize();
            Microsoft.Iris.Application.Window.SetBackgroundColor(new WindowColor(80, 0, 0));
            Microsoft.Iris.Application.Window.RequestLoad("file://" + compiledFile + "#TextDisplay");
            Microsoft.Iris.Application.Run();
        }
    }
}
