using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace ONBOXAppl
{
    public static class ExtensionMethods
    {
        public static double PlaneDistanceTo(this XYZ firstPoint, XYZ secondPoint)
        {
            XYZ p1 = new XYZ(firstPoint.X, firstPoint.Y, 0);
            XYZ p2 = new XYZ(secondPoint.X, secondPoint.Y, 0);

            return p1.DistanceTo(p2);
        }
        
        public static XYZ FlattenZ(this XYZ currentXYZ)
        {
            return new XYZ(currentXYZ.X, currentXYZ.Y, 0);
        }

        internal static bool IsAlmostEqualTo(this double firstNumber, double secondNumber, double tolerance = 0.01)
        {
            double diference = Math.Abs(firstNumber - secondNumber);

            if (diference <= tolerance)
            {
                return true;
            }

            return false;
        }
    }
}
