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
    public partial class RenumberColumnsSelectionUI : Window
    {
        internal IList<ColumnTypesInfo> columnTypesInfo = new List<ColumnTypesInfo>();
        internal IList<LevelInfo> columnLevelInfo = new List<LevelInfo>();

        public RenumberColumnsSelectionUI()
        {
            InitializeComponent();
            GuessNextNumber();
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
                columnLevelInfo = RenumberColumnsSelection.GetAllLevelInfo();
            }
            if (columnTypesInfo.Count == 0)
            {
                columnTypesInfo = RenumberColumnsSelection.GetColumTypesInfo();
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

            GuessNextNumber();
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

        private void btnMultipleClear_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            RenumberColumnsSelection.ClearRenumbering();
        }

        private void btnSelectMultiple_Click(object sender, RoutedEventArgs e)
        {
            int nextNumber = 0;
            if (!int.TryParse(txtNumber.Text, out nextNumber))
            {
                MessageBox.Show(Properties.Messages.RenumberColumns_NoGuess, Properties.Messages.Common_Error);
                return;
            }
            
            if (comboColumnOrder.SelectedIndex == 0)
            {
                ONBOXApplication.storedColumnRenumOrder = ColumnRenumberOrder.Ascending;
            }
            else
            {
                ONBOXApplication.storedColumnRenumOrder = ColumnRenumberOrder.Descending;
            }

            ONBOXApplication.columnsLevelIndicator = txtLvlIndicator.Text;
            ONBOXApplication.columnsConcatWord = txtConcat.Text;
            this.DialogResult = true;

            RenumberColumnsSelection.DoRenumbering(true, nextNumber);
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        
        private void btnGuess_Click(object sender, RoutedEventArgs e)
        {
            GuessNextNumber();
        }

        private void GuessNextNumber()
        {
            int nextNumber = RenumberColumnsSelection.GetLastNumberedColumn() + 1;
            
            txtNumber.Text = nextNumber.ToString();
        }
    }
}
