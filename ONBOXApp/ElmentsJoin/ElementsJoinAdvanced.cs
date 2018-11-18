using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using System.Windows.Controls;

namespace ONBOXAppl
{
    class JoinFailurAdvHandler : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            string transName = failuresAccessor.GetTransactionName();

            IList<FailureMessageAccessor> failMessages = failuresAccessor.GetFailureMessages();

            if (failMessages.Count == 0)
                return FailureProcessingResult.Continue;

            if (transName == Properties.Messages.ElementJoin_JoinTransaction || transName == Properties.Messages.ElementJoin_UnJoinTransaction)
            {
                foreach (FailureMessageAccessor currentMessage in failMessages)
                    failuresAccessor.ResolveFailure(currentMessage);      

                return FailureProcessingResult.ProceedWithCommit;
            }

            return FailureProcessingResult.Continue;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class ElementsJoinAdvanced : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            try
            {
                //IList<Level> projectLevelList = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level))
                //    .Cast<Level>().OrderBy(l => l.Elevation).ToList();

                IList<LevelInfo> projectLevelList = Utils.GetInformation.GetAllLevelsInfo(doc);

                ElementsJoinUIAdvanced currentUI = new ElementsJoinUIAdvanced(projectLevelList);
                currentUI.ShowDialog();

                if (currentUI.DialogResult == false)
                {
                    return Result.Cancelled;
                }

                string firstCatName = (currentUI.comboFirstCategory.SelectedItem as ComboBoxItem).Content.ToString();
                string seconCatName = (currentUI.comboSecondCategory.SelectedItem as ComboBoxItem).Content.ToString();

                BuiltInCategory firstCat = checkCategory(currentUI.comboFirstCategory.SelectedIndex);
                BuiltInCategory secondCat = checkCategory(currentUI.comboFirstCategory.SelectedIndex);

                if (firstCat == BuiltInCategory.INVALID || secondCat == BuiltInCategory.INVALID)
                {
                    return Result.Cancelled;
                }

                Level lowerLevel = null;
                Level upperLevel = null;

                Element lowerLevelElement = null;
                Element upperLevelElement = null;

                if (currentUI.selectedLowerLevel != 0 && currentUI.selectedLowerLevel != -1)
                    lowerLevelElement = doc.GetElement(new ElementId(currentUI.selectedLowerLevel));

                if (currentUI.selectedUpperLevel != 0 && currentUI.selectedUpperLevel != -1)
                    upperLevelElement = doc.GetElement(new ElementId(currentUI.selectedUpperLevel));

                if (lowerLevelElement != null)
                    lowerLevel = lowerLevelElement as Level;

                if (upperLevelElement != null)
                    upperLevel = GetNextLevel(upperLevelElement as Level);


                IList<Element> ElementsToJoin = GetElementsOnLevels(doc, firstCat, lowerLevel, upperLevel);
                IList<ElementId> elementsIdsToJoin = new List<ElementId>();

                foreach (Element currentElement in ElementsToJoin)
                {
                    elementsIdsToJoin.Add(currentElement.Id);
                }

                IList<Element> elementsIntersecting = new List<Element>();

                using (Transaction t = new Transaction(doc, Properties.Messages.ElementJoin_JoinTransaction))
                {
                    t.Start();

                    FailureHandlingOptions joinFailOp = t.GetFailureHandlingOptions();
                    joinFailOp.SetFailuresPreprocessor(new JoinFailurAdvHandler());
                    t.SetFailureHandlingOptions(joinFailOp);

                    IList<Element> elementsIToBeJoin = GetElementsOnLevels(doc, secondCat, lowerLevel, upperLevel);
                    IList<ElementId> elementsIdsToBeJoin = new List<ElementId>();

                    foreach (Element currentElement in elementsIToBeJoin)
                    {
                        elementsIdsToBeJoin.Add(currentElement.Id);
                    }

                    foreach (Element currentElementToJoin in ElementsToJoin)
                    {
                        BoundingBoxXYZ bBox = currentElementToJoin.get_BoundingBox(null);
                        bBox.Enabled = true;
                        Outline outlineToJoin = new Outline(bBox.Min, bBox.Max);

                        BoundingBoxIntersectsFilter bbIntersects = new BoundingBoxIntersectsFilter(outlineToJoin, Utils.ConvertM.cmToFeet(0.5));

                        elementsIntersecting = new FilteredElementCollector(doc, elementsIdsToBeJoin).OfCategory(secondCat).WherePasses(bbIntersects).ToElements();

                        ElementIntersectsElementFilter intersectFilter = new ElementIntersectsElementFilter(currentElementToJoin);

                        foreach (Element currentElementToBeJoined in elementsIntersecting)
                        {
                            if (currentUI.join)
                            {

                                if (currentElementToJoin.Id == currentElementToBeJoined.Id)
                                    continue;

                                if (currentElementToJoin.Category.Id != currentElementToBeJoined.Category.Id)
                                {
                                    if (!intersectFilter.PassesFilter(currentElementToBeJoined))
                                        continue;
                                }



                                if (!JoinGeometryUtils.AreElementsJoined(doc, currentElementToJoin, currentElementToBeJoined))
                                {
                                    try
                                    {
                                        JoinGeometryUtils.JoinGeometry(doc, currentElementToJoin, currentElementToBeJoined);
                                    }
                                    catch (Exception e)
                                    {
                                        System.Diagnostics.Debug.Print(e.Message);
                                    }
                                }

                            }
                            else
                            {
                                if (JoinGeometryUtils.AreElementsJoined(doc, currentElementToJoin, currentElementToBeJoined))
                                {
                                    try
                                    {
                                        JoinGeometryUtils.UnjoinGeometry(doc, currentElementToJoin, currentElementToBeJoined);
                                    }
                                    catch (Exception e)
                                    {
                                        System.Diagnostics.Debug.Print(e.Message);
                                    }
                                }
                            }
                        }

                    }
                    t.Commit();
                }
            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
            }

