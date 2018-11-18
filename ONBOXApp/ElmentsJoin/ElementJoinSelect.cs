using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;

namespace ONBOXAppl
{
    enum SelectElementsToJoin { selectFirstElements, selectSecondElements, showFirstSelectedElements, showSecondSelectedElements, deselectFirst, deselectSecond, join, unjoin, unsubscribe, undefined }
    public class RequestElementsSelectHandler : IExternalEventHandler
    {

        class JoinContextSelectionFilter : ISelectionFilter
        {
            int currentComboboxIndex;

            public JoinContextSelectionFilter(int targetComboboxIndex)
            {
                currentComboboxIndex = targetComboboxIndex;
            }

            public bool AllowElement(Element elem)
            {
                int categoryId = elem.Category.Id.GetHashCode();

                if (currentComboboxIndex == 0)
                {
                    if (categoryId == BuiltInCategory.OST_Walls.GetHashCode() || categoryId == BuiltInCategory.OST_StructuralFraming.GetHashCode() ||
                        categoryId == BuiltInCategory.OST_Columns.GetHashCode() || categoryId == BuiltInCategory.OST_Floors.GetHashCode())
                        return true;
                }
                else if (currentComboboxIndex == 1)
                {
                    if (categoryId == BuiltInCategory.OST_Walls.GetHashCode())
                        return true;
                }
                else if (currentComboboxIndex == 2)
                {
                    if (categoryId == BuiltInCategory.OST_StructuralFraming.GetHashCode())
                        return true;
                }
                else if (currentComboboxIndex == 3)
                {
                    if (categoryId == BuiltInCategory.OST_Columns.GetHashCode())
                        return true;
                }
                else if (currentComboboxIndex == 4)
                {
                    if (categoryId == BuiltInCategory.OST_Floors.GetHashCode())
                        return true;
                }

                return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }

        UIDocument uidoc;
        ElementJoinSelectUI joinSelectUI;

