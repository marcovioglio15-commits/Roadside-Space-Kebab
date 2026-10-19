using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Selects a local animation source using portable names and occurrences instead of transient component references.</summary>
    internal static class LocalInteractionMenu
    {
        #region Methods

        #region Drawing

        /// <summary>Lists only supported interactions on the animation's exact object or the preset's sample source.</summary>
        /// <param name="property">Detached source descriptor receiving a menu selection.</param>
        internal static void Draw(SerializedProperty property)
        {
            // Component inspectors use the component's own object, even when its ObjectItem belongs to a parent.
            GameObject owner = property.serializedObject.targetObject is Component component
                ? component.gameObject : HierarchyPathMenu.Source(property);
            LocalInteractionReference reference = (LocalInteractionReference)property.boxedValue;
            ObjectInteraction selected = reference.Resolve(owner);
            string label = !reference.Assigned ? "Select Interaction" : reference.Kind + " / " + reference.Name;
            if (reference.Assigned && owner != null && selected == null)
                label += " (missing)";
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, property);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(property));
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Source Interaction", property.tooltip));
            using (new EditorGUI.DisabledScope(owner == null))
                if (GUI.Button(rect, new GUIContent(label, property.tooltip), EditorStyles.popup))
                    Show(property, owner, selected, rect);
        }

        /// <summary>Builds the component list on demand and guards delayed choices against workspace changes.</summary>
        /// <param name="property">Descriptor to replace.</param>
        /// <param name="owner">Exact object supplying source cards.</param>
        /// <param name="selected">Currently resolved source, if present.</param>
        /// <param name="rect">Popup anchor in the current editor window.</param>
        private static void Show(SerializedProperty property, GameObject owner, ObjectInteraction selected, Rect rect)
        {
            UnityEngine.Object target = property.serializedObject.targetObject;
            string path = property.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(target, path);
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            // Bind a snapshot of the selected card; no source GameObject reference enters a preset.
            foreach (ObjectInteraction candidate in owner.GetComponents<ObjectInteraction>())
            {
                if (candidate is ObjectTriggerAnimation || InteractionTemplateKinds.Of(candidate) is not InteractionTemplateKind kind)
                    continue;
                LocalInteractionReference reference = LocalInteractionReference.Capture(candidate);
                string label = kind + "/" + candidate.InteractionName + " [" + (reference.Occurrence + 1) + "]";
                menu.AddItem(new GUIContent(label), candidate == selected, () =>
                {
                    if (!guard())
                        return;
                    using SerializedObject data = new SerializedObject(target);
                    data.FindProperty(path).boxedValue = reference;
                    data.ApplyModifiedProperties();
                    StudioFieldMenu.Notify(target);
                });
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No other local interaction"));
            menu.DropDown(rect);
        }

        #endregion

        #endregion
    }
}
