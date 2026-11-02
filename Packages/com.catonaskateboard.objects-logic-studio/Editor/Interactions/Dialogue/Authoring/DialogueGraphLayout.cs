using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Places conversation blocks in deterministic columns with no overlaps.</summary>
    internal sealed class DialogueGraphLayout
    {
        #region State

        internal const float Width = 300f;
        internal const float RowHeight = 72f;
        internal readonly Dictionary<string, Rect> Blocks = new Dictionary<string, Rect>();
        private readonly Dictionary<string, DialogueNode> nodes = new Dictionary<string, DialogueNode>();
        private readonly Dictionary<string, int> columns = new Dictionary<string, int>();
        private readonly Queue<string> pending = new Queue<string>();
        private readonly Dictionary<int, float> heights = new Dictionary<int, float>();
        internal Vector2 Size { get; private set; }
        internal Rect Bounds { get; private set; }
        internal Rect StartRect { get; private set; }

        #endregion

        #region Methods

        #region Arrangement

        /// <summary>Uses first-reached graph depth for columns and array order for vertical peers.</summary>
        /// <param name="tree">Sheet to arrange without changing its settings.</param>
        internal void Build(DialogueTree tree)
        {
            Blocks.Clear();
            nodes.Clear();
            columns.Clear();
            pending.Clear();
            heights.Clear();
            foreach (DialogueNode block in tree.Nodes)
                if (block != null && !string.IsNullOrEmpty(block.Id))
                    nodes[block.Id] = block;
            if (nodes.ContainsKey(tree.Start))
            {
                columns[tree.Start] = 0;
                pending.Enqueue(tree.Start);
            }
            // Back edges never move an already placed block or create a layout cycle.
            while (pending.Count > 0)
            {
                string id = pending.Dequeue();
                foreach (DialogueRow row in nodes[id].Entries)
                    if (row != null && nodes.ContainsKey(row.Next) && !columns.ContainsKey(row.Next))
                    {
                        columns[row.Next] = columns[id] + 1;
                        pending.Enqueue(row.Next);
                    }
            }
            Size = Vector2.zero;
            StartRect = new Rect(tree.HasStartPosition ? tree.StartPosition : new Vector2(18f, 40f), new Vector2(90f, 32f));
            Bounds = StartRect;
            foreach (DialogueNode block in tree.Nodes)
            {
                if (block == null || string.IsNullOrEmpty(block.Id))
                    continue;
                int column = columns.TryGetValue(block.Id, out int depth) ? depth : 0;
                float top = heights.TryGetValue(column, out float previous) ? previous : 40f;
                float height = block.End ? 56f : 88f + (block.Entries?.Length ?? 0) * RowHeight;
                Rect rect = new Rect(150f + column * 410f, top, Width, height);
                if (block.HasPosition)
                    rect.position = block.Position;
                Blocks[block.Id] = rect;
                heights[column] = rect.yMax + 70f;
                Size = Vector2.Max(Size, rect.max + new Vector2(50f, 30f));
                Bounds = Rect.MinMaxRect(Mathf.Min(Bounds.xMin, rect.xMin), Mathf.Min(Bounds.yMin, rect.yMin),
                    Mathf.Max(Bounds.xMax, rect.xMax), Mathf.Max(Bounds.yMax, rect.yMax));
            }
        }

        /// <summary>Returns one indented row rectangle inside its parent block.</summary>
        /// <param name="block">Parent block rectangle.</param>
        /// <param name="index">Zero-based entry position.</param>
        /// <returns>Unscaled entry row rectangle.</returns>
        internal static Rect Row(Rect block, int index)
        {
            return new Rect(block.x + 24f, block.y + 50f + index * RowHeight, block.width - 36f, RowHeight - 8f);
        }

        #endregion

        #endregion
    }
}
