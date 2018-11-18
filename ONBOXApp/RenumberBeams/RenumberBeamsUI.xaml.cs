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
    /// Interaction logic for RenumberBeamsUI.xaml
    /// </summary>
    public partial class RenumberBeamsUI : Window
    {
        internal IList<LevelInfo> lvlInfo = new List<LevelInfo>();
        internal IList<BeamTypesInfo> beamTypesInfo = new List<BeamTypesInfo>();

        public RenumberBeamsUI()
        {
            InitializeComponent();
        }

        private void renumberBeamsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            comboDecimalPlaces.SelectedIndex = ONBOXApplication.BeamsDecimalPlaces - 1;
            if (ONBOXApplication.storedBeamRenumOrder == BeamRenumberOrder.Horizontal)
            {
                comboBeamOrder.SelectedIndex = 0;
            }
            else if (ONBOXApplication.storedBeamRenumOrder == BeamRenumberOrder.Vertical)
            {
                comboBeamOrder.SelectedIndex = 1;
            }
            else
            {
                comboBeamOrder.SelectedIndex = 2;
            }

            lvlInfo = RenumberBeams.GetAllLevelInfo();
            beamTypesInfo = RenumberBeams.GetBeamTypesInfo();

            if (ONBOXApplication.storedBeamLevelInfo.Count == 0)
            {
                int counter = 1;
                foreach (LevelInfo currentLvlInfo in lvlInfo)
                {
                    currentLvlInfo.levelPrefix = "V" + (counter++).ToString();
                }
                ONBOXApplication.storedBeamLevelInfo = lvlInfo.ToList();
            }
            if (ONBOXApplication.storedBeamTypesInfo.Count == 0)
            {
                ONBOXApplication.storedBeamTypesInfo = beamTypesInfo.ToList();
            }
        }

        private void btnLevels_Click(object sender, RoutedEventArgs e)
        {
            RenumberBeamLevelUI renumBeamLevelWindow = new RenumberBeamLevelUI(this);
            renumBeamLevelWindow.ShowDialog();
        }

        private void btnTypes_Click(object sender, RoutedEventArgs e)
        {
            RenumberBeamTypesUI renumBeamWindow = new RenumberBeamTypesUI(this);
            renumBeamWindow.ShowDialog();
        }

        private void btnRenumber_Click(object sender, RoutedEventArgs e)
        {
            if (comboBeamOrder.SelectedIndex == 0)
            {
                ONBOXApplication.storedBeamRenumOrder = BeamRenumberOrder.Horizontal;
            }
            else if (comboBeamOrder.SelectedIndex == 1)
            {
                ONBOXApplication.storedBeamRenumOrder = BeamRenumberOrder.Vertical;
            }
            else
            {
                ONBOXApplication.storedBeamRenumOrder = BeamRenumberOrder.None;
            }

            ONBOXApplication.BeamsDecimalPlaces = comboDecimalPlaces.SelectedIndex + 1;
            this.DialogResult = true;
        }



    }
}
