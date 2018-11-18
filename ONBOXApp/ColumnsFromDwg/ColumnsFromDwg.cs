using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI.Selection;
using System.Drawing;

namespace ONBOXAppl
{
    public class CADSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element is ImportInstance)
            {
                Element e = element.Document.GetElement(element.GetTypeId());
                if (e is CADLinkType)
                    return true;
            }
            return false;
        }

        public bool AllowReference(Reference refer, XYZ point)
        {
            return false;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class ColumnsFromDwg : IExternalCommand
    {

        class ColumnsTobeRotated
        {
            internal FamilyInstance currentInstance;
            internal double currentAngle;
        }

        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513363"));
        static UIDocument uidoc = null;

        double minLineLengthInCm = 14;
        double maxLineLengthInCm = 400;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            
            try
            {
                IList<FamilyWithImage> allColumnsType = getAllColumnFamilies();

                if (allColumnsType.Count == 0)
                {
                    message = Properties.Messages.BeamsFromCAD_NoColumnFamilyLoaded;
                    return Result.Failed;
                }

                IList<Element> allLevels = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level)).OrderBy(l => (l as Level).Elevation).ToList();
                Element selElem = doc.GetElement(sel.PickObject(ObjectType.Element, new CADSelectionFilter(), Properties.Messages.BeamsFromCAD_SelectCADInstance));
                ImportInstance importInstance = selElem as ImportInstance;
                Transform instanceTransform = importInstance.GetTotalTransform();
                GeometryElement geoElem = selElem.get_Geometry(new Options() { DetailLevel = ViewDetailLevel.Coarse, ComputeReferences = false, IncludeNonVisibleObjects = false });

                string selectedLayerItem = "";
                //Gather information the layers of the ImportedInstance
                IList<DwgLayerInfo> layerInfo = GetLayerInfo(geoElem);

                if (layerInfo.Count < 1)
                {
                    message = Properties.Messages.BeamsFromCAD_NoValidCADElements;
                    return Result.Failed;
                }

                ColumnsFromDwgUI columnsFromDwgWindow = new ColumnsFromDwgUI(layerInfo);

                if (columnsFromDwgWindow.ShowDialog() == false)
                {
                    return Result.Cancelled;
                }
                else
                    selectedLayerItem = columnsFromDwgWindow.GetSelectedLayerName();

                using (TransactionGroup tGroup = new TransactionGroup(doc, Properties.Messages.BeamsFromCAD_Transaction))
                {
                    tGroup.Start();
                    IList<ColumnsTobeRotated> fInnstances = new List<ColumnsTobeRotated>();
                    
                    CreateInstances(geoElem, doc, selectedLayerItem, allLevels, fInnstances);

                    using (Transaction tRotate = new Transaction(doc, Properties.Messages.BeamsFromCAD_RotateTransaction))
                    {
                        tRotate.Start();
                        foreach (ColumnsTobeRotated fi in fInnstances)
                        {
                            RotateInstance(doc, fi.currentInstance, fi.currentAngle);
                        }
                        tRotate.Commit();
                    }

                    tGroup.Assimilate();
                }

            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
                if (excep is OperationCanceledException)
                    return Result.Cancelled;
                else
                    return Result.Failed;
            }


            return Result.Succeeded; ;
        }


        private void CreateInstances(GeometryElement geoElem, Document doc, string selectedLayerItem, IList<Element> allLevels, IList<ColumnsTobeRotated> fInnstances)
        {
            double angle = 0;
            using (Transaction tCreate = new Transaction(doc, Properties.Messages.BeamsFromCAD_Transaction))
            {
                tCreate.Start();
                foreach (GeometryObject geoObj in geoElem)
                {
                    if (geoObj is GeometryInstance)
                    {

                        GeometryInstance geoInstance = geoObj as GeometryInstance;
                        GeometryElement geo = geoInstance.GetInstanceGeometry();

                        foreach (var currentGeo in geo)
                        {
                            #region if Poly Line
                            if (currentGeo is PolyLine)
                            {
                                PolyLine currentPolyLine = currentGeo as PolyLine;
                                int numCoordinates = currentPolyLine.NumberOfCoordinates;

                                if (numCoordinates == 5)
                                {
                                    IList<XYZ> coordinates = currentPolyLine.GetCoordinates();
                                    if ((((doc.GetElement(currentPolyLine.GraphicsStyleId) as GraphicsStyle).GraphicsStyleCategory).Name) != selectedLayerItem)
                                    {
                                        continue;
                                    }

                                    IList<XYZ> organizedPoints = new List<XYZ>();
                                    XYZ pointOfInterest = new XYZ(-999, 999, 0);

                                    Line line1 = Line.CreateBound(coordinates.ElementAt(0), coordinates.ElementAt(1));
                                    XYZ direction = line1.Direction;

                                    Line line2 = Line.CreateBound(coordinates.ElementAt(1), coordinates.ElementAt(2));

                                    if ((direction.X < 0) && (direction.Y < 0))
                                    {
                                        direction = new XYZ(direction.X * (-1), direction.Y, direction.Z);
                                        angle = direction.AngleTo(XYZ.BasisY) + (Math.PI / 2);
                                    }
                                    else if (direction.X < 0)
                                    {
                                        angle = direction.AngleTo(XYZ.BasisY) + (Math.PI / 2);
                                    }
                                    else if (direction.Y < 0)
                                    {
                                        if (line1.ApproximateLength < line2.ApproximateLength)
                                        {
                                            direction = new XYZ(direction.X, direction.Y * (-1), direction.Z);
                                            angle = direction.AngleTo(XYZ.BasisX) + (Math.PI / 2);
                                            if (direction.Y == 1)
                                                angle = angle + (Math.PI / 2);
                                        }
                                        else
                                        {
                                            direction = new XYZ(direction.X, direction.Y * (-1), direction.Z);
                                            angle = direction.AngleTo(XYZ.BasisY) + (Math.PI / 2);
                                        }

                                    }
                                    else if ((direction.X > 0) || (direction.Y > 0))
                                    {
                                        angle = direction.AngleTo(XYZ.BasisX);
                                    }

                                    double angleG = Utils.ConvertM.radiansToDegrees(angle);
                                    double line1Length = line1.ApproximateLength;
                                    double line1LengthCm = Math.Round(Utils.ConvertM.feetToCm(line1Length), 1);

                                    if (line1LengthCm < minLineLengthInCm || line1LengthCm > maxLineLengthInCm)
                                        continue;

                                    XYZ line2Direct = line2.Direction;
                                    double angle22 = Utils.ConvertM.radiansToDegrees(line2Direct.AngleTo(XYZ.BasisX));
                                    double line2Length = line2.ApproximateLength;
                                    double line2LengthCm = Math.Round(Utils.ConvertM.feetToCm(line2Length), 1);

                                    if (line2LengthCm < minLineLengthInCm || line2LengthCm > maxLineLengthInCm)
                                        continue;

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

                                    FamilyInstance fi = null;
                                    IList<Element> createdColumns = new List<Element>();

                                    for (int l = 0; l < ONBOXApplication.StoredColumnsDwgLevels.Count; l++)
                                    {
                                        if (ONBOXApplication.StoredColumnsDwgLevels.ElementAt(l).willBeNumbered == false)
                                            continue;

                                        XYZ midPoint = Utils.GetPoint.getMidPoint(point1, point2);
                                        FamilyWithImage familyInfo = ONBOXApplication.storedColumnFamiliesInfo.ElementAt(ONBOXApplication.selectedColumnFamily);
                                        FamilySymbol fs = uidoc.Document.GetElement((uidoc.Document.GetElement(new ElementId(familyInfo.FamilyID)) as Family).GetFamilySymbolIds().First()) as FamilySymbol;
                                        string newTypeName = (line1LengthCm).ToString() + " x " + (line2LengthCm).ToString() + "cm";
                                        ElementType newType = null;

                                        if (!Utils.FindElements.CheckTypeForDuplicate(newTypeName, fs.Family, doc))
                                        {

                                            newType = fs.Duplicate(newTypeName);
                                            newType.LookupParameter("b").Set(line1Length);
                                            newType.LookupParameter("h").Set(line2Length);
                                        }
                                        else
                                        {
                                            newType = Utils.FindElements.GetFamilySymbol(newTypeName, fs.Family, doc);
                                        }

                                        FamilySymbol fs2 = newType as FamilySymbol;
                                        fi = doc.Create.NewFamilyInstance(midPoint, fs2, (allLevels.First() as Level), Autodesk.Revit.DB.Structure.StructuralType.Column);

                                        ElementId topLevelID = new ElementId(ONBOXApplication.StoredColumnsDwgLevels.ElementAt(l + 1).levelId);
                                        ElementId baseLevelID = new ElementId(ONBOXApplication.StoredColumnsDwgLevels.ElementAt(l).levelId);

                                        fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).Set(baseLevelID);
                                        fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).Set(topLevelID);

                                        fInnstances.Add(new ColumnsTobeRotated { currentInstance = fi, currentAngle = angle});

                                    }
                                }
                            }
                            #endregion

                            #region if Arc - Circle
                            if (currentGeo is Arc)
                            {
                                Arc currentArc = currentGeo as Arc;

                                if ((currentArc.IsCyclic == false) || (currentArc.IsBound == true))
                                    continue;
                                if ((((doc.GetElement(currentArc.GraphicsStyleId) as GraphicsStyle).GraphicsStyleCategory).Name) != selectedLayerItem)
                                    continue;

                                XYZ currentArcLocation = currentArc.Center;
                                //currentArcLocation = dwgTransform.OfPoint(currentArcLocation);
                                double currentArcDiameter = currentArc.Radius * 2;
                                string currentArcDiameterCm = Math.Round(Utils.ConvertM.feetToCm(currentArcDiameter)).ToString();
                                //currentArcLocation = dwgTransform.OfPoint(currentArcLocation);

                                foreach (LevelInfo currentLevelInfo in ONBOXApplication.StoredColumnsDwgLevels)
                                {
                                    if (currentLevelInfo.willBeNumbered == false)
                                        continue;

                                    FamilyWithImage familyInfo = ONBOXApplication.storedColumnFamiliesCircInfo.ElementAt(ONBOXApplication.selectedColumnCircFamily);
                                    FamilySymbol fs = uidoc.Document.GetElement((uidoc.Document.GetElement(new ElementId(familyInfo.FamilyID)) as Family).GetFamilySymbolIds().First()) as FamilySymbol;
                                    string newTypeName = currentArcDiameterCm + "cm";
                                    ElementType newType = null;

                                    if (!Utils.FindElements.CheckTypeForDuplicate(newTypeName, fs.Family, doc))
                                    {
                                        newType = fs.Duplicate(newTypeName);
                                        newType.LookupParameter("b").Set(currentArcDiameter);
                                        if (newType.LookupParameter("h") != null)
                                            newType.LookupParameter("h").Set(currentArcDiameter);
                                    }
                                    else
                                    {
                                        newType = Utils.FindElements.GetFamilySymbol(newTypeName, fs.Family, doc);
                                    }

                                    FamilySymbol fs2 = newType as FamilySymbol;
                                    FamilyInstance fi = doc.Create.NewFamilyInstance(currentArcLocation, fs2, (allLevels.First() as Level), Autodesk.Revit.DB.Structure.StructuralType.Column);

                                    ElementId topLevelID = new ElementId((ONBOXApplication.StoredColumnsDwgLevels.ElementAt(ONBOXApplication.StoredColumnsDwgLevels.IndexOf(currentLevelInfo) + 1)).levelId);
                                    ElementId baseLevelID = new ElementId(currentLevelInfo.levelId);

                                    fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).Set(baseLevelID);
                                    fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).Set(topLevelID);

                                    fInnstances.Add(new ColumnsTobeRotated { currentInstance = fi, currentAngle = angle });

                                }

                            }
                            #endregion
                        }

                    }
                }
                tCreate.Commit();
            }
        }

        private bool IsOrthogonal(XYZ direction)
        {
            if ((direction.X >= 1.01) && (direction.X <= 1.01))
                return true;
            if ((direction.X >= -1.01) && (direction.X <= -1.01))
                return true;
            if ((direction.Y >= 1.01) && (direction.Y <= 1.01))
                return true;
            if ((direction.Y >= -1.01) && (direction.Y <= -1.01))
                return true;
            return false;
        }

        static internal IList<LevelInfo> GetAllLevelInfo()
        {
            return Utils.GetInformation.GetAllLevelsInfo(uidoc);
        }

        static internal IList<TypeWithImage> getAllColumnTypes()
        {
            IList<Element> allColumnTypes = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsElementType().ToList();
            IList<TypeWithImage> allTypesWithImage = new List<TypeWithImage>();

            foreach (Element currentElement in allColumnTypes)
            {
                string currentTypeName = (currentElement as FamilySymbol).Name;
                string currentFamilyName = (currentElement as FamilySymbol).FamilyName + ": ";
                Bitmap currentTypeImage = (currentElement as FamilySymbol).GetPreviewImage(new Size(50, 50));

                var hBitmap = currentTypeImage.GetHbitmap();
                var imgSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());


                allTypesWithImage.Add(new TypeWithImage() { FamilyName = currentFamilyName, TypeName = currentTypeName, Image = imgSource });
            }

            return allTypesWithImage;

        }

        static internal IList<FamilyWithImage> getAllColumnFamilies()
        {
            IList<Family> allColumnFamilies = new FilteredElementCollector(uidoc.Document).OfClass(typeof(Family)).Cast<Family>().Where(f => f.FamilyCategoryId == new ElementId(BuiltInCategory.OST_StructuralColumns)).ToList();
            //Test if there is a parameter called b and other called h in the family

            IList<Family> allColumnFamiliesFilt = allColumnFamilies.Where(f =>
            {
                FamilySymbol fSymbol = uidoc.Document.GetElement(f.GetFamilySymbolIds().First()) as FamilySymbol;
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
                Bitmap currentFirstTypeBitmap = (uidoc.Document.GetElement(((currentElem as Family)
                    .GetFamilySymbolIds()).First()) as FamilySymbol).GetPreviewImage(new Size(60, 60));

                var hBitmap = currentFirstTypeBitmap.GetHbitmap();

                var imgSource = System.Windows.Interop.Imaging.
                    CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                allFamilyWithImage.Add(new FamilyWithImage() { FamilyName = currentFamilyName, Image = imgSource, FamilyID = currentFamilyID });
            }

            return allFamilyWithImage;
        }

        static internal IList<FamilyWithImage> getAllColumnCircularFamilies()
        {
            IList<Family> allColumnFamilies = new FilteredElementCollector(uidoc.Document).OfClass(typeof(Family)).Cast<Family>().Where(f => f.FamilyCategoryId == new ElementId(BuiltInCategory.OST_StructuralColumns)).ToList();
            //Test if there is a parameter called b and this parameter is double

            IList<Family> allColumnFamiliesFilt = allColumnFamilies.Where(f =>
            {
                FamilySymbol fSymbol = uidoc.Document.GetElement(f.GetFamilySymbolIds().First()) as FamilySymbol;
                if ((fSymbol.LookupParameter("b") != null) && (fSymbol.LookupParameter("b").StorageType == StorageType.Double))
                {
                    return true;
                }
                return false;
            }).ToList();

            IList<FamilyWithImage> allFamilyWithImage = new List<FamilyWithImage>();

            foreach (Family currentElem in allColumnFamiliesFilt)
            {
                string currentFamilyName = (currentElem as Family).Name;
                int currentFamilyID = currentElem.Id.IntegerValue;
                Bitmap currentFirstTypeBitmap = (uidoc.Document.GetElement(((currentElem as Family)
                    .GetFamilySymbolIds()).First()) as FamilySymbol).GetPreviewImage(new Size(60, 60));

                var hBitmap = currentFirstTypeBitmap.GetHbitmap();

                var imgSource = System.Windows.Interop.Imaging.
                    CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                allFamilyWithImage.Add(new FamilyWithImage() { FamilyName = currentFamilyName, Image = imgSource, FamilyID = currentFamilyID });
            }

            return allFamilyWithImage;
        }

        private void RotateInstance(Document targetDoc, FamilyInstance targetInstance, double targetAngle)
        {
            XYZ fiLocation = (targetInstance.Location as LocationPoint).Point;
            Line rotationLine = Line.CreateUnbound(fiLocation, XYZ.BasisZ);
            ElementTransformUtils.RotateElement(targetDoc, targetInstance.Id, rotationLine, targetAngle);
        }

        private IList<DwgLayerInfo> GetLayerInfo(GeometryElement targetElem)
        {
            IList<DwgLayerInfo> dwgLayersInfo = new List<DwgLayerInfo>();
            IList<string> layerAlreadyAdded = new List<string>();

            foreach (GeometryObject geoObj in targetElem)
            {
                if (geoObj is GeometryInstance)
                {
                    GeometryInstance geoInstance = geoObj as GeometryInstance;
                    GeometryElement geoSymbol = geoInstance.GetInstanceGeometry();
                    foreach (GeometryObject currentGeo in geoSymbol)
                    {
                        if ((currentGeo is PolyLine) || (currentGeo is Arc))
                        {
                            //PolyLine currentPolyLine = currentGeo as PolyLine;
                            GraphicsStyle Layer = uidoc.Document.GetElement(currentGeo.GraphicsStyleId) as GraphicsStyle;
                            string layerName = Layer.GraphicsStyleCategory.Name;

                            if (!layerAlreadyAdded.Contains(layerName))
                            {
                                layerAlreadyAdded.Add(layerName);
                                byte redChan = Layer.GraphicsStyleCategory.LineColor.Red;
                                byte greenChan = Layer.GraphicsStyleCategory.LineColor.Green;
                                byte blueChan = Layer.GraphicsStyleCategory.LineColor.Blue;
                                System.Windows.Media.Color currentColor = new System.Windows.Media.Color() { R = redChan, G = greenChan, B = blueChan, A = ((byte)255) };
                                System.Windows.Media.SolidColorBrush currentColorBrush = new System.Windows.Media.SolidColorBrush(currentColor);

                                dwgLayersInfo.Add(new DwgLayerInfo() { Name = layerName, ColorBrush = currentColorBrush });
                            }
                        }
                    }
                }
            }
            return dwgLayersInfo;
        }
    }
}
