using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Reuses a preset's entry list for a specific occurrence or a range of subsequent spawns.</summary>
    [Serializable]
    public sealed class DialogueSpawnPreset
    {
        #region Fields
        [Header("Spawn Content")]
        [Tooltip("Exact affects one occurrence; From remains active until a later threshold or exact override wins.")]
        public OrderSpawnMatch Match;
        [Tooltip("One-based spawn occurrence, shared by this object's Dialogue and Available Orders interactions.")]
        public int Spawn = 1;
        [Tooltip("Dialogue preset supplying only Entries and Selection. Its trigger, audio, inputs and spawn rules are not imported.")]
        public DialoguePreset Preset;
        #endregion
    }
}
