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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ONBOXAppl
{
    /// <summary>
    /// Interaction logic for BeamsFromColumnsUI
    /// </summary>
    public partial class BeamsFromColumnsUI : Window
    {
        ExternalEvent localExternalEvent = null;
        RequestBeamsFromColumnsHandler localExternalEventHandler = null;
        internal bool isShowned = false;
        internal ExternalOperation beamFromColumnsCurrentOperation = ExternalOperation.Reload;

        #region Constants
        private const int minBeamWidthValue = 10;
        private const int maxBeamWidthValue = 50;
        private const int defaultBeamWidthValue = 14;

        private const int minBeamHeightValue = 20;
        private const int maxBeamHeightValue = 150;
        private const int defaultBeamHeightValue = 60;

        private const int minBeamHeightPerc = 5;
        private const int maxBeamHeightPerc = 15;
        private const int defaultBeamHeightPerc = 10;
        #endregion

        internal int selectedBeamFamilyID = 0;

        public BeamsFromColumnsUI(ExternalEvent targetExternalEvent, RequestBeamsFromColumnsHandler targetExternalEventHandler)
        {
            localExternalEvent = targetExternalEvent;
            localExternalEventHandler = targetExternalEventHandler;
            InitializeComponent();
        }

        internal void FreezeCreateButton()
        {
            btnCreate.IsEnabled = false;
            btnReload.IsEnabled = false;
        }

        internal void UnFreezeCreateButton()
        {
            btnCreate.IsEnabled = true;
            btnReload.IsEnabled = true;
        }

        private void btnReload_Click(object sender, RoutedEventArgs e)
        {
            ReloadBeamFamilies(ONBOXApplication.onboxApp.uiApp);
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
                        beamFromColumnsCurrentOperation = ExternalOperation.Reload;
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
            UIApplication currentUiApp = ONBOXApplication.onboxApp.uiApp;

            if (currentUiApp != null)
            {
                if (currentUiApp.ActiveUIDocument != null)
                {
                    if (currentUiApp.ActiveUIDocument.Document.IsFamilyDocument)
                    {
                        this.IsEnabled = false;
                    }
                    else
                    {
                        if (localExternalEvent.IsPending == false)
                        {
                            this.IsEnabled = true;
                            FreezeCreateButton();
                            beamFromColumnsCurrentOperation = ExternalOperation.Create;
                            localExternalEvent.Raise();
                        }
                    }
                }else
                {
                    this.IsEnabled = false;
                }
            }
            else
            {
                this.IsEnabled = false;
            }
        }

        private void BeamFromColumnsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ReloadBeamFamilies(ONBOXApplication.onboxApp.uiApp);
        }

        private void BeamFromColumnsWindow_Closed(object sender, EventArgs e)
        {
            isShowned = false;
            beamFromColumnsCurrentOperation = ExternalOperation.Unsubscribe;
            localExternalEvent.Raise();

            base.Close();
        }

        internal void PopulateFamiliesComboBox()
        {

            if (ONBOXApplication.storedBeamFamilesInfo.Count > 0)
            {
                //if we have beams in the project enable the combofamily and the create button
                comboFamily.IsEnabled = true;
                btnCreate.IsEnabled = true;

                comboFamily.ItemsSource = ONBOXApplication.storedBeamFamilesInfo;

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
            else
            {
                //if we dont have beams in the project disable the combofamily and the create button
                comboFamily.IsEnabled = false;
                btnCreate.IsEnabled = false;
            }
        }

        private void comboFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedBeamFamilyID = (comboFamily.Items.GetItemAt(comboFamily.SelectedIndex) as FamilyWithImage).FamilyID;
        }

        private void comboBeamWidth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //avoid try to change when the window is loading
            if (comboBeamWidth != null && txtBeamWidth != null)
            {
                if (comboBeamWidth.SelectedIndex == 0 || comboBeamWidth.SelectedIndex == 1)
                {
                    txtBeamWidth.Text = "";
                    txtBeamWidth.IsEnabled = false;
                }
                else
                {
                    txtBeamWidth.Text = defaultBeamWidthValue.ToString();
                    txtBeamWidth.IsEnabled = true;
                }
            }
        }

        private void comboBeamHeight_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //avoid try to change when the window is loading
            if (comboBeamHeight != null && txtBeamHeight != null)
            {
                if (comboBeamHeight.SelectedIndex == 0)
                {
                    txtBeamHeight.Text = defaultBeamHeightValue.ToString();
                }
                else
                {
                    txtBeamHeight.Text = defaultBeamHeightPerc.ToString();
                }
            }
        }

        private bool IsNumber(string Text)
        {
            int output;
            return int.TryParse(Text, out output);
        }

        private void Validate_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = (sender as System.Windows.Controls.TextBox);
            string currentText = textBox.Text;
            if (IsNumber(currentText) == false)
            {
                textBox.Text = "";
            }
        }

        internal int GetTxtBoxBeamWidthText()
        {
            int output;
            if (int.TryParse(txtBeamWidth.Text, out output))
            {
                if (output < minBeamWidthValue)
                {
                    txtBeamWidth.Text = minBeamWidthValue.ToString();
                    return minBeamWidthValue;
                }
                if (output > maxBeamWidthValue)
                {
                    txtBeamWidth.Text = maxBeamWidthValue.ToString();
                    return maxBeamWidthValue;
                }

                return output;
            }
            txtBeamWidth.Text = minBeamWidthValue.ToString();
            return minBeamWidthValue;
        }

        internal int GetTxtBoxBeamHeightText()
        {
            int output;
            if (int.TryParse(txtBeamHeight.Text, out output))
            {
                if (comboBeamHeight.SelectedIndex == 0)
                {
                    if (output < minBeamHeightValue)
                    {
                        txtBeamHeight.Text = minBeamHeightValue.ToString();
                        return minBeamHeightValue;
                    }
                    if (output > maxBeamHeightValue)
                    {
                        txtBeamHeight.Text = maxBeamHeightValue.ToString();
                        return maxBeamHeightValue;
                    }
                    return output;
                }
                else if (comboBeamHeight.SelectedIndex == 1)
                {
                    if (output < minBeamHeightPerc)
                    {
                        txtBeamHeight.Text = minBeamHeightPerc.ToString();
                        return minBeamHeightPerc;
                    }
                    if (output > maxBeamHeightPerc)
                    {
                        txtBeamHeight.Text = maxBeamHeightPerc.ToString();
                        return maxBeamHeightPerc;
                    }
                    return output;
                }
            }
            else if (comboBeamHeight.SelectedIndex == 0)
            {
                txtBeamHeight.Text = minBeamHeightValue.ToString();
                return minBeamHeightValue;
            }
            txtBeamHeight.Text = minBeamHeightPerc.ToString();
            return minBeamHeightPerc;
        }

        internal int GetBeamWidthMode()
        {
            return comboBeamWidth.SelectedIndex;
        }

        internal int GetBeamHeigthMode()
        {
            return comboBeamHeight.SelectedIndex;
        }

        internal bool IsChain()
        {
            return (bool)checkChain.IsChecked;
        }

        private void btnLoadFamily_Click(object sender, RoutedEventArgs e)
        {
            LoadFamily(ONBOXApplication.onboxApp.uiApp);
        }

        private void LoadFamily(UIApplication targetApp)
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
                        beamFromColumnsCurrentOperation = ExternalOperation.LoadFamily;
                        localExternalEvent.Raise();
                    }
                }
            }
            else
            {
                this.IsEnabled = false;
            }
        }
    }
}

