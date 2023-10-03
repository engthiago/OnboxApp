#if R2024

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;


namespace ONBOXAppl
{
    public class TypeSelectionFilter<T> : ISelectionFilter where T : Element
    {
        public bool AllowElement(Element elem)
        {
            if (elem is T) return true;
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    public class FindBoundsResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public CurveLoop OuterBounds { get; set; }
        public List<CurveLoop> InnerBounds { get; set; }
    }

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
    }

    public class BoundsService
    {
        public FindBoundsResult FindBounds(Floor floor)
        {
            var result = new FindBoundsResult();
            var doc = floor.Document;

            IList<Reference> topFacesReferences = HostObjectUtils.GetTopFaces(floor);
            IList<CurveLoop> edgeLoops = null;

            if (topFacesReferences.Count > 1)
            {
                using (var tempGroup = new TransactionGroup(doc, "tempTransactionGroup"))
                {
                    tempGroup.Start();

                    using (var tempTransaction = new Transaction(doc, "tempTransaction"))
                    {
                        tempTransaction.Start();

                        var slabEditor = floor.GetSlabShapeEditor();
                        slabEditor.ResetSlabShape();

                        tempTransaction.Commit();
                    }

                    topFacesReferences = HostObjectUtils.GetTopFaces(floor);
                    GeometryObject currentFaceObj = floor.GetGeometryObjectFromReference(topFacesReferences[0]);
                    if (currentFaceObj is PlanarFace planarFace)
                    {
                        var plannarDirection = planarFace.FaceNormal;
                        var plannarOrigin = planarFace.Origin;
                        edgeLoops = planarFace.GetEdgesAsCurveLoops();
                    }

                    tempGroup.RollBack();
                }
            }
            else
            {
                GeometryObject currentFaceObj = floor.GetGeometryObjectFromReference(topFacesReferences[0]);
                if (currentFaceObj is PlanarFace planarFace)
                {
                    //var plannarDirection = planarFace.FaceNormal;
                    //var plannarOrigin = planarFace.Origin;
                    edgeLoops = planarFace.GetEdgesAsCurveLoops();
                }
            }

            if (edgeLoops == null)
            {
                result.ErrorMessage = "Error stabilishing floor edges";
            }

            //Sort the curves so the outer loop comes first
            IList<IList<CurveLoop>> curveLoopLoop = ExporterIFCUtils.SortCurveLoops(edgeLoops);
            var orderedLoops = curveLoopLoop.FirstOrDefault();
            if (orderedLoops == null)
            {
                result.ErrorMessage = "Error sorting floor bounds";
            }

            if (orderedLoops.Count == 0)
            {
                result.ErrorMessage = "Error finding the outer bounds of the floor";
            }

            result.OuterBounds = orderedLoops[0];
            result.InnerBounds = orderedLoops.Skip(1).ToList();
            result.Success = true;
            return result;
        }
    }

    public class ElementRayIntersectorResult
    {
        public bool Success { get; set; }
        public ReferenceWithContext Context { get; set; }
        public XYZ HitPoint { get; set; }
    }

    public class ElementRayIntersector
    {
        private readonly ReferenceIntersector refIntersector;
        private readonly double rayHeight = 100_000;
        private readonly XYZ direction = XYZ.BasisZ.Negate();

        public ElementRayIntersector(List<ElementId> againstElements, View3D view3d)
        {
            this.refIntersector = new ReferenceIntersector(againstElements, FindReferenceTarget.Element, view3d);
        }

