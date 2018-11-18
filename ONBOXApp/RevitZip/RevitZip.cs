using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

#if R2016 || R2017
using Autodesk.Revit.Utility;
#else
using Autodesk.Revit.DB.Visual;
#endif

namespace ONBOXAppl
{
    [Transaction(TransactionMode.Manual)]
    class RevitZip : IExternalCommand
    {
        IList<string> ADDITIONAL_RENDER_PATHS = new List<string>();
        string DEFAULT_AUTODESK_SHARED_FOLDER = "";
        IList<string> ALL_AUTODESK_SHARED_SUBFOLDERS = new List<string>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            IList<string> allDependenciesStrings = new List<string>();

            try
            {
                uidoc.Document.Save();
            }
            catch (Exception)
            {
                message = Properties.Messages.PackageProject_SaveProjectFirst;
                return Result.Cancelled;
            }

            string RevitFile = uidoc.Document.PathName;
            string RevitFileName = Path.GetFileName(RevitFile);
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Filter = Properties.Messages.PackageProject_FileTypeName + "|*.zip";
            saveDialog.FileName = Path.GetFileNameWithoutExtension(RevitFileName) + ".zip";

            if (saveDialog.ShowDialog() != DialogResult.OK)
            {
                return Result.Cancelled;
            }

            string saveFile = saveDialog.FileName;
            string saveDirectory = Path.GetDirectoryName(saveFile) + @"\Project\";

            //RevitZipUI currentUI = new RevitZipUI();
            //currentUI.ShowDialog();

            //if (currentUI.DialogResult == false)
            //{
            //    return Result.Cancelled;
            //}

            PackageProject(uidoc, allDependenciesStrings, saveDirectory, saveFile);

            return Result.Succeeded;
        }

        void PackageProject(UIDocument targetUiDoc, IList<string> targetList, string targetSaveDirectory, string targetFileName)
        {
            Document doc = targetUiDoc.Document;

            #region Configure Additional Rendering Folders
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string revitVersion = targetUiDoc.Application.Application.VersionName;
            string AutodeskPath = "\\Autodesk\\Revit\\" + revitVersion + "\\Revit.ini";

            string fullRevitIniPath = appDataFolder + AutodeskPath;

            if (File.Exists(fullRevitIniPath))
            {
                using (StreamReader stR = new StreamReader(fullRevitIniPath))
                {
                    string currentLine;
                    while ((currentLine = stR.ReadLine()) != null)
                    {
                        if (currentLine.Contains("AdditionalRenderAppearancePaths"))
                        {
                            StringBuilder strB = new StringBuilder(currentLine);
                            strB.Replace("AdditionalRenderAppearancePaths=", "");
                            ADDITIONAL_RENDER_PATHS = GetAllPathsOrFileNamesFromAutodeskPathFormat(strB.ToString());
                            break;
                        }
                    }
                    stR.Close();
                }
            }
            #endregion

            DEFAULT_AUTODESK_SHARED_FOLDER = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Common Files\Autodesk Shared\";

            #region Configure Autodesk Shared Subfolders

            if (Directory.Exists(DEFAULT_AUTODESK_SHARED_FOLDER))
            {
                ALL_AUTODESK_SHARED_SUBFOLDERS = Directory.GetDirectories(DEFAULT_AUTODESK_SHARED_FOLDER, "*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        IList<string> allFiles = Directory.GetFiles(f).ToList();
                        foreach (string filename in allFiles)
                        {
                            if (IsSupportedTextureFileFormat(filename))
                            {
                                return true;
                            }
                        }
                        return false;
                    }).ToList();
            }

            #endregion

            IList<Document> documentsList = new List<Document>();
            documentsList.Add(doc);
            targetList.Add(doc.PathName);

