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
    /// Interaction logic for renumberParkingUI.xaml
    /// </summary>
    public partial class renumberParkingUI : Window
    {

        public renumberParkingUI()
        {
            InitializeComponent();
        }

        private void btnRenumber_Click(object sender, RoutedEventArgs e)
        {
            if ((ONBOXApplication.currentFirstParking == null) && (comboFirstParking.SelectedIndex == 2))
            {
                MessageBox.Show(Properties.Messages.RenumberParking_SelectFirstOrChangeNumbering);
            }
            else
            {
                this.DialogResult = true;
                this.Close();
            }

            if (comboFirstParking.SelectedIndex == 0)
            {
                ONBOXApplication.parkingRenumType = ONBOXApplication.RenumberType.Ascending;
            }
            else
            {
                if (comboFirstParking.SelectedIndex == 1)
                {
                    ONBOXApplication.parkingRenumType = ONBOXApplication.RenumberType.Descending;
                }
                else
                {
                    ONBOXApplication.parkingRenumType = ONBOXApplication.RenumberType.FirstParking;
                }
            }
        }

        private void btnSelectParking_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            RenumberParking.selectParking(this);
            ONBOXApplication.parkingRenumType = ONBOXApplication.RenumberType.FirstParking;
        }

        internal void comboFirstAdjust()
        {

            if (ONBOXApplication.parkingRenumType == ONBOXApplication.RenumberType.FirstParking)
            {
                this.comboFirstParking.SelectedIndex = 2;
            }
            else
            {
                if (ONBOXApplication.parkingRenumType == ONBOXApplication.RenumberType.Ascending)
                {
                    this.comboFirstParking.SelectedIndex = 0;
                }
                else
                {
                    this.comboFirstParking.SelectedIndex = 1;
                }
            }

            this.ShowDialog();
        }

        private void btnPrefix_Click(object sender, RoutedEventArgs e)
        {
            renumberParkingPrefixUI prefixUI = new renumberParkingPrefixUI();

            prefixUI.ShowDialog();
        }

        private void btnRenumberTypes_Click(object sender, RoutedEventArgs e)
        {
            RenumberTypeUI TypeUI = new RenumberTypeUI();

            TypeUI.ShowDialog();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (ONBOXApplication.storedParkingTypesInfo.Count == 0)
            {
                ONBOXApplication.storedParkingTypesInfo = RenumberParking.getAllParkingTypesInfo();
            }
            if (ONBOXApplication.storedParkingLevelInfo.Count == 0)
            {
                ONBOXApplication.storedParkingLevelInfo = RenumberParking.getAllLevels();
            }
        }
    }
}
