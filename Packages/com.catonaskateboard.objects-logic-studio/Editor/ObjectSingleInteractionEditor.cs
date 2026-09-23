using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Routes all single-interaction configuration through the persistent workspace transaction.</summary>
    [CustomEditor(typeof(ObjectSingleInteraction), true)]
    internal sealed class ObjectSingleInteractionEditor : UnityEditor.Editor
    {
        #region Methods

        #region Inspector

        /// <summary>Shows the applied feature and opens its specific tool card.</summary>
        public override void OnInspectorGUI()
        {
            // The component inspector does not silently bypass retained drafts.
            ObjectSingleInteraction feature = (ObjectSingleInteraction)target;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("action"));
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode
                || !ObjectAuthoringSave.TryValidate(feature.gameObject, out _)))
                if (GUILayout.Button(new GUIContent("Open Objects Logic Studio", "Edit this feature's binding and settings with Apply and Discard.")))
                    ObjectsLogicStudioWindow.Open(feature);
            if (!feature.TryValidate(out string warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        #endregion

        #endregion
    }
}
