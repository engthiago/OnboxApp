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

namespace ONBOXAppl.TopoEdit
{
    /// <summary>
    /// Interaction logic for Canvas.xaml
    /// </summary>
    public partial class Canvas : Window
    {
        public Canvas()
        {
            InitializeComponent();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = e.OriginalSource is Rectangle ? Cursors.SizeAll : Cursors.Arrow;
        }

    }
}
