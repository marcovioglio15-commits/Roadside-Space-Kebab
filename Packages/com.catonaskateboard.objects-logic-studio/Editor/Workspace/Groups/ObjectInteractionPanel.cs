using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Routes all interaction families through the same group discovery and focused editing path.</summary>
    internal static class ObjectInteractionPanel
    {
        #region Methods

        #region Groups

        /// <summary>Discovers groups using the current draft's activation modes and dependencies.</summary>
        /// <param name="state">Workspace supplying the selected interaction.</param>
        /// <param name="data">Serialized wrapper for that workspace.</param>
        /// <returns>Available groups with exact field paths.</returns>
        internal static List<ObjectGroupInfo> Groups(ObjectWorkspace state, SerializedObject data)
        {
            List<ObjectGroupInfo> groups = new List<ObjectGroupInfo>();
            using (ObjectGroupScope scope = new ObjectGroupScope(groups))
                Draw(state, data);
            ObjectGroupModes.Collect(state, groups);
            ObjectInteraction selected = ObjectWorkspaceDrafts.Selected(state);
            foreach (ObjectGroupInfo group in groups)
            {
                group.TypeName = selected != null ? selected.GetType().FullName : string.Empty;
                if (selected is ObjectAvailableOrders && group.Title == "Order reveal")
                    group.Paths = new[] { "Extended.Draft.Orders.WaitForCompletion", "Extended.Draft.OrdersCompletionId" };
            }
            return ObjectGroupThemes.Combine(groups);
        }

        /// <summary>Draws reusable group controls without replacing the existing feature-specific editors.</summary>
        /// <param name="state">Workspace or detached group document.</param>
        /// <param name="data">Serialized wrapper for the document.</param>
        internal static void Draw(ObjectWorkspace state, SerializedObject data)
        {
            ObjectInteraction feature = ObjectWorkspaceDrafts.Selected(state);
            switch (feature)
            {
                case ObjectHover hover:
                    Hover(state, data, hover);
                    break;
                case ObjectSingleInteraction:
                    SingleInteractionControls.Draw(data, state);
                    break;
                case ObjectExtendedInteraction:
                    ExtendedInteractionControls.Draw(data, state);
                    break;
            }
        }

        /// <summary>Draws one compact row per thematic group, with independent editing and presets.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="groups">Cached groups for the selected draft.</param>
        /// <param name="search">Current search terms.</param>
        internal static void Overview(ObjectWorkspace state, List<ObjectGroupInfo> groups, string search)
        {
            using SerializedObject data = new SerializedObject(state);
            foreach (ObjectGroupInfo group in groups)
            {
                if (!ObjectSettingsSearch.MatchesGroup(group, search))
                    continue;
                string requirement = ObjectGroupAvailability.Requirement(data, group, out _);
                if (!string.IsNullOrEmpty(requirement) && string.IsNullOrWhiteSpace(search))
                    continue;
                using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
                if (ObjectWorkspaceGUI.Row(group.Title, false, ObjectFieldText.Short(group.Tooltip)))
                    ObjectGroupWindow.Open(state, group);
                if (GUILayout.Button(new GUIContent("Edit", "Open these settings."), EditorStyles.miniButton, GUILayout.Width(42f)))
                    ObjectGroupWindow.Open(state, group);
                if (!string.IsNullOrEmpty(requirement))
                    EditorGUILayout.LabelField(requirement, EditorStyles.wordWrappedMiniLabel);
            }
        }

        #endregion

        #region Hover

        /// <summary>Includes hover binding, label and feedback settings in the same group model as other interactions.</summary>
        /// <param name="state">Workspace or detached group document.</param>
        /// <param name="data">Serialized document.</param>
        /// <param name="hover">Selected existing hover component.</param>
        private static void Hover(ObjectWorkspace state, SerializedObject data, ObjectHover hover)
        {
            data.Update();
            SerializedProperty binding = data.FindProperty("Binding");
            if (state.Sections.Draw("Interaction", "Name, switch and anchor.", binding, "Name", "Enabled", "AnchorPath"))
            {
                using StudioIndentScope sectionContent1 = new StudioIndentScope();
                HoverControls.Field(binding, "Name");
                HoverControls.Field(binding, "Enabled");
                SerializedProperty path = binding.FindPropertyRelative("AnchorPath");
                Transform current = HoverHierarchy.Resolve(hover.transform, path.stringValue);
                Transform selected = (Transform)StudioFieldGUI.ObjectField(new GUIContent("Anchor", "Choose a point on this object. Leave empty to use its root."), current, typeof(Transform), true);
                if (selected != current && (selected == null || selected.IsChildOf(hover.transform)))
                    path.stringValue = HoverHierarchy.Path(hover.transform, selected);
            }
            if (state.Sections.Draw("Player tool", "Choose which tools can use this interaction.", binding, "ToolRequirement"))
                using (new StudioIndentScope())
                    InteractionToolControls.Draw(binding.FindPropertyRelative("ToolRequirement"));
            InteractionFlagControls.Draw(binding.FindPropertyRelative("FlagChange"), state.Sections);
            InteractionVfxControls.Draw(binding.FindPropertyRelative("VisualEffect"), state.Sections, 0f);
            HoverControls.Draw(data.FindProperty("Draft"), state.Sections);
            if (state.Sections.Draw("Debug guides", "Show guides when this object is selected.", binding, "DrawGizmos"))
                using (new StudioIndentScope())
                    HoverControls.Field(binding, "DrawGizmos");
            if (data.ApplyModifiedProperties())
                state.Persist();
        }

        #endregion

        #endregion
    }
}
