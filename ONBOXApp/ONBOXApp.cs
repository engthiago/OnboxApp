using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Media.Imaging;
using System.IO;
using System.Xml;
using System.Globalization;
using System.Reflection;
using Autodesk.Revit.UI.Events;

namespace ONBOXAppl
{
    enum ExternalOperation { Create, Reload, Unsubscribe, LoadFamily };

    partial class ONBOXApplication : IExternalApplication
    {
        static internal string licensePath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\OnboxApps\\";
        static internal string onboxVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        static internal string licenseFile = licensePath + "oba" + onboxVersion + ".lc";
        static internal string revitVersion = "";
        static internal string revitVersionName = "";
        static internal string revitVersionBuild = "";
        static internal string revitLanguage = "";
        static internal bool isRegister = false;

        static internal ONBOXApplication onboxApp = null;
        internal UIApplication uiApp = null;

        internal BeamsFromColumnsUI beamsFromColumnsWindow = null;
        internal RequestBeamsFromColumnsHandler requestBeamsFromColumnsHandler = null;
        internal ExternalEvent externalBeamFromColumnsEvent = null;

        internal BeamsFromWallsUI beamsFromWallsWindow = null;
        internal RequestBeamsFromWallsHandler requestBeamsFromWallsHandler = null;
        internal ExternalEvent externalBeamsFromWallsEvent = null;

        internal BeamsUpdateUI beamsUpdateWindow = null;
        internal RequestBeamsUpdateHandler requestBeamsUpdateHandler = null;
        internal ExternalEvent externalBeamsUpdateEvent = null;

 
        internal ElementJoinSelectUI joinElementSelectWindow = null;
        internal RequestElementsSelectHandler requestjoinElementSelecHandler = null;
        internal ExternalEvent joinElementSelecEvent = null;


        static AddInId appId = new AddInId(new Guid("33DC8041-8285-4A21-B0A3-63DCC326A4C8"));

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            //Creates the ONBOX Ribbon and implements buttons
            CreateONBOXRibbon(application);

            //Stores this application
            onboxApp = this;
            revitVersion = application.ControlledApplication.VersionNumber;
            revitVersionName = application.ControlledApplication.VersionName;
            revitVersionBuild = application.ControlledApplication.VersionBuild;
            revitLanguage = application.ControlledApplication.Language.ToString();

            return Result.Succeeded;
        }

        internal void ShowBeamsFromColumnsUI()
        {
            if (beamsFromColumnsWindow == null || beamsFromColumnsWindow.isShowned == false)
            {
                //Implements the viewChange(view activated) event
                if (uiApp != null)
                {
                    uiApp.ViewActivated += BeamsFromColumns_ViewActivated;
                }

                //Implements the create (Beams from columns) event
                requestBeamsFromColumnsHandler = new RequestBeamsFromColumnsHandler();
                externalBeamFromColumnsEvent = ExternalEvent.Create(requestBeamsFromColumnsHandler);

                //Implements the BeamsFromColumns UI Window
                //Pass the external event and the event handler to the instance of the UI
                beamsFromColumnsWindow = new BeamsFromColumnsUI(externalBeamFromColumnsEvent, requestBeamsFromColumnsHandler);
                beamsFromColumnsWindow.Show();
                beamsFromColumnsWindow.isShowned = true;
            }
        }

        internal void ShowBeamsFromWallsUI()
        {
            if (beamsFromWallsWindow == null || beamsFromWallsWindow.isShowned == false)
            {
                //Implements the viewChange(view activated) event
                if (uiApp != null)
                {
                    uiApp.ViewActivated += BeamsFromWalls_ViewActivated;
                }

                //Impements the create (Beams from walls) events
                requestBeamsFromWallsHandler = new RequestBeamsFromWallsHandler();
                externalBeamsFromWallsEvent = ExternalEvent.Create(requestBeamsFromWallsHandler);

                //Implements the BeamsFromWalls UI Window
                //Pass the external event and the event handler to the instance of the UI
                beamsFromWallsWindow = new BeamsFromWallsUI(externalBeamsFromWallsEvent, requestBeamsFromWallsHandler);
                beamsFromWallsWindow.Show();
            }
        }

