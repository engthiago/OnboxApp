using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.PointClouds;
using Autodesk.Revit.DB.Architecture;

namespace ONBOXAppl
{

    [Transaction(TransactionMode.Manual)]
    class CreateBoundingBoxReferencePlanes : IExternalCommand
    {

        double pointMinDistance = Utils.ConvertM.cmToFeet(3);
        double pointDistance = Utils.ConvertM.cmToFeet(0.1);

        int xNumberOfDivisions = 5;
        int yNumberOfDivisions = 5;


        static AddInId appId = new AddInId(new Guid("B31111F3-772B-4207-8C1A-891689513360"));

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            Selection sel = uidoc.Selection;

            //FamilySymbol origin = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(t => t.Name == "DebugPointRed").FirstOrDefault() as FamilySymbol;
            //FamilySymbol secondPoint = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(t => t.Name == "DebugPointYellow").FirstOrDefault() as FamilySymbol;
            //FamilySymbol cVector = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(t => t.Name == "DebugPointBlue").FirstOrDefault() as FamilySymbol;

            //ReferencePlane rf = doc.GetElement(sel.PickObject(ObjectType.Element)) as ReferencePlane;

            //using (Transaction t = new Transaction(doc, "Ponto"))
            //{
            //    t.Start();

            //    origin.Activate();
            //    doc.Create.NewFamilyInstance(rf.BubbleEnd, origin, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            //    secondPoint.Activate();
            //    doc.Create.NewFamilyInstance(rf.FreeEnd, secondPoint, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            //    cVector.Activate();
            //    doc.Create.NewFamilyInstance(rf., cVector, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            //    t.Commit();
            //}



            //try
            //{

            if (uidoc.ActiveView is View3D == false)
            {
                message = "Você deve estar em uma vista 3d para utilizar este comando. Por favor, entre em uma vista compatível e rode o comando novamente.";
                return Result.Failed;
            }

            PointCloudInstance pcInstance = doc.GetElement(sel.PickObject(ObjectType.Element, new PointCloundSelectionFilter(), "Por favor, selecione uma nuvem de pontos")) as PointCloudInstance;
            //Element pcInstance = doc.GetElement(sel.PickObject(ObjectType.Element));

            if (pcInstance == null)
            {
                message = "Objeto inválido selecionado. Este comando funciona somente com instancias de nuvem de pontos, por favor, rode o comando novamente e selecione uma nuvem de pontos.";
                return Result.Failed;
            }

            View3D this3dView = uidoc.ActiveView as View3D;

            BoundingBoxXYZ boundingBox = null;

            //Use CropBox if there is one to use
            if (this3dView.CropBoxActive == true)
            {
                boundingBox = this3dView.CropBox;
            }
            else
            {
                boundingBox = pcInstance.get_BoundingBox(uidoc.ActiveView);
            }

            boundingBox.Enabled = true;
            Line l1 = Line.CreateBound(boundingBox.Min, boundingBox.Max);

            double crossAngle = XYZ.BasisX.AngleOnPlaneTo(l1.Direction, XYZ.BasisZ);
            double hypotenuse = boundingBox.Min.PlaneDistanceTo(boundingBox.Max);

            double xDirDistance = hypotenuse * Math.Cos(crossAngle);
            double yDirDistance = hypotenuse * Math.Sin(crossAngle);

            double xIncrAmount = 1;
            double yIncrAmount = 1;

            EstabilishIteractionPoints(xDirDistance, ref xNumberOfDivisions, ref xIncrAmount);
            EstabilishIteractionPoints(yDirDistance, ref yNumberOfDivisions, ref yIncrAmount);


            IList<Plane> currentPlanes = new List<Plane>();

            using (Transaction t = new Transaction(doc, "Criar topografia"))
            {
                t.Start();
                CreateSetOfPlanes(doc, boundingBox.Min.FlattenZ(), xIncrAmount, yIncrAmount, XYZ.BasisY, XYZ.BasisX);
                CreateSetOfPlanes(doc, boundingBox.Min.FlattenZ(), yIncrAmount, xIncrAmount, XYZ.BasisX, XYZ.BasisY);
                t.Commit();
            }

            //}
            //catch (Exception excep)
            //{
            //    ExceptionManager eManager = new ExceptionManager(excep);
            //    return Result.Cancelled;
            //}

            return Result.Succeeded;
        }

        private void CreateSetOfPlanes(Document doc, XYZ startingPoint, double mainDirectionAmount , double secondDirectionAmount, XYZ mainDirection, XYZ secondDirection)
        {
            for (int i = 0; i <= xNumberOfDivisions; i++)
            {
                double xMult = (i * secondDirectionAmount);
                for (int j = 0; j < yNumberOfDivisions; j++)
                {
                    double yMult = (j * mainDirectionAmount);
                    XYZ firstPoint = startingPoint + secondDirection.Multiply(xMult) + mainDirection.Multiply(yMult);
                    XYZ secondPoint = firstPoint + mainDirection.Multiply(mainDirectionAmount);
                    XYZ thirdPoint = new XYZ(0, 0, 1);
                    doc.Create.NewReferencePlane(firstPoint, secondPoint, thirdPoint, doc.ActiveView);
                }
            }
        }

        private void EstabilishIteractionPoints(double targetDistance, ref int divisions, ref double increaseAmount)
        {
            if (targetDistance / divisions <= Utils.ConvertM.cmToFeet(100))
                divisions = (int)(Math.Ceiling(targetDistance / Utils.ConvertM.cmToFeet(100)));

            increaseAmount = targetDistance / divisions;
        }

        private void EstabilishIteractionPoints(double targetDistance, double maxPointDistInFeet, out int numberOfInteractions, out double increaseAmount)
        {
            if (maxPointDistInFeet > targetDistance)
                numberOfInteractions = 1;
            else
                numberOfInteractions = (int)(Math.Ceiling(targetDistance / maxPointDistInFeet));

            increaseAmount = 1.00 / numberOfInteractions;
        }

        private bool ListContainsPoint(IList<XYZ> targetListOfPoints, XYZ targetPoint)
        {
            foreach (XYZ currentPoint in targetListOfPoints)
            {
                if (currentPoint.IsAlmostEqualTo(targetPoint, pointMinDistance))
                    return true;
            }

            return false;
        }

    }
}
