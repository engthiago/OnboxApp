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
using Autodesk.Revit.UI;

namespace ONBOXAppl
{
    /// <summary>
    /// Interaction logic for BeamsFromWallsUI.xaml
    /// </summary>
    public partial class BeamsUpdateUI : Window
    {
        internal bool isShowned = false;
        ExternalEvent localExternalEvent = null;
        RequestBeamsUpdateHandler localExternalEventHandler = null;
        internal ExternalOperation beamFromWallsCurrentOperation = ExternalOperation.Reload;

        #region Const

        //BeamHeight
        const int BeamMinHeight = 20;
        const int BeamMaxHeight = 150;
        const int BeamDefaultHeight = 60;
        const int BeamMinHeightPerc = 5;
        const int BeamMaxHeightPerc = 20;
        const int BeamDefaultHeightPerc = 10;

        //BeamWidth
        const int BeamMinWidth = 14;
        const int BeamMaxWidth = 40;
        const int BeamDefaultWidth = 14;
        const int BeamMinDiffWidth = -5;
        const int BeamMaxDiffWidth = 5;
        const int BeamDefaultDiffWidth = 0;

        #endregion

        #region Beam Properties

        //BeamFamily
        internal int SelectedBeamFamilyID { get { return (comboFamily.SelectedItem as FamilyWithImage).FamilyID; } }

        //Dimentions
        internal BeamFromWallHeightMode BeamHeightMode { get { return (BeamFromWallHeightMode)comboBeamHeight.SelectedIndex; } }
        internal BeamFromWallWidthMode BeamWidthMode { get { return (BeamFromWallWidthMode)comboBeamWidth.SelectedIndex; } }
        internal int BeamHeightInfo
        {
            get
            {
                int numberToReturn = 0;
                if (int.TryParse(textHeight.Text, out numberToReturn))
                {
                    switch (comboBeamHeight.SelectedIndex)
                    {
                        case 0:
                            numberToReturn = MinMaxTextBoxValues(numberToReturn, BeamMinHeight, BeamMaxHeight);
                            break;
                        case 1:
                            numberToReturn = MinMaxTextBoxValues(numberToReturn, BeamMinHeightPerc, BeamMaxHeightPerc);
                            break;
                        case 2:
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    switch (comboBeamHeight.SelectedIndex)
                    {
                        case 0:
                            numberToReturn = BeamDefaultHeight;
                            break;
                        case 1:
                            numberToReturn = BeamDefaultHeightPerc;
                            break;
                        case 2:
                            break;
                        default:
                            break;
                    }
                }

                if (textHeight.IsEnabled)
                    textHeight.Text = numberToReturn.ToString();
                return numberToReturn;
            }
        }
        internal int BeamWidthInfo
        {
            get
            {
                int numberToReturn = 0;
                if (int.TryParse(textWidth.Text, out numberToReturn))
                {
                    switch (comboBeamWidth.SelectedIndex)
                    {
                        case 0:
                            numberToReturn = MinMaxTextBoxValues(numberToReturn, BeamMinDiffWidth, BeamMaxDiffWidth);
                            break;
                        case 1:
                            numberToReturn = MinMaxTextBoxValues(numberToReturn, BeamMinWidth, BeamMaxWidth);
                            break;
                        default:
                            break;
                    }

                }
                else
                {
                    switch (comboBeamWidth.SelectedIndex)
                    {
                        case 0:
                            numberToReturn = BeamDefaultDiffWidth;
                            break;
                        case 1:
                            numberToReturn = BeamDefaultWidth;
                            break;
                        default:
                            break;
                    }
                }

                if (textWidth.IsEnabled)
                    textWidth.Text = numberToReturn.ToString();
                return numberToReturn;
            }
        }

        #endregion

        internal BeamsUpdateUI(ExternalEvent targetEvent, RequestBeamsUpdateHandler targetHandler)
        {
            InitializeComponent();
            localExternalEvent = targetEvent;
            localExternalEventHandler = targetHandler;
        }

        private void beamsFromWallsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            isShowned = true;
            ReloadBeamFamilies(ONBOXApplication.onboxApp.uiApp);
        }

        private void beamsFromWallsWindow_Closed(object sender, EventArgs e)
        {
            isShowned = false;
            beamFromWallsCurrentOperation = ExternalOperation.Unsubscribe;
            localExternalEvent.Raise();

            base.Close();
        }

