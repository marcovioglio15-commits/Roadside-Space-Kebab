using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Composes an entire object's shared settings and interaction presets for import and export.</summary>
    [CreateAssetMenu(fileName = "Object Master", menuName = "Objects Logic Studio/Object Master Preset")]
    internal sealed class ObjectMasterPreset : ScriptableObject
    {
        #region Types

        /// <summary>Assigns an interaction preset to one explicit hierarchy slot.</summary>
        [Serializable]
        internal sealed class InteractionEntry
        {
            [Tooltip("Name shown in the import preview.")]
            public string Name;
            [Tooltip("Object path relative to the configuration root.")]
            public string Path;
            [Tooltip("Exact source component identifier.")]
            public long SourceId;
            [Tooltip("Which same-type component this entry represents.")]
            public int Index;
            [Tooltip("Main preset containing this interaction's group presets.")]
            public ObjectInteractionPreset Preset;
        }

        /// <summary>Assigns shared item or identity settings to one object slot.</summary>
        [Serializable]
        internal sealed class SharedEntry
        {
            [Tooltip("Object path relative to the configuration root.")]
            public string Path;
            [Tooltip("Shared identity or item settings.")]
            public ObjectSharedPreset Preset;
        }

        #endregion

        #region Fields

        [Header("Object")]
        [Tooltip("What this configuration is for.")]
        public string Description = string.Empty;
        [Tooltip("Source prefab. Include it when exporting a complete object.")]
        public GameObject SourcePrefab;
        [Tooltip("Identity and shared item presets for the root and its children.")]
        public SharedEntry[] Shared = Array.Empty<SharedEntry>();
        [Tooltip("Main interaction presets. Each one links to its smaller group presets.")]
        public InteractionEntry[] Interactions = Array.Empty<InteractionEntry>();
        [Tooltip("Unity version used when the configuration was captured.")]
        [HideInInspector]
        public string UnityVersion;
        [Tooltip("Configuration format used by this preset.")]
        [HideInInspector]
        public int Format = 1;

        #endregion
    }
}
