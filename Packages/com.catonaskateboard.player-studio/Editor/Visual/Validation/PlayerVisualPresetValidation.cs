using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Checks visual asset references without adding Editor dependencies to runtime settings.</summary>
    internal static class PlayerVisualPresetValidation
    {
        #region Methods

        #region Validation

        /// <summary>Checks a Visual preset's applied data and optional source asset before assigning it to a master.</summary>
        /// <param name="serialized">Current or proposed serialized values of one PlayerVisualPreset.</param>
        /// <param name="warning">Receives an invalid offset, missing reference or non-root prefab warning.</param>
        /// <returns>True for a valid offset with an empty source or the root of a prefab asset.</returns>
        public static bool TryValidate(SerializedObject serialized, out string warning)
        {
            // Read candidate properties too, so pending edits use exactly the same validation.
            return PlayerVisualDraft.Read(serialized).TryGetSettings(out _, out warning);
        }

        /// <summary>Checks source identity separately from the shared numeric rules.</summary>
        /// <param name="prefab">Proposed asset root, or null for an existing scene model.</param>
        /// <param name="isMissing">Whether a lost reference needs explicit replacement or clearing.</param>
        /// <param name="warning">Receives a missing or incompatible source warning.</param>
        /// <returns>True for a deliberate empty source or a persistent prefab root.</returns>
        public static bool TryValidateSource(GameObject prefab, bool isMissing, out string warning)
        {
            // Never convert a broken reference into an implicit choice of an existing child.
            warning = string.Empty;
            if (isMissing)
            {
                warning = "The Visual prefab reference is missing. Assign a prefab or clear the missing reference explicitly.";
                return false;
            }
            if (prefab == null)
                return true;
            // Accept an entire prefab asset, not a scene instance, preview object or child inside an asset.
            if (!EditorUtility.IsPersistent(prefab) || !PrefabUtility.IsPartOfPrefabAsset(prefab)
                || AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(prefab)) != prefab)
            {
                warning = "Choose the root of a prefab asset from the Project window for the Visual source.";
                return false;
            }

            return true;
        }

        #endregion

        #endregion
    }
}
