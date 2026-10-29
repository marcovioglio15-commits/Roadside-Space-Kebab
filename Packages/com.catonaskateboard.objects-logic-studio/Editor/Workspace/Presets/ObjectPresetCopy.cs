using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Creates a separate reusable branch while keeping its other preset references shared.</summary>
    internal static class ObjectPresetCopy
    {
        #region Methods

        #region Assets

        /// <summary>Saves a copy selected through the preset inspector without changing the original asset.</summary>
        /// <typeparam name="T">Reusable preset type.</typeparam>
        /// <param name="source">Preset to copy.</param>
        /// <returns>The separate asset, or null when the file picker is cancelled.</returns>
        internal static T Save<T>(T source) where T : ScriptableObject
        {
            string path = EditorUtility.SaveFilePanelInProject("Copy preset", source.name + " Copy", "asset", "Save a separate preset for this reference.");
            if (string.IsNullOrEmpty(path))
                return null;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            T copy = Object.Instantiate(source);
            copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, path);
            if (copy is ObjectGroupPreset group && group.Context != null)
            {
                group.Context = Object.Instantiate(group.Context);
                AssetDatabase.AddObjectToAsset(group.Context, copy);
            }
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            return copy;
        }

        #endregion

        #endregion
    }
}
