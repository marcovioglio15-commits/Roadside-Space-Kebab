using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares named text colors through explicit imports into individual entries.</summary>
    [CreateAssetMenu(fileName = "SO_DialogueColors", menuName = "Objects Logic Studio/Dialogue Color Palette")]
    internal sealed class DialoguePalettePreset : ScriptableObject
    {
        #region Fields

        [Header("Text Colors")]
        [Tooltip("Named colors copied into an entry when Import Palette is used.")]
        public DialogueNamedColor[] Colors = Array.Empty<DialogueNamedColor>();

        #endregion
    }
}
