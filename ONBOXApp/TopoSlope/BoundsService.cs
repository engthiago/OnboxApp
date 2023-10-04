#if R2024

using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using ONBOXAppl.Properties;

namespace ONBOXAppl
{
    public class BoundsService
    {
        public FindBoundsResult FindBounds(Floor floor)
        {
            var result = new FindBoundsResult();
            var doc = floor.Document;

            IList<Reference> topFacesReferences = HostObjectUtils.GetTopFaces(floor);
            IList<CurveLoop> edgeLoops = null;

            if (topFacesReferences.Count > 1)
            {
                using (var tempGroup = new TransactionGroup(doc, "tempTransactionGroup"))
                {
                    tempGroup.Start();

                    using (var tempTransaction = new Transaction(doc, "tempTransaction"))
                    {
                        tempTransaction.Start();

                        var slabEditor = floor.GetSlabShapeEditor();
                        slabEditor.ResetSlabShape();

                        tempTransaction.Commit();
                    }

                    topFacesReferences = HostObjectUtils.GetTopFaces(floor);
                    GeometryObject currentFaceObj = floor.GetGeometryObjectFromReference(topFacesReferences[0]);
                    if (currentFaceObj is PlanarFace planarFace)
                    {
                        var plannarDirection = planarFace.FaceNormal;
                        var plannarOrigin = planarFace.Origin;
                        edgeLoops = planarFace.GetEdgesAsCurveLoops();
                    }

                    tempGroup.RollBack();
                }
            }
            else
            {
                GeometryObject currentFaceObj = floor.GetGeometryObjectFromReference(topFacesReferences[0]);
                if (currentFaceObj is PlanarFace planarFace)
                {
                    //var plannarDirection = planarFace.FaceNormal;
                    //var plannarOrigin = planarFace.Origin;
                    edgeLoops = planarFace.GetEdgesAsCurveLoops();
                }
            }

            if (edgeLoops == null)
            {
                result.ErrorMessage = Messages.Toposolid_SlopeGrading_NoFloorEdges;
            }

            //Sort the curves so the outer loop comes first
            IList<IList<CurveLoop>> curveLoopLoop = ExporterIFCUtils.SortCurveLoops(edgeLoops);
            var orderedLoops = curveLoopLoop.FirstOrDefault();
            if (orderedLoops == null)
            {
                result.ErrorMessage = Messages.Toposolid_SlopeGrading_ErrorSortingEdges;
            }

            if (orderedLoops.Count == 0)
            {
                result.ErrorMessage = Messages.Toposolid_SlopeGrading_ErrorFindingEdges;
            }

            result.OuterBounds = orderedLoops[0];
            result.InnerBounds = orderedLoops.Skip(1).ToList();
            result.Success = true;
            return result;
        }
    }
}

#endif