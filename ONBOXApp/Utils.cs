using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.ApplicationServices;
using System.Linq;
using ONBOXAppl;
using System.Windows.Controls;
using System.Windows;

namespace Utils
{

    public class beamFailureHandler : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failAcess)
        {
            failAcess.DeleteAllWarnings();
            return FailureProcessingResult.Continue;
        }
    }

    public static class FindElements
    {
        public static Element findElement(string targetTypeName, string targetFamily, BuiltInCategory targetCategory, Document targetDocument)
        {

            //Creates a element filter collector for the active document
            FilteredElementCollector elemColl = new FilteredElementCollector(targetDocument);
            //Filter the category
            ElementCategoryFilter catFilter = new ElementCategoryFilter(targetCategory);

            Element returnedElement = elemColl.WherePasses(catFilter).WhereElementIsElementType().Cast<FamilySymbol>().Where(s => s.FamilyName == targetFamily).Where(q => q.Name == targetTypeName).First();

            if (returnedElement != null)
            {
                return returnedElement;
            }
            else
            {
                return null;
            }
        }

        public static bool thisTypeExist(string targetTypeName, string targetFamily, BuiltInCategory targetCategory, Document targetDocument)
        {
            FilteredElementCollector typeExistCol = new FilteredElementCollector(targetDocument);
            ElementCategoryFilter catFilter = new ElementCategoryFilter(targetCategory);

            Element thisFamilyElement = typeExistCol.OfClass(typeof(Family)).Cast<Family>().Where(q => q.Name == targetFamily).First();

            Family thisFamily = thisFamilyElement as Family;

            ISet<ElementId> FamilySymbols = thisFamily.GetFamilySymbolIds();

            foreach (ElementId thisSymbolID in FamilySymbols)
            {
                string SymbolName = targetDocument.GetElement(thisSymbolID).Name;
                if (SymbolName == targetTypeName)
                {
                    return true;
                }
                else
                {
                    continue;
                }
            }
            // if the Type Name doesnt exist
            return false;
        }

        static internal bool CheckTypeForDuplicate(string targetName, Family targetFamily, Document targetDoc)
        {
            IList<ElementId> FamilyTypes = targetFamily.GetFamilySymbolIds().ToList();
            bool exists = false;
            foreach (ElementId currentTypeID in FamilyTypes)
            {
                if ((targetDoc.GetElement(currentTypeID) as FamilySymbol).Name == targetName)
                {
                    exists = true;
                }
            }
            return exists;
        }

        static internal FamilySymbol GetFamilySymbol(string targetName, Family targetFamily, Document targetDoc)
        {
            IList<ElementId> FamilyTypes = targetFamily.GetFamilySymbolIds().ToList();
            FamilySymbol symbolToReturn = null;
            foreach (ElementId currentTypeID in FamilyTypes)
            {
                if ((targetDoc.GetElement(currentTypeID) as FamilySymbol).Name == targetName)
                {
                    symbolToReturn = targetDoc.GetElement(currentTypeID) as FamilySymbol;
                }
            }
            return symbolToReturn;
        }

        static internal IList<Element> GetElementsInLevelBounds(Document targetDoc, ElementId targetLevelId, double targetToleranceInCm, BuiltInCategory targetCategory)
        {
            Level targetLevel = targetDoc.GetElement(targetLevelId) as Level;

            double levelElevation = targetLevel.ProjectElevation;

            double minHeight = levelElevation - Utils.ConvertM.cmToFeet(targetToleranceInCm);
            double maxHeight = levelElevation + Utils.ConvertM.cmToFeet(targetToleranceInCm);

            XYZ minPoint = new XYZ(-999, -999, minHeight);
            XYZ maxPoint = new XYZ(999, 999, maxHeight);

            Outline targetOutline = new Outline(minPoint, maxPoint);

            BoundingBoxIntersectsFilter bbIntersect = new BoundingBoxIntersectsFilter(targetOutline);
            BoundingBoxIsInsideFilter bbInside = new BoundingBoxIsInsideFilter(targetOutline);
            LogicalOrFilter orFilter = new LogicalOrFilter(bbInside, bbIntersect);

            IList<Element> elementsToReturn = new FilteredElementCollector(targetDoc).OfCategory(targetCategory).WherePasses(orFilter).WhereElementIsNotElementType().ToList();

            return elementsToReturn;

        }
    }

    public static class ConvertM
    {
        static public double mToFeet(double x)
        {
            return x * 3.28084;
        }
        static public double mmToFeet(double x)
        {
            return x / 304.8;
        }
        static public double feetTomm(double x)
        {
            return x / 0.00328084;
        }
        static public double cmToFeet(double x)
        {
            return x / 30.48;
        }
        static public double feetToCm(double x)
        {
            return x / 0.0328084;
        }
        static public double feetToM(double x)
        {
            return x / 3.28084;
        }
        static public double degreesToRadians(double x)
        {
            return (x * (Math.PI / 180));
        }
        static public double radiansToDegrees(double x)
        {
            return (x * (180 / Math.PI));
        }

        //static public double RoundDoubleNumber(double targetNumber, int targetAmount)
        //{
        //    targetNumber = Math.Round(targetNumber);
        //    string stringNumber = targetNumber.ToString();

        //    int NumberToRound = int.Parse(stringNumber.Last().ToString());
        //    int NumberToIncrease = int.Parse(stringNumber.ElementAt(stringNumber.Count() - 2).ToString()) * 10;

        //    int getPreNumbers = 0;

        //    int i1 = stringNumber.Count();
        //    int i2 = i1 - 2;
        //    int.TryParse(stringNumber.Substring(0, i2), out getPreNumbers);

        //    double modResult = (NumberToRound % targetAmount);

        //    if (modResult == 0)
        //    {
        //        return targetNumber;
        //    }
        //    else
        //    {
        //        if (getPreNumbers != 0)
        //        {
        //            string NumberToReturnString = getPreNumbers.ToString() + (NumberToIncrease + targetAmount).ToString();
        //            return int.Parse(NumberToReturnString);
        //        }
        //        else
        //        {
        //            return NumberToIncrease + targetAmount;
        //        }
        //    }
        //}

        static public double RoundDoubleNumber(double targetNumber, int targetAmount, int maxNumber = 150)
        {
            double numberToReturn = Math.Round(targetNumber / targetAmount) * targetAmount;
            if (numberToReturn > maxNumber)
                return maxNumber;
            return numberToReturn;
        }

    }

    public static class GetPoint
    {
        public static XYZ getMidPoint(XYZ point1, XYZ point2)
        {
            double X1 = point1.X;
            double Y1 = point1.Y;
            double Z1 = point1.Z;

            double X2 = point2.X;
            double Y2 = point2.Y;
            double Z2 = point2.Z;


            XYZ midpoint = new XYZ(((X1 + X2) / 2), ((Y1 + Y2) / 2), ((Z1 + Z2) / 2));

            return midpoint;
        }

        public static bool PointsUpwards(XYZ targetVector)
        {
            double horizontalLength = targetVector.X * targetVector.X + targetVector.Y * targetVector.Y;
            double verticalLength = targetVector.Z * targetVector.Z;

            return targetVector.Z > 0 && verticalLength / horizontalLength > 0.3;
        }
    }

    public class GetDwgGeometryInformation
    {
        internal static IList<XYZ> GetDwgGeometryMidPoints(DwgGeometryType targetGeometryType, ImportInstance targetImportedInstance, string targetLayerName)
        {
            GeometryElement geoElem = targetImportedInstance.get_Geometry(new Options() { DetailLevel = ViewDetailLevel.Coarse, ComputeReferences = false, IncludeNonVisibleObjects = true });
            IList<XYZ> midPoints = new List<XYZ>();
            foreach (GeometryObject geoObj in geoElem)
            {
                if (geoObj is GeometryInstance)
                {
                    GeometryInstance geoInstance = geoObj as GeometryInstance;
                    GeometryElement geo = geoInstance.GetInstanceGeometry();

                    foreach (var currentGeo in geo)
                    {
                        #region if Poly Line
                        if ((currentGeo is PolyLine) && (targetGeometryType == DwgGeometryType.Rectangle))
                        {
                            PolyLine currentPolyLine = currentGeo as PolyLine;
                            int numCoordinates = currentPolyLine.NumberOfCoordinates;

                            if (numCoordinates == 5)
                            {

                                if (targetLayerName != null)
                                {
                                    if ((((targetImportedInstance.Document.GetElement(currentPolyLine.GraphicsStyleId) as GraphicsStyle).GraphicsStyleCategory).Name) != targetLayerName)
                                    {
                                        continue;
                                    }
                                }

                                IList<XYZ> coordinates = currentPolyLine.GetCoordinates();

                                IList<XYZ> first3Points = new List<XYZ>();
                                for (int i = 0; i < 3; i++)
                                {
                                    first3Points.Add(coordinates.ElementAt(i));
                                }

                                double maxDist = double.NegativeInfinity;
                                XYZ point1 = new XYZ();
                                XYZ point2 = new XYZ();

                                foreach (XYZ currentPoint1 in first3Points)
                                {
                                    foreach (XYZ currentPoint2 in first3Points)
                                    {
                                        double currentDist = currentPoint1.DistanceTo(currentPoint2);
                                        if (currentDist > maxDist)
                                        {
                                            maxDist = currentDist;
                                            point1 = currentPoint1;
                                            point2 = currentPoint2;
                                        }
                                    }
                                }

                                XYZ currentMidPoint = Utils.GetPoint.getMidPoint(point1, point2);
                                //We need to check if there`s point already near this one (or in the same place)
                                //If so, we cant create a duplicate point or a point almost equal to another
                                bool theresPointNear = false;
                                foreach (XYZ currentPoint in midPoints)
                                {
                                    if (currentMidPoint.DistanceTo(currentPoint) < 0.3)
                                    {
                                        theresPointNear = true;
                                    }
                                }
                                if (theresPointNear == false)
                                    midPoints.Add(currentMidPoint);
                            }
                        }
                        #endregion

                        #region if Arc - Circle
                        if ((currentGeo is Arc) && (targetGeometryType == DwgGeometryType.Circle))
                        {
                            Arc currentArc = currentGeo as Arc;

                            if ((currentArc.IsCyclic == false) || (currentArc.IsBound == true))
                                continue;

                            if (targetLayerName != null)
                            {
                                if ((((targetImportedInstance.Document.GetElement(currentArc.GraphicsStyleId) as GraphicsStyle).GraphicsStyleCategory).Name) != targetLayerName)
                                    continue;
                            }

                            XYZ currentMidPoint = currentArc.Center;
                            //We need to check if there`s point already near this one (or in the same place)
                            //If so, we cant create a duplicate point or a point almost equal to another
                            bool theresPointNear = false;
                            foreach (XYZ currentPoint in midPoints)
                            {
                                if (currentMidPoint.DistanceTo(currentPoint) < 0.3)
                                {
                                    theresPointNear = true;
                                }
                            }
                            if (theresPointNear == false)
                                midPoints.Add(currentMidPoint);
                        }


                    }
                    #endregion
                }
            }
            return midPoints;
        }
    }

    static internal class CheckFamilyInstanceForIntersection
    {
        static internal void checkForDuplicates(FamilyInstance targetBeam, Document doc, bool deleteOnlyShorterBeam = false)
        {
            //We will check if theres another beam here:
            IList<ElementId> listOfElementsThatWillBeDeleted = new List<ElementId>();
            Options op = new Options() { ComputeReferences = false, IncludeNonVisibleObjects = true, DetailLevel = ViewDetailLevel.Fine };
            GeometryElement beamGeometryElement = targetBeam.get_Geometry(op);

            BoundingBoxXYZ beamBoundingBox = beamGeometryElement.GetBoundingBox();

            Outline beam3dVolume = new Outline(beamBoundingBox.Min, beamBoundingBox.Max);

            BoundingBoxIntersectsFilter Intersect = new BoundingBoxIntersectsFilter(beam3dVolume, Utils.ConvertM.cmToFeet(-3));
            IList<Element> beamThatIntersects = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WherePasses(Intersect).ToList();

            if (beamThatIntersects.Count > 1)
            {
                Curve targetBeamCurve = (targetBeam.Location as LocationCurve).Curve;
                Line targetBeamBeamLine = targetBeamCurve as Line;
                //double newUpperBeamCurveLength = newUpperBeamCurve.ApproximateLength;
                if (targetBeamBeamLine != null)
                {
                    XYZ targetBeamCurveDirection = targetBeamBeamLine.Direction;
                    foreach (Element beamElement in beamThatIntersects)
                    {
                        Curve beamElementLocationCurve = (beamElement.Location as LocationCurve).Curve;
                        Line beamElementLocationLine = beamElementLocationCurve as Line;

                        if (beamElementLocationLine == null)
                            continue;

                        XYZ beamElementLocationCurveDirection = beamElementLocationLine.Direction;
                        //double beamElementLocationCurveLength = beamElementLocationCurve.ApproximateLength;
                        double angleBetweenBeams = beamElementLocationCurveDirection.AngleTo(targetBeamCurveDirection);
                        //compare the angles
                        if ((angleBetweenBeams < 0.1) || (Math.Abs(angleBetweenBeams - Math.PI) < 0.1))
                        {
                            SetComparisonResult stRes = targetBeamBeamLine.Intersect(beamElementLocationLine);
                            if (stRes != SetComparisonResult.Disjoint)
                            {
                                if (beamElement.Id != targetBeam.Id)
                                {
                                    if (deleteOnlyShorterBeam)
                                    {
                                        if (targetBeamBeamLine.ApproximateLength >= beamElementLocationLine.ApproximateLength)
                                            listOfElementsThatWillBeDeleted.Add(beamElement.Id);
                                        else
                                            listOfElementsThatWillBeDeleted.Add(targetBeam.Id);
                                    }
                                    else
                                    {
                                        listOfElementsThatWillBeDeleted.Add(beamElement.Id);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    //if is a curved beam
                    foreach (Element beamElement in beamThatIntersects)
                    {
                        Curve beamElementLocationCurve = (beamElement.Location as LocationCurve).Curve;
                        if (((beamElementLocationCurve as Line) == null) && (targetBeamBeamLine == null))
                        {
                            //Here we need to check the length and some other things to compare better
                            //We need to implement a better code here

                            if (beamElement.Id != targetBeam.Id)
                                listOfElementsThatWillBeDeleted.Add(beamElement.Id);

                        }
                    }
                }
                doc.Delete(listOfElementsThatWillBeDeleted);
            }
        }

        static internal void checkForDuplicatesWithGroups(FamilyInstance targetBeam, Document doc, bool includeGroups, bool deleteOnlyShorterBeam = false)
        {
            //We will check if theres another beam here:
            IList<ElementId> listOfElementsThatWillBeDeleted = new List<ElementId>();
            Options op = new Options() { ComputeReferences = false, IncludeNonVisibleObjects = true, DetailLevel = ViewDetailLevel.Fine };
            GeometryElement beamGeometryElement = targetBeam.get_Geometry(op);

            BoundingBoxXYZ beamBoundingBox = beamGeometryElement.GetBoundingBox();

            Outline beam3dVolume = new Outline(beamBoundingBox.Min, beamBoundingBox.Max);

            BoundingBoxIntersectsFilter Intersect = new BoundingBoxIntersectsFilter(beam3dVolume, Utils.ConvertM.cmToFeet(-3));
            IList<Element> beamThatIntersects = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WherePasses(Intersect).ToList();

            if (beamThatIntersects.Count > 1)
            {
                Curve targetBeamCurve = (targetBeam.Location as LocationCurve).Curve;
                Line targetBeamBeamLine = targetBeamCurve as Line;
                //double newUpperBeamCurveLength = newUpperBeamCurve.ApproximateLength;
                if (targetBeamBeamLine != null)
                {
                    XYZ targetBeamCurveDirection = targetBeamBeamLine.Direction;
                    foreach (Element beamElement in beamThatIntersects)
                    {
                        Curve beamElementLocationCurve = (beamElement.Location as LocationCurve).Curve;
                        Line beamElementLocationLine = beamElementLocationCurve as Line;

                        if (beamElementLocationLine == null)
                            continue;

                        XYZ beamElementLocationCurveDirection = beamElementLocationLine.Direction;
                        //double beamElementLocationCurveLength = beamElementLocationCurve.ApproximateLength;
                        double angleBetweenBeams = beamElementLocationCurveDirection.AngleTo(targetBeamCurveDirection);
                        //compare the angles
                        if ((angleBetweenBeams < 0.1) || (Math.Abs(angleBetweenBeams - Math.PI) < 0.1))
                        {
                            SetComparisonResult stRes = targetBeamBeamLine.Intersect(beamElementLocationLine);
                            if (stRes != SetComparisonResult.Disjoint)
                            {
                                if (beamElement.Id != targetBeam.Id)
                                {
                                    if (includeGroups)
                                    {
                                        if (!beamElement.GroupId.Equals(ElementId.InvalidElementId))
                                        {
                                            //listOfElementsThatWillBeDeleted.Add(beamElement.GroupId);
                                            Group BeamsGroup = doc.GetElement(beamElement.GroupId) as Group;
                                            IList<ElementId> allBeamsInGroup = BeamsGroup.GetMemberIds();
                                            foreach (ElementId currentBeamID in allBeamsInGroup)
                                            {
                                                listOfElementsThatWillBeDeleted.Add(currentBeamID);
                                            }
                                            BeamsGroup.UngroupMembers();
                                            continue;
                                        }
                                    }

                                    if (!listOfElementsThatWillBeDeleted.Contains(beamElement.GroupId))
                                    {
                                        if (deleteOnlyShorterBeam)
                                        {
                                            if (targetBeamBeamLine.ApproximateLength >= beamElementLocationLine.ApproximateLength)
                                                listOfElementsThatWillBeDeleted.Add(beamElement.Id);
                                            else
                                                listOfElementsThatWillBeDeleted.Add(targetBeam.Id);
                                        }
                                        else
                                        {
                                            listOfElementsThatWillBeDeleted.Add(beamElement.Id);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    //if is a curved beam
                    foreach (Element beamElement in beamThatIntersects)
                    {
                        Curve beamElementLocationCurve = (beamElement.Location as LocationCurve).Curve;
                        if (((beamElementLocationCurve as Line) == null) && (targetBeamBeamLine == null))
                        {
                            //Here we need to check the length and some other things to compare better
                            //We need to implement a better code here
                            if (includeGroups)
                            {
                                if (!beamElement.GroupId.Equals(ElementId.InvalidElementId))
                                {
                                    Group BeamsGroup = doc.GetElement(beamElement.GroupId) as Group;
                                    IList<ElementId> allBeamsInGroup = BeamsGroup.GetMemberIds();
                                    foreach (ElementId currentBeamID in allBeamsInGroup)
                                    {
                                        listOfElementsThatWillBeDeleted.Add(currentBeamID);
                                    }
                                    BeamsGroup.UngroupMembers();
                                    continue;
                                }
                            }

                            if (beamElement.Id != targetBeam.Id)
                                listOfElementsThatWillBeDeleted.Add(beamElement.Id);

                        }
                    }
                }
                doc.Delete(listOfElementsThatWillBeDeleted);
            }
        }

        static internal void checkForGroupsDuplicates(IList<ElementId> listOfMembers, Document doc)
        {
            IList<ElementId> listOfElementsThatWillBeDeleted = new List<ElementId>();
            IList<Element> beamThatIntersects = new List<Element>();

            Outline beam3dVolume = null;

            foreach (ElementId currentBeamID in listOfMembers)
            {
                Element currentBeam = doc.GetElement(currentBeamID);

                if (currentBeam == null)
                    continue;

                if (beam3dVolume == null)
                {
                    BoundingBoxXYZ beamBB = currentBeam.get_BoundingBox(null);
                    beamBB.Enabled = true;
                    beam3dVolume = new Outline(beamBB.Min, beamBB.Max);
                }
                else
                {
                    BoundingBoxXYZ beamBB = currentBeam.get_BoundingBox(null);
                    beamBB.Enabled = true;
                    beam3dVolume.AddPoint(beamBB.Min);
                    beam3dVolume.AddPoint(beamBB.Max);
                }
            }

            if (beam3dVolume == null)
                return;

            BoundingBoxIntersectsFilter Intersect = new BoundingBoxIntersectsFilter(beam3dVolume, Utils.ConvertM.cmToFeet(-3));
            beamThatIntersects = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WherePasses(Intersect).ToList();


            foreach (Element currentIntersectingBeam in beamThatIntersects)
            {

                if (!listOfMembers.Contains(currentIntersectingBeam.Id))
                {
                    if (!currentIntersectingBeam.GroupId.Equals(ElementId.InvalidElementId))
                    {
                        Group BeamsGroup = doc.GetElement(currentIntersectingBeam.GroupId) as Group;
                        IList<ElementId> allBeamsInGroup = BeamsGroup.GetMemberIds();
                        foreach (ElementId currentBeamID in allBeamsInGroup)
                        {
                            listOfElementsThatWillBeDeleted.Add(currentBeamID);
                        }
                        BeamsGroup.UngroupMembers();
                        continue;
                    }

                    if (!listOfElementsThatWillBeDeleted.Contains(currentIntersectingBeam.Id))
                    {
                        listOfElementsThatWillBeDeleted.Add(currentIntersectingBeam.Id);
                    }
                }
            }

            doc.Delete(listOfElementsThatWillBeDeleted);
            doc.Regenerate();
        }

        static internal double checkMaxDistanceBetweenSupports(FamilyInstance targetBeam, Document doc)
        {
            ElementIntersectsElementFilter intesectFilter = new ElementIntersectsElementFilter(targetBeam);

            IList<Element> intersectingColumns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType().WherePasses(intesectFilter).ToList();

            //XYZ pointOfInterest = new XYZ(-99999, 99999, -99999);
            XYZ pointOfInterest = (targetBeam.Location as LocationCurve).Curve.GetEndPoint(0);

            intersectingColumns = intersectingColumns.Where(c => (c is FamilyInstance)).OrderBy(c =>
                {
                    FamilyInstance fi = c as FamilyInstance;
                    if (!fi.IsSlantedColumn)
                    {
                        return (fi.Location as LocationPoint).Point.DistanceTo(pointOfInterest);
                    }
                    else
                    {
                        return (fi.Location as LocationCurve).Curve.Distance(pointOfInterest);
                    }
                }).ToList();

            //IList<double> distances = new List<double>();
            double maxDistBetweenSupport = double.NegativeInfinity;
            for (int i = 0; i < (intersectingColumns.Count - 1); i++)
            {
                XYZ firstPoint = IntersectPointBetweenBeamAndColumn(targetBeam, intersectingColumns[i]);
                XYZ secondPoint = IntersectPointBetweenBeamAndColumn(targetBeam, intersectingColumns[i + 1]);

                double currentDistance = firstPoint.DistanceTo(secondPoint);

                if (currentDistance > maxDistBetweenSupport)
                {
                    maxDistBetweenSupport = currentDistance;
                }
            }
            return maxDistBetweenSupport;
        }

        static internal bool checkMaxDistanceBetweenSupports(FamilyInstance targetBeam, Document doc, out double maxDistBetweenSupport)
        {
            ElementIntersectsElementFilter intesectFilter = new ElementIntersectsElementFilter(targetBeam);

            IList<Element> intersectingColumns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType().WherePasses(intesectFilter).ToList();

            //XYZ pointOfInterest = new XYZ(-99999, 99999, -99999);
            XYZ pointOfInterest = (targetBeam.Location as LocationCurve).Curve.GetEndPoint(0);

            intersectingColumns = intersectingColumns.Where(c => (c is FamilyInstance)).OrderBy(c =>
            {
                FamilyInstance fi = c as FamilyInstance;
                if (!fi.IsSlantedColumn)
                {
                    return (fi.Location as LocationPoint).Point.DistanceTo(pointOfInterest);
                }
                else
                {
                    return (fi.Location as LocationCurve).Curve.Distance(pointOfInterest);
                }
            }).ToList();

            maxDistBetweenSupport = double.NegativeInfinity;
            if (intersectingColumns.Count > 0)
            {
                for (int i = 0; i < (intersectingColumns.Count - 1); i++)
                {
                    XYZ firstPoint = IntersectPointBetweenBeamAndColumn(targetBeam, intersectingColumns[i]);
                    XYZ secondPoint = IntersectPointBetweenBeamAndColumn(targetBeam, intersectingColumns[i + 1]);

                    double currentDistance = firstPoint.DistanceTo(secondPoint);

                    if (currentDistance > maxDistBetweenSupport)
                    {
                        maxDistBetweenSupport = currentDistance;
                    }
                }
                return true; 
            }

            return false;
        }

        static internal XYZ IntersectPointBetweenBeamAndColumn(Element targetBeam, Element targetColumn)
        {
            FamilyInstance beamInstance = targetBeam as FamilyInstance;
            FamilyInstance columnInstance = targetColumn as FamilyInstance;

            // The beam and the column could have more than one solid modeled on the family
            // We will querry both and search for the solid that has more volume (main solid)
            Solid mainBeamSolid = GetMainSolid(beamInstance);
            Solid mainColumnSolid = GetMainSolid(columnInstance);

            Solid intersectingSolid = null;
            XYZ intersectingPoint = null;

            intersectingSolid = BooleanOperationsUtils.ExecuteBooleanOperation(mainBeamSolid, mainColumnSolid, BooleanOperationsType.Intersect);

            if (intersectingSolid != null)
            {
                intersectingPoint = intersectingSolid.ComputeCentroid();
            }

            return intersectingPoint;

        }

        static internal Solid GetMainSolid(FamilyInstance targetFamilyInstance)
        {
            GeometryElement columGeometryElem = targetFamilyInstance.get_Geometry(new Options());
            Solid mainSolid = null;

            foreach (GeometryObject currentObj in columGeometryElem)
            {
                Solid currentSolid = null;

                if (currentObj is Solid)
                {
                    currentSolid = currentObj as Solid;
                }
                else if (currentObj is GeometryInstance)
                {
                    GeometryInstance currentInstance = currentObj as GeometryInstance;
                    GeometryElement currentInstElement = currentInstance.GetInstanceGeometry();
                    foreach (GeometryObject currentInstObj in currentInstElement)
                    {
                        if (currentInstObj is Solid)
                        {
                            currentSolid = currentInstObj as Solid;
                        }
                    }
                }

                if (mainSolid == null)
                {
                    mainSolid = currentSolid;
                }
                else if (currentSolid != null)
                {
                    if (currentSolid.Volume > mainSolid.Volume)
                    {
                        mainSolid = currentSolid;
                    }
                }
            }

            return mainSolid;

        }

        static internal void JoinBeamToWalls(FamilyInstance targetBeam, Document doc)
        {
            ElementIntersectsElementFilter elementFilter = new ElementIntersectsElementFilter(targetBeam);

            IList<Element> WallsIntersectingBeam = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall)).WhereElementIsNotElementType().WherePasses(elementFilter).ToList();

            foreach (Element currentWall in WallsIntersectingBeam)
            {
                try
                {
                    JoinGeometryUtils.JoinGeometry(doc, targetBeam, currentWall);
                }
                catch
                {
                }

            }
        }

        static internal void CheckForDuplicatesAndIntersectingBeams(FamilyInstance targetBeam, Document doc)
        {
            //We will check if theres another beam here:
            IList<ElementId> listOfElementsThatWillBeDeleted = new List<ElementId>();
            Options op = new Options() { ComputeReferences = false, IncludeNonVisibleObjects = false, DetailLevel = ViewDetailLevel.Coarse };
            GeometryElement beamGeometryElement = targetBeam.get_Geometry(op);

            BoundingBoxXYZ beamBoundingBox = beamGeometryElement.GetBoundingBox();

            Outline beam3dVolume = new Outline(beamBoundingBox.Min, beamBoundingBox.Max);

            BoundingBoxIntersectsFilter Intersect = new BoundingBoxIntersectsFilter(beam3dVolume, Utils.ConvertM.cmToFeet(-3));
            ElementIntersectsElementFilter IntersectElemen = new ElementIntersectsElementFilter(targetBeam);
            IList<Element> beamThatIntersects = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WherePasses(Intersect).WherePasses(IntersectElemen).ToList();

            if (beamThatIntersects.Count > 0)
            {
                Curve targetBeamCurve = (targetBeam.Location as LocationCurve).Curve;
                Line targetBeamBeamLine = targetBeamCurve as Line;
                //double newUpperBeamCurveLength = newUpperBeamCurve.ApproximateLength;
                if (targetBeamBeamLine != null)
                {
                    XYZ targetBeamCurveDirection = targetBeamBeamLine.Direction;
                    foreach (Element beamElement in beamThatIntersects)
                    {
                        Curve beamElementLocationCurve = (beamElement.Location as LocationCurve).Curve;
                        Line beamElementLocationLine = beamElementLocationCurve as Line;

                        if (beamElementLocationLine == null)
                            continue;

                        XYZ beamElementLocationCurveDirection = beamElementLocationLine.Direction;
                        //double beamElementLocationCurveLength = beamElementLocationCurve.ApproximateLength;
                        double angleBetweenBeams = beamElementLocationCurveDirection.AngleTo(targetBeamCurveDirection);
                        //compare the angles
                        if ((angleBetweenBeams < 0.1) || (Math.Abs(angleBetweenBeams - Math.PI) < 0.1))
                        {
                            if (beamElement.Id != targetBeam.Id)
                            {
                                if (targetBeamBeamLine.ApproximateLength >= beamElementLocationLine.ApproximateLength)
                                    listOfElementsThatWillBeDeleted.Add(beamElement.Id);
                                else
                                    listOfElementsThatWillBeDeleted.Add(targetBeam.Id);
                            }
                        }
                    }
                }
                else
                {
                    //if is a curved beam
                    foreach (Element beamElement in beamThatIntersects)
                    {
                        Curve beamElementLocationCurve = (beamElement.Location as LocationCurve).Curve;
                        if (((beamElementLocationCurve as Line) == null) && (targetBeamBeamLine == null))
                        {
                            //Here we need to check the length and some other things to compare better
                            //We need to implement a better code here

                            if (beamElement.Id != targetBeam.Id)
                                listOfElementsThatWillBeDeleted.Add(beamElement.Id);

                        }
                    }
                }
                doc.Delete(listOfElementsThatWillBeDeleted);
            }
        }

    }

    static internal class GetInformation
    {
        static internal IList<LevelInfo> GetAllLevelsInfo(UIDocument targetUidoc)
        {
            IList<LevelInfo> levelInformation = new List<LevelInfo>();

            IList<Element> allLevels = new FilteredElementCollector(targetUidoc.Document).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level)).WhereElementIsNotElementType().OrderBy(e => (e as Level).Elevation).ToList();

            foreach (Element currentElement in allLevels)
            {
                Level currentLevel = currentElement as Level;
                string currentLevelName = currentLevel.Name;
                int currentLevelId = currentLevel.Id.IntegerValue;
                LevelInfo currentLevelInformation = new LevelInfo();
                currentLevelInformation.levelName = currentLevelName;
                currentLevelInformation.levelId = currentLevelId;
                currentLevelInformation.levelPrefix = "";
                currentLevelInformation.willBeNumbered = true;
                levelInformation.Add(currentLevelInformation);
            }

            return levelInformation;
        }

        static internal IList<LevelInfo> GetAllLevelsInfo(Document targetDoc)
        {
            IList<LevelInfo> levelInformation = new List<LevelInfo>();
            IList<Element> allLevels = new FilteredElementCollector(targetDoc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level)).WhereElementIsNotElementType().Where(e => (e as Level) != null).OrderBy(e => (e as Level).Elevation).ToList();

            foreach (Element currentElement in allLevels)
            {
                Level currentLevel = currentElement as Level;
                string currentLevelName = currentLevel.Name;
                int currentLevelId = currentLevel.Id.IntegerValue;
                LevelInfo currentLevelInformation = new LevelInfo();
                currentLevelInformation.levelName = currentLevelName;
                currentLevelInformation.levelId = currentLevelId;
                currentLevelInformation.levelPrefix = "";
                currentLevelInformation.willBeNumbered = true;
                levelInformation.Add(currentLevelInformation);
            }

            return levelInformation;
        }

        static internal IList<RevitLinksInfo> GetAllRevitInstances(Document targetDoc)
        {
            IList<RevitLinkInstance> allLinkedDocuments = new FilteredElementCollector(targetDoc).OfCategory(BuiltInCategory.OST_RvtLinks).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
            IList<RevitLinksInfo> allLinkedDocumentsInfo = new List<RevitLinksInfo>();

            foreach (RevitLinkInstance currentLink in allLinkedDocuments)
            {
                if (currentLink.GetLinkDocument() == null)
                    continue;
                RevitLinksInfo currentLinkInfo = new RevitLinksInfo(currentLink);
                allLinkedDocumentsInfo.Add(currentLinkInfo);
            }

            return allLinkedDocumentsInfo;

        }

        static internal Level GetCorrespondingLevelInthisDoc(Document targetDoc, Level targetLevel, double targetLinkedDocHeight)
        {
            if (targetLevel != null)
            {
                IList<Level> allLevelinThisDoc = GetAllLevels(targetDoc);
                double tolerance = Utils.ConvertM.cmToFeet(1);
                double elevation = targetLinkedDocHeight + targetLevel.ProjectElevation;
                Level correspondingLevelinThisDoc = null;

                foreach (Level currentLevel in allLevelinThisDoc)
                {
                    if (Math.Abs(currentLevel.ProjectElevation - elevation) <= tolerance)
                    {
                        correspondingLevelinThisDoc = currentLevel;
                    }
                }

                if (correspondingLevelinThisDoc == null)
                {
                    correspondingLevelinThisDoc = Level.Create(targetDoc, elevation);
                }

                return correspondingLevelinThisDoc;
            }

            return null;
        }

        static internal IList<Level> GetAllLevels(Document targetDoc)
        {
            return new FilteredElementCollector(targetDoc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level)).WhereElementIsNotElementType().Cast<Level>().ToList();
        }

        static internal IList<FamilyWithImage> GetAllBeamFamilies(Document targetDoc)
        {

            IList<Family> allColumnFamilies = new FilteredElementCollector(targetDoc).OfClass(typeof(Family)).Cast<Family>().Where(f => f.FamilyCategoryId == new ElementId(BuiltInCategory.OST_StructuralFraming)).ToList();
            //Test if there is a parameter called b and other called h in the family

            IList<Family> allColumnFamiliesFilt = allColumnFamilies.Where(f =>
            {
                FamilySymbol fSymbol = targetDoc.GetElement(f.GetFamilySymbolIds().First()) as FamilySymbol;
                if ((fSymbol.LookupParameter("b") != null) && (fSymbol.LookupParameter("h") != null))
                {
                    if ((fSymbol.LookupParameter("b").StorageType == StorageType.Double) && (fSymbol.LookupParameter("h").StorageType == StorageType.Double))
                        return true;
                }
                return false;
            }).ToList();

            IList<FamilyWithImage> allFamilyWithImage = new List<FamilyWithImage>();

            foreach (Family currentElem in allColumnFamiliesFilt)
            {
                string currentFamilyName = (currentElem as Family).Name;
                int currentFamilyID = currentElem.Id.IntegerValue;
                System.Drawing.Bitmap currentFirstTypeBitmap = (targetDoc.GetElement(((currentElem as Family)
                    .GetFamilySymbolIds()).First()) as FamilySymbol).GetPreviewImage(new System.Drawing.Size(60, 60));

                var hBitmap = currentFirstTypeBitmap.GetHbitmap();

                var imgSource = System.Windows.Interop.Imaging.
                    CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                allFamilyWithImage.Add(new FamilyWithImage() { FamilyName = currentFamilyName, Image = imgSource, FamilyID = currentFamilyID });
            }

            return allFamilyWithImage;
        }

        static internal int GetLastElementNumbered(BuiltInCategory targetCategory, Document targetDocument)
        {
            IList<Element> AllInstancesOfCategory = new FilteredElementCollector(targetDocument).OfCategory(targetCategory).OfClass(typeof(FamilyInstance)).ToList();

            if (AllInstancesOfCategory.Count < 1)
                return 0;

            int lastNumber = 0;

            foreach (Element currentElement in AllInstancesOfCategory)
            {
                Parameter currentParamenter = currentElement.get_Parameter(BuiltInParameter.DOOR_NUMBER);

                if (!currentParamenter.HasValue)
                    return 0;

                string tempString = currentParamenter.AsString();
                int tempInt = GetNumberFromString.GetTheFirstIntFromString(tempString, false);

                if (tempInt > lastNumber)
                {
                    lastNumber = tempInt;
                }
            }

            return lastNumber;
            
        }
    }

    enum DwgGeometryType { Rectangle, Circle, Point }

    static internal class GetNumberFromString
    {
        static internal int GetTheFirstIntFromString(string targetString, bool isLoopingBackWards)
        {
            int lastIntPos = 0;
            int lastLetterPos = 0;
            bool foundAInt = false;

            //if the string is empty or null we dont need to check it
            if (targetString == "" || targetString == null)
                return 0;

            if (isLoopingBackWards)
            {
                //If the order is backwards and the string starts with a integer
                //a error will be generated, so we artificially add a letter at the
                //begining of the string to avoid that error
                int testInt = 0;
                if (int.TryParse(targetString[0].ToString(), out testInt))
                {
                    targetString = "a" + targetString;
                }

                lastLetterPos = targetString.Length - 1;
                for (int i = targetString.Length - 1; i >= 0; i--)
                {
                    int tempInt = 0;

                    if (int.TryParse(targetString[i].ToString(), out tempInt))
                    {
                        if (!foundAInt)
                        {
                            lastIntPos = i;
                            foundAInt = true;
                        }
                    }
                    else
                    {
                        lastLetterPos = i + 1;
                        if (foundAInt)
                            break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < targetString.Length; i++)
                {
                    int tempInt = 0;

                    if (int.TryParse(targetString[i].ToString(), out tempInt))
                    {
                        lastIntPos = i;
                        foundAInt = true;
                    }
                    else
                    {
                        if (foundAInt)
                            break;
                        lastLetterPos = i + 1;
                    }
                }

                if (!foundAInt)
                    return 0;
            }

            int resolvedInt = 0;

            //if the lastLetterPos is greater than the length of the string, we havent found any integer on the string
            if (lastLetterPos <= targetString.Length)
            {
                string returnedString = targetString.Substring(lastLetterPos, lastIntPos - lastLetterPos + 1);
                int.TryParse(returnedString, out resolvedInt);
            }

            return resolvedInt;
        }
    }
}
