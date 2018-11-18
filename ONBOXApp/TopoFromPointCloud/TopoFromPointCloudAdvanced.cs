using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.PointClouds;
using Autodesk.Revit.DB.Architecture;

namespace ONBOXAppl
{

    [Transaction(TransactionMode.Manual)]
    class TopoFromPointCloudAdvanced : IExternalCommand
    {
        //FamilySymbol normalPoint;

        double pointMinDistance = Utils.ConvertM.cmToFeet(.2);
        int pointMaxQuantity = 100;
        double pointDist = Utils.ConvertM.cmToFeet(10);

        int xNumberOfDivisions = 10;
        int yNumberOfDivisions = 10;

        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513360"));

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            try
            {
                //normalPoint = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(t => t.Name.Contains("DebugPoint")).FirstOrDefault() as FamilySymbol;

                if (uidoc.ActiveView is View3D == false)
                {
                    message = Properties.Messages.BeamsFromColumns_Not3dView;
                    return Result.Failed;
                }

                PointCloudInstance pcInstance = doc.GetElement(sel.PickObject(ObjectType.Element, new PointCloundSelectionFilter(), Properties.Messages.TopoFromPointCloud_SelectPointCloudInstance)) as PointCloudInstance;

                //Element pcInstance = doc.GetElement(sel.PickObject(ObjectType.Element));

                if (pcInstance == null)
                {
                    message = Properties.Messages.TopoFromPointCloud_InvalidPointCloud;
                    return Result.Failed;
                }



                View3D this3dView = uidoc.ActiveView as View3D;
                BoundingBoxXYZ boundingBox = null;

                //Use CropBox if there is one to use
                if (this3dView.CropBoxActive == true)
                {
                    boundingBox = this3dView.CropBox;
                }
                else
                {
                    boundingBox = pcInstance.get_BoundingBox(uidoc.ActiveView);
                }

                boundingBox.Enabled = true;
                Line l1 = Line.CreateBound(boundingBox.Min, boundingBox.Max);

                double crossAngle = XYZ.BasisX.AngleOnPlaneTo(l1.Direction, XYZ.BasisZ);
                double hypotenuse = boundingBox.Min.PlaneDistanceTo(boundingBox.Max);

                double xDirDistance = hypotenuse * Math.Cos(crossAngle);
                double yDirDistance = hypotenuse * Math.Sin(crossAngle);

                double xIncrAmount = 1;
                double yIncrAmount = 1;

                EstabilishIteractionPoints(xDirDistance, ref xNumberOfDivisions, ref xIncrAmount);
                EstabilishIteractionPoints(yDirDistance, ref yNumberOfDivisions, ref yIncrAmount);

                double axIncrAmount = xIncrAmount;
                double ayIncrAmount = yIncrAmount;

                TopoPointCloudUIAdvanced currentUI = new TopoPointCloudUIAdvanced(
                    Math.Round(Utils.ConvertM.feetToM(xDirDistance), 2), Math.Round(Utils.ConvertM.feetToM(yDirDistance),2), xNumberOfDivisions,
                    yNumberOfDivisions , Math.Round(Utils.ConvertM.feetToM(xIncrAmount),2), Math.Round(Utils.ConvertM.feetToM(yIncrAmount),2),
                    pointMaxQuantity);


                currentUI.ShowDialog();

                if (currentUI.DialogResult == false)
                    return Result.Cancelled;

                xNumberOfDivisions = currentUI.xNumberOfDivisions;
                yNumberOfDivisions = currentUI.yNumberOfDivisions;
                xIncrAmount = Utils.ConvertM.mToFeet(currentUI.xIncrAmount);
                yIncrAmount = Utils.ConvertM.mToFeet(currentUI.yIncrAmount);
                pointMaxQuantity = currentUI.pointMaxQuantity;

                pointDist = pcInstance.GetTotalTransform().Scale * pointDist;
                IList<XYZ> topographyPointList = new List<XYZ>();

                using (Transaction t = new Transaction(doc, Properties.Messages.TopoFromPointCloud_Transaction))
                {
                    t.Start();
                    for (int i = 0; i < xNumberOfDivisions; i++)
                    {
                        for (int j = 0; j < yNumberOfDivisions; j++)
                        {
                            IList<Plane> xPlanes = CreatePlaneBound(doc, boundingBox.Min.FlattenZ(), i, j, xIncrAmount, yIncrAmount, XYZ.BasisX, XYZ.BasisY);
                            IList<Plane> yPlanes = CreatePlaneBound2(doc, boundingBox.Min.FlattenZ(), j, i, yIncrAmount, xIncrAmount, XYZ.BasisY, XYZ.BasisX);
#if R2016

                            Plane planeZ1 = new Plane(XYZ.BasisZ, new XYZ(0, 0, -9999));
                            Plane planeZ2 = new Plane(-XYZ.BasisZ, new XYZ(0, 0, 9999));
#else
                            Plane planeZ1 = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, -9999));
                            Plane planeZ2 = Plane.CreateByNormalAndOrigin(-XYZ.BasisZ, new XYZ(0, 0, 9999));
#endif

                            //doc.Create.NewFamilyInstance(planeZ1.Normal, normalPoint, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            //doc.Create.NewFamilyInstance(planeZ2.Normal, normalPoint, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                            IList<Plane> currentPlanes = xPlanes.Union(yPlanes).ToList();
                            currentPlanes.Add(planeZ1);
                            currentPlanes.Add(planeZ2);

                            PointCloudFilter ptFilter = PointCloudFilterFactory.CreateMultiPlaneFilter(currentPlanes);
                            PointCollection pointCollection = pcInstance.GetPoints(ptFilter, pointDist, pointMaxQuantity);

                            IList<XYZ> currentBoxPoints = new List<XYZ>();
                            foreach (CloudPoint currentCloudPoint in pointCollection)
                            {
                                XYZ currentXYZ = new XYZ(currentCloudPoint.X, currentCloudPoint.Y, currentCloudPoint.Z);
                                currentXYZ = pcInstance.GetTotalTransform().OfPoint(currentXYZ);
                                if (!ListContainsPoint(currentBoxPoints, currentXYZ))
                                {
                                    currentBoxPoints.Add(currentXYZ);
                                }
                            }

                            topographyPointList = topographyPointList.Union(currentBoxPoints).ToList();

                        }
                    }

