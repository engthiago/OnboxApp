#if R2024

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ONBOXAppl
{
    public class FindBoundsResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public CurveLoop OuterBounds { get; set; }
        public List<CurveLoop> InnerBounds { get; set; }
    }
}

#endif