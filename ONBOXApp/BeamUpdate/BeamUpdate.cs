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
    class BeamSelectionFilter : ISelectionFilter
    {

        public bool AllowElement(Element elem)
        {
            if (elem.Category.Id.IntegerValue.ToString() == BuiltInCategory.OST_StructuralFraming.GetHashCode().ToString())
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

    class RequestBeamsUpdateHandler : IExternalEventHandler
    {
        UIDocument uidoc = null;
        Document doc = null;
        Selection sel = null;
        BeamsUpdateUI currentUI = null;

        public void Execute(UIApplication app)
        {
            uidoc = app.ActiveUIDocument;
            doc = uidoc.Document;
            sel = uidoc.Selection;
            currentUI = ONBOXAppl.ONBOXApplication.onboxApp.beamsUpdateWindow;

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
                    default:
                        break;
                }
            }
        }

        #region Create
        private void CreateBeams()
        {
            Element selectedElement = null;
            try
            {
                while (true)
                {

                    #region MinMaxDimentions
                    double BeamMinHeight = Utils.ConvertM.cmToFeet(20);
                    double BeamMaxHeight = Utils.ConvertM.cmToFeet(150);
                    double BeamMinWidth = Utils.ConvertM.cmToFeet(14);
                    double BeamMaxWidth = Utils.ConvertM.cmToFeet(40);
                    #endregion

                    selectedElement = doc.GetElement(sel.PickObject(ObjectType.Element, new BeamSelectionFilter(), "Selecione uma viga."));

                    FamilyInstance selectedBeam = selectedElement as FamilyInstance;
                    if (selectedBeam == null)
                    {
                        continue;
                    }

                    Wall selectedWall = GetMainWallAssociateWithBeam(selectedBeam);

                    if (selectedWall == null)
                    {
                        if (currentUI.BeamHeightMode == BeamFromWallHeightMode.HighestOpening || currentUI.BeamWidthMode == BeamFromWallWidthMode.WallWidth)
                            continue;
                    }

                    using (Transaction beamTransation = new Transaction(doc, "Atualizar viga"))
                    {
                        beamTransation.Start();

                        ElementId beamGroupId = selectedBeam.GroupId;

                        if (!beamGroupId.Equals(ElementId.InvalidElementId))
                        {
                            Group groupToUngroup = doc.GetElement(beamGroupId) as Group;
                            groupToUngroup.UngroupMembers();
                        }
                        
                        #region BeamParameters

                        //Levels
                        Level beamLevel = doc.GetElement(selectedBeam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId()) as Level;

                        //Offsets
                        double end0Offset = selectedBeam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION).AsDouble();
                        double end1Offset = selectedBeam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION).AsDouble();

                        //Unconnected Height
                        double wallUncHeight = selectedWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();

                        //Height and Width
                        double beamHeight = Utils.ConvertM.cmToFeet(60);
                        double beamWidth = Utils.ConvertM.cmToFeet(14);

                        //Get Information about the UI and the Geometry and set the variables beamWidth and beamHeight accordingly
                        DefineBeamParameters(selectedWall, doc, beamLevel, wallUncHeight, ref  beamWidth, ref beamHeight);

                        //Curve
                        Curve wallCurve = (selectedBeam.Location as LocationCurve).Curve;

                        #endregion

                        Family currentFamily = doc.GetElement(new ElementId(currentUI.SelectedBeamFamilyID)) as Family;
                        FamilySymbol currentFamilySymbol = doc.GetElement(currentFamily.GetFamilySymbolIds().First()) as FamilySymbol;
                        if (currentFamilySymbol == null)
                            throw new Exception("Erro na separação do tipo de família.");

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

                        double beamHeightInCM = Math.Round(Utils.ConvertM.feetToCm(beamHeight));
                        double beamWidthInCM = Math.Round(Utils.ConvertM.feetToCm(beamWidth));

                        beamHeight = Utils.ConvertM.cmToFeet((beamHeightInCM / roundNumber) * roundNumber);
                        beamWidth = Utils.ConvertM.cmToFeet((beamWidthInCM / roundNumber) * roundNumber);

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


                        currentFamilySymbol.Activate();
                        selectedBeam.Symbol = currentFamilySymbol;


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

        private Wall GetMainWallAssociateWithBeam(FamilyInstance targetBeam)
        {
            BoundingBoxXYZ beamBB = targetBeam.get_BoundingBox(null);
            beamBB.Enabled = true;

            Outline beamOutline = new Outline(beamBB.Min, beamBB.Max);

            BoundingBoxIntersectsFilter bbIntersects = new BoundingBoxIntersectsFilter(beamOutline);

            IList<Wall> WallsIntersectingBeamWithTheSameDirection = new List<Wall>();
            Curve targetBeamCurve = (targetBeam.Location as LocationCurve).Curve;
            Line targetBeamLine = targetBeamCurve as Line;
            Arc targetBeamArc = targetBeamCurve as Arc;

            if (targetBeamLine != null)
            {
                WallsIntersectingBeamWithTheSameDirection = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls)
                    .OfClass(typeof(Wall)).WhereElementIsNotElementType().WherePasses(bbIntersects).Cast<Wall>().Where(w =>
                    {
                        Curve wallCurve = (w.Location as LocationCurve).Curve;
                        Line wallLine = wallCurve as Line;

                        if (wallLine != null)
                        {
                            XYZ wallDirection = wallLine.Direction;
                            XYZ beamDirection = targetBeamLine.Direction;

                            double dotProduct = wallDirection.DotProduct(beamDirection);

                            if (Math.Abs(dotProduct) >= 0.95 && Math.Abs(dotProduct) <= 1.05)
                            {
                                return true;
                            }

                        }
                        return false;

                    }).ToList();

            }
            else
            {
                WallsIntersectingBeamWithTheSameDirection = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls)
                    .OfClass(typeof(Wall)).WhereElementIsNotElementType().WherePasses(bbIntersects).Cast<Wall>().Where(w =>
                    {
                        Curve wallCurve = (w.Location as LocationCurve).Curve;
                        Arc wallArc = wallCurve as Arc;

                        if (wallArc != null)
                        {
                            XYZ wallDirection = Line.CreateBound(wallArc.GetEndPoint(0), wallArc.GetEndPoint(1)).Direction;
                            XYZ beamDirection = Line.CreateBound(targetBeamArc.GetEndPoint(0), targetBeamArc.GetEndPoint(1)).Direction;

                            double dotProduct = wallDirection.DotProduct(beamDirection);

                            if (Math.Abs(dotProduct) >= 0.95 && Math.Abs(dotProduct) <= 1.05)
                            {
                                return true;
                            }

                        }
                        return false;

                    }).ToList();
            }

            if (WallsIntersectingBeamWithTheSameDirection.Count == 0)
            {
                return null;
            }
            else if (WallsIntersectingBeamWithTheSameDirection.Count == 1)
            {
                return WallsIntersectingBeamWithTheSameDirection.First();
            }
            else
            {
                return WallsIntersectingBeamWithTheSameDirection.First();
            }


        }
        #endregion


        private void Reload()
        {
            IList<FamilyWithImage> beamFamilies = GetAllBeamFamilies();
            ONBOXApplication.onboxApp.beamsUpdateWindow.PopulateComboFamily(beamFamilies);
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

        private void Unsubscribe()
        {

            ONBOXApplication.onboxApp.externalBeamsUpdateEvent.Dispose();
            ONBOXApplication.onboxApp.externalBeamsUpdateEvent = null;
            ONBOXApplication.onboxApp.requestBeamsUpdateHandler = null;

            ONBOXApplication.onboxApp.uiApp.ViewActivated -= ONBOXApplication.onboxApp.BeamsUpdate_ViewActivated;

        }

        public string GetName()
        {
            return "ONBOX : Beams update request handler";
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
    }

    [Transaction(TransactionMode.Manual)]
    class BeamUpdate : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                ONBOXApplication.onboxApp.uiApp = commandData.Application;
                ONBOXApplication.onboxApp.ShowBeamsUpdateUI();
            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}