                    if (topographyPointList.Count < 3)
                    {
                        message = Properties.Messages.TopoFromPointCloud_NotEnoughPoints;
                        return Result.Failed;
                    }

                    TopographySurface topoSurface = TopographySurface.Create(doc, topographyPointList);

                    t.Commit();
                }
            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
                return Result.Cancelled;
            }

            return Result.Succeeded;
        }

        private IList<Plane> CreateSetOfPlanes(Document doc, XYZ startingPoint, double mainDirectionAmount, double secondDirectionAmount, XYZ mainDirection, XYZ secondDirection)
        {
            IList<Plane> currentPlanes = new List<Plane>();
            for (int i = 0; i < xNumberOfDivisions; i++)
            {
                if (i > 0)
                    break;

                double xMult = (i * secondDirectionAmount);
                for (int j = 0; j < yNumberOfDivisions; j++)
                {
                    if (j > 0)
                        break;

                    double yMult = (j * mainDirectionAmount);
                    XYZ firstPoint = startingPoint + secondDirection.Multiply(xMult) + mainDirection.Multiply(yMult);
                    XYZ secondPoint = firstPoint + mainDirection.Multiply(mainDirectionAmount);
                    XYZ thirdPoint = new XYZ(0, 0, 1);

                    Plane currentPlane = doc.Create.NewReferencePlane(firstPoint, secondPoint, thirdPoint, doc.ActiveView).GetPlane();

                    XYZ firstPoint2 = firstPoint + secondDirection.Multiply(secondDirectionAmount);
                    XYZ secondPoint2 = firstPoint2 + mainDirection.Multiply(mainDirectionAmount);

                    Plane currentPlane2 = doc.Create.NewReferencePlane(secondPoint2, firstPoint2, thirdPoint, doc.ActiveView).GetPlane();

                    currentPlanes.Add(currentPlane);
                    currentPlanes.Add(currentPlane2);

                }
            }
            return currentPlanes;
        }

        private IList<Plane> CreatePlaneBound(Document doc, XYZ startingPoint, double mainPosition, double secondPosition, double mainDirectionAmount, double secondDirectionAmount, XYZ mainDirection, XYZ secondDirection)
        {

            IList<Plane> currentPlanes = new List<Plane>();

            double xMult = (secondPosition * secondDirectionAmount);
            double yMult = (mainPosition * mainDirectionAmount);

            using (SubTransaction st = new SubTransaction(doc))
            {
                st.Start();
                XYZ firstPoint = startingPoint + secondDirection.Multiply(xMult) + mainDirection.Multiply(yMult);
                XYZ secondPoint = firstPoint + mainDirection.Multiply(mainDirectionAmount);
                XYZ thirdPoint = new XYZ(0, 0, 1);

                XYZ midp1 = Utils.GetPoint.getMidPoint(firstPoint, secondPoint);

                Plane currentPlane = doc.Create.NewReferencePlane(firstPoint, secondPoint, thirdPoint, doc.ActiveView).GetPlane();

                XYZ firstPoint2 = firstPoint + secondDirection.Multiply(secondDirectionAmount - Utils.ConvertM.cmToFeet(2));
                XYZ secondPoint2 = firstPoint2 + mainDirection.Multiply(mainDirectionAmount);

                XYZ midp2 = Utils.GetPoint.getMidPoint(firstPoint2, secondPoint2);

                Plane currentPlane2 = doc.Create.NewReferencePlane(secondPoint2, firstPoint2, thirdPoint, doc.ActiveView).GetPlane();
                currentPlanes.Add(currentPlane);
                currentPlanes.Add(currentPlane2);
                st.RollBack();
            }


            //normalPoint.Activate();
            //doc.Create.NewFamilyInstance(midp1 + currentPlane.Normal, normalPoint, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            //doc.Create.NewFamilyInstance(midp2 + currentPlane2.Normal, normalPoint, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            return currentPlanes;
        }

        private IList<Plane> CreatePlaneBound2(Document doc, XYZ startingPoint, double mainPosition, double secondPosition, double mainDirectionAmount, double secondDirectionAmount, XYZ mainDirection, XYZ secondDirection)
        {

            IList<Plane> currentPlanes = new List<Plane>();

            double xMult = (secondPosition * secondDirectionAmount);
            double yMult = (mainPosition * mainDirectionAmount);

            using (SubTransaction st = new SubTransaction(doc))
            {
                st.Start();
                XYZ firstPoint = startingPoint + secondDirection.Multiply(xMult) + mainDirection.Multiply(yMult);
                XYZ secondPoint = firstPoint + mainDirection.Multiply(mainDirectionAmount);
                XYZ thirdPoint = new XYZ(0, 0, 1);

                XYZ midp1 = Utils.GetPoint.getMidPoint(firstPoint, secondPoint);

                Plane currentPlane = doc.Create.NewReferencePlane(secondPoint, firstPoint, thirdPoint, doc.ActiveView).GetPlane();

                XYZ firstPoint2 = firstPoint + secondDirection.Multiply(secondDirectionAmount - Utils.ConvertM.cmToFeet(2));
                XYZ secondPoint2 = firstPoint2 + mainDirection.Multiply(mainDirectionAmount);

                XYZ midp2 = Utils.GetPoint.getMidPoint(firstPoint2, secondPoint2);

                Plane currentPlane2 = doc.Create.NewReferencePlane(firstPoint2, secondPoint2, thirdPoint, doc.ActiveView).GetPlane();

                currentPlanes.Add(currentPlane);
                currentPlanes.Add(currentPlane2);
                st.RollBack();
            }

            //normalPoint.Activate();
            //doc.Create.NewFamilyInstance(midp1 + currentPlane.Normal, normalPoint, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            //doc.Create.NewFamilyInstance(midp2 + currentPlane2.Normal, normalPoint, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            return currentPlanes;
        }


        //private void CreateSetOfPlanes(Document doc, IList<XYZ> targetPointList, PointCloudInstance targetCloudInstance, XYZ startingPoint, double xIncreaseAmount, double yIncreaseAmount)
        //{
        //    Plane zFirstBound = doc.Application.Create.NewPlane(XYZ.BasisZ, new XYZ(0, 0, -9999));
        //    Plane zSecondBound = doc.Application.Create.NewPlane(-XYZ.BasisZ, new XYZ(0, 0, 9999));

        //    for (int i = 0; i < xNumberOfDivisions; i++)
        //    {
        //        XYZ xFirstBoundOrigin = startingPoint + XYZ.BasisX * (i * xIncreaseAmount);
        //        XYZ xSeconBoundOrigin = xFirstBoundOrigin + XYZ.BasisX * (xIncreaseAmount);

        //        Plane xFirstBound = doc.Application.Create.NewPlane(XYZ.BasisX, xFirstBoundOrigin);
        //        Plane xSecondBound = doc.Application.Create.NewPlane(-XYZ.BasisX, xSeconBoundOrigin);

        //        //CreateRefPlaneFromPlane(doc, xFirstBoundOrigin, XYZ.BasisX);
        //        //CreateRefPlaneFromPlane(doc, xSeconBoundOrigin, -XYZ.BasisX);

        //        for (int j = 0; j < yNumberOfDivisions; j++)
        //        {
        //            XYZ yFirstBoundOrigin = startingPoint + XYZ.BasisY * (j * yIncreaseAmount) + XYZ.BasisX * (i * xIncreaseAmount);
        //            XYZ ySecondBoundOrigin = yFirstBoundOrigin + XYZ.BasisY * (yIncreaseAmount);

        //            Plane yFirstBound = doc.Application.Create.NewPlane(XYZ.BasisY, yFirstBoundOrigin);
        //            Plane ySecondBound = doc.Application.Create.NewPlane(-XYZ.BasisY, ySecondBoundOrigin);

        //            CreateRefPlaneFromPlane(doc, yFirstBoundOrigin, XYZ.BasisY);
        //            //CreateRefPlaneFromPlane(doc, ySecondBoundOrigin, -XYZ.BasisY);

        //            IList<Plane> currentPlanes = new List<Plane> {xFirstBound, xSecondBound, yFirstBound, ySecondBound, zFirstBound, zSecondBound };

        //            PointCloudFilter ptFilter = PointCloudFilterFactory.CreateMultiPlaneFilter(currentPlanes);
        //            PointCollection pointCollection  = targetCloudInstance.GetPoints(ptFilter, pointDistance, pointMaxQuantity); //TODO check for performace, increase the pointDistance

        //            foreach (CloudPoint currentCloudPoint in pointCollection)
        //            {
        //                XYZ currentXYZ = new XYZ(currentCloudPoint.X, currentCloudPoint.Y, currentCloudPoint.Z);
        //                currentXYZ = targetCloudInstance.GetTotalTransform().OfPoint(currentXYZ);
        //                //TODO create another list with just the current Points to analyse
        //                if(!ListContainsPoint(targetPointList, currentXYZ))
        //                {
        //                    targetPointList.Add(currentXYZ);
        //                }
        //            }
        //        }
        //    }
        //}

        private void EstabilishIteractionPoints(double targetDistance, ref int divisions, ref double increaseAmount)
        {
            if (targetDistance / divisions <= Utils.ConvertM.cmToFeet(100))
                divisions = (int)(Math.Ceiling(targetDistance / Utils.ConvertM.cmToFeet(100)));

            increaseAmount = targetDistance / divisions;
        }

        private void EstabilishIteractionPoints(double targetDistance, double maxPointDistInFeet, out int numberOfInteractions, out double increaseAmount)
        {
            if (maxPointDistInFeet > targetDistance)
                numberOfInteractions = 1;
            else
                numberOfInteractions = (int)(Math.Ceiling(targetDistance / maxPointDistInFeet));

            increaseAmount = 1.00 / numberOfInteractions;
        }

        private bool ListContainsPoint(IList<XYZ> targetListOfPoints, XYZ targetPoint)
        {
            foreach (XYZ currentPoint in targetListOfPoints)
            {
                if (currentPoint.FlattenZ().IsAlmostEqualTo(targetPoint.FlattenZ(), pointMinDistance))
                    return true;
            }

            return false;
        }


        private void CreateRefPlaneFromPlane(Document doc, XYZ pOrigin, XYZ pNormal)
        {
            XYZ direction = pNormal.CrossProduct(XYZ.BasisZ);

            XYZ planeFirstP = pOrigin + direction * Utils.ConvertM.cmToFeet(200);
            XYZ planeSecondP = pOrigin - direction * Utils.ConvertM.cmToFeet(200);

            doc.Create.NewReferencePlane(planeFirstP, planeSecondP, XYZ.BasisZ, doc.ActiveView);
        }

    }
}
