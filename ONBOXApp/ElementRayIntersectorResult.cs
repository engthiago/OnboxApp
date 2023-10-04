#if R2024

using Autodesk.Revit.DB;

namespace ONBOXAppl
{
    public class ElementRayIntersectorResult
    {
        public bool Success { get; set; }
        public ReferenceWithContext Context { get; set; }
        public XYZ HitPoint { get; set; }
    }
}

#endif