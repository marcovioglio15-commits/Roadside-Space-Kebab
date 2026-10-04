using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Transfers independent Hover settings through explicit Import, Export and Update actions.</summary>
    internal static class HoverPresetView
    {
        #region Methods

        #region Controls

        /// <summary>Draws the same explicit snapshot actions as the other interaction cards.</summary>
        /// <param name="state">Workspace retaining the destination's independent draft.</param>
        internal static void Draw(ObjectWorkspace state)
        {
            // Selecting a source does not import it or overwrite its saved configuration.
            HoverPreset selected = (HoverPreset)StudioGUI.ObjectField(new GUIContent("Hover Preset",
                "Reusable snapshot. Import copies values here; Apply saves only this interaction."), state.Source, typeof(HoverPreset), false);
            if (selected != state.Source)
            {
                Undo.RecordObject(state, "Select hover preset");
                state.Source = selected;
                state.SourceBaseline = InteractionPresetWrites.Capture(selected);
                state.Persist();
            }
            using (new StudioButton.RowScope())
            {
                using (new EditorGUI.DisabledScope(state.Source == null))
                {
                    if (StudioButton.Draw(new GUIContent("Import", "Copy the selected snapshot into this object's pending settings.")))
                        Import(state);
                    if (StudioButton.Draw(new GUIContent("Update", "Overwrite the selected preset with this draft, retaining other objects' local settings.")))
                        Update(state);
                }
                if (StudioButton.Draw(new GUIContent("Export", "Save the current Hover draft as a new reusable preset.")))
                    Export(state);
            }
        }

        #endregion

        #region Transfer

        /// <summary>Copies a saved snapshot without replacing the target object's conflict baseline.</summary>
        /// <param name="state">Destination workspace.</param>
        internal static void Import(ObjectWorkspace state)
        {
            // Import remains valid even when the source is read-only.
            if (state.Source == null)
                return;
            if (!state.Source.Configuration.TryValidate(out string warning) || !state.Source.ToolRequirement.TryValidate(out warning))
            {
                Debug.LogWarning(warning, state.Source);
                return;
            }
            Undo.RecordObject(state, "Import hover preset");
            state.Draft = ObjectWorkspace.Copy(state.Source.Configuration);
            state.Binding.ToolRequirement = ObjectWorkspace.Copy(state.Source.ToolRequirement);
            state.SourceBaseline = InteractionPresetWrites.Capture(state.Source);
            state.Persist();
        }

        /// <summary>Updates only the chosen asset after checking its saved baseline.</summary>
        /// <param name="state">Workspace providing the current draft.</param>
        internal static void Update(ObjectWorkspace state)
        {
            // Apply never calls this method; asset writes are always explicit.
            if (state.Source == null)
                return;
            if (!AssetDatabase.IsOpenForEdit(state.Source) || InteractionPresetWrites.Capture(state.Source) != state.SourceBaseline)
            {
                Debug.LogWarning("The Hover preset is read-only or changed elsewhere. Import its current values before updating it.", state.Source);
                return;
            }
            if (!state.Draft.TryValidate(out string warning) || !state.Binding.ToolRequirement.TryValidate(out warning))
            {
                Debug.LogWarning(warning, state.Source);
                return;
            }
            Undo.RecordObject(state, "Update hover preset");
            Undo.RecordObject(state.Source, "Update hover preset");
            Write(state.Source, state);
            state.SourceBaseline = InteractionPresetWrites.Capture(state.Source);
            state.Persist();
        }

        /// <summary>Saves the current proposal at a new unique path.</summary>
        /// <param name="state">Workspace providing settings and retaining the new preset selection.</param>
        private static void Export(ObjectWorkspace state)
        {
            // Cancellation and invalid settings leave the source and destination untouched.
            if (!state.Draft.TryValidate(out string warning) || !state.Binding.ToolRequirement.TryValidate(out warning))
            {
                Debug.LogWarning(warning);
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("Export Hover preset", "Hover Preset", "asset", "Save the current Hover settings.");
            if (string.IsNullOrEmpty(path))
                return;
            HoverPreset preset = ObjectPresetAssets.Create(path, null);
            Write(preset, state);
            Undo.RecordObject(state, "Select exported Hover preset");
            Undo.RegisterCreatedObjectUndo(preset, "Export Hover preset");
            state.Source = preset;
            state.SourceBaseline = InteractionPresetWrites.Capture(preset);
            state.Persist();
        }

        /// <summary>Writes a detached snapshot while retaining asset identity and native references.</summary>
        /// <param name="preset">Explicit asset destination.</param>
        /// <param name="state">Workspace supplying the snapshot.</param>
        private static void Write(HoverPreset preset, ObjectWorkspace state)
        {
            // Runtime reads only the local component snapshot after Apply.
            JsonUtility.FromJsonOverwrite("{\"configuration\":" + JsonUtility.ToJson(state.Draft)
                + ",\"ToolRequirement\":" + JsonUtility.ToJson(state.Binding.ToolRequirement) + "}", preset);
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssetIfDirty(preset);
        }

        #endregion

        #endregion
    }
}
