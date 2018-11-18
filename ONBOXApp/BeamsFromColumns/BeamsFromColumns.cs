using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;

namespace ONBOXAppl
{
    class ColumnSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category.Id.ToString() == (BuiltInCategory.OST_StructuralColumns).GetHashCode().ToString())
                return true;
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }


    class ColumnAndBeamSelectionFilter : ISelectionFilter
    {
        FamilyInstance currentBeam = null;

        public ColumnAndBeamSelectionFilter(FamilyInstance targetBeam)
        {
            currentBeam = targetBeam;
        }

        public bool AllowElement(Element elem)
        {
            if (elem.Category.Id.IntegerValue == BuiltInCategory.OST_StructuralColumns.GetHashCode())
            {
                return true;
            }
            else if (elem.Id == currentBeam.Id)
            {
                return true;
            }

            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    public class RequestBeamsFromColumnsHandler : IExternalEventHandler
    {
        UIDocument uidoc = null;
        Document doc = null;
        Selection sel = null;
        BeamsFromColumnsUI currentUI = null;
        const double roundNumber = 5;

        public void Execute(UIApplication app)
        {

            if (app != null)
            {
                uidoc = app.ActiveUIDocument;
                doc = uidoc.Document;
                sel = uidoc.Selection;

                switch (ONBOXAppl.ONBOXApplication.onboxApp.beamsFromColumnsWindow.beamFromColumnsCurrentOperation)
                {
                    case ExternalOperation.Create:
                        CreateBeams();
                        break;
                    case ExternalOperation.Reload:
                        Reload();
                        break;
                    case ExternalOperation.Unsubscribe:
                        Unsubscribe();
                        break;
                    case ExternalOperation.LoadFamily:
                        LoadFamily();
                        break;
                    default:
                        break;
                }
            }

        }

        private void LoadFamily()
        {
            try
            {
                RevitCommandId loadFamilyCommand = RevitCommandId.LookupPostableCommandId(PostableCommand.LoadShapes);
                if (uidoc.Application.CanPostCommand(loadFamilyCommand))
                {
                    uidoc.Application.PostCommand(loadFamilyCommand);
                }
            }
            catch
            {
            }
        }

        public string GetName()
        {
            return "ONBOX : Beams from columns request handler";
        }

        private void CreateBeams()
        {

            FamilyInstance firstColumn = null;
            FamilyInstance secondColumn = null;
            FamilyInstance createdBeam = null;
            FamilyInstance lastSelectedBeam = null;
            FamilyInstance lastSelectedColumn = null;

            //TODO if is not 3d view, ask user to go to a 3d view

            try
            {
                while (true)
                {
                    //Create a reference copy of the Window so we can refer to it easily on the code
                    currentUI = ONBOXAppl.ONBOXApplication.onboxApp.beamsFromColumnsWindow;

                    View currentView = ONBOXApplication.onboxApp.uiApp.ActiveUIDocument.ActiveView;
                    View currentGraphicalView = ONBOXApplication.onboxApp.uiApp.ActiveUIDocument.ActiveGraphicalView;

                    if (currentView.Id != currentGraphicalView.Id)
                    {
                        TaskDialog.Show(Properties.Messages.Common_Error, Properties.Messages.BeamsFromColumns_NotGraphicalView);
                        return;
                    }

                    if (currentView as View3D == null)
                    {
                        TaskDialog.Show(Properties.Messages.Common_Error, Properties.Messages.BeamsFromColumns_Not3dView);
                        return;
                    }

                    if (createdBeam != null)
                    {
                        FamilyInstance currentSelection = doc.GetElement(sel.PickObject(ObjectType.Element, new ColumnAndBeamSelectionFilter(createdBeam), Properties.Messages.BeamsFromColumns_SelectFirstColumnOrBeam)) as FamilyInstance;

                        if (currentSelection.Category.Id.IntegerValue == BuiltInCategory.OST_StructuralColumns.GetHashCode())
                        {
                            if (currentUI.IsChain() == false || lastSelectedColumn == null)
                            {
                                firstColumn = doc.GetElement(currentSelection.Id) as FamilyInstance;
                                secondColumn = doc.GetElement(sel.PickObject(ObjectType.Element, new ColumnSelectionFilter(), Properties.Messages.BeamsFromColumns_SelectSecondColumn)) as FamilyInstance;
                                createdBeam = CreateBeam(ref firstColumn, ref secondColumn);
                                lastSelectedColumn = doc.GetElement(secondColumn.Id) as FamilyInstance;
                            }
                            else
                            {
                                firstColumn = doc.GetElement(lastSelectedColumn.Id) as FamilyInstance;
                                secondColumn = doc.GetElement(currentSelection.Id) as FamilyInstance;
                                createdBeam = CreateBeam(ref firstColumn, ref secondColumn);
                                lastSelectedColumn = doc.GetElement(secondColumn.Id) as FamilyInstance;
                            }
                        }else
                        {
                            lastSelectedBeam = AdjustBeam(currentSelection, lastSelectedBeam);
                        }
                    }
                    else
                    {
                        if (currentUI.IsChain() == false || lastSelectedColumn == null)
                        {
                            firstColumn = doc.GetElement(sel.PickObject(ObjectType.Element, new ColumnSelectionFilter(), Properties.Messages.BeamsFromColumns_SelectFirstColumn)) as FamilyInstance;
                            secondColumn = doc.GetElement(sel.PickObject(ObjectType.Element, new ColumnSelectionFilter(), Properties.Messages.BeamsFromColumns_SelectSecondColumn)) as FamilyInstance;
                            createdBeam = CreateBeam(ref firstColumn, ref secondColumn);
                            lastSelectedColumn = doc.GetElement(secondColumn.Id) as FamilyInstance; 
                        }else
                        {
                            firstColumn = doc.GetElement(lastSelectedColumn.Id) as FamilyInstance;
                            secondColumn = doc.GetElement(sel.PickObject(ObjectType.Element, new ColumnSelectionFilter(), Properties.Messages.BeamsFromColumns_SelectSecondColumn)) as FamilyInstance;
                            createdBeam = CreateBeam(ref firstColumn, ref secondColumn);
                            lastSelectedColumn = doc.GetElement(secondColumn.Id) as FamilyInstance;
                        }
                    }
                }
            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
            }
            finally
            {
                currentUI.UnFreezeCreateButton();
            }

        }

        private FamilyInstance AdjustBeam(FamilyInstance currentSelection, FamilyInstance lastSelectedBeam)
        {
            FamilyInstance adjustedBeam;
            FamilyInstance targetColumn = doc.GetElement(sel.PickObject(ObjectType.Element, new ColumnSelectionFilter(), Properties.Messages.BeamsFromColumns_SelectColumnToOffset)) as FamilyInstance;

            Line currentBeamLine = ((currentSelection.Location as LocationCurve).Curve as Line);

            if (currentBeamLine == null)
                return null;

            XYZ crossDirection = XYZ.BasisZ.CrossProduct(currentBeamLine.Direction).Normalize();
            double x1 = CenterToBorder(crossDirection, currentSelection);
            double x2 = CenterToBorder(crossDirection, targetColumn);

            double x3 = Math.Abs(x2 - x1);

            using (Transaction t = new Transaction(doc, Properties.Messages.BeamsFromColumns_Transaction_AdjustOffset))
            {
                t.Start();
                currentSelection.get_Parameter(BuiltInParameter.YZ_JUSTIFICATION).Set(0);

                if (lastSelectedBeam != null && currentSelection.Id == lastSelectedBeam.Id)
                {
                    x3 = x3 * (-1);
                    adjustedBeam = null;
                }
                else
                {
                    adjustedBeam = currentSelection;
                }

                currentSelection.get_Parameter(BuiltInParameter.Y_OFFSET_VALUE).Set(x3);
                t.Commit();
            }
            return adjustedBeam;
        }

        private FamilyInstance CreateBeam(ref FamilyInstance firstColumn, ref FamilyInstance secondColumn)
        {
            FamilyInstance createdBeam = null;

            //the UI will implement those variables in the future
            bool isFirstColumnTop = true;
            bool isSecondColumnTop = true;

            ElementId firstColumnLevelID = null;
            XYZ firstColumnPoint = null;
            double firstColumnOffset;

            ElementId secondColumnLevelID = null;
            XYZ secondColumnPoint = null;
            double secondColumnOffset;

            if (isFirstColumnTop == true)
                GetTopLevelPointAndOffset(firstColumn, out firstColumnLevelID, out firstColumnPoint, out firstColumnOffset);
            else
                GetBaseLevelPointAndOffset(firstColumn, out firstColumnLevelID, out firstColumnPoint, out firstColumnOffset);

            if (isSecondColumnTop == true)
                GetTopLevelPointAndOffset(secondColumn, out secondColumnLevelID, out secondColumnPoint, out secondColumnOffset);
            else
                GetBaseLevelPointAndOffset(secondColumn, out secondColumnLevelID, out secondColumnPoint, out secondColumnOffset);


            if ((firstColumnLevelID != null) && (firstColumnPoint != null) && (secondColumnLevelID != null) && (secondColumnPoint != null))
            {
                //Since the structural columns can have different levels and height we have to choose one level to be the main level (the level that the beam will be placed)
                //After that the offset for the second column can become wrong because it took in consideration the level that the second column was
                //So we have to add the difference bettewen the second level height and the first level height from the offset
                //If the first level is above the second the difference will subtract (because its a negative number) if not it will add (beacause is a positive number)
                //If both column was in the same level the difference will be 0, so if thats the case, nothing will change

                double firstColumnLevelHeight = (doc.GetElement(firstColumnLevelID) as Level).Elevation;
                double secondColumnLevelHeight = (doc.GetElement(secondColumnLevelID) as Level).Elevation;

                double difference = secondColumnLevelHeight - firstColumnLevelHeight;

                secondColumnOffset = secondColumnOffset + difference;

                //If the distance from one point to another (not considering the z position) is less than 10cm will will skip this iteration of the while loop
                //we create new points so we can 0 the z value to compare, if one column is on top of another this aproach will consider than next to each other
                XYZ firstPointToCheck = new XYZ(firstColumnPoint.X, firstColumnPoint.Y, 0);
                XYZ secondPointToCheck = new XYZ(secondColumnPoint.X, secondColumnPoint.Y, 0);

                if (firstPointToCheck.DistanceTo(secondPointToCheck) < Utils.ConvertM.cmToFeet(10))
                    return null;

                //if the distance is more that 10cm we will create a line and continue on with the code
                Line BeamLine = Line.CreateBound(firstColumnPoint, secondColumnPoint);

                //here we can have 2 problems
                //1 - since we are inside a try block, theres no need to check if the window is instanciated, if not a execption will be throwned and catched
                //2 - if the family doesnt exist the user probably deleted it and then tried to use it (he doesnt press the reload button also)
                FamilySymbol newBeamTypeSymbol = null;
                FamilySymbol BeamTypeFromFamilySymbol = null;

                IList<FamilySymbol> ListBeamTypesFromFamilySymbol = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .WhereElementIsElementType().Cast<FamilySymbol>().Where(t => t.Family.Id.IntegerValue == currentUI.selectedBeamFamilyID).ToList();

                if (ListBeamTypesFromFamilySymbol.Count > 0)
                    BeamTypeFromFamilySymbol = ListBeamTypesFromFamilySymbol.FirstOrDefault();
                else
                {
                    TaskDialog.Show(Properties.Messages.Common_Error, Properties.Messages.BeamsFromColumns_DeletedBeamFamily);
                    return null;
                }

                //Duplicate the type and add the options of the UI
                double beamWidth = 14;
                double beamHeigth = 60;

                if (currentUI.GetBeamWidthMode() == 0)
                {
                    Parameter bP = firstColumn.Symbol.LookupParameter("b");
                    Parameter hP = firstColumn.Symbol.LookupParameter("h");
                    double b = 0;
                    double h = 0;
                    double firstColumnWidth = 0;

                    if (bP == null)
                    {
                        firstColumnWidth = hP.AsDouble();
                    }
                    else if (hP == null)
                    {
                        firstColumnWidth = bP.AsDouble();
                    }
                    else
                    {
                        b = firstColumn.Symbol.LookupParameter("b").AsDouble();
                        h = firstColumn.Symbol.LookupParameter("h").AsDouble();
                        firstColumnWidth = b < h ? b : h;
                    }

                    beamWidth = firstColumnWidth;
                }
                else if (currentUI.GetBeamWidthMode() == 1)
                {
                    Parameter bP = firstColumn.Symbol.LookupParameter("b");
                    Parameter hP = firstColumn.Symbol.LookupParameter("h");
                    double b = 0;
                    double h = 0;
                    double secondColumnWidth = 0;

                    if (bP == null)
                    {
                        secondColumnWidth = hP.AsDouble();
                    }
                    else if (hP == null)
                    {
                        secondColumnWidth = bP.AsDouble();
                    }
                    else
                    {
                        b = firstColumn.Symbol.LookupParameter("b").AsDouble();
                        h = firstColumn.Symbol.LookupParameter("h").AsDouble();
                        secondColumnWidth = b < h ? b : h;
                    }

                    beamWidth = secondColumnWidth;
                }
                else
                {
                    beamWidth = Utils.ConvertM.cmToFeet(currentUI.GetTxtBoxBeamWidthText());
                }

                if (currentUI.GetBeamHeigthMode() == 0)
                {
                    beamHeigth = Utils.ConvertM.cmToFeet(currentUI.GetTxtBoxBeamHeightText());
                }
                else if (currentUI.GetBeamHeigthMode() == 1)
                {
                    double tempHeigth = Utils.ConvertM.feetToCm(BeamLine.ApproximateLength) / currentUI.GetTxtBoxBeamHeightText();
                    tempHeigth = Math.Ceiling(tempHeigth / roundNumber) * roundNumber;
                    if (tempHeigth < 40)
                        tempHeigth = 40;
                    beamHeigth = Utils.ConvertM.cmToFeet(tempHeigth);
                }

                using (Transaction t = new Transaction(doc, Properties.Messages.BeamsFromColumns_Transaction))
                {
                    t.Start();

                    string newTypeName = Math.Round(Utils.ConvertM.feetToCm(beamWidth)).ToString() + " x " + Math.Round(Utils.ConvertM.feetToCm(beamHeigth)).ToString() + "cm";
                    Family targetFamily = doc.GetElement(new ElementId(currentUI.selectedBeamFamilyID)) as Family;

                    bool TypeAlreadyCreated = Utils.FindElements.CheckTypeForDuplicate(newTypeName, targetFamily, doc);

                    if (TypeAlreadyCreated)
                    {
                        newBeamTypeSymbol = Utils.FindElements.GetFamilySymbol(newTypeName, targetFamily, doc);
                    }
                    else
                    {
                        newBeamTypeSymbol = BeamTypeFromFamilySymbol.Duplicate(newTypeName) as FamilySymbol;
                        newBeamTypeSymbol.LookupParameter("b").Set(beamWidth);
                        newBeamTypeSymbol.LookupParameter("h").Set(beamHeigth);
                    }

                    newBeamTypeSymbol.Activate();
                    createdBeam = doc.Create.NewFamilyInstance(BeamLine, newBeamTypeSymbol, doc.GetElement(firstColumnLevelID) as Level, Autodesk.Revit.DB.Structure.StructuralType.Beam);

                    //Get the parameters
                    Parameter createdBeamLevelParam = createdBeam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                    Parameter createdBeamFirstPointOffset = createdBeam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION);
                    Parameter createdBeamSecondPointOffset = createdBeam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION);

                    //Reset the parameters
                    createdBeamFirstPointOffset.Set(1);
                    createdBeamSecondPointOffset.Set(1);
                    createdBeamLevelParam.Set(firstColumnLevelID);
                    createdBeamFirstPointOffset.Set(0);
                    createdBeamSecondPointOffset.Set(0);

                    //Set the parameters
                    createdBeamFirstPointOffset.Set(firstColumnOffset);
                    createdBeamSecondPointOffset.Set(secondColumnOffset);

                    doc.Regenerate();

                    if (currentUI.GetBeamHeigthMode() == 1)
                    {
                        //check the maximum distance between supports
                        //this block of code will correct the beam height so it will only consider the maximum distance beetween the supports
                        //not the entire distance
                        double maxDist = Utils.CheckFamilyInstanceForIntersection.checkMaxDistanceBetweenSupports(createdBeam, doc);
                        double tempHeigth = Utils.ConvertM.feetToCm(maxDist) / currentUI.GetTxtBoxBeamHeightText();
                        double correctedHeight = Math.Ceiling(tempHeigth / roundNumber) * roundNumber;
                        if (correctedHeight < 40)
                            correctedHeight = 40;
                        correctedHeight = Utils.ConvertM.cmToFeet(correctedHeight);
                        string newCorrectedTypeName = Math.Round(Utils.ConvertM.feetToCm(beamWidth)).ToString() + " x " + Math.Round(Utils.ConvertM.feetToCm(correctedHeight)).ToString() + "cm";
                        FamilySymbol newCorrectedBeamTypeSymbol = null;

                        if (Utils.FindElements.CheckTypeForDuplicate(newCorrectedTypeName, targetFamily, doc))
                        {
                            newCorrectedBeamTypeSymbol = Utils.FindElements.GetFamilySymbol(newCorrectedTypeName, targetFamily, doc);
                        }
                        else
                        {
                            newCorrectedBeamTypeSymbol = BeamTypeFromFamilySymbol.Duplicate(newCorrectedTypeName) as FamilySymbol;
                            newCorrectedBeamTypeSymbol.LookupParameter("b").Set(beamWidth);
                            newCorrectedBeamTypeSymbol.LookupParameter("h").Set(correctedHeight);
                        }

                        createdBeam.Symbol = newCorrectedBeamTypeSymbol;
                    }

                    Utils.CheckFamilyInstanceForIntersection.checkForDuplicates(createdBeam, doc);

                    t.Commit();
                }
            }
            return createdBeam;
        }

        private double CenterToBorder(XYZ targetDirection, FamilyInstance targetFamilyInstance)
        {
            ReferenceIntersector rayCaster = new ReferenceIntersector(targetFamilyInstance.Id, FindReferenceTarget.Face, doc.ActiveView as View3D);
            BoundingBoxXYZ familyBB = targetFamilyInstance.get_BoundingBox(null);

            Location familyLocation = targetFamilyInstance.Location;
            XYZ objectCenter = Utils.GetPoint.getMidPoint(familyBB.Min, familyBB.Max);

            if (familyLocation is LocationPoint)
            {
                LocationPoint familyPoint = targetFamilyInstance.Location as LocationPoint;
                objectCenter = new XYZ(familyPoint.Point.X, familyPoint.Point.Y, objectCenter.Z);
            }
            else
            {
                LocationCurve familyCurve = targetFamilyInstance.Location as LocationCurve;
                XYZ midPoint = Utils.GetPoint.getMidPoint(familyCurve.Curve.GetEndPoint(1), familyCurve.Curve.GetEndPoint(0));
                Line line = (familyCurve.Curve as Line);
                if (line == null)
                    return 0;
                XYZ direct = line.Direction;
                XYZ cross = XYZ.BasisZ.CrossProduct(direct);
                double offset = targetFamilyInstance.get_Parameter(BuiltInParameter.Y_OFFSET_VALUE).AsDouble();
                XYZ offsetFromMidle = cross.Multiply(offset);
                objectCenter = midPoint.Add(offsetFromMidle);
            }


            IList<ReferenceWithContext> hitFaces = rayCaster.Find(objectCenter, targetDirection);
            ReferenceWithContext refFace = null;

            if (hitFaces != null)
            {
                if (hitFaces.Count > 0)
                {
                    refFace = hitFaces.LastOrDefault();
                }
            }

            if (refFace != null)
            {
                //FamilySymbol pSafe = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)
                //    .WhereElementIsElementType().Where(t => t.Name == "DebugPointSafe").FirstOrDefault() as FamilySymbol;

                //using (Transaction ta = new Transaction(doc, "hey"))
                //{
                //    ta.Start();
                //    doc.Create.NewFamilyInstance(refFace.GetReference().GlobalPoint, pSafe, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                //    doc.Create.NewFamilyInstance(objectCenter, pSafe, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                //    ta.Commit();
                //}
                return refFace.GetReference().GlobalPoint.DistanceTo(objectCenter);
            }

            return 0;

        }

        private void Unsubscribe()
        {

            ONBOXApplication.onboxApp.externalBeamFromColumnsEvent.Dispose();
            ONBOXApplication.onboxApp.externalBeamFromColumnsEvent = null;
            ONBOXApplication.onboxApp.requestBeamsFromColumnsHandler = null;

            ONBOXApplication.onboxApp.uiApp.ViewActivated -= ONBOXApplication.onboxApp.BeamsFromColumns_ViewActivated;

        }

        private void Reload()
        {
            ONBOXApplication.storedBeamFamilesInfo = BeamsFromColumns.getAllBeamFamilies();
            ONBOXApplication.onboxApp.beamsFromColumnsWindow.PopulateFamiliesComboBox();
        }

        private void GetBaseLevelPointAndOffset(FamilyInstance targetColumn, out ElementId baseLevelID, out XYZ basePoint, out double baseOffset)
        {
            baseLevelID = targetColumn.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId();

            if (targetColumn.IsSlantedColumn == false)
            {
                basePoint = (targetColumn.Location as LocationPoint).Point;
            }
            else
            {
                Curve columnCurve = (targetColumn.Location as LocationCurve).Curve;
                XYZ columnFirstPoint = columnCurve.GetEndPoint(0);
                XYZ columnSecondPoint = columnCurve.GetEndPoint(1);

                if (columnFirstPoint.Z < columnSecondPoint.Z)
                    basePoint = columnFirstPoint;
                else
                    basePoint = columnSecondPoint;
            }

            baseOffset = targetColumn.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM).AsDouble();
        }

        private void GetTopLevelPointAndOffset(FamilyInstance targetColumn, out ElementId topLevelID, out XYZ topPoint, out double topOffset)
        {
            topLevelID = targetColumn.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId();

            if (targetColumn.IsSlantedColumn == false)
            {
                topPoint = (targetColumn.Location as LocationPoint).Point;
            }
            else
            {
                Curve columnCurve = (targetColumn.Location as LocationCurve).Curve;
                XYZ columnFirstPoint = columnCurve.GetEndPoint(0);
                XYZ columnSecondPoint = columnCurve.GetEndPoint(1);

                if (columnFirstPoint.Z > columnSecondPoint.Z)
                    topPoint = columnFirstPoint;
                else
                    topPoint = columnSecondPoint;
            }

            topOffset = targetColumn.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM).AsDouble();
        }

    }

    [Transaction(TransactionMode.Manual)]
    class BeamsFromColumns : IExternalCommand
    {
        static UIDocument uidoc = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                ONBOXApplication.onboxApp.uiApp = commandData.Application;
                ONBOXApplication.onboxApp.ShowBeamsFromColumnsUI();
            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        static internal IList<FamilyWithImage> getAllBeamFamilies()
        {
            uidoc = ONBOXApplication.onboxApp.uiApp.ActiveUIDocument;

            IList<Family> allColumnFamilies = new FilteredElementCollector(uidoc.Document).OfClass(typeof(Family)).Cast<Family>().Where(f => f.FamilyCategoryId == new ElementId(BuiltInCategory.OST_StructuralFraming)).ToList();
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
                System.Drawing.Bitmap currentFirstTypeBitmap = (uidoc.Document.GetElement(((currentElem as Family)
                    .GetFamilySymbolIds()).First()) as FamilySymbol).GetPreviewImage(new System.Drawing.Size(60, 60));

                var hBitmap = currentFirstTypeBitmap.GetHbitmap();

                var imgSource = System.Windows.Interop.Imaging.
                    CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                allFamilyWithImage.Add(new FamilyWithImage() { FamilyName = currentFamilyName, Image = imgSource, FamilyID = currentFamilyID });
            }

            return allFamilyWithImage;
        }

    }
}
