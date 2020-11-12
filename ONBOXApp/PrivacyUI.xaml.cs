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
    /// Interaction logic for PrivacyUI.xaml
    /// </summary>
    public partial class PrivacyUI : Window
    {
        public PrivacyUI()
        {
            InitializeComponent();
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://app.onboxdesign.com.br/accounts/request");
        }
    }
}
