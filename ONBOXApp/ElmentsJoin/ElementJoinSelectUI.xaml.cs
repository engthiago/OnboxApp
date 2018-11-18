using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
    /// Interaction logic for ElementJoinSelectUI.xaml
    /// </summary>
    public partial class ElementJoinSelectUI : Window
    {
        internal bool isShowned = false;
        ExternalEvent currentExternalEvent = null;
        RequestElementsSelectHandler currentElemenSelectHandler = null;
        internal SelectElementsToJoin selectElementsSelectOperation = SelectElementsToJoin.undefined;

        internal IList<Element> currentFirstElementList = new List<Element>();
        internal IList<Element> currentSecondElementList = new List<Element>();

        public ElementJoinSelectUI(ExternalEvent targetEvent, RequestElementsSelectHandler targetHandler)
        {
            InitializeComponent();

            currentExternalEvent = targetEvent;
            currentElemenSelectHandler = targetHandler;
        }

        private void JoinElementsWindow_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void JoinElementsWindow_Closed(object sender, EventArgs e)
        {
            isShowned = false;
            selectElementsSelectOperation = SelectElementsToJoin.unsubscribe;
            currentExternalEvent.Raise();
            base.Close();
        }

        private void btnSelectSecondElementsJoin_Click(object sender, RoutedEventArgs e)
        {
            FreezeUI();
            selectElementsSelectOperation = SelectElementsToJoin.selectSecondElements;
            currentExternalEvent.Raise();
        }

        private void btnSelectFirstElementsJoin_Click(object sender, RoutedEventArgs e)
        {
            FreezeUI();
            selectElementsSelectOperation = SelectElementsToJoin.selectFirstElements;
            currentExternalEvent.Raise();
        }

        private void btnUnjoin_Click(object sender, RoutedEventArgs e)
        {
            FreezeUI();
            selectElementsSelectOperation = SelectElementsToJoin.unjoin;
            currentExternalEvent.Raise();
        }

        private void btnJoin_Click(object sender, RoutedEventArgs e)
        {
            FreezeUI();
            selectElementsSelectOperation = SelectElementsToJoin.join;
            currentExternalEvent.Raise();
        }

        internal void ChangeFirstElementsNumber(int targetNumber)
        {
            FreezeUI();
            string sufix = targetNumber > 1 || targetNumber == 0 ? " " + Properties.WindowLanguage.JoinUnjoinSelected_MarkedElements : " " + Properties.WindowLanguage.JoinUnjoinSelected_MarkedElement;

            lblFirstElementsSelected.Content = targetNumber.ToString() + sufix;
        }

        internal void ChangeSecondElementsNumber(int targetNumber)
        {
            FreezeUI();
            string sufix = targetNumber > 1 || targetNumber == 0 ? " " + Properties.WindowLanguage.JoinUnjoinSelected_MarkedElements : " " + Properties.WindowLanguage.JoinUnjoinSelected_MarkedElement;

            lblSecondElementsSelected.Content = targetNumber.ToString() + sufix;
        }

        private void btnShowFirstElements_Click(object sender, RoutedEventArgs e)
        {
            FreezeUI();
            selectElementsSelectOperation = SelectElementsToJoin.showFirstSelectedElements;
            currentExternalEvent.Raise();
        }

        private void btnShowSecondElements_Click(object sender, RoutedEventArgs e)
        {
            FreezeUI();
            selectElementsSelectOperation = SelectElementsToJoin.showSecondSelectedElements;
            currentExternalEvent.Raise();
        }

        private void btnDeSelectSecondElements_Click(object sender, RoutedEventArgs e)
        {
            FreezeUI();
            selectElementsSelectOperation = SelectElementsToJoin.deselectSecond;
            currentExternalEvent.Raise();
        }

        private void btnDeSelectFirstElements_Click(object sender, RoutedEventArgs e)
        {
            FreezeUI();
            selectElementsSelectOperation = SelectElementsToJoin.deselectFirst;
            currentExternalEvent.Raise();
        }

        internal void FreezeUI()
        {
            this.IsEnabled = false;
        }

        internal void UnFreezeUI()
        {
            this.IsEnabled = true;
        }
    }
}
