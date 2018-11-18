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
    class JoinFailureHandler : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            //IList<FailureMessageAccessor> failureMessages = failuresAccessor.GetFailureMessages();

            //foreach (FailureMessageAccessor currenMessageAcessor in failureMessages)
            //{
            //    failuresAccessor.ResolveFailure(currenMessageAcessor);
            //}

            failuresAccessor.DeleteAllWarnings();

            return FailureProcessingResult.Continue;
        }
    }


    [Transaction(TransactionMode.Manual)]
    class ElementsJoin : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            try
            {

                ElementsJoinUI currentUI = new ElementsJoinUI();
                currentUI.ShowDialog();

                if (currentUI.DialogResult == false)
                {
                    return Result.Cancelled;
                }

                string firstCatName = (currentUI.comboFirstCategory.SelectedItem as ComboBoxItem).Content.ToString();
                string seconCatName = (currentUI.comboSecondCategory.SelectedItem as ComboBoxItem).Content.ToString();

                BuiltInCategory firstCat = checkCategory(firstCatName);
                BuiltInCategory secondCat = checkCategory(seconCatName);

                if (firstCat == BuiltInCategory.INVALID || secondCat == BuiltInCategory.INVALID)
                {
                    return Result.Cancelled;
                }

                IList<Element> ElementsToJoin = new FilteredElementCollector(doc).OfCategory(firstCat).WhereElementIsNotElementType().ToList();

                using (Transaction t = new Transaction(doc, Properties.Messages.ElementJoin_Transaction))
                {
                    t.Start();

                    FailureHandlingOptions joinFailOp = t.GetFailureHandlingOptions();
                    joinFailOp.SetFailuresPreprocessor(new JoinFailureHandler());
                    t.SetFailureHandlingOptions(joinFailOp);

                    foreach (Element currentElementToJoin in ElementsToJoin)
                    {
                        BoundingBoxXYZ bBox = currentElementToJoin.get_BoundingBox(null);
                        bBox.Enabled = true;
                        Outline outLine = new Outline(bBox.Min, bBox.Max);

                        BoundingBoxIntersectsFilter bbIntersects = new BoundingBoxIntersectsFilter(outLine, Utils.ConvertM.cmToFeet(3));

                        IList<Element> elementsIntersecting = new FilteredElementCollector(doc).OfCategory(secondCat).WhereElementIsNotElementType().WherePasses(bbIntersects).ToList();

                        foreach (Element currentElementToBeJoined in elementsIntersecting)
                        {
                            try
                            {
                                JoinGeometryUtils.JoinGeometry(doc, currentElementToJoin, currentElementToBeJoined);
                            }
                            catch
                            {
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

        private BuiltInCategory checkCategory(string categoryName)
        {
            if (categoryName == "Paredes")
            {
                return BuiltInCategory.OST_Walls;
            }
            else if (categoryName == "Vigas")
            {
                return BuiltInCategory.OST_StructuralFraming;
            }
            else if (categoryName == "Pilares")
            {
                return BuiltInCategory.OST_StructuralColumns;
            }
            else if (categoryName == "Lajes")
            {
                return BuiltInCategory.OST_Floors;
            }

            return BuiltInCategory.INVALID;
        }
    }
}
