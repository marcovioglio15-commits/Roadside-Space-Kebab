using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares a connected conversation without storing progress on the asset.</summary>
#if OBJECTS_LOGIC_STUDIO_DIALOGUE_GRAPH
    [CreateAssetMenu(menuName = "Objects Logic Studio/Dialogue Tree Preset")]
#endif
    public sealed class DialogueTree : ScriptableObject
    {
        #region Fields
        [Header("Dialogue Tree")]
        [Tooltip("Stable key of the first node; selected by name in the graph editor.")]
        public string Start = string.Empty;
        [Tooltip("Connected flow nodes. Shared entries retain their own independent editing sessions.")]
        public DialogueNode[] Nodes = Array.Empty<DialogueNode>();
        #endregion
        #region Methods
        #region Validation
        /// <summary>Checks nodes, links and subtree dependencies before authoring or activation.</summary>
        /// <param name="warning">Receives the first invalid node or connection.</param>
        /// <param name="orders">Optional destination's order catalogs.</param>
        /// <param name="bound">Whether local order names must resolve on the destination.</param>
        /// <param name="consumption">Whether entry consumption filters are active.</param>
        /// <returns>True for a connected, executable tree with no immediate loops or recursive subtree calls.</returns>
        public bool TryValidate(out string warning, OrderSettings orders = null, bool bound = false, bool consumption = true)
        {
            return DialogueTreeValidation.Validate(this, orders, bound, consumption, out warning);
        }
        #endregion
        #endregion
    }
}
