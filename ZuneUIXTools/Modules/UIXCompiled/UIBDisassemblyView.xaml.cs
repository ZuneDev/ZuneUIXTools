using MahApps.Metro.Controls;
using Microsoft.Iris.Debug.Data;
using System.Windows;
using System.Windows.Controls;

namespace ZuneUIXTools.Modules.UIXCompiled
{
    /// <summary>
    /// Interaction logic for UIBDisassemblyView.xaml
    /// </summary>
    public partial class UIBDisassemblyView : UserControl
    {
        public UIBDisassemblyViewModel ViewModel => (UIBDisassemblyViewModel)DataContext;

        public UIBDisassemblyView()
        {
            InitializeComponent();

            InstructionListView.Loaded += InstructionListView_Loaded;
        }

        private void InstructionListView_Loaded(object sender, RoutedEventArgs e)
        {
            InstructionListView.Loaded -= InstructionListView_Loaded;
            InstructionListView.FindChild<ScrollViewer>().ScrollChanged += InstructionListView_ScrollChanged;
        }

        private void InstructionListView_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ViewModel.AutoScroll && e.ExtentHeightChange != 0)
                ((ScrollViewer)sender).ScrollToBottom();
        }

        private void InstructionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count <= 0)
                return;

            ViewModel.SetInspector(e.AddedItems[0] as InterpreterEntry);
        }
    }
}
