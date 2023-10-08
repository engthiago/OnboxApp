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
    /// Interaction logic for TopoSlopesUI.xaml
    /// </summary>
    public partial class TopoSlopesUI : Window
    {

        #region Const

        double minMaxDist = 0.1;
        double maxMaxDist = 5;
        double defaultMaxDist = 1;

        double minAngle = 5;
        double maxAngle = 85;
        double defaultAngle = 45;

        #endregion

        internal double MaxDist
        {
            get
            {
                double tempNumber = defaultMaxDist;
                if (double.TryParse(textMaxDist.Text, out tempNumber))
                {
                    if (tempNumber < minMaxDist || tempNumber > maxMaxDist)
                        return defaultMaxDist;
                }
                else
                {
                    return defaultMaxDist;
                }

                return tempNumber;

            }
            set { return; }
        }
        internal double Angle
        {
            get
            {
                double tempNumber = defaultAngle;
                if (double.TryParse(textAngle.Text, out tempNumber))
                {
                    if (tempNumber < minAngle || tempNumber > maxAngle)
                        return defaultAngle;
                }
                else
                {
                    return defaultAngle;
                }

                return tempNumber;
            }
            set { return; }
        }
        internal bool IsContinuous { get { return (bool)checkContinuous.IsChecked; } set { return; } }

        public TopoSlopesUI()
        {
            InitializeComponent();
#if R2024
            this.Title = Properties.WindowLanguage.ToposolidSolidGrading_Title;
#endif
        }

        private void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
