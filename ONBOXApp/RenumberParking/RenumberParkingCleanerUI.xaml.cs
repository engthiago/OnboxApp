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
using System.Windows.Shapes;

namespace ONBOXAppl
{
    /// <summary>
    /// Interaction logic for RenumberParkingCleanerUI.xaml
    /// </summary>
    public partial class RenumberParkingCleanerUI : Window
    {
        public RenumberParkingCleanerUI()
        {
            InitializeComponent();
        }

        private void btnLevel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            RenumberCleaner.RenumClean(false);
        }

        private void btnAll_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            RenumberCleaner.RenumClean(true);
        }
    }
}
