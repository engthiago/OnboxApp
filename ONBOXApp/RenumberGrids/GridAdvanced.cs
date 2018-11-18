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
    enum GridOrientation { Horizontal, Vertical }

    [Transaction(TransactionMode.Manual)]
    class RenumberGridsAdvanced : IExternalCommand
    {
        enum GridLength { Smaller, Equal, Bigger }

        private UIDocument uidoc = null;
        static internal bool isVerticalGridsNumbered = true;
        static internal bool canUseSubNumering = true;
        string WholeName = "A";
        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513329"));

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            IList<Element> allGrids = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Grids).WhereElementIsNotElementType().ToElements();
            IList<Element> allMultiGrids = new FilteredElementCollector(doc).OfClass(typeof(MultiSegmentGrid)).ToElements();
            IList<ElementId> allMultiGridsIDs = new List<ElementId>();
            IList<Grid> allSingleGrids = new List<Grid>();
            IList<string> allGridNames = new List<string>();

            try
            {
                if (allGrids.Count == 0)
                {
                    message = Properties.Messages.RenumberGrids_NoGrids;
                    return Result.Failed;
                }

                RenumberGridsAdvUI currentGridUI = new RenumberGridsAdvUI(GetAllGridsInfo(), this);
                currentGridUI.ShowDialog();
                if (currentGridUI.DialogResult == false)
                {
                    return Result.Cancelled;
                }

            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
                return Result.Cancelled;
            }
            return Result.Succeeded;

        }

        internal void RenumberProcess(IList<GridInfo> targetGridInfoList)
        {
            using (TransactionGroup tg = new TransactionGroup(uidoc.Document, Properties.Messages.RenumberGrids_Transaction))
            {
                tg.Start();
                using (Transaction t0 = new Transaction(uidoc.Document, Properties.Messages.RenumberGrids_Transaction_AdjustNames))
                {
                    t0.Start();
                    IList<Element> allGrids = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_Grids).WhereElementIsNotElementType().ToElements();
                    for (int i = 0; i < allGrids.Count; i++)
                    {
                        Grid currentGrid = allGrids.ElementAt(i) as Grid;
                        currentGrid.Name = string.Format("ONBOX {0}", i.ToString());
                    }
                    t0.Commit();
                }

                using (Transaction t1 = new Transaction(uidoc.Document, Properties.Messages.RenumberGrids_Transaction))
                {
                    t1.Start();
                    ApplyTheRenumberingOnTheActualGrids(targetGridInfoList);
                    t1.Commit();
                }
                tg.Assimilate();
            }
        }

        internal void ApplyTheRenumberingOnTheActualGrids(IList<GridInfo> targetGridInfoList)
        {
            IList<Element> allMultiGrids = new FilteredElementCollector(uidoc.Document).OfClass(typeof(MultiSegmentGrid)).ToElements();
            IList<ElementId> allMultiGridsIDs = new List<ElementId>();

            foreach (Element currentMultiGridElement in allMultiGrids)
            {
                MultiSegmentGrid currentMultiGrid = currentMultiGridElement as MultiSegmentGrid;
                if (currentMultiGrid == null) continue;
                allMultiGridsIDs = allMultiGridsIDs.Union(currentMultiGrid.GetGridIds()).ToList();
            }

            foreach (GridInfo currentGridInfo in targetGridInfoList)
            {
                bool isCurrentGridPartOfMultiGridItem = false;
                Grid currentGrid = uidoc.Document.GetElement(new ElementId(currentGridInfo.Id)) as Grid;

                if (currentGrid == null)
                {
                    isCurrentGridPartOfMultiGridItem = false;
                    continue;
                }

                if (allMultiGridsIDs.Contains(currentGrid.Id))
                    isCurrentGridPartOfMultiGridItem = true;

                if (isCurrentGridPartOfMultiGridItem)
                {
                    ElementId multisegGridID = MultiSegmentGrid.GetMultiSegementGridId(currentGrid);
                    MultiSegmentGrid multiSegGrid = uidoc.Document.GetElement(multisegGridID) as MultiSegmentGrid;
                    if (multiSegGrid == null) continue;

                    foreach (ElementId currentMultiSegGridMemberID in multiSegGrid.GetGridIds())
                    {
                        Grid currentMultiSegGrid = uidoc.Document.GetElement(currentMultiSegGridMemberID) as Grid;
                        if (currentMultiSegGrid == null) continue;
                        currentMultiSegGrid.Name = currentGridInfo.newName;
                    }

                    multiSegGrid.Name = currentGridInfo.newName;
                }
                else
                {
                    if (currentGrid == null) continue;
                    currentGrid.Name = currentGridInfo.newName;
                }

            }
        }

        internal IList<GridInfo> GetAllGridsInfo()
        {
            IList<GridInfo> gridsInformation = new List<GridInfo>();

            if (uidoc != null)
            {
                IList<Element> allGrids = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_Grids).WhereElementIsNotElementType().ToElements();
                IList<Element> allMultiGrids = new FilteredElementCollector(uidoc.Document).OfClass(typeof(MultiSegmentGrid)).ToElements();

                IList<ElementId> allMultiGridsIds = new List<ElementId>();

                foreach (Element currentMultiGrid in allMultiGrids)
                {
                    ICollection<ElementId> currentMultiGridIds = (currentMultiGrid as MultiSegmentGrid).GetGridIds();
                    currentMultiGridIds.Remove(currentMultiGridIds.FirstOrDefault());
                    allMultiGridsIds = allMultiGridsIds.Union(currentMultiGridIds).ToList();
                }

                IList<GridInfo> allHorizontalGridsInfo = new List<GridInfo>();
                IList<GridInfo> allVerticalGridsInfo = new List<GridInfo>();

                foreach (Element currentGridElement in allGrids)
                {
                    if (allMultiGridsIds.Contains(currentGridElement.Id))
                        continue;

                    Grid currentGrid = currentGridElement as Grid;
                    string currentGridOrientation = "";

                    if (VerifyGridOrientation(currentGrid) == GridOrientation.Horizontal)
                    {
                        currentGridOrientation = "Horizontal";
                        allHorizontalGridsInfo.Add(new GridInfo() { Id = currentGrid.Id.IntegerValue, newName = "", prevName = currentGrid.Name, orientation = currentGridOrientation });
                    }
                    else
                    {
                        currentGridOrientation = "Vertical";
                        allVerticalGridsInfo.Add(new GridInfo() { Id = currentGrid.Id.IntegerValue, newName = "", prevName = currentGrid.Name, orientation = currentGridOrientation });
                    }
                }

                allHorizontalGridsInfo = allHorizontalGridsInfo.Where(g => g is GridInfo).OrderByDescending(g => (uidoc.Document.GetElement(new ElementId(g.Id)) as Grid).Curve.GetEndPoint(0).Y).ToList();
                allVerticalGridsInfo = allVerticalGridsInfo.Where(g => g is GridInfo).OrderBy(g => (uidoc.Document.GetElement(new ElementId(g.Id)) as Grid).Curve.GetEndPoint(0).X).ToList();

                gridsInformation = allHorizontalGridsInfo.Union(allVerticalGridsInfo).ToList();

            }

            return gridsInformation;
        }

        internal IList<GridInfo> RenumberTable(IList<GridInfo> allOrganizedSingleGrids)
        {
            IList<GridInfo> allOrganizedHorizontalGrids = new List<GridInfo>();
            IList<GridInfo> allOrganizedVerticalGrids = new List<GridInfo>();
            try
            {
                foreach (GridInfo currentGrid in allOrganizedSingleGrids)
                {
                    if (currentGrid.orientation == "Horizontal")
                        allOrganizedHorizontalGrids.Add(currentGrid);
                    else
                        allOrganizedVerticalGrids.Add(currentGrid);
                }

                IList<Element> allMultiGrids = new FilteredElementCollector(uidoc.Document).OfClass(typeof(MultiSegmentGrid)).ToElements();
                IList<ElementId> allMultiGridsIDs = new List<ElementId>();
                foreach (Element e in allMultiGrids)
                {
                    MultiSegmentGrid currentMultiSegGrid = e as MultiSegmentGrid;
                    allMultiGridsIDs.Add(currentMultiSegGrid.Id);
                }

                bool isNumber = isVerticalGridsNumbered;
                doTheRenumberingOrLettering(uidoc.Document, allOrganizedVerticalGrids, allMultiGridsIDs, isNumber);

                isNumber = isNumber ? false : true;
                doTheRenumberingOrLettering(uidoc.Document, allOrganizedHorizontalGrids, allMultiGridsIDs, isNumber);

            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
            }

            return allOrganizedHorizontalGrids.Union(allOrganizedVerticalGrids).ToList();
        }

        private void doTheRenumberingOrLettering(Document doc, IList<GridInfo> targetListOfGrids, IList<ElementId> allMultiGridsIDs, bool isNumber)
        {
            Grid prevGrid = null;
            bool prevGridIsSub = false;
            int counter = 0;
            int subCounter = 1;
            char letter = 'A';
            char subLetter = 'a';
            WholeName = "A";


            double currentGridLength = 0;
            double prevGridLength = 0;

            IList<ElementId> allMultiGridSubGridsIDs = new List<ElementId>();

            foreach (ElementId currentMultiGridId in allMultiGridsIDs)
            {
                allMultiGridSubGridsIDs = allMultiGridSubGridsIDs.Union((uidoc.Document.GetElement(currentMultiGridId) as MultiSegmentGrid).GetGridIds()).ToList();
            }

            foreach (GridInfo currentGridInfo in targetListOfGrids)
            {
                Grid currentGrid = null;

                currentGrid = uidoc.Document.GetElement(new ElementId(currentGridInfo.Id)) as Grid;

                if (allMultiGridSubGridsIDs.Contains(new ElementId(currentGridInfo.Id)))
                {
                    MultiSegmentGrid currentMultiSegGrid = doc.GetElement(MultiSegmentGrid.GetMultiSegementGridId(currentGrid)) as MultiSegmentGrid;

                    currentGridLength = getMultiGridLengthSameOrientationMainGrid(doc, currentMultiSegGrid, currentGrid);
                }
                else
                {
                    currentGridLength = getTheoricalLengthOfTheGrid(currentGrid);
                }
                
                if (prevGrid != null)
                {
                    if (allMultiGridsIDs.Contains(MultiSegmentGrid.GetMultiSegementGridId(prevGrid)))
                    {
                        MultiSegmentGrid PrevMultiSegGrid = doc.GetElement(MultiSegmentGrid.GetMultiSegementGridId(prevGrid)) as MultiSegmentGrid;
                        prevGridLength = getMultiGridLengthSameOrientationMainGrid(doc, PrevMultiSegGrid, prevGrid);
                    }
                    else
                    {
                        prevGridLength = getTheoricalLengthOfTheGrid(prevGrid);
                    }
                    GridInfo prevGridInfo = targetListOfGrids.Where(g => g.Id == prevGrid.Id.IntegerValue).FirstOrDefault();
                    if (canUseSubNumering)
                    {
                        if (CompareGridLength(prevGridLength, currentGridLength) == GridLength.Equal)
                        {
                            if (prevGridIsSub == false)
                            {
                                if (isNumber)
                                    currentGridInfo.newName = increaseWholeNumber(doc, currentGridInfo, ref counter, ref subCounter, ref prevGridIsSub);
                                else
                                    currentGridInfo.newName = increaseWholeLetter(doc, currentGridInfo, ref letter, ref subLetter, ref prevGridIsSub);
                            }
                            if (prevGridIsSub == true)
                            {
                                if (isNumber)
                                    currentGridInfo.newName = increaseSubNumber(doc, currentGridInfo, ref prevGridInfo, ref subCounter, ref prevGridIsSub);
                                else
                                    currentGridInfo.newName = increaseSubLetter(doc, currentGridInfo, ref prevGridInfo, ref subLetter, ref prevGridIsSub);
                            }
                        }
                        if (CompareGridLength(prevGridLength, currentGridLength) == GridLength.Bigger)
                        {
                            if (isNumber)
                                currentGridInfo.newName = increaseSubNumber(doc, currentGridInfo, ref prevGridInfo, ref subCounter, ref prevGridIsSub);
                            else
                                currentGridInfo.newName = increaseSubLetter(doc, currentGridInfo, ref prevGridInfo, ref subLetter, ref prevGridIsSub);
                        }
                        if (CompareGridLength(prevGridLength, currentGridLength) == GridLength.Smaller)
                        {
                            if (isNumber)
                                currentGridInfo.newName = increaseWholeNumber(doc, currentGridInfo, ref counter, ref subCounter, ref prevGridIsSub);
                            else
                                currentGridInfo.newName = increaseWholeLetter(doc, currentGridInfo, ref letter, ref subLetter, ref prevGridIsSub);
                        }
                    }
                    else
                    {
                        if (isNumber)
                            currentGridInfo.newName = increaseWholeNumber(doc, currentGridInfo, ref counter, ref subCounter, ref prevGridIsSub);
                        else
                            currentGridInfo.newName = increaseWholeLetter(doc, currentGridInfo, ref letter, ref subLetter, ref prevGridIsSub);
                    }
                }
                else
                {
                    if (isNumber)
                        currentGridInfo.newName = increaseWholeNumber(doc, currentGridInfo, ref counter, ref subCounter, ref prevGridIsSub);
                    else
                        currentGridInfo.newName = increaseWholeLetter(doc, currentGridInfo, ref letter, ref subLetter, ref prevGridIsSub);
                }
                prevGrid = currentGrid;
            }
        }

        //get the total length of the multisegment grid providing one of the grids of the multiseg grid
        private double getMultiGridTotalLength(Document doc, Grid targetGrid)
        {
            MultiSegmentGrid currentMultiSegGrid = doc.GetElement(MultiSegmentGrid.GetMultiSegementGridId(targetGrid)) as MultiSegmentGrid;
            double totalLength = 0;
            foreach (ElementId eID in currentMultiSegGrid.GetGridIds())
            {
                Grid currentGrid = doc.GetElement(eID) as Grid;
                totalLength += getTheoricalLengthOfTheGrid(currentGrid);
            }
            return totalLength;
        }

        //get the total length of the multisegment grid providing the multisegment grid
        private double getMultiGridTotalLength(Document doc, MultiSegmentGrid targetMultiSegGrid)
        {
            double totalLength = 0;
            foreach (ElementId eID in targetMultiSegGrid.GetGridIds())
            {
                Grid currentGrid = doc.GetElement(eID) as Grid;
                totalLength += getTheoricalLengthOfTheGrid(currentGrid);
            }
            return totalLength;
        }

        //get the length of only the Grids that have the same orientation that the main grid
        private double getMultiGridLengthSameOrientationMainGrid(Document doc, MultiSegmentGrid targetMultiSegGrid, Grid targetMainGrid)
        {
            double totalLength = 0;
            foreach (ElementId eID in targetMultiSegGrid.GetGridIds())
            {
                Grid currentMultiSegGrid = doc.GetElement(eID) as Grid;
                if (VerifyGridOrientation(currentMultiSegGrid) == VerifyGridOrientation(targetMainGrid))
                {
                    Grid currentGrid = doc.GetElement(eID) as Grid;
                    totalLength += getTheoricalLengthOfTheGrid(currentGrid);
                }
            }
            return totalLength;
        }

        //get the length of a line or curved based grid, if the grid is curved it will calculate the distance bettween the 2 points of the grid in a straight line (makes more sense for grids)
        private double getTheoricalLengthOfTheGrid(Grid targetGrid)
        {
            double theoricalLength;
            if (targetGrid.Curve is Line)
            {
                theoricalLength = targetGrid.Curve.ApproximateLength;
            }
            else
            {
                theoricalLength = targetGrid.Curve.GetEndPoint(0).DistanceTo(targetGrid.Curve.GetEndPoint(1));
            }
            return theoricalLength;
        }

        //verify which orientation the specified grid is
        private GridOrientation VerifyGridOrientation(Grid targetGrid)
        {
            Curve gridCurve = targetGrid.Curve;
            XYZ direction = ((gridCurve.GetEndPoint(1) - gridCurve.GetEndPoint(0)).Normalize());
            double angle = Utils.ConvertM.radiansToDegrees(direction.AngleOnPlaneTo(XYZ.BasisX, XYZ.BasisZ));
            //Debug.Print(currentGrid.Name + " " + direction.ToString() + " " + angle);

            if ((angle >= 315) && (angle <= 360.01))
            {
                //HorizontalGrids.Add(currentGrid);
                return GridOrientation.Horizontal;
            }
            if ((angle >= 0) && (angle <= 45.01))
            {
                //HorizontalGrids.Add(currentGrid);
                return GridOrientation.Horizontal;
            }
            if ((angle >= 135) && (angle <= 225.01))
            {
                //HorizontalGrids.Add(currentGrid);
                return GridOrientation.Horizontal;
            }

            if ((angle > 45.01) && (angle < 135))
            {
                //VerticalGrids.Add(currentGrid);
                return GridOrientation.Vertical;
            }

            if ((angle > 225.01) && (angle < 315))
            {
                //VerticalGrids.Add(currentGrid);
                return GridOrientation.Vertical;
            }
            return GridOrientation.Vertical;
        }

        //verify if one grid is smaller, equal or bigger than other
        private GridLength CompareGridLength(double prev, double current)
        {
            if (((current - prev) < 0.01) && ((current - prev) > -0.01))
            {
                return GridLength.Equal;
            }
            else if ((current - prev) < 0.01)
            {
                return GridLength.Bigger;
            }
            else
            {
                return GridLength.Smaller;
            }
        }

        //increase the whole number of a grid
        private string increaseWholeNumber(Document doc, GridInfo currentGridInfo, ref int counter, ref int subCounter, ref bool prevGridIsSub)
        {
            currentGridInfo.newName = (counter + 1).ToString();
            subCounter = 1;
            counter++;
            prevGridIsSub = false;
            return currentGridInfo.newName;
        }
        
        //increase the whole letter of a grid
        private string increaseWholeLetter(Document doc, GridInfo currentGridInfo, ref char letter, ref char subLetter, ref bool prevGridIsSub)
        {
            prevGridIsSub = false;
            currentGridInfo.newName = WholeName;
            subLetter = 'a';
            if (letter != 90)
            {
                letter++;
                WholeName = WholeName.Remove(WholeName.Length - 1) + letter.ToString();
            }
            else
            {
                letter = 'A';
                char first = WholeName.First();
                if (first == 90)
                {
                    WholeName = "A" + letter.ToString();
                }
                else
                {
                    first++;
                    WholeName = WholeName.Remove(0);
                    WholeName = first.ToString() + WholeName + letter.ToString();
                }
            }
            return currentGridInfo.newName;
        }

        //increase the sub number of a grid
        private string increaseSubNumber(Document doc, GridInfo currentGridInfo, ref GridInfo prevGridInfo, ref int subCounter, ref bool prevGridIsSub)
        {
            //TODO verify nex line, is it correct to pick just the first char of the string?
            currentGridInfo.newName = prevGridInfo.newName.ElementAt(0).ToString() + "." + subCounter;
            prevGridIsSub = true;
            subCounter++;
            return currentGridInfo.newName;
        }

        //increase the sub letter of a grid
        private string increaseSubLetter(Document doc, GridInfo currentGridInfo, ref GridInfo prevGridInfo, ref char subLetter, ref bool prevGridIsSub)
        {

            //TODO verify nex line, is it correct to pick just the first char of the string?
            currentGridInfo.newName = prevGridInfo.newName.ElementAt(0).ToString() + "." + subLetter;
            prevGridIsSub = true;
            subLetter++;
            return currentGridInfo.newName;
        }
    }
}