using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;

namespace ONBOXAppl
{

    enum BeamOrientation { Vertical, Horizontal, Diagonal }
    enum BeamRenumberOrder { Vertical, Horizontal, None }

    [Transaction(TransactionMode.Manual)]
    class RenumberBeams : IExternalCommand
    {
        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513364"));

        static UIDocument uidoc = null;

        IList<Element> allBeams = new List<Element>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            allBeams = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilyInstance)).ToList();

            if (allBeams.Count == 0)
            {
                message = Properties.Messages.RenumberBeams_NoBeamFamilyLoaded;
                return Result.Failed;
            }

            RenumberBeamsUI reBeamsUI = new RenumberBeamsUI();

            if (reBeamsUI.ShowDialog() == true)
            {
                DoTheRenumberingBeams();
            }

            return Result.Succeeded;
        }

        private void DoTheRenumberingBeams()
        {
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            IList<Level> allLevel = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level))
                .WhereElementIsNotElementType().Cast<Level>().OrderBy(l => l.Elevation).ToList();

            XYZ pointOfInterest = new XYZ(-9999, 9999, -9999);

            IList<Element> HorizontalBeams = allBeams.Where(b => verifyBeamOrientation(b) == BeamOrientation.Horizontal).OrderBy(e => GetNearestEndPoint(e, pointOfInterest).DistanceTo(pointOfInterest)).ToList();
            IList<Element> VerticalBeams = allBeams.Where(b => verifyBeamOrientation(b) == BeamOrientation.Vertical).OrderBy(e => GetNearestEndPoint(e, pointOfInterest).DistanceTo(pointOfInterest)).ToList();

            int counter = 0;
            string prefix = "V";
            int decimalPlaces = 2;

            try
            {
                using (Transaction t = new Transaction(doc, Properties.Messages.RenumberBeams_RenumberBeams))
                {
                    t.Start();
                    foreach (Level currentLevel in allLevel)
                    {

                        LevelInfo lvlInfo = ONBOXApplication.storedBeamLevelInfo.Where(l => l.levelId == currentLevel.Id.IntegerValue).First() as LevelInfo;

                        //checks if the current level will be numbered
                        if (lvlInfo.willBeNumbered == false)
                            continue;

                        prefix = lvlInfo.levelPrefix;
                        decimalPlaces = ONBOXApplication.BeamsDecimalPlaces;

                        IList<Element> HorizontalBeamsInLevel = HorizontalBeams.Where(b => b.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId() == currentLevel.Id).ToList();
                        IList<Element> VerticalBeamsInLevel = VerticalBeams.Where(b => b.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId() == currentLevel.Id).ToList();
                        IList<Element> allBeamsInLevel = allBeams.Where(b => b.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId() == currentLevel.Id).ToList();

                        //Checks if the horizontal or vertical comes first, or allbeams
                        if (ONBOXApplication.storedBeamRenumOrder == BeamRenumberOrder.Horizontal)
                        {
                            RenumberListOfBeams(HorizontalBeamsInLevel, ref counter, prefix, decimalPlaces);
                            RenumberListOfBeams(VerticalBeamsInLevel, ref counter, prefix, decimalPlaces);
                        }
                        else if (ONBOXApplication.storedBeamRenumOrder == BeamRenumberOrder.Vertical)
                        {
                            RenumberListOfBeams(VerticalBeamsInLevel, ref  counter, prefix, decimalPlaces);
                            RenumberListOfBeams(HorizontalBeamsInLevel, ref counter, prefix, decimalPlaces);
                        }
                        else
                        {
                            RenumberListOfBeams(allBeamsInLevel, ref counter, prefix, decimalPlaces);
                        }

                        //Reset counter for the next level if the user wants to
                        if (ONBOXApplication.isNumBeamLevel == true)
                        {
                            counter = 0;
                        }

                    }
                    t.Commit();
                }

            }
            catch (Exception excep)
            {
                ExceptionManager eManager = new ExceptionManager(excep);
            }


        }

        private void RenumberListOfBeams(IList<Element> targetBeams, ref  int currentCounter, string prefix, int decimalPlaces)
        {

            foreach (Element currentBeamElem in targetBeams)
            {
                BeamTypesInfo beamInfo = ONBOXApplication.storedBeamTypesInfo.Where(b => b.TypeId == currentBeamElem.GetTypeId().IntegerValue).First();
                if (beamInfo.WillBeNumbered == false)
                {
                    continue;
                }
                string typePrefix = beamInfo.TypePrefix;
                currentCounter++;
                string currentName = prefix + typePrefix + InsertZeros(currentCounter, decimalPlaces) + currentCounter;
                currentBeamElem.get_Parameter(BuiltInParameter.DOOR_NUMBER).Set((currentName).ToString());
            }
        }

        //verify which orientation the specify Beam is
        private BeamOrientation verifyBeamOrientation(Element targetBeam)
        {
            Curve beamCurve = (targetBeam.Location as LocationCurve).Curve;
            XYZ direction = ((beamCurve.GetEndPoint(1) - beamCurve.GetEndPoint(0)).Normalize());
            double angle = Utils.ConvertM.radiansToDegrees(direction.AngleOnPlaneTo(XYZ.BasisX, XYZ.BasisZ));

            if ((angle >= 315) && (angle <= 360.01))
            {
                return BeamOrientation.Horizontal;
            }
            if ((angle >= 0) && (angle <= 45.01))
            {
                return BeamOrientation.Horizontal;
            }
            if ((angle >= 135) && (angle <= 225.01))
            {
                return BeamOrientation.Horizontal;
            }

            if ((angle > 45.01) && (angle < 135))
            {
                return BeamOrientation.Vertical;
            }

            if ((angle > 225.01) && (angle < 315))
            {
                return BeamOrientation.Vertical;
            }
            return BeamOrientation.Vertical;
        }

        private int HowManyDigits(int currentCounter)
        {
            return currentCounter.ToString().Count();
        }

        private string InsertZeros(int currentCounter, int decimalPlaces)
        {
            int numberOfZeros = decimalPlaces - HowManyDigits(currentCounter);
            string currentCounterString = "";

            for (int i = numberOfZeros; i > 0; i--)
            {
                currentCounterString += (0).ToString();
            }

            return currentCounterString;
        }

        private XYZ GetNearestEndPoint(Element e, XYZ targetPoint)
        {
            Curve beamCurve = (e.Location as LocationCurve).Curve;
            XYZ firstPoint = beamCurve.GetEndPoint(0);
            XYZ seconPoint = beamCurve.GetEndPoint(1);
            XYZ nearestPoint = null;

            if (firstPoint.DistanceTo(targetPoint) < seconPoint.DistanceTo(targetPoint))
            {
                nearestPoint = firstPoint;
            }
            else
            {
                nearestPoint = seconPoint;
            }

            return nearestPoint;

        }

        static internal IList<BeamTypesInfo> GetBeamTypesInfo()
        {
            IList<ElementId> allUsedBeamTypesIDs = new List<ElementId>();
            IList<BeamTypesInfo> AllUsedBeamTypesInfo = new List<BeamTypesInfo>();

            IList<Element> allBeamgInstances = new FilteredElementCollector(uidoc.Document).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType().ToList();

            foreach (Element currentElement in allBeamgInstances)
            {
                if (!allUsedBeamTypesIDs.Contains(currentElement.GetTypeId()))
                {
                    int typeID = currentElement.GetTypeId().IntegerValue;
                    string typeName = currentElement.Name;
                    BeamTypesInfo currentBeamInfo = new BeamTypesInfo() { TypeName = typeName, TypeId = typeID, WillBeNumbered = true, TypePrefix = "" };

                    allUsedBeamTypesIDs.Add(currentElement.GetTypeId());
                    AllUsedBeamTypesInfo.Add(currentBeamInfo);
                }
            }

            return AllUsedBeamTypesInfo;
        }

        static internal IList<LevelInfo> GetAllLevelInfo()
        {
            return Utils.GetInformation.GetAllLevelsInfo(uidoc);
        }

    }
}
