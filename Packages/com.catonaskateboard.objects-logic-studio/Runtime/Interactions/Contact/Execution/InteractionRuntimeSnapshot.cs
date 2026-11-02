using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Copies serialized settings through Unity's native serializer without overwriting a component.</summary>
    internal static class InteractionRuntimeCopy
    {
        #region Methods

        #region Settings

        /// <summary>Copies mutable settings while retaining referenced assets as read-only inputs.</summary>
        /// <param name="source">Serializable settings object.</param>
        /// <param name="destination">Existing instance-owned settings object.</param>
        internal static void Into(object source, object destination)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), destination);
        }

        #endregion

        #endregion
    }

    /// <summary>Restores one card's serialized configuration independently of its gameplay progress.</summary>
    internal sealed class InteractionRuntimeSnapshot
    {
        #region State

        internal readonly ObjectInteraction Feature;
        internal readonly bool Enabled;
        private readonly bool removed;
        private readonly object[] sections;
        private readonly string[] values;
        private readonly string name;
        private readonly string tools;
        private readonly string flags;
        private readonly string effects;
        private readonly InputActionReference action;
        private readonly InputActionReference advance;

        #endregion

        #region Methods

        #region Snapshot

        /// <summary>Captures settings once at a contact transaction boundary.</summary>
        /// <param name="feature">Exact existing card whose configuration may change.</param>
        internal InteractionRuntimeSnapshot(ObjectInteraction feature)
        {
            Feature = feature;
            Enabled = feature.enabled;
            removed = InteractionRuntimeState.IsRemoved(feature);
            name = feature.InteractionName;
            tools = JsonUtility.ToJson(feature.ToolRequirement);
            flags = JsonUtility.ToJson(feature.FlagChange);
            effects = JsonUtility.ToJson(feature.VisualEffect);
            action = feature is ObjectSingleInteraction single ? single.Action : feature is ObjectRequestedInteraction request
                ? request.Action : feature is ObjectDialogue dialogue ? dialogue.StartAction : null;
            advance = feature is ObjectDialogue speech ? speech.AdvanceAction : null;
            sections = InteractionRuntimeCatalog.Sections(feature);
            values = new string[sections.Length];
            for (int index = 0; index < sections.Length; index++)
                values[index] = JsonUtility.ToJson(sections[index]);
        }

        /// <summary>Restores copied settings without resetting active progress during a validation probe.</summary>
        /// <param name="refresh">Whether to restore enabled state and affected caches.</param>
        internal void Restore(bool refresh)
        {
            if (Feature == null)
                return;
            for (int index = 0; index < sections.Length; index++)
                JsonUtility.FromJsonOverwrite(values[index], sections[index]);
            Feature.SetRuntimeName(name);
            JsonUtility.FromJsonOverwrite(tools, Feature.ToolRequirement);
            JsonUtility.FromJsonOverwrite(flags, Feature.FlagChange);
            JsonUtility.FromJsonOverwrite(effects, Feature.VisualEffect);
            InteractionRuntimeCatalog.BindActions(Feature, action, advance);
            InteractionRuntimeState.SetRemoved(Feature, removed);
            if (!refresh)
                return;
            bool changed = Feature.enabled != Enabled;
            Feature.enabled = Enabled;
            if (!changed)
                InteractionRuntimeCatalog.Refresh(Feature);
        }

        #endregion

        #endregion
    }
}
