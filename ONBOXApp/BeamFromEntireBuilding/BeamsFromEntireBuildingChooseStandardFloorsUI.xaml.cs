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
    /// Interaction logic for BeamsFromEntireBuildingChooseStandardFloorsUI.xaml
    /// </summary>
    public partial class BeamsFromEntireBuildingChooseStandardFloorsUI : Window
    {
        internal IList<LevelInfo> currentLevelInfoList = new List<LevelInfo>();
        internal bool pickStandardLevelsByName = true;
        internal string standardLevelName = "";
        internal bool isCaseSensitive = false;

        internal BeamsFromEntireBuildingChooseStandardFloorsUI(IList<LevelInfo> targetLevelInfo, bool targetPickStandardLevelsByName, string targetStandardLevelName, bool targetIsCaseSensitive)
        {
            InitializeComponent();
            currentLevelInfoList = targetLevelInfo;
            pickStandardLevelsByName = targetPickStandardLevelsByName;
            standardLevelName = targetStandardLevelName;
        }

        private void BeamsEntireBuildingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //set default values
            textStandardLevelName.Text = standardLevelName;
            radioPickStandardLevelsByName.IsChecked = true;
            checkIsCaseSensitive.IsChecked = isCaseSensitive;

            PopulateGridLevel();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            currentLevelInfoList = gridLevel.ItemsSource as List<LevelInfo>;
            pickStandardLevelsByName = (bool)radioPickStandardLevelsByName.IsChecked;
            standardLevelName = string.IsNullOrWhiteSpace(textStandardLevelName.Text) ? Properties.WindowLanguage.BeamsForBuildingLevelOptions_Standard : textStandardLevelName.Text;

            this.DialogResult = true;
        }

        private void radioPickStandardLevelsByName_Click(object sender, RoutedEventArgs e)
        {
            checkControlsEnabled();
        }

        private void radioPickStandardLevels_Click(object sender, RoutedEventArgs e)
        {
            checkControlsEnabled();
        }

        private void checkControlsEnabled()
        {
            if (radioPickStandardLevelsByName.IsChecked == true)
            {
                textStandardLevelName.IsEnabled = true;
                checkIsCaseSensitive.IsEnabled = true;
                gridLevel.IsEnabled = false;
            }
            if (radioPickStandardLevels.IsChecked == true)
            {
                textStandardLevelName.IsEnabled = false;
                checkIsCaseSensitive.IsEnabled = false;
                gridLevel.IsEnabled = true;
            }
        }

        private void gridLevel_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ContextMenu ct = new ContextMenu();

            MenuItem selectAll = new MenuItem();
            selectAll.Header = Properties.WindowLanguage.BeamsForBuildingLevelOptions_SelectAll;
            selectAll.Click += selectAll_Click;
            ct.Items.Add(selectAll);

            MenuItem selectNone = new MenuItem();
            selectNone.Header = Properties.WindowLanguage.BeamsForBuildingLevelOptions_SelectNone;
            selectNone.Click += selectNone_Click;
            ct.Items.Add(selectNone);

            MenuItem selectInvert = new MenuItem();
            selectInvert.Header = Properties.WindowLanguage.BeamsForBuildingLevelOptions_Invert;
            selectInvert.Click += selectInvert_Click;
            ct.Items.Add(selectInvert);

            gridLevel.ContextMenu = ct;
            gridLevel.ContextMenu.PlacementTarget = gridLevel;
            gridLevel.ContextMenu.IsOpen = true;
        }

        void selectAll_Click(object sender, RoutedEventArgs e)
        {
            ToggleLvlInfoList(true);
            PopulateGridLevel();
        }

        private void PopulateGridLevel()
        {
            checkControlsEnabled();

            if (gridLevel != null)
            {
                if (currentLevelInfoList != null)
                {
                    if (gridLevel.HasItems)
                    {
                        gridLevel.Columns.Clear();
                    }

                    gridLevel.ItemsSource = currentLevelInfoList;
                    gridLevel.AutoGenerateColumns = false;
                    gridLevel.CanUserAddRows = false;
                    gridLevel.CanUserDeleteRows = false;
                    gridLevel.CanUserReorderColumns = false;
                    gridLevel.CanUserSortColumns = false;
                    gridLevel.CanUserResizeRows = false;
                    gridLevel.GridLinesVisibility = DataGridGridLinesVisibility.None;

                    DataGridTextColumn dt1 = new DataGridTextColumn();
                    dt1.Header = Properties.WindowLanguage.BeamsForBuildingLevelOptions_Name; ;
                    dt1.Binding = new Binding("levelName");
                    dt1.CanUserSort = false;
                    dt1.IsReadOnly = true;
                    dt1.Width = 150;

                    DataGridCheckBoxColumn dt2 = new DataGridCheckBoxColumn();
                    dt2.Header = Properties.WindowLanguage.BeamsForBuildingLevelOptions_IsStandard;
                    dt2.Binding = new Binding("isStandardLevel");
                    dt2.CanUserSort = false;
                    dt2.Width = 100;

                    gridLevel.Columns.Add(dt1);
                    gridLevel.Columns.Add(dt2); 
                }
            }
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
            foreach (LevelInfo currentLevelInfo in currentLevelInfoList)
            {
                if (!check)
                    currentLevelInfo.isStandardLevel = false;
                else
                    currentLevelInfo.isStandardLevel = true;
            }
        }

        void InvertLvlInfoList()
        {
            foreach (LevelInfo currentLevelInfo in currentLevelInfoList)
            {
                if (currentLevelInfo.isStandardLevel)
                    currentLevelInfo.isStandardLevel = false;
                else
                    currentLevelInfo.isStandardLevel = true;
            }
        }

    }
}
