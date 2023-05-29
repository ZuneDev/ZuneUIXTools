using Microsoft.Iris.Debug.Data;
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
        }

        private void InstructionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count <= 0)
                return;

            ViewModel.SetInspector(e.AddedItems[0] as InterpreterEntry);
        }
    }
}
