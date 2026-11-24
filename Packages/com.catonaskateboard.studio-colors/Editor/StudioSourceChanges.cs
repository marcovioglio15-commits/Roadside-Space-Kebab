using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Notifies detached Studio editors after explicit source writes and completed Undo restoration.</summary>
    public static class StudioSourceChanges
    {
        #region State

        private static bool pending;

        #endregion
        #region Events

        /// <summary>Refreshes clean editor proposals while pending proposals retain their conflict baselines.</summary>
        public static event Action Changed;

        #endregion
        #region Methods
        #region Notifications

        /// <summary>Coalesces source notifications until the current transaction has completed.</summary>
        public static void Notify()
        {
            if (pending)
                return;
            pending = true;
            EditorApplication.delayCall += Publish;
        }

        /// <summary>Refreshes each editor independently so a failed subscriber cannot interrupt a completed save.</summary>
        private static void Publish()
        {
            pending = false;
            if (Changed == null)
                return;
            foreach (Action changed in Changed.GetInvocationList())
                try
                {
                    changed();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Studio source refresh failed: " + exception.Message);
                }
        }

        #endregion
        #endregion
    }
}
