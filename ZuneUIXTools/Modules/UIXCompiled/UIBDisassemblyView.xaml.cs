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

            ViewModel.SetInspector(e.AddedItems[0]);
        }
    }
}
