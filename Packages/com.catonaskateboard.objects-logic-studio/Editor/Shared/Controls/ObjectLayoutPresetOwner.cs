using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps a preset preview's sample and save information after its settings window closes.</summary>
    [Serializable]
    internal sealed class ObjectLayoutPresetOwner
    {
        #region Fields

        [Header("Preset")]
        [Tooltip("Isolated sample used to resolve the preset’s local links.")]
        public GameObject Preview;
        [Tooltip("Preset receiving the independent layout.")]
        public ObjectGroupPreset Preset;
        [Tooltip("Settings group identifying the preset's fields.")]
        public ObjectGroupInfo Group;
        [Tooltip("Preset state before the layout is applied.")]
        public string Stamp;
        [Tooltip("Whether the sample resolves the preset’s local links.")]
        public bool CanSave;

        #endregion

        #region Methods

        #region Persistence

        /// <summary>Writes only the independent layout after its settings editor has closed.</summary>
        /// <param name="document">Retained sample workspace.</param>
        /// <param name="proposal">Settings containing the layout edits.</param>
        /// <param name="paths">Exact layout fields to transfer.</param>
        /// <param name="fillThreshold">Optional Actual Item Fill threshold.</param>
        internal void Apply(ObjectWorkspace document, ObjectWorkspace proposal, string[] paths, int fillThreshold)
        {
            Stamp = ObjectGroupPresetStore.WriteLayout(Preset, Group, document, proposal, paths, fillThreshold, Stamp, CanSave, out _);
        }

        #endregion

        #endregion
    }
}
