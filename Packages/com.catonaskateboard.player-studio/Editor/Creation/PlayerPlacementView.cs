using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Switches scene presentation only after the placement option explicitly requests the player view.</summary>
    internal static class PlayerPlacementView
    {
        #region Methods

        /// <summary>Disables competing scene cameras and listeners in the same creation Undo group.</summary>
        /// <param name="player">New player whose view remains active.</param>
        /// <param name="scene">Only scene affected by this explicit placement option.</param>
        internal static void Activate(GameObject player, Scene scene)
        {
            // Other loaded scenes and cameras inside the new player remain untouched.
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Behaviour component in root.GetComponentsInChildren<Behaviour>(true))
                    if (component.enabled && (component is Camera || component is AudioListener)
                        && !component.transform.IsChildOf(player.transform))
                    {
                        Undo.RecordObject(component, "Use Player View");
                        component.enabled = false;
                        if (PrefabUtility.IsPartOfPrefabInstance(component))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                    }
        }

        #endregion
    }
}
