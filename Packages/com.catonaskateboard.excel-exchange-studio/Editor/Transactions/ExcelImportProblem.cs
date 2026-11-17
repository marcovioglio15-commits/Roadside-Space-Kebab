using System;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Retains a correction's location while stopping an incomplete collection update.</summary>
    internal sealed class ExcelImportProblem : FormatException
    {
        #region Fields

        internal readonly ExcelPreviewRow Row;

        #endregion
        #region Methods

        /// <summary>Propagates a user-facing correction without losing its cell and field.</summary>
        /// <param name="row">Error details shown in the review.</param>
        internal ExcelImportProblem(ExcelPreviewRow row) : base(row.Message)
        {
            // Preserve the failed row across the collection-level error boundary.
            Row = row;
        }

        #endregion
    }
}
