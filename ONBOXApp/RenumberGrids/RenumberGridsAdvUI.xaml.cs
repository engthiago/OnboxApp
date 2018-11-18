using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    public partial class RenumberGridsAdvUI : Window
    {
        IList<GridInfo> gridInfoList = new List<GridInfo>();
        IList<GridInfo> gridInfoOriginalList = new List<GridInfo>();
        RenumberGridsAdvanced currentCommand = null;

        //new
        public delegate Point GetPosition(IInputElement element);
        int rowIndex = -1;

        internal RenumberGridsAdvUI(IList<GridInfo> targetGridInfo, RenumberGridsAdvanced targetCommand)
        {
            InitializeComponent();
            gridInfoList = targetGridInfo.ToList();
            foreach (GridInfo currentGrid in gridInfoList)
            {
                gridInfoOriginalList.Add(new GridInfo { Id = currentGrid.Id, orientation = currentGrid.orientation });
            }
            currentCommand = targetCommand;

            //new
            gridGrids.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(gridGrids_PreviewMouseLeftButtonDown);
            gridGrids.Drop += new DragEventHandler(gridGrids_Drop);
        }

        private void gridGrids_Drop(object sender, DragEventArgs e)
        {
            if (rowIndex < 0)
                return;
            int index = GetCurrentRowIndex(e.GetPosition);
            if (index < 0)
                return;
            if (index == rowIndex)
                return;
            if (index >= gridGrids.Items.Count - 1)
            {
                return;
            }
            //IList<DataGridCellInfo> productCollection = gridGrids.SelectedCells;
            //DataGridCellInfo changedProduct = productCollection[rowIndex];


            if (rowIndex > (gridGrids.Items.Count - 1))
                return;

            try
            {
                DataGridCellInfo changedProduct = gridGrids.SelectedCells[0];

                GridInfo changedInfo = changedProduct.Item as GridInfo;

                gridInfoList.RemoveAt(rowIndex);
                gridInfoList.Insert(index, changedInfo);

                PopulateGrids();
            }
            catch
            {
            }
        }

        private void gridGrids_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            rowIndex = GetCurrentRowIndex(e.GetPosition);
            if (rowIndex < 0)
                return;
            gridGrids.SelectedIndex = rowIndex;
            GridInfo selectedEmp = gridGrids.Items[rowIndex] as GridInfo;
            if (selectedEmp == null)
                return;
            DragDropEffects dragdropeffects = DragDropEffects.Move;
            if (DragDrop.DoDragDrop(gridGrids, selectedEmp, dragdropeffects)
                                != DragDropEffects.None)
            {
                gridGrids.SelectedItem = selectedEmp;
            }
        }

        private bool GetMouseTargetRow(Visual theTarget, GetPosition position)
        {
            try
            {
                Rect rect = VisualTreeHelper.GetDescendantBounds(theTarget);
                Point point = position((IInputElement)theTarget);
                return rect.Contains(point);
            }
            catch
            {
                return false;
            }
        }

        private DataGridRow GetRowItem(int index)
        {
            if (gridGrids.ItemContainerGenerator.Status
                    != GeneratorStatus.ContainersGenerated)
                return null;
            return gridGrids.ItemContainerGenerator.ContainerFromIndex(index)
                                                            as DataGridRow;
        }

        private int GetCurrentRowIndex(GetPosition pos)
        {
            int curIndex = -1;
            for (int i = 0; i < gridGrids.Items.Count; i++)
            {
                DataGridRow itm = GetRowItem(i);
                if (GetMouseTargetRow(itm, pos))
                {
                    curIndex = i;
                    break;
                }
            }
            return curIndex;
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

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            SetGlobalVariables();
            DialogResult = true;
        }

        private void SetGlobalVariables()
        {
            if (comboVertical.SelectedIndex == 1)
            {
                RenumberGrids.isVerticalGridsNumbered = false;
            }
            else
            {
                RenumberGrids.isVerticalGridsNumbered = true;
            }

            if (checkUseSubNum.IsChecked == false)
            {
                RenumberGrids.canUseSubNumering = false;
            }
            else
            {
                RenumberGrids.canUseSubNumering = true;
            }
        }

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
            comboVertical.SelectedIndex = 0;
            gridInfoList = currentCommand.RenumberTable(gridInfoList);
            PopulateGrids();
        }

        private void PopulateGrids()
        {
            if (gridGrids != null)
            {
                if (gridGrids.Columns.Count > 0)
                {
                    gridGrids.Columns.Clear();
                }

                gridGrids.AutoGenerateColumns = false;
                gridGrids.CanUserAddRows = false;
                gridGrids.CanUserDeleteRows = false;
                gridGrids.CanUserResizeRows = false;
                gridGrids.CanUserResizeColumns = false;
                gridGrids.CanUserReorderColumns = false;
                gridGrids.CanUserSortColumns = false;
                gridGrids.ItemsSource = gridInfoList;

                DataGridTextColumn column0 = new DataGridTextColumn();
                column0.Header = Properties.WindowLanguage.RenumberGrids_NewNumbering;
                column0.Binding = new Binding("newName");
                column0.Width = 120;

                DataGridTextColumn column1 = new DataGridTextColumn();
                column1.Header = Properties.WindowLanguage.RenumberGrids_PrevNumbering;
                column1.Binding = new Binding("prevName");
                column1.IsReadOnly = true;
                column1.Width = 120;

                DataGridTextColumn column2 = new DataGridTextColumn();
                column2.Header = Properties.WindowLanguage.RenumberGrids_Orientation;
                column2.Binding = new Binding("orientation");
                column2.IsReadOnly = true;
                column2.Width = 120;

                gridGrids.Columns.Add(column0);
                gridGrids.Columns.Add(column1);
                gridGrids.Columns.Add(column2);

                gridGrids.Items.Refresh();
            }

        }

        private void ChangeOrientation_Click(object sender, RoutedEventArgs e)
        {
            IList<DataGridCellInfo> selectedCellsInfoList = gridGrids.SelectedCells;

            foreach (DataGridCellInfo currentGridCellInfo in selectedCellsInfoList)
            {
                GridInfo currentGridInfo = currentGridCellInfo.Item as GridInfo;

                foreach (GridInfo currentGridofListInfo in gridInfoList)
                {
                    if (currentGridofListInfo.Id == currentGridInfo.Id)
                    {
                        if (currentGridofListInfo.orientation == "Horizontal")
                            currentGridInfo.orientation = "Vertical";
                        else
                            currentGridInfo.orientation = "Horizontal";
                    }
                }
            }

            PopulateGrids();
        }

        private void btnResetOrientation_Click(object sender, RoutedEventArgs e)
        {
            IList<GridInfo> tempList = gridInfoList.ToList();
            gridInfoList.Clear();

            foreach (GridInfo currentTempGrid in tempList)
            {
                gridInfoList.Add(new GridInfo()
                {
                    Id = currentTempGrid.Id,
                    newName = currentTempGrid.newName,
                    prevName = currentTempGrid.prevName,
                    orientation = gridInfoOriginalList.Where(g => g.Id == currentTempGrid.Id).FirstOrDefault().orientation
                });
            }

            PopulateGrids();
        }

        private void btnResetOrder_Click(object sender, RoutedEventArgs e)
        {
            IList<GridInfo> tempList = gridInfoList.ToList();
            gridInfoList.Clear();

            foreach (GridInfo currentGridOriginal in gridInfoOriginalList)
            {
                gridInfoList.Add(new GridInfo()
                {
                    Id = currentGridOriginal.Id,
                    newName = tempList.Where(g => g.Id == currentGridOriginal.Id).FirstOrDefault().newName,
                    prevName = tempList.Where(g => g.Id == currentGridOriginal.Id).FirstOrDefault().prevName,
                    orientation = tempList.Where(g => g.Id == currentGridOriginal.Id).FirstOrDefault().orientation
                });
            }

            PopulateGrids();
        }

        private void Apply()
        {
            RenumberTable();
            foreach (GridInfo currentGrid in gridInfoList)
            {
                foreach (GridInfo currentSecondGrid in gridInfoList)
                {
                    if (currentGrid.Id == currentSecondGrid.Id)
                        continue;
                    if (currentGrid.newName == currentSecondGrid.newName)
                    {
                        MessageBox.Show(Properties.Messages.RenumberGrids_SameNumbering, Properties.Messages.Common_Error, MessageBoxButton.OK);
                        return;
                    }
                }
            }
            currentCommand.RenumberProcess(gridInfoList);
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            Apply();
        }

        private void btnResetRenumber_Click(object sender, RoutedEventArgs e)
        {
            RenumberTable();
        }

        private void RenumberTable()
        {
            RenumberGridsAdvanced.isVerticalGridsNumbered = (this.comboVertical.SelectedIndex == 0) ? true : false;
            RenumberGridsAdvanced.canUseSubNumering = (this.checkUseSubNum.IsChecked == true) ? true : false;
            gridInfoList = currentCommand.RenumberTable(gridInfoList);
            PopulateGrids();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Apply();
            SetGlobalVariables();
            this.DialogResult = true;
        }
    }
}
