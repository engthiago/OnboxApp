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
    /// Interaction logic for renumberBlocks.xaml
    /// </summary>
    public partial class renumberBlocksUI : Window
    {
        string prefix = "";
        string number = "";

        public renumberBlocksUI()
        {
            InitializeComponent();
        }

        public renumberBlocksUI(string initPrefix, string initNumber)
        {
            InitializeComponent();
            prefix = initPrefix;
            number = initNumber;
        }

        private void btnLevel_Click(object sender, RoutedEventArgs e)
        {
            thisLevel();
        }

        private void btnAll_Click(object sender, RoutedEventArgs e)
        {
            allLevel();
        }

        private void allLevel()
        {
            int tempNumber = 0;
            string tempPrefix = "";
            RenumberBlockParking.getLevelLastNumber(true, out tempPrefix, out tempNumber);

            txtPrefix.Text = tempPrefix;
            txtNumber.Text = (tempNumber + 1).ToString();
        }

        private void thisLevel()
        {
            int tempNumber = 0;
            string tempPrefix = "";
            RenumberBlockParking.getLevelLastNumber(false, out tempPrefix, out tempNumber);

            txtPrefix.Text = tempPrefix;
            txtNumber.Text = (tempNumber + 1).ToString();
        }

        private void renumberBlocksWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtPrefix.Text = prefix;
            if (number != "")
            {
                txtNumber.Text = (int.Parse(number) + 1).ToString();
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void btnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (txtPrefix.Text != null)
            {
                prefix = txtPrefix.Text;
            }
            int testInt;
            if (int.TryParse(txtNumber.Text, out testInt) == false)
            {
                MessageBox.Show(Properties.Messages.RenumberParking_IntegerNumber);
                return;
            }
            else
            {
                this.Close();
                number = (int.Parse(txtNumber.Text) - 1).ToString();
                RenumberBlockParking.renameBlock(prefix, number, true);
            }
        }

        private void btnMultipleClear_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            RenumberBlockParking.RenumCleanSelected(prefix, number);
        }

        private void btnSelectMultiple_Click(object sender, RoutedEventArgs e)
        {
            if (txtPrefix.Text != null)
            {
                prefix = txtPrefix.Text;
            }
            int testInt;
            if (int.TryParse(txtNumber.Text, out testInt) == false)
            {
                MessageBox.Show(Properties.Messages.RenumberParking_IntegerNumber);
                return;
            }
            else
            {
                this.Close();
                number = (int.Parse(txtNumber.Text) - 1).ToString();
                RenumberBlockParking.renameBlock(prefix, number, false);
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

    }
}
