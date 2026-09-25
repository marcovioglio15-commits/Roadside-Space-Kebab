using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors the small prefab dependency used to prepare products before their first activation.</summary>
    internal static class AssemblyAuthoring
    {
        #region Methods

        #region Authoring

        /// <summary>Creates or reconnects an inactive staging child through the existing prefab Undo transaction.</summary>
        /// <param name="station">Editable table component.</param>
        internal static void Prepare(ObjectAssemblyStation station)
        {
            // Shared staging authoring preserves native Undo and saves through the caller's prefab transaction.
            InteractionStagingAuthoring.Prepare(station, "Assembly Staging");
        }

        #endregion

        #endregion
    }
}
