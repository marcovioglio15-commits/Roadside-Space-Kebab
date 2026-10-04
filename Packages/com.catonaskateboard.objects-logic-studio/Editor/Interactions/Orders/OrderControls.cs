using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Pairs named local consuming interactions with explicit order text and a shared scene board.</summary>
    internal static class OrderControls
    {
        #region Methods
        #region Drawing

        /// <summary>Edits pending order rows through selectors restricted to eligible contact actions.</summary>
        /// <param name="draft">Serialized detached order proposal.</param>
        /// <param name="owner">Current prefab object.</param>
        internal static void Draw(SerializedProperty draft, GameObject owner)
        {
            // Scene links use a stable board ID because prefab assets cannot store scene component references.
            SerializedProperty settings = draft.FindPropertyRelative("Settings");
            HoverControls.Field(settings, "Board", "Board ID");
            if (StudioButton.Draw(new GUIContent("Scene Order Board", "Configure the shared world-space text slots in the gameplay scene.")))
                OrderBoardWindow.Open();
            SerializedProperty entries = settings.FindPropertyRelative("Entries");
            SerializedProperty sources = draft.FindPropertyRelative("Sources");
            for (int index = 0; index < entries.arraySize; index++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    using (new StudioButton.RowScope())
                    {
                        GUILayout.Label("Order " + (index + 1), EditorStyles.boldLabel);
                        if (StudioButton.Draw(new GUIContent("Remove", "Remove this order entry from the proposal.")))
                        {
                            entries.DeleteArrayElementAtIndex(index);
                            sources.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }
                    using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                    Source(sources.GetArrayElementAtIndex(index), owner);
                    HoverControls.Field(entry, "Text");
                    HoverControls.Field(entry, "FilterConsumed", "Filter Flags");
                    if (entry.FindPropertyRelative("FilterConsumed").boolValue)
                        using (new EditorGUI.IndentLevelScope())
                            ObjectFlagSelector.Draw(entry.FindPropertyRelative("ConsumedFlags"),
                                new GUIContent("Consumed Flags", "Complete this order when the consumed item had any selected flag. Earlier nonmatching orders remain open."));
                }
            if (StudioButton.Draw(new GUIContent("+ Add Order", "Add an independent order and text slot. Its consuming action may also be used by other orders.")))
            {
                entries.arraySize++;
                sources.arraySize = entries.arraySize;
                SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                added.FindPropertyRelative("Source").objectReferenceValue = null;
                added.FindPropertyRelative("Text").stringValue = "Order";
                added.FindPropertyRelative("FilterConsumed").boolValue = false;
                added.FindPropertyRelative("ConsumedFlags").arraySize = 0;
                sources.GetArrayElementAtIndex(sources.arraySize - 1).longValue = 0;
            }
        }

        /// <summary>Offers only enabled consuming interactions on the current object.</summary>
        /// <param name="property">Stable source identity.</param>
        /// <param name="owner">Prefab object being edited.</param>
        private static void Source(SerializedProperty property, GameObject owner)
        {
            // Names label the menu while file IDs distinguish duplicate component names.
            ObjectContactModifier[] candidates = owner != null ? owner.GetComponents<ObjectContactModifier>() : System.Array.Empty<ObjectContactModifier>();
            string label = "Select consuming interaction";
            foreach (ObjectContactModifier candidate in candidates)
                if (ObjectWorkspaceTarget.FileId(candidate) == property.longValue)
                    label = candidate.InteractionName;
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, property);
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Consume Action", "A Modify By Contact with Consume on this object. Each consumption fulfils its first matching unfinished order in the list."));
            if (!GUI.Button(rect, label, EditorStyles.popup))
                return;
            GenericMenu menu = new GenericMenu();
            foreach (ObjectContactModifier candidate in candidates)
                if (OrderSettings.Eligible(candidate, owner))
                {
                    long identity = ObjectWorkspaceTarget.FileId(candidate);
                    string path = property.propertyPath;
                    SerializedObject data = property.serializedObject;
                    menu.AddItem(new GUIContent(candidate.InteractionName + " (" + identity + ")"), identity == property.longValue, () =>
                    {
                        // Apply affects only the detached editor proposal.
                        data.Update();
                        data.FindProperty(path).longValue = identity;
                        data.ApplyModifiedProperties();
                    });
                }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("Enable Consume on a local Modify By Contact first"));
            menu.DropDown(rect);
        }

        #endregion
        #endregion
    }
}
