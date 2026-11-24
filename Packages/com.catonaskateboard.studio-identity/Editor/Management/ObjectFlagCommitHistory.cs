using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Connects a complete multi-asset commit to one native Unity Undo step.</summary>
    [InitializeOnLoad]
    internal static class ObjectFlagCommitHistory
    {
        #region Types

        /// <summary>Retains the native Undo marker and its exact before and after states.</summary>
        private sealed class Entry
        {
            internal ObjectFlagHistoryState Marker;
            internal ObjectFlagCommitSnapshot Before;
            internal ObjectFlagCommitSnapshot After;
            internal bool Applied = true;
        }

        #endregion
        #region State

        private static readonly List<Entry> entries = new List<Entry>();

        #endregion
        #region Methods
        #region History

        /// <summary>Observes Undo once per editor domain, including when the tool window is closed.</summary>
        static ObjectFlagCommitHistory()
        {
            Undo.undoRedoPerformed += Restore;
        }

        /// <summary>Records one committed proposal without mixing it with previous draft edits.</summary>
        /// <param name="before">State before applying.</param>
        /// <param name="after">Successfully applied state.</param>
        internal static void Record(ObjectFlagCommitSnapshot before, ObjectFlagCommitSnapshot after)
        {
            ObjectFlagHistoryState marker = ScriptableObject.CreateInstance<ObjectFlagHistoryState>();
            marker.hideFlags = HideFlags.HideAndDontSave;
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(marker, "Apply identity flags");
            marker.Applied = true;
            Undo.FlushUndoRecordObjects();
            entries.Add(new Entry { Marker = marker, Before = before, After = after });
            Undo.IncrementCurrentGroup();
        }

        /// <summary>Restores only changed markers and rejects files modified outside their Undo history.</summary>
        private static void Restore()
        {
            ObjectFlagProject.UndoRestored = false;
            foreach (Entry entry in entries)
            {
                if (entry.Marker == null || entry.Marker.Applied == entry.Applied)
                    continue;
                ObjectFlagProject.UndoRestored = true;
                try
                {
                    if (!(entry.Applied ? entry.After : entry.Before).MatchesFiles())
                        throw new InvalidOperationException("Affected files changed outside the tool. Identity flag Undo was skipped.");
                    (entry.Marker.Applied ? entry.After : entry.Before).Restore();
                    entry.Applied = entry.Marker.Applied;
                }
                catch (Exception exception)
                {
                    entry.Marker.Applied = entry.Applied;
                    Debug.LogWarning(exception.Message);
                }
            }
        }

        #endregion
        #endregion
    }

    /// <summary>Stores the small native state restored by Unity's Undo stack.</summary>
    internal sealed class ObjectFlagHistoryState : ScriptableObject
    {
        #region Fields

        [Tooltip("Whether the associated flag commit is currently applied.")]
        public bool Applied;

        #endregion
    }
}
