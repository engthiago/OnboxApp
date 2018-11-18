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
    [Transaction(TransactionMode.Manual)]
    class ElementsCopyToLevels : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            try
            {
                FamilyInstance selectedBeam = doc.GetElement(sel.PickObject(ObjectType.Element, new BeamSelectionFilter(), Properties.Messages.CopyBeamsToLevels_SelectBeam)) as FamilyInstance;

                ElementId levelID = selectedBeam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId();

                IList<Element> beamInSourceLevel = Utils.FindElements.GetElementsInLevelBounds(doc, levelID, 3.00, BuiltInCategory.OST_StructuralFraming);

                beamInSourceLevel = beamInSourceLevel.Where(b => b.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId() == levelID).ToList();

                IList<LevelInfo> allLevelInfo = Utils.GetInformation.GetAllLevelsInfo(doc);

                ElementsCopyUI currentUI = new ElementsCopyUI(allLevelInfo);

                currentUI.ShowDialog();

                if (currentUI.DialogResult == false)
                    return Result.Cancelled;


                var selectedLevelsInfo = currentUI.listLevels.SelectedItems;

                if (selectedLevelsInfo.Count == 0)
                    return Result.Cancelled;

                using (Transaction t = new Transaction(doc, Properties.Messages.CopyBeamsToLevels_Transaction))
                {
                    t.Start();
                    foreach (LevelInfo currentLevelInfo in selectedLevelsInfo)
                    {
                        ElementId currentLevelID = new ElementId(currentLevelInfo.levelId);
                        Level currentLevel = doc.GetElement(currentLevelID) as Level;

                        if (currentUI.checkEraseBeamsOnTarget.IsChecked == true)
                        {
                            IList<Element> beamsTodelete = Utils.FindElements.GetElementsInLevelBounds(doc, currentLevelID, 3.00, BuiltInCategory.OST_StructuralFraming);

                            beamsTodelete = beamsTodelete.Where(b => b.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId() == currentLevelID).ToList();

                            foreach (Element currentBeamToDelete in beamsTodelete)
                            {
                                doc.Delete(currentBeamToDelete.Id);
                            }
                        }

                        foreach (Element currentBeam in beamInSourceLevel)
                        {
                            IList<ElementId> copiedElementsIDs = ElementTransformUtils.CopyElement(doc, currentBeam.Id, XYZ.Zero).ToList();

                            foreach (ElementId currentcopiedID in copiedElementsIDs)
                            {
                                Element currentCopiedElement = doc.GetElement(currentcopiedID);

                                if (currentCopiedElement is FamilyInstance)
                                {
                                    currentCopiedElement.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION).Set(1);
                                    currentCopiedElement.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION).Set(1);

                                    currentCopiedElement.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).Set(currentLevelID);

                                    currentCopiedElement.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION).Set(0);
                                    currentCopiedElement.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION).Set(0);

                                    if (currentUI.checkEraseBeamsOnTarget.IsChecked == false)
                                    {
                                        doc.Regenerate();
                                        Utils.CheckFamilyInstanceForIntersection.checkForDuplicates(currentCopiedElement as FamilyInstance, doc);
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
    }
}
