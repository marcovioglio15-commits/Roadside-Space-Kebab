using CatOnASkateboard.StudioIdentity.Editor;
using CatOnASkateboard.StudioIdentity;
using CatOnASkateboard.StudioInput.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws conditional settings shared by feature drafts and reusable presets.</summary>
    internal static class ExtendedInteractionControls
    {
        #region Methods

        #region Workspace

        /// <summary>Edits the retained feature without modifying its prefab before Apply.</summary>
        /// <param name="data">Serialized persistent workspace.</param>
        /// <param name="state">Workspace owning the feature and foldouts.</param>
        /// <returns>True when proposal data changed.</returns>
        internal static bool Draw(SerializedObject data, ObjectWorkspace state)
        {
            // Navigation remains independent of configuration changes.
            data.Update();
            SerializedProperty draft = data.FindProperty("Extended.Draft");
            Field(draft, "Name");
            Field(draft, "Enabled");
            InteractionToolControls.Draw(draft.FindPropertyRelative("ToolRequirement"));
            switch (state.Extended.Kind)
            {
                case ExtendedInteractionKind.Slice:
                    SliceControls.Draw(draft.FindPropertyRelative("Slice"), state.Sections);
                    StudioInputActionMenu.Draw(data, "Extended.Draft.StartAction", "ObjectsLogicStudio.Slice", Button);
                    break;
                case ExtendedInteractionKind.SpawnManagement:
                    SpawnManagementControls.Draw(draft.FindPropertyRelative("SpawnManagement"), state.Sections);
                    break;
                case ExtendedInteractionKind.AssemblyProduct:
                    AssemblyControls.DrawProduct(draft.FindPropertyRelative("AssemblyProduct"), state.Sections, state);
                    break;
                case ExtendedInteractionKind.AssemblyStation:
                    AssemblyControls.DrawStation(draft.FindPropertyRelative("AssemblyStation"), state.Sections);
                    if (draft.FindPropertyRelative("AssemblyStation.Trigger").enumValueIndex == (int)AssemblyStationTrigger.InputAction)
                        StudioInputActionMenu.Draw(data, "Extended.Draft.StartAction", "ObjectsLogicStudio.Assembly", Button);
                    break;
                case ExtendedInteractionKind.Outline:
                    DrawOutline(draft.FindPropertyRelative("Outline"), state.Sections, state.Target.Resolve()?.GetComponent<ObjectHover>() != null);
                    break;
                case ExtendedInteractionKind.Unlock:
                    UnlockInteractionControls.Draw(draft.FindPropertyRelative("Unlock"), state.Sections);
                    break;
                case ExtendedInteractionKind.ModifyByContact:
                    DrawContact(draft.FindPropertyRelative("Contact"), state.Sections);
                    break;
                case ExtendedInteractionKind.Dialogue:
                    DrawDialogue(draft.FindPropertyRelative("Dialogue"), state.Sections);
                    if (state.Sections.Draw("Dialogue Input", "Bind activation and page advancement to the player's existing action maps."))
                        using (new EditorGUI.IndentLevelScope())
                        {
                            if (draft.FindPropertyRelative("Dialogue.Trigger").enumValueIndex == (int)DialogueTrigger.InputAction)
                                StudioInputActionMenu.Draw(data, "Extended.Draft.StartAction", "ObjectsLogicStudio.Dialogue.Start", Button);
                            StudioInputActionMenu.Draw(data, "Extended.Draft.AdvanceAction", "ObjectsLogicStudio.Dialogue.Advance", Button);
                        }
                    break;
            }
            if (state.Sections.Draw("Interaction Debug", "Show selected-object contact or range guides."))
                using (new EditorGUI.IndentLevelScope())
                    Field(draft, "DrawGizmos");
            InteractionFlagControls.Draw(draft.FindPropertyRelative("FlagChange"), state.Sections);
            InteractionVfxControls.Draw(draft.FindPropertyRelative("VisualEffect"), state.Sections, state.Extended.Draft.VfxDuration(state.Extended.Kind));
            bool changed = data.ApplyModifiedProperties();
            if (changed)
                state.Persist();
            return changed;
        }

        /// <summary>Restricts command pickers to performed Button actions.</summary>
        /// <param name="action">Action offered by the shared map selector.</param>
        /// <returns>True when the action represents a discrete command.</returns>
        internal static bool Button(InputAction action)
        {
            // Continuous movement axes cannot advance dialogue or unlock a feature.
            return action.type == InputActionType.Button;
        }

        #endregion

        #region Outline

        /// <summary>Edits reusable shell appearance independently of authored geometry.</summary>
        /// <param name="settings">Serialized outline shader configuration.</param>
        /// <param name="sections">Retained foldout visibility.</param>
        /// <param name="hasHover">Whether the edited object supplies Hover targeting.</param>
        internal static void DrawOutline(SerializedProperty settings, ObjectStudioSections sections, bool hasHover = false)
        {
            // The shared render pass uses the original visible surfaces.
            if (sections.Draw("Outline Appearance", "Configure visible-edge width in pixels, color, light intensity and crease angle."))
                using (new EditorGUI.IndentLevelScope())
                {
                    if (hasHover)
                        Field(settings, "HoverOnly");
                    Field(settings, "Thickness");
                    if (settings.FindPropertyRelative("Thickness").floatValue > 0f)
                    {
                        Field(settings, "Color");
                        Field(settings, "Intensity");
                        Field(settings, "EdgeAngle");
                    }
                }
        }

        #endregion

        #region Contact

        /// <summary>Shows contact timing and independent participant effects.</summary>
        /// <param name="settings">Serialized contact settings.</param>
        /// <param name="sections">Retained foldout visibility.</param>
        internal static void DrawContact(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Flag selection follows the project's current catalog.
            if (sections.Draw("Passive Contact", "Require continuous contact with another flagged Object Item."))
                using (new EditorGUI.IndentLevelScope())
                {
                    ObjectFlagSelector.Draw(settings.FindPropertyRelative("Flags"));
                    Field(settings, "ContactDuration");
                    Field(settings, "ContactTolerance");
                    Field(settings, "QueryInterval");
                    Field(settings, "IncludeTriggers");
                }
            if (sections.Draw("Carried Items", "Exclude carried participants from both contact activation and ongoing modification."))
                using (new EditorGUI.IndentLevelScope())
                {
                    ExcludeCarried(settings, "AllowCarriedSelf", "Exclude Carried Self");
                    ExcludeCarried(settings, "AllowCarriedOther", "Exclude Carried Contact Item");
                }
            if (sections.Draw("Modification", "Control transition timing and interruption behavior."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "Duration");
                    Field(settings, "WhileContact");
                    if (settings.FindPropertyRelative("WhileContact").boolValue)
                        Field(settings, "RevertDelay");
                    if (!settings.FindPropertyRelative("WhileContact").boolValue && settings.FindPropertyRelative("Duration").floatValue > 0f)
                    {
                        Field(settings, "CompleteAfterSeparation");
                        if (!settings.FindPropertyRelative("CompleteAfterSeparation").boolValue
                            || !settings.FindPropertyRelative("AllowCarriedSelf").boolValue
                            || !settings.FindPropertyRelative("AllowCarriedOther").boolValue)
                            Field(settings, "ResumeAfterInterruption");
                    }
                    if (!settings.FindPropertyRelative("WhileContact").boolValue
                        && !settings.FindPropertyRelative("Self.Consume").boolValue && !settings.FindPropertyRelative("Other.Consume").boolValue)
                        Field(settings, "RepeatAfterSeparation");
                }
            if (sections.Draw("Self Effects", "Modify the item owning this component."))
                using (new EditorGUI.IndentLevelScope())
                    DrawEffects(settings.FindPropertyRelative("Self"), false, !settings.FindPropertyRelative("WhileContact").boolValue);
            if (sections.Draw("Contact Item Effects", "Modify the other item using paths relative to its root."))
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawEffects(settings.FindPropertyRelative("Other"), true, !settings.FindPropertyRelative("WhileContact").boolValue);
                    Field(settings, "ChangeContactFlag");
                    if (settings.FindPropertyRelative("ChangeContactFlag").boolValue)
                    {
                        Field(settings, "ContactFlagOperation");
                        if (settings.FindPropertyRelative("ContactFlagOperation").enumValueIndex != (int)ObjectFlagOperation.Clear)
                            Flag(settings.FindPropertyRelative("ContactFlag"));
                    }
                }
            if (sections.Draw("Temporary Restrictions", "Suspend interaction families while effects run; pausing releases these restrictions."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "BlockSelf");
                    Field(settings, "BlockOther");
                }
        }

        /// <summary>Shows only controls used by the participant's enabled effects.</summary>
        /// <param name="effects">Serialized participant effect configuration.</param>
        /// <param name="other">Whether a sample counterpart prefab must supply the hierarchy.</param>
        /// <param name="consumption">Whether irreversible consumption is supported by the selected mode.</param>
        private static void DrawEffects(SerializedProperty effects, bool other, bool consumption)
        {
            // Discrete effects commit only after the configured duration.
            GameObject source = HierarchyPathMenu.Source(effects, other);
            Field(effects, "Tint");
            if (effects.FindPropertyRelative("Tint").boolValue)
            {
                Field(effects, "Multiplier");
                Field(effects, "ColorProperty");
                HierarchyPathMenu.Draw(effects.FindPropertyRelative("RendererPath"), source, false, true);
                Field(effects, "MaterialSlot");
            }
            ItemAppearanceControls.DrawList(effects.FindPropertyRelative("Meshes"), source, true);
            if (consumption || effects.FindPropertyRelative("Consume").boolValue)
                Field(effects, "Consume");
        }

        /// <summary>Displays existing carry eligibility as an explicit exclusion without changing saved defaults.</summary>
        /// <param name="settings">Contact settings retaining the original eligibility fields.</param>
        /// <param name="field">Serialized allow-carried flag to edit.</param>
        /// <param name="label">Participant-specific exclusion label.</param>
        private static void ExcludeCarried(SerializedProperty settings, string field, string label)
        {
            // Invert only the control's presentation; existing prefab and preset values retain their meaning.
            SerializedProperty property = settings.FindPropertyRelative(field);
            EditorGUI.BeginChangeCheck();
            bool excluded = EditorGUILayout.Toggle(new GUIContent(label,
                "Do not start or continue this modification while this participant is carried. Picking it up interrupts using the configured restart or resume policy."), !property.boolValue);
            if (EditorGUI.EndChangeCheck())
                property.boolValue = !excluded;
        }

        #endregion

        #region Dialogue

        /// <summary>Edits dialogue ordering, interruption and explicit page sequences.</summary>
        /// <param name="settings">Serialized dialogue settings.</param>
        /// <param name="sections">Retained foldout visibility.</param>
        internal static void DrawDialogue(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Item-specific input and HUD bindings are drawn by the workspace.
            if (sections.Draw("Dialogue Activation", "Choose trigger, range and arbitration priority."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "Trigger");
                    Field(settings, "Distance");
                    Field(settings, "ExitDistance");
                    Field(settings, "Priority");
                }
            if (sections.Draw("Dialogue Visibility", "Configure startup, page advancement and hiding independently."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "RequireSightToStart");
                    Field(settings, "RequireSightToContinue");
                    Field(settings, "HideWhenSightLost");
                    if (settings.FindPropertyRelative("RequireSightToStart").boolValue
                        || settings.FindPropertyRelative("RequireSightToContinue").boolValue
                        || settings.FindPropertyRelative("HideWhenSightLost").boolValue)
                    {
                        Field(settings, "ObstacleMask");
                        Field(settings, "SightOffset");
                    }
                }
            if (sections.Draw("Dialogue Flow", "Choose entry order and what happens when the player returns."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "Selection");
                    Field(settings, "Interruption");
                    Field(settings, "ReplayOnReturn");
                }
            if (!sections.Draw("Dialogue Entries", "Each entry contains ordered pages and optional consumption requirements."))
                return;
            using EditorGUI.IndentLevelScope sectionIndent = new EditorGUI.IndentLevelScope();
            SerializedProperty entries = settings.FindPropertyRelative("Entries");
            EditorGUILayout.PropertyField(entries.FindPropertyRelative("Array.size"), new GUIContent("Entries", entries.tooltip));
            // Retain native page-list editing within each indented entry.
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded,
                    new GUIContent((index + 1) + ". " + entry.FindPropertyRelative("Name").stringValue, "Edit this entry's conditions and pages."), true);
                if (!entry.isExpanded)
                    continue;
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(entry, "Name");
                    if (settings.FindPropertyRelative("Selection").enumValueIndex == (int)DialogueSelection.WeightedRandom)
                        Field(entry, "Weight");
                    DrawRequirements(entry);
                    Field(entry, "Lines");
                }
            }
        }

        /// <summary>Provides explicit add/remove rows pairing a project flag with a positive integer quantity.</summary>
        /// <param name="entry">Dialogue entry owning the requirements.</param>
        private static void DrawRequirements(SerializedProperty entry)
        {
            // Array size is an implementation detail; each visible row represents one requirement.
            SerializedProperty requirements = entry.FindPropertyRelative("RequiredFlags");
            EditorGUILayout.LabelField(new GUIContent("Consumed Flags", "Required counts of previously consumed items, grouped by project flag."), EditorStyles.boldLabel);
            if (requirements.arraySize > 1)
                Field(entry, "RequireAll");
            FlagRequirementControls.Draw(requirements, "+ Add Consumed Flag", false);
        }

        #endregion

        #region Fields

        /// <summary>Draws a project flag without replacing a missing saved flag during repaint.</summary>
        /// <param name="property">Serialized flag string.</param>
        internal static void Flag(SerializedProperty property)
        {
            // Every identity selector shares filtering, combinations and catalog creation.
            ObjectFlagSelector.Draw(property);
        }

        /// <summary>Draws scalar values without duplicate headers and preserves native array and flag editing.</summary>
        /// <param name="owner">Configuration containing the field.</param>
        /// <param name="name">Exact serialized field name.</param>
        private static void Field(SerializedProperty owner, string name)
        {
            // Restriction masks need the flags control instead of an enum ordinal popup.
            if (name is "BlockSelf" or "BlockOther")
            {
                SerializedProperty property = owner.FindPropertyRelative(name);
                property.intValue = (int)(InteractionChannels)EditorGUILayout.EnumFlagsField(
                    new GUIContent(property.displayName, property.tooltip), (InteractionChannels)property.intValue);
            }
            else
                HoverControls.Field(owner, name);
        }

        #endregion

        #endregion
    }
}
