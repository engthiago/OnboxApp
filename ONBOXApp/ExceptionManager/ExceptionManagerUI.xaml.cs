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
    /// Interaction logic for ExceptionManager.xaml
    /// </summary>
    public partial class ExceptionManagerUI : Window
    {
        Exception currentException = null;
        string currentCustomInfo = null;
        bool currentIsJustWarning = false;

        public ExceptionManagerUI(Exception targetException)
        {
            InitializeComponent();
            currentException = targetException;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            string errorInfo = "";
            errorInfo += Properties.Messages.Exceptions_Message + ": " + currentException.Message;
            errorInfo += "\n" +  Properties.Messages.Exceptions_Source + ": " + currentException.Source;
            errorInfo += "\n" + Properties.Messages.Exceptions_Location + ": " + currentException.TargetSite;
            errorInfo += "\n" + Properties.Messages.Exceptions_Detail + ": " + currentException.StackTrace;
            errorInfo += "\n" + Properties.Messages.Exceptions_Data + ": " + currentException.Data;
            errorInfo += "\n" + Properties.Messages.Exceptions_Help + ": " + currentException.HelpLink;
            textError.Text = errorInfo;

            if (currentCustomInfo != null)
            {
                txtInformation.Text = currentCustomInfo;
            }

            if (currentIsJustWarning)
            {
                stackReport.Visibility = Visibility.Collapsed;
                imgWarningType.Source = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/Warning.png", UriKind.Absolute));
            }
        }

        public ExceptionManagerUI(Exception targetException, string customInfo, bool isJustWarning)
        {
            InitializeComponent();
            currentException = targetException;
            currentCustomInfo = customInfo;
            currentIsJustWarning = isJustWarning;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
