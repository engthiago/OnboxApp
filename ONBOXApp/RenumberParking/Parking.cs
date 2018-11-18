using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using System.Diagnostics;

namespace ONBOXAppl
{
    public class ParkingSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element.Category.Id.ToString() == BuiltInCategory.OST_Parking.GetHashCode().ToString())
            {
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
    class RenumberParking : IExternalCommand
    {
        static private UIDocument uidoc = null;
        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513354"));

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            IList<Element> parkingElements = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Parking).WhereElementIsNotElementType().ToList();
            IList<Element> allLevels = new FilteredElementCollector(doc).OfClass(typeof(Level)).ToList();

            Options op = new Options() { DetailLevel = ViewDetailLevel.Coarse, ComputeReferences = false, IncludeNonVisibleObjects = false };

            //Checks if theres parking slots instances placed in the project
            if (parkingElements.Count == 0)
            {
                message = Properties.Messages.RenumberParking_NoParking;
                return Result.Failed;
            }

            //Calls the UI
            renumberParkingUI parkingUI = new renumberParkingUI();
            if (parkingUI.ShowDialog() == false)
            {
                return Result.Cancelled;
            }

            try
            {
                //TODO Try to bring this to the UI instead (make a variable in the WPF form)
                #region Checks the ONBOXAppl to see what parkings will be numered

                IList<ElementId> typesThatWillBeNumbered = new List<ElementId>();

                foreach (ParkingTypesInfo currentParkingInfo in ONBOXApplication.storedParkingTypesInfo)
                {
                    if (currentParkingInfo.willBeNumbered == true)
                    {
                        typesThatWillBeNumbered.Add(new ElementId(currentParkingInfo.TypeId));
                    }
                }

                parkingElements = parkingElements.Where(e => typesThatWillBeNumbered.Contains(e.GetTypeId())).ToList();

                #endregion

                //The point of interest will be a far away point in the top left corner x negative and y positive if theres no preview park renumbered
                //Otherwise use the last park, so it will be sequenced
                XYZ pointOfInterest = new XYZ();
                if (ONBOXApplication.currentFirstParking == null)
                    pointOfInterest = new XYZ(-99999, 99999, -99999);
                else
                    pointOfInterest = (ONBOXApplication.currentFirstParking.Location as LocationPoint).Point;

                //Get All Parking Slots Instances
                parkingElements = parkingElements.Where(i => i is Element).OrderBy(e =>
                {
                    double locationX = (e.Location as LocationPoint).Point.DistanceTo(pointOfInterest);

                    return locationX;
                }).ToList();

                int counter = 1;

                //TODO Again, try to remove these Global Variables
                if (ONBOXApplication.currentFirstParking == null) //since we removed the button that selects the first element this will be always true
                {
                    if (ONBOXApplication.parkingRenumType == ONBOXApplication.RenumberType.Ascending)
                    {
                        allLevels = allLevels.Where(i => i is Level).OrderBy(l => (l as Level).Elevation).ToList();
                    }
                    if (ONBOXApplication.parkingRenumType == ONBOXApplication.RenumberType.Descending)
                    {
                        allLevels = allLevels.Where(i => i is Level).OrderByDescending(l => (l as Level).Elevation).ToList();
                    }

                }
                #region FirstElement (will never run currently)
                else //currently it will never run this block of code because we remove the button in the renumberParking UI that set this condition
                {
                    double levelHeight = (doc.GetElement(ONBOXApplication.currentFirstParking.LevelId) as Level).Elevation;
                    int numberOfElementsUnder = 0;

                    allLevels = allLevels.Where(i => i is Level).OrderBy(l => ((l as Level).Elevation) - levelHeight).ToList();

                    foreach (Element currentLevel in allLevels)
                    {
                        foreach (Element currentParking in parkingElements)
                        {
                            if ((currentLevel.Id != ONBOXApplication.currentFirstParking.LevelId) && (ONBOXApplication.currentFirstParking.Id != currentParking.Id))
                            {
                                if ((doc.GetElement(currentParking.LevelId) as Level).Elevation - (doc.GetElement(ONBOXApplication.currentFirstParking.LevelId) as Level).Elevation < 0)
                                {
                                    numberOfElementsUnder++;
                                }
                            }
                        }

                    }
                    counter = counter - numberOfElementsUnder;

                }
                #endregion

                //Start the Renumbering process
                using (Transaction t = new Transaction(doc, Properties.Messages.RenumberParking_Transaction))
                {
                    t.Start();
                    //TODO Instead of looping trough the levels, try to loop trough the LevelInfo
                    foreach (Element eLevel in allLevels)
                    {
                        //The storedParkingLevelInfo is global variable and it got initialisead in the UI, calling the getAllLevels() method in this class
                        //TODO Again, move this to a local variable
                        LevelInfo lvlInfo = ONBOXApplication.storedParkingLevelInfo.Where(e => e.levelId == eLevel.Id.IntegerValue).First();

                        if (lvlInfo.willBeNumbered == false)
                        {
                            continue;
                        }

                        Element prevElement = null;
                        IList<Element> RemainingParkingsinLevel = new List<Element>();
                        IList<ElementId> UsedParkingsInLevel = new List<ElementId>();
                        Element firstElement = null;
                        string currentLevelPrefix = "";

                        //Loop through all levell info and get the related prefix if there is one
                        //TODO use this in conjuntion with the storedParkingInfo so we only loop through the level info one time
                        foreach (LevelInfo currentLevelInfo in ONBOXApplication.storedParkingLevelInfo)
                        {
                            if (currentLevelInfo.levelId == eLevel.Id.IntegerValue)
                            {
                                currentLevelPrefix = currentLevelInfo.levelPrefix;
                            }
                        }

                        //Get All the parking Elements that belongs to this specific level
                        foreach (Element ePark in parkingElements)
                        {
                            if (ePark.LevelId == (eLevel as Level).Id)
                            {
                                RemainingParkingsinLevel.Add(ePark);
                            }
                        }

                        if (RemainingParkingsinLevel.Count == 0)
                            continue;

                        //We dont use foreach here because we will mess with the List inside of the block
                        //The other point is that we loop here based on the blocks (amount of parkings near each other)
                        //TODO Maybe find a diferent way to loop here, since 'While (true)' is a little dangerous
                        while (true) // continue the loop until there is no more parking slot to renumber
                        {
                            IList<Element> tempParkings = RemainingParkingsinLevel.ToList();
                            RemainingParkingsinLevel.Clear();

                            IList<Element> block = new List<Element>();

                            //Get All remaning parkings (the ones that havent been renamed yet) 
                            //Here is another loop that probably could be optimized
                            //TODO Optimize this as well
                            foreach (Element tempPark in tempParkings)
                            {
                                if (!UsedParkingsInLevel.Contains(tempPark.Id))
                                    RemainingParkingsinLevel.Add(tempPark);
                            }

                            if (RemainingParkingsinLevel.Count == 0)
                            {
                                break;
                            }

                            //The next park to be renumbered will be the first one next to the point of interest
                            //See the pointOfInterest definition to know more
                            RemainingParkingsinLevel = RemainingParkingsinLevel.Where(e => e is Element).OrderBy(e =>
                            {
                                double location = (e.Location as LocationPoint).Point.DistanceTo(pointOfInterest);
                                return location;
                            }).ToList();

                            foreach (Element ePark in RemainingParkingsinLevel)
                            {
                                //In the case that this is the first element to be renumbered
                                if (prevElement == null)
                                {
                                    if (counter == 0)
                                    {
                                        counter++;
                                    }

                                    //Again, thats a Global Variable
                                    //TODO Create a local Variable for this one as well
                                    string typePrefix = ONBOXApplication.storedParkingTypesInfo.Where(e => e.TypeId == ePark.GetTypeId().IntegerValue).First().TypePrefix;

                                    ePark.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(currentLevelPrefix + typePrefix + counter.ToString());
                                    counter++;
                                    UsedParkingsInLevel.Add(ePark.Id);
                                    prevElement = ePark;
                                    firstElement = ePark;
                                }
                                else
                                {
                                    foreach (Element currentPark in RemainingParkingsinLevel)
                                    {
                                        double distance = (currentPark.Location as LocationPoint).Point.DistanceTo((prevElement.Location as LocationPoint).Point);
                                        distance = Utils.ConvertM.feetToM(distance);

                                        //TODO Gobal Variable to local
                                        ParkingTypesInfo currentTypeInfo = ONBOXApplication.storedParkingTypesInfo.Where(e => e.TypeId == currentPark.GetTypeId().IntegerValue).First();

                                        //The tolerance to include this parking as a part of the current block will be this
                                        double tolerance = currentTypeInfo.TypeWidth + 0.1;

                                        //Checks if the current parking is near the prev parking and if it wasnt already renumbered
                                        if ((distance < tolerance) && (!UsedParkingsInLevel.Contains(currentPark.Id)))
                                        {
                                            //Now we have to check if the park has the same orientation that the prevpark
                                            FamilyInstance currentParkInstance = currentPark as FamilyInstance;
                                            XYZ currenParkOrientation = currentParkInstance.FacingOrientation;
                                            FamilyInstance prevParkInstance = prevElement as FamilyInstance;
                                            XYZ prevParkOrientation = prevParkInstance.FacingOrientation;

                                            if (currenParkOrientation.IsAlmostEqualTo(prevParkOrientation, 0.1))
                                            {
                                                block.Add(currentPark);
                                                if (counter == 0)
                                                {
                                                    counter++;
                                                }
                                                currentPark.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(currentLevelPrefix + currentTypeInfo.TypePrefix + counter.ToString());
                                                UsedParkingsInLevel.Add(currentPark.Id);
                                                counter++;
                                                prevElement = currentPark;
                                            }
                                        }
                                    }

                                    IList<Element> blockToRenumber = new List<Element>();

                                    foreach (Element currentParkinLevel in RemainingParkingsinLevel)
                                    {
                                        double distance = (currentParkinLevel.Location as LocationPoint).Point.DistanceTo((prevElement.Location as LocationPoint).Point);
                                        distance = Utils.ConvertM.feetToM(distance);

                                        ParkingTypesInfo currentTypeInfo = ONBOXApplication.storedParkingTypesInfo.Where(e => e.TypeId == currentParkinLevel.GetTypeId().IntegerValue).First();
                                        double tolerance = currentTypeInfo.TypeWidth + 0.1;

                                        if ((distance < tolerance) && (!UsedParkingsInLevel.Contains(currentParkinLevel.Id)))
                                        {
                                            FamilyInstance currentParkInstance = currentParkinLevel as FamilyInstance;
                                            XYZ currenParkOrientation = currentParkInstance.FacingOrientation;
                                            FamilyInstance prevParkInstance = prevElement as FamilyInstance;
                                            XYZ prevParkOrientation = prevParkInstance.FacingOrientation;

                                            if (currenParkOrientation.IsAlmostEqualTo(prevParkOrientation, 0.1))
                                            {
                                                blockToRenumber.Add(currentParkinLevel);
                                                UsedParkingsInLevel.Add(currentParkinLevel.Id);
                                                prevElement = currentParkinLevel;
                                            }
                                        }
                                    }

                                    foreach (Element currentParking in blockToRenumber)
                                    {
                                        if (counter == 0)
                                        {
                                            counter++;
                                        }
                                        ParkingTypesInfo currentTypeInfo = ONBOXApplication.storedParkingTypesInfo.Where(e => e.TypeId == currentParking.GetTypeId().IntegerValue).First();
                                        currentParking.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(currentLevelPrefix + currentTypeInfo.TypePrefix + counter.ToString());
                                        counter++;
                                        UsedParkingsInLevel.Add(currentParking.Id);
                                    }

                                    if (blockToRenumber.Count == 0)
                                    {
                                        tempParkings.Clear();
                                        tempParkings = RemainingParkingsinLevel.ToList();
                                        IList<Element> remain = new List<Element>();

                                        foreach (Element currentParking in tempParkings)
                                        {
                                            if (!UsedParkingsInLevel.Contains(currentParking.Id))
                                            {
                                                remain.Add(currentParking);
                                            }
                                        }

                                        if (remain.Count == 0)
                                            break;

                                        remain = remain.Where(p => p is Element).OrderBy(e =>
                                        {
                                            double locationX = getMidPoint(e, op).DistanceTo(pointOfInterest);
                                            return locationX;
                                        }).ToList();

                                        prevElement = remain.First();
                                    }
                                }
                            }
                        }
                        if (ONBOXApplication.isNumIndenLevel == true)
                        {
                            counter = 1;
                        }
                    }
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

        private XYZ getMidPoint(Element e, Options op)
        {
            XYZ point = new XYZ();

            BoundingBoxXYZ bb = e.get_BoundingBox(uidoc.ActiveView);
            bb.Enabled = true;
            point = Utils.GetPoint.getMidPoint(bb.Min, bb.Max);

            return point;
        }

        static internal void selectParking(renumberParkingUI UI)
        {
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            try
            {
                ONBOXApplication.currentFirstParking = doc.GetElement(sel.PickObject(ObjectType.Element, new ParkingSelectionFilter(), "Selecione a primeira vaga de garagem"));
                ONBOXApplication.parkingRenumType = ONBOXApplication.RenumberType.FirstParking;
            }
            catch (Exception)
            {
                ONBOXApplication.currentFirstParking = null;
                ONBOXApplication.parkingRenumType = ONBOXApplication.RenumberType.Ascending;
            }
            finally
            {
                UI.comboFirstAdjust();
            }

        }

        static internal IList<LevelInfo> getAllLevels()
        {
            IList<LevelInfo> levelInformation = new List<LevelInfo>();

            IList<Element> allLevels = new FilteredElementCollector(uidoc.Document).OfClass(typeof(Level))
                .WhereElementIsNotElementType().OrderBy(e => (e as Level).Elevation).ToList();

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
                //currentLevelInformation.isStandardLevel = false;
                levelInformation.Add(currentLevelInformation);
            }

            return levelInformation;
        }

        static internal IList<ParkingTypesInfo> getAllParkingTypesInfo()
        {
            IList<ElementId> allUsedParkingTypesIDs = new List<ElementId>();
            IList<ParkingTypesInfo> AllUsedParkingTypesInfo = new List<ParkingTypesInfo>();

            IList<Element> allParkingInstances = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_Parking)
                .WhereElementIsNotElementType().ToList();

            foreach (Element currentElement in allParkingInstances)
            {
                if (!allUsedParkingTypesIDs.Contains(currentElement.GetTypeId()))
                {
                    int typeID = currentElement.GetTypeId().IntegerValue;
                    string typeName = currentElement.Name;
                    ParkingTypesInfo currentTypeInfo = new ParkingTypesInfo() { TypeName = typeName, TypeId = typeID, willBeNumbered = true, TypePrefix = "", TypeWidth = GetParkingWidth(currentElement) };

                    allUsedParkingTypesIDs.Add(currentElement.GetTypeId());
                    AllUsedParkingTypesInfo.Add(currentTypeInfo);
                }
            }

            return AllUsedParkingTypesInfo;
        }

        static internal double GetParkingWidth(Element targetElement)
        {
            FamilyInstance fi = targetElement as FamilyInstance;
            FamilySymbol fs = fi.Symbol;
            BoundingBoxXYZ bb = fs.get_Geometry(new Options()).GetBoundingBox();

            Line hLine = Line.CreateBound(bb.Min, bb.Max);

            double angleX = XYZ.BasisX.AngleTo(bb.Max - bb.Min);
            double angleY = XYZ.BasisY.AngleTo(bb.Max - bb.Min);

            double dimX = hLine.Length * Math.Cos(angleX);
            double dimY = hLine.Length * Math.Cos(angleY);
            double dimXMeters = Utils.ConvertM.feetToM(dimX);
            double dimYMeters = Utils.ConvertM.feetToM(dimY);

            if (dimXMeters < dimYMeters)
                return dimXMeters;
            else
                return dimYMeters;
        }

    }

    [Transaction(TransactionMode.Manual)]
    class RenumberBlockParking : IExternalCommand
    {
        static private UIDocument uidoc = null;
        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513355"));

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            
            if (!(uidoc.ActiveView is ViewPlan))
            {
                message = Properties.Messages.RenumberParking_NotPlanView;
                return Result.Failed;
            }

            IList<Element> parkingElements = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_Parking)
                .WhereElementIsNotElementType().Where(e => e.LevelId == uidoc.ActiveView.GenLevel.Id).ToList();

            if (parkingElements.Count == 0)
            {
                message = Properties.Messages.RenumberParking_NoParking;
                return Result.Failed;
            }


            renumberBlocksUI rBlockUI = new renumberBlocksUI();
            rBlockUI.ShowDialog();

            return Result.Succeeded;
        }

        static internal void getLevelLastNumber(bool isSearchingForAllLevels, out string Prefix, out int Number)
        {
            IList<Element> parkingElements = new List<Element>();

            ElementId currentLevelId = uidoc.ActiveView.LevelId;

            if (isSearchingForAllLevels == true)
            {
                parkingElements = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_Parking).WhereElementIsNotElementType().ToList();
            }
            else
            {
                parkingElements = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_Parking)
                    .WhereElementIsNotElementType().Where(e => e.LevelId == uidoc.ActiveView.GenLevel.Id).ToList();
            }

            int biggerNumber = 0;
            string biggerNumberPrefix = "";

            int prevNumber = int.MinValue;

            foreach (Element currentElem in parkingElements)
            {
                int currentNumber = 0;
                string currentPrefix = "";
                string currentNumberAndPrefix = currentElem.get_Parameter(BuiltInParameter.DOOR_NUMBER).AsString();
                int numberOfElementThatSeparatesStringOfInt = 0;

                if (currentNumberAndPrefix != null)
                {
                    for (int i = currentNumberAndPrefix.Count(); i > 0; i--)
                    {
                        string currentCharString = currentNumberAndPrefix.ElementAt(i - 1).ToString();
                        int tempInt = 0;
                        if (int.TryParse(currentCharString, out tempInt))
                        {
                            numberOfElementThatSeparatesStringOfInt = i - 1;
                        }
                        else
                        {
                            break;
                        }
                    }

                    int.TryParse(currentNumberAndPrefix.Substring(numberOfElementThatSeparatesStringOfInt), out currentNumber);
                }

                if (currentNumber != 0)
                {
                    currentPrefix = currentNumberAndPrefix.Remove(numberOfElementThatSeparatesStringOfInt);
                }
                else
                {
                    continue;
                }

                if (prevNumber < currentNumber)
                {
                    biggerNumber = currentNumber;
                    biggerNumberPrefix = currentPrefix;
                    prevNumber = currentNumber;
                }
            }

            Prefix = biggerNumberPrefix;
            Number = biggerNumber;
        }

        static internal void renameBlock(string inPrefix, string inNumber, bool isSingleParkingSelected)
        {
            int lastUsedNumber = int.Parse(inNumber);

            try
            {
                Selection sel = uidoc.Selection;
                Document doc = uidoc.Document;
                IList<Element> parkingElements = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Parking).WhereElementIsNotElementType().ToList();
                IList<Element> remainingParking = new List<Element>();
                IList<ElementId> usedparking = new List<ElementId>();
                Element pickedOrFirstSelectedParking = null;
                IList<ParkingTypesInfo> parkingInfo = getAllParkingTypesInfo();

                if (isSingleParkingSelected)
                {
                    pickedOrFirstSelectedParking = doc.GetElement(sel.PickObject(ObjectType.Element, new ParkingSelectionFilter(), Properties.Messages.RenumberParking_SelectParkingSpace));
                    IList<Element> parkingElementsInLevel = parkingElements.ToList()
                        .Where(p => p.LevelId == pickedOrFirstSelectedParking.LevelId)
                        .Where(p => (p as FamilyInstance).Symbol.Id == (pickedOrFirstSelectedParking as FamilyInstance).Symbol.Id).ToList();

                    foreach (Element e in parkingElementsInLevel)
                    {
                        if (!usedparking.Contains(e.Id))
                        {
                            remainingParking.Add(e);
                        }
                    }
                }
                else
                {
                    remainingParking = sel.PickElementsByRectangle(new ParkingSelectionFilter(), Properties.Messages.RenumberParking_SelectParkingSpace);
                    pickedOrFirstSelectedParking = doc.GetElement(sel.PickObject(ObjectType.Element, new ParkingSelectionFilter(), Properties.Messages.RenumberParking_SelectFirstParkingSpace));

                    //if the user didnt select anything on the box selection, return
                    if (remainingParking.Count < 1)
                        return;

                    //if the user didnt select anything on the pick object, return
                    if (pickedOrFirstSelectedParking == null)
                        return;

                    //User only parkings with the same type to the first park (picked one) and also order then by the first park
                    remainingParking = remainingParking.Where(e => (e as FamilyInstance).Symbol.Id == (pickedOrFirstSelectedParking as FamilyInstance).Symbol.Id)
                        .OrderBy(e => (e.Location as LocationPoint).Point.DistanceTo((pickedOrFirstSelectedParking.Location as LocationPoint).Point)).ToList();

                }


                using (Transaction t = new Transaction(doc, Properties.Messages.RenumberParking_Transaction))
                {
                    t.Start();
                    if (remainingParking.Count == 1)
                    {
                        lastUsedNumber++;
                        remainingParking.First().get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(inPrefix + lastUsedNumber.ToString());
                        usedparking.Add(remainingParking.First().Id);
                    }
                    else
                    {
                        Element prevElement = null;
                        remainingParking = remainingParking.Where(p => p is Element).OrderBy(p =>
                        {
                            return (p.Location as LocationPoint).Point.DistanceTo((pickedOrFirstSelectedParking.Location as LocationPoint).Point);
                        }).ToList();


                        if (prevElement == null)
                        {
                            lastUsedNumber++;
                            remainingParking.First().get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(inPrefix + lastUsedNumber.ToString());
                            usedparking.Add(remainingParking.First().Id);
                            prevElement = remainingParking.First();
                        }

                        foreach (Element currentRemaining in remainingParking)
                        {
                            if (isSingleParkingSelected)
                            {
                                double dist = (currentRemaining.Location as LocationPoint).Point.DistanceTo((prevElement.Location as LocationPoint).Point);
                                dist = Utils.ConvertM.feetToM(dist);

                                ParkingTypesInfo currentTypeInfo = parkingInfo.Where(e => e.TypeId == currentRemaining.GetTypeId().IntegerValue).First();
                                double tolerance = currentTypeInfo.TypeWidth + 0.3;

                                FamilyInstance currentParkInstance = currentRemaining as FamilyInstance;
                                XYZ currenParkOrientation = currentParkInstance.FacingOrientation;
                                FamilyInstance prevParkInstance = prevElement as FamilyInstance;
                                XYZ prevParkOrientation = prevParkInstance.FacingOrientation;

                                if ((dist < tolerance) && (!usedparking.Contains(currentRemaining.Id)))
                                {
                                    if (currenParkOrientation.IsAlmostEqualTo(prevParkOrientation, 0.1))
                                    {
                                        lastUsedNumber++;
                                        currentRemaining.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(inPrefix + lastUsedNumber.ToString());
                                        usedparking.Add(currentRemaining.Id);
                                        prevElement = currentRemaining;
                                    }
                                }
                            }
                            else
                            {
                                if (currentRemaining != prevElement)
                                {
                                    lastUsedNumber++;
                                    currentRemaining.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(inPrefix + lastUsedNumber.ToString());
                                    usedparking.Add(currentRemaining.Id);
                                    prevElement = currentRemaining; 
                                }
                            }
                        }

                    }
                    t.Commit();
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                renumberBlocksUI repeatUICreation = new renumberBlocksUI(inPrefix, lastUsedNumber.ToString());
                repeatUICreation.ShowDialog();
            }

        }

        static internal IList<ParkingTypesInfo> getAllParkingTypesInfo()
        {
            IList<ElementId> allUsedParkingTypesIDs = new List<ElementId>();
            IList<ParkingTypesInfo> AllUsedParkingTypesInfo = new List<ParkingTypesInfo>();

            IList<Element> allParkingInstances = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_Parking)
                .WhereElementIsNotElementType().ToList();

            foreach (Element currentElement in allParkingInstances)
            {
                if (!allUsedParkingTypesIDs.Contains(currentElement.GetTypeId()))
                {
                    int typeID = currentElement.GetTypeId().IntegerValue;
                    string typeName = currentElement.Name;
                    ParkingTypesInfo currentTypeInfo = new ParkingTypesInfo() { TypeName = typeName, TypeId = typeID, willBeNumbered = true, TypePrefix = "", TypeWidth = RenumberParking.GetParkingWidth(currentElement) };

                    allUsedParkingTypesIDs.Add(currentElement.GetTypeId());
                    AllUsedParkingTypesInfo.Add(currentTypeInfo);
                }
            }

            return AllUsedParkingTypesInfo;
        }

        static internal void RenumCleanSelected(string inPrefix, string inNumber)
        {
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            View thisView = uidoc.ActiveView;

            try
            {
                IList<Element> allParkingElement = sel.PickElementsByRectangle(new ParkingSelectionFilter(), Properties.Messages.RenumberParking_SelectParkingSpace);

                if (allParkingElement.Count > 0)
                {
                    try
                    {
                        using (Transaction cleanerTrans = new Transaction(doc, Properties.Messages.RenumberParking_ClearParkNumbering))
                        {
                            cleanerTrans.Start();
                            foreach (Element currentElemen in allParkingElement)
                            {
                                currentElemen.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set("");
                            }
                            cleanerTrans.Commit();
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            finally
            {
                Level currentLevel = uidoc.ActiveView.GenLevel;

                IList<Element> AllParkingInLevel = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Parking).OfClass(typeof(FamilyInstance))
                    .Where(p => p.LevelId == currentLevel.Id).ToList();

                double lastUsedNumber = 0;

                if (AllParkingInLevel.Count > 0)
                {
                    foreach (Element currentParking in AllParkingInLevel)
                    {
                        Parameter numberParam = currentParking.get_Parameter(BuiltInParameter.DOOR_NUMBER);
                        
                        if (numberParam.HasValue)
                        {
                            double currentParkingNumber = -232.23;
                            if (double.TryParse(numberParam.AsString(), out currentParkingNumber))
                            {
                                if (lastUsedNumber < currentParkingNumber)
                                {
                                    lastUsedNumber = currentParkingNumber;
                                }
                            }
                        }
                            
                    }
                }

                renumberBlocksUI repeatUICreation = new renumberBlocksUI(inPrefix, lastUsedNumber.ToString());
                repeatUICreation.ShowDialog();
            }
        }

    }

    [Transaction(TransactionMode.Manual)]
    class RenumberCleaner : IExternalCommand
    {
        static private UIDocument uidoc = null;
        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513359"));

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            uidoc = commandData.Application.ActiveUIDocument;

            if (!(uidoc.ActiveView is ViewPlan))
            {
                message = Properties.Messages.RenumberParking_NotPlanView;
                return Result.Failed;
            }


            RenumberParkingCleanerUI cleanerUI = new RenumberParkingCleanerUI();
            cleanerUI.ShowDialog();

            return Result.Succeeded;
        }

        static internal void RenumClean(bool cleanAllLevels)
        {
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            View thisView = uidoc.ActiveView;

            IList<Element> allParkingElement = new List<Element>();

            if (cleanAllLevels)
            {
                allParkingElement = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Parking).WhereElementIsNotElementType().ToList();
            }
            else
            {
                allParkingElement = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Parking)
                    .WhereElementIsNotElementType().Where(e => e.LevelId == thisView.GenLevel.Id).ToList();
            }

            if (allParkingElement.Count > 0)
            {
                try
                {
                    using (Transaction cleanerTrans = new Transaction(doc, Properties.Messages.RenumberParking_ClearParkNumbering))
                    {
                        cleanerTrans.Start();
                        foreach (Element currentElemen in allParkingElement)
                        {
                            currentElemen.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set("");
                        }
                        cleanerTrans.Commit();
                    }
                }
                catch
                {
                }
            }

        }

    }
}