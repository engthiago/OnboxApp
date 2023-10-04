#if R2024

using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace ONBOXAppl
{
    public class StatiTypeSelectionFilter
    {
        static public bool AllowElement<T>(T elem) where T: Element
        {
            if (elem is T) return true;
            return false;
        }

        static public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    public class TypeSelectionFilter<T> : ISelectionFilter where T : Element
    {
        public bool AllowElement(Element elem)
        {
            if (elem is T) return true;
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}

#endif