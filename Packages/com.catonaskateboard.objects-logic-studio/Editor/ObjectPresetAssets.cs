using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Creates explicit reusable hover assets from type defaults or saved source values.</summary>
    internal static class ObjectPresetAssets
    {
        #region Methods

        #region Creation

        /// <summary>Saves a new preset at a unique path without mutating its optional source.</summary>
        /// <param name="path">Requested project asset path.</param>
        /// <param name="source">Saved configuration to duplicate, or null for defaults.</param>
        /// <returns>The new persistent preset.</returns>
        internal static HoverPreset Create(string path, HoverPreset source)
        {
            // Native serialization retains font and sprite references while configuration objects are copied.
            HoverPreset created = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<HoverPreset>();
            created.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssetIfDirty(created);
            return created;
        }

        #endregion

        #endregion
    }
}
