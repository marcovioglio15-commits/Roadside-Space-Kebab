using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Transfers rule snapshots only after all destination mappings and asset conflicts pass validation.</summary>
    internal static class UnlockPresetOperations
    {
        #region Methods

        #region Capture

        /// <summary>Captures one pending rule or the selected object's complete set.</summary>
        /// <param name="state">Workspace providing the active draft.</param>
        /// <param name="owner">Object whose rules are captured.</param>
        /// <param name="set">Whether to include every local rule.</param>
        /// <returns>Validated portable rule snapshots.</returns>
        internal static UnlockRuleSnapshot[] Capture(ObjectWorkspace state, GameObject owner, bool set)
        {
            // Include the selected draft without silently applying other pending object edits.
            if (owner == null)
                throw new InvalidOperationException("Open the destination prefab workspace first.");
            ObjectInteractionUnlock[] rules = set ? owner.GetComponents<ObjectInteractionUnlock>()
                : new[] { state.Extended.Resolve(owner) as ObjectInteractionUnlock };
            if (rules.Length == 0)
                throw new InvalidOperationException("Add and configure at least one availability rule.");
            UnlockRuleSnapshot[] snapshots = new UnlockRuleSnapshot[rules.Length];
            for (int index = 0; index < rules.Length; index++)
            {
                if (rules[index] == null)
                    throw new InvalidOperationException("Select an existing availability rule.");
                snapshots[index] = state.Extended.Resolve(owner) == rules[index]
                    ? UnlockPresetMapping.Capture(state.Extended.Draft, owner) : UnlockPresetMapping.Capture(rules[index]);
                if (!UnlockPresetMapping.TryResolve(snapshots[index], owner.transform.root, out _, out string warning))
                    throw new InvalidOperationException(warning);
            }
            return snapshots;
        }

        /// <summary>Writes a validated snapshot to a new or explicitly updated preset.</summary>
        /// <param name="preset">Rule or rule-set asset receiving data.</param>
        /// <param name="rules">Validated snapshots to retain.</param>
        internal static void Assign(ScriptableObject preset, UnlockRuleSnapshot[] rules)
        {
            // The inherited tool field stays consistent with the single rule's portable payload.
            switch (preset)
            {
                case UnlockRulePreset single:
                    single.Rule = rules[0];
                    single.ToolRequirement = ObjectWorkspace.Copy(rules[0].ToolRequirement);
                    break;
                case UnlockRuleSetPreset set:
                    set.Rules = rules;
                    break;
            }
        }

        #endregion

        #region Import

        /// <summary>Remaps a single rule into the pending card without changing its source preset.</summary>
        /// <param name="state">Workspace receiving the proposal.</param>
        /// <param name="owner">Destination object containing compatible interactions.</param>
        /// <param name="preset">Source rule snapshot.</param>
        internal static void ImportRule(ObjectWorkspace state, GameObject owner, UnlockRulePreset preset)
        {
            // Reject the complete mapping before modifying the retained draft.
            if (preset == null || owner == null)
                throw new InvalidOperationException("Select a rule preset and open the destination prefab.");
            if (!UnlockPresetMapping.TryResolve(preset.Rule, owner.transform.root, out InteractionUnlockSettings settings, out string warning))
                throw new InvalidOperationException(warning);
            Undo.RecordObject(state, "Import availability rule");
            state.Extended.Draft.Name = preset.Rule.Name;
            state.Extended.Draft.Enabled = preset.Rule.Enabled;
            state.Extended.Draft.Unlock = UnlockInteractionDraft.Capture(settings);
            state.Extended.Draft.ToolRequirement = ObjectWorkspace.Copy(preset.Rule.ToolRequirement);
            state.Extended.PresetBaseline = InteractionPresetWrites.Capture(preset);
            state.Persist();
        }

        /// <summary>Replaces local availability rules as one reversible prefab transaction.</summary>
        /// <param name="state">Workspace refreshed after the import.</param>
        /// <param name="owner">Open destination prefab object.</param>
        /// <param name="preset">Complete rule set to map onto existing ordinary interactions.</param>
        internal static void ImportSet(ObjectWorkspace state, GameObject owner, UnlockRuleSetPreset preset)
        {
            // Preflight every mapping before adding, removing or editing any rule component.
            if (state.HasChanges || EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(owner))
                throw new InvalidOperationException("Apply or Discard pending edits and open the prefab workspace before importing a rule set.");
            if (!ObjectAuthoringSave.TryValidate(owner, out string warning) || preset == null || !preset.TryValidate(out warning))
                throw new InvalidOperationException(warning);
            InteractionUnlockSettings[] settings = new InteractionUnlockSettings[preset.Rules.Length];
            for (int index = 0; index < settings.Length; index++)
                if (!UnlockPresetMapping.TryResolve(preset.Rules[index], owner.transform.root, out settings[index], out warning))
                    throw new InvalidOperationException(warning);

            // Preserve matching components so unrelated local presentation remains available.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Import availability rule set");
            try
            {
                ObjectInteractionUnlock[] existing = owner.GetComponents<ObjectInteractionUnlock>();
                ObjectInteractionUnlock selected = null;
                for (int index = 0; index < settings.Length; index++)
                {
                    ObjectInteractionUnlock rule = index < existing.Length ? existing[index] : Undo.AddComponent<ObjectInteractionUnlock>(owner);
                    Undo.RecordObject(rule, "Import availability rule set");
                    JsonUtility.FromJsonOverwrite("{\"settings\":" + JsonUtility.ToJson(settings[index])
                        + ",\"toolRequirement\":" + JsonUtility.ToJson(preset.Rules[index].ToolRequirement) + "}", rule);
                    using (SerializedObject data = new SerializedObject(rule))
                    {
                        data.FindProperty("interactionName").stringValue = preset.Rules[index].Name;
                        data.FindProperty("m_Enabled").boolValue = preset.Rules[index].Enabled;
                        data.FindProperty("settingsPreset").objectReferenceValue = null;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    EditorUtility.SetDirty(rule);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rule);
                    selected ??= rule;
                }
                for (int index = settings.Length; index < existing.Length; index++)
                    Undo.DestroyObjectImmediate(existing[index]);
                ObjectAuthoringSave.Save(owner);
                Undo.RecordObject(state, "Import availability rule set");
                state.Extended.Select(selected);
                state.RuleSetBaseline = InteractionPresetWrites.Capture(preset);
                state.Persist();
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                // Undo restores component data even if native prefab saving fails.
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        #endregion

        #endregion
    }
}
