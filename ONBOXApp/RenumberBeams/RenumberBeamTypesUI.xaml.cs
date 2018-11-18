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
    /// Interaction logic for RenumberBeamTypesUI.xaml
    /// </summary>
    public partial class RenumberBeamTypesUI : Window
    {
        RenumberBeamsUI parentBeamUI = null;
        IList<BeamTypesInfo> beamsInfo = new List<BeamTypesInfo>();

        public RenumberBeamTypesUI(RenumberBeamsUI parentUI)
        {
            InitializeComponent();
            parentBeamUI = parentUI;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < gridTypes.Items.Count; i++)
            {
                object cellInfo = gridTypes.Items.GetItemAt(i);
                parentBeamUI.beamTypesInfo.ElementAt(i).WillBeNumbered = (cellInfo as BeamTypesInfo).WillBeNumbered;
                parentBeamUI.beamTypesInfo.ElementAt(i).TypePrefix = (cellInfo as BeamTypesInfo).TypePrefix;
            }

            ONBOXApplication.storedBeamTypesInfo = parentBeamUI.beamTypesInfo.ToList();
            this.Close();
        }

        private void renumberTypesWindow_Loaded(object sender, RoutedEventArgs e)
        {

            foreach (BeamTypesInfo currentStoredInfo in ONBOXApplication.storedBeamTypesInfo)
            {
                foreach (BeamTypesInfo currentInfo in parentBeamUI.beamTypesInfo)
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
            gridTypes.ItemsSource = parentBeamUI.beamTypesInfo;

            DataGridTextColumn dt1 = new DataGridTextColumn();
            dt1.Header = Properties.WindowLanguage.RenumberBeamsTypeOptions_Name;
            dt1.Binding = new Binding("TypeName");
            dt1.CanUserSort = false;
            dt1.IsReadOnly = true;
            dt1.Width = 150;

            DataGridCheckBoxColumn dt2 = new DataGridCheckBoxColumn();
            dt2.Header = Properties.WindowLanguage.RenumberBeamsTypeOptions_Use;
            dt2.Binding = new Binding("WillBeNumbered");
            dt2.CanUserSort = false;
            dt2.Width = 50;

            DataGridTextColumn dt3 = new DataGridTextColumn();
            dt3.Header = Properties.WindowLanguage.RenumberBeamsTypeOptions_Prefix;
            dt3.Binding = new Binding("TypePrefix");
            dt3.CanUserSort = false;
            dt3.Width = 60;

            gridTypes.Columns.Add(dt1);
            gridTypes.Columns.Add(dt2);
            gridTypes.Columns.Add(dt3);

        }
    }
}
