#if R2024

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ONBOXAppl
{
    public class ElementRayIntersector
    {
        private readonly ReferenceIntersector refIntersector;
        private readonly double rayHeight = 100_000;
        private readonly XYZ direction = XYZ.BasisZ.Negate();

        public ElementRayIntersector(List<ElementId> againstElements, View3D view3d)
        {
            this.refIntersector = new ReferenceIntersector(againstElements, FindReferenceTarget.Element, view3d);
        }

        public ElementRayIntersectorResult Shoot(XYZ point)
        {
            var result = new ElementRayIntersectorResult();
            var refPoint = new XYZ(point.X, point.Y, this.rayHeight);
            var refResult = refIntersector.FindNearest(refPoint, this.direction);

            if (refResult != null)
            {
                result.Success = true;
                result.Context = refResult;
                result.HitPoint = refPoint.Add(direction.Multiply(refResult.Proximity));
            }

            return result;
        }
    }
}

#endif