        public ElementRayIntersectorResult Shoot(XYZ point)
        {
            var result = new ElementRayIntersectorResult();
            var refPoint = new XYZ(point.X, point.Y, this.rayHeight);
            var refResult = refIntersector.FindNearest(refPoint, this.direction);

            if (refResult != null)
            {
                result.Success = true;
                result.Context = refResult;
                result.HitPoint = refPoint.Add(direction.Multiply(refResult.Proximity));
            }

            return result;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class TopoSolidSlopeCommand : IExternalCommand
    {
        public bool PickOrGetSelectedElement<T, S>(UIDocument uidoc, S filter, string pickingPrompt, out string message, out T element) where T : Element where S : ISelectionFilter
        {
            var doc = uidoc.Document;
            var selection = uidoc.Selection.GetElementIds();
            element = null;
            message = null;
            if (selection.Count > 0)
            {
                foreach (var selId in selection)
                {
                    element = doc.GetElement(selId) as T;
                    if (element != null)
                    {
                        break;
                    }
                }
            }

            try
            {
                if (element == null)
                {
                    var reference = uidoc.Selection.PickObject(ObjectType.Element, filter, pickingPrompt);
                    element = doc.GetElement(reference) as T;
                }
            }
            catch (OperationCanceledException ex)
            {
                return false;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }

            if (element == null)
            {
                return false;
            }

            return true;
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            View3D current3dView = doc.ActiveView as View3D;
            if (current3dView == null)
            {
                message = Properties.Messages.SlopeGradingFromPads_Not3DView;
                return Result.Failed;
            }

            var topoSurfaces = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Toposolid)
                .WhereElementIsNotElementType()
                .ToList();

            if (topoSurfaces.Count == 0)
            {
                message = "No Toposurface found in the project";
                return Result.Failed;
            }

            //var topoSolidFilter = new TypeSelectionFilter<Toposolid>();
            //Toposolid topoSolid = null;
            //if (!this.PickOrGetSelectedElement(uidoc, topoSolidFilter, "Pick a topoSolid", out message, out topoSolid))
            //{
            //    return Result.Failed;
            //}

            var floorSelFilter = new TypeSelectionFilter<Floor>();
            Floor floor = null;
            if (!this.PickOrGetSelectedElement(uidoc, floorSelFilter, "Pick a floor", out message, out floor))
            {
                return Result.Failed;
            }

            //var stopWatch = new Stopwatch();
            //stopWatch.Start();

            //var topFaces = HostObjectUtils.GetTopFaces(floor);
            //if (topFaces == null || topFaces.Count == 0)
            //{
            //    message = "No top face found";
            //    return Result.Failed;
            //}

            //if (topFaces.Count > 1)
            //{
            //    message = "More than one top face found";
            //    return Result.Failed;
            //}

            //var pointSymbol = new FilteredElementCollector(doc)
            //    .OfCategory(BuiltInCategory.OST_GenericModel)
            //    .WhereElementIsElementType()
            //    .First(f => f.Name == "Red") as FamilySymbol;

            var boundsService = new BoundsService();
            var boundResult = boundsService.FindBounds(floor);

            if (!boundResult.Success)
            {
                message = boundResult.ErrorMessage;
                return Result.Failed;
            }

            var maxSpacing = 5;
            var curveRaster = new CurveRasterizationService();
            var allLines = new List<Curve>();
            foreach (var curve in boundResult.OuterBounds)
            {
                if (curve is HermiteSpline || curve is NurbSpline)
                {
                    var lines = curveRaster.RasterizeByTesselation(curve, maxSpacing * 0.5);
                    allLines.AddRange(lines);
                }
                else
                {
                    var lines = curveRaster.RasterizeByParameter(curve, maxSpacing);
                    allLines.AddRange(lines);
                }
            }

            var outerCuveLoop = CurveLoop.Create(allLines);


            //foreach (var curve in outerCuveLoop)
            //{
            //    doc.Create.NewDetailCurve(doc.ActiveView, curve);
            //}

            //foreach (var curve in outerCurveOffset)
            //{
            //    doc.Create.NewDetailCurve(doc.ActiveView, curve);
            //}

            var topoIntersector = new ElementRayIntersector(topoSurfaces.Select(e => e.Id).ToList(), current3dView);
            var floorIntersector = new ElementRayIntersector(new List<ElementId> { floor.Id }, current3dView);
            var maxDist = double.NegativeInfinity;

            var outerLoopPoints = new List<(Toposolid, XYZ)>();
            var offsetOuterLoopPoints = new List<(Toposolid, XYZ)>();

            foreach (var curve in outerCuveLoop)
            {
                var point = curve.GetEndPoint(0);
                var topoResult = topoIntersector.Shoot(point);
                if (topoResult.Success)
                {
                    var floorResult = floorIntersector.Shoot(point);
                    if (floorResult.Success)
                    {
                        var topoSolid = doc.GetElement(topoResult.Context.GetReference().ElementId) as Toposolid;
                        if (topoSolid.HostTopoId != ElementId.InvalidElementId)
                        {
                            var param = topoSolid.GetParameter(new ForgeTypeId("autodesk.revit.parameter:toposolidSubdivideHeignt-1.0.0"));
                            if (param != null)
                            {
                                var zDiff = param.AsDouble();
                                floorResult.HitPoint = new XYZ(floorResult.HitPoint.X, floorResult.HitPoint.Y, floorResult.HitPoint.Z - zDiff);
                            }
                            topoSolid = doc.GetElement(topoSolid.HostTopoId) as Toposolid;
                        }
                        outerLoopPoints.Add((topoSolid, floorResult.HitPoint));

                        var currentDist = topoResult.HitPoint.DistanceTo(point);
                        maxDist = Math.Max(maxDist, currentDist);

                        floorResult.Context.Dispose();
                    }
                    topoResult.Context.Dispose();
                }
            }

            if (maxDist == double.NegativeInfinity)
            {
                message = Properties.Messages.SlopeGradingFromPads_NoTopoAssociate;
                return Result.Failed;
            }

            double offsetDist = maxDist / Math.Tan(0.524);
            if (offsetDist > 0)
            {
                var outerCurveOffset = CurveLoop.CreateViaOffset(outerCuveLoop, offsetDist, XYZ.BasisZ);
                foreach (var curve in outerCurveOffset)
                {
                    var topoResult = topoIntersector.Shoot(curve.GetEndPoint(0));
                    if (topoResult.Success)
                    {
                        var topoSolid = doc.GetElement(topoResult.Context.GetReference().ElementId) as Toposolid;
                        if (topoSolid.HostTopoId != ElementId.InvalidElementId)
                        {
                            var param = topoSolid.GetParameter(new ForgeTypeId("autodesk.revit.parameter:toposolidSubdivideHeignt-1.0.0"));
                            if (param != null)
                            {
                                var zDiff = param.AsDouble();
                                topoResult.HitPoint = new XYZ(topoResult.HitPoint.X, topoResult.HitPoint.Y, topoResult.HitPoint.Z - zDiff);
                            }
                            topoSolid = doc.GetElement(topoSolid.HostTopoId) as Toposolid;
                        }

                        offsetOuterLoopPoints.Add((topoSolid, topoResult.HitPoint));
                    }

                    topoResult.Context.Dispose();
                }
            }

            using (var t = new Transaction(doc, "Offset"))
            {
                t.Start();

                var topoSolids = new HashSet<Toposolid>();
                foreach (var tuple in outerLoopPoints)
                {
                    var topoSolid = tuple.Item1;
                    var point = tuple.Item2;

                    topoSolids.Add(topoSolid);

                    topoSolid.GetSlabShapeEditor().DrawPoint(point);
                }

                foreach (var tuple in offsetOuterLoopPoints)
                {
                    var topoSolid = tuple.Item1;
                    var point = tuple.Item2;

                    topoSolids.Add(topoSolid);

                    topoSolid.GetSlabShapeEditor().DrawPoint(point);
                }

                foreach (var toposolid in topoSolids)
                {
                    try
                    {
                        if (!JoinGeometryUtils.AreElementsJoined(doc, floor, toposolid))
                        {
                            JoinGeometryUtils.JoinGeometry(doc, floor, toposolid);
                        }
                    }
                    catch{}
                }

                t.Commit();
            }

            //stopWatch.Stop();
            //TaskDialog.Show("Time", stopWatch.ElapsedMilliseconds.ToString());

            return Result.Succeeded;
        }
    }
}

#endif