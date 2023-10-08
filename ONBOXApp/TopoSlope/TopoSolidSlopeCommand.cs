#if R2024

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ONBOXAppl.Properties;

namespace ONBOXAppl
{

    [Transaction(TransactionMode.Manual)]
    public class TopoSolidSlopeCommand : IExternalCommand
    {

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
                message = Messages.Toposolid_SlopeGrading_NoTopoSolid;
                return Result.Failed;
            }

            //Runs the command
            try
            {
                //UI
                TopoSlopesUI topoSlopeWindow = new TopoSlopesUI();

                if (topoSlopeWindow.ShowDialog() == false)
                    return Result.Cancelled;

                double maxPointDistInMeters = topoSlopeWindow.MaxDist;
                double maxPointDistInFeet = UnitUtils.ConvertToInternalUnits(maxPointDistInMeters, UnitTypeId.Meters);
                double targetAngle = topoSlopeWindow.Angle;
                double targetAngleInRadians = UnitUtils.Convert(targetAngle, UnitTypeId.Degrees, UnitTypeId.Radians);
                bool isContinuous = topoSlopeWindow.IsContinuous;

                bool enterLoop = true;
                while (enterLoop)
                {
                    if (isContinuous == false)
                        enterLoop = false;

                    if (RunTopoGrading(uidoc, topoSurfaces, ref message, maxPointDistInFeet, targetAngleInRadians) == Result.Failed)
                    {
                        return Result.Failed;
                    }
                }

            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
            }

            return Result.Succeeded;


        }

        public Result RunTopoGrading(UIDocument uidoc, List<Element> topoSurfaces, ref string message, double maxPointDistInFeet, double targetAngleInRadians)
        {
            Document doc = uidoc.Document;
            View3D current3dView = doc.ActiveView as View3D;
            //var topoSolidFilter = new TypeSelectionFilter<Toposolid>();
            //Toposolid topoSolid = null;
            //if (!this.PickOrGetSelectedElement(uidoc, topoSolidFilter, "Pick a topoSolid", out message, out topoSolid))
            //{
            //    return Result.Failed;
            //}

            var floorSelFilter = new TypeSelectionFilter<Floor>();
            Floor floor = null;
            var pickResult = SelectionUtils.PickOrGetSelectedElement(uidoc, floorSelFilter, Messages.Toposolid_SlopeGrading_PickFloor, out message, out floor);
            if (pickResult != Result.Succeeded)
            {
                return pickResult;
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

            var maxSpacing = maxPointDistInFeet;
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
                message = Messages.SlopeGradingFromPads_NoTopoAssociate;
                return Result.Failed;
            }

            double offsetDist = maxDist / Math.Tan(targetAngleInRadians);
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

                        topoResult.Context.Dispose();
                    }
                }
            }

            using (var t = new Transaction(doc, Messages.Toposolid_SlopeGrading_Create))
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
                    catch { }
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