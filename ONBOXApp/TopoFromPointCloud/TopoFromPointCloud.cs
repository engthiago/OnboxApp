using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.PointClouds;
using Autodesk.Revit.DB.Architecture;

namespace ONBOXAppl
{
    public class PointCloundSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element is PointCloudInstance)
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
    class TopoFromPointCloud : IExternalCommand
    {

        double pointMinDistance = Utils.ConvertM.cmToFeet(3);
        int pointMaxQuantity = 500000;
        double pointDistance = Utils.ConvertM.cmToFeet(0.1);

        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513360"));

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            Selection sel = uidoc.Selection;

            try
            {

                if (uidoc.ActiveView is View3D == false)
                {
                    message = "Você deve estar em uma vista 3d para utilizar este comando. Por favor, entre em uma vista compatível e rode o comando novamente.";
                    return Result.Failed;
                }

                PointCloudInstance pcInstance = doc.GetElement(sel.PickObject(ObjectType.Element, new PointCloundSelectionFilter(), "Por favor, selecione uma nuvem de pontos")) as PointCloudInstance;

                if (pcInstance == null)
                {
                    message = "Objeto inválido selecionado. Este comando funciona somente com instancias de nuvem de pontos, por favor, rode o comando novamente e selecione uma nuvem de pontos.";
                    return Result.Failed;
                }

                View3D this3dView = uidoc.ActiveView as View3D;

                BoundingBoxXYZ boundingBox = null;

                //Use CropBox if there is one to use
                if (this3dView.CropBoxActive == true)
                {
                    boundingBox = this3dView.CropBox;
                }
                else
                {
                    boundingBox = pcInstance.get_BoundingBox(uidoc.ActiveView);
                }

                List<Plane> planes = new List<Plane>();
                XYZ midpoint = (boundingBox.Min + boundingBox.Max) / 2.0;

#if R2016
                // X boundaries
                planes.Add(new Plane(XYZ.BasisX, boundingBox.Min));
                planes.Add(new Plane(-XYZ.BasisX, boundingBox.Max));

                // Y boundaries
                planes.Add(new Plane(XYZ.BasisY, boundingBox.Min));
                planes.Add(new Plane(-XYZ.BasisY, boundingBox.Max));

                // Z boundaries
                planes.Add(new Plane(XYZ.BasisZ, boundingBox.Min));
                planes.Add(new Plane(-XYZ.BasisZ, boundingBox.Max));
#else
                // X boundaries
                planes.Add(Plane.CreateByNormalAndOrigin(XYZ.BasisX, boundingBox.Min));
                planes.Add(Plane.CreateByNormalAndOrigin(-XYZ.BasisX, boundingBox.Max));

                // Y boundaries
                planes.Add(Plane.CreateByNormalAndOrigin(XYZ.BasisY, boundingBox.Min));
                planes.Add(Plane.CreateByNormalAndOrigin(-XYZ.BasisY, boundingBox.Max));

                // Z boundaries
                planes.Add(Plane.CreateByNormalAndOrigin(XYZ.BasisZ, boundingBox.Min));
                planes.Add(Plane.CreateByNormalAndOrigin(-XYZ.BasisZ, boundingBox.Max)); 
#endif

                using (Transaction t = new Transaction(doc, "Criar Topografia"))
                {
                    t.Start();
                    // Create filter
                    PointCloudFilter pcFilter = PointCloudFilterFactory.CreateMultiPlaneFilter(planes);
                    pcInstance.FilterAction = SelectionFilterAction.Highlight;

                    PointCollection pcColection = pcInstance.GetPoints(pcFilter, pointDistance, pointMaxQuantity);
                    IList<XYZ> pcPoints = new List<XYZ>();
                    XYZ origin = pcInstance.GetTotalTransform().Origin;
                
                    if (pcColection.Count < 3)
                    {
                        t.RollBack();
                        message = "Número de pontos insuficiente para criar a topografia. Por favor utilize outra nuvem de pontos.";
                        return Result.Failed;
                    }

                    foreach (XYZ currentPoint in pcColection)
                    {
                        XYZ pointCopy = new XYZ(currentPoint.X, currentPoint.Y, currentPoint.Z);
                        pointCopy = pcInstance.GetTotalTransform().OfPoint(pointCopy);

                        if (!ListContainsPoint(pcPoints, pointCopy))
                            pcPoints.Add(pointCopy);
                    }

                    TopographySurface topoSurface = TopographySurface.Create(doc, pcPoints);
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


        private bool ListContainsPoint(IList<XYZ> targetListOfPoints, XYZ targetPoint)
        {
            foreach (XYZ currentPoint in targetListOfPoints)
            {
                if (currentPoint.IsAlmostEqualTo(targetPoint, pointMinDistance))
                    return true;
            }

            return false;
        }

    }
}