        internal void ReloadBeamFamilies(UIApplication targetApp)
        {
            if (targetApp.ActiveUIDocument != null)
            {
                if (targetApp.ActiveUIDocument.Document.IsFamilyDocument)
                {
                    this.IsEnabled = false;
                }
                else
                {
                    if (localExternalEvent.IsPending == false)
                    {
                        this.IsEnabled = true;
                        beamFromWallsCurrentOperation = ExternalOperation.Reload;
                        localExternalEvent.Raise();
                    }
                }
            }
            else
            {
                this.IsEnabled = false;
            }
        }

        private void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (localExternalEvent.IsPending == false)
            {
                this.IsEnabled = true;
                FreezeCreateButton();
                beamFromWallsCurrentOperation = ExternalOperation.Create;
                localExternalEvent.Raise();
            }
        }

        private void FreezeCreateButton()
        {
            btnCreate.IsEnabled = false;
            btnReload.IsEnabled = false;
        }

        internal void UnFreezeCreateButton()
        {
            btnCreate.IsEnabled = true;
            btnReload.IsEnabled = true;
        }

        private int MinMaxTextBoxValues(int targetValue, int minValue, int maxValue)
        {
            if (targetValue < minValue)
                return minValue;
            if (targetValue > maxValue)
                return maxValue;
            return targetValue;
        }

        private void comboBeamHeight_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if the user selects "Heigher Opening" we have to freeze the text
            if (comboBeamHeight != null && textHeight != null)
            {
                if (comboBeamHeight.SelectedIndex == 2)
                {
                    textHeight.IsEnabled = false;
                    textHeight.Text = "";
                }
                else
                {
                    //if the user selects other we have to check for the default value in each case
                    textHeight.IsEnabled = true;
                    if (comboBeamHeight.SelectedIndex == 0)
                    {
                        textHeight.Text = BeamDefaultHeight.ToString();
                    }
                    else if (comboBeamHeight.SelectedIndex == 1)
                    {
                        textHeight.Text = BeamDefaultHeightPerc.ToString();
                    }
                }
            }
        }

        private void comboBeamWidth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //when the user changes the combobox of the width we have to check the appropriate 
            //default value
            if (comboBeamWidth != null && textWidth != null)
            {
                if (comboBeamWidth.SelectedIndex == 0)
                {
                    textWidth.Text = BeamDefaultDiffWidth.ToString();
                }
                else if (comboBeamWidth.SelectedIndex == 1)
                {
                    textWidth.Text = BeamDefaultWidth.ToString();
                }
            }
        }

        internal void PopulateComboFamily(IList<FamilyWithImage> targetListOfFamilies)
        {
            if (targetListOfFamilies != null)
            {
                if (targetListOfFamilies.Count > 0)
                {
                    btnCreate.IsEnabled = true;
                    comboFamily.IsEnabled = true;

                    comboFamily.ItemsSource = targetListOfFamilies;

                    DataTemplate dataBeamTemplate = new DataTemplate();
                    dataBeamTemplate.DataType = typeof(FamilyWithImage);

                    FrameworkElementFactory mainStackPanel = new FrameworkElementFactory(typeof(StackPanel));
                    mainStackPanel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

                    FrameworkElementFactory familyImage = new FrameworkElementFactory(typeof(Image));
                    familyImage.SetBinding(Image.SourceProperty, new Binding("Image"));
                    mainStackPanel.AppendChild(familyImage);

                    FrameworkElementFactory familyName = new FrameworkElementFactory(typeof(Label));
                    familyName.SetBinding(Label.ContentProperty, new Binding("FamilyName"));
                    mainStackPanel.AppendChild(familyName);

                    dataBeamTemplate.VisualTree = mainStackPanel;
                    comboFamily.ItemTemplate = dataBeamTemplate;

                    if (comboFamily.HasItems)
                        comboFamily.SelectedIndex = 0;

                }
                else
                {
                    btnCreate.IsEnabled = false;
                    comboFamily.IsEnabled = false;
                }
            }
            else
            {
                btnCreate.IsEnabled = false;
                comboFamily.IsEnabled = false;
            }

        }

        private void btnReload_Click(object sender, RoutedEventArgs e)
        {
            ReloadBeamFamilies(ONBOXApplication.onboxApp.uiApp);
        }

    }
}
