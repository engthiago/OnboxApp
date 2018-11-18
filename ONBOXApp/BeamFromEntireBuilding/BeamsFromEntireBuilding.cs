using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI.Selection;

namespace ONBOXAppl
{
    [Transaction(TransactionMode.Manual)]
    class BeamsFromEntireBuilding : IExternalCommand
    {
        internal Document doc = null;
        internal Document currentDoc = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            doc = uidoc.Document;
            Selection sel = uidoc.Selection;
           
            try
            {
                IList<FamilyWithImage> allBeamsInfo = Utils.GetInformation.GetAllBeamFamilies(doc);

                if (allBeamsInfo.Count < 1)
                {
                    message = Properties.Messages.BeamsForBuilding_NoBeamFamilyLoaded;
                    return Result.Failed;
                }

                BeamsFromEntireBuildingUI currentUI = new BeamsFromEntireBuildingUI(this, allBeamsInfo);

                currentUI.ShowDialog();

                if (currentUI.DialogResult == false)
                {
                    return Result.Cancelled;
                }

                FamilyWithImage currentFamilyWithImage = currentUI.CurrentFamilyWithImage;
                Family currentFamily = doc.GetElement(new ElementId(currentFamilyWithImage.FamilyID)) as Family;
                //for now we will set the symbol for the first beam of that family
                //later on we will duplicate it and check if it exist or not
                ElementId fsID = currentFamily.GetFamilySymbolIds().First();
                FamilySymbol fs = doc.GetElement(fsID) as FamilySymbol;
                double beamHeight = Utils.ConvertM.cmToFeet(currentUI.BeamHeight);
                double beamWidth = Utils.ConvertM.cmToFeet(currentUI.BeamWidth);
                bool createBeamsInIntermediateLevels = currentUI.CreateBeamsInIntermediateLevels;
                bool ignoreStandardLevels = currentUI.GroupAndDuplicateLevels;
                bool isLinked = currentUI.IsLinked;
                double minWallWidth = Utils.ConvertM.cmToFeet(currentUI.MinWallWidth);
                IList<LevelInfo> allLevelInfo = currentUI.LevelInfoList;
                string standardLevelName = currentUI.StandardLevelName;
                bool pickStandardLevelsByName = currentUI.PickStandardLevelsByName;
                bool isCaseSensitive = currentUI.IsCaseSensitive;
                bool isJoinBeams = (bool)currentUI.checkJoinBeams.IsChecked;

                //Will be set if needed
                int linkedInstanceID = -1;
                Transform linkedInstanceTransf = null;
                IList<string> listOfNames = new List<string>();
                listOfNames.Add(standardLevelName);

                if (isLinked == false)
                {
                    currentDoc = doc;
                }
                else
                {
                    RevitLinkInstance rvtInstance = doc.GetElement(new ElementId(currentUI.SelectedRevitLinkInfo.Id)) as RevitLinkInstance;
                    currentDoc = rvtInstance.GetLinkDocument();
                    linkedInstanceTransf = rvtInstance.GetTotalTransform();
                    linkedInstanceID = currentUI.SelectedRevitLinkInfo.Id;
                }

                if (pickStandardLevelsByName)
                {
                    foreach (LevelInfo currentLevelInfo in allLevelInfo)
                    {
                        if (LevelNameContainsString(currentLevelInfo.levelName, listOfNames, isCaseSensitive))
                        {
                            currentLevelInfo.isStandardLevel = true;
                        }
                    }
                }

                string beamWidthInCm = Math.Round(Utils.ConvertM.feetToCm(beamWidth)).ToString();
                string beamHeigthInCm = Math.Round(Utils.ConvertM.feetToCm(beamHeight)).ToString();
                string newTypeName = beamWidthInCm + " x " + beamHeigthInCm + "cm";
                FamilySymbol currentFamilySymbol = null;

                using (Transaction t = new Transaction(doc, Properties.Messages.BeamsForBuilding_Transaction))
                {
                    t.Start();

                    if (!Utils.FindElements.thisTypeExist(newTypeName, currentFamily.Name, BuiltInCategory.OST_StructuralFraming, doc))
                    {
                        currentFamilySymbol = fs.Duplicate(newTypeName) as FamilySymbol;

                        Parameter parameterB = currentFamilySymbol.LookupParameter("b");
                        Parameter parameterH = currentFamilySymbol.LookupParameter("h");

                        //TODO check for code like this that can throw exceptions
                        parameterB.Set(beamWidth);
                        parameterH.Set(beamHeight);
                    }
                    else
                    {
                        currentFamilySymbol = Utils.FindElements.findElement(newTypeName, currentFamily.Name, BuiltInCategory.OST_StructuralFraming, doc) as FamilySymbol;
                    }

                    currentFamilySymbol.Activate();
                    bool isThisTheFirstStandardLevel = true;

                    foreach (LevelInfo currentLevelInfo in allLevelInfo)
                    {
                        if (currentLevelInfo == null)
                            continue;

                        if (!currentLevelInfo.willBeNumbered)
                            continue;

                        Level currentLevel = currentDoc.GetElement(new ElementId(currentLevelInfo.levelId)) as Level;

                        XYZ minPoint = new XYZ(-9999, -9999, currentLevel.ProjectElevation - 0.1);
                        XYZ maxPoint = new XYZ(9999, 9999, currentLevel.ProjectElevation + 0.1);

                        Outline levelOutLine = new Outline(minPoint, maxPoint);
                        BoundingBoxIntersectsFilter levelBBIntersect = new BoundingBoxIntersectsFilter(levelOutLine, 0.05);
                        BoundingBoxIsInsideFilter levelBBInside = new BoundingBoxIsInsideFilter(levelOutLine, 0.05);

                        LogicalOrFilter levelInsideOrIntersectFilter = new LogicalOrFilter(levelBBInside, levelBBIntersect);

                        IList<Element> wallsThatIntersectsCurrentLevel = new FilteredElementCollector(currentDoc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType()
                            .WherePasses(levelInsideOrIntersectFilter).ToList();

                        if (currentLevelInfo.isStandardLevel && ignoreStandardLevels)
                        {
                            if (isThisTheFirstStandardLevel)
                            {
                                isThisTheFirstStandardLevel = false;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        foreach (Element currentWallElment in wallsThatIntersectsCurrentLevel)
                        {
                            Wall currentWall = currentWallElment as Wall;

                            //wall is valid?
                            if (currentWall == null)
                                continue;

                            if (currentWall.Width < minWallWidth)
                                continue;

                            if (!createBeamsInIntermediateLevels)
                            {
                                if (currentWallElment.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId() != currentLevel.Id &&
                                    currentWallElment.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId() != currentLevel.Id)
                                {
                                    continue;
                                }
                            }

                            LocationCurve wallLocationCurve = currentWall.Location as LocationCurve;

                            //we need to verify if this wall has locationCurve
                            if (wallLocationCurve == null)
                                continue;

                            Curve wallCurve = ((currentWall.Location) as LocationCurve).Curve;

                            if (isLinked)
                                wallCurve = wallCurve.CreateTransformed(linkedInstanceTransf);

                            double wallBaseOffset = currentWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();

                            Level wallBaseLevel = currentDoc.GetElement(currentWall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId()) as Level;

                            double wallBaseTotalHeight = wallBaseLevel.ProjectElevation + wallBaseOffset;

                            double wallTotalHeight = currentWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();

                            if (wallTotalHeight < beamHeight)
                                continue;

                            double levelHeight = currentLevel.ProjectElevation;
                            Level currentLevelInProject = null;

                            if (isLinked)
                            {
                                levelHeight += linkedInstanceTransf.Origin.Z;

                                IList<Level> allLevelsInProject = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Levels).OfClass(typeof(Level)).Cast<Level>().ToList();

                                foreach (Level levelCanditate in allLevelsInProject)
                                {
                                    if (Math.Abs(levelHeight - levelCanditate.ProjectElevation) <= 0.1)
                                    {
                                        currentLevelInProject = levelCanditate;
                                        break;
                                    }
                                }

                                if (currentLevelInProject == null)
                                {
                                    currentLevelInProject = Level.Create(doc, levelHeight);
                                }
                            }
                            else
                            {
                                currentLevelInProject = currentLevel;
                            }

                            FamilyInstance currentBeamInstance = doc.Create.NewFamilyInstance(wallCurve, currentFamilySymbol, currentLevelInProject, Autodesk.Revit.DB.Structure.StructuralType.Beam);

                            AdjustBeamParameters(currentBeamInstance, currentLevelInProject);

                            doc.Regenerate();
                            Utils.CheckFamilyInstanceForIntersection.CheckForDuplicatesAndIntersectingBeams(currentBeamInstance, doc);
                        }
                    }

                    if (isJoinBeams)
                    {
                        IList<FamilyInstance> allBeamsInstances = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
                            .OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();

                        foreach (FamilyInstance currentBeam in allBeamsInstances)
                        {
                            Utils.CheckFamilyInstanceForIntersection.JoinBeamToWalls(currentBeam, doc);
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

        private bool LevelNameContainsString(Level l, IList<string> listOfStrings, bool caseSensitive = false)
        {
            foreach (string currentName in listOfStrings)
            {
                string levelName = l.Name;
                string currentNameTocompare = currentName;

                if (!caseSensitive)
                {
                    currentNameTocompare = currentNameTocompare.ToLower();
                    levelName = levelName.ToLower();
                }

                if (levelName.Contains(currentNameTocompare))
                    return true;
            }
            return false;
        }

        private bool LevelNameContainsString(string targetlevelName, IList<string> listOfStrings, bool caseSensitive = false)
        {
            foreach (string currentName in listOfStrings)
            {
                string levelName = targetlevelName;
                string currentNameTocompare = currentName;

                if (!caseSensitive)
                {
                    currentNameTocompare = currentNameTocompare.ToLower();
                    levelName = levelName.ToLower();
                }

                if (levelName.Contains(currentNameTocompare))
                    return true;
            }
            return false;
        }

        private void AdjustBeamParameters(FamilyInstance targetBeamInstance, Level topLevel, Level bottomLevel, double wallHeight, bool isInvertedUpperBeam = false, bool isLowerBeam = false)
        {
            Parameter newBeamParamRefLevel = targetBeamInstance.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            Parameter newBeamParamFistPoint = targetBeamInstance.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION);
            Parameter newBeamParamSecondPoint = targetBeamInstance.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION);
            Parameter newBeamParamZJust = targetBeamInstance.get_Parameter(BuiltInParameter.Z_JUSTIFICATION);
            Parameter newBeamParamStructuralUsage = targetBeamInstance.get_Parameter(BuiltInParameter.INSTANCE_STRUCT_USAGE_PARAM);

            newBeamParamFistPoint.Set(1);
            newBeamParamSecondPoint.Set(1);

            if (!isLowerBeam)
            {
                if (topLevel != null)
                {
                    newBeamParamRefLevel.Set(topLevel.Id);
                    newBeamParamFistPoint.Set(0);
                    newBeamParamSecondPoint.Set(0);
                }
                else
                {
                    newBeamParamFistPoint.Set(wallHeight);
                    newBeamParamSecondPoint.Set(wallHeight);
                }

                if (isInvertedUpperBeam)
                    newBeamParamZJust.Set(3);
            }
            if (isLowerBeam)
            {
                newBeamParamFistPoint.Set(0);
                newBeamParamSecondPoint.Set(0);
            }

            targetBeamInstance.Document.Regenerate();

        }

        private void AdjustBeamParameters(FamilyInstance targetBeamInstance, Level targetLevel)
        {
            Parameter newBeamParamRefLevel = targetBeamInstance.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            Parameter newBeamParamFistPoint = targetBeamInstance.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION);
            Parameter newBeamParamSecondPoint = targetBeamInstance.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION);

            newBeamParamFistPoint.Set(1);
            newBeamParamSecondPoint.Set(1);

            newBeamParamRefLevel.Set(targetLevel.Id);

            newBeamParamFistPoint.Set(0);
            newBeamParamSecondPoint.Set(0);
        }

        internal IList<LevelInfo> GetAllLevelInfo(bool isLinked, int linkedInstanceID = -1)
        {
            IList<LevelInfo> allLevelInfo = new List<LevelInfo>();
            Document currentTargetDoc = null;

            if (isLinked)
            {
                RevitLinkInstance rvtInstance = doc.GetElement(new ElementId(linkedInstanceID)) as RevitLinkInstance;
                Transform linkedInstanceTransf = rvtInstance.GetTotalTransform();
                currentTargetDoc = rvtInstance.GetLinkDocument();
                allLevelInfo = Utils.GetInformation.GetAllLevelsInfo(currentTargetDoc);
            }
            else
            {
                currentTargetDoc = doc;
                allLevelInfo = Utils.GetInformation.GetAllLevelsInfo(currentTargetDoc);
            }

            return allLevelInfo;
        }

    }
}