            return Result.Succeeded;
        }

        private BuiltInCategory checkCategory(int itemIndex)
        {
            if (itemIndex == 0)
            {
                return BuiltInCategory.OST_Walls;
            }
            else if (itemIndex == 1)
            {
                return BuiltInCategory.OST_StructuralFraming;
            }
            else if (itemIndex == 2)
            {
                return BuiltInCategory.OST_StructuralColumns;
            }
            else if (itemIndex == 3)
            {
                return BuiltInCategory.OST_Floors;
            }

            return BuiltInCategory.INVALID;
        }


        private IList<Element> GetElementsOnLevels(Document targetDoc, BuiltInCategory targetCategory, Level targetLowerLevel, Level targetUpperLevel)
        {
            //if target lower level is null, we will check everything lower than the first level
            //if target upper level is null, we will check everything upper than the last level

            IList<Element> elementList = new List<Element>();

            double minElevation = -99999;
            double maxElevation = 99999;

            if (targetLowerLevel != null)
                minElevation = targetLowerLevel.Elevation;

            if (targetUpperLevel != null)
                maxElevation = targetUpperLevel.Elevation;

            BoundingBoxXYZ selectBox = new BoundingBoxXYZ();

            XYZ minPoint = new XYZ(-99999, -99999, minElevation + Utils.ConvertM.cmToFeet(1));
            XYZ maxPoint = new XYZ(99999, 99999, maxElevation - Utils.ConvertM.cmToFeet(1));

            double temMin = Utils.ConvertM.feetToM(minPoint.Z);
            double temMax = Utils.ConvertM.feetToM(maxPoint.Z);

            Outline selectOutline = new Outline(minPoint, maxPoint);
            BoundingBoxIntersectsFilter bbInsideFilter = new BoundingBoxIntersectsFilter(selectOutline);

            elementList = new FilteredElementCollector(targetDoc).OfCategory(targetCategory).WherePasses(bbInsideFilter).ToList();

            return elementList;
        }

        private Level GetNextLevel(Level targetLevel)
        {
            Document targetDoc = targetLevel.Document;

            IList<Level> levelList = new FilteredElementCollector(targetDoc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level))
                    .Cast<Level>().OrderBy(l => l.Elevation).ToList();

            IList<ElementId> levelIdList = new List<ElementId>();

            foreach (Level currentLevel in levelList)
                levelIdList.Add(currentLevel.Id);

            int nextLevelLocation = levelIdList.IndexOf(targetLevel.Id) + 1;

            ElementId nextLevelId = new ElementId(-1);

            if (levelIdList.Count - 1 >= nextLevelLocation)
                nextLevelId = levelIdList.ElementAt(nextLevelLocation);

            if (nextLevelId != ElementId.InvalidElementId)
                return targetDoc.GetElement(nextLevelId) as Level;

            //If theres no next level, just return the current level that was passed on the function
            return targetLevel;
        }

    }
}
