using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ONBOXAppl
{
    public class GridInfo
    {
        public int Id { get; set; }
        public string prevName { get; set; }
        public string newName { get; set; }
        public string orientation { get; set; }
    }

    class LevelInfo
    {
        public string levelName { get; set; }
        public int levelId { get; set; }
        public string levelPrefix { get; set; }
        public bool willBeNumbered { get; set; }
        public bool isStandardLevel { get; set; }
    }

    class ParkingTypesInfo
    {
        public string TypeName { get; set; }
        public int TypeId { get; set; }
        public bool willBeNumbered { get; set; }
        public double TypeWidth { get; set; }
        public string TypePrefix { get; set; }
    }

    class BeamTypesInfo
    {
        public string TypeName { get; set; }
        public int TypeId { get; set; }
        public bool WillBeNumbered { get; set; }
        public string TypePrefix { get; set; }
    }

    class ColumnTypesInfo
    {
        public string TypeName { get; set; }
        public int TypeId { get; set; }
        public bool WillBeNumbered { get; set; }
        public string TypePrefix { get; set; }
    }

    class TypeWithImage
    {
        public string TypeName { get; set; }
        public string FamilyName { get; set; }
        public System.Windows.Media.Imaging.BitmapSource Image { get; set; }
    }

    class FamilyWithImage
    {
        public string FamilyName { get; set; }
        public int FamilyID { get; set; }
        public System.Windows.Media.Imaging.BitmapSource Image { get; set; }
    }

    public class DwgLayerInfo
    {
        public string Name { get; set; }
        public System.Windows.Media.SolidColorBrush ColorBrush { get; set; }
    }

    class RevitLinksInfo
    {
        public string Name { get; set; }
        public int Id { get; set; }

        public RevitLinksInfo(RevitLinkInstance targetInstance)
        {
            Name = targetInstance.GetLinkDocument().Title;
            Id = targetInstance.Id.IntegerValue;
        }
    }

    partial class ONBOXApplication : IExternalApplication
    {

        #region renumberParking ONBOXAppl

        internal enum RenumberType { Ascending, Descending, FirstParking };

        //Prefix Window
        static internal IList<LevelInfo> storedParkingLevelInfo = new List<LevelInfo>();
        static internal bool isNumIndenLevel = false;
        static internal RenumberType parkingRenumType = RenumberType.Ascending;
        static internal Element currentFirstParking = null;

        //Types Window
        static internal IList<ParkingTypesInfo> storedParkingTypesInfo = new List<ParkingTypesInfo>();

        //Number by block 

        #endregion

        #region RenumberBeam ONBOXAppl

        static internal IList<BeamTypesInfo> storedBeamTypesInfo = new List<BeamTypesInfo>();
        static internal IList<LevelInfo> storedBeamLevelInfo = new List<LevelInfo>();

        static internal BeamRenumberOrder storedBeamRenumOrder = BeamRenumberOrder.Horizontal;
        static internal bool isNumBeamLevel = true;

        static internal int BeamsDecimalPlaces = 2;

        #endregion

        #region RenumberColumns ONBOXAppl

        static internal IList<ColumnTypesInfo> storedColumnTypesInfo = new List<ColumnTypesInfo>();
        static internal IList<LevelInfo> storedColumnLevelInfo = new List<LevelInfo>();

        static internal ColumnRenumberOrder storedColumnRenumOrder = ColumnRenumberOrder.Ascending;

        static internal string columnsLevelIndicator = "Lance";
        static internal string columnsConcatWord = "a";

        #endregion

        #region ColumnsFromDwg ONBOXAppl

        static internal IList<LevelInfo> StoredColumnsDwgLevels = new List<LevelInfo>();
        static internal IList<FamilyWithImage> storedColumnFamiliesInfo = new List<FamilyWithImage>();
        static internal IList<FamilyWithImage> storedColumnFamiliesCircInfo = new List<FamilyWithImage>();
        static internal int selectedColumnFamily= 0;
        static internal int selectedColumnCircFamily = 0;

        #endregion

        #region BeamsFromColumns ONBOXAppl

        static internal IList<FamilyWithImage> storedBeamFamilesInfo = new List<FamilyWithImage>();

        #endregion
    }
}