        internal void ShowBeamsUpdateUI()
        {
            if (beamsUpdateWindow == null || beamsUpdateWindow.isShowned == false)
            {
                if (uiApp != null)
                {
                    uiApp.ViewActivated += BeamsUpdate_ViewActivated;
                }

                requestBeamsUpdateHandler = new RequestBeamsUpdateHandler();
                externalBeamsUpdateEvent = ExternalEvent.Create(requestBeamsUpdateHandler);

                beamsUpdateWindow = new BeamsUpdateUI(externalBeamsUpdateEvent, requestBeamsUpdateHandler);
                beamsUpdateWindow.Show();
            }
        }

        internal void ShowJoinElementsSelectUI()
        {
            if (joinElementSelectWindow == null || joinElementSelectWindow.isShowned == false)
            {
                if (uiApp != null)
                {
                    uiApp.ViewActivated += joinElementSelectWindow_ViewActivated;
                }

                requestjoinElementSelecHandler = new RequestElementsSelectHandler();
                joinElementSelecEvent = ExternalEvent.Create(requestjoinElementSelecHandler);

                joinElementSelectWindow = new ElementJoinSelectUI(joinElementSelecEvent, requestjoinElementSelecHandler);
                joinElementSelectWindow.Show();
                joinElementSelectWindow.isShowned = true;

            }
        }

        internal void joinElementSelectWindow_ViewActivated(object sender, ViewActivatedEventArgs e)
        {
            
        }

        internal void BeamsUpdate_ViewActivated(object sender, Autodesk.Revit.UI.Events.ViewActivatedEventArgs e)
        {
            if (beamsUpdateWindow != null || beamsUpdateWindow.isShowned)
            {
                beamsUpdateWindow.ReloadBeamFamilies(uiApp);
            }
        }

        internal void BeamsFromWalls_ViewActivated(object sender, Autodesk.Revit.UI.Events.ViewActivatedEventArgs e)
        {
            if (beamsFromWallsWindow != null || beamsFromWallsWindow.isShowned)
            {
                beamsFromWallsWindow.ReloadBeamFamilies(uiApp);
            }
        }

        internal void BeamsFromColumns_ViewActivated(object sender, Autodesk.Revit.UI.Events.ViewActivatedEventArgs e)
        {
            if (beamsFromColumnsWindow != null || beamsFromColumnsWindow.isShowned)
            {
                beamsFromColumnsWindow.ReloadBeamFamilies(uiApp);
            }
        }

