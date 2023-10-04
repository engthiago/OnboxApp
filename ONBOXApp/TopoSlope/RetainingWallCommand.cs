using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ONBOXAppl.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ONBOXAppl
{
    [Transaction(TransactionMode.Manual)]
    internal class RetainingWallCommand : IExternalCommand
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

            var wallFilter = new TypeSelectionFilter<Wall>();
            var pickResult = SelectionUtils.PickFace(uidoc, wallFilter, "Please pick a face of a wall.", out message);
            if (pickResult.result != Result.Succeeded)
            {
                return pickResult.result;
            }
            var wall = pickResult.element as Wall;
            var face = wall.GetGeometryObjectFromReference(pickResult.reference) as Face;

            var locationCurve = wall.Location as LocationCurve;
            if (locationCurve == null || face == null)
            {
                message = "Wall Geometry is too complex";
                return Result.Failed;
            }

            var curveRasterization = new CurveRasterizationService();
            var lines = curveRasterization.RasterizeByType(locationCurve.Curve, 5);

            var flattenLines = lines.Select((l) =>
            {
                var start = l.GetEndPoint(0);
                var end = l.GetEndPoint(1);

                start = new XYZ(start.X, start.Y, 0);
                end = new XYZ(end.X, end.Y, 0);

                return Line.CreateBound(start, end);
            });

            var loop = CurveLoop.Create(flattenLines.Select(l => l as Curve).ToList());
            var pickSideLoop = CurveLoop.CreateViaOffset(loop, wall.Width / 2, XYZ.BasisZ);
            var oposideSideLoop = CurveLoop.CreateViaOffset(loop, -wall.Width / 2, XYZ.BasisZ);

            var interestPoint = pickResult.reference.GlobalPoint;
            interestPoint = new XYZ(interestPoint.X, interestPoint.Y, 0);

            if (this.DistanceFromCurveLoop(pickSideLoop, interestPoint) > this.DistanceFromCurveLoop(oposideSideLoop, interestPoint))
            {
                var temp = oposideSideLoop;
                oposideSideLoop = pickSideLoop;
                pickSideLoop = temp;
            }

            //var topoFilter = new TypeSelectionFilter<Toposolid>();
            //Toposolid topoSolid = null;
            //var pickSolidResult = SelectionUtils.PickOrGetSelectedElement(uidoc, topoFilter, "Please pick a Topo Solid.", out message, out topoSolid);
            //if (pickSolidResult != Result.Succeeded)
            //{
            //    return pickSolidResult;
            //}

            var topoIntersector = new ElementRayIntersector(topoSurfaces.Select(e => e.Id).ToList(), current3dView);
            using (Transaction t = new Transaction(doc, "Create"))
            {
                t.Start();

                var skp = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));

                foreach (var curve in loop)
                {
                    doc.Create.NewModelCurve(curve, skp);
                }
                foreach (var curve in pickSideLoop)
                {
                    doc.Create.NewModelCurve(curve, skp);
                }
                //foreach (var curve in oposideSideLoop)
                //{
                //    doc.Create.NewModelCurve(curve, skp);
                //}

                var pickSidePoints = new List<(Toposolid, XYZ)>();
                for (int i = 0; i < pickSideLoop.Count(); i++)
                {
                    var curve = pickSideLoop.ElementAt(i);
                    var topoResult = topoIntersector.Shoot(curve.GetEndPoint(0));
                    PopulateRays(doc, pickSidePoints, topoResult);

                    if (i == pickSideLoop.Count() - 1)
                    {
                        var topoResult2 = topoIntersector.Shoot(curve.GetEndPoint(1));
                        PopulateRays(doc, pickSidePoints, topoResult2);
                    }
                }

                var oposideSidePoints = new List<(Toposolid, XYZ)>();
                for (int i = 0; i < oposideSideLoop.Count(); i++)
                {
                    var curve = oposideSideLoop.ElementAt(i);
                    var topoResult = topoIntersector.Shoot(curve.GetEndPoint(0));
                    PopulateRays(doc, oposideSidePoints, topoResult);

                    if (i == oposideSideLoop.Count() - 1)
                    {
                        var topoResult2 = topoIntersector.Shoot(curve.GetEndPoint(1));
                        PopulateRays(doc, oposideSidePoints, topoResult2);
                    }
                }

                foreach (var tuple in pickSidePoints)
                {
                    var topoSolid = tuple.Item1;
                    var point = tuple.Item2;

                    //topoSolids.Add(topoSolid);

                    topoSolid.GetSlabShapeEditor().DrawPoint(point);
                }

                var wallHeightParam = wall.GetParameter(new ForgeTypeId("autodesk.revit.parameter:wallBaseOffset-1.0.0"));
                foreach (var tuple in oposideSidePoints)
                {
                    var topoSolid = tuple.Item1;
                    var point = tuple.Item2;

                    point = new XYZ(point.X, point.Y, 5);
                    //topoSolids.Add(topoSolid);

                    topoSolid.GetSlabShapeEditor().DrawPoint(point);
                }

                t.Commit();
            }


            return Result.Succeeded;
        }

        private static void PopulateRays(Document doc, List<(Toposolid, XYZ)> offsetOuterLoopPoints, ElementRayIntersectorResult topoResult)
        {
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

        public double DistanceFromCurveLoop(CurveLoop curveLoop, XYZ point)
        {
            var minDist = double.MaxValue;
            foreach (var curve in curveLoop)
            {
                var dist = curve.Distance(point);
                minDist = Math.Min(minDist, dist);
            }
            return minDist;
        }
    }
}
