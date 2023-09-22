using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ONBOXAppl
{
    [Transaction(TransactionMode.Manual)]
    class TopoEditCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TopoEdit.Canvas canvas = new TopoEdit.Canvas();
            canvas.ShowDialog();

            return Result.Succeeded;
        }
    }
}
