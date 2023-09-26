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
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CanvasTests
{
    //Points="-5,350 100,320 120,310 160,330 180,320 650,320 650,500 -5,500"

    public class TopoLineProfile
    {
        public List<Point> Points { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool pressed = false;

        private Point cursorPos = new Point();

        private Point prevDragPoint = new Point();
        private Shape selectedShape;

        private bool isPanning;
        private Point prevPanning = new Point();
        private Point panningOffset = new Point();

        private float zoom = 1;

        private TopoLineProfile topography = new TopoLineProfile();

        public MainWindow()
        {
            InitializeComponent();
            this.Update();
        }

        private bool IsCloseTo(Point p0, Point p1, double eps = 10)
        {
            if (Math.Abs(p0.X - p1.X) > eps) return false;
            if (Math.Abs(p0.Y - p1.Y) > eps) return false;

            return true;
        }

        private double ScreenToWorld(double n, double panningDiff, float zoom)
        {
            return (n + panningDiff) * zoom;
        }

        private double WorldToScreen(double n, double panningDiff, float zoom)
        {
            return (n / zoom) + panningDiff;
        }

        private Point ScreenToWorld(Point p, Point panningDiff, float zoom)
        {
            var x = ScreenToWorld(p.X, panningDiff.X, zoom);
            var y = ScreenToWorld(p.Y, panningDiff.Y, zoom);
            return new Point(x, y);
        }

        private Point WorldToScreen(Point p, Point panningDiff, float zoom)
        {
            var x = WorldToScreen(p.X, panningDiff.X, zoom);
            var y = WorldToScreen(p.Y, panningDiff.Y, zoom);
            return new Point(x, y);
        }

        private void Update()
        {
            topography.Points = new List<Point>
            {
                new Point(-5, 350),
                new Point(100, 320),
                new Point(120, 310),
                new Point(160, 330),
                new Point(180, 320),
                new Point(650, 320),
                new Point(650, 500),
                new Point(-5, 500),
            };

            //foreach (Point point in topography.Points)
            //{
            //    var ellipse = new Ellipse();

            //    ellipse.StrokeThickness = 4;
            //    ellipse.Stroke = new SolidColorBrush(Colors.Transparent);
            //    ellipse.Fill = new SolidColorBrush(Colors.Black);
            //    ellipse.Width = pointSize;
            //    ellipse.Height = pointSize;
            //    ellipse.SetValue(Canvas.LeftProperty, point.X);
            //    ellipse.SetValue(Canvas.TopProperty, point.Y);
            //    ellipse.Margin = new Thickness(-16 / 2);

            //    ellipse.ToolTip = $"Position: {point.X} {point.Y}";
            //    ToolTipService.SetBetweenShowDelay(ellipse, 0);
            //    ToolTipService.SetInitialShowDelay(ellipse, 0);
            //    ToolTipService.SetShowDuration(ellipse, 999999);

            //    ellipse.MouseDown += Ellipse_MouseDown;
            //    ellipse.MouseEnter += Polygon_MouseEnter;
            //    ellipse.MouseLeave += Polygon_MouseLeave;

            //    this.Canvas.Children.Add(ellipse);

            //}

            var transformedPoints = new List<Point>();
            foreach (var point in topography.Points)
            {
                transformedPoints.Add(
                    this.WorldToScreen(point, this.panningOffset, this.zoom)
                    );
            }

            this.Terrain.Points = new PointCollection(transformedPoints);
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
                Debug.WriteLine($"{this.cursorPos.X}, {this.cursorPos.Y}");
                if (e.OriginalSource is Ellipse ellipse)
                {
                    this.selectedShape = ellipse;
                    this.prevDragPoint = e.GetPosition(this);
                }
            }
        }

        private void Polygon_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.isPanning = false;
            this.Cursor = Cursors.Arrow;
            Debug.WriteLine("Mouse up");
            this.selectedShape = null;
            this.pressed = false;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            this.cursorPos = e.GetPosition(this);

            if (this.pressed && this.selectedShape != null)
            {
                this.selectedShape.SetValue(Canvas.LeftProperty, this.cursorPos.X);
                this.selectedShape.SetValue(Canvas.TopProperty, this.cursorPos.Y);

                for (var i = 0; i < this.Terrain.Points.Count; i++)
                {
                    var point = this.Terrain.Points[i];
                    var isClose = this.IsCloseTo(point, this.prevDragPoint);
                    if (isClose)
                    {
                        this.Terrain.Points[i] = this.cursorPos;
                        this.prevDragPoint = this.cursorPos;
                        Debug.WriteLine("Point found");
                    }
                }
            }
            if (this.isPanning)
            {
                //var diffX = this.currentPoint.X - this.prevPanning.X;
                //var diffY = this.currentPoint.Y - this.prevPanning.Y;
                //for (var i = 0; i < this.Terrain.Points.Count; i++)
                //{
                //    var point = this.Terrain.Points[i];
                //    this.Terrain.Points[i] = new Point(point.X + diffX, point.Y + diffY);
                //}
                this.Update();
                this.panningOffset.X += this.cursorPos.X - this.prevPanning.X;
                this.panningOffset.Y += this.cursorPos.Y - this.prevPanning.Y;
                this.prevPanning = this.cursorPos;
                Debug.WriteLine("New panning point");
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            this.selectedShape = null;
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

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            //var beforeZoomWorld = this.ScreenToWorld(this.cursorPos, panningOffset, zoom);
            //var beforePanning = new Point(this.panningOffset.X, this.panningOffset.Y);

            if (e.Delta > 0) 
            {
                if (zoom < 0.01) return;
                zoom *= 0.95f;
            }
            else
            {
                zoom *= 1.05f;
            }

            this.Update();

            //var afterZoomWorld = this.ScreenToWorld(this.cursorPos, panningOffset, zoom);

            //var beforeZoomPoint = this.WorldToScreen(beforeZoomWorld, beforePanning, zoom);
            //var afterZoomPoint = this.WorldToScreen(afterZoomWorld, this.panningOffset, zoom);

            //this.panningOffset.X += afterZoomPoint.X - beforeZoomPoint.X;
            //this.panningOffset.Y += afterZoomPoint.Y - beforeZoomPoint.Y;

            //this.Update();

            //Debug.WriteLine($"Before {beforeZoom}");
            //Debug.WriteLine($"After {afterPoint}");
        }
    }
}
