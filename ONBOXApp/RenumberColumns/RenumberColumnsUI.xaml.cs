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
    /// Interaction logic for RenumberColumnsUI.xaml
    /// </summary>
    public partial class RenumberColumnsUI : Window
    {
        internal IList<ColumnTypesInfo> columnTypesInfo = new List<ColumnTypesInfo>();
        internal IList<LevelInfo> columnLevelInfo = new List<LevelInfo>();

        public RenumberColumnsUI()
        {
            InitializeComponent();
        }

        private void RenumberColumnsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (ONBOXApplication.storedColumnRenumOrder == ColumnRenumberOrder.Ascending)
            {
                comboColumnOrder.SelectedIndex = 0;
            }
            else
            {
                comboColumnOrder.SelectedIndex = 1;
            }

            txtLvlIndicator.Text = ONBOXApplication.columnsLevelIndicator;
            txtConcat.Text = ONBOXApplication.columnsConcatWord;

            if (columnLevelInfo.Count == 0)
            {
                columnLevelInfo = RenumberColumns.GetAllLevelInfo();
            }
            if (columnTypesInfo.Count == 0)
            {
                columnTypesInfo = RenumberColumns.GetColumTypesInfo();
            }

            if (ONBOXApplication.storedColumnLevelInfo.Count == 0)
            {
                int counter = 1;
                foreach (LevelInfo currentLvlInfo in columnLevelInfo)
                {
                    currentLvlInfo.levelPrefix = (counter++).ToString();
                }
                ONBOXApplication.storedColumnLevelInfo = columnLevelInfo.ToList();
            }
            if (ONBOXApplication.storedColumnTypesInfo.Count == 0)
            {
                ONBOXApplication.storedColumnTypesInfo = columnTypesInfo.ToList();
            }
        }

        private void btnLevels_Click(object sender, RoutedEventArgs e)
        {
            RenumberColumnsLevelsUI renumberColumnsLevelWindow = new RenumberColumnsLevelsUI(this);
            renumberColumnsLevelWindow.ShowDialog();
        }

        private void btnTypes_Click(object sender, RoutedEventArgs e)
        {
            RenumberColumnsTypeUI renumberColumnsTypesWIndow = new RenumberColumnsTypeUI(this);
            renumberColumnsTypesWIndow.ShowDialog();
        }

        private void btnRenumber_Click(object sender, RoutedEventArgs e)
        {
            RenumberColumns.DoRenumbering(false, 1);
            this.DialogResult = true;
        }
    }
}
