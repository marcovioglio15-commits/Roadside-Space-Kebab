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
        [Tooltip("Exact preset edited by these mappings. Other assets of the same type use separate configurations.")]
        public ScriptableObject Target;
        [Tooltip("Logical settings group within the linked preset.")]
        public string Scope = string.Empty;
        [Header("Workbook")]
        [Tooltip("Workbook supplying imports and available worksheet tabs.")]
        public string WorkbookPath = string.Empty;
        [Tooltip("Worksheet tab selected from the import workbook. All mapped coordinates use this worksheet.")]
        public string Worksheet = "Sheet1";
        [Tooltip("Optional workbook copied before export. Unmapped cells and formatting are preserved.")]
        public string TemplatePath = string.Empty;
        [Tooltip("Read stored Excel formula results. Missing results and Excel errors still block import.")]
        public bool UseFormulaResults;
        [Header("Layout")]
        [Tooltip("Rows displayed in the mapping grid. Does not limit imported items.")]
        [Range(1, 200)]
        public int Rows = 12;
        [Tooltip("Columns displayed in the mapping grid.")]
        [Range(1, 30)]
        public int Columns = 10;
        [Tooltip("One mapping per anchor cell. Collection mappings continue vertically from their anchor.")]
        public ExcelCellMapping[] Mappings = Array.Empty<ExcelCellMapping>();
        [Tooltip("Shared row matching and replacement rules for each repeated collection.")]
        public ExcelCollectionGroup[] Groups = Array.Empty<ExcelCollectionGroup>();

        #endregion
    }

    /// <summary>Maps one field or workbook value to an anchor cell.</summary>
    [Serializable]
    public sealed class ExcelCellMapping
    {
        #region Fields

        [Header("Cell")]
        [Tooltip("Anchor address, such as C2. Repeated fields continue down this column.")]
        public string Cell = "A2";
        [Tooltip("Field value, parent lookup, literal text or export formula.")]
        public ExcelMappingKind Kind;
        [Tooltip("Logical field path. Empty array tokens represent repeated elements.")]
        public string Path = string.Empty;
        [Tooltip("Repeated collection group. Empty maps a single value.")]
        public string Group = string.Empty;
        [Tooltip("Explicit element positions when a field inside a collection maps to a single cell.")]
        public int[] Indices = Array.Empty<int>();
        [Tooltip("Operations allowed to use this mapping.")]
        public ExcelDirection Direction;
        [Tooltip("Workbook text or formula for a Literal or Formula mapping.")]
        public string Text = string.Empty;
        [Tooltip("Leave the current value unchanged when an imported cell is empty.")]
        public bool KeepEmpty;
        [Tooltip("Separator for an array of values stored in one cell. Quotes preserve embedded separators.")]
        public string Separator = ";";

        #endregion
    }

    /// <summary>Shares row identity and collection policies across related columns.</summary>
    [Serializable]
    public sealed class ExcelCollectionGroup
    {
        #region Fields

        [Header("Collection")]
        [Tooltip("Unique group label used by its column mappings.")]
        public string Name = "Rows";
        [Tooltip("Collection replaced or updated by this group, such as Entries[] or Entries[].Variants[].")]
        public string Path = string.Empty;
        [Tooltip("Match by current element position or by an explicit unique field.")]
        public ExcelMatchMode Match;
        [Tooltip("Element field identifying a row when matching by key, for example Name.")]
        public string KeyPath = "Name";
        [Tooltip("Keep existing elements, add missing ones, or rebuild the selected collection from declared defaults.")]
        public ExcelCollectionPolicy Policy;
        [Tooltip("Anchor of the column defining row presence. Empty uses the first data column.")]
        public string GuideCell = string.Empty;
        [Tooltip("Stop at the first empty row key, use a final row, or use an Excel table boundary.")]
        public ExcelRowExtent Extent;
        [Tooltip("Last included worksheet row when using a fixed boundary.")]
        [Range(2, 1000)]
        public int LastRow = 100;
        [Tooltip("Exact Excel table name when using a table boundary.")]
        public string TableName = string.Empty;

        #endregion
    }
}
