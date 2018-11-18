using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.DB.Architecture;

namespace ONBOXAppl
{
    class FloorSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            Floor f = elem as Floor;
            return f != null;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class aFromFloors : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            Floor currentFloor = doc.GetElement(sel.PickObject(ObjectType.Element, new FloorSelectionFilter(), "Selecione um piso")) as Floor;

            IList<Reference> topFaceReferenceList = HostObjectUtils.GetTopFaces(currentFloor);

            using (Transaction t = new Transaction(doc, "Criar plataformas"))
            {
                t.Start();
                foreach (Reference currentRef in topFaceReferenceList)
                {
                    Face currentFace = GetFaceFromReference(currentRef, currentFloor);
                    CreatePadFromFace(currentFace, currentFloor, doc);
                }
                t.Commit();

            }

            try
            {

            }
            catch (Exception)
            {

            }

            return Result.Succeeded;
        }

        private void CreatePadFromFace(Face targetFace, Floor targetFloor, Document targetDoc)
        {
            if (targetFace is PlanarFace == false)
                return;

            IList<CurveLoop> faceLoops = targetFace.GetEdgesAsCurveLoops();
            faceLoops = GetFlattenCurveLoops(faceLoops);

            //TODO use the UI to specify wich type will be used
            BuildingPadType targetPadType = new FilteredElementCollector(targetDoc).OfClass(typeof(BuildingPadType)).FirstOrDefault() as BuildingPadType;

            BuildingPad currentPad = BuildingPad.Create(targetDoc, targetPadType.Id, targetFloor.LevelId, faceLoops);      
                      
        }


        private IList<CurveLoop> GetFlattenCurveLoops(IList<CurveLoop> targetLoop)
        {
            IList<CurveLoop> targetFlattenLoop = new List<CurveLoop>();

            foreach (CurveLoop currentLoop in targetLoop)
            {
                CurveLoop currentFlattenLoop = new CurveLoop();
                foreach (Curve currentCurve in currentLoop)
                {
                    XYZ firstPoint = new XYZ(currentCurve.GetEndPoint(0).X, currentCurve.GetEndPoint(0).Y, 0);
                    XYZ secondPoint = new XYZ(currentCurve.GetEndPoint(1).X, currentCurve.GetEndPoint(1).Y, 0);

                    currentFlattenLoop.Append(Line.CreateBound(firstPoint, secondPoint));
                }
                targetFlattenLoop.Add(currentFlattenLoop);
            }

            return targetFlattenLoop;
        }

        private Face GetFaceFromReference(Reference targetReference, Element targetElement)
        {
            Face currentFace = null;
            if (targetReference != null && targetElement != null)
            {
                GeometryObject currentGeometryObject = targetElement.GetGeometryObjectFromReference(targetReference);
                if (currentGeometryObject != null)
                {
                    if (currentGeometryObject is Face)
                        currentFace = currentGeometryObject as Face;
                }
            }

            return currentFace;
        }

    }
}
