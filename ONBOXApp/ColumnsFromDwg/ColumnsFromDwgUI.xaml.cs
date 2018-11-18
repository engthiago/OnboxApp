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
    /// Interaction logic for ColumnsFromDwgUI.xaml
    /// </summary>
    public partial class ColumnsFromDwgUI : Window
    {
        IList<LevelInfo> LevelInfoList = new List<LevelInfo>();
        IList<DwgLayerInfo> layersInfo = new List<DwgLayerInfo>();

        public ColumnsFromDwgUI(IList<DwgLayerInfo> targetLayersInfo)
        {
            InitializeComponent();
            layersInfo = targetLayersInfo.ToList();
        }

        private void ColumnsFromDWGWindow_Loaded(object sender, RoutedEventArgs e)
        {

            //Populate Information

            #region Populate Rect Families
            ONBOXApplication.storedColumnFamiliesInfo = ColumnsFromDwg.getAllColumnFamilies();
            Label n = new Label();
            comboTypes.ItemsSource = ONBOXApplication.storedColumnFamiliesInfo;

            DataTemplate dFamiliesTemplate = new DataTemplate();
            dFamiliesTemplate.DataType = typeof(FamilyWithImage);

            FrameworkElementFactory fTemplate = new FrameworkElementFactory(typeof(StackPanel));
            fTemplate.Name = "myComboFactory";
            fTemplate.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory fImage = new FrameworkElementFactory(typeof(Image));
            fImage.SetBinding(Image.SourceProperty, new Binding("Image"));
            fTemplate.AppendChild(fImage);

            FrameworkElementFactory fText1 = new FrameworkElementFactory(typeof(Label));
            fText1.SetBinding(Label.ContentProperty, new Binding("FamilyName"));
            fText1.SetValue(Label.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            fText1.SetValue(Label.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
            fTemplate.AppendChild(fText1);

            dFamiliesTemplate.VisualTree = fTemplate;
            comboTypes.ItemTemplate = dFamiliesTemplate;
            if (comboTypes.HasItems)
                comboTypes.SelectedIndex = 0;
            #endregion

            #region Populate Circ Families
            ONBOXApplication.storedColumnFamiliesCircInfo = ColumnsFromDwg.getAllColumnCircularFamilies();
            comboTypesCirc.ItemsSource = ONBOXApplication.storedColumnFamiliesCircInfo;

            DataTemplate dCircFamiliesTemplate = new DataTemplate();
            dCircFamiliesTemplate.DataType = typeof(FamilyWithImage);

            FrameworkElementFactory fCircTemplate = new FrameworkElementFactory(typeof(StackPanel));
            fCircTemplate.Name = "myComboFactory2";
            fCircTemplate.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory fCircImage = new FrameworkElementFactory(typeof(Image));
            fCircImage.SetBinding(Image.SourceProperty, new Binding("Image"));
            fCircTemplate.AppendChild(fCircImage);

            FrameworkElementFactory fCircText1 = new FrameworkElementFactory(typeof(Label));
            fCircText1.SetBinding(Label.ContentProperty, new Binding("FamilyName"));
            fCircText1.SetValue(Label.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            fCircText1.SetValue(Label.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
            fCircTemplate.AppendChild(fCircText1);

            dCircFamiliesTemplate.VisualTree = fCircTemplate;
            comboTypesCirc.ItemTemplate = dCircFamiliesTemplate;
            if (comboTypesCirc.HasItems)
                comboTypesCirc.SelectedIndex = 0;
            #endregion

            #region Populate ComboBox Layers
            comboLayers.ItemsSource = layersInfo;
            DataTemplate dLayersTemplate = new DataTemplate();
            dLayersTemplate.DataType = typeof(string);
            FrameworkElementFactory fLayersFact = new FrameworkElementFactory(typeof(StackPanel));
            fLayersFact.Name = "layersFactory";
            fLayersFact.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory fLayerTick = new FrameworkElementFactory(typeof(StackPanel));
            fLayerTick.SetValue(StackPanel.BackgroundProperty, new SolidColorBrush(Colors.Black));
            fLayerTick.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            fLayerTick.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            fLayerTick.SetValue(StackPanel.HeightProperty, 12d);
            fLayerTick.SetValue(StackPanel.WidthProperty, 12d);
            FrameworkElementFactory fLayerColor = new FrameworkElementFactory(typeof(StackPanel));
            fLayerColor.SetBinding(StackPanel.BackgroundProperty, new Binding("ColorBrush"));
            fLayerColor.SetValue(StackPanel.HeightProperty, 10d);
            fLayerColor.SetValue(StackPanel.WidthProperty, 10d);
            fLayerColor.SetValue(StackPanel.MarginProperty, new Thickness(1, 1, 1, 1));
            fLayerColor.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            fLayerColor.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Bottom);
            fLayerTick.AppendChild(fLayerColor);
            fLayersFact.AppendChild(fLayerTick);

            FrameworkElementFactory fParentForNameLabel = new FrameworkElementFactory(typeof(StackPanel));
            FrameworkElementFactory fLayerName = new FrameworkElementFactory(typeof(Label));
            fLayerName.SetBinding(Label.ContentProperty, new Binding("Name"));
            fLayerName.SetValue(Label.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Left);
            fLayerName.SetValue(Label.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Top);
            fParentForNameLabel.AppendChild(fLayerName);
            fLayersFact.AppendChild(fParentForNameLabel);

            dLayersTemplate.VisualTree = fLayersFact;
            comboLayers.ItemTemplate = dLayersTemplate;
            if (comboLayers.HasItems)
                comboLayers.SelectedIndex = 0;
            #endregion

            PopulateGridLevel(true);

        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            ONBOXApplication.selectedColumnFamily = comboTypes.SelectedIndex;
            ONBOXApplication.selectedColumnCircFamily = comboTypesCirc.SelectedIndex;

            for (int i = 0; i < LevelInfoList.Count; i++)
            {
                object cellInfo = gridLevel.Items.GetItemAt(i);
                LevelInfoList.ElementAt(i).willBeNumbered = (cellInfo as LevelInfo).willBeNumbered;
            }

            ONBOXApplication.StoredColumnsDwgLevels = LevelInfoList.ToList();
            this.DialogResult = true;
        }

        //Called from the Command to know the name of the selected layer
        internal string GetSelectedLayerName()
        {
            object comboInfo = comboLayers.SelectedItem;
            return (comboInfo as DwgLayerInfo).Name;
        }

        private void gridLevel_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void gridLevel_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ContextMenu ct = new ContextMenu();

            MenuItem selectAll = new MenuItem();
            selectAll.Header = Properties.WindowLanguage.ColumnsFromCAD_SelectAll;
            selectAll.Click += selectAll_Click;
            ct.Items.Add(selectAll);

            MenuItem selectNone = new MenuItem();
            selectNone.Header = Properties.WindowLanguage.ColumnsFromCAD_SelectNone;
            selectNone.Click += selectNone_Click;
            ct.Items.Add(selectNone);

            MenuItem selectInvert = new MenuItem();
            selectInvert.Header = Properties.WindowLanguage.ColumnsFromCAD_InvertSelection;
            selectInvert.Click += selectInvert_Click;
            ct.Items.Add(selectInvert);

            gridLevel.ContextMenu = ct;
            gridLevel.ContextMenu.PlacementTarget = gridLevel;
            gridLevel.ContextMenu.IsOpen = true;
        }

        private void PopulateGridLevel(bool isFirstTime = false)
        {
            #region Populate DataGrid
            if (gridLevel != null)
            {
                if (gridLevel.HasItems)
                    gridLevel.Columns.Clear();

                if (isFirstTime)
                {
                    LevelInfoList = ColumnsFromDwg.GetAllLevelInfo();

                    if (ONBOXApplication.StoredColumnsDwgLevels.Count == 0)
                    {
                        ONBOXApplication.StoredColumnsDwgLevels = ColumnsFromDwg.GetAllLevelInfo();
                        ONBOXApplication.StoredColumnsDwgLevels.Last().willBeNumbered = false;
                    }

                    foreach (LevelInfo currentStoredLevelInfo in ONBOXApplication.StoredColumnsDwgLevels)
                    {
                        foreach (LevelInfo currentLevelInfo in LevelInfoList)
                        {
                            if (currentLevelInfo.levelId == currentStoredLevelInfo.levelId)
                            {
                                currentLevelInfo.willBeNumbered = currentStoredLevelInfo.willBeNumbered;
                            }
                        }
                        LevelInfoList.Last().willBeNumbered = false;
                    }
                }

                gridLevel.AutoGenerateColumns = false;
                gridLevel.CanUserAddRows = false;
                gridLevel.CanUserDeleteRows = false;
                gridLevel.CanUserResizeRows = false;
                gridLevel.CanUserReorderColumns = false;
                gridLevel.ItemsSource = LevelInfoList.ToList();

                DataGridCheckBoxColumn dt0 = new DataGridCheckBoxColumn();
                dt0.Header = Properties.WindowLanguage.ColumnsFromCAD_LevelUse;
                dt0.Binding = new Binding("willBeNumbered");
                dt0.CanUserSort = false;
                dt0.Width = 50;

                DataGridTextColumn dt1 = new DataGridTextColumn();
                dt1.Header = Properties.WindowLanguage.ColumnsFromCAD_LevelName;
                dt1.Binding = new Binding("levelName");
                dt1.CanUserSort = false;
                dt1.IsReadOnly = true;
                dt1.Width = 150;

                gridLevel.Columns.Add(dt1);
                gridLevel.Columns.Add(dt0);
            }
            #endregion

            #region Disable LastLevel

            gridLevel.IsSynchronizedWithCurrentItem = false;
            gridLevel.EnableColumnVirtualization = false;
            gridLevel.EnableRowVirtualization = false;

            int lastNumber = LevelInfoList.IndexOf(LevelInfoList.Last());
            LevelInfo lastLvlInfo = gridLevel.Items.GetItemAt(lastNumber) as LevelInfo;
            DataGridColumn targetColumn = gridLevel.Columns.ElementAt(1);

            //This process is to disable the last (higher) level from the gridLevels
            //We have to get down to the specific cell and get the content of the cell and then convert it to a checkbox since no other way worked as expected
            gridLevel.Focus();
            gridLevel.ScrollIntoView(lastLvlInfo);
            DataGridCellInfo cellInfo = new DataGridCellInfo(lastLvlInfo, targetColumn);
            gridLevel.CurrentCell = cellInfo;
            //gridLevel.BeginEdit();
            DataGridRow row = (DataGridRow)gridLevel.ItemContainerGenerator.ContainerFromIndex(gridLevel.Items.IndexOf(gridLevel.CurrentCell.Item));
            DataGridCell cel = cellInfo.Column.GetCellContent(row).Parent as DataGridCell;
            CheckBox lastCheckBox = cel.Content as CheckBox;
            lastCheckBox.IsEnabled = false;
            lastCheckBox.IsChecked = false;

            #endregion
        }

        void selectAll_Click(object sender, RoutedEventArgs e)
        {
            ToggleLvlInfoList(true);
            PopulateGridLevel();
        }

        void selectInvert_Click(object sender, RoutedEventArgs e)
        {
            InvertLvlInfoList();
            PopulateGridLevel();
        }

        void selectNone_Click(object sender, RoutedEventArgs e)
        {
            ToggleLvlInfoList(false);
            PopulateGridLevel();
        }

        void ToggleLvlInfoList(bool check)
        {
            foreach (LevelInfo currentLevelInfo in LevelInfoList)
            {
                if (!check)
                    currentLevelInfo.willBeNumbered = false;
                else
                    currentLevelInfo.willBeNumbered = true;
            }
        }

        void InvertLvlInfoList()
        {
            foreach (LevelInfo currentLevelInfo in LevelInfoList)
            {
                if (currentLevelInfo.willBeNumbered)
                    currentLevelInfo.willBeNumbered = false;
                else
                    currentLevelInfo.willBeNumbered = true;
            }
        }
    }
}
