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
    /// Interaction logic for BeamsFromEntireBuildingUI.xaml
    /// </summary>
    public partial class BeamsFromEntireBuildingUI : Window
    {
        IList<FamilyWithImage> currentFamilyList = new List<FamilyWithImage>();
        BeamsFromEntireBuilding currentCommand = null;

        #region Consts

        double minBeamWidth = 10;
        double defaultBeamWidth = 14;
        double maxBeamWidth = 40;

        double minBeamHeight = 20;
        double defaultBeamHeight = 60;
        double maxBeamHeight = 150;

        #endregion

        #region Properties

        internal FamilyWithImage CurrentFamilyWithImage { get; set; }
        internal double BeamWidth
        {
            get
            {
                double tempWidth = 0;
                if (double.TryParse(textWidth.Text, out tempWidth))
                {
                    if (tempWidth < minBeamWidth || tempWidth > maxBeamWidth)
                        return defaultBeamWidth;
                    else
                        return tempWidth;
                }
                return defaultBeamWidth;
            }

            set { return; }
        }
        internal double BeamHeight
        {
            get
            {
                double tempHeight = 0;
                if (double.TryParse(textHeight.Text, out tempHeight))
                {
                    if (tempHeight < minBeamHeight || tempHeight > maxBeamHeight)
                        return defaultBeamHeight;
                    else
                        return tempHeight;
                }
                return defaultBeamHeight;
            }
            set { return; }
        }
        internal bool IsLinked { get { return (bool)checkIsLinked.IsChecked; } set { return; } }
        internal RevitLinksInfo SelectedRevitLinkInfo
        {
            get
            {
                if (gridLinkedFiles.SelectedIndex == -1)
                {
                    if (gridLinkedFiles.HasItems)
                    {
                        gridLinkedFiles.SelectedIndex = 0;
                    }
                }

                RevitLinksInfo itemToReturn = gridLinkedFiles.SelectedItem as RevitLinksInfo;

                return itemToReturn;
            }

            set { return; }
        }
        internal bool CreateBeamsInIntermediateLevels { get { return (bool)checkIntermediateLevels.IsChecked; } }
        internal bool GroupAndDuplicateLevels { get { return (bool)checkGroupCopy.IsChecked; } }
        internal int MinWallWidth
        {
            get
            {
                int currentMinWallWidth = 0;
                if (int.TryParse(textMinWallWidth.Text, out currentMinWallWidth))
                {
                    return currentMinWallWidth;
                }
                else
                {
                    return 14;
                }
            }
        }

        //We dont need getter here because it will be validate from the child UI Window
        internal IList<LevelInfo> LevelInfoList { get; set; }
        internal bool PickStandardLevelsByName { get; set; }
        internal string StandardLevelName { get; set; }
        internal bool IsCaseSensitive { get; set; }


        #endregion

        internal BeamsFromEntireBuildingUI(BeamsFromEntireBuilding targetCommand, IList<FamilyWithImage> familyWihImageList)
        {
            InitializeComponent();

            //set default properties on the constructor
            currentCommand = targetCommand;
            PickStandardLevelsByName = true;
            StandardLevelName = Properties.WindowLanguage.BeamsForBuildingLevelOptions_Standard;
            currentFamilyList = familyWihImageList;
            LevelInfoList = currentCommand.GetAllLevelInfo(false);

        }

        private void BeamsEntireBuildingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //set default UI Config
            if (checkIntermediateLevels != null)
                checkIntermediateLevels.IsChecked = true;
            if (textMinWallWidth != null)
                textMinWallWidth.Text = MinWallWidth.ToString();
            if (textWidth != null)
                textWidth.Text = defaultBeamWidth.ToString();
            if (textHeight != null)
                textHeight.Text = defaultBeamHeight.ToString();

            checkJoinBeams.IsChecked = false;

            if (comboFamily != null)
            {
                comboFamily.ItemsSource = currentFamilyList;

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
                comboFamily.ItemTemplate = dFamiliesTemplate;
                if (comboFamily.HasItems)
                    comboFamily.SelectedIndex = 0;
            }
            if (gridLinkedFiles != null)
            {
                gridLinkedFiles.AutoGenerateColumns = false;
                gridLinkedFiles.CanUserAddRows = false;
                gridLinkedFiles.CanUserDeleteRows = false;
                gridLinkedFiles.CanUserReorderColumns = false;
                gridLinkedFiles.CanUserSortColumns = false;
                gridLinkedFiles.CanUserResizeRows = false;
                gridLinkedFiles.ItemsSource = Utils.GetInformation.GetAllRevitInstances(currentCommand.doc);

                if (gridLinkedFiles.HasItems == false)
                {
                    gridLinkedFiles.IsEnabled = false;
                    checkIsLinked.IsEnabled = false;
                }

                DataGridTextColumn dt1 = new DataGridTextColumn();
                dt1.Header = Properties.WindowLanguage.BeamsForBuilding_LinkedFileName;
                dt1.Binding = new Binding("Name");
                dt1.IsReadOnly = true;
                dt1.CanUserSort = false;

                gridLinkedFiles.Columns.Add(dt1);
            }

            PopulateGridLevel();

        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            CurrentFamilyWithImage = (comboFamily.SelectedItem as FamilyWithImage);

            this.DialogResult = true;

        }

        private void btnLevels_Click(object sender, RoutedEventArgs e)
        {
            BeamsFromEntireBuildingChooseStandardFloorsUI beamsStandardFloorsUI = new BeamsFromEntireBuildingChooseStandardFloorsUI(LevelInfoList, PickStandardLevelsByName, StandardLevelName, IsCaseSensitive);
            beamsStandardFloorsUI.ShowDialog();

            if (beamsStandardFloorsUI.DialogResult == true)
            {
                LevelInfoList = beamsStandardFloorsUI.currentLevelInfoList;
                PickStandardLevelsByName = beamsStandardFloorsUI.pickStandardLevelsByName;
                StandardLevelName = beamsStandardFloorsUI.standardLevelName;
                IsCaseSensitive = beamsStandardFloorsUI.isCaseSensitive;
            }

        }

        private void PopulateGridLevel()
        {
            if (gridLevel != null)
            {
                if (gridLevel.HasItems)
                {
                    gridLevel.Columns.Clear();
                }

                gridLevel.ItemsSource = LevelInfoList;
                gridLevel.AutoGenerateColumns = false;
                gridLevel.CanUserAddRows = false;
                gridLevel.CanUserDeleteRows = false;
                gridLevel.CanUserReorderColumns = false;
                gridLevel.CanUserSortColumns = false;
                gridLevel.CanUserResizeRows = false;

                DataGridTextColumn dt1 = new DataGridTextColumn();
                dt1.Header = Properties.WindowLanguage.BeamsForBuilding_LevelName;
                dt1.Binding = new Binding("levelName");
                dt1.CanUserSort = false;
                dt1.IsReadOnly = true;
                dt1.Width = 150;

                DataGridCheckBoxColumn dt2 = new DataGridCheckBoxColumn();
                dt2.Header = Properties.WindowLanguage.BeamsForBuilding_LevelUse;
                dt2.Binding = new Binding("willBeNumbered");
                dt2.CanUserSort = false;
                dt2.Width = 50;

                gridLevel.Columns.Add(dt1);
                gridLevel.Columns.Add(dt2);
            }
        }

        private void checkIsLinked_Click(object sender, RoutedEventArgs e)
        {
            if (checkIsLinked.IsChecked == true)
            {
                LevelInfoList = currentCommand.GetAllLevelInfo(true, SelectedRevitLinkInfo.Id);
                IsLinked = true;
                checkJoinBeams.IsEnabled = false;
                checkJoinBeams.IsChecked = false;
            }
            else
            {
                LevelInfoList = currentCommand.GetAllLevelInfo(false);
                IsLinked = false;
                checkJoinBeams.IsEnabled = true;
            }

            PopulateGridLevel();
        }

        private void gridLinkedFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLinked)
            {
                SelectedRevitLinkInfo = gridLinkedFiles.SelectedItem as RevitLinksInfo;
                LevelInfoList = currentCommand.GetAllLevelInfo(true, SelectedRevitLinkInfo.Id);
                PopulateGridLevel();
            }
        }

        private void gridLevel_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ContextMenu ct = new ContextMenu();

            MenuItem selectAll = new MenuItem();
            selectAll.Header = Properties.WindowLanguage.BeamsForBuilding_SelectAll;
            selectAll.Click += selectAll_Click;
            ct.Items.Add(selectAll);

            MenuItem selectNone = new MenuItem();
            selectNone.Header = Properties.WindowLanguage.BeamsForBuilding_SelectNone;
            selectNone.Click += selectNone_Click;
            ct.Items.Add(selectNone);

            MenuItem selectInvert = new MenuItem();
            selectInvert.Header = Properties.WindowLanguage.BeamsForBuilding_Invert;
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
