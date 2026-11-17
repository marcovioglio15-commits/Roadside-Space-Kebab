using System;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Selects which operation consumes a cell mapping.</summary>
    public enum ExcelDirection { Both, Import, Export }
    /// <summary>Distinguishes data, row identity and workbook-only cells.</summary>
    public enum ExcelMappingKind { Data = 0, ParentKey = 1, Literal = 3, Formula = 4 }
    /// <summary>Selects how spreadsheet rows identify existing elements.</summary>
    public enum ExcelMatchMode { Position, Key }
    /// <summary>Selects whether an import preserves, adds or rebuilds collection elements.</summary>
    public enum ExcelCollectionPolicy { UpdateExisting, Merge, ReplaceCollection }
    /// <summary>Selects how repeated spreadsheet rows end.</summary>
    public enum ExcelRowExtent { FirstBlankKey, LastRow, ExcelTable }

    /// <summary>Links reusable spreadsheet mappings to one exact project preset.</summary>
    public sealed class ExcelExchangePreset : ScriptableObject
    {
        #region Fields

        [Header("Target")]
        [Tooltip("Preset updated by this setup.")]
        public ScriptableObject Target;
        [Tooltip("Preset section used by this setup.")]
        public string Scope = string.Empty;
        [Header("Workbook")]
        [Tooltip("Workbook supplying imports and available worksheet tabs.")]
        public string WorkbookPath = string.Empty;
        [Tooltip("Worksheet tab selected from the import workbook. All mapped coordinates use this worksheet.")]
        public string Worksheet = "Sheet1";
        [Tooltip("Optional starting file for export. Other cells keep their contents and formatting.")]
        public string TemplatePath = string.Empty;
        [Tooltip("Limit each exported column to this multiple of its shortest non-empty text. Blank cells are ignored.")]
        [Range(1f, 20f)]
        public float ColumnWidthRatio = 5f;
        [Tooltip("Use formula results saved by Excel. Recalculate and save the file first.")]
        public bool UseFormulaResults;
        [Header("Layout")]
        [Tooltip("Rows displayed in the mapping grid. Does not limit imported items.")]
        [Range(1, 200)]
        public int Rows = 12;
        [Tooltip("Columns displayed in the mapping grid.")]
        [Range(1, 30)]
        public int Columns = 10;
        [Tooltip("Place each field in its first cell. Lists continue down the column.")]
        public ExcelCellMapping[] Mappings = Array.Empty<ExcelCellMapping>();
        [Tooltip("How spreadsheet rows update or replace each list.")]
        public ExcelCollectionGroup[] Groups = Array.Empty<ExcelCollectionGroup>();

        #endregion
    }

    /// <summary>Maps one field or workbook value to an anchor cell.</summary>
    [Serializable]
    public sealed class ExcelCellMapping
    {
        #region Fields

        [Header("Cell")]
        [Tooltip("First cell for this field, such as C2. List values continue below it.")]
        public string Cell = "A2";
        [Tooltip("Preset field, parent name, export text or formula.")]
        public ExcelMappingKind Kind;
        [Tooltip("Field selected from the preset.")]
        public string Path = string.Empty;
        [Tooltip("List filled by this column. Leave empty for one value.")]
        public string Group = string.Empty;
        [Tooltip("Item positions used when reading or writing just one cell.")]
        public int[] Indices = Array.Empty<int>();
        [Tooltip("Use this cell for import, export or both.")]
        public ExcelDirection Direction;
        [Tooltip("Text or formula written when exporting.")]
        public string Text = string.Empty;
        [Tooltip("Leave the current value unchanged when an imported cell is empty.")]
        public bool KeepEmpty;
        [Tooltip("Character between list entries, usually ;. Put quotes around entries containing that character.")]
        public string Separator = ";";

        #endregion
    }

    /// <summary>Shares row identity and collection policies across related columns.</summary>
    [Serializable]
    public sealed class ExcelCollectionGroup
    {
        #region Fields

        [Header("Collection")]
        [Tooltip("Name shared by the columns for this list.")]
        public string Name = "Rows";
        [Tooltip("Preset list updated by these columns.")]
        public string Path = string.Empty;
        [Tooltip("Match rows by their order or by a field such as Name.")]
        public ExcelMatchMode Match;
        [Tooltip("Field identifying each item, usually Name.")]
        public string KeyPath = "Name";
        [Tooltip("Update existing items, add new ones or replace the list.")]
        public ExcelCollectionPolicy Policy;
        [Tooltip("First cell of the column used to find filled rows. Empty uses the Name column or first mapped field.")]
        public string GuideCell = string.Empty;
        [Tooltip("Stop at the first blank item, a chosen last row or the end of an Excel table.")]
        public ExcelRowExtent Extent;
        [Tooltip("Last row to import when Read Until is set to Last Row.")]
        [Range(2, 1000)]
        public int LastRow = 100;
        [Tooltip("Excel table whose rows will be imported.")]
        public string TableName = string.Empty;

        #endregion
    }
}
