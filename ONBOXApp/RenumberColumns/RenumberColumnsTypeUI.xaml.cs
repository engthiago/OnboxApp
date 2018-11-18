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
    /// Interaction logic for RenumberColumnsTypeUI.xaml
    /// </summary>
    public partial class RenumberColumnsTypeUI : Window
    {
        IList<ColumnTypesInfo> localColumnTypesInfo = new List<ColumnTypesInfo>();

        public RenumberColumnsTypeUI(Window targetParentWindow)
        {
            if (targetParentWindow is RenumberColumnsSelectionUI)
                localColumnTypesInfo = (targetParentWindow as RenumberColumnsSelectionUI).columnTypesInfo;
            else
                localColumnTypesInfo = (targetParentWindow as RenumberColumnsUI).columnTypesInfo;

            InitializeComponent();
        }

        private void RenumberColumnsTypeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (ColumnTypesInfo currentStoredInfo in ONBOXApplication.storedColumnTypesInfo)
            {
                foreach (ColumnTypesInfo currentInfo in localColumnTypesInfo)
                {
                    if (currentInfo.TypeId == currentStoredInfo.TypeId)
                    {
                        currentInfo.WillBeNumbered = currentStoredInfo.WillBeNumbered;
                        currentInfo.TypePrefix = currentStoredInfo.TypePrefix;
                    }
                }
            }

            gridTypes.AutoGenerateColumns = false;
            gridTypes.CanUserAddRows = false;
            gridTypes.CanUserDeleteRows = false;
            gridTypes.CanUserResizeRows = false;
            gridTypes.CanUserReorderColumns = false;
            gridTypes.ItemsSource = localColumnTypesInfo;

            DataGridTextColumn dt1 = new DataGridTextColumn();
            dt1.Header = Properties.WindowLanguage.RenumberColumnsTypeOptions_Name;
            dt1.Binding = new Binding("TypeName");
            dt1.CanUserSort = false;
            dt1.IsReadOnly = true;
            dt1.Width = 150;

            DataGridCheckBoxColumn dt2 = new DataGridCheckBoxColumn();
            dt2.Header = Properties.WindowLanguage.RenumberColumnsTypeOptions_Use;
            dt2.Binding = new Binding("WillBeNumbered");
            dt2.CanUserSort = false;
            dt2.Width = 50;

            DataGridTextColumn dt3 = new DataGridTextColumn();
            dt3.Header = Properties.WindowLanguage.RenumberColumnsTypeOptions_Prefix;
            dt3.Binding = new Binding("TypePrefix");
            dt3.CanUserSort = false;
            dt3.Width = 60;

            gridTypes.Columns.Add(dt1);
            gridTypes.Columns.Add(dt2);
            gridTypes.Columns.Add(dt3);
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < gridTypes.Items.Count; i++)
            {
                object cellInfo = gridTypes.Items.GetItemAt(i);
                localColumnTypesInfo.ElementAt(i).WillBeNumbered = (cellInfo as ColumnTypesInfo).WillBeNumbered;
                localColumnTypesInfo.ElementAt(i).TypePrefix = (cellInfo as ColumnTypesInfo).TypePrefix;
            }

            ONBOXApplication.storedColumnTypesInfo = localColumnTypesInfo.ToList();
            this.Close();
        }

    }
}
