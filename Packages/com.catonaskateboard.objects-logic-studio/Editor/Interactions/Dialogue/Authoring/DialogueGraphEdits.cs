#if OBJECTS_LOGIC_STUDIO_DIALOGUE_GRAPH
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Groups structural graph changes into native Undo operations without modifying shared entries.</summary>
    internal static class DialogueGraphEdits
    {
        #region Methods
        #region Mutation
        /// <summary>Adds a newly identified operation at the requested canvas point.</summary>
        /// <param name="tree">Detached graph proposal.</param>
        /// <param name="kind">Operation to create.</param>
        /// <param name="position">Unscaled graph coordinate.</param>
        /// <returns>The new node for selection.</returns>
        internal static DialogueNode Add(DialogueTree tree, DialogueNodeKind kind, Vector2 position)
        {
            Undo.RecordObject(tree, "Add Dialogue Node");
            DialogueNode node = new DialogueNode { Id = Guid.NewGuid().ToString("N"), Name = kind.ToString(), Kind = kind, Position = position };
            ArrayUtility.Add(ref tree.Nodes, node);
            if (tree.Nodes.Length == 1)
                tree.Start = node.Id;
            EditorUtility.SetDirty(tree);
            return node;
        }

        /// <summary>Duplicates node configuration while keeping entry and subtree references shared.</summary>
        /// <param name="tree">Detached graph receiving the duplicate.</param>
        /// <param name="node">Node to duplicate.</param>
        /// <returns>The independent node with a new key.</returns>
        internal static DialogueNode Duplicate(DialogueTree tree, DialogueNode node)
        {
            Undo.RecordObject(tree, "Duplicate Dialogue Node");
            DialogueNode copy = ObjectWorkspace.Copy(node);
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Position += new Vector2(35f, 45f);
            copy.Name += " Copy";
            ArrayUtility.Add(ref tree.Nodes, copy);
            EditorUtility.SetDirty(tree);
            return copy;
        }

        /// <summary>Removes one node and clears its incoming links without deleting referenced assets.</summary>
        /// <param name="tree">Detached graph proposal.</param>
        /// <param name="node">Node selected for removal.</param>
        internal static void Remove(DialogueTree tree, DialogueNode node)
        {
            Undo.RecordObject(tree, "Remove Dialogue Node");
            List<DialogueNode> nodes = new List<DialogueNode>(tree.Nodes);
            nodes.Remove(node);
            foreach (DialogueNode other in nodes)
            {
                if (other.Next == node.Id)
                    other.Next = string.Empty;
                if (other.Otherwise == node.Id)
                    other.Otherwise = string.Empty;
                foreach (DialogueBranch branch in other.Branches)
                    if (branch.Target == node.Id)
                        branch.Target = string.Empty;
            }
            tree.Nodes = nodes.ToArray();
            if (tree.Start == node.Id)
                tree.Start = string.Empty;
            EditorUtility.SetDirty(tree);
        }

        /// <summary>Commits one output connection after its destination is selected.</summary>
        /// <param name="tree">Detached graph proposal.</param>
        /// <param name="node">Source node.</param>
        /// <param name="port">Minus one for Next, minus two for Otherwise, or a branch index.</param>
        /// <param name="target">Destination key, or empty to disconnect.</param>
        internal static void Connect(DialogueTree tree, DialogueNode node, int port, string target)
        {
            // Undo may remove a source or branch while a connection menu is still open.
            if (node == null || port < -2 || port >= node.Branches.Length)
                return;
            Undo.RecordObject(tree, "Connect Dialogue Nodes");
            switch (port)
            {
                case -1:
                    node.Next = target;
                    break;
                case -2:
                    node.Otherwise = target;
                    break;
                default:
                    node.Branches[port].Target = target;
                    break;
            }
            EditorUtility.SetDirty(tree);
        }
        #endregion
        #endregion
    }
}
#endif
