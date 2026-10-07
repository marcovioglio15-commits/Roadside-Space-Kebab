using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws the same conditional tool restriction on every interaction card and inspector.</summary>
    [CustomPropertyDrawer(typeof(InteractionToolRequirement))]
    [InitializeOnLoad]
    internal sealed class InteractionToolControls : PropertyDrawer
    {
        #region Methods

        #region Controls

        /// <summary>Retains mode-dependent controls inside generic Studio structures.</summary>
        static InteractionToolControls()
        {
            StudioStructureGUI.Register<InteractionToolRequirement>(Draw);
        }

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
            DrawField(position, property.FindPropertyRelative("Mode"), label);
            SerializedProperty selected = Selected(property);
            if (selected != null)
            {
                position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                if (selected.isArray)
                    DrawTools(position, selected);
                else
                    DrawField(position, selected, new GUIContent(selected.displayName, selected.tooltip));
            }
            EditorGUI.EndProperty();
        }

        /// <summary>Reserves height only for settings visible in the selected mode.</summary>
        /// <param name="property">Serialized requirement.</param>
        /// <param name="label">Parent field label.</param>
        /// <returns>Required inspector height.</returns>
        public override float GetPropertyHeight(SerializedProperty property, UnityEngine.GUIContent label)
        {
            // Every array row owns a remove command; no size field can truncate the selection.
            SerializedProperty selected = Selected(property);
            return EditorGUIUtility.singleLineHeight + (selected != null
                ? (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * (selected.isArray ? selected.arraySize + 1 : 1) : 0f);
        }

        /// <summary>Draws a native field with the same transfer menu and color as window controls.</summary>
        /// <param name="position">One field row.</param>
        /// <param name="property">Mode or tool reference.</param>
        /// <param name="label">Field label and tooltip.</param>
        private static void DrawField(Rect position, SerializedProperty property, GUIContent label)
        {
            StudioFieldMenu.Context(position, property);
            using StudioFieldColors colors = new StudioFieldColors(position, StudioFieldColors.Key(property));
            EditorGUI.PropertyField(position, property, label);
        }

        /// <summary>Edits a native inspector selection one reference at a time.</summary>
        /// <param name="position">First row of the selection.</param>
        /// <param name="tools">Serialized array of tool definitions.</param>
        private static void DrawTools(Rect position, SerializedProperty tools)
        {
            // Keep stable row heights when an individual selection is removed.
            for (int index = 0; index < tools.arraySize; index++)
            {
                Rect remove = new Rect(position.xMax - 24f, position.y, 24f, position.height);
                DrawField(new Rect(position.x, position.y, position.width - 28f, position.height), tools.GetArrayElementAtIndex(index),
                    new GUIContent("Tool " + (index + 1), tools.tooltip));
                if (GUI.Button(remove, new GUIContent("-", "Remove this tool from the selection.")))
                {
                    tools.GetArrayElementAtIndex(index).objectReferenceValue = null;
                    tools.DeleteArrayElementAtIndex(index);
                    return;
                }
                position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
            }
            StudioFieldMenu.Context(position, tools);
            using StudioFieldColors colors = new StudioFieldColors(position, StudioFieldColors.Key(tools));
            if (GUI.Button(position, new GUIContent("+ Add Tool", "Append one empty tool reference without changing existing selections.")))
            {
                tools.arraySize++;
                tools.GetArrayElementAtIndex(tools.arraySize - 1).objectReferenceValue = null;
            }
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
