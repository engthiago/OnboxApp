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
    /// Interaction logic for RenumberGridsUI.xaml
    /// </summary>
    public partial class RenumberGridsUI : Window
    {
        public RenumberGridsUI()
        {
            InitializeComponent();
        }

        private void comboVertical_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboVertical.SelectedIndex == 0)
                comboHorizontal.SelectedIndex = 1;
            if (comboVertical.SelectedIndex == 1)
                comboHorizontal.SelectedIndex = 0;
        }

        private void comboHorizontal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboHorizontal.SelectedIndex == 0)
                comboVertical.SelectedIndex = 1;
            if (comboHorizontal.SelectedIndex == 1)
                comboVertical.SelectedIndex = 0;
        }

        private void btnRenumber_Click(object sender, RoutedEventArgs e)
        {
            if (comboVertical.SelectedIndex == 1)
            {
                RenumberGridsAdvanced.isVerticalGridsNumbered = false;
            }
            else
            {
                RenumberGridsAdvanced.isVerticalGridsNumbered = true;
            }

            if (checkUseSubNum.IsChecked == false)
            {
                RenumberGridsAdvanced.canUseSubNumering = false;

            }
            else
            {
                RenumberGridsAdvanced.canUseSubNumering = true;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
            comboVertical.SelectedIndex = 0;
        }

    }
}
