using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Finds short free-space routes through openings using the attracted body's compound geometry.</summary>
    internal sealed class BlackHolePath
    {
        #region State

        /// <summary>Stores one visited lattice position and its cheapest known predecessor.</summary>
        private struct Node
        {
            internal Vector3Int Cell;
            internal int Parent;
            internal float Cost;
            internal float Score;
            internal bool Closed;
            internal int Slot;
        }

        private readonly BlackHoleObstacleMotion motion;
        private readonly Dictionary<Vector3Int, int> visited = new Dictionary<Vector3Int, int>();
        private readonly List<Node> nodes = new List<Node>();
        private readonly List<int> open = new List<int>();
        private readonly List<Vector3> points = new List<Vector3>();
        private Vector3 origin;
        private Vector3 destination;
        private Quaternion rotation;
        private float spacing;

        #endregion
        #region Properties

        /// <summary>Whether a verified route still contains an intermediate point.</summary>
        internal bool Pending => points.Count > 0;

        #endregion
        #region Methods
        #region Search

        /// <summary>Retains the shape-query owner while reusing search storage across replans.</summary>
        /// <param name="motion">Compound geometry and collision filtering for this body.</param>
        internal BlackHolePath(BlackHoleObstacleMotion motion)
        {
            this.motion = motion;
        }

        /// <summary>Searches actual free space instead of treating a mesh's enclosing bounds as a solid wall.</summary>
        /// <param name="pose">Current body pose; route translations retain this orientation.</param>
        /// <param name="target">Desired body-origin destination.</param>
        /// <param name="step">Distance between neighboring search positions in metres.</param>
        /// <returns>True when a complete route to the destination was found.</returns>
        internal bool Build(Pose pose, Vector3 target, float step)
        {
            // Storage is retained; a bounded search cannot freeze a frame on sealed or unreachable geometry.
            Clear();
            visited.Clear();
            nodes.Clear();
            open.Clear();
            origin = pose.position;
            rotation = pose.rotation;
            destination = target;
            spacing = step;
            Add(Vector3Int.zero, -1, 0f);
            for (int expansion = 0; expansion < 768 && open.Count > 0; expansion++)
            {
                int index = Take();
                Node current = nodes[index];
                if (current.Closed)
                    continue;
                current.Closed = true;
                nodes[index] = current;
                Vector3 position = Position(current.Cell);
                if (motion.Clear(new Pose(position, rotation), destination - position))
                {
                    Trace(index);
                    return true;
                }
                // Axis neighbors cannot cut diagonally through a corner or cross the back of a shelf.
                for (int axis = 0; axis < 6; axis++)
                {
                    Vector3Int offset = axis switch
                    {
                        0 => Vector3Int.up, 1 => Vector3Int.down, 2 => Vector3Int.right,
                        3 => Vector3Int.left, 4 => Vector3Int.forward, _ => Vector3Int.back
                    };
                    Vector3Int cell = current.Cell + offset;
                    float cost = current.Cost + spacing;
                    if (visited.TryGetValue(cell, out int known) && nodes[known].Cost <= cost)
                        continue;
                    if (!motion.Clear(new Pose(position, rotation), (Vector3)offset * spacing))
                        continue;
                    Add(cell, index, cost);
                }
            }
            return false;
        }

        /// <summary>Adds or improves a reachable position in the priority queue.</summary>
        /// <param name="cell">Lattice coordinate relative to the acquisition pose.</param>
        /// <param name="parent">Predecessor index, or minus one for the start.</param>
        /// <param name="cost">Accumulated travel in metres.</param>
        private void Add(Vector3Int cell, int parent, float cost)
        {
            Node node = new Node
            {
                Cell = cell, Parent = parent, Cost = cost, Slot = open.Count,
                Score = cost + Vector3.Distance(Position(cell), destination) * 1.15f
            };
            if (!visited.TryGetValue(cell, out int index))
            {
                index = nodes.Count;
                visited.Add(cell, index);
                nodes.Add(node);
            }
            else
            {
                node.Slot = nodes[index].Closed ? open.Count : nodes[index].Slot;
                nodes[index] = node;
            }
            // A small binary heap avoids rescanning the complete frontier for every expansion.
            int slot = node.Slot;
            if (slot == open.Count)
                open.Add(index);
            while (slot > 0)
            {
                int parentSlot = (slot - 1) / 2;
                if (nodes[open[parentSlot]].Score <= node.Score)
                    break;
                open[slot] = open[parentSlot];
                SetSlot(open[slot], slot);
                slot = parentSlot;
            }
            open[slot] = index;
            SetSlot(index, slot);
        }

        /// <summary>Removes the cheapest queued position and restores heap ordering.</summary>
        /// <returns>Index into the retained node list.</returns>
        private int Take()
        {
            int result = open[0];
            int last = open[open.Count - 1];
            open.RemoveAt(open.Count - 1);
            if (open.Count == 0)
                return result;
            int slot = 0;
            while (slot * 2 + 1 < open.Count)
            {
                int child = slot * 2 + 1;
                if (child + 1 < open.Count && nodes[open[child + 1]].Score < nodes[open[child]].Score)
                    child++;
                if (nodes[last].Score <= nodes[open[child]].Score)
                    break;
                open[slot] = open[child];
                SetSlot(open[slot], slot);
                slot = child;
            }
            open[slot] = last;
            SetSlot(last, slot);
            return result;
        }

        /// <summary>Tracks queue indices so improving a route updates its existing heap entry.</summary>
        /// <param name="index">Node whose entry moved.</param>
        /// <param name="slot">Current heap position.</param>
        private void SetSlot(int index, int slot)
        {
            Node node = nodes[index];
            node.Slot = slot;
            nodes[index] = node;
        }

        /// <summary>Converts one lattice cell into a virtual body position.</summary>
        /// <param name="cell">Relative lattice coordinate.</param>
        /// <returns>World-space body origin.</returns>
        private Vector3 Position(Vector3Int cell)
        {
            return origin + (Vector3)cell * spacing;
        }

        /// <summary>Stores predecessors in reverse so consuming a waypoint does not shift the list.</summary>
        /// <param name="index">Last reachable node with a clear segment to the destination.</param>
        private void Trace(int index)
        {
            points.Add(destination);
            while (index > 0)
            {
                points.Add(Position(nodes[index].Cell));
                index = nodes[index].Parent;
            }
        }

        #endregion
        #region Following

        /// <summary>Skips intermediate nodes only when the body's complete shape can follow the shortcut.</summary>
        /// <param name="pose">Current physical pose.</param>
        /// <param name="fallback">Destination used after the route has been consumed.</param>
        /// <param name="shortcuts">Whether this scheduled refresh may perform visibility sweeps.</param>
        /// <returns>Next reachable body-origin waypoint.</returns>
        internal Vector3 Target(Pose pose, Vector3 fallback, bool shortcuts)
        {
            // Smoothing uses collision sweeps, preserving the narrow passage discovered by the search.
            while (shortcuts && points.Count > 1 && motion.Clear(pose, points[points.Count - 2] - pose.position))
                points.RemoveAt(points.Count - 1);
            if (points.Count > 0 && (points[points.Count - 1] - pose.position).sqrMagnitude < 0.0025f)
                points.RemoveAt(points.Count - 1);
            return points.Count > 0 ? points[points.Count - 1] : fallback;
        }

        /// <summary>Discards stale waypoints after a destination change or restored direct route.</summary>
        internal void Clear()
        {
            points.Clear();
        }

        #endregion
        #endregion
    }
}