        private void CreateONBOXRibbon(UIControlledApplication application)
        {
            string dll = System.Reflection.Assembly.GetExecutingAssembly().Location;

            string ribbonONBOX = "Onbox App";
            application.CreateRibbonTab(ribbonONBOX);

            IList<RibbonPanel> allProjectPanels = new List<RibbonPanel>();
            IList<RibbonPanel> allFreePanels = new List<RibbonPanel>();

            RibbonPanel panelRenumber = application.CreateRibbonPanel(ribbonONBOX, Properties.RibbonLanguage.RenumberElements_Title);
            RibbonPanel panelStructuralMembers = application.CreateRibbonPanel(ribbonONBOX, Properties.RibbonLanguage.StructuralElements_Title);
            RibbonPanel panelModifyElem = application.CreateRibbonPanel(ribbonONBOX, Properties.RibbonLanguage.ModifyElements_Title);
            RibbonPanel panelTopo = application.CreateRibbonPanel(ribbonONBOX, Properties.RibbonLanguage.Topography_Title);
            RibbonPanel panelManage = application.CreateRibbonPanel(ribbonONBOX, Properties.RibbonLanguage.Manage_Title);
            RibbonPanel panelAbout = application.CreateRibbonPanel(ribbonONBOX, Properties.RibbonLanguage.MoreInfo_Title);
            RibbonPanel panelNotifications = null;

            allProjectPanels.Add(panelRenumber);
            allProjectPanels.Add(panelStructuralMembers);
            allProjectPanels.Add(panelModifyElem);
            allProjectPanels.Add(panelTopo);
            allProjectPanels.Add(panelManage);
            allFreePanels.Add(panelAbout);
            allFreePanels.Add(panelNotifications);

            PushButton btnRenumberGrids = panelRenumber.AddItem(new PushButtonData(Properties.RibbonLanguage.RenumberElements_Grids, Properties.RibbonLanguage.RenumberElements_Grids.Replace("\\n", "\n"), dll, "ONBOXAppl.RenumberGridsAdvanced")) as PushButton;
            btnRenumberGrids.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnRenumberGrids));
            SplitButton sptRenumberParking = panelRenumber.AddItem(new SplitButtonData(Properties.RibbonLanguage.RenumberElements_RenumberParkingSpaces, Properties.RibbonLanguage.RenumberElements_RenumberParkingSpaces)) as SplitButton;
            sptRenumberParking.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.sptRenumberParking));
            PushButton btnRenumberBeams = panelRenumber.AddItem(new PushButtonData(Properties.RibbonLanguage.RenumberElements_RenumberBeams, Properties.RibbonLanguage.RenumberElements_RenumberBeams.Replace("\\n", "\n"), dll, "ONBOXAppl.RenumberBeams")) as PushButton;
            btnRenumberBeams.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnRenumberBeams));
            SplitButton sptRenumberColumns = panelRenumber.AddItem(new SplitButtonData(Properties.RibbonLanguage.RenumberElements_RenumberColumns, Properties.RibbonLanguage.RenumberElements_RenumberColumns)) as SplitButton;
            sptRenumberColumns.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.sptRenumberColumns));
            SplitButton sptJoinElements = panelModifyElem.AddItem(new SplitButtonData(Properties.RibbonLanguage.ModifyElements_JoinMultipleElements, Properties.RibbonLanguage.ModifyElements_JoinMultipleElements)) as SplitButton;
            sptJoinElements.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.sptJoinElements));

            PushButton btnCreateBeamsFromBuilding = panelStructuralMembers.AddItem(new PushButtonData(Properties.RibbonLanguage.StructuralElements_BeamsForBuilding, Properties.RibbonLanguage.StructuralElements_BeamsForBuilding.Replace("\\n", "\n"), dll, "ONBOXAppl.BeamsFromEntireBuilding")) as PushButton;
            btnCreateBeamsFromBuilding.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnCreateBeamsFromBuilding));
            PushButton btnCreateBeam = panelStructuralMembers.AddItem(new PushButtonData(Properties.RibbonLanguage.StructuralElements_BeamsFromWalls, Properties.RibbonLanguage.StructuralElements_BeamsFromWalls.Replace("\\n","\n"), dll, "ONBOXAppl.BeamsFromWalls")) as PushButton;
            btnCreateBeam.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnCreateBeam));
            PushButton btnCreateBeamsFromColumns = panelStructuralMembers.AddItem(new PushButtonData(Properties.RibbonLanguage.StructuralElements_BeamsFromColumns, Properties.RibbonLanguage.StructuralElements_BeamsFromColumns.Replace("\\n", "\n"), dll, "ONBOXAppl.BeamsFromColumns")) as PushButton;
            btnCreateBeamsFromColumns.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnCreateBeamsFromColumns));
            PushButton btnCreateColumnsFromDwg = panelStructuralMembers.AddItem(new PushButtonData(Properties.RibbonLanguage.StructuralElements_ColumnsFromCAD, Properties.RibbonLanguage.StructuralElements_ColumnsFromCAD.Replace("\\n", "\n"), dll, "ONBOXAppl.ColumnsFromDwg")) as PushButton;
            btnCreateColumnsFromDwg.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnCreateColumnsFromDwg));

            //PushButton btnCreateBeamsUpdate = panelModifyElem.AddItem(new PushButtonData("  Atualizar \n vigas  ", "  Atualizar \n vigas  ", dll, "ONBOXAppl.BeamUpdate")) as PushButton;
            PushButton btnElementsCopyLevel = panelModifyElem.AddItem(new PushButtonData(Properties.RibbonLanguage.ModifyElements_CopyBeamsToLevels, Properties.RibbonLanguage.ModifyElements_CopyBeamsToLevels.Replace("\\n", "\n"), dll, "ONBOXAppl.ElementsCopyToLevels")) as PushButton;
            btnElementsCopyLevel.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnElementsCopyLevel));
            PushButton btnElmentsJoin = sptJoinElements.AddPushButton(new PushButtonData(Properties.RibbonLanguage.ModifyElements_JoinElements, Properties.RibbonLanguage.ModifyElements_JoinElements.Replace("\\n", "\n"), dll, "ONBOXAppl.ElementsJoinAdvanced")) as PushButton;
            btnElmentsJoin.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnElmentsJoin));
            PushButton btnElmentsSelectJoin = sptJoinElements.AddPushButton(new PushButtonData(Properties.RibbonLanguage.ModifyElements_SelectElementsToJoin, Properties.RibbonLanguage.ModifyElements_SelectElementsToJoin.Replace("\\n", "\n"), dll, "ONBOXAppl.ElementJoinSelect")) as PushButton;
            btnElmentsSelectJoin.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnElmentsSelectJoin));

            PushButton btnCreateTopoFromPointCloud = panelTopo.AddItem(new PushButtonData(Properties.RibbonLanguage.Topography_SurfaceByPointCloud, Properties.RibbonLanguage.Topography_SurfaceByPointCloud.Replace("\\n", "\n"), dll, "ONBOXAppl.TopoFromPointCloudAdvanced")) as PushButton;
            btnCreateTopoFromPointCloud.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnCreateTopoFromPointCloud));
            //PushButton btnCreateTopoFromDwgMarks = panelTopo.AddItem(new PushButtonData("  Topografia por CAD planimétrico  ", "  Topografia por \n  planimetria  ", dll, "ONBOXAppl.TopoFromDwgMarks")) as PushButton;
            PushButton btnTopoSlope = panelTopo.AddItem(new PushButtonData(Properties.RibbonLanguage.Topography_SlopeByPads, Properties.RibbonLanguage.Topography_SlopeByPads.Replace("\\n", "\n"), dll, "ONBOXAppl.TopoSlopes")) as PushButton;
            btnTopoSlope.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnTopoSlope));

            PushButton btnRevitZip = panelManage.AddItem(new PushButtonData(Properties.RibbonLanguage.Manage_PackageProject, Properties.RibbonLanguage.Manage_PackageProject.Replace("\\n", "\n"), dll, "ONBOXAppl.RevitZip")) as PushButton;
            btnRevitZip.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnRevitZip));

            PushButton btnONBOXSite = panelAbout.AddItem(new PushButtonData("  Onbox  ", "  Onbox  ", dll, "ONBOXAppl.SiteONBOX")) as PushButton;
            //PushButton btnProjectExamplesFolder = panelAbout.AddItem(new PushButtonData(Properties.RibbonLanguage.About_SampleProjects, Properties.RibbonLanguage.About_SampleProjects.Replace("\\n", "\n"), dll, "ONBOXAppl.ProjectFolder")) as PushButton;
            PushButton btnInfo = panelAbout.AddItem(new PushButtonData(Properties.RibbonLanguage.About_Title, Properties.RibbonLanguage.About_Title, dll, "ONBOXAppl.AboutONBOXApp")) as PushButton;

            //SplitButtons for Parking
            PushButton btnRenumberParking = sptRenumberParking.AddPushButton(new PushButtonData(Properties.RibbonLanguage.RenumberElements_ParkingSpaces, Properties.RibbonLanguage.RenumberElements_ParkingSpaces.Replace("\\n", "\n"), dll, "ONBOXAppl.RenumberParking")) as PushButton;
            btnRenumberParking.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnRenumberParking));
            PushButton btnRenumberBlockParking = sptRenumberParking.AddPushButton(new PushButtonData(Properties.RibbonLanguage.RenumberElements_NearestSpaces, Properties.RibbonLanguage.RenumberElements_NearestSpaces.Replace("\\n", "\n"), dll, "ONBOXAppl.RenumberBlockParking")) as PushButton;
            btnRenumberBlockParking.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnRenumberBlockParking));
            PushButton btnRenumberClearParking = sptRenumberParking.AddPushButton(new PushButtonData(Properties.RibbonLanguage.RenumberElements_ClearParkingNumbering, Properties.RibbonLanguage.RenumberElements_ClearParkingNumbering.Replace("\\n", "\n"), dll, "ONBOXAppl.RenumberCleaner")) as PushButton;
            btnRenumberClearParking.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnRenumberClearParking));
            //SplitButtons for Columns
            PushButton btnRenumberColumns = sptRenumberColumns.AddPushButton(new PushButtonData(Properties.RibbonLanguage.RenumberElements_Columns, Properties.RibbonLanguage.RenumberElements_Columns.Replace("\\n", "\n"), dll, "ONBOXAppl.RenumberColumns")) as PushButton;
            btnRenumberColumns.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnRenumberColumns));
            PushButton btnRenumberColumnsSelect = sptRenumberColumns.AddPushButton(new PushButtonData(Properties.RibbonLanguage.RenumberElements_SelectColumns, Properties.RibbonLanguage.RenumberElements_SelectColumns.Replace("\\n", "\n"), dll, "ONBOXAppl.RenumberColumnsSelection")) as PushButton;
            btnRenumberColumnsSelect.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Properties.HelpLinks.btnRenumberColumnsSelect));

            BitmapImage grid32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberGrid32.png", UriKind.Absolute));
            BitmapImage grid16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberGrid16.png", UriKind.Absolute));
            BitmapImage parking32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberParking32.png", UriKind.Absolute));
            BitmapImage parking16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberParking16.png", UriKind.Absolute));
            BitmapImage parkingDelete16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberDeleteParking16.png", UriKind.Absolute));
            BitmapImage parkingDelete32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberDeleteParking32.png", UriKind.Absolute));
            BitmapImage parkingBlock16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberBlockParking16.png", UriKind.Absolute));
            BitmapImage parkingBlock32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberBlockParking32.png", UriKind.Absolute));
            BitmapImage beams32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberBeams32.png", UriKind.Absolute));
            BitmapImage beams16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberBeams16.png", UriKind.Absolute));
            BitmapImage Column16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenumberColumns16.png", UriKind.Absolute));
            BitmapImage Column32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenameColumns32.png", UriKind.Absolute));
            BitmapImage Column16Select = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenameColumns16.png", UriKind.Absolute));
            BitmapImage Column32Select = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnRenameColumns32Select.png", UriKind.Absolute));
            BitmapImage BeamsFromBuilding16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnBeamsFromBuilding16.png", UriKind.Absolute));
            BitmapImage BeamsFromBuilding32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnBeamsFromBuilding32.png", UriKind.Absolute));
            BitmapImage createBeam16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnWallsToBeams16.png", UriKind.Absolute));
            BitmapImage createBeam32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnWallsToBeamsCreate32.png", UriKind.Absolute));
            BitmapImage createUpdate32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnWallsToBeamsUpdate32.png", UriKind.Absolute));
            BitmapImage createBeamsFromColumns16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnColumnsToBeams16.png", UriKind.Absolute));
            BitmapImage createBeamsFromColumns32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnColumnsToBeams32.png", UriKind.Absolute));
            BitmapImage ColumnsFromDwg16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnColumnsFromDWG16.png", UriKind.Absolute));
            BitmapImage ColumnsFromDwg32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnColumnsFromDWG32.png", UriKind.Absolute));
            BitmapImage ElementsCopy16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnBeamsCopyLevels16.png", UriKind.Absolute));
            BitmapImage ElementsCopy32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnBeamsCopyLevels.png", UriKind.Absolute));
            BitmapImage ElementsJoin16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnJoinMultiple16.png", UriKind.Absolute));
            BitmapImage ElementsJoin32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnJoinMultiple.png", UriKind.Absolute));
            BitmapImage ElementsSelectJoin16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnJoinSelectMultiple16.png", UriKind.Absolute));
            BitmapImage ElementsSelectJoin32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnJoinSelectMultiple.png", UriKind.Absolute));
            BitmapImage TopoFromPointCloud16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnTopoFromPointCloud16.png", UriKind.Absolute));
            BitmapImage TopoFromPointCloud32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnTopoFromPointCloud32.png", UriKind.Absolute));
            BitmapImage TopoFromDWG16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnTopoFromDWG16.png", UriKind.Absolute));
            BitmapImage TopoFromDWG32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnTopoFromDWG.png", UriKind.Absolute));
            BitmapImage TopoSlope16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnTopoSlope16.png", UriKind.Absolute));
            BitmapImage TopoSlope32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnTopoSlope.png", UriKind.Absolute));
            BitmapImage ONBOX32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/onBox32.png", UriKind.Absolute));
            BitmapImage ONBOX16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/onBox16.png", UriKind.Absolute));
            BitmapImage INFO32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnInfo32.png", UriKind.Absolute));
            BitmapImage INFO16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnInfo16.png", UriKind.Absolute));
            //BitmapImage ProjectExamples16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnProjectExamplesFolder16.png", UriKind.Absolute));
            //BitmapImage ProjectExamples32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnProjectExamplesFolder.png", UriKind.Absolute));
            BitmapImage Package16 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnPackage16.png", UriKind.Absolute));
            BitmapImage Package32 = new BitmapImage(new Uri("pack://application:,,,/ONBOXAppl;component/Resources/btnPackage32.png", UriKind.Absolute));

            btnRenumberGrids.LargeImage = grid32;
            btnRenumberGrids.Image = grid16;
            btnRenumberGrids.ToolTip = Properties.RibbonLanguage.RenumberElements_Grids_ToolTip;
            btnRenumberGrids.LongDescription = Properties.RibbonLanguage.RenumberElements_Grids_Descrip;

            //sptRenumberParking.LongDescription = "Comando para renumerar vagas de estacionamento automaticamente.";

            btnRenumberBeams.LargeImage = beams32;
            btnRenumberBeams.Image = beams16;
            btnRenumberBeams.ToolTip = Properties.RibbonLanguage.RenumberElements_RenumberBeams_ToolTip;
            btnRenumberBeams.LongDescription = Properties.RibbonLanguage.RenumberElements_RenumberBeams_Descrip; 

            btnRenumberColumns.LargeImage = Column32;
            btnRenumberColumns.Image = Column16;
            btnRenumberColumns.ToolTip = Properties.RibbonLanguage.RenumberElements_Columns_ToolTip;
            btnRenumberColumns.LongDescription =Properties.RibbonLanguage.RenumberElements_Columns_Descrip;

            btnRenumberColumnsSelect.LargeImage = Column32Select;
            btnRenumberColumnsSelect.Image = Column16Select;
            btnRenumberColumnsSelect.ToolTip = Properties.RibbonLanguage.RenumberElements_SelectColumns_ToolTip;
            btnRenumberColumnsSelect.LongDescription = Properties.RibbonLanguage.RenumberElements_SelectColumns_Descrip;

            btnCreateBeamsFromBuilding.Image = BeamsFromBuilding16;
            btnCreateBeamsFromBuilding.LargeImage = BeamsFromBuilding32;
            btnCreateBeamsFromBuilding.ToolTip = Properties.RibbonLanguage.StructuralElements_BeamsForBuilding_ToolTip;
            btnCreateBeamsFromBuilding.LongDescription = Properties.RibbonLanguage.StructuralElements_BeamsForBuilding_Descrip;

            btnCreateBeam.Image = createBeam16;
            btnCreateBeam.LargeImage = createBeam32;
            btnCreateBeam.ToolTip = Properties.RibbonLanguage.StructuralElements_BeamsFromWalls_ToolTip;
            btnCreateBeam.LongDescription = Properties.RibbonLanguage.StructuralElements_BeamsFromWalls_Descrip;

            //btnCreateBeamsUpdate.LargeImage = createUpdate32;
            //btnCreateBeamsUpdate.ToolTip = "Atualizar vigas estruturais";
            //btnCreateBeamsUpdate.LongDescription = "Comando para atualizar vigas estruturais, o comando cria tipos automaticamente, é possivel também pré-dimensionar a altura da viga dentre outras opções.";

            btnCreateColumnsFromDwg.Image = ColumnsFromDwg16;
            btnCreateColumnsFromDwg.LargeImage = ColumnsFromDwg32;
            btnCreateColumnsFromDwg.ToolTip = Properties.RibbonLanguage.StructuralElements_ColumnsFromCAD_ToolTip;
            btnCreateColumnsFromDwg.LongDescription = Properties.RibbonLanguage.StructuralElements_ColumnsFromCAD_Descrip;

            btnElementsCopyLevel.Image = ElementsCopy16;
            btnElementsCopyLevel.LargeImage = ElementsCopy32;
            btnElementsCopyLevel.ToolTip = Properties.RibbonLanguage.ModifyElements_CopyBeamsToLevels_ToolTip;
            btnElementsCopyLevel.LongDescription = Properties.RibbonLanguage.ModifyElements_CopyBeamsToLevels_Descrip;

            btnElmentsJoin.Image = ElementsJoin16;
            btnElmentsJoin.LargeImage = ElementsJoin32;
            btnElmentsJoin.ToolTip = Properties.RibbonLanguage.ModifyElements_JoinElements_ToolTip;
            btnElmentsJoin.LongDescription = Properties.RibbonLanguage.ModifyElements_JoinElements_Descrip;
            btnElmentsSelectJoin.Image = ElementsSelectJoin16;
            btnElmentsSelectJoin.LargeImage = ElementsSelectJoin32;
            btnElmentsSelectJoin.ToolTip = Properties.RibbonLanguage.ModifyElements_SelectElementsToJoin_ToolTip;
            btnElmentsSelectJoin.LongDescription = Properties.RibbonLanguage.ModifyElements_SelectElementsToJoin_Descrip;

            btnCreateTopoFromPointCloud.Image = TopoFromPointCloud16;
            btnCreateTopoFromPointCloud.LargeImage = TopoFromPointCloud32;
            btnCreateTopoFromPointCloud.ToolTip = Properties.RibbonLanguage.Topography_SurfaceByPointCloud_ToolTip;
            btnCreateTopoFromPointCloud.LongDescription = Properties.RibbonLanguage.Topography_SurfaceByPointCloud_Descrip;

            btnCreateBeamsFromColumns.ToolTip = Properties.RibbonLanguage.StructuralElements_BeamsFromColumns_ToolTip;
            btnCreateBeamsFromColumns.LongDescription = Properties.RibbonLanguage.StructuralElements_BeamsFromColumns_Descrip;
            btnCreateBeamsFromColumns.Image = createBeamsFromColumns16;
            btnCreateBeamsFromColumns.LargeImage = createBeamsFromColumns32;

            //btnCreateTopoFromDwgMarks.ToolTip = "Criar topografia a partir de arquivos de CAD planimétricos";
            //btnCreateTopoFromDwgMarks.LongDescription = "Comando para criar topografia a partir de arquivos CAD não altimétricos, ou seja, o arquivo de não contém cota Z, o comando lê o arquivo de texto mais próximo que contém informação de altura do ponto.";
            //btnCreateTopoFromDwgMarks.Image = TopoFromDWG16;
            //btnCreateTopoFromDwgMarks.LargeImage = TopoFromDWG32;

            btnTopoSlope.ToolTip = Properties.RibbonLanguage.Topography_SlopeByPads_ToolTip;
            btnTopoSlope.LongDescription = Properties.RibbonLanguage.Topography_SlopeByPads_Descrip;
            btnTopoSlope.Image = TopoSlope16;
            btnTopoSlope.LargeImage = TopoSlope32;

            btnONBOXSite.LargeImage = ONBOX32;
            btnONBOXSite.Image = ONBOX16;
            btnONBOXSite.ToolTip = Properties.RibbonLanguage.About_Site_ToolTip;
            btnONBOXSite.LongDescription = Properties.RibbonLanguage.About_Site_Descrip;

            btnInfo.LargeImage = INFO32;
            btnInfo.Image = INFO16;
            btnInfo.ToolTip = Properties.RibbonLanguage.About_Onbox_ToolTip;
            btnInfo.ToolTip = Properties.RibbonLanguage.About_Onbox_Descrip;

            //btnProjectExamplesFolder.Image = ProjectExamples16;
            //btnProjectExamplesFolder.LargeImage = ProjectExamples32;
            //btnProjectExamplesFolder.ToolTip = Properties.RibbonLanguage.About_SampleProjects_ToolTip;
            //btnProjectExamplesFolder.LongDescription = Properties.RibbonLanguage.About_SampleProjects_Descrip;

            btnRevitZip.Image = Package16;
            btnRevitZip.LargeImage = Package32;
            btnRevitZip.ToolTip = Properties.RibbonLanguage.Manage_PackageProject_ToolTip;
            btnRevitZip.LongDescription = Properties.RibbonLanguage.Manage_PackageProject_Descrip;

            //SplitButtons
            btnRenumberParking.LargeImage = parking32;
            btnRenumberParking.Image = parking16;
            btnRenumberParking.ToolTip = Properties.RibbonLanguage.RenumberElements_RenumberParkingSpaces_ToolTip;
            btnRenumberParking.LongDescription = Properties.RibbonLanguage.RenumberElements_RenumberParkingSpaces_Descrip;

            btnRenumberBlockParking.LargeImage = parkingBlock32;
            btnRenumberBlockParking.Image = parkingBlock16;
            btnRenumberBlockParking.ToolTip = Properties.RibbonLanguage.RenumberElements_NearestSpaces_ToolTip;
            btnRenumberBlockParking.LongDescription = Properties.RibbonLanguage.RenumberElements_NearestSpaces_Descrip;

            btnRenumberClearParking.LargeImage = parkingDelete32;
            btnRenumberClearParking.Image = parkingDelete16;
            btnRenumberClearParking.ToolTip = Properties.RibbonLanguage.RenumberElements_ClearParkingNumbering_ToolTip;
            btnRenumberClearParking.LongDescription = Properties.RibbonLanguage.RenumberElements_ClearParkingNumbering_Descrip;

            foreach (RibbonPanel currentRibbonPanel in allProjectPanels)
            {
                if (currentRibbonPanel != null)
                {
                    foreach (RibbonItem currentItem in currentRibbonPanel.GetItems())
                    {
                        if (currentItem is PushButton)
                        {
                            PushButton currentPushButton = currentItem as PushButton;
                            currentPushButton.AvailabilityClassName = "ONBOXAppl.ButtonAvailableOnProjectEnv";
                        }
                        if (currentItem is SplitButton)
                        {
                            SplitButton currentSplitButton = currentItem as SplitButton;
                            foreach (PushButton currentPushButton in currentSplitButton.GetItems())
                            {
                                currentPushButton.AvailabilityClassName = "ONBOXAppl.ButtonAvailableOnProjectEnv";
                            }
                        }
                    }
                }
            }
            
            foreach (RibbonPanel currentRibbonPanel in allFreePanels)
            {
                if (currentRibbonPanel != null)
                {
                    foreach (RibbonItem currentItem in currentRibbonPanel.GetItems())
                    {
                        if (currentItem is PushButton)
                        {
                            PushButton currentPushButton = currentItem as PushButton;
                            currentPushButton.AvailabilityClassName = "ONBOXAppl.ButtonAvailableAlways";
                        }
                        if (currentItem is SplitButton)
                        {
                            SplitButton currentSplitButton = currentItem as SplitButton;
                            foreach (PushButton currentPushButton in currentSplitButton.GetItems())
                            {
                                currentPushButton.AvailabilityClassName = "ONBOXAppl.ButtonAvailableAlways";
                            }
                        }
                    }
                }
            }
            
        }
    }

    class ButtonAvailableOnProjectEnv : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            if (applicationData.ActiveUIDocument == null)
            {
                return false;
            }
            else if (applicationData.ActiveUIDocument.Document == null)
            {
                return false;
            }
            else if (applicationData.ActiveUIDocument.Document.IsFamilyDocument)
                return false;
            else
                return true;
        }
    }
    class ButtonAvailableAlways : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return true;
        }
    }
}
