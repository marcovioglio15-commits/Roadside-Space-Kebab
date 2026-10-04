using CatOnASkateboard.StudioIdentity.Editor;
using CatOnASkateboard.StudioColors.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Saves delayed flag selections made inside the recoverable object workspace.</summary>
    [InitializeOnLoad]
    internal static class ObjectFlagWorkspaceBridge
    {
        #region Methods

        #region Persistence

        /// <summary>Connects popup completion to workspace persistence after editor reload.</summary>
        static ObjectFlagWorkspaceBridge()
        {
            // Ordinary scene and asset fields keep Unity's native serialization behavior.
            StudioFieldMenu.Changed += Persist;
            StudioFieldMenu.CollectGuards += CaptureGuard;
            ObjectFlagSelector.SelectionChanged += Persist;
            ObjectFlagSelector.CollectSelectionGuards += CaptureGuard;
        }

        /// <summary>Persists a workspace after the selector has applied its serialized change.</summary>
        /// <param name="owner">Object modified by the popup.</param>
        private static void Persist(UnityEngine.Object owner)
        {
            // Popup changes happen after the workspace's normal GUI transaction has finished.
            if (owner is ObjectWorkspace workspace)
                workspace.Persist();
        }

        /// <summary>Keeps a delayed flag creation attached to the card that originally opened it.</summary>
        /// <param name="owner">Workspace or native asset being edited.</param>
        /// <param name="path">Serialized field awaiting a flag choice.</param>
        /// <param name="guards">Checks evaluated before the delayed assignment.</param>
        private static void CaptureGuard(UnityEngine.Object owner, string path, List<Func<bool>> guards)
        {
            // Native component fields have stable owners; drafts also need their navigation identity.
            if (owner is not ObjectWorkspace workspace)
                return;
            string context = Context(workspace, path);
            guards.Add(() => Context(workspace, path) == context);
        }

        /// <summary>Identifies the selected observer or prefab card without including incidental layout state.</summary>
        /// <param name="workspace">Recoverable tool state.</param>
        /// <param name="path">Field whose owner determines the context.</param>
        /// <returns>A stable navigation key for this delayed selection.</returns>
        private static string Context(ObjectWorkspace workspace, string path)
        {
            // Scroll, foldouts and ordinary field edits do not invalidate the active selection.
            if (path.StartsWith("Observer.", StringComparison.Ordinal))
                return workspace.Observer.ObserverId + "|" + workspace.Observer.CameraId + "|" + workspace.Observer.PlayerId;
            return JsonUtility.ToJson(workspace.Target) + "|" + workspace.Category + "|" + workspace.Single.Kind
                + "|" + workspace.Extended.Kind + "|" + workspace.Extended.ComponentId;
        }

        #endregion

        #endregion
    }
}