            IList<RevitLinkInstance> allRevitInstance = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks)
                .WhereElementIsNotElementType().Cast<RevitLinkInstance>().ToList();
            foreach (RevitLinkInstance currentRevitLink in allRevitInstance)
            {
                if (currentRevitLink != null)
                {
                    Document currentLinkedDoc = currentRevitLink.GetLinkDocument();
                    if (currentLinkedDoc != null)
                    {
                        if (!documentsList.Contains(currentLinkedDoc))
                        {
                            documentsList.Add(currentLinkedDoc);
                        }
                    }
                }
            }

            GetKeynotesAssemblyCodesSharedParamAndLinks(documentsList, targetList, doc);
            GetTexturesFromAllDocuments(documentsList, targetList);

            CopyFiles(targetList, doc, targetSaveDirectory);
            ZipProject(targetSaveDirectory, targetFileName);
        }

        void ZipProject(string targetSaveDirectory, string targetFileName)
        {
            ZipFile.CreateFromDirectory(targetSaveDirectory, targetFileName);
        }

        void GetTexturesFromAllDocuments(IList<Document> targetDocumentsList, IList<string> targetList)
        {
            foreach (Document currentDoc in targetDocumentsList)
            {
                IList<Element> allMat = new FilteredElementCollector(currentDoc).OfClass(typeof(Material)).ToList();
                foreach (Element currentMatElemn in allMat)
                {
                    Material currentMat = currentMatElemn as Material;
                    AppearanceAssetElement currentAppearance = currentDoc.GetElement(currentMat.AppearanceAssetId) as AppearanceAssetElement;

                    foreach (string currentString in GetAssetPropValueFromMaterial(currentAppearance, targetList))
                    {
                        targetList.Add(currentString);
                    }
                }
            }
        }

        void GetKeynotesAssemblyCodesSharedParamAndLinks(IList<Document> targetDocumentsList, IList<string> targetList, Document activeDoc)
        {
            foreach (Document currentDoc in targetDocumentsList)
            {
                ModelPath targetLocation = ModelPathUtils.ConvertUserVisiblePathToModelPath(currentDoc.PathName);
                TransmissionData targetData = TransmissionData.ReadTransmissionData(targetLocation);

                if (targetData != null)
                {
                    ICollection<ElementId> externalReferences = targetData.GetAllExternalFileReferenceIds();

                    foreach (ElementId currentFileId in externalReferences)
                    {
                        if (currentFileId != ElementId.InvalidElementId)
                        {
                            ExternalFileReference extRef = targetData.GetLastSavedReferenceData(currentFileId);
                            //TODO CORRECT PROBLEMATIC IF STATEMENT HERE!!!!!!!!!!!!!!!!!!
                            if (extRef.GetLinkedFileStatus() != LinkedFileStatus.Invalid)
                            {
                                ModelPath currenFileLink = extRef.GetAbsolutePath();
                                if (!currenFileLink.Empty)
                                {
                                    string currentFileLinkString = ModelPathUtils.ConvertModelPathToUserVisiblePath(currenFileLink);
                                    CheckStringAValidLinkPathCorrectItAndAddToList(currentFileLinkString, targetList, activeDoc);
                                }
                            }
                        }
                    }
                }
            }
        }

        void CopyFiles(IList<string> targetList, Document mainDoc, string targetSaveDirectory)
        {
            if (!Directory.Exists(targetSaveDirectory))
            {
                Directory.CreateDirectory(targetSaveDirectory);
            }

            foreach (string currentDependecy in targetList)
            {
                string fileName = Path.GetFileName(currentDependecy);

                if (IsSupportedTextureFileFormat(fileName)) //When we have images files
                {
                    if (!Directory.Exists(targetSaveDirectory + "Images\\"))
                    {
                        Directory.CreateDirectory(targetSaveDirectory + "Images\\");
                    }

                    if (!File.Exists(targetSaveDirectory + "Images\\" + fileName))
                    {
                        File.Copy(currentDependecy, targetSaveDirectory + "Images\\" + fileName);
                    }
                }
                else if (fileName.Contains(".rvt")) //When we have Revit files
                {
                    if (!mainDoc.PathName.Contains(fileName)) //It is the main file or is links?
                    {
                        if (!Directory.Exists(targetSaveDirectory + "Revit Links\\"))
                        {
                            Directory.CreateDirectory(targetSaveDirectory + "Revit Links\\");
                        }

                        if (!File.Exists(targetSaveDirectory + "Revit Links\\" + fileName))
                        {
                            File.Copy(currentDependecy, targetSaveDirectory + "Revit Links\\" + fileName);
                        }
                    }
                    else // This is the main file
                    {
                        if (!File.Exists(targetSaveDirectory + fileName))
                        {
                            File.Copy(currentDependecy, targetSaveDirectory + fileName);
                        }
                    }
                }
                else if (IsSupportedCADFileFormat(fileName))
                {
                    if (!Directory.Exists(targetSaveDirectory + "CAD Links\\"))
                    {
                        Directory.CreateDirectory(targetSaveDirectory + "CAD Links\\");
                    }

                    if (!File.Exists(targetSaveDirectory + "CAD Links\\" + fileName))
                    {
                        File.Copy(currentDependecy, targetSaveDirectory + "CAD Links\\" + fileName);
                    }
                }
                else if (IsIFCFileFormat(fileName))
                {
                    if (!Directory.Exists(targetSaveDirectory + "IFC Links\\"))
                    {
                        Directory.CreateDirectory(targetSaveDirectory + "IFC Links\\");
                    }

                    if (!File.Exists(targetSaveDirectory + "IFC Links\\" + fileName))
                    {
                        File.Copy(currentDependecy, targetSaveDirectory + "IFC Links\\" + fileName);
                    }
                }
                else if (isSupportedPointCloudFormat(fileName))
                {
                    if (!Directory.Exists(targetSaveDirectory + "Point Clouds\\"))
                    {
                        Directory.CreateDirectory(targetSaveDirectory + "Point Clouds\\");
                    }

                    if (!File.Exists(targetSaveDirectory + "Point Clouds\\" + fileName))
                    {
                        File.Copy(currentDependecy, targetSaveDirectory + "Point Clouds\\" + fileName);
                    }
                }
                else
                {
                    if (!Directory.Exists(targetSaveDirectory + "Extras\\"))
                    {
                        Directory.CreateDirectory(targetSaveDirectory + "Extras\\");
                    }

                    if (!File.Exists(targetSaveDirectory + "Extras\\" + fileName))
                    {
                        File.Copy(currentDependecy, targetSaveDirectory + "Extras\\" + fileName);
                    }
                }
            }
        }

        IList<string> GetAssetPropValueFromMaterial(AppearanceAssetElement currentAppearance, IList<string> targetList)
        {
            IList<string> valuesToReturn = new List<string>();

            if (currentAppearance != null)
            {
                Asset thisAsset = currentAppearance.GetRenderingAsset();
                if (thisAsset != null)
                {
                    for (int i = 0; i < thisAsset.Size; i++)
                    {
                        AssetProperty currentProp = thisAsset[i];

                        if (currentProp != null)
                        {
                            AssetPropertyString currentPropString = currentProp as AssetPropertyString;
                            if (currentPropString != null)
                            {
                                if (currentPropString.Value != null && currentPropString.Value != "")
                                    CheckStringAValidTexturePathCorrectItAndAddToList(currentPropString.Value, targetList);
                            }
                        }

                        IList<AssetProperty> allProp = currentProp.GetAllConnectedProperties();

                        if (allProp != null && allProp.Count > 0)
                        {
                            foreach (AssetProperty currentConnectedProp in allProp)
                            {

#if R2016 || R2017
                                if (currentConnectedProp.Type == AssetPropertyType.APT_Asset)
                                {
                                    Asset currentConnectedAsset = currentConnectedProp as Asset;
                                    if (currentConnectedAsset != null)
                                    {
                                        for (int j = 0; j < currentConnectedAsset.Size; j++)
                                        {
                                            AssetProperty currentConnectedAssetProp = currentConnectedAsset[j];
                                            if (currentConnectedAssetProp != null)
                                            {
                                                AssetPropertyString currentConnectedAssetPropString = currentConnectedAssetProp as AssetPropertyString;
                                                if (currentConnectedAssetPropString != null)
                                                {
                                                    if (currentConnectedAssetPropString.Value != null && currentConnectedAssetPropString.Value != "")
                                                        CheckStringAValidTexturePathCorrectItAndAddToList(currentConnectedAssetPropString.Value, targetList);
                                                }
                                            }
                                        }
                                    }
                                }
#else
                                if (currentConnectedProp.Type == AssetPropertyType.Asset)
                                {
                                    Asset currentConnectedAsset = currentConnectedProp as Asset;
                                    if (currentConnectedAsset != null)
                                    {
                                        for (int j = 0; j < currentConnectedAsset.Size; j++)
                                        {
                                            AssetProperty currentConnectedAssetProp = currentConnectedAsset[j];
                                            if (currentConnectedAssetProp != null)
                                            {
                                                AssetPropertyString currentConnectedAssetPropString = currentConnectedAssetProp as AssetPropertyString;
                                                if (currentConnectedAssetPropString != null)
                                                {
                                                    if (currentConnectedAssetPropString.Value != null && currentConnectedAssetPropString.Value != "")
                                                        CheckStringAValidTexturePathCorrectItAndAddToList(currentConnectedAssetPropString.Value, targetList);
                                                }
                                            }
                                        }
                                    }
                                }
#endif

                            }
                        }
                    }
                }
            }
            return valuesToReturn;
        }

        void CheckStringAValidTexturePathCorrectItAndAddToList(string targetPath, IList<string> targetList)
        {
            //FOR DEBUG ONLY
            //if (targetPath.Contains("Thermal_Moisture.Roof.Tiles.Spanish.Brown.Colour.png"))
            //{
            //    targetPath = targetPath + "";
            //}

            if (File.Exists(targetPath))
            {
                string file = Path.GetFileName(targetPath);
                string dir = Path.GetDirectoryName(targetPath);
                string temp = Path.GetTempPath();

                if (!dir.Contains(temp))
                {
                    if (!targetList.Contains(targetPath))
                        targetList.Add(targetPath);
                }

            }
            else if (IsSupportedTextureFileFormat(targetPath))
            {
                //If we get to this point we have a missing (red error) texture or
                //Or revit have set the path using the Additional Render Paths (on the options menu) + the missing file name or
                //we have a special case (Autodesk standard) of separating paths with the char |

                #region MoreDetailAboutMissingFiles
                //If theres a missing texture Revit will use the AdditionalRenderAppearancePaths list (See the options menu)
                //to try to locate the textures. It is importante to note that revit wont change the original location (so the rvt file wont change)
                //it will temporarily create a path using the Render appearance path and the file name
                //so it is important to search in the list of the directories to find the file using the same technique that revit uses;
                #endregion

                if (targetPath.Contains("|")) //if we have the autodesk standard case
                {
                    string currentSubstring = targetPath;

                    do
                    {
                        currentSubstring = currentSubstring.Substring(targetPath.IndexOf('|'));
                        string currentSubstringPath = targetPath.Substring(0, targetPath.IndexOf('|'));

                        #region (CURRENT DEACTIVE - is working without it for now) This Part is to correct the path so it will be in the standard, for instance C:\\Folder1\\SubFolder1\\SubFolder2
                        //if (currentSubstringPath.Contains("\\"))
                        //{
                        //    StringBuilder tempBuilder = new StringBuilder(currentSubstringPath);
                        //    tempBuilder.Replace("\\", "");
                        //    currentSubstringPath = tempBuilder.ToString();
                        //}
                        //if (currentSubstringPath.Contains("/"))
                        //{
                        //    StringBuilder tempBuilder = new StringBuilder(currentSubstringPath);
                        //    tempBuilder.Replace("/", "\\");
                        //    currentSubstringPath = tempBuilder.ToString();
                        //}
                        #endregion

                        if (File.Exists(currentSubstringPath))
                        {
                            if (!targetList.Contains(currentSubstringPath))
                                targetList.Add(currentSubstringPath);
                        }

                        // TODO REMOVE IT IF ITS WORKING FINE ALREADY
                        //else if (File.Exists(DEFAULT_AUTODESK_SHARED_FOLDER + currentSubstringPath))
                        //{
                        //    if (!targetList.Contains(DEFAULT_AUTODESK_SHARED_FOLDER + currentSubstringPath))
                        //        targetList.Add(DEFAULT_AUTODESK_SHARED_FOLDER + currentSubstringPath);
                        //}

                    }
                    while (currentSubstring.Contains("|"));
                }
                else
                {
                    if (ADDITIONAL_RENDER_PATHS.Count > 0) //try to find the texture using the paths, if theres paths configured
                    {
                        foreach (string currentPath in ADDITIONAL_RENDER_PATHS)
                        {
                            string file = Path.GetFileName(targetPath);
                            string renderPathAndFileName = currentPath + file;

                            if (File.Exists(renderPathAndFileName))
                            {
                                if (!targetList.Contains(renderPathAndFileName))
                                    targetList.Add(renderPathAndFileName);
                            }
                        }
                    }

                    if (ALL_AUTODESK_SHARED_SUBFOLDERS.Count > 0)//try to find the file inside the directories inside the Autodesk Shared folders
                    {
                        foreach (string acturalPath in ALL_AUTODESK_SHARED_SUBFOLDERS)
                        {
                            string renderPathAndFileName2 = acturalPath + @"\" + targetPath;

                            if (File.Exists(renderPathAndFileName2))
                            {
                                if (!targetList.Contains(renderPathAndFileName2))
                                    targetList.Add(renderPathAndFileName2);
                            }
                        }
                    }
                }
            }
        }

        void CheckStringAValidLinkPathCorrectItAndAddToList(string targetPath, IList<string> targetList, Document activeDocument)
        {
            //FOR DEBUG ONLY
            //if (targetPath.Contains(""))
            //{
            //    targetPath = targetPath + "";
            //}

            if (string.IsNullOrWhiteSpace(targetPath))
                return;

            if (File.Exists(targetPath))
            {
                string file = Path.GetFileName(targetPath);
                string dir = Path.GetDirectoryName(targetPath);

                if (!targetList.Contains(targetPath))
                    targetList.Add(targetPath);
            }
            else
            {
                //TODO Improve the code here, currently its searching only for the folder of the active document of the UI and its subfolders
                string documentPath = activeDocument.PathName;
                string documentFolder = Path.GetDirectoryName(documentPath);

                IList<string> SubDir = new List<string>();
                SubDir.Add(documentFolder);

                string[] subFolders = Directory.GetDirectories(documentFolder, "*", SearchOption.AllDirectories);

                foreach (string currentSubdir in subFolders)
                {
                    SubDir.Add(currentSubdir);
                }

                foreach (string currentDir in SubDir)
                {
                    string fileName = null;
                    try { fileName = Path.GetFileName(targetPath); } catch { }

                    if (fileName != null)
                    {
                        string fullDir = currentDir + @"\" + fileName;
                        if (File.Exists(fullDir))
                        {
                            if (!targetList.Contains(fullDir))
                                targetList.Add(fullDir);
                            break;
                        }
                    }
                }
            }
        }

        IList<string> GetAllPathsOrFileNamesFromAutodeskPathFormat(string targetPath)
        {
            IList<string> ListOfPathsOrFileNames = new List<string>();
            string currentSubstring = targetPath;
            string currentSubstringPath = "";
            do
            {
                if (currentSubstring.Contains("|")) //Get the first | symbol, so get the path and remove it from the string
                {
                    currentSubstringPath = currentSubstring.Substring(0, currentSubstring.IndexOf('|'));
                    currentSubstring = currentSubstring.Remove(0, currentSubstring.IndexOf('|') + 1);
                }
                else //if we dont get the | it means that we are on the last path, so get the last path and clear the string
                {
                    currentSubstringPath = currentSubstring.Substring(0);
                    currentSubstring = "";
                }

                //Replace the "..\\..\\.." to "C:\\" of the autodesk standard
                if (currentSubstringPath.Contains("..\\..\\..\\"))
                {
                    currentSubstringPath = currentSubstringPath.Replace("..\\..\\..", Path.GetPathRoot(Environment.SystemDirectory));
                }
                ListOfPathsOrFileNames.Add(currentSubstringPath);
            }
            while (currentSubstring != "");
            return ListOfPathsOrFileNames;
        }

        bool IsSupportedTextureFileFormat(string targetPath)
        {
            string targetExtension = null;
            try { targetExtension = Path.GetExtension(targetPath); } catch { return false; };

            if (targetExtension.Contains(".jpg")
                || targetExtension.Contains(".png")
                || targetExtension.Contains(".tga")
                || targetExtension.Contains(".bmp")
                || targetExtension.Contains(".tif")
                || targetExtension.Contains(".avi")
                || targetExtension.Contains(".pan")
                || targetExtension.Contains(".ivr")
                || targetExtension.Contains(".exr")
                )
            {
                return true;
            }
            return false;
        }

        bool IsSupportedCADFileFormat(string targetPath)
        {
            string targetExtension = null;
            try { targetExtension = Path.GetExtension(targetPath); } catch { return false; };

            if (targetExtension.Contains(".dwg")
                || targetExtension.Contains(".dxf")
                || targetExtension.Contains(".dgn")
                || targetExtension.Contains(".dwf")
                || targetExtension.Contains(".dwfx")
                || targetExtension.Contains(".sat")
                || targetExtension.Contains(".skp")
                )
            {
                return true;
            }
            return false;
        }

        bool IsIFCFileFormat(string targetPath)
        {
            string targetExtension = null;
            try { targetExtension = Path.GetExtension(targetPath); } catch { return false; };

            if (targetExtension.Contains(".ifc"))
            {
                return true;
            }
            return false;
        }

        bool isSupportedPointCloudFormat(string targetPath)
        {
            string targetExtension = null;
            try { targetExtension = Path.GetExtension(targetPath); } catch { return false; };

            if (targetExtension.Contains(".rcp")
                || targetExtension.Contains(".rcs")
                || targetExtension.Contains(".3dd")
                || targetExtension.Contains(".asc")
                || targetExtension.Contains(".cl3")
                || targetExtension.Contains(".clr")
                || targetExtension.Contains(".fls")
                || targetExtension.Contains(".ixf")
            )
            {
                return true;
            }
            return false;
        }
    }
}
