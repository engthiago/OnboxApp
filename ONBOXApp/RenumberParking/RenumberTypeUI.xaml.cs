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
    /// Interaction logic for RenumberTypeUI.xaml
    /// </summary>
    public partial class RenumberTypeUI : Window
    {
        private IList<ParkingTypesInfo> parkInfo = new List<ParkingTypesInfo>();

        public RenumberTypeUI()
        {
            InitializeComponent();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < gridTypes.Items.Count; i++)
            {
                object cellInfo = gridTypes.Items.GetItemAt(i);
                parkInfo.ElementAt(i).willBeNumbered = (cellInfo as ParkingTypesInfo).willBeNumbered;
                parkInfo.ElementAt(i).TypePrefix = (cellInfo as ParkingTypesInfo).TypePrefix;
            }

            ONBOXApplication.storedParkingTypesInfo = parkInfo.ToList();
            this.Close();
        }

        private void RenumberTypeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (ONBOXApplication.storedParkingTypesInfo.Count == 0)
            {
                parkInfo = RenumberParking.getAllParkingTypesInfo();
            }
            else
            {
                parkInfo = RenumberParking.getAllParkingTypesInfo();

                foreach (ParkingTypesInfo currentStoredInfo in ONBOXApplication.storedParkingTypesInfo)
                {
                    foreach (ParkingTypesInfo currentInfo in parkInfo)
                    {
                        if (currentInfo.TypeId == currentStoredInfo.TypeId)
                        {
                            currentInfo.willBeNumbered = currentStoredInfo.willBeNumbered;
                            currentInfo.TypePrefix = currentStoredInfo.TypePrefix;
                        }
                    }
                }
            }


            gridTypes.AutoGenerateColumns = false;
            gridTypes.CanUserAddRows = false;
            gridTypes.CanUserDeleteRows = false;
            gridTypes.CanUserResizeRows = false;
            gridTypes.CanUserReorderColumns = false;
            gridTypes.ItemsSource = parkInfo;

            DataGridTextColumn dt1 = new DataGridTextColumn();
            dt1.Header = Properties.WindowLanguage.RenumberParkTypeOptions_Name;
            dt1.Binding = new Binding("TypeName");
            dt1.CanUserSort = false;
            dt1.IsReadOnly = true;
            dt1.Width = 150;

            DataGridCheckBoxColumn dt2 = new DataGridCheckBoxColumn();
            dt2.Header = Properties.WindowLanguage.RenumberParkTypeOptions_Use;
            dt2.Binding = new Binding("willBeNumbered");
            dt2.CanUserSort = false;
            dt2.Width = 50;

            DataGridTextColumn dt3 = new DataGridTextColumn();
            dt3.Header = Properties.WindowLanguage.RenumberParkTypeOptions_Prefix;
            dt3.Binding = new Binding("TypePrefix");
            dt3.CanUserSort = false;
            dt3.Width = 60;

            gridTypes.Columns.Add(dt1);
            gridTypes.Columns.Add(dt2);
            gridTypes.Columns.Add(dt3);

        }
    }
}
