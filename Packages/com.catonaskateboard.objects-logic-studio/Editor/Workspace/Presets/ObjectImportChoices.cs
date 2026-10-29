using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Stores explicit choices made in the master import preview.</summary>
    internal static class ObjectImportChoices
    {
        #region Types

        /// <summary>Selects an interaction to update or add during import.</summary>
        [Serializable]
        internal sealed class Interaction
        {
            [Tooltip("Import this interaction.")]
            public bool Include = true;
            [Tooltip("Add a new interaction instead of updating an existing one.")]
            public bool Add;
            [Tooltip("Existing component to update.")]
            public ObjectInteraction Target;
            [Tooltip("Choose individual settings groups to import.")]
            public bool ChooseGroups;
            [Tooltip("Settings groups selected for import.")]
            public System.Collections.Generic.List<string> Groups = new System.Collections.Generic.List<string>();
        }

        /// <summary>Maps a source object path to an existing destination object.</summary>
        [Serializable]
        internal sealed class Node
        {
            [Tooltip("Object slot from the master.")]
            public string Path;
            [Tooltip("Object to use for this slot.")]
            public Transform Target;
        }

        /// <summary>Replaces a missing or ambiguous component or scene link.</summary>
        [Serializable]
        internal sealed class Link
        {
            [Tooltip("Source slot being replaced.")]
            public string Key;
            [Tooltip("Object or asset to use for this slot.")]
            public UnityEngine.Object Target;
        }

        #endregion
    }
}
