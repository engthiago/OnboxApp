using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.IFC;

namespace ONBOXAppl
{

    class PadSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem is BuildingPad)
            {
                return true;
            }
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    class TopographyFailurePreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            return FailureProcessingResult.Continue;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class TopoSlopes : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;

            //Checks if there is building pads on the project
            //If theres no building pads, also there's no surface, so we dont need to check that
            IList<Element> allBuildingPadsInProject = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_BuildingPad).OfClass(typeof(BuildingPad)).WhereElementIsNotElementType().ToList();
            if (allBuildingPadsInProject.Count == 0)
            {
                message = Properties.Messages.SlopeGradingFromPads_NoPads;
                return Result.Failed;
            }

            //Runs the command
            try
            {
                //UI
                TopoSlopesUI topoSlopeWindow = new TopoSlopesUI();

                if (topoSlopeWindow.ShowDialog() == false)
                    return Result.Cancelled;

                double maxPointDistInMeters = topoSlopeWindow.MaxDist;
                double maxPointDistInFeet = Utils.ConvertM.cmToFeet(maxPointDistInMeters * 100);
                double targetAngle = topoSlopeWindow.Angle;
                double targetAngleInRadians = Utils.ConvertM.degreesToRadians(targetAngle);
                bool isContinuous = topoSlopeWindow.IsContinuous;

                bool enterLoop = true;
                while (enterLoop)
                {
                    if (isContinuous == false)
                        enterLoop = false;

                    if (SlopeSelectedBuildingPad(uidoc, ref message, maxPointDistInFeet, targetAngleInRadians) == Result.Failed)
                    {
                        return Result.Failed;
                    }
                }

            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
            }

            return Result.Succeeded;
        }

        private void EstabilishInteractionPoints(Curve currentCurve, double maxPointDistInFeet, out int numberOfInteractions, out double increaseAmount)
        {
            double currentCurveLength = currentCurve.ApproximateLength;

            if (maxPointDistInFeet > currentCurveLength)
            {
                numberOfInteractions = 1;
            }
            else
            {
                numberOfInteractions = (int)Math.Ceiling(currentCurveLength / maxPointDistInFeet);
            }

            increaseAmount = 1.00 / numberOfInteractions;      
        }

        private Result SlopeSelectedBuildingPad(UIDocument targetUIdoc, ref string message, double maxPointDistInFeet, double targetAngleInRadians)
        {
            Document doc = targetUIdoc.Document;
            Selection sel = targetUIdoc.Selection;

            View3D current3dView = doc.ActiveView as View3D;
            if (current3dView == null)
            {
                message = Properties.Messages.SlopeGradingFromPads_Not3DView;
                return Result.Failed;
            }

            BuildingPad selectedBuildingPad = doc.GetElement(sel.PickObject(ObjectType.Element, new PadSelectionFilter(), Properties.Messages.SlopeGradingFromPads_SelectPad)) as BuildingPad;

            //Check if the Pad is associate with a Surface (never seen one doesnt, but anyways)
            ElementId topoElementID = selectedBuildingPad.HostId;

            if (topoElementID.Equals(ElementId.InvalidElementId))
            {
                message = Properties.Messages.SlopeGradingFromPads_NoTopoAssociate;
                return Result.Failed;
            }

            TopographySurface currentTopo = doc.GetElement(topoElementID) as TopographySurface;

            if (currentTopo == null)
            {
                message = Properties.Messages.SlopeGradingFromPads_NoTopoAssociate; ;
                return Result.Failed;
            }

            IList<CurveLoop> PadBoundaryLoops = new List<CurveLoop>();
            CurveLoop outerLoop = null;

            IList<Reference> TopFacesReferences = HostObjectUtils.GetTopFaces(selectedBuildingPad);

            if (TopFacesReferences.Count > 1)
            {
                message = Properties.Messages.SlopeGradingFromPads_PadsWithMoreThanOneUpperFace;
                return Result.Failed;
            }

            XYZ plannarDirection = XYZ.BasisZ;
            XYZ plannarOrigin = XYZ.Zero;

            // interate on the only face
            foreach (Reference currentFaceRef in TopFacesReferences)
            {
                GeometryObject currentFaceObj = selectedBuildingPad.GetGeometryObjectFromReference(currentFaceRef);
                if (currentFaceObj is PlanarFace)
                {
                    PlanarFace currentPlanarFace = currentFaceObj as PlanarFace;
                    plannarDirection = currentPlanarFace.FaceNormal;
                    plannarOrigin = currentPlanarFace.Origin;
                    PadBoundaryLoops = currentPlanarFace.GetEdgesAsCurveLoops();
                }
                else
                {
                    message = Properties.Messages.SlopeGradingFromPads_UpperFaceNotPlanar;
                    return Result.Failed;
                }
            }

            //Sort the curves so the outer loop comes first
            IList<IList<CurveLoop>> curveLoopLoop = ExporterIFCUtils.SortCurveLoops(PadBoundaryLoops);

            if (curveLoopLoop.Count > 0)
            {
                IList<CurveLoop> firstList = curveLoopLoop.First();
                if (firstList.Count > 0)
                {
                    outerLoop = firstList.First();
                }
            }

            if (outerLoop == null)
            {
                message = Properties.Messages.SlopeGradingFromPads_OuterLoopIssue;
                return Result.Failed;
            }

            //This will be the list of elements that the ReferenceIntersector will shoot the rays
            //If we try to shoot the rays only in the toposurface and the it has subregions, the reference
            //intersection will not recognize these regions, so its necessary to shoot rays to the surface and its subregions
            IList<ElementId> currentSubRegionsAndSurface = currentTopo.GetHostedSubRegionIds();
            currentSubRegionsAndSurface.Add(topoElementID);

            //Search for the max distance from the Pad to the topography
            //Doesnt matter if it is upwards or downwards, but we will check that, since the ray has to go to one direction
            //This is to estabilish what the distance will be using to create the slope to the right amount
            ReferenceIntersector topoRefIntersec = new ReferenceIntersector(currentSubRegionsAndSurface, FindReferenceTarget.Mesh, current3dView);

            double maxDist = double.NegativeInfinity;

            foreach (Curve currentCurve in outerLoop)
            {
                int numberOfInteractions = 0;
                double increaseAmount = 0;
                double currentParameter = 0;
                EstabilishInteractionPoints(currentCurve, maxPointDistInFeet, out numberOfInteractions, out increaseAmount);

                for (int i = 0; i < numberOfInteractions; i++)
                {

                    if (i == 0)
                        currentParameter = 0;
                    else
                        currentParameter += increaseAmount;

                    XYZ currentPointToEvaluate = currentCurve.Evaluate(currentParameter, true);

                    ReferenceWithContext currentRefContext = topoRefIntersec.FindNearest(currentPointToEvaluate, XYZ.BasisZ);
                    if (currentRefContext == null)
                        currentRefContext = topoRefIntersec.FindNearest(currentPointToEvaluate, XYZ.BasisZ.Negate());

                    if (currentRefContext == null)
                        continue;

                    double currentDist = currentRefContext.Proximity;
                    if (currentDist > maxDist)
                        maxDist = currentDist;
                }
            }

            // if we haven't changed the maxdist yet, something went wrong
            if (maxDist == double.NegativeInfinity)
            {
                message = Properties.Messages.SlopeGradingFromPads_NoTopoAssociate;
                return Result.Failed;
            }

            //Estabilish the offset from the pad
            double offsetDist = maxDist / Math.Tan(targetAngleInRadians);

            using (TopographyEditScope topoEditGroup = new TopographyEditScope(doc, Properties.Messages.SlopeGradingFromPads_Transaction))
            {
                topoEditGroup.Start(topoElementID);
                using (Transaction t = new Transaction(doc, Properties.Messages.SlopeGradingFromPads_Transaction))
                {
                    t.Start();

                    CurveLoop offsetLoop = null;

                    try
                    {
                        offsetLoop = CurveLoop.CreateViaOffset(outerLoop, offsetDist, plannarDirection);
                    }
                    catch
                    {
                        message += Properties.Messages.SlopeGradingFromPads_OuterOffsetLoopIssue;
                        return Result.Failed;
                    }

                    #region DebugCurve Loop

                    //Plane p = new Plane(plannarDirection, plannarOrigin);
                    //SketchPlane sktP = SketchPlane.Create(doc, p);
                    //foreach (Curve currentOffsetCurve in offsetLoop)
                    //{
                    //    doc.Create.NewModelCurve(currentOffsetCurve, sktP);
                    //}

                    #endregion


                    foreach (Curve currentOffsetCurve in offsetLoop)
                    {
                        int numberOfInteractions = 0;
                        double increaseAmount = 0;
                        double currentParameter = 0;

                        EstabilishInteractionPoints(currentOffsetCurve, maxPointDistInFeet, out numberOfInteractions, out increaseAmount);

                        for (int i = 0; i < numberOfInteractions; i++)
                        {
                            if (i == 0)
                                currentParameter = 0;
                            else
                                currentParameter += increaseAmount;

                            XYZ currentPointOffset = currentOffsetCurve.Evaluate(currentParameter, true);

                            ReferenceWithContext currentRefContext = topoRefIntersec.FindNearest(currentPointOffset, XYZ.BasisZ);
                            if (currentRefContext == null)
                                currentRefContext = topoRefIntersec.FindNearest(currentPointOffset, XYZ.BasisZ.Negate());
                            //if we couldn't find points upwards and downwards, the topo is near the border, so we cant add points
                            if (currentRefContext == null)
                                continue;

                            Reference currentReference = currentRefContext.GetReference();
                            XYZ currentPointToAdd = currentReference.GlobalPoint;

                            if (currentTopo.ContainsPoint(currentPointToAdd))
                                continue;

                            IList<XYZ> ListPointToAdd = new List<XYZ>();
                            ListPointToAdd.Add(currentPointToAdd);

                            currentTopo.AddPoints(ListPointToAdd);
                        }
                    }

                    foreach (Curve currentCurve in outerLoop)
                    {
                        int numberOfInteractions = 0;
                        double increaseAmount = 0;
                        double currentParameter = 0;
                        EstabilishInteractionPoints(currentCurve, maxPointDistInFeet, out numberOfInteractions, out increaseAmount);

                        for (int i = 0; i < numberOfInteractions; i++)
                        {
                            if (i == 0)
                                currentParameter = 0;
                            else
                                currentParameter += increaseAmount;

                            XYZ currentPointToAdd = currentCurve.Evaluate(currentParameter, true);

                            if (currentTopo.ContainsPoint(currentPointToAdd))
                                continue;

                            IList<XYZ> ListPointToAdd = new List<XYZ>();
                            ListPointToAdd.Add(currentPointToAdd);

                            currentTopo.AddPoints(ListPointToAdd);
                        }
                    }
                    t.Commit();
                }
                topoEditGroup.Commit(new TopographyFailurePreprocessor());

                return Result.Succeeded;
            }
        }
    }

}
