using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Architecture;

namespace ONBOXAppl
{
    [Transaction(TransactionMode.Manual)]
    class TopoFromDwgMarks : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            Selection sel = uidoc.Selection;
            try
            {

                ImportInstance currentDwg = doc.GetElement(sel.PickObject(ObjectType.Element, new CADSelectionFilter(), "Selecione um elemento DWG para criacao do terreno")) as ImportInstance;

                IList<XYZ> currentDwgPoints = Utils.GetDwgGeometryInformation.GetDwgGeometryMidPoints(Utils.DwgGeometryType.Circle, currentDwg, null);

                if (currentDwgPoints.Count < 3)
                {
                    message = "Não foram encontrados pontos suficientes para a criação da topografia";
                    return Result.Failed;
                }

                IList<XYZ> currentDwgPointsWithHeight = new List<XYZ>();
                IList<Element> textElements = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TextNotes).WhereElementIsNotElementType().ToList();

                foreach (XYZ currentPoint in currentDwgPoints)
                {
                    double minDist = double.PositiveInfinity;

                    TextElement nearestText = null;

                    foreach (Element currentTextElement in textElements)
                    {
                        XYZ currentTextPosition = (currentTextElement as TextElement).Coord;
                        XYZ currentTextPositionZ0 = new XYZ(currentTextPosition.X, currentTextPosition.Y, 0);
                        XYZ currentPointZ0 = new XYZ(currentPoint.X, currentPoint.Y, 0);
                        double currentDist = currentPointZ0.DistanceTo(currentTextPositionZ0);

                        if (currentDist < minDist)
                        {
                            minDist = currentDist;
                            nearestText = currentTextElement as TextElement;
                        }

                    }

                    if (nearestText != null)
                    {
                        string textValue = nearestText.Text;
                        double zValue = 0;

                        if (double.TryParse(textValue, out zValue))
                        {
                            //zValue = zValue * 1000;
                            zValue = Utils.ConvertM.mToFeet(zValue);
                            XYZ currentPointWithHeight = new XYZ(currentPoint.X, currentPoint.Y, zValue);
                            currentDwgPointsWithHeight.Add(currentPointWithHeight);
                        }
                        else
                        {
                            currentDwgPointsWithHeight.Add(currentPoint);
                        }
                    }
                    else
                    {
                        currentDwgPointsWithHeight.Add(currentPoint);
                    }

                }


                using (Transaction t = new Transaction(doc, "Criar Terreno"))
                {
                    t.Start();

                    TopographySurface currentTopo = TopographySurface.Create(doc, currentDwgPointsWithHeight);

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
    }
}
