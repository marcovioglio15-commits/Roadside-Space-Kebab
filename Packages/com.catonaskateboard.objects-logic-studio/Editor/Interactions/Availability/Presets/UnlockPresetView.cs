using CatOnASkateboard.StudioColors.Editor;
using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares explicit Import, Export and Update controls for one rule and an object's rule set.</summary>
    internal static class UnlockPresetView
    {
        #region Methods

        #region Controls

        /// <summary>Draws operations for the expanded availability-rule card.</summary>
        /// <param name="state">Workspace containing the pending rule.</param>
        /// <param name="owner">Selected prefab object.</param>
        internal static void DrawRule(ObjectWorkspace state, GameObject owner)
        {
            // A single import remains pending until the normal Apply action.
            Draw(state, owner, false);
        }

        /// <summary>Draws operations for all rules on the selected object.</summary>
        /// <param name="state">Workspace retaining the selected set.</param>
        /// <param name="owner">Selected prefab object.</param>
        /// <returns>True after structural import so the caller refreshes its card cache.</returns>
        internal static bool DrawSet(ObjectWorkspace state, GameObject owner)
        {
            // Set import replaces local rule components as a complete structural edit.
            return Draw(state, owner, true);
        }

        /// <summary>Uses the same conflict checks and snapshot operations at both preset scopes.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="owner">Selected prefab object.</param>
        /// <param name="set">Whether these controls operate on the complete local set.</param>
        /// <returns>True after importing a complete set.</returns>
        private static bool Draw(ObjectWorkspace state, GameObject owner, bool set)
        {
            // Asset selection is independent from explicit copying in either direction.
            ScriptableObject previous = set ? state.RuleSet : state.Extended.Preset;
            ScriptableObject selected = (ScriptableObject)EditorGUILayout.ObjectField(new GUIContent(set ? "Rule Set" : "Rule Preset",
                set ? "Import replaces all availability rules on this object after mapping every target and source."
                    : "Transfer this rule to compatible existing interactions on another prefab."), previous,
                set ? typeof(UnlockRuleSetPreset) : typeof(UnlockRulePreset), false);
            if (selected != previous)
                Select(state, selected, set);
            using (new StudioButton.RowScope())
            {
                try
                {
                    using (new EditorGUI.DisabledScope(selected == null || owner == null || set && state.HasChanges))
                        if (StudioButton.Draw(new GUIContent("Import", set ? "Replace and save all local rules with Undo. Apply or Discard pending edits first."
                            : "Remap this preset onto the destination and copy it into the pending card.")))
                        {
                            if (set)
                                UnlockPresetOperations.ImportSet(state, owner, (UnlockRuleSetPreset)selected);
                            else
                                UnlockPresetOperations.ImportRule(state, owner, (UnlockRulePreset)selected);
                            return set;
                        }
                    using (new EditorGUI.DisabledScope(selected == null || owner == null))
                        if (StudioButton.Draw(new GUIContent("Update", "Write the current rule configuration to the selected preset.")))
                            Update(state, owner, selected, set);
                    using (new EditorGUI.DisabledScope(owner == null))
                        if (StudioButton.Draw(new GUIContent("Export", "Save the current rule configuration in a new reusable preset.")))
                            Export(state, owner, set);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(exception.Message, selected);
                }
            }
            return false;
        }

        #endregion

        #region Assets

        /// <summary>Retains the chosen asset and its conflict-detection baseline.</summary>
        /// <param name="state">Workspace receiving the selection.</param>
        /// <param name="preset">Selected asset or null.</param>
        /// <param name="set">Whether the selection belongs to the complete set.</param>
        private static void Select(ObjectWorkspace state, ScriptableObject preset, bool set)
        {
            // Selecting an asset alone never replaces current rule settings.
            if (set)
            {
                state.RuleSet = (UnlockRuleSetPreset)preset;
                state.RuleSetBaseline = InteractionPresetWrites.Capture(preset);
            }
            else
            {
                state.Extended.Preset = (UnlockRulePreset)preset;
                state.Extended.PresetBaseline = InteractionPresetWrites.Capture(preset);
            }
            state.Persist();
        }

        /// <summary>Updates only an unchanged, writable selected asset.</summary>
        /// <param name="state">Workspace providing draft values and baseline.</param>
        /// <param name="owner">Object whose rules are captured.</param>
        /// <param name="preset">Selected destination asset.</param>
        /// <param name="set">Whether to capture all local rules.</param>
        internal static void Update(ObjectWorkspace state, GameObject owner, ScriptableObject preset, bool set)
        {
            // Complete validation precedes Undo and persistent writes.
            if (!InteractionPresetWrites.ValidateAsset(preset, set ? state.RuleSetBaseline : state.Extended.PresetBaseline, out string warning))
                throw new InvalidOperationException(warning);
            UnlockRuleSnapshot[] snapshots = UnlockPresetOperations.Capture(state, owner, set);
            Undo.RecordObject(state, "Update availability preset");
            Undo.RecordObject(preset, "Update availability preset");
            UnlockPresetOperations.Assign(preset, snapshots);
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssetIfDirty(preset);
            Select(state, preset, set);
        }

        /// <summary>Exports validated mappings without applying pending object edits.</summary>
        /// <param name="state">Workspace providing the current draft.</param>
        /// <param name="owner">Object whose rules are captured.</param>
        /// <param name="set">Whether to export all local rules.</param>
        private static void Export(ObjectWorkspace state, GameObject owner, bool set)
        {
            // Validate before opening a file picker so incomplete mappings cannot create unusable assets.
            UnlockRuleSnapshot[] snapshots = UnlockPresetOperations.Capture(state, owner, set);
            string path = EditorUtility.SaveFilePanelInProject("Export availability preset", set ? "Unlock Rule Set" : "Unlock Rule", "asset", "Save reusable rule settings.");
            if (string.IsNullOrEmpty(path))
                return;
            ScriptableObject preset = set ? ScriptableObject.CreateInstance<UnlockRuleSetPreset>() : ScriptableObject.CreateInstance<UnlockRulePreset>();
            UnlockPresetOperations.Assign(preset, snapshots);
            AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(preset, "Export availability preset");
            AssetDatabase.SaveAssetIfDirty(preset);
            Select(state, preset, set);
        }

        #endregion

        #endregion
    }
}
