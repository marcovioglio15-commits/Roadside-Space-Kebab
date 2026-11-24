using System;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits contact docking slots in the interaction and the shared magnet preview.</summary>
    internal static class ContactMagnetControls
    {
        #region Methods
        #region Drawing

        /// <summary>Shows timing and slot controls only while contact docking is enabled.</summary>
        /// <param name="settings">Serialized docking configuration.</param>
        /// <param name="workspace">Workspace retaining the interaction and its layout source.</param>
        internal static void Draw(SerializedProperty settings, ObjectWorkspace workspace)
        {
            // Keep inactive attraction settings out of the interaction form.
            if (!workspace.Sections.Draw("Lock Item With Magnets", "Dock contact items in the nearest free slot.", settings))
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            HoverControls.Field(settings, "Enabled");
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            HoverControls.Field(settings, "Instant");
            if (!settings.FindPropertyRelative("Instant").boolValue)
                HoverControls.Field(settings, "Duration");
            if (StudioButton.Draw(new GUIContent("Magnet Layout", "Place docking slots in the shared 3D preview.")))
            {
                settings.serializedObject.ApplyModifiedProperties();
                AssemblyPreviewWindow.OpenContact(workspace);
                GUIUtility.ExitGUI();
            }
            // Slot edits remain part of the workspace draft until Apply.
            HoverControls.Field(settings, "WhenOccupied");
            DrawBackground(settings.FindPropertyRelative("PreviewPrefab"), workspace.Target.Resolve());
            SerializedProperty magnets = settings.FindPropertyRelative("Magnets");
            for (int index = 0; index < magnets.arraySize; index++)
            {
                SerializedProperty magnet = magnets.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        magnet.isExpanded = StudioArrayGUI.Foldout(magnet, new GUIContent($"{index + 1}. {magnet.FindPropertyRelative("Name").stringValue}", "Edit this docking slot."));
                        if (GUILayout.Button(new GUIContent("−", "Remove this docking slot."), GUILayout.Width(28f)))
                        {
                            magnets.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }
                    if (magnet.isExpanded)
                        DrawMagnet(magnet);
                }
            }
            if (!StudioButton.Draw(new GUIContent("+ Add Magnet", "Add a docking slot accepted by the contact modifications.")))
                return;
            magnets.arraySize++;
            SerializedProperty added = magnets.GetArrayElementAtIndex(magnets.arraySize - 1);
            added.boxedValue = new ContactMagnet { Name = "Magnet " + magnets.arraySize };
            added.isExpanded = true;
        }

        /// <summary>Shows an independent background model while defaulting to the current interaction object.</summary>
        /// <param name="property">Optional background prefab override.</param>
        /// <param name="owner">Default object geometry.</param>
        internal static void DrawBackground(SerializedProperty property, GameObject owner)
        {
            // Store only an override so imported layouts default to their new destination object.
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object selected = StudioFieldGUI.ObjectField(StudioFieldMenu.Value(property, new GUIContent("Preview Object", property.tooltip)),
                property.objectReferenceValue != null ? property.objectReferenceValue : owner, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
                property.objectReferenceValue = selected;
        }

        /// <summary>Draws only the fields shared by contact slots and their preview projection.</summary>
        /// <param name="magnet">Contact slot or projected assembly slot.</param>
        internal static void DrawMagnet(SerializedProperty magnet)
        {
            // The same row works for contact data and its independent preview projection.
            HoverControls.Field(magnet, "Name");
            SerializedProperty any = magnet.FindPropertyRelative("AnyItem") ?? magnet.FindPropertyRelative("AnyIngredient");
            any.boolValue = !StudioFieldGUI.Toggle(StudioFieldMenu.Value(any, new GUIContent("Filter By Flags", "Restrict this magnet to the selected item flags.")), !any.boolValue);
            HoverControls.Field(magnet, "Priority");
            if (!any.boolValue)
                ObjectFlagSelector.Draw(magnet.FindPropertyRelative("Flags"));
            InventoryFillMagnetControls.Pose(magnet, false);
            SerializedProperty prefab = magnet.FindPropertyRelative("PreviewPrefab");
            prefab.objectReferenceValue = StudioFieldGUI.ObjectField(StudioFieldMenu.Value(prefab, new GUIContent("Preview Prefab", prefab.tooltip)),
                prefab.objectReferenceValue, typeof(GameObject), false);
        }

        #endregion
        #region Projection

        /// <summary>Projects contact poses into the common geometry and handle format.</summary>
        /// <param name="settings">Contact docking configuration.</param>
        /// <returns>Independent settings used only by the layout draft.</returns>
        internal static AssemblyProductSettings Preview(ContactMagnetSettings settings)
        {
            // Render-only projection reuses the existing geometry cache and handles.
            return new AssemblyProductSettings
            {
                Magnets = Array.ConvertAll(settings.Magnets, magnet => new AssemblyMagnet
                {
                    Name = magnet.Name, AnyIngredient = magnet.AnyItem,
                    Flags = magnet.Flags == null ? null : (ObjectFlag[])magnet.Flags.Clone(),
                    Position = magnet.Position, Rotation = magnet.Rotation, PreviewPrefab = magnet.PreviewPrefab
                })
            };
        }

        #endregion
        #endregion
    }
}
