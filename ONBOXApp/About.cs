using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Xml;

namespace ONBOXAppl
{
    [Transaction(TransactionMode.Manual)]
    class SiteONBOX : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            System.Diagnostics.Process.Start("http://www.onboxdesign.com.br/");
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class AboutONBOXApp : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            AboutUI aboutWindow = new AboutUI();
            aboutWindow.ShowDialog();
            return Result.Succeeded;
        }
    }
    [Transaction(TransactionMode.Manual)]
    class ProjectFolder : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string version = commandData.Application.Application.VersionNumber;
            if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\Autodesk\\Revit\\Addins\\" + version + "\\ONBOX\\Project Examples"))
            {
                System.Diagnostics.Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\Autodesk\\Revit\\Addins\\" + version + "\\ONBOX\\Project Examples");
            }
            else
            {
                TaskDialog.Show(Properties.Messages.Common_Error, Properties.Messages.SampleProjects_DirNotFound);
            }
            return Result.Succeeded;
        }
    }
}
