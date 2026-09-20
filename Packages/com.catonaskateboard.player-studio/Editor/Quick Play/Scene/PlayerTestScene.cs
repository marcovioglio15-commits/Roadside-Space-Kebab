using System.IO;
using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Installs the editable test scene without opening or changing the user's loaded scenes.</summary>
    internal static class PlayerTestScene
    {
        #region Paths

        internal const string Folder = PlayerDefaultAssets.ContentRoot + "/Testing";
        internal const string Path = Folder + "/Player Test.unity";
        private const string template = "Packages/com.catonaskateboard.player-studio/Runtime/Defaults/Testing/Player Test.unity";

        #endregion

        #region Methods

        /// <summary>Copies the bundled obstacle course once; later user changes are preserved.</summary>
        /// <returns>The editable scene with its spawn marker, ramps, stairs and obstacles.</returns>
        internal static SceneAsset Ensure()
        {
            // Copying an asset also works while the current scene has never been saved.
            SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(Path);
            if (existing != null)
                return existing;
            if (File.Exists(Path))
                throw new IOException("Cannot load the existing Player Test scene.");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(PlayerDefaultAssets.ContentRoot, "Testing");
            if (!AssetDatabase.CopyAsset(template, Path))
                throw new IOException("Unity could not install the bundled Player Test scene.");
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(Path);
        }

        #endregion
    }
}