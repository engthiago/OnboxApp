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
    /// Interaction logic for ElementsJoinUI.xaml
    /// </summary>
    public partial class ElementsJoinUIAdvanced : Window
    {
        internal IList<LevelInfo> LevelInfoList = new List<LevelInfo>();
        internal int selectedLowerLevel = 0;
        internal int selectedUpperLevel = 0;
        internal bool join = true;

        internal ElementsJoinUIAdvanced(IList<LevelInfo> targetLevelInfoList)
        {
            InitializeComponent();

            LevelInfo underFirstLevel = new LevelInfo() { levelName = Properties.WindowLanguage.JoinUnjoin_DownToLevel + " " + targetLevelInfoList.FirstOrDefault().levelName, levelId = -1 };
            LevelInfo aboveLastLevel = new LevelInfo() { levelName = Properties.WindowLanguage.JoinUnjoin_UpToLevel + " " + targetLevelInfoList.LastOrDefault().levelName, levelId = -1 };
            LevelInfoList.Add(underFirstLevel);
            LevelInfoList = LevelInfoList.Union(targetLevelInfoList).ToList();
            LevelInfoList.Add(aboveLastLevel);
        }

        private void currentJoinWindow_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void lowerLevel_Loaded(object sender, RoutedEventArgs e)
        {
            //Removes the last option (it will be above last level, see constructor)
            IList<LevelInfo> currentLevelListWithoutLast = LevelInfoList.ToList();
            if (currentLevelListWithoutLast.Count > 0)
            {
                currentLevelListWithoutLast.RemoveAt(currentLevelListWithoutLast.Count - 1);
                PopulateComboLevel(comboLowerLevel, currentLevelListWithoutLast); 
            }
        }

        private void upperLevel_Loaded(object sender, RoutedEventArgs e)
        {
            //PopulateComboLevel(comboUpperLevel, LevelInfoList, true);
        }

        private void PopulateComboLevel(ComboBox targetCombo, IList<LevelInfo> targetLevelList, bool selectLast = false)
        {
            if (targetLevelList != null)
            {
                if (targetCombo.HasItems)
                {
                    targetCombo.ItemsSource = new List<LevelInfo>();
                }

                if (targetCombo != null)
                {
                    targetCombo.ItemsSource = targetLevelList.ToList();

                    DataTemplate dFamiliesTemplate = new DataTemplate();
                    dFamiliesTemplate.DataType = typeof(LevelInfo);

                    FrameworkElementFactory fTemplate = new FrameworkElementFactory(typeof(StackPanel));
                    fTemplate.Name = "myComboFactory";
                    fTemplate.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                    fTemplate.SetValue(StackPanel.MarginProperty, new Thickness(0));

                    FrameworkElementFactory fText1 = new FrameworkElementFactory(typeof(Label));
                    fText1.SetBinding(Label.ContentProperty, new Binding("levelName"));
                    fText1.SetValue(Label.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
                    fText1.SetValue(Label.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
                    fTemplate.AppendChild(fText1);

                    dFamiliesTemplate.VisualTree = fTemplate;
                    targetCombo.ItemTemplate = dFamiliesTemplate;
                    if (targetCombo.HasItems)
                    {
                        if (selectLast)
                            targetCombo.SelectedIndex = targetCombo.Items.Count - 1;
                        else
                            targetCombo.SelectedIndex = 0;
                    }
                        
                }

            }
        }

        private void comboLowerLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboLowerLevel.HasItems)
            {
                selectedLowerLevel = (comboLowerLevel.Items.GetItemAt(comboLowerLevel.SelectedIndex) as LevelInfo).levelId;

                IList<LevelInfo> currentLevelsAboveSelectedLowerLevel = LevelInfoList.ToList();
                if (comboLowerLevel.SelectedIndex != 0)
                {
                    for (int i = comboLowerLevel.SelectedIndex -1; i >= 0; i--)
                    {
                        currentLevelsAboveSelectedLowerLevel.RemoveAt(i);
                    }

                    PopulateComboLevel(comboUpperLevel, currentLevelsAboveSelectedLowerLevel, true); 
                }else
                {
                    PopulateComboLevel(comboUpperLevel, LevelInfoList, true);
                }

            }
        }

        private void comboUpperLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboUpperLevel.HasItems)
                selectedUpperLevel = (comboUpperLevel.Items.GetItemAt(comboUpperLevel.SelectedIndex) as LevelInfo).levelId;
        }

        private void btnUN_Click(object sender, RoutedEventArgs e)
        {
            join = false;
            this.DialogResult = true;
        }
    }
}