        public void Execute(UIApplication app)
        {
            try
            {
                if (app != null)
                {
                    uidoc = app.ActiveUIDocument;
                    joinSelectUI = ONBOXApplication.onboxApp.joinElementSelectWindow;

                    if (uidoc != null)
                    {
                        switch (joinSelectUI.selectElementsSelectOperation)
                        {
                            case SelectElementsToJoin.selectFirstElements:
                                SelectFirstElements();
                                break;
                            case SelectElementsToJoin.selectSecondElements:
                                SelectSecontElements();
                                break;
                            case SelectElementsToJoin.showFirstSelectedElements:
                                ShowFirstSelectedElements();
                                break;
                            case SelectElementsToJoin.showSecondSelectedElements:
                                ShowSecondSelectedElements();
                                break;
                            case SelectElementsToJoin.deselectFirst:
                                DeselectFirst();
                                break;
                            case SelectElementsToJoin.deselectSecond:
                                DeselectSecond();
                                break;
                            case SelectElementsToJoin.join:
                                JoinElements();
                                break;
                            case SelectElementsToJoin.unjoin:
                                UnJoinElements();
                                break;
                            case SelectElementsToJoin.unsubscribe:
                                Unsubscribe();
                                break;
                            case SelectElementsToJoin.undefined:
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                joinSelectUI.UnFreezeUI();
            }
        }

        private void DeselectSecond()
        {
            Deselect(false);
        }

        private void DeselectFirst()
        {
            Deselect(true);
        }

        private void Deselect(bool isFirst)
        {
            if (uidoc != null)
            {
                if (uidoc.Document != null)
                {
                    if (joinSelectUI != null)
                    {
                        if (isFirst)
                        {                
                            joinSelectUI.currentFirstElementList = new List<Element>();
                            joinSelectUI.ChangeFirstElementsNumber(joinSelectUI.currentFirstElementList.Count);
                        }
                        else
                        {
                            joinSelectUI.currentSecondElementList = new List<Element>();
                            joinSelectUI.ChangeSecondElementsNumber(joinSelectUI.currentSecondElementList.Count);
                        }

                        uidoc.Selection.SetElementIds(new List<ElementId>());
                        uidoc.RefreshActiveView();
                    }
                }
            }
        }

        private void ShowSecondSelectedElements()
        {
            ShowSelectedElements(joinSelectUI.currentSecondElementList);
        }

        private void ShowFirstSelectedElements()
        {
            ShowSelectedElements(joinSelectUI.currentFirstElementList);
        }

        private void ShowSelectedElements(IList<Element> targetListToSelect)
        {
            IList<Element> elementList = targetListToSelect.ToList();

            if (uidoc != null)
            {
                if (elementList != null)
                {
                    IList<ElementId> ElementsToSelect = new List<ElementId>();
                    foreach (Element currentElement in elementList)
                    {
                        //The user can delete an element that was previewsly marked, so its important to put a try catch here!!
                        try
                        {
                            if (currentElement != null)
                            {
                                if (!(currentElement.Id == ElementId.InvalidElementId))
                                    ElementsToSelect.Add(currentElement.Id);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                    uidoc.Selection.SetElementIds(ElementsToSelect);
                    uidoc.RefreshActiveView();
                }
            }
        }

        private void UnJoinElements()
        {
            JoinOrUnJoinElements(false);
        }

        private void JoinElements()
        {
            JoinOrUnJoinElements(true);
        }

        private void JoinOrUnJoinElements(bool join)
        {
            if (uidoc != null)
            {
                if (joinSelectUI.currentFirstElementList != null && joinSelectUI.currentSecondElementList != null)
                {
                    bool secondElementsWasEmpty = false;
                    // if we the second elements were not selected, use the first list
                    if (joinSelectUI.currentFirstElementList.Count == 0)
                        return;

                    if (joinSelectUI.currentSecondElementList.Count == 0)
                    {
                        secondElementsWasEmpty = true;
                        joinSelectUI.currentSecondElementList = joinSelectUI.currentFirstElementList.ToList();
                    }

                    string transactionName = join ? Properties.Messages.ElementJoinSelect_JoinTransaction : Properties.Messages.ElementJoinSelect_UnJoinTransaction;

                    using (Transaction t = new Transaction(uidoc.Document, transactionName))
                    {
                        t.Start();

                        FailureHandlingOptions joinFailOp = t.GetFailureHandlingOptions();
                        joinFailOp.SetFailuresPreprocessor(new JoinFailurAdvHandler());
                        t.SetFailureHandlingOptions(joinFailOp);

                        foreach (Element currentElementToJoin in joinSelectUI.currentFirstElementList)
                        {
                            BoundingBoxXYZ bBox = currentElementToJoin.get_BoundingBox(null);
                            bBox.Enabled = true;
                            Outline outLine = new Outline(bBox.Min, bBox.Max);

                            BoundingBoxIntersectsFilter bbIntersects = new BoundingBoxIntersectsFilter(outLine, Utils.ConvertM.cmToFeet(3));
                            ElementIntersectsElementFilter intersectFilter = new ElementIntersectsElementFilter(currentElementToJoin);

                            foreach (Element currentElementToBeJoined in joinSelectUI.currentSecondElementList)
                            {
                                if (join)
                                {
                                    if (currentElementToJoin.Id == currentElementToBeJoined.Id)
                                        continue;

                                    if (!bbIntersects.PassesFilter(currentElementToBeJoined))
                                        continue;

                                    if (currentElementToJoin.Category.Id != currentElementToBeJoined.Category.Id)
                                    {
                                        if (!intersectFilter.PassesFilter(currentElementToBeJoined))
                                            continue;
                                    }

                                    if (!JoinGeometryUtils.AreElementsJoined(uidoc.Document, currentElementToJoin, currentElementToBeJoined))
                                    {
                                        try
                                        {
                                            JoinGeometryUtils.JoinGeometry(uidoc.Document, currentElementToJoin, currentElementToBeJoined);
                                        }
                                        catch (Exception e)
                                        {
                                            System.Diagnostics.Debug.Print(e.Message);
                                        }
                                    }
                                }
                                else
                                {
                                    if (JoinGeometryUtils.AreElementsJoined(uidoc.Document, currentElementToJoin, currentElementToBeJoined))
                                    {
                                        try
                                        {
                                            JoinGeometryUtils.UnjoinGeometry(uidoc.Document, currentElementToJoin, currentElementToBeJoined);
                                        }
                                        catch (Exception e)
                                        {
                                            System.Diagnostics.Debug.Print(e.Message);
                                        }
                                    }
                                }
                            }
                        }

                        if (secondElementsWasEmpty)
                            joinSelectUI.currentSecondElementList = new List<Element>();

                        t.Commit();
                    }
                }
            }
        }

        private void SelectSecontElements()
        {
            SelectElements(joinSelectUI.currentSecondElementList, false);
        }

        private void SelectFirstElements()
        {
            SelectElements(joinSelectUI.currentFirstElementList, true);
        }

        private void SelectElements(IList<Element> targetElementList, bool isFirst)
        {
            try
            {
                if (joinSelectUI != null)
                {
                    if (uidoc != null)
                    {
                        Document doc = uidoc.Document;
                        if (doc != null)
                        {
                            View currentView = ONBOXApplication.onboxApp.uiApp.ActiveUIDocument.ActiveView;
                            View currentGraphicalView = ONBOXApplication.onboxApp.uiApp.ActiveUIDocument.ActiveGraphicalView;

                            if (currentView.Id != currentGraphicalView.Id)
                            {
                                TaskDialog.Show(Properties.Messages.Common_Error, Properties.Messages.ElementJoinSelect_NotGraphicalView);
                                return;
                            }

                            Selection sel = uidoc.Selection;

                            int comboSelectIndex = 0;

                            if (isFirst)
                                comboSelectIndex = joinSelectUI.comboFirstElements.SelectedIndex;
                            else
                                comboSelectIndex = joinSelectUI.comboSecondElements.SelectedIndex;

                            targetElementList = sel.PickElementsByRectangle(new JoinContextSelectionFilter(comboSelectIndex), Properties.Messages.ElementJoinSelect_SelectElements);

                            IList<ElementId> ElementSelectedIdList = new List<ElementId>();
                            foreach (var currentElement in targetElementList)
                            {
                                ElementSelectedIdList.Add(currentElement.Id);
                            }

                            sel.SetElementIds(ElementSelectedIdList);
                            uidoc.RefreshActiveView();

                            if (isFirst)
                            {
                                joinSelectUI.ChangeFirstElementsNumber(targetElementList.Count);
                                joinSelectUI.currentFirstElementList = targetElementList;
                            }
                            else
                            {
                                joinSelectUI.ChangeSecondElementsNumber(targetElementList.Count);
                                joinSelectUI.currentSecondElementList = targetElementList;

                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public string GetName()
        {
            return "ONBOX : Join elements request handler";
        }

        internal void Unsubscribe()
        {
            ONBOXApplication.onboxApp.joinElementSelecEvent.Dispose();
            ONBOXApplication.onboxApp.joinElementSelecEvent = null;
            ONBOXApplication.onboxApp.requestjoinElementSelecHandler = null;

            ONBOXApplication.onboxApp.uiApp.ViewActivated -= ONBOXApplication.onboxApp.joinElementSelectWindow_ViewActivated;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class ElementJoinSelect : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                ONBOXApplication.onboxApp.uiApp = commandData.Application;
                ONBOXApplication.onboxApp.ShowJoinElementsSelectUI();
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
