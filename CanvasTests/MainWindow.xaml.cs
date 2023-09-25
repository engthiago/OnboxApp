using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace CanvasTests
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool pressed = false;
        private Point currentPoint = new Point();
        private Point prevPoint = new Point();
        private Shape shape;
        private bool isPanning;
        private Point prevPanning = new Point();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Polygon_MouseEnter(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource is Shape shape)
            {
                shape.Stroke = new SolidColorBrush(Colors.Black);
            }
        }

        private void Polygon_MouseLeave(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource is Shape shape)
            {
                shape.Stroke = new SolidColorBrush(Colors.Transparent);
            }
        }

        private void Ellipse_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.pressed = true;
                Debug.WriteLine($"{this.currentPoint.X}, {this.currentPoint.Y}");
                if (e.OriginalSource is Ellipse ellipse)
                {
                    this.shape = ellipse;
                    this.prevPoint = e.GetPosition(this);
                }
            }
        }

        private void Polygon_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.isPanning = false;
            this.Cursor = Cursors.Arrow;
            Debug.WriteLine("Mouse up");
            this.shape = null;
            this.pressed = false;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            this.currentPoint = e.GetPosition(this);
            if (this.pressed && this.shape != null)
            {
                this.shape.SetValue(Canvas.LeftProperty, this.currentPoint.X);
                this.shape.SetValue(Canvas.TopProperty, this.currentPoint.Y);

                for (var i = 0; i < this.Poly.Points.Count; i++)
                {
                    var point = this.Poly.Points[i];
                    var isClose = this.IsCloseTo(point, this.prevPoint);
                    if (isClose)
                    {
                        this.Poly.Points[i] = this.currentPoint;
                        this.prevPoint = this.currentPoint;
                        Debug.WriteLine("Point found");
                    }
                }
            }
            if (this.isPanning)
            {
                var diffX = this.currentPoint.X - this.prevPanning.X;
                var diffY = this.currentPoint.Y - this.prevPanning.Y;
                for (var i = 0; i < this.Poly.Points.Count; i++)
                {
                    var point = this.Poly.Points[i];
                    this.Poly.Points[i] = new Point(point.X + diffX, point.Y + diffY);
                }
                this.prevPanning = this.currentPoint;
                Debug.WriteLine("New panning point");
            }
        }

        private bool IsCloseTo(Point p0, Point p1, double eps = 10)
        {
            if (Math.Abs(p0.X - p1.X) > eps) return false;
            if (Math.Abs(p0.Y - p1.Y) > eps) return false;

            return true;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            this.shape = null;
            this.pressed = false;
            this.isPanning = false;
            this.Cursor = Cursors.Arrow;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                this.isPanning = true;
                this.prevPanning = e.GetPosition(this);
                this.Cursor = Cursors.SizeAll;
            }
        }
    }
}
