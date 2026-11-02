using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Applies explicit tree edits with stable keys and native Undo.</summary>
    internal static class DialogueGraphEdits
    {
        #region Methods

        #region Mutation

        /// <summary>Creates a minimal Start block and explicit End block for a new sheet.</summary>
        /// <param name="tree">New unsaved tree.</param>
        internal static void Initialize(DialogueTree tree)
        {
            DialogueNode start = new DialogueNode { Id = Guid.NewGuid().ToString("N"), Name = "Dialogue" };
            DialogueNode end = new DialogueNode { Id = Guid.NewGuid().ToString("N"), Name = "End", End = true };
            tree.Nodes = new[] { start, end };
            tree.Start = start.Id;
        }

        /// <summary>Adds one block without disturbing existing links or selection history.</summary>
        /// <param name="tree">Detached sheet.</param>
        /// <param name="position">Optional pointer position in canvas coordinates.</param>
        /// <returns>The newly added block.</returns>
        internal static DialogueNode Add(DialogueTree tree, Vector2? position = null)
        {
            Undo.RecordObject(tree, "Add dialogue block");
            DialogueNode block = new DialogueNode { Id = Guid.NewGuid().ToString("N"), Name = "New Block" };
            if (position.HasValue)
            {
                block.Position = position.Value;
                block.HasPosition = true;
            }
            List<DialogueNode> nodes = new List<DialogueNode>(tree.Nodes) { block };
            tree.Nodes = nodes.ToArray();
            EditorUtility.SetDirty(tree);
            return block;
        }

        /// <summary>Adds an entry directly beneath its containing block.</summary>
        /// <param name="tree">Detached sheet.</param>
        /// <param name="block">Block receiving the row.</param>
        /// <returns>The new row ready for asset and trigger selection.</returns>
        internal static DialogueRow AddEntry(DialogueTree tree, DialogueNode block)
        {
            Undo.RecordObject(tree, "Add dialogue entry");
            DialogueNode end = Array.Find(tree.Nodes, candidate => candidate.End);
            DialogueRow row = new DialogueRow { Id = Guid.NewGuid().ToString("N"), Name = "New Entry", Next = end?.Id ?? string.Empty };
            List<DialogueRow> entries = new List<DialogueRow>(block.Entries) { row };
            block.Entries = entries.ToArray();
            EditorUtility.SetDirty(tree);
            return row;
        }

        /// <summary>Connects a row by internal keys, keeping displayed names freely editable.</summary>
        /// <param name="tree">Detached sheet.</param>
        /// <param name="row">Source entry.</param>
        /// <param name="destination">Target block key.</param>
        internal static void Connect(DialogueTree tree, DialogueRow row, string destination)
        {
            Undo.RecordObject(tree, "Connect dialogue entry");
            row.Next = destination;
            EditorUtility.SetDirty(tree);
        }

        /// <summary>Removes the selected row or block while preserving dangling links for explicit correction.</summary>
        /// <param name="tree">Detached sheet.</param>
        /// <param name="blockId">Selected block key.</param>
        /// <param name="rowId">Optional selected row key.</param>
        internal static void Remove(DialogueTree tree, string blockId, string rowId)
        {
            DialogueNode block = Array.Find(tree.Nodes, candidate => candidate.Id == blockId);
            if (block == null)
                return;
            Undo.RecordObject(tree, "Remove dialogue selection");
            if (!string.IsNullOrEmpty(rowId))
                block.Entries = Array.FindAll(block.Entries, row => row.Id != rowId);
            else
                tree.Nodes = Array.FindAll(tree.Nodes, node => node != block);
            EditorUtility.SetDirty(tree);
        }

        #endregion

        #endregion
    }
}
