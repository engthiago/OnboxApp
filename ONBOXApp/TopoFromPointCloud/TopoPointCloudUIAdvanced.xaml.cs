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
    /// Interaction logic for TopoPointCloudUIAdvanced.xaml
    /// </summary>
    public partial class TopoPointCloudUIAdvanced : Window
    {
        internal int pointMaxQuantity;
        internal double xDirDistance;
        internal double xIncrAmount;
        internal int xNumberOfDivisions;
        internal double yDirDistance;
        internal double yIncrAmount;
        internal int yNumberOfDivisions;

        public TopoPointCloudUIAdvanced(double xDirDistance, double yDirDistance, int xNumberOfDivisions, int yNumberOfDivisions, double xIncrAmount, double yIncrAmount, int pointMaxQuantity)
        {
            InitializeComponent();

            this.xDirDistance = xDirDistance;
            this.yDirDistance = yDirDistance;
            this.xNumberOfDivisions = xNumberOfDivisions;
            this.yNumberOfDivisions = yNumberOfDivisions;
            this.xIncrAmount = xIncrAmount;
            this.yIncrAmount = yIncrAmount;
            this.pointMaxQuantity = pointMaxQuantity;
        }

        private void TopoFromPointsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateFields();
        }

        private void UpdateFields()
        {
            lblXTotalDist.Content = xDirDistance.ToString() + "m";
            txtXdiv.Text = xNumberOfDivisions.ToString();
            lblXIncrDist.Content = xIncrAmount.ToString() + "m";


            lblYTotalDist.Content = yDirDistance.ToString() + "m";
            txtYdiv.Text = yNumberOfDivisions.ToString();
            lblYIncrDist.Content = yIncrAmount.ToString() + "m";

            txtMaxPoints.Text = pointMaxQuantity.ToString();
            lblTotalPoints.Content = (xNumberOfDivisions * yNumberOfDivisions * pointMaxQuantity).ToString();
        }

        private void CalculateFields()
        {
            int prevXNumberOfDivisions = xNumberOfDivisions;
            int prevYNumberOfDivisions = yNumberOfDivisions;

            int.TryParse(txtXdiv.Text, out xNumberOfDivisions);
            int.TryParse(txtYdiv.Text, out yNumberOfDivisions);

            xNumberOfDivisions = xNumberOfDivisions < 1 ? prevXNumberOfDivisions : xNumberOfDivisions;
            yNumberOfDivisions = yNumberOfDivisions < 1 ? prevYNumberOfDivisions : yNumberOfDivisions;

            xIncrAmount = Math.Round(EstabilishIteractionPoints(xDirDistance, xNumberOfDivisions), 2);
            yIncrAmount = Math.Round(EstabilishIteractionPoints(yDirDistance, yNumberOfDivisions), 2);

            int prevPointMaxQuantity = pointMaxQuantity;
            int.TryParse(txtMaxPoints.Text, out pointMaxQuantity);
            txtMaxPoints.Text = pointMaxQuantity.ToString();

            lblXIncrDist.Content = xIncrAmount.ToString() + "m";
            lblYIncrDist.Content = yIncrAmount.ToString() + "m";
            txtXdiv.Text = xNumberOfDivisions.ToString();
            txtYdiv.Text = yNumberOfDivisions.ToString();

            int totalMaxPoints = xNumberOfDivisions * yNumberOfDivisions * pointMaxQuantity;
            pointMaxQuantity = pointMaxQuantity < 50 || pointMaxQuantity > 2000 ? prevPointMaxQuantity : pointMaxQuantity;
            lblTotalPoints.Content = totalMaxPoints.ToString();

        }

        private bool isMoreThanMaxNumberOfPointsAllowed()
        {
            int currentTotalPoints = 0;
            if (int.TryParse(lblTotalPoints.Content.ToString(), out currentTotalPoints))
            {
                return currentTotalPoints > 100000 ? true : false;
            }
            return true;
        }

        private bool isLessThanMaxNumberOfPointsAllowed()
        {
            int currentTotalPoints = 0;
            if (int.TryParse(lblTotalPoints.Content.ToString(), out currentTotalPoints))
            {
                return currentTotalPoints < 100 ? true : false;
            }
            return true;
        }

        private double EstabilishIteractionPoints(double xDirDistance, int xNumberOfDivisions)
        {
            return xDirDistance / xNumberOfDivisions;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (isMoreThanMaxNumberOfPointsAllowed())
                MessageBox.Show(Properties.Messages.TopoFromPointCloud_MaxPointsReached, Properties.Messages.Common_Error);
            else if (isLessThanMaxNumberOfPointsAllowed())
                MessageBox.Show(Properties.Messages.TopoFromPointCloud_MinPointsReached, Properties.Messages.Common_Error);
            else
                this.DialogResult = true;
        }

        private void txtXdiv_LostFocus(object sender, RoutedEventArgs e)
        {
            CalculateFields();
        }

        private void txtYdiv_LostFocus(object sender, RoutedEventArgs e)
        {
            CalculateFields();
        }

        private void txtMaxPoints_LostFocus(object sender, RoutedEventArgs e)
        {
            CalculateFields();
        }

        private void txtXdiv_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                CalculateFields();
            }
        }

        private void txtYdiv_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                CalculateFields();
            }
        }

        private void txtMaxPoints_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                CalculateFields();
            }
        }
    }
}
