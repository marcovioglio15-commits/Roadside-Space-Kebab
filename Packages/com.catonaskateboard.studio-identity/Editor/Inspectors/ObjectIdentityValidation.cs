using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Validates identity selections on the editor thread after asset and scene loading.</summary>
    [InitializeOnLoad]
    internal static class ObjectIdentityValidation
    {
        #region State

        private static readonly ConcurrentQueue<ObjectIdentity> pending = new ConcurrentQueue<ObjectIdentity>();
        private static readonly HashSet<ObjectIdentity> batch = new HashSet<ObjectIdentity>();

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Connects loading-thread requests to a single deferred editor validation pass.</summary>
        static ObjectIdentityValidation()
        {
            ObjectIdentity.ValidationRequested += Queue;
            EditorApplication.update += ValidatePending;
        }

        /// <summary>Retains a request without reading Unity objects from a loading thread.</summary>
        /// <param name="identity">Component whose serialized membership changed.</param>
        private static void Queue(ObjectIdentity identity)
        {
            pending.Enqueue(identity);
        }

        #endregion
        #region Validation

        /// <summary>Reports stable invalid selections once per queued component without modifying assets.</summary>
        internal static void ValidatePending()
        {
            // Imports and compilation can leave otherwise valid asset references temporarily unresolved.
            if (pending.IsEmpty || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            while (pending.TryDequeue(out ObjectIdentity identity))
                if (identity != null)
                    batch.Add(identity);
            try
            {
                foreach (ObjectIdentity identity in batch)
                    if (identity != null && !ObjectFlagRules.TryValidate(identity.AuthoredFlags, true, out string warning))
                    {
                        string path = EditorUtility.IsPersistent(identity) ? AssetDatabase.GetAssetPath(identity) : identity.gameObject.scene.path;
                        Debug.LogWarning((string.IsNullOrEmpty(path) ? identity.name : ObjectFlagLabels.Root(identity, path)) + ": " + warning, identity);
                    }
            }
            finally
            {
                batch.Clear();
            }
        }

        #endregion
        #endregion
    }
}
