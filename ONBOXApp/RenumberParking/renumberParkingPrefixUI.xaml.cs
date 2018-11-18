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
    /// Interaction logic for renumberParkingPrefixUI.xaml
    /// </summary>
    public partial class renumberParkingPrefixUI : Window
    {
        IList<LevelInfo> lvlInfo = new List<LevelInfo>();

        public renumberParkingPrefixUI()
        {
            InitializeComponent();
        }

        private void renumberParkingPrefixWindow_Loaded(object sender, RoutedEventArgs e)
        {
            lvlInfo = RenumberParking.getAllLevels();
            IList<LevelInfo> prevLvlInfo = ONBOXApplication.storedParkingLevelInfo.ToList();

            if (ONBOXApplication.isNumIndenLevel == true)
            {
                checkIsNumIndenLevel.IsChecked = true;
            }
            else
            {
                checkIsNumIndenLevel.IsChecked = false;
            }

            foreach (LevelInfo currentLvlInfo in lvlInfo)
            {
                foreach (LevelInfo currentPrevLvlInfo in prevLvlInfo)
                {
                    if (currentLvlInfo.levelId == currentPrevLvlInfo.levelId)
                    {
                        currentLvlInfo.levelPrefix = currentPrevLvlInfo.levelPrefix;
                        currentLvlInfo.willBeNumbered = currentPrevLvlInfo.willBeNumbered;
                    }
                }
            }

            gridLevel.AutoGenerateColumns = false;
            gridLevel.CanUserAddRows = false;
            gridLevel.CanUserDeleteRows = false;
            gridLevel.CanUserResizeRows = false;
            gridLevel.CanUserReorderColumns = false;
            gridLevel.ItemsSource = lvlInfo;

            DataGridCheckBoxColumn dt0 = new DataGridCheckBoxColumn();
            dt0.Header = Properties.WindowLanguage.RenumberParkLevelOptions_Use;
            dt0.Binding = new Binding("willBeNumbered");
            dt0.CanUserSort = false;
            dt0.Width = 50;

            DataGridTextColumn dt1 = new DataGridTextColumn();
            dt1.Header = Properties.WindowLanguage.RenumberParkLevelOptions_Name;
            dt1.Binding = new Binding("levelName");
            dt1.CanUserSort = false;
            dt1.IsReadOnly = true;
            dt1.Width = 150;

            DataGridTextColumn dt2 = new DataGridTextColumn();
            dt2.Header = Properties.WindowLanguage.RenumberParkLevelOptions_Prefix;
            dt2.Binding = new Binding("levelPrefix");
            dt2.CanUserSort = false;
            dt2.Width = 60;

            gridLevel.Columns.Add(dt1);
            gridLevel.Columns.Add(dt0);
            gridLevel.Columns.Add(dt2);
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (checkIsNumIndenLevel.IsChecked == true)
            {
                ONBOXApplication.isNumIndenLevel = true;
            }
            else
            {
                ONBOXApplication.isNumIndenLevel = false;
            }

            for (int i = 0; i < lvlInfo.Count; i++)
            {
                object cellInfo = gridLevel.Items.GetItemAt(i);
                lvlInfo.ElementAt(i).levelPrefix = (cellInfo as LevelInfo).levelPrefix;
                lvlInfo.ElementAt(i).willBeNumbered = (cellInfo as LevelInfo).willBeNumbered;
            }

            ONBOXApplication.storedParkingLevelInfo = lvlInfo.ToList();
            this.Close();
        }

        private void checkIsNumIndenLevel_Click(object sender, RoutedEventArgs e)
        {
        }


    }
}
