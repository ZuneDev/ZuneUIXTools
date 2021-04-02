using Microsoft.Iris;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            MarkupSystem.Startup(true);
            bool isSuccess = MarkupCompiler.Compile(
                new[]
                {
                    new CompilerInput()
                    {
                        OutputFileName = @"D:\Repos\yoshiask\ZuneUIXTools\test\testA.uib",
                        SourceFileName = @"D:\Repos\yoshiask\ZuneUIXTools\test\testA.uix"
                    }
                },
                new CompilerInput()
            );
        }
    }
}
