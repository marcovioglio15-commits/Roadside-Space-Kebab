using System;
using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Observable execution boundaries of a single dialogue session.</summary>
    public enum DialogueRunState { Idle, Page, Waiting, Completed, Failed }

    /// <summary>Executes connected entries with bounded traversal, local selection cursors and nested tree calls.</summary>
    public sealed class DialogueRunner
    {
        #region State
        private readonly DialogueTreePlan root;
        private readonly Dictionary<DialogueNode, int> cursors = new Dictionary<DialogueNode, int>();
        private readonly Dictionary<DialogueNode, int> initialCursors = new Dictionary<DialogueNode, int>();
        private readonly Stack<(DialogueTreePlan Plan, string Next)> stack = new Stack<(DialogueTreePlan, string)>();
        private readonly HashSet<DialogueNode> visited = new HashSet<DialogueNode>();
        private readonly List<int> eligible = new List<int>();
        private DialogueTreePlan plan;
        private DialogueContext context;
        private DialogueNode node;
        private Random random;
        private int seed;
        private int page;
        private float remaining;
        #endregion
        #region Properties
        /// <summary>Top-level tree retained for this runner's lifetime.</summary>
        public DialogueTree Root => root.Tree;
        /// <summary>Tree currently executing, including a nested subtree.</summary>
        public DialogueTree CurrentTree => plan?.Tree;
        /// <summary>Stable current node key for editor highlighting.</summary>
        public string CurrentNode => node?.Id;
        /// <summary>Current input, timer or terminal boundary.</summary>
        public DialogueRunState State { get; private set; }
        /// <summary>Current page, or null while waiting or after termination.</summary>
        public DialogueLine Line => State == DialogueRunState.Page ? node.Entry.Lines[page] : null;
        /// <summary>Changes only at graph or page boundaries.</summary>
        public int Revision { get; private set; }
        /// <summary>Diagnostic retained when invalid control flow exceeds the runtime safety budget.</summary>
        public string Error { get; private set; } = string.Empty;
        #endregion
        #region Methods
        #region Session
        /// <summary>Indexes a validated graph once without touching its assets.</summary>
        /// <param name="tree">Validated root tree.</param>
        /// <param name="seed">Initial local random seed; global Unity randomness is untouched.</param>
        public DialogueRunner(DialogueTree tree, int seed = 1)
        {
            root = new DialogueTreePlan(tree);
            this.seed = seed;
        }

        /// <summary>Checks eligibility without advancing sequence cursors or sampling random choices.</summary>
        /// <param name="snapshot">Captured request predicates.</param>
        /// <returns>True when a displayable entry is reachable.</returns>
        public bool CanStart(DialogueContext snapshot)
        {
            visited.Clear();
            return root.CanEnter(root.Tree.Start, snapshot, visited);
        }

        /// <summary>Starts a new request and retains its initial cursor and random state for Restart.</summary>
        /// <param name="snapshot">Immutable eligibility for the entire conversation.</param>
        public void Start(DialogueContext snapshot)
        {
            context = snapshot;
            initialCursors.Clear();
            foreach (KeyValuePair<DialogueNode, int> cursor in cursors)
                initialCursors.Add(cursor.Key, cursor.Value);
            seed = unchecked(seed * 1664525 + 1013904223);
            Restart();
        }

        /// <summary>Restarts the same request with the same random choices and pre-request sequence cursors.</summary>
        public void Restart()
        {
            if (context == null)
                return;
            cursors.Clear();
            foreach (KeyValuePair<DialogueNode, int> cursor in initialCursors)
                cursors.Add(cursor.Key, cursor.Value);
            random = new Random(seed);
            stack.Clear();
            plan = root;
            Error = string.Empty;
            Move(root.Tree.Start);
        }

        /// <summary>Consumes exactly one advance press at a visible page boundary.</summary>
        public void Advance()
        {
            if (State != DialogueRunState.Page)
                return;
            if (++page < node.Entry.Lines.Length)
                Revision++;
            else
                Move(node.Next);
        }

        /// <summary>Advances an active wait using scaled time supplied by the owner or preview.</summary>
        /// <param name="delta">Non-negative elapsed seconds.</param>
        public void Tick(float delta)
        {
            if (State != DialogueRunState.Waiting || !float.IsFinite(delta) || delta <= 0f)
                return;
            remaining -= delta;
            if (remaining <= 0f)
                Move(node.Next);
        }
        #endregion
        #region Execution
        /// <summary>Runs immediate nodes until reaching one page, timer or terminal boundary.</summary>
        /// <param name="id">Destination key in the current tree.</param>
        private void Move(string id)
        {
            // Entry filters can skip nodes, so runtime protection also covers otherwise valid filtered loops.
            for (int step = 0; step < 256; step++)
            {
                Revision++;
                if (string.IsNullOrEmpty(id))
                {
                    if (stack.Count > 0)
                    {
                        (plan, id) = stack.Pop();
                        continue;
                    }
                    State = DialogueRunState.Completed;
                    return;
                }
                node = plan.Nodes[id];
                switch (node.Kind)
                {
                    case DialogueNodeKind.Entry:
                        if (context.Matches(node.Entry))
                        {
                            page = 0;
                            State = DialogueRunState.Page;
                            return;
                        }
                        id = node.Next;
                        break;
                    case DialogueNodeKind.Condition:
                        id = context.Matches(node.Condition) ? node.Next : node.Otherwise;
                        break;
                    case DialogueNodeKind.Select:
                        id = Choose();
                        break;
                    case DialogueNodeKind.Wait:
                        remaining = node.Duration;
                        State = DialogueRunState.Waiting;
                        return;
                    case DialogueNodeKind.Subtree:
                        stack.Push((plan, node.Next));
                        plan = plan.Dependencies[node.Subtree];
                        id = plan.Tree.Start;
                        break;
                    case DialogueNodeKind.End:
                        if (stack.Count == 0)
                        {
                            State = DialogueRunState.Completed;
                            return;
                        }
                        (plan, id) = stack.Pop();
                        break;
                }
            }
            State = DialogueRunState.Failed;
            Error = "Dialogue exceeded 256 immediate node transitions. Check filtered loops and add an input or wait boundary.";
        }

        /// <summary>Selects only branches that can reach an eligible page, without allocating per rendered frame.</summary>
        /// <returns>The chosen destination or the authored fallback.</returns>
        private string Choose()
        {
            eligible.Clear();
            double total = 0d;
            for (int index = 0; index < node.Branches.Length; index++)
            {
                DialogueBranch branch = node.Branches[index];
                visited.Clear();
                if (!context.Matches(branch.Condition) || node.Selection == DialogueSelection.WeightedRandom && plan.Weight(branch) <= 0d
                    || !plan.CanEnter(branch.Target, context, visited))
                    continue;
                eligible.Add(index);
                total += plan.Weight(branch);
            }
            if (eligible.Count == 0)
                return node.Otherwise;
            switch (node.Selection)
            {
                case DialogueSelection.Sequence:
                    cursors.TryGetValue(node, out int next);
                    for (int offset = 0; offset < node.Branches.Length; offset++)
                    {
                        int candidate = (next + offset) % node.Branches.Length;
                        if (!eligible.Contains(candidate))
                            continue;
                        cursors[node] = (candidate + 1) % node.Branches.Length;
                        return node.Branches[candidate].Target;
                    }
                    break;
                case DialogueSelection.Random:
                    return node.Branches[eligible[random.Next(eligible.Count)]].Target;
                case DialogueSelection.WeightedRandom:
                    double draw = random.NextDouble() * total;
                    foreach (int index in eligible)
                    {
                        draw -= plan.Weight(node.Branches[index]);
                        if (draw < 0d)
                            return node.Branches[index].Target;
                    }
                    break;
            }
            return node.Branches[eligible[eligible.Count - 1]].Target;
        }
        #endregion
        #endregion
    }
}
