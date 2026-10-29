using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Gives unsaved editor preview components distinct temporary IDs without changing prefab identities.</summary>
    internal static class ObjectPreviewIdentity
    {
        #region State

        private static readonly Dictionary<ulong, long> identities = new Dictionary<ulong, long>();

        #endregion

        #region Methods

        #region Registration

        /// <summary>Registers only objects that do not already have a saved prefab identity.</summary>
        /// <param name="root">Explicit preview or import transaction root.</param>
        internal static void Register(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                RegisterObject(transform.gameObject);
                foreach (Component component in transform.GetComponents<Component>())
                    if (component != null)
                        RegisterObject(component);
            }
        }

        /// <summary>Retains a negative editor-instance identity only for an unsaved object.</summary>
        /// <param name="value">Object participating in the explicit preview.</param>
        private static void RegisterObject(UnityEngine.Object value)
        {
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(value);
            if (id.targetObjectId == 0 && !EditorUtility.IsPersistent(value))
                identities[EntityId.ToULong(value.GetEntityId())] = -System.Math.Abs(unchecked((long)EntityId.ToULong(value.GetEntityId())));
        }

        /// <summary>Removes temporary identities after a preview is closed or an import finishes.</summary>
        /// <param name="root">Registered preview or transaction root.</param>
        internal static void Unregister(GameObject root)
        {
            if (root == null)
                return;
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                identities.Remove(EntityId.ToULong(transform.gameObject.GetEntityId()));
                foreach (Component component in transform.GetComponents<Component>())
                    if (component != null)
                        identities.Remove(EntityId.ToULong(component.GetEntityId()));
            }
        }

        /// <summary>Reads an identity only for objects explicitly enrolled in editor preview work.</summary>
        /// <param name="value">Object being resolved.</param>
        /// <param name="identity">Receives the temporary negative ID.</param>
        /// <returns>True when a temporary identity exists.</returns>
        internal static bool TryGet(UnityEngine.Object value, out long identity)
        {
            identity = 0;
            return value != null && identities.TryGetValue(EntityId.ToULong(value.GetEntityId()), out identity);
        }

        #endregion

        #endregion
    }
}
