using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Routes component editing to the persistent workspace and displays the applied binding.</summary>
    [CustomEditor(typeof(ObjectHover))]
    internal sealed class ObjectHoverEditor : UnityEditor.Editor
    {
        #region Methods

        #region Inspector

        /// <summary>Shows the applied preset and opens its component-specific draft session.</summary>
        public override void OnInspectorGUI()
        {
            // The component inspector does not bypass the tool's Apply/Discard contract.
            ObjectHover hover = (ObjectHover)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(new GUIContent("Preset", "Applied reusable hover configuration."), hover.Preset, typeof(HoverPreset), false);
                EditorGUILayout.ObjectField(new GUIContent("Label", "Preauthored UI owned by this interaction."), hover.Label, typeof(HoverLabel), true);
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(new GUIContent("Open Objects Logic Studio", "Edit this interaction in a persistent Apply/Discard session.")))
                    ObjectsLogicStudioWindow.Open(hover);
            if (!hover.TryValidate(out string warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        #endregion

        #endregion
    }
}
