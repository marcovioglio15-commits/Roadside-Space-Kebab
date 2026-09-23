using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Imports and exports independent settings snapshots without changing other items or input bindings.</summary>
    internal static class SingleInteractionPresetView
    {
        #region Methods

        #region Drawing

        /// <summary>Shows a type-specific preset picker inside the selected interaction card.</summary>
        /// <param name="state">Workspace retaining the destination draft.</param>
        internal static void Draw(ObjectWorkspace state)
        {
            // Import replaces only settings; the existing action, name and enabled state remain item-owned.
            SingleInteractionSession session = state.Single;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                SingleInteractionPreset selected = (SingleInteractionPreset)EditorGUILayout.ObjectField(
                    new GUIContent(session.Kind + " Preset", "Reusable settings snapshot. Import copies values into this draft; Apply writes them to the prefab."),
                    session.Preset, PresetType(session.Kind), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(state, "Select interaction preset");
                    session.Preset = selected;
                    state.Persist();
                }
                using (new EditorGUI.DisabledScope(session.Preset == null))
                    if (GUILayout.Button(new GUIContent("Import", "Copy this preset's settings into the draft with Undo. Apply saves the result to this prefab."), GUILayout.Width(54f)))
                    {
                        if (TryImport(state, out string warning))
                            GUI.changed = true;
                        else
                            Debug.LogWarning(warning, session.Preset);
                    }
                if (GUILayout.Button(new GUIContent("Export", "Save the current draft settings as a new preset asset. Existing presets and other items are unchanged."), GUILayout.Width(54f)))
                    Export(state);
            }
        }

        #endregion

        #region Transfer

        /// <summary>Copies a validated matching preset into the retained proposal.</summary>
        /// <param name="state">Workspace receiving copied settings.</param>
        /// <param name="warning">Receives a missing, incompatible or invalid preset.</param>
        /// <returns>True when the settings were imported.</returns>
        internal static bool TryImport(ObjectWorkspace state, out string warning)
        {
            // Reject incompatible assets before recording an edit or touching any draft values.
            warning = "Choose a preset for the selected interaction type.";
            if (state.Single.Preset == null || state.Single.Preset.Kind != state.Single.Kind
                || !state.Single.Preset.TryValidate(out warning))
                return false;
            Undo.RecordObject(state, "Import " + state.Single.Kind + " settings");
            switch (state.Single.Preset)
            {
                case GrabPreset grab:
                    state.Single.Draft.Grab = ObjectWorkspace.Copy(grab.Settings);
                    break;
                case DropPreset drop:
                    state.Single.Draft.Release = ObjectWorkspace.Copy(drop.Settings);
                    break;
                case ThrowPreset launch:
                    state.Single.Draft.Release = ObjectWorkspace.Copy(launch.Settings);
                    state.Single.Draft.Throw = ObjectWorkspace.Copy(launch.Trajectory);
                    break;
            }
            state.Persist();
            return true;
        }

        /// <summary>Builds an independent asset candidate from the selected draft.</summary>
        /// <param name="session">Interaction type and current settings.</param>
        /// <returns>A transient preset owned by the caller until saved or destroyed.</returns>
        internal static SingleInteractionPreset Create(SingleInteractionSession session)
        {
            // Only the active interaction payload is copied; scene references never enter these assets.
            SingleInteractionPreset preset = (SingleInteractionPreset)ScriptableObject.CreateInstance(PresetType(session.Kind));
            switch (preset)
            {
                case GrabPreset grab:
                    grab.Settings = ObjectWorkspace.Copy(session.Draft.Grab);
                    break;
                case DropPreset drop:
                    drop.Settings = ObjectWorkspace.Copy(session.Draft.Release);
                    break;
                case ThrowPreset launch:
                    launch.Settings = ObjectWorkspace.Copy(session.Draft.Release);
                    launch.Trajectory = ObjectWorkspace.Copy(session.Draft.Throw);
                    break;
            }
            return preset;
        }

        /// <summary>Saves a validated draft snapshot at an explicitly chosen new asset path.</summary>
        /// <param name="state">Workspace retaining selection and current settings.</param>
        private static void Export(ObjectWorkspace state)
        {
            // A canceled path chooser and invalid settings leave the workspace untouched.
            SingleInteractionPreset created = Create(state.Single);
            try
            {
                if (!created.TryValidate(out string warning))
                {
                    Debug.LogWarning(warning);
                    return;
                }
                string path = EditorUtility.SaveFilePanelInProject("Export " + state.Single.Kind + " preset",
                    state.Single.Kind + " Preset", "asset", "Save reusable settings for this interaction.");
                if (string.IsNullOrEmpty(path))
                    return;
                AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
                AssetDatabase.SaveAssetIfDirty(created);
                state.Single.Preset = created;
                state.Persist();
            }
            finally
            {
                // A saved asset belongs to the project; only unsaved candidates need disposal.
                if (!EditorUtility.IsPersistent(created))
                    UnityEngine.Object.DestroyImmediate(created);
            }
        }

        /// <summary>Constrains the asset picker and factory to one dedicated preset type.</summary>
        /// <param name="kind">Selected interaction.</param>
        /// <returns>The matching ScriptableObject type.</returns>
        private static Type PresetType(SingleInteractionKind kind)
        {
            // Explicit types avoid reflection-based field discovery and ambiguous shared release presets.
            return kind switch
            {
                SingleInteractionKind.Grab => typeof(GrabPreset),
                SingleInteractionKind.Drop => typeof(DropPreset),
                SingleInteractionKind.Throw => typeof(ThrowPreset),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        #endregion

        #endregion
    }
}
