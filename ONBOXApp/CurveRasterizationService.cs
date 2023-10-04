#if R2024

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ONBOXAppl
{
    public class CurveRasterizationService
    {
        public List<Line> RasterizeByParameter(Curve curve, double maxSpacing)
        {
            var points = new List<XYZ>();
            var divisions = Math.Ceiling(curve.ApproximateLength / maxSpacing);
            var step = curve.ApproximateLength / divisions;

            points.Add(curve.GetEndPoint(0));

            var dist = step;
            while (dist <= curve.ApproximateLength)
            {
                var param = dist / curve.ApproximateLength;
                var currentPoint = curve.Evaluate(param, true);
                points.Add(currentPoint);
                dist += step;
            }

            var lines = new List<Line>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                var start = points[i];
                var end = points[i + 1];

                lines.Add(Line.CreateBound(start, end));
            }

            var lastPoint = points[points.Count - 1];
            var endPoint = curve.GetEndPoint(1);
            if (lastPoint.DistanceTo(endPoint) > 0.001)
            {
                lines.Add(Line.CreateBound(lastPoint, curve.GetEndPoint(1)));
            }

            return lines;
        }

        public List<Line> RasterizeByTesselation(Curve curve, double maxSpacing)
        {
            var tessellatedPoints = curve.Tessellate();
            var prevPoint = tessellatedPoints[0];
            var points = new List<XYZ>();
            points.Add(prevPoint);

            for (int i = 1; i < tessellatedPoints.Count - 1; i++)
            {
                var currentPoint = tessellatedPoints[i];
                var nextPoint = tessellatedPoints[i + 1];
                if (nextPoint.DistanceTo(prevPoint) > maxSpacing)
                {
                    points.Add(currentPoint);
                    prevPoint = currentPoint;
                }
            }

            var lastPoint = tessellatedPoints[tessellatedPoints.Count - 1];
            if (lastPoint.DistanceTo(prevPoint) < maxSpacing)
            {
                points[points.Count - 1] = lastPoint;
            }
            else
            {
                points.Add(lastPoint);
            }

            var lines = new List<Line>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                var start = points[i];
                var end = points[i + 1];

                lines.Add(Line.CreateBound(start, end));
            }

            return lines;
        }

        public List<Line> RasterizeByType(Curve curve, double maxSpacing)
        {
            if (curve is HermiteSpline || curve is NurbSpline)
            {
                return this.RasterizeByTesselation(curve, maxSpacing * 0.5);
            }
            else
            {
                return this.RasterizeByParameter(curve, maxSpacing);
            }
        }
    }
}

#endif