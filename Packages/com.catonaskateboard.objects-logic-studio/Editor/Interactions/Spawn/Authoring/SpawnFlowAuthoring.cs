using CatOnASkateboard.MenuStudio;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors a persistent transition controller without adding UI to spawned managers.</summary>
    internal static class SpawnFlowAuthoring
    {
        #region Methods
        #region Authoring

        /// <summary>Adds a missing controller; shared scene UI is configured through Scene Observer.</summary>
        /// <param name="manager">Day-flow component receiving its persistent transition controller.</param>
        internal static void Prepare(ObjectSpawnManager manager)
        {
            // Persistent flow owns only its controller; the scene owns every UI element.
            using SerializedObject data = new SerializedObject(manager);
            SerializedProperty reference = data.FindProperty("transition");
            if (reference.objectReferenceValue is MenuSceneTransition)
                return;
            MenuSceneTransition transition = manager.GetComponent<MenuSceneTransition>();
            if (transition == null)
                transition = Undo.AddComponent<MenuSceneTransition>(manager.gameObject);
            reference.objectReferenceValue = transition;
            data.ApplyModifiedProperties();
        }

        #endregion
        #endregion
    }
}
