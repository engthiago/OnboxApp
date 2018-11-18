using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace ONBOXAppl
{
    enum BeamFromWallHeightMode { Fixed, DistHeightPerc, HighestOpening }
    enum BeamFromWallWidthMode { WallWidth, Fixed }

    class LinkedWallsSelectionFilter : ISelectionFilter
    {
        UIDocument uidoc = null;

        public LinkedWallsSelectionFilter(UIDocument targetUIDocument)
        {
            uidoc = targetUIDocument;
        }

        public bool AllowElement(Element elem)
        {
            return true;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            RevitLinkInstance linkedInst = uidoc.Document.GetElement(reference) as RevitLinkInstance;
            Document linkedDoc = linkedInst.GetLinkDocument();
            Element currentElement = linkedDoc.GetElement(reference.CreateReferenceInLink());

            if (currentElement != null)
            {
                if (currentElement is Wall)
                {
                    return true;
                }
            }

            return false;
        }
    }
    class WallsSelectionFilter : ISelectionFilter
    {

        public bool AllowElement(Element elem)
        {
            if (elem is Wall)
                return true;
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }

    }

    [Transaction(TransactionMode.Manual)]
    class BeamsFromWalls : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                ONBOXAppl.ONBOXApplication.onboxApp.uiApp = commandData.Application;
                ONBOXAppl.ONBOXApplication.onboxApp.ShowBeamsFromWallsUI();
            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }

    public class RequestBeamsFromWallsHandler : IExternalEventHandler
    {
        UIDocument uidoc = null;
        Document doc = null;
        Selection sel = null;
        BeamsFromWallsUI currentUI = null;

        public void Execute(UIApplication app)
        {
            uidoc = app.ActiveUIDocument;
            doc = uidoc.Document;
            sel = uidoc.Selection;
            currentUI = ONBOXApplication.onboxApp.beamsFromWallsWindow;

            //BeamsFromColumnsWindow.beamFromColumnsCurrentOperation
            if (app != null)
            {
                switch (currentUI.beamFromWallsCurrentOperation)
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

        private void CreateBeams()
        {
            Element selectedElement = null;
            try
            {
                while (true)
                {
                    RevitLinkInstance linkedInst = null;
                    //We initially create and set a variable currentDoc to be the document that will be using to getParameters later
                    //If the we are working with a linked document, this document will be changing for the corresponding document
                    Document currentDoc = doc;

                    View currentView = ONBOXApplication.onboxApp.uiApp.ActiveUIDocument.ActiveView;
                    View currentGraphicalView = ONBOXApplication.onboxApp.uiApp.ActiveUIDocument.ActiveGraphicalView;

                    if (currentView.Id != currentGraphicalView.Id)
                    {
                        TaskDialog.Show(Properties.Messages.Common_Error, Properties.Messages.BeamsFromWalls_NotGraphicalView);
                        return;
                    }

                    #region MinMaxDimentions
                    double BeamMinHeight = Utils.ConvertM.cmToFeet(20);
                    double BeamMaxHeight = Utils.ConvertM.cmToFeet(150);
                    double BeamMinWidth = Utils.ConvertM.cmToFeet(10);
                    double BeamMaxWidth = Utils.ConvertM.cmToFeet(50);
                    #endregion

                    if (currentUI.UseLinks)
                    {
                        Reference linkedRef = sel.PickObject(ObjectType.LinkedElement, new LinkedWallsSelectionFilter(uidoc), Properties.Messages.BeamsFromWalls_SelectWallFromLink);
                        linkedInst = doc.GetElement(linkedRef) as RevitLinkInstance;
                        //change the current document to be the linked one
                        currentDoc = linkedInst.GetLinkDocument();
                        selectedElement = currentDoc.GetElement(linkedRef.CreateReferenceInLink().ElementId);
                    }
                    else
                        selectedElement = doc.GetElement(sel.PickObject(ObjectType.Element, new WallsSelectionFilter(), Properties.Messages.BeamsFromWalls_SelectArchitecturalWall));


                    Wall selectedWall = selectedElement as Wall;
                    if (selectedWall == null)
                    {
                        continue;
                    }

                    using (Transaction beamTransation = new Transaction(doc, Properties.Messages.BeamsFromWalls_Transaction))
                    {
                        beamTransation.Start();

                        #region WallParameters

                        //Levels
                        Level wallBottomLevel = currentDoc.GetElement(selectedWall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId()) as Level;
                        Level wallTopLevel = currentDoc.GetElement(selectedWall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId()) as Level;

                        //try to find the corresponding levels in this doc if exists
                        if (currentUI.UseLinks)
                        {
                            double linkedDocumentHeight = linkedInst.GetTotalTransform().Origin.Z;

                            wallBottomLevel = Utils.GetInformation.GetCorrespondingLevelInthisDoc(doc, wallBottomLevel, linkedDocumentHeight);
                            wallTopLevel = Utils.GetInformation.GetCorrespondingLevelInthisDoc(doc, wallTopLevel, linkedDocumentHeight);

                        }

                        //Offsets
                        double wallTopOffset = selectedWall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).AsDouble();
                        double wallBottomOffset = selectedWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();

                        //Unconnected Height
                        double wallUncHeight = selectedWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();

                        //Height and Width
                        double beamHeight = Utils.ConvertM.cmToFeet(60);
                        double beamWidth = Utils.ConvertM.cmToFeet(14);

                        DefineBeamParameters(selectedWall, currentDoc, wallBottomLevel, wallUncHeight, ref  beamWidth, ref beamHeight);

                        //Curve
                        Curve wallCurve = (selectedWall.Location as LocationCurve).Curve;
                        if (currentUI.UseLinks)
                            wallCurve = wallCurve.CreateTransformed(linkedInst.GetTotalTransform());

                        #endregion

                        Family currentFamily = doc.GetElement(new ElementId(currentUI.SelectedBeamFamilyID)) as Family;

                        if (currentFamily == null)
                        {
                            TaskDialog.Show(Properties.Messages.Common_Error, Properties.Messages.BeamsFromWalls_DeletedBeamFamily);
                            return;
                        }

                        FamilySymbol currentFamilySymbol = null;

                        currentFamilySymbol = doc.GetElement(currentFamily.GetFamilySymbolIds().First()) as FamilySymbol;

                        //Verify if the dimentions is bettween min an max
                        if (beamHeight < BeamMinHeight)
                            beamHeight = BeamMinHeight;
                        if (beamHeight > BeamMaxHeight)
                            beamHeight = BeamMaxHeight;

                        if (beamWidth < BeamMinWidth)
                            beamWidth = BeamMinWidth;
                        if (beamWidth > BeamMaxWidth)
                            beamWidth = BeamMaxWidth;

                        //Round the dimensions in cm
                        const double roundNumber = 5;

                        double beamHeightInCM = Math.Ceiling((Utils.ConvertM.feetToCm(beamHeight) / roundNumber)) * roundNumber;
                        double beamWidthInCM = Math.Ceiling((Utils.ConvertM.feetToCm(beamWidth) / roundNumber)) * roundNumber;

                        beamHeight = Utils.ConvertM.cmToFeet((beamHeightInCM));
                        beamWidth = Utils.ConvertM.cmToFeet((beamWidthInCM));

                        //Build the Name of the Future FamilySymbol and checks if we have another with the same name, if we have another one use that instead
                        string newTypeName = beamWidthInCM.ToString() + " x " + beamHeightInCM.ToString() + "cm";

                        if (!Utils.FindElements.thisTypeExist(newTypeName, currentFamily.Name, BuiltInCategory.OST_StructuralFraming, doc))
                        {
                            currentFamilySymbol = currentFamilySymbol.Duplicate(newTypeName) as FamilySymbol;

                            Parameter parameterB = currentFamilySymbol.LookupParameter("b");
                            Parameter parameterH = currentFamilySymbol.LookupParameter("h");

                            parameterB.Set(beamWidth);
                            parameterH.Set(beamHeight);

                        }
                        else
                        {
                            currentFamilySymbol = Utils.FindElements.findElement(newTypeName, currentFamily.Name, BuiltInCategory.OST_StructuralFraming, doc) as FamilySymbol;
                        }

                        //Create the Beams
                        FamilyInstance UpperBeamInstance = null;
                        FamilyInstance LowerBeamInstance = null;
                        currentFamilySymbol.Activate();

                        if (currentUI.UpperBeam)
                        {
                            bool isInvertedUpperBeam = false;

                            if (currentUI.InvertedUpperBeam)
                                isInvertedUpperBeam = true;

                            UpperBeamInstance = doc.Create.NewFamilyInstance(wallCurve, currentFamilySymbol, wallBottomLevel, Autodesk.Revit.DB.Structure.StructuralType.Beam);

                            //Regenerate the document so we can adjust the parameters
                            doc.Regenerate();
                            AdjustBeamParameters(UpperBeamInstance, wallTopLevel, wallBottomLevel, wallUncHeight, isInvertedUpperBeam);
                        }
                        if (currentUI.LowerBeam)
                        {
                            LowerBeamInstance = doc.Create.NewFamilyInstance(wallCurve, currentFamilySymbol, wallBottomLevel, Autodesk.Revit.DB.Structure.StructuralType.Beam);
                            doc.Regenerate();
                            AdjustBeamParameters(LowerBeamInstance, wallTopLevel, wallBottomLevel, wallUncHeight, false, true);
                        }

                        if (UpperBeamInstance != null)
                        {
                            CheckBeamsDistancesWithouSupports(UpperBeamInstance, currentFamily, currentFamilySymbol, roundNumber, beamWidth);
                            doc.Regenerate();
                            Utils.CheckFamilyInstanceForIntersection.checkForDuplicates(UpperBeamInstance, doc);
                            doc.Regenerate();
                            if (currentUI.JoinWall)
                                //we dont need to check if its linked because the UI blocks join walls when is linked
                                Utils.CheckFamilyInstanceForIntersection.JoinBeamToWalls(UpperBeamInstance, doc);
                        }
                        if (LowerBeamInstance != null)
                        {
                            Utils.CheckFamilyInstanceForIntersection.checkForDuplicates(LowerBeamInstance, doc);
                            if (currentUI.JoinWall)
                                //we dont need to check if its linked because the UI blocks join walls when is linked
                                Utils.CheckFamilyInstanceForIntersection.JoinBeamToWalls(LowerBeamInstance, doc);
                        }


                        //continue to code
                        //////////////////

                        beamTransation.Commit();
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

        private void CheckBeamsDistancesWithouSupports(FamilyInstance targetBeam, Family targetbeamFamily, FamilySymbol targetFamilySymbol, double roundNumber, double beamWidth)
        {
            if (currentUI.BeamHeightMode == BeamFromWallHeightMode.DistHeightPerc)
            {
                //check the maximum distance between supports
                //this block of code will correct the beam height so it will only consider the maximum distance beetween the supports
                //not the entire distance
                double maxDist = double.NegativeInfinity;
                if (Utils.CheckFamilyInstanceForIntersection.checkMaxDistanceBetweenSupports(targetBeam, doc, out maxDist))
                {
                    double tempHeigth = Utils.ConvertM.feetToCm(maxDist) / currentUI.BeamHeightInfo;
                    double correctedHeight = Math.Round(tempHeigth / roundNumber) * roundNumber;
                    if (correctedHeight < 40)
                        correctedHeight = 40;
                    correctedHeight = Utils.ConvertM.cmToFeet(correctedHeight);
                    string newCorrectedTypeName = Math.Round(Utils.ConvertM.feetToCm(beamWidth)).ToString() + " x " + Math.Round(Utils.ConvertM.feetToCm(correctedHeight)).ToString() + "cm";
                    FamilySymbol newCorrectedBeamTypeSymbol = null;

                    if (Utils.FindElements.CheckTypeForDuplicate(newCorrectedTypeName, targetbeamFamily, doc))
                    {
                        newCorrectedBeamTypeSymbol = Utils.FindElements.GetFamilySymbol(newCorrectedTypeName, targetbeamFamily, doc);
                    }
                    else
                    {
                        newCorrectedBeamTypeSymbol = targetFamilySymbol.Duplicate(newCorrectedTypeName) as FamilySymbol;
                        newCorrectedBeamTypeSymbol.LookupParameter("b").Set(beamWidth);
                        newCorrectedBeamTypeSymbol.LookupParameter("h").Set(correctedHeight);
                    }

                    targetBeam.Symbol = newCorrectedBeamTypeSymbol; 
                }
            }
        }

        private void AdjustBeamParameters(FamilyInstance targetBeamInstance, Level topLevel, Level bottomLevel, double wallHeight, bool isInvertedUpperBeam = false, bool isLowerBeam = false)
        {
            Parameter newBeamParamRefLevel = targetBeamInstance.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            Parameter newBeamParamFistPoint = targetBeamInstance.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION);
            Parameter newBeamParamSecondPoint = targetBeamInstance.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION);
            Parameter newBeamParamZJust = targetBeamInstance.get_Parameter(BuiltInParameter.Z_JUSTIFICATION);
            Parameter newBeamParamStructuralUsage = targetBeamInstance.get_Parameter(BuiltInParameter.INSTANCE_STRUCT_USAGE_PARAM);

            newBeamParamFistPoint.Set(1);
            newBeamParamSecondPoint.Set(1);

            if (!isLowerBeam)
            {
                if (topLevel != null)
                {
                    newBeamParamRefLevel.Set(topLevel.Id);
                    newBeamParamFistPoint.Set(0);
                    newBeamParamSecondPoint.Set(0);
                }
                else
                {
                    newBeamParamFistPoint.Set(wallHeight);
                    newBeamParamSecondPoint.Set(wallHeight);
                }

                if (isInvertedUpperBeam)
                    newBeamParamZJust.Set(3);
            }
            if (isLowerBeam)
            {
                newBeamParamFistPoint.Set(0);
                newBeamParamSecondPoint.Set(0);
            }

            doc.Regenerate();

        }

        private void DefineBeamParameters(Wall targetWall, Document targetDocument, Level targetWallBottomLevel, double targetWallHeight, ref double targetWidth, ref double targetHeight)
        {
            switch (currentUI.BeamWidthMode)
            {
                case BeamFromWallWidthMode.WallWidth:
                    targetWidth = targetWall.Width + Utils.ConvertM.cmToFeet(currentUI.BeamWidthInfo);
                    break;
                case BeamFromWallWidthMode.Fixed:
                    targetWidth = Utils.ConvertM.cmToFeet(currentUI.BeamWidthInfo);
                    break;
                default:
                    break;
            }

            switch (currentUI.BeamHeightMode)
            {
                case BeamFromWallHeightMode.Fixed:
                    targetHeight = Utils.ConvertM.cmToFeet(currentUI.BeamHeightInfo);
                    break;
                case BeamFromWallHeightMode.DistHeightPerc:
                    double wallLength = (targetWall.Location as LocationCurve).Curve.ApproximateLength;
                    targetHeight = wallLength / currentUI.BeamHeightInfo;
                    break;
                case BeamFromWallHeightMode.HighestOpening:
                    targetHeight = FindHighestOpening(targetWall, targetDocument, targetWallBottomLevel, targetWallHeight);
                    break;
                default:
                    break;
            }
        }

        private double FindHighestOpening(Wall targetWall, Document targetDocument, Level targetWallBottomLevel, double targetWallHeight)
        {
            double tempMinHeight = double.PositiveInfinity;
            IList<ElementId> ElementsHostedWallIDs = targetWall.FindInserts(true, true, true, true);

            foreach (ElementId eID in ElementsHostedWallIDs)
            {
                Element currentElement = targetDocument.GetElement(eID);

                if (currentElement is Opening)
                {
                    Opening thisOpening = targetDocument.GetElement(eID) as Opening;
                    Parameter wallOffset = thisOpening.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET);
                    //if the height is positive it means that the opening if above the wall itself
                    //so it will not interest us
                    double thisHeight = wallOffset.AsDouble() * (-1);
                    if ((thisHeight > Utils.ConvertM.cmToFeet(30)) && (thisHeight < tempMinHeight))
                    {
                        tempMinHeight = thisHeight;
                    }
                }
                else
                {
                    if (currentElement is FamilyInstance)
                    {
                        FamilyInstance thisDoorOrWindow = targetDocument.GetElement(eID) as FamilyInstance;
                        Parameter baseHead = thisDoorOrWindow.get_Parameter(BuiltInParameter.INSTANCE_HEAD_HEIGHT_PARAM);
                        double thisHeight = targetWallHeight - baseHead.AsDouble();
                        if ((thisHeight > Utils.ConvertM.cmToFeet(30)) && (thisHeight < tempMinHeight))
                        {
                            tempMinHeight = thisHeight;
                        }
                    }
                }
            }

            //return Math.Round(tempMinHeight, 2);
            return tempMinHeight;
        }

        private void Unsubscribe()
        {

            ONBOXApplication.onboxApp.externalBeamsFromWallsEvent.Dispose();
            ONBOXApplication.onboxApp.externalBeamsFromWallsEvent = null;
            ONBOXApplication.onboxApp.requestBeamsFromWallsHandler = null;

            ONBOXApplication.onboxApp.uiApp.ViewActivated -= ONBOXApplication.onboxApp.BeamsFromWalls_ViewActivated;

        }

        private void Reload()
        {
            IList<FamilyWithImage> beamFamilies = GetAllBeamFamilies();
            ONBOXApplication.onboxApp.beamsFromWallsWindow.PopulateComboFamily(beamFamilies);
        }

        public string GetName()
        {
            return "ONBOX : Beams from walls request handler";
        }

        private IList<FamilyWithImage> GetAllBeamFamilies()
        {

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
