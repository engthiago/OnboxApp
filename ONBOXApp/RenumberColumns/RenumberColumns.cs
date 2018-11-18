using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI.Selection;

namespace ONBOXAppl
{
    enum ColumnRenumberOrder { Ascending, Descending }

    class ColumnNOTSlantedSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category.Id.ToString() == (BuiltInCategory.OST_StructuralColumns).GetHashCode().ToString())
            {
                FamilyInstance columnInstance = elem as FamilyInstance;
                if (columnInstance != null)
                {
                    if (columnInstance.IsSlantedColumn == false)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    //BIG PROBLEM HERE ->> RenumberColuns and RenumberColunsSelection have duplicate methods that are EXACTLY the same
    //the problem is that they use the uidoc to get the doc to perform the activities, so lets try to pass the doc
    //as an argument to those functions and clean out the duplicates

    [Transaction(TransactionMode.Manual)]
    class RenumberColumns : IExternalCommand
    {
        static UIDocument uidoc = null;
        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513361"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (uidoc.ActiveView as ViewPlan == null)
            {
                message = Properties.Messages.RenumberColumns_NotPlanView;
                return Result.Failed;
            }

            XYZ pointOfInterest = new XYZ(-9999, 9999, -9999);

            IList<Element> allColumns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance)).Where(e => (e as FamilyInstance).IsSlantedColumn == false)
            .OrderBy(e => (e.Location as LocationPoint).Point.DistanceTo(pointOfInterest)).ToList();

            if (allColumns.Count == 0)
            {
                message = Properties.Messages.RenumberColumns_NoColumnFamilyLoaded;
                return Result.Failed;
            }

            uidoc = commandData.Application.ActiveUIDocument;

            RenumberColumnsUI renumberColumnsWindow = new RenumberColumnsUI();
            renumberColumnsWindow.ShowDialog();

            return Result.Succeeded;
        }

        static internal void DoRenumbering(bool selectColumnRow, int counter)
        {
            try
            {
                Document doc = uidoc.Document;

                string sufix = "";
                string lance = " " + Properties.WindowLanguage.RenumberColumns_LevelLabeler + " ";
                string concatWord = " " + Properties.WindowLanguage.RenumberColumns_LevelConcatenator + " ";

                string space = " ";
                lance = space + ONBOXApplication.columnsLevelIndicator + space;
                concatWord = space + ONBOXApplication.columnsConcatWord + space;

                XYZ pointOfInterest = new XYZ(-9999, 9999, -9999);

                IList<Element> allColumns = new List<Element>();

                if (selectColumnRow)
                {
                    allColumns = uidoc.Selection.PickElementsByRectangle(new ColumnNOTSlantedSelectionFilter(), Properties.Messages.RenumberColumns_SelectTheRowOfColumns);

                    Element firstColumn = doc.GetElement(uidoc.Selection.PickObject(ObjectType.Element, new ColumnNOTSlantedSelectionFilter(), Properties.Messages.RenumberColumns_SelectFirstColumn));
                    XYZ firstPointLocation = (firstColumn.Location as LocationPoint).Point;

                    allColumns = allColumns.Where(e => (e as FamilyInstance) != null).OrderBy(e => (e.Location as LocationPoint).Point.DistanceTo(firstPointLocation)).ToList();
                }
                else
                {
                    allColumns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance)).Where(e => (e as FamilyInstance).IsSlantedColumn == false)
                        .OrderBy(e => (e.Location as LocationPoint).Point.DistanceTo(pointOfInterest)).ToList();
                }

                IList<Element> allLevels = new List<Element>();

                if (ONBOXApplication.storedColumnRenumOrder == ColumnRenumberOrder.Ascending)
                {
                    allLevels = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level)).WhereElementIsNotElementType().OrderBy(l => (l as Level).Elevation).ToList();
                }
                else
                {
                    allLevels = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level)).WhereElementIsNotElementType().OrderByDescending(l => (l as Level).Elevation).ToList();
                }

                IList<ElementId> allUsedColumnsIDs = new List<ElementId>();

                double topLevelHeight = (allLevels.Last() as Level).Elevation;
                double bottomLevelHeight = (allLevels.First() as Level).Elevation;

                using (Transaction t = new Transaction(doc, Properties.Messages.RenumberColumns_Transaction))
                {
                    t.Start();
                    for (int i = 0; i < allLevels.Count; i++)
                    {
                        foreach (Element currentColumn in allColumns)
                        {
                            Element currentColumnBaseLevel = doc.GetElement(currentColumn.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId());

                            if (currentColumnBaseLevel.Id == (allLevels.ElementAt(i) as Level).Id)
                            {
                                if (allUsedColumnsIDs.Contains(currentColumn.Id))
                                {
                                    continue;
                                }

                                ColumnTypesInfo currentColumnTypeInfo = ONBOXApplication.storedColumnTypesInfo.Where(colInfo => colInfo.TypeId == currentColumn.GetTypeId().IntegerValue).First();
                                if (currentColumnTypeInfo.WillBeNumbered == false)
                                    continue;

                                LevelInfo currentLevelInfo = ONBOXApplication.storedColumnLevelInfo.Where(lvlInfo => lvlInfo.levelId == allLevels.ElementAt(i).Id.IntegerValue).First();
                                if (currentLevelInfo.willBeNumbered == false)
                                    continue;

                                Element currenColumnTopLevel = doc.GetElement(currentColumn.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId());
                                allUsedColumnsIDs.Add(currentColumn.Id);

                                string columnBaseSufix = ONBOXApplication.storedColumnLevelInfo.Where(colInfo => colInfo.levelId == currentColumnBaseLevel.Id.IntegerValue).First().levelPrefix;
                                string columnTopSufix = ONBOXApplication.storedColumnLevelInfo.Where(colInfo => colInfo.levelId == currenColumnTopLevel.Id.IntegerValue).First().levelPrefix;

                                if (currenColumnTopLevel.Id != currentColumnBaseLevel.Id)
                                {
                                    sufix = lance + columnBaseSufix + concatWord + columnTopSufix;
                                }
                                else
                                {
                                    sufix = lance + columnBaseSufix;
                                }

                                //the main column
                                currentColumn.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(currentColumnTypeInfo.TypePrefix + counter + sufix);
                                XYZ cLocationPoint = (currentColumn.Location as LocationPoint).Point;

                                // a tiny little boundingbox or outline on the center of the pilar
                                //double offSet = 0.1;
                                //XYZ minPoint = new XYZ(cLocationPoint.X - offSet, cLocationPoint.Y - offSet, topLevelHeight);
                                //XYZ maxPoint = new XYZ(cLocationPoint.X + offSet, cLocationPoint.Y + offSet, bottomLevelHeight);

                                GeometryElement gElem = currentColumn.get_Geometry(new Options());
                                BoundingBoxXYZ bb = gElem.GetBoundingBox();
                                bb.Enabled = true;
                                XYZ minPoint = bb.Min;
                                XYZ maxPoint = new XYZ(bb.Max.X, bb.Max.Y, topLevelHeight);

                                Outline outLine = new Outline(minPoint, maxPoint);

                                BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outLine);

                                //IList<Element> remainingColumns = allColumns.Where(c => (!allUsedColumnsIDs.Contains(c.Id))).ToList();

                                IList<Element> ColumnsInZLine = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance))
                                    .WherePasses(bbFilter).Where(c => (!allUsedColumnsIDs.Contains(c.Id))).ToList();

                                //we will try to find other columns equal to this column and are in the same z line (position) on other levels, so they have the same name
                                for (int l = 0; l < allLevels.Count; l++)
                                {
                                    currentLevelInfo = ONBOXApplication.storedColumnLevelInfo.Where(lvlInfo => lvlInfo.levelId == allLevels.ElementAt(l).Id.IntegerValue).First();
                                    if (currentLevelInfo.willBeNumbered == false)
                                        continue;

                                    //if the current level is the same level as the level of the main column, we dont need to check again
                                    if (l == i)
                                        continue;

                                    Element currentcolumnInLine = null;
                                    if (ColumnsInZLine.Count > 0)
                                    {
                                        IList<Element> columnsInZlineInThisLevel = ColumnsInZLine.Where(column => column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId() == allLevels.ElementAt(l).Id).ToList();
                                        if (columnsInZlineInThisLevel.Count == 1)
                                        {
                                            currentcolumnInLine = columnsInZlineInThisLevel.First();
                                        }
                                        else if (columnsInZlineInThisLevel.Count == 0)
                                        {
                                            continue;
                                        }
                                        else if (columnsInZlineInThisLevel.Count > 1)// if we found more than one column on this level we have to check wich one is more similiar to the main column
                                        {
                                            //we try to find the nearest column to the main one
                                            double nearDist = double.PositiveInfinity;
                                            XYZ mainColumnPosition = (currentColumn.Location as LocationPoint).Point;
                                            foreach (Element currentColumnCandidate in columnsInZlineInThisLevel)
                                            {
                                                XYZ currentCanditatePosition = (currentColumnCandidate.Location as LocationPoint).Point;
                                                double currentDist = mainColumnPosition.DistanceTo(currentCanditatePosition);
                                                if (currentDist < nearDist)
                                                {
                                                    nearDist = currentDist;
                                                    currentcolumnInLine = currentColumnCandidate;
                                                }
                                            }
                                        }

                                        if ((currentcolumnInLine == null) || (currentColumn.Id == currentcolumnInLine.Id))
                                            continue;

                                        currentColumnTypeInfo = ONBOXApplication.storedColumnTypesInfo.Where(colInfo => colInfo.TypeId == currentcolumnInLine.GetTypeId().IntegerValue).First();
                                        if (currentColumnTypeInfo.WillBeNumbered == false)
                                            continue;

                                        currentColumnBaseLevel = doc.GetElement(currentcolumnInLine.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId());
                                        currenColumnTopLevel = doc.GetElement(currentcolumnInLine.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId());

                                        columnBaseSufix = ONBOXApplication.storedColumnLevelInfo.Where(colInfo => colInfo.levelId == currentColumnBaseLevel.Id.IntegerValue).First().levelPrefix;
                                        columnTopSufix = ONBOXApplication.storedColumnLevelInfo.Where(colInfo => colInfo.levelId == currenColumnTopLevel.Id.IntegerValue).First().levelPrefix;

                                        if (currenColumnTopLevel.Id != currentColumnBaseLevel.Id)
                                        {
                                            sufix = lance + columnBaseSufix + concatWord + columnTopSufix;
                                        }
                                        else
                                        {
                                            sufix = lance + columnBaseSufix;
                                        }

                                        allUsedColumnsIDs.Add(currentColumn.Id);
                                        sufix = lance + columnBaseSufix + concatWord + columnTopSufix;
                                        currentcolumnInLine.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(currentColumnTypeInfo.TypePrefix + counter + sufix);
                                        allUsedColumnsIDs.Add(currentcolumnInLine.Id);
                                    }
                                }
                                counter++;
                            }
                        }
                    }
                    t.Commit();
                }
            }
            catch
            {
            }
            finally
            {
                if (selectColumnRow)
                {
                    RenumberColumnsSelectionUI renumberColumnsWindow = new RenumberColumnsSelectionUI();
                    renumberColumnsWindow.ShowDialog();
                }
            }
        }

        static internal void ClearRenumbering()
        {
            Document doc = uidoc.Document;
            try
            {
                IList<Element> allColumns = uidoc.Selection.PickElementsByRectangle(new ColumnNOTSlantedSelectionFilter(), Properties.Messages.RenumberColumns_SelectTheRowOfColumns);

                using (Transaction t = new Transaction(doc, Properties.Messages.RenumberColumns_RemoveNumbers_Transaction))
                {
                    t.Start();
                    foreach (Element currentColumn in allColumns)
                    {
                        XYZ cLocationPoint = (currentColumn.Location as LocationPoint).Point;
                        GeometryElement gElem = currentColumn.get_Geometry(new Options());
                        BoundingBoxXYZ bb = gElem.GetBoundingBox();
                        bb.Enabled = true;
                        XYZ minPoint = bb.Min;
                        XYZ maxPoint = new XYZ(bb.Max.X, bb.Max.Y, 99999);

                        Outline outLine = new Outline(minPoint, maxPoint);

                        BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outLine);

                        IList<Element> ColumnsInZLine = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance))
                            .WherePasses(bbFilter).ToList();

                        foreach (Element currentZlineColumn in ColumnsInZLine)
                        {
                            currentZlineColumn.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set("");
                        }

                    }
                    t.Commit();
                }
            }
            catch
            {
            }
            finally
            {
                RenumberColumnsSelectionUI renumberColumnsWindow = new RenumberColumnsSelectionUI();
                renumberColumnsWindow.ShowDialog();
            }
        }

        static internal IList<ColumnTypesInfo> GetColumTypesInfo()
        {
            IList<ElementId> allUsedColumnTypesIDs = new List<ElementId>();
            IList<ColumnTypesInfo> AllUsedColumnTypesInfo = new List<ColumnTypesInfo>();

            IList<Element> allColumnInstances = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType().ToList();

            foreach (Element currentElement in allColumnInstances)
            {
                if (!allUsedColumnTypesIDs.Contains(currentElement.GetTypeId()))
                {
                    int typeID = currentElement.GetTypeId().IntegerValue;
                    string typeName = currentElement.Name;
                    ColumnTypesInfo currentColumnInfo = new ColumnTypesInfo() { TypeName = typeName, TypeId = typeID, WillBeNumbered = true, TypePrefix = "P" };

                    allUsedColumnTypesIDs.Add(currentElement.GetTypeId());
                    AllUsedColumnTypesInfo.Add(currentColumnInfo);
                }
            }

            return AllUsedColumnTypesInfo;
        }

        static internal IList<LevelInfo> GetAllLevelInfo()
        {
            return Utils.GetInformation.GetAllLevelsInfo(uidoc);
        }

        static internal int GetLastNumberedColumn()
        {
            return Utils.GetInformation.GetLastElementNumbered(BuiltInCategory.OST_StructuralColumns, uidoc.Document);
        }

        private IList<Element> SortColumnsByDistance(IList<Element> targetColumns, Element targetColumn)
        {
            IList<Element> listToReturn = targetColumns.Where(c => c is Element).OrderBy(c =>
            {
                if (c.Location is LocationPoint)
                {
                    if (targetColumn.Location is LocationPoint)
                    {
                        return (c.Location as LocationPoint).Point.DistanceTo((targetColumn.Location as LocationPoint).Point);
                    }
                    else
                    {
                        Curve LCurveFirst = (targetColumn.Location as LocationCurve).Curve;
                        XYZ LCurveFirstFirstPoint = LCurveFirst.GetEndPoint(0);
                        XYZ LCurveFirstSecondPoint = LCurveFirst.GetEndPoint(1);

                        if (LCurveFirstFirstPoint.Z < LCurveFirstSecondPoint.Z)
                        {
                            return (c.Location as LocationPoint).Point.DistanceTo((LCurveFirstFirstPoint));
                        }
                        else
                        {
                            return (c.Location as LocationPoint).Point.DistanceTo((LCurveFirstSecondPoint));
                        }
                    }
                }
                else
                {
                    Curve lCurve = (c.Location as LocationCurve).Curve;
                    XYZ lCurveFirstPoint = lCurve.GetEndPoint(0);
                    XYZ lCurveSecondPoint = lCurve.GetEndPoint(1);

                    if (lCurveFirstPoint.Z < lCurveSecondPoint.Z)
                    {
                        if (targetColumn.Location is LocationPoint)
                        {
                            return (targetColumn.Location as LocationPoint).Point.DistanceTo(lCurveFirstPoint);
                        }
                        else
                        {
                            Curve LCurveFirst = (targetColumn.Location as LocationCurve).Curve;
                            XYZ LCurveFirstFirstPoint = LCurveFirst.GetEndPoint(0);
                            XYZ LCurveFirstSecondPoint = LCurveFirst.GetEndPoint(1);

                            if (LCurveFirstFirstPoint.Z < LCurveFirstSecondPoint.Z)
                            {
                                return (LCurveFirstFirstPoint.DistanceTo(lCurveFirstPoint));
                            }
                            else
                            {
                                return (LCurveFirstSecondPoint.DistanceTo(lCurveFirstPoint));
                            }
                        }
                    }
                    else
                    {
                        if (targetColumn.Location is LocationPoint)
                        {
                            return (targetColumn.Location as LocationPoint).Point.DistanceTo(lCurveSecondPoint);
                        }
                        else
                        {
                            Curve LCurveFirst = (targetColumn.Location as LocationCurve).Curve;
                            XYZ LCurveFirstFirstPoint = LCurveFirst.GetEndPoint(0);
                            XYZ LCurveFirstSecondPoint = LCurveFirst.GetEndPoint(1);

                            if (LCurveFirstFirstPoint.Z < LCurveFirstSecondPoint.Z)
                            {
                                return (LCurveFirstFirstPoint.DistanceTo(lCurveSecondPoint));
                            }
                            else
                            {
                                return (LCurveFirstSecondPoint.DistanceTo(lCurveSecondPoint));
                            }
                        }
                    }
                }
            }).ToList();

            return listToReturn;
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
    }

    [Transaction(TransactionMode.Manual)]
    class RenumberColumnsSelection : IExternalCommand
    {
        static UIDocument uidoc = null;
        static AddInId appId = new AddInId(new Guid("C31111F3-772B-4207-8C1A-891689513361"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (uidoc.ActiveView as ViewPlan == null)
            {
                message = Properties.Messages.RenumberColumns_NotPlanView;
                return Result.Failed;
            }

            XYZ pointOfInterest = new XYZ(-9999, 9999, -9999);

            IList<Element> allColumns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance)).Where(e => (e as FamilyInstance).IsSlantedColumn == false)
            .OrderBy(e => (e.Location as LocationPoint).Point.DistanceTo(pointOfInterest)).ToList();

            if (allColumns.Count == 0)
            {
                message = Properties.Messages.RenumberColumns_NoColumnFamilyLoaded;
                return Result.Failed;
            }

            uidoc = commandData.Application.ActiveUIDocument;

            RenumberColumnsSelectionUI renumberColumnsWindow = new RenumberColumnsSelectionUI();
            renumberColumnsWindow.ShowDialog();

            return Result.Succeeded;
        }

        static internal void DoRenumbering(bool selectColumnRow, int counter)
        {
            try
            {
                Document doc = uidoc.Document;

                string sufix = "";
                string lance = " " + Properties.WindowLanguage.RenumberColumns_LevelLabeler + " ";
                string concatWord = " " + Properties.WindowLanguage.RenumberColumns_LevelConcatenator + " ";

                string space = " ";
                lance = space + ONBOXApplication.columnsLevelIndicator + space;
                concatWord = space + ONBOXApplication.columnsConcatWord + space;

                XYZ pointOfInterest = new XYZ(-9999, 9999, -9999);

                IList<Element> allColumns = new List<Element>();

                if (selectColumnRow)
                {
                    allColumns = uidoc.Selection.PickElementsByRectangle(new ColumnNOTSlantedSelectionFilter(), Properties.Messages.RenumberColumns_SelectTheRowOfColumns);

                    Element firstColumn = doc.GetElement(uidoc.Selection.PickObject(ObjectType.Element, new ColumnNOTSlantedSelectionFilter(), Properties.Messages.RenumberColumns_SelectFirstColumn));
                    XYZ firstPointLocation = (firstColumn.Location as LocationPoint).Point;

                    allColumns = allColumns.Where(e => (e as FamilyInstance) != null).OrderBy(e => (e.Location as LocationPoint).Point.DistanceTo(firstPointLocation)).ToList();
                }
                else
                {
                    allColumns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance)).Where(e => (e as FamilyInstance).IsSlantedColumn == false)
                        .OrderBy(e => (e.Location as LocationPoint).Point.DistanceTo(pointOfInterest)).ToList();
                }

                IList<Element> allLevels = new List<Element>();

                if (ONBOXApplication.storedColumnRenumOrder == ColumnRenumberOrder.Ascending)
                {
                    allLevels = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level)).WhereElementIsNotElementType().OrderBy(l => (l as Level).Elevation).ToList();
                }
                else
                {
                    allLevels = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level)).WhereElementIsNotElementType().OrderByDescending(l => (l as Level).Elevation).ToList();
                }

                IList<ElementId> allUsedColumnsIDs = new List<ElementId>();

                double topLevelHeight = (allLevels.Last() as Level).Elevation;
                double bottomLevelHeight = (allLevels.First() as Level).Elevation;

                using (Transaction t = new Transaction(doc, Properties.Messages.RenumberColumns_Transaction))
                {
                    t.Start();
                    for (int i = 0; i < allLevels.Count; i++)
                    {
                        foreach (Element currentColumn in allColumns)
                        {
                            Element currentColumnBaseLevel = doc.GetElement(currentColumn.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId());

                            if (currentColumnBaseLevel.Id == (allLevels.ElementAt(i) as Level).Id)
                            {
                                if (allUsedColumnsIDs.Contains(currentColumn.Id))
                                {
                                    continue;
                                }

                                ColumnTypesInfo currentColumnTypeInfo = ONBOXApplication.storedColumnTypesInfo.Where(colInfo => colInfo.TypeId == currentColumn.GetTypeId().IntegerValue).First();
                                if (currentColumnTypeInfo.WillBeNumbered == false)
                                    continue;

                                LevelInfo currentLevelInfo = ONBOXApplication.storedColumnLevelInfo.Where(lvlInfo => lvlInfo.levelId == allLevels.ElementAt(i).Id.IntegerValue).First();
                                if (currentLevelInfo.willBeNumbered == false)
                                    continue;

                                Element currenColumnTopLevel = doc.GetElement(currentColumn.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId());
                                allUsedColumnsIDs.Add(currentColumn.Id);

                                string columnBaseSufix = ONBOXApplication.storedColumnLevelInfo.Where(colInfo => colInfo.levelId == currentColumnBaseLevel.Id.IntegerValue).First().levelPrefix;
                                string columnTopSufix = ONBOXApplication.storedColumnLevelInfo.Where(colInfo => colInfo.levelId == currenColumnTopLevel.Id.IntegerValue).First().levelPrefix;

                                if (currenColumnTopLevel.Id != currentColumnBaseLevel.Id)
                                {
                                    sufix = lance + columnBaseSufix + concatWord + columnTopSufix;
                                }
                                else
                                {
                                    sufix = lance + columnBaseSufix;
                                }

                                //the main column
                                currentColumn.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(currentColumnTypeInfo.TypePrefix + counter + sufix);
                                XYZ cLocationPoint = (currentColumn.Location as LocationPoint).Point;

                                // a tiny little boundingbox or outline on the center of the pilar
                                //double offSet = 0.1;
                                //XYZ minPoint = new XYZ(cLocationPoint.X - offSet, cLocationPoint.Y - offSet, topLevelHeight);
                                //XYZ maxPoint = new XYZ(cLocationPoint.X + offSet, cLocationPoint.Y + offSet, bottomLevelHeight);

                                GeometryElement gElem = currentColumn.get_Geometry(new Options());
                                BoundingBoxXYZ bb = gElem.GetBoundingBox();
                                bb.Enabled = true;
                                XYZ minPoint = bb.Min;
                                XYZ maxPoint = new XYZ(bb.Max.X, bb.Max.Y, topLevelHeight);

                                Outline outLine = new Outline(minPoint, maxPoint);

                                BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outLine);

                                //IList<Element> remainingColumns = allColumns.Where(c => (!allUsedColumnsIDs.Contains(c.Id))).ToList();

                                IList<Element> ColumnsInZLine = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance))
                                    .WherePasses(bbFilter).Where(c => (!allUsedColumnsIDs.Contains(c.Id))).ToList();

                                //we will try to find other columns equal to this column and are in the same z line (position) on other levels, so they have the same name
                                for (int l = 0; l < allLevels.Count; l++)
                                {
                                    currentLevelInfo = ONBOXApplication.storedColumnLevelInfo.Where(lvlInfo => lvlInfo.levelId == allLevels.ElementAt(l).Id.IntegerValue).First();
                                    if (currentLevelInfo.willBeNumbered == false)
                                        continue;

                                    //if the current level is the same level as the level of the main column, we dont need to check again
                                    if (l == i)
                                        continue;

                                    Element currentcolumnInLine = null;
                                    if (ColumnsInZLine.Count > 0)
                                    {
                                        IList<Element> columnsInZlineInThisLevel = ColumnsInZLine.Where(column => column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId() == allLevels.ElementAt(l).Id).ToList();
                                        if (columnsInZlineInThisLevel.Count == 1)
                                        {
                                            currentcolumnInLine = columnsInZlineInThisLevel.First();
                                        }
                                        else if (columnsInZlineInThisLevel.Count == 0)
                                        {
                                            continue;
                                        }
                                        else if (columnsInZlineInThisLevel.Count > 1)// if we found more than one column on this level we have to check wich one is more similiar to the main column
                                        {
                                            //we try to find the nearest column to the main one
                                            double nearDist = double.PositiveInfinity;
                                            XYZ mainColumnPosition = (currentColumn.Location as LocationPoint).Point;
                                            foreach (Element currentColumnCandidate in columnsInZlineInThisLevel)
                                            {
                                                XYZ currentCanditatePosition = (currentColumnCandidate.Location as LocationPoint).Point;
                                                double currentDist = mainColumnPosition.DistanceTo(currentCanditatePosition);
                                                if (currentDist < nearDist)
                                                {
                                                    nearDist = currentDist;
                                                    currentcolumnInLine = currentColumnCandidate;
                                                }
                                            }
                                        }

                                        if ((currentcolumnInLine == null) || (currentColumn.Id == currentcolumnInLine.Id))
                                            continue;

                                        currentColumnTypeInfo = ONBOXApplication.storedColumnTypesInfo.Where(colInfo => colInfo.TypeId == currentcolumnInLine.GetTypeId().IntegerValue).First();
                                        if (currentColumnTypeInfo.WillBeNumbered == false)
                                            continue;

                                        currentColumnBaseLevel = doc.GetElement(currentcolumnInLine.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId());
                                        currenColumnTopLevel = doc.GetElement(currentcolumnInLine.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId());

                                        columnBaseSufix = ONBOXApplication.storedColumnLevelInfo.Where(colInfo => colInfo.levelId == currentColumnBaseLevel.Id.IntegerValue).First().levelPrefix;
                                        columnTopSufix = ONBOXApplication.storedColumnLevelInfo.Where(colInfo => colInfo.levelId == currenColumnTopLevel.Id.IntegerValue).First().levelPrefix;

                                        if (currenColumnTopLevel.Id != currentColumnBaseLevel.Id)
                                        {
                                            sufix = lance + columnBaseSufix + concatWord + columnTopSufix;
                                        }
                                        else
                                        {
                                            sufix = lance + columnBaseSufix;
                                        }

                                        allUsedColumnsIDs.Add(currentColumn.Id);
                                        sufix = lance + columnBaseSufix + concatWord + columnTopSufix;
                                        currentcolumnInLine.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set(currentColumnTypeInfo.TypePrefix + counter + sufix);
                                        allUsedColumnsIDs.Add(currentcolumnInLine.Id);
                                    }
                                }
                                counter++;
                            }
                        }
                    }
                    t.Commit();
                }
            }
            catch
            {
            }
            finally
            {
                if (selectColumnRow)
                {
                    RenumberColumnsSelectionUI renumberColumnsWindow = new RenumberColumnsSelectionUI();
                    renumberColumnsWindow.ShowDialog();
                }
            }
        }

        static internal void ClearRenumbering()
        {
            Document doc = uidoc.Document;
            try
            {
                IList<Element> allColumns = uidoc.Selection.PickElementsByRectangle(new ColumnNOTSlantedSelectionFilter(), Properties.Messages.RenumberColumns_SelectTheRowOfColumns);

                using (Transaction t = new Transaction(doc, Properties.Messages.RenumberColumns_RemoveNumbers_Transaction))
                {
                    t.Start();
                    foreach (Element currentColumn in allColumns)
                    {
                        XYZ cLocationPoint = (currentColumn.Location as LocationPoint).Point;
                        GeometryElement gElem = currentColumn.get_Geometry(new Options());
                        BoundingBoxXYZ bb = gElem.GetBoundingBox();
                        bb.Enabled = true;
                        XYZ minPoint = bb.Min;
                        XYZ maxPoint = new XYZ(bb.Max.X, bb.Max.Y, 99999);

                        Outline outLine = new Outline(minPoint, maxPoint);

                        BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outLine);

                        IList<Element> ColumnsInZLine = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance))
                            .WherePasses(bbFilter).ToList();

                        foreach (Element currentZlineColumn in ColumnsInZLine)
                        {
                            currentZlineColumn.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set("");
                        }

                    }
                    t.Commit();
                }
            }
            catch
            {
            }
            finally
            {
                RenumberColumnsSelectionUI renumberColumnsWindow = new RenumberColumnsSelectionUI();
                renumberColumnsWindow.ShowDialog();
            }
        }

        static internal IList<ColumnTypesInfo> GetColumTypesInfo()
        {
            IList<ElementId> allUsedColumnTypesIDs = new List<ElementId>();
            IList<ColumnTypesInfo> AllUsedColumnTypesInfo = new List<ColumnTypesInfo>();

            IList<Element> allColumnInstances = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType().ToList();

            foreach (Element currentElement in allColumnInstances)
            {
                if (!allUsedColumnTypesIDs.Contains(currentElement.GetTypeId()))
                {
                    int typeID = currentElement.GetTypeId().IntegerValue;
                    string typeName = currentElement.Name;
                    ColumnTypesInfo currentColumnInfo = new ColumnTypesInfo() { TypeName = typeName, TypeId = typeID, WillBeNumbered = true, TypePrefix = "P" };

                    allUsedColumnTypesIDs.Add(currentElement.GetTypeId());
                    AllUsedColumnTypesInfo.Add(currentColumnInfo);
                }
            }

            return AllUsedColumnTypesInfo;
        }

        static internal IList<LevelInfo> GetAllLevelInfo()
        {
            return Utils.GetInformation.GetAllLevelsInfo(uidoc);
        }

        static internal int GetLastNumberedColumn()
        {
            return Utils.GetInformation.GetLastElementNumbered(BuiltInCategory.OST_StructuralColumns, uidoc.Document);
        }

        private IList<Element> SortColumnsByDistance(IList<Element> targetColumns, Element targetColumn)
        {
            IList<Element> listToReturn = targetColumns.Where(c => c is Element).OrderBy(c =>
            {
                if (c.Location is LocationPoint)
                {
                    if (targetColumn.Location is LocationPoint)
                    {
                        return (c.Location as LocationPoint).Point.DistanceTo((targetColumn.Location as LocationPoint).Point);
                    }
                    else
                    {
                        Curve LCurveFirst = (targetColumn.Location as LocationCurve).Curve;
                        XYZ LCurveFirstFirstPoint = LCurveFirst.GetEndPoint(0);
                        XYZ LCurveFirstSecondPoint = LCurveFirst.GetEndPoint(1);

                        if (LCurveFirstFirstPoint.Z < LCurveFirstSecondPoint.Z)
                        {
                            return (c.Location as LocationPoint).Point.DistanceTo((LCurveFirstFirstPoint));
                        }
                        else
                        {
                            return (c.Location as LocationPoint).Point.DistanceTo((LCurveFirstSecondPoint));
                        }
                    }
                }
                else
                {
                    Curve lCurve = (c.Location as LocationCurve).Curve;
                    XYZ lCurveFirstPoint = lCurve.GetEndPoint(0);
                    XYZ lCurveSecondPoint = lCurve.GetEndPoint(1);

                    if (lCurveFirstPoint.Z < lCurveSecondPoint.Z)
                    {
                        if (targetColumn.Location is LocationPoint)
                        {
                            return (targetColumn.Location as LocationPoint).Point.DistanceTo(lCurveFirstPoint);
                        }
                        else
                        {
                            Curve LCurveFirst = (targetColumn.Location as LocationCurve).Curve;
                            XYZ LCurveFirstFirstPoint = LCurveFirst.GetEndPoint(0);
                            XYZ LCurveFirstSecondPoint = LCurveFirst.GetEndPoint(1);

                            if (LCurveFirstFirstPoint.Z < LCurveFirstSecondPoint.Z)
                            {
                                return (LCurveFirstFirstPoint.DistanceTo(lCurveFirstPoint));
                            }
                            else
                            {
                                return (LCurveFirstSecondPoint.DistanceTo(lCurveFirstPoint));
                            }
                        }
                    }
                    else
                    {
                        if (targetColumn.Location is LocationPoint)
                        {
                            return (targetColumn.Location as LocationPoint).Point.DistanceTo(lCurveSecondPoint);
                        }
                        else
                        {
                            Curve LCurveFirst = (targetColumn.Location as LocationCurve).Curve;
                            XYZ LCurveFirstFirstPoint = LCurveFirst.GetEndPoint(0);
                            XYZ LCurveFirstSecondPoint = LCurveFirst.GetEndPoint(1);

                            if (LCurveFirstFirstPoint.Z < LCurveFirstSecondPoint.Z)
                            {
                                return (LCurveFirstFirstPoint.DistanceTo(lCurveSecondPoint));
                            }
                            else
                            {
                                return (LCurveFirstSecondPoint.DistanceTo(lCurveSecondPoint));
                            }
                        }
                    }
                }
            }).ToList();

            return listToReturn;
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
    }

}
