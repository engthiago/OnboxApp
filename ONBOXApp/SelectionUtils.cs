﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI;
using System;

namespace ONBOXAppl
{
    public static class SelectionUtils
    {
        static public Result PickOrGetSelectedElement<T, S>(UIDocument uidoc, S filter, string pickingPrompt, out string message, out T element) where T : Element where S : ISelectionFilter
        {
            var doc = uidoc.Document;
            var selection = uidoc.Selection.GetElementIds();
            element = null;
            message = null;
            if (selection.Count > 0)
            {
                foreach (var selId in selection)
                {
                    element = doc.GetElement(selId) as T;
                    if (element != null)
                    {
                        break;
                    }
                }
            }

            try
            {
                if (element == null)
                {
                    var reference = uidoc.Selection.PickObject(ObjectType.Element, filter, pickingPrompt);
                    element = doc.GetElement(reference) as T;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            if (element == null)
            {
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        static public (Result result, Reference reference, Element element) PickFace<S>(UIDocument uidoc, S filter, string pickingPrompt, out string message) where S : ISelectionFilter
        {
            var doc = uidoc.Document;
            var selection = uidoc.Selection.GetElementIds();
            message = null;
            Element element;

            Reference reference;
            try
            {
                reference = uidoc.Selection.PickObject(ObjectType.Face, filter, pickingPrompt);
                element = doc.GetElement(reference);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return (Result.Cancelled, null, null);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return (Result.Failed, null, null);
            }

            if (element == null)
            {
                return (Result.Failed, null, null);
            }

            return (Result.Succeeded, reference, element);
        }

    }
}
