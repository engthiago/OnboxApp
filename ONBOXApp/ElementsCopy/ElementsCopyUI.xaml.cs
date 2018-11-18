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
    /// Interaction logic for ElementsCopyUI.xaml
    /// </summary>
    public partial class ElementsCopyUI : Window
    {
        IList<LevelInfo> currentDocLevels = new List<LevelInfo>();

        internal ElementsCopyUI(IList<LevelInfo> docLevels)
        {
            InitializeComponent();
            currentDocLevels = docLevels;
        }

        private void CopyBeamsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            listLevels.ItemsSource = currentDocLevels;

            DataTemplate listLevelTemplate = new DataTemplate();
            listLevelTemplate.DataType = typeof(LevelInfo);

            FrameworkElementFactory fContainer = new FrameworkElementFactory(typeof(StackPanel));

            FrameworkElementFactory fName = new FrameworkElementFactory(typeof(ListViewItem));
            fName.SetBinding(ListViewItem.ContentProperty, new Binding("levelName"));
            fContainer.AppendChild(fName);

            listLevelTemplate.VisualTree = fContainer;
            listLevels.ItemTemplate = listLevelTemplate;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
