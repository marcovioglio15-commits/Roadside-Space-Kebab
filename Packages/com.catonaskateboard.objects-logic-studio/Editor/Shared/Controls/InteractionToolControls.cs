using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws the same conditional tool restriction on every interaction card and inspector.</summary>
    [CustomPropertyDrawer(typeof(InteractionToolRequirement))]
    internal sealed class InteractionToolControls : PropertyDrawer
    {
        #region Methods

        #region Controls

        /// <summary>Shows only the identities used by the selected requirement mode.</summary>
        /// <param name="property">Serialized tool requirement.</param>
        internal static void Draw(SerializedProperty property)
        {
            // Field labels inherit the runtime configuration tooltips.
            StudioGUI.PropertyField(property.FindPropertyRelative("Mode"), new UnityEngine.GUIContent("Player Tool", property.FindPropertyRelative("Mode").tooltip));
            SerializedProperty selected = Selected(property);
            if (selected != null)
                StudioGUI.PropertyField(selected, true);
        }

        /// <summary>Draws native inspector properties with the same mode-dependent visibility.</summary>
        /// <param name="position">Rectangle allocated by Unity.</param>
        /// <param name="property">Serialized requirement.</param>
        /// <param name="label">Label supplied by the parent inspector.</param>
        public override void OnGUI(UnityEngine.Rect position, SerializedProperty property, UnityEngine.GUIContent label)
        {
            // Use rect-based fields so nested arrays remain compatible with native inspectors.
            EditorGUI.BeginProperty(position, label, property);
            position.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("Mode"), label);
            SerializedProperty selected = Selected(property);
            if (selected != null)
            {
                position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                position.height = EditorGUI.GetPropertyHeight(selected, true);
                EditorGUI.PropertyField(position, selected, true);
            }
            EditorGUI.EndProperty();
        }

        /// <summary>Reserves height only for settings visible in the selected mode.</summary>
        /// <param name="property">Serialized requirement.</param>
        /// <param name="label">Parent field label.</param>
        /// <returns>Required inspector height.</returns>
        public override float GetPropertyHeight(SerializedProperty property, UnityEngine.GUIContent label)
        {
            // Array expansion contributes its native height.
            SerializedProperty selected = Selected(property);
            return EditorGUIUtility.singleLineHeight + (selected != null
                ? EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(selected, true) : 0f);
        }

        /// <summary>Resolves the optional mode-specific identity field.</summary>
        /// <param name="property">Owning requirement.</param>
        /// <returns>One tool field, a selection array, or null.</returns>
        private static SerializedProperty Selected(SerializedProperty property)
        {
            // Unrestricted and unarmed modes require no asset picker.
            return (InteractionToolMode)property.FindPropertyRelative("Mode").enumValueIndex switch
            {
                InteractionToolMode.Specific => property.FindPropertyRelative("Tool"),
                InteractionToolMode.Selection => property.FindPropertyRelative("Tools"),
                _ => null
            };
        }

        #endregion

        #endregion
    }
}
