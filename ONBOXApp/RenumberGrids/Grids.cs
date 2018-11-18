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
    [Transaction(TransactionMode.Manual)]
    class RenumberGrids : IExternalCommand
    {
        static internal bool isVerticalGridsNumbered = true;
        static internal bool canUseSubNumering = true;
        string WholeName = "A";
        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513353"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
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
                    message = "Não foi possível encontrar eixos no projeto, por favor crie-os e rode o comando novamente";
                    return Result.Failed;
                }

                RenumberGridsUI currentGridUI = new RenumberGridsUI();
                currentGridUI.ShowDialog();
                if (currentGridUI.DialogResult == false)
                {
                    return Result.Cancelled;
                }

                foreach (Element e in allMultiGrids)
                {
                    MultiSegmentGrid currentMultiSegGrid = e as MultiSegmentGrid;
                    allMultiGridsIDs.Add(currentMultiSegGrid.Id);
                }

                foreach (Element e in allGrids)
                {
                    Grid currentGrid = e as Grid;
                    if (allGridNames.Contains(currentGrid.Name))
                        continue;
                    allGridNames.Add(currentGrid.Name);
                    allSingleGrids.Add(currentGrid);
                }

                IList<Grid> allOrganizedSingleGrids = new List<Grid>();

                foreach (Grid currentGrid in allSingleGrids)
                {
                    if (allMultiGridsIDs.Contains(MultiSegmentGrid.GetMultiSegementGridId(currentGrid)))
                        continue;
                    allOrganizedSingleGrids.Add(currentGrid);
                }

                IList<Grid> allOrganizedHorizontalGrids = new List<Grid>();
                IList<Grid> allOrganizedVerticalGrids = new List<Grid>();

                IList<Grid> allOrganizedHorizontalMainGridsFromMultiGrids = new List<Grid>();
                IList<Grid> allOrganizedVerticalMainGridsFromMultiGrids = new List<Grid>();

                foreach (Grid currentGrid in allOrganizedSingleGrids)
                {
                    if (verifyGridOrientation(currentGrid) == "isVertical")
                    {
                        allOrganizedVerticalGrids.Add(currentGrid);
                    }
                    else
                    {
                        allOrganizedHorizontalGrids.Add(currentGrid);
                    }
                }

                foreach (Element e in allMultiGrids)
                {
                    MultiSegmentGrid curentMultiGrid = e as MultiSegmentGrid;
                    IList<ElementId> GridsIds = curentMultiGrid.GetGridIds().ToList();
                    IList<Curve> gridCurves = new List<Curve>();
                    Grid MainGrid = null;

                    foreach (ElementId eID in GridsIds)
                    {
                        gridCurves.Add((doc.GetElement(eID) as Grid).Curve);
                    }

                    XYZ farAwayPoint = new XYZ(9999999, -9999999, 9999999);
                    gridCurves = gridCurves.Where(c => c is Curve).OrderBy(p => p.Distance(farAwayPoint)).ToList();

                    Curve lastCurve = gridCurves.Last();

                    //Organize the lists to the order of positioning
                    //I dont know why, but I had to put a "where" query that always pass just to make "OrderBy" and "OrderByDescending" work
                    ElementId MainGridID = allGrids.Where(g => g is Grid).OrderBy(d => lastCurve.Distance((d as Grid).Curve.GetEndPoint(1))).First().Id;

                    MainGrid = doc.GetElement(MainGridID) as Grid;

                    if (verifyGridOrientation(MainGrid) == "isVertical")
                    {
                        allOrganizedVerticalMainGridsFromMultiGrids.Add(MainGrid);
                    }
                    else
                    {
                        allOrganizedHorizontalMainGridsFromMultiGrids.Add(MainGrid);
                    }

                }

                //Organize the lists to the order of positioning
                //I dont know why, but I had to put a "where" query that always pass just to make "OrderBy" and "OrderByDescending" work
                allOrganizedHorizontalGrids = allOrganizedHorizontalGrids.Union(allOrganizedHorizontalMainGridsFromMultiGrids).Where(g => g is Grid).OrderByDescending(f => f.Curve.GetEndPoint(0).Y).ToList();
                allOrganizedVerticalGrids = allOrganizedVerticalGrids.Union(allOrganizedVerticalMainGridsFromMultiGrids).Where(g => g is Grid).OrderBy(f => f.Curve.GetEndPoint(0).X).ToList();

                using (TransactionGroup tG = new TransactionGroup(doc, "Renumerar Eixos"))
                {
                    tG.Start();

                    using (Transaction RenameAllGrids = new Transaction(doc, "Ajustar Nomes"))
                    {
                        RenameAllGrids.Start();
                        for (int i = 0; i < allGrids.Count; i++)
                        {
                            Grid currentGrid = allGrids.ElementAt(i) as Grid;
                            currentGrid.Name = string.Format("ONBOX {0}", i.ToString());
                        }
                        RenameAllGrids.Commit();
                    }

                    using (Transaction RenumberVerticalGrids = new Transaction(doc, "Renumerar Eixos Verticais"))
                    {
                        RenumberVerticalGrids.Start();

                        bool isNumber = true;

                        if (isVerticalGridsNumbered == false)
                            isNumber = false;

                        doTheRenumberingOrLettering(doc, allOrganizedVerticalGrids, allOrganizedVerticalMainGridsFromMultiGrids, allMultiGridsIDs, isNumber);

                        RenumberVerticalGrids.Commit();

                    }

                    using (Transaction RenumberHorizontalGrids = new Transaction(doc, "Renumerar Eixos Verticais"))
                    {
                        RenumberHorizontalGrids.Start();

                        bool isNumber = false;

                        if (isVerticalGridsNumbered == false)
                            isNumber = true;

                        doTheRenumberingOrLettering(doc, allOrganizedHorizontalGrids, allOrganizedHorizontalMainGridsFromMultiGrids, allMultiGridsIDs, isNumber);

                        RenumberHorizontalGrids.Commit();
                    }

                    using (Transaction regenerate = new Transaction(doc, "Regen"))
                    {
                        regenerate.Start();
                        doc.Regenerate();
                        uidoc.RefreshActiveView();
                        regenerate.Commit();
                    }

                    tG.Assimilate();
                }
                Debug.Print("end");
            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
                return Result.Cancelled;
            }
            return Result.Succeeded;

        }

        private void doTheRenumberingOrLettering(Document doc, IList<Grid> targetListOfGrids, IList<Grid> targetListOfGridsFromMultiSegGrids, IList<ElementId> allMultiGridsIDs, bool isNumber)
        {
            Grid prevGrid = null;
            bool prevGridIsSub = false;
            int counter = 0;
            int subCounter = 1;
            char letter = 'A';
            char subLetter = 'a';


            double currentGridLength = 0;
            double prevGridLength = 0;

            foreach (Grid currentGrid in targetListOfGrids)
            {
                if (targetListOfGridsFromMultiSegGrids.Contains(currentGrid))
                {
                    MultiSegmentGrid currentMultiSegGrid = doc.GetElement(MultiSegmentGrid.GetMultiSegementGridId(currentGrid)) as MultiSegmentGrid;
                    currentGridLength = getMultiGridLengthSameOrientationMainGrid(doc, currentMultiSegGrid, currentGrid);

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
                        if (canUseSubNumering)
                        {
                            if (isBiggerEqualSmaller(prevGridLength, currentGridLength) == "isEqual")
                            {
                                if (prevGridIsSub == false)
                                {
                                    if (isNumber)
                                        increaseWholeNumber(doc, ref currentMultiSegGrid, ref counter, ref subCounter, ref prevGridIsSub);
                                    else
                                        increaseWholeLetter(doc, ref currentMultiSegGrid, ref letter, ref subLetter, ref prevGridIsSub);
                                }
                                if (prevGridIsSub == true)
                                {
                                    if (isNumber)
                                        increaseSubNumber(doc, ref currentMultiSegGrid, ref prevGrid, ref subCounter, ref prevGridIsSub);
                                    else
                                        increaseSubLetter(doc, ref currentMultiSegGrid, ref prevGrid, ref subLetter, ref prevGridIsSub);
                                }
                            }
                            if (isBiggerEqualSmaller(prevGridLength, currentGridLength) == "isBigger")
                            {
                                if (isNumber)
                                    increaseSubNumber(doc, ref currentMultiSegGrid, ref prevGrid, ref subCounter, ref prevGridIsSub);
                                else
                                    increaseSubLetter(doc, ref currentMultiSegGrid, ref prevGrid, ref subLetter, ref prevGridIsSub);
                            }
                            if (isBiggerEqualSmaller(prevGridLength, currentGridLength) == "isSmaller")
                            {
                                if (isNumber)
                                    increaseWholeNumber(doc, ref currentMultiSegGrid, ref counter, ref subCounter, ref prevGridIsSub);
                                else
                                    increaseWholeLetter(doc, ref currentMultiSegGrid, ref letter, ref subLetter, ref prevGridIsSub);
                            }
                        }
                        else
                        {
                            if (isNumber)
                                increaseWholeNumber(doc, ref currentMultiSegGrid, ref counter, ref subCounter, ref prevGridIsSub);
                            else
                                increaseWholeLetter(doc, ref currentMultiSegGrid, ref letter, ref subLetter, ref prevGridIsSub);
                        }
                    }
                    else
                    {
                        if (isNumber)
                            increaseWholeNumber(doc, ref currentMultiSegGrid, ref counter, ref subCounter, ref prevGridIsSub);
                        else
                            increaseWholeLetter(doc, ref currentMultiSegGrid, ref letter, ref subLetter, ref prevGridIsSub);
                    }
                }
                else
                {
                    currentGridLength = getTheoricalLengthOfTheGrid(currentGrid);

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
                        if (canUseSubNumering)
                        {
                            if (isBiggerEqualSmaller(prevGridLength, currentGridLength) == "isEqual")
                            {
                                if (prevGridIsSub == false)
                                {
                                    if (isNumber)
                                        currentGrid.Name = increaseWholeNumber(doc, currentGrid, ref counter, ref subCounter, ref prevGridIsSub);
                                    else
                                        currentGrid.Name = increaseWholeLetter(doc, currentGrid, ref letter, ref subLetter, ref prevGridIsSub);
                                }
                                if (prevGridIsSub == true)
                                {
                                    if (isNumber)
                                        currentGrid.Name = increaseSubNumber(doc, currentGrid, ref prevGrid, ref subCounter, ref prevGridIsSub);
                                    else
                                        currentGrid.Name = increaseSubLetter(doc, currentGrid, ref prevGrid, ref subLetter, ref prevGridIsSub);
                                }
                            }
                            if (isBiggerEqualSmaller(prevGridLength, currentGridLength) == "isBigger")
                            {
                                if (isNumber)
                                    currentGrid.Name = increaseSubNumber(doc, currentGrid, ref prevGrid, ref subCounter, ref prevGridIsSub);
                                else
                                    currentGrid.Name = increaseSubLetter(doc, currentGrid, ref prevGrid, ref subLetter, ref prevGridIsSub);
                            }
                            if (isBiggerEqualSmaller(prevGridLength, currentGridLength) == "isSmaller")
                            {
                                if (isNumber)
                                    currentGrid.Name = increaseWholeNumber(doc, currentGrid, ref counter, ref subCounter, ref prevGridIsSub);
                                else
                                    currentGrid.Name = increaseWholeLetter(doc, currentGrid, ref letter, ref subLetter, ref prevGridIsSub);
                            }
                        }
                        else
                        {
                            if (isNumber)
                                currentGrid.Name = increaseWholeNumber(doc, currentGrid, ref counter, ref subCounter, ref prevGridIsSub);
                            else
                                currentGrid.Name = increaseWholeLetter(doc, currentGrid, ref letter, ref subLetter, ref prevGridIsSub);
                        }
                    }
                    else
                    {
                        if (isNumber)
                            currentGrid.Name = increaseWholeNumber(doc, currentGrid, ref counter, ref subCounter, ref prevGridIsSub);
                        else
                            currentGrid.Name = increaseWholeLetter(doc, currentGrid, ref letter, ref subLetter, ref prevGridIsSub);
                    }
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
                if (verifyGridOrientation(currentMultiSegGrid) == verifyGridOrientation(targetMainGrid))
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

        //verify which orientation the specify grid is
        private string verifyGridOrientation(Grid targetGrid)
        {
            Curve gridCurve = targetGrid.Curve;
            XYZ direction = ((gridCurve.GetEndPoint(1) - gridCurve.GetEndPoint(0)).Normalize());
            double angle = Utils.ConvertM.radiansToDegrees(direction.AngleOnPlaneTo(XYZ.BasisX, XYZ.BasisZ));
            //Debug.Print(currentGrid.Name + " " + direction.ToString() + " " + angle);

            if ((angle >= 315) && (angle <= 360.01))
            {
                //HorizontalGrids.Add(currentGrid);
                return "isHorizontal";
            }
            if ((angle >= 0) && (angle <= 45.01))
            {
                //HorizontalGrids.Add(currentGrid);
                return "isHorizontal";
            }
            if ((angle >= 135) && (angle <= 225.01))
            {
                //HorizontalGrids.Add(currentGrid);
                return "isHorizontal";
            }

            if ((angle > 45.01) && (angle < 135))
            {
                //VerticalGrids.Add(currentGrid);
                return "isVertical";
            }

            if ((angle > 225.01) && (angle < 315))
            {
                //VerticalGrids.Add(currentGrid);
                return "isVertical";
            }
            return "isVertical";
        }

        //verify if one grid is smaller, equal or bigger than other
        private string isBiggerEqualSmaller(double prev, double current)
        {
            if (((current - prev) < 0.01) && ((current - prev) > -0.01))
            {
                return "isEqual";
            }
            else if ((current - prev) < 0.01)
            {
                return "isBigger";
            }
            else
            {
                return "isSmaller";
            }
        }

        //increase the whole number of a multi segmented grid
        private void increaseWholeNumber(Document doc, ref MultiSegmentGrid currentMultiSegGrid, ref int counter, ref int subCounter, ref bool prevGridIsSub)
        {
            currentMultiSegGrid.Name = (counter + 1).ToString();
            subCounter = 1;
            foreach (ElementId eID in currentMultiSegGrid.GetGridIds())
            {
                Grid currentGridInMultiSegGrid = doc.GetElement(eID) as Grid;
                currentGridInMultiSegGrid.Name = (counter + 1).ToString();
            }
            counter++;
            prevGridIsSub = false;
        }

        //increase the whole number of a grid
        private string increaseWholeNumber(Document doc, Grid currentGrid, ref int counter, ref int subCounter, ref bool prevGridIsSub)
        {
            currentGrid.Name = (counter + 1).ToString();
            subCounter = 1;
            counter++;
            prevGridIsSub = false;
            return currentGrid.Name;
        }

        //increase the whole letter of a multi segmented grid
        private void increaseWholeLetter(Document doc, ref MultiSegmentGrid currentMultiSegGrid, ref char letter, ref char subLetter, ref bool prevGridIsSub)
        {
            currentMultiSegGrid.Name = WholeName;
            subLetter = 'a';
            foreach (ElementId eID in currentMultiSegGrid.GetGridIds())
            {
                Grid currentGridInMultiSegGrid = doc.GetElement(eID) as Grid;
                currentGridInMultiSegGrid.Name = WholeName;
            }
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
            prevGridIsSub = false;
        }

        //increase the whole letter of a grid
        private string increaseWholeLetter(Document doc, Grid currentGrid, ref char letter, ref char subLetter, ref bool prevGridIsSub)
        {
            prevGridIsSub = false;
            currentGrid.Name = WholeName;
            subLetter = 'a';
            if (letter != 90)
            {
                letter++;
                WholeName = WholeName.Remove(WholeName.Length -1) + letter.ToString();
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
            return currentGrid.Name;
        }

        //increase the sub number number of a multi segmented grid
        private void increaseSubNumber(Document doc, ref MultiSegmentGrid currentMultiSegGrid, ref Grid prevGrid, ref int subCounter, ref bool prevGridIsSub)
        {
            currentMultiSegGrid.Name = prevGrid.Name.ElementAt(0).ToString() + "." + subCounter;
            foreach (ElementId eID in currentMultiSegGrid.GetGridIds())
            {
                Grid currentGridInMultiSegGrid = doc.GetElement(eID) as Grid;
                currentGridInMultiSegGrid.Name = prevGrid.Name.ElementAt(0).ToString() + "." + subCounter;
            }
            prevGridIsSub = true;
            subCounter++;
        }

        private void increaseSubLetter(Document doc, ref MultiSegmentGrid currentMultiSegGrid, ref Grid prevGrid, ref char subLetter, ref bool prevGridIsSub)
        {
            currentMultiSegGrid.Name = prevGrid.Name.ElementAt(0).ToString() + "." + subLetter;
            foreach (ElementId eID in currentMultiSegGrid.GetGridIds())
            {
                Grid currentGridInMultiSegGrid = doc.GetElement(eID) as Grid;
                currentGridInMultiSegGrid.Name = prevGrid.Name.ElementAt(0).ToString() + "." + subLetter;
            }
            prevGridIsSub = true;
            subLetter++;
        }

        //increase the sub number of a grid
        private string increaseSubNumber(Document doc, Grid currentGrid, ref Grid prevGrid, ref int subCounter, ref bool prevGridIsSub)
        {
            currentGrid.Name = prevGrid.Name.ElementAt(0).ToString() + "." + subCounter;
            prevGridIsSub = true;
            subCounter++;
            return currentGrid.Name;
        }

        //increase the sub letter of a grid
        private string increaseSubLetter(Document doc, Grid currentGrid, ref Grid prevGrid, ref char subLetter, ref bool prevGridIsSub)
        {
            currentGrid.Name = prevGrid.Name.ElementAt(0).ToString() + "." + subLetter;
            prevGridIsSub = true;
            subLetter++;
            return currentGrid.Name;
        }
    }
}