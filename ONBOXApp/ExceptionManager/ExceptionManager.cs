using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ONBOXAppl
{
    internal class ExceptionManager
    {
        internal ExceptionManager(Exception targetException)
        {
            if ((targetException is Autodesk.Revit.Exceptions.OperationCanceledException) == false)
            {
                ExceptionManagerUI exceptUI = new ExceptionManagerUI(targetException);
                exceptUI.ShowDialog();
            }
        }

        public ExceptionManager(Exception targetException, string customInfo, bool isJustWarning)
        {
            if ((targetException is Autodesk.Revit.Exceptions.OperationCanceledException) == false)
            {
                ExceptionManagerUI exceptUI = new ExceptionManagerUI(targetException, customInfo, isJustWarning);
                exceptUI.ShowDialog();
            }
        }
    }
}