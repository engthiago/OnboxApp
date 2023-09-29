using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace CanvasTests
{
    //Points="-5,350 100,320 120,310 160,330 180,320 650,320 650,500 -5,500"
    public class Point2d
    {
        public Point2d(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }

        public Point2d()
        {
            this.X = 0;
            this.Y = 0;
        }

        public double X { get; set; }
        public double Y { get; set; }

        public double Distance(Point2d point)
        {
            var dx = this.X - point.X;
            var dy = this.Y - point.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double Distance(Point point)
        {
            var dx = this.X - point.X;
            var dy = this.Y - point.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool IsAlmostEqualTo(Point2d p1, double eps = 0.001)
        {
            if (Math.Abs(this.X - p1.X) > eps) return false;
            if (Math.Abs(this.Y - p1.Y) > eps) return false;

            return true;
        }

        public Point2d Sub(Point2d p2)
        {
            return new Point2d(this.X - p2.X, this.Y - p2.Y);
        }

        static public implicit operator Point(Point2d point)
        {
            return new Point(point.X, point.Y);
        }

        public static implicit operator Point2d(Point v)
        {
            return new Point2d(v.X, v.Y);
        }

        public override string ToString()
        {
            return $"{this.X} {this.Y}";
        }
    }

    public class TopoLineProfile
    {
        public List<Point2d> Points { get; set; }
    }

    public class BoundingBox2d
    {
        public Point2d Min { get; set; }
        public Point2d Max { get; set; }
        public Point2d Center { get; set; }
        public bool IsValid { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool pressed = false;
        private Point2d cursorPos = new Point2d();

        private Shape selectedShape;
        private Point2d selectedShapePos;
        private bool isPanning;
        private Point2d prevPanning = new Point2d();
        private Point2d panningOffset = new Point2d();

        private float zoom = 1;

        private TopoLineProfile topography = new TopoLineProfile();
        private double pointSize = 8;
        private Point2d dragCursorWorld = new Point2d();
        private List<Point2d> debugPoints = new List<Point2d>();

        private Point2d centerPoint = new Point2d();
        private readonly double WindowTitleHeight;

        public MainWindow()
        {
            this.InitializeComponent();

            this.topography.Points = new List<Point2d>
            {
                new Point2d(0, 0),
                new Point2d(100, 120),
                new Point2d(120, 110),
                new Point2d(160, 130),
                new Point2d(180, 120),
                new Point2d(650, 120),
                new Point2d(650, -100),
                new Point2d(0, -100),
            };

            this.WindowTitleHeight = new WindowChrome().CaptionHeight;
            this.Loaded += this.MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //this.ZoomToFit();
        }

        private double ScreenToWorld(double n, double panningDiff, float zoom)
        {
            return (n - panningDiff) / zoom;
        }

        private double WorldToScreen(double n, double panningDiff, float zoom)
        {
            //n = viewHeight - n;
            return ((n * zoom) + panningDiff);
        }

        private Point2d ScreenToWorld(Point p, Point panningDiff, float zoom)
        {
            var x = this.ScreenToWorld(p.X, panningDiff.X, zoom);
            var y = this.ScreenToWorld(p.Y, -panningDiff.Y, zoom);
            y = this.ActualHeight - y;
            return new Point2d(x, y);
        }

        private Point2d WorldToScreen(Point p, Point panningDiff, float zoom)
        {
            var x = this.WorldToScreen(p.X, panningDiff.X, zoom);

            var y = this.ActualHeight - p.Y;
            y = this.WorldToScreen(y, -panningDiff.Y, zoom);

            return new Point2d(x, y);
        }

        private void Update()
        {
            this.Canvas.Children.Clear();

            this.Canvas.Children.Add(this.Terrain);

            var transformedPoints = new List<Point>();
            foreach (var point in this.topography.Points)
            {
                var transfPoint = this.WorldToScreen(point, this.panningOffset, this.zoom);
                transformedPoints.Add(transfPoint);
            }
            this.Terrain.Points = new PointCollection(transformedPoints);

            var bb = this.ComputeBoundingBox(this.topography.Points);
            if (bb.IsValid)
            {
                var widthInPx = this.WorldToScreen(Math.Abs(bb.Max.X - bb.Min.X), 0, this.zoom);
                var heightInPx = this.WorldToScreen(Math.Abs(bb.Max.Y - bb.Min.Y), 0, this.zoom);

                var width = bb.Max.X - bb.Min.X;
                var ratio = widthInPx / heightInPx;

                this.terrainScale.ScaleX = 6000 / (width) / 74;
                this.terrainScale.ScaleY = ratio * this.terrainScale.ScaleX;
            }

            foreach (var point in this.topography.Points)
            {
                var transfPoint = this.WorldToScreen(point, this.panningOffset, this.zoom);
                var rendererPoint = this.CreateRendererPoint(transfPoint, point, this.pointSize);
                this.Canvas.Children.Add(rendererPoint);
            }

            foreach (var point in this.debugPoints)
            {
                var transfPoint = this.WorldToScreen(point, this.panningOffset, this.zoom);
                var debg = this.CreateRendererPoint(transfPoint, point, 20);
                debg.IsEnabled = false;
                debg.Fill = new SolidColorBrush(Colors.Red);
                this.Canvas.Children.Add(debg);
            }

            //var centerPointScreen = this.WorldToScreen(this.centerPoint, this.panningOffset, this.zoom);
            //var centerPointElem = this.CreateRendererPoint(centerPointScreen, this.centerPoint, this.pointSize);
            //centerPointElem.IsEnabled = false;
            //centerPointElem.Fill = new SolidColorBrush(Colors.Red);
            //this.Canvas.Children.Add(centerPointElem);
        }

        private void UpdateMousePosStats()
        {
            var worldPos = this.ScreenToWorld(this.cursorPos, this.panningOffset, this.zoom);
            var screenPos = this.WorldToScreen(worldPos, this.panningOffset, this.zoom);

            this.Coords.Content = $"{this.cursorPos.X} {this.cursorPos.Y}\n{worldPos.X} {worldPos.Y}\n{screenPos.X} {screenPos.Y}\n{this.zoom}";
        }

        public Ellipse CreateRendererPoint(Point2d rendererPoint, Point2d originalPoint, double pointSize)
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

            ellipse.MouseDown += this.Window_MouseDown;
            ellipse.MouseUp += this.Window_MouseUp;
            ellipse.MouseEnter += this.Polygon_MouseEnter;
            ellipse.MouseLeave += this.Polygon_MouseLeave;

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

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.isPanning = false;
            this.Cursor = Cursors.Arrow;
            this.selectedShape = null;
            this.pressed = false;
            this.Update();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            this.cursorPos = e.GetPosition(this);
            this.UpdateMousePosStats();

            if (this.pressed)
            {
                this.dragCursorWorld = this.ScreenToWorld(this.cursorPos, this.panningOffset, this.zoom);
                if (this.selectedShape != null)
                {
                    this.selectedShapePos.X = this.dragCursorWorld.X;
                    this.selectedShapePos.Y = this.dragCursorWorld.Y;
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
            this.panningOffset.Y -= this.cursorPos.Y - this.prevPanning.Y;
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
                    this.ZoomToFit();
                }
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.cursorPos = e.GetPosition(this);
                this.pressed = true;
                this.dragCursorWorld = this.ScreenToWorld(this.cursorPos, this.panningOffset, this.zoom);
                //this.renderDragCursor = true;
                this.Update();

                if (e.OriginalSource is Ellipse shape)
                {
                    if (shape.Tag?.ToString() != "drag")
                    {
                        this.selectedShape = shape;
                        this.selectedShapePos = this.topography.Points.OrderBy(p => p.Distance(this.dragCursorWorld)).FirstOrDefault();
                    }
                }
            }

        }

        public BoundingBox2d ComputeBoundingBox(List<Point2d> points)
        {
            var bb = new BoundingBox2d();

            // Compute BB
            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;

            if (points.Count == 0)
            {
                return bb;
            }

            foreach (var point in this.topography.Points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            var width = Math.Abs(maxX - minX);
            var height = Math.Abs(maxY - minY);

            if (width > 0 && height > 0)
            {
                bb.IsValid = true;
            }

            bb.Min = new Point2d(minX, minY);
            bb.Max = new Point2d(maxX, maxY);

            bb.Center = new Point2d((minX + maxX) / 2, (minY + maxY) / 2);

            return bb;
        }

        private void ZoomToFit()
        {
            var bb = this.ComputeBoundingBox(this.topography.Points);
            if (bb.IsValid)
            {
                var minScreen = this.WorldToScreen(bb.Min, this.prevPanning, this.zoom);
                var maxScreen = this.WorldToScreen(bb.Max, this.prevPanning, this.zoom);

                var widthScreen = Math.Abs(maxScreen.X - minScreen.X);
                var heightScreen = Math.Abs(maxScreen.Y - minScreen.Y);

                var widthRatio = this.Width / widthScreen;
                var heightRatio = this.Height / heightScreen;

                var minRatio = Math.Min(widthRatio, heightRatio) * 0.9;
                if (minRatio > 0)
                {
                    this.zoom *= (float)minRatio;
                }

                var pos = this.WorldToScreen(bb.Center, this.panningOffset, this.zoom);
                this.panningOffset.Y = this.panningOffset.Y + pos.Y - (this.Height / 2) + this.WindowTitleHeight;
                this.panningOffset.X = this.panningOffset.X - pos.X + (this.Width / 2) - this.WindowTitleHeight / 2;
            }
            else
            {
                this.panningOffset = new Point2d();
                this.zoom = 1;
                return;
            }

            this.Update();
            this.UpdateMousePosStats();
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pointOnWorldBefore = this.ScreenToWorld(this.cursorPos, this.panningOffset, this.zoom);

            if (e.Delta > 0) 
            {
                this.zoom *= 1.10f;
                this.zoom = Math.Min(this.zoom, 100f);
            }
            else
            {
                this.zoom *= 0.90f;
                this.zoom = Math.Max(this.zoom, 0.01f);
            }

            // Offset panning to zoom where the mouse cursor is
            var screenDifference = this.WorldToScreen(pointOnWorldBefore, this.panningOffset, this.zoom);
            var x = screenDifference.X - this.cursorPos.X;
            var y = screenDifference.Y - this.cursorPos.Y;
            this.panningOffset.X -= x;
            this.panningOffset.Y += y;

            //Debug.WriteLine(pointOnWorldBefore);
            //Debug.WriteLine(screenDifference);

            this.Update();
            this.UpdateMousePosStats();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.Update();
        }
    }
}
