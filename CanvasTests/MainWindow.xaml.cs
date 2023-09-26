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

        private Shape selectedShape;

        private bool isPanning;
        private Point prevPanning = new Point();
        private Point panningOffset = new Point();

        private float zoom = 1;

        private TopoLineProfile topography = new TopoLineProfile();
        private double pointSize = 16;
        private Point dragCursorWorld = new Point();

        public MainWindow()
        {
            InitializeComponent();

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

            this.Update();
        }

        private bool IsCloseTo(Point p0, Point p1, double eps = 8)
        {
            if (Math.Abs(p0.X - p1.X) > eps) return false;
            if (Math.Abs(p0.Y - p1.Y) > eps) return false;

            return true;
        }

        private double ScreenToWorld(double n, double panningDiff, float zoom)
        {
            return (n - panningDiff) * zoom;
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
            Canvas.Children.Clear();

            this.Canvas.Children.Add(this.Terrain);

            var transformedPoints = new List<Point>();
            foreach (var point in topography.Points)
            {
                var transfPoint = this.WorldToScreen(point, this.panningOffset, this.zoom);
                transformedPoints.Add(transfPoint);
            }
            this.Terrain.Points = new PointCollection(transformedPoints);


            foreach (Point point in topography.Points)
            {
                var transfPoint = this.WorldToScreen(point, this.panningOffset, zoom);
                var rendererPoint = this.CreateRendererPoint(transfPoint, point, this.pointSize);
                this.Canvas.Children.Add(rendererPoint);
            }
        }

        public Ellipse CreateRendererPoint(Point rendererPoint, Point originalPoint, double pointSize)
        {
            var ellipse = new Ellipse();

            ellipse.StrokeThickness = 4;
            ellipse.Stroke = new SolidColorBrush(Colors.Transparent);
            ellipse.Fill = new SolidColorBrush(Colors.Black);
            ellipse.Width = pointSize;
            ellipse.Height = pointSize;
            ellipse.SetValue(Canvas.LeftProperty, rendererPoint.X);
            ellipse.SetValue(Canvas.TopProperty, rendererPoint.Y);
            ellipse.Margin = new Thickness(-pointSize / 2);

            ellipse.ToolTip = $"Position: {originalPoint.X} {originalPoint.Y}";
            ToolTipService.SetBetweenShowDelay(ellipse, 0);
            ToolTipService.SetInitialShowDelay(ellipse, 0);
            ToolTipService.SetShowDuration(ellipse, 999999);

            ellipse.MouseDown += Ellipse_MouseDown;
            ellipse.MouseUp += Window_MouseUp;
            ellipse.MouseEnter += Polygon_MouseEnter;
            ellipse.MouseLeave += Polygon_MouseLeave;

            return ellipse;
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
                if (e.OriginalSource is Ellipse ellipse)
                {
                    this.selectedShape = ellipse;
                    var cursor = e.GetPosition(this);
                    this.dragCursorWorld = this.ScreenToWorld(cursor, this.panningOffset, zoom);
                    Debug.WriteLine(dragCursorWorld);
                }
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.isPanning = false;
            this.Cursor = Cursors.Arrow;
            this.selectedShape = null;
            this.pressed = false;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            this.cursorPos = e.GetPosition(this);

            if (this.pressed && this.selectedShape != null)
            {

                for (var i = 0; i < this.Terrain.Points.Count; i++)
                {
                    var terrainPoint = this.Terrain.Points[i];
                    var isClose = this.IsCloseTo(this.dragCursorWorld, terrainPoint);
                    if (isClose)
                    {
                        var cursorWorld = this.ScreenToWorld(this.cursorPos, this.panningOffset, zoom);
                        this.topography.Points[i] = cursorWorld;
                        this.dragCursorWorld = cursorWorld;
                        Debug.WriteLine(terrainPoint);
                        //break;
                    }
                }

                this.Update();
            }

            if (this.isPanning)
            {
                this.Pan();
            }
        }

        private void Pan()
        {
            this.Update();
            this.panningOffset.X += this.cursorPos.X - this.prevPanning.X;
            this.panningOffset.Y += this.cursorPos.Y - this.prevPanning.Y;
            this.prevPanning = this.cursorPos;
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

                if (e.ClickCount == 2)
                {
                    Debug.WriteLine("Zoom to fit");
                }
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            //var beforeZoomWorld = this.ScreenToWorld(this.cursorPos, panningOffset, zoom);
            //var beforePanning = new Point(this.panningOffset.X, this.panningOffset.Y);
            //var beforePos = this.ScreenToWorld(this.cursorPos, panningOffset, zoom);

            if (e.Delta > 0) 
            {
                if (zoom < 0.01) return;
                zoom *= 0.95f;
            }
            else
            {
                zoom *= 1.05f;
            }

            //var afterPos = this.ScreenToWorld(this.cursorPos, panningOffset, zoom);

            //Point diff = (Point)(afterPos - beforePos);
            //diff = this.WorldToScreen(diff, panningOffset, zoom);

            //this.panningOffset.X += diff.X;
            //this.panningOffset.Y += diff.Y;

            //Debug.WriteLine($"Bef {beforePos}");
            //Debug.WriteLine($"After {afterPos}");
            //Debug.WriteLine($"Diff {diff}");

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
