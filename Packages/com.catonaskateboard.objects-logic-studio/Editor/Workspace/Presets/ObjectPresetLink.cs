using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Stores asset references directly and describes object-local references as importable slots.</summary>
    [Serializable]
    internal sealed class ObjectPresetLink
    {
        #region Fields

        [Header("Reference")]
        [Tooltip("Referenced asset, if this is an asset link.")]
        public UnityEngine.Object Asset;
        [Tooltip("Whether this link points inside the source object.")]
        public bool Local;
        [Tooltip("Whether a scene connection must be chosen on import.")]
        public bool External;
        [Tooltip("Object path inside the source hierarchy.")]
        public string Path = string.Empty;
        [Tooltip("Component type used by this slot.")]
        public string TypeName = string.Empty;
        [Tooltip("Which same-type component was selected.")]
        public int Index;
        [Tooltip("Number of same-type components when captured.")]
        public int Count;
        [Tooltip("Source prefab identifier.")]
        public string SourceGuid = string.Empty;
        [Tooltip("Source object identifier.")]
        public long FileId;
        [Tooltip("Name shown when a scene connection needs replacing.")]
        public string Label = string.Empty;

        #endregion

        #region Properties

        /// <summary>Stable key shared by repeated references to the same source slot.</summary>
        internal string Key => SourceGuid + ":" + FileId + ":" + Path + ":" + TypeName + ":" + Index;

        #endregion

        #region Methods

        #region Capture

        /// <summary>Separates reusable assets from local hierarchy and scene connections.</summary>
        /// <param name="value">Source field reference.</param>
        /// <param name="root">Root of the configuration being exported.</param>
        /// <returns>A durable link that does not retain a scene instance in a preset asset.</returns>
        internal static ObjectPresetLink Capture(UnityEngine.Object value, Transform root)
        {
            ObjectPresetLink link = new ObjectPresetLink();
            if (value == null)
                return link;
            Transform transform = value is GameObject gameObject ? gameObject.transform : value is Component component ? component.transform : null;
            if (transform != null && root != null && transform.IsChildOf(root))
            {
                link.Local = true;
                link.Path = AnimationUtility.CalculateTransformPath(transform, root);
                link.TypeName = value.GetType().AssemblyQualifiedName;
                link.FileId = ObjectWorkspaceTarget.FileId(value);
                ObjectWorkspaceTarget target = new ObjectWorkspaceTarget();
                target.Capture(root.gameObject, 0);
                link.SourceGuid = target.PrefabGuid;
                if (value is Component local)
                {
                    Component[] siblings = transform.GetComponents(value.GetType());
                    link.Index = Array.IndexOf(siblings, local);
                    link.Count = siblings.Length;
                }
                return link;
            }
            if (EditorUtility.IsPersistent(value))
            {
                link.Asset = value;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out link.SourceGuid, out link.FileId);
                link.TypeName = value.GetType().AssemblyQualifiedName;
            }
            else
            {
                link.External = true;
                link.TypeName = value.GetType().AssemblyQualifiedName;
                link.Label = value.name;
                link.SourceGuid = GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
            }
            return link;
        }

        #endregion

        #region Resolution

        /// <summary>Resolves a local slot only when the hierarchy and component choice are unambiguous.</summary>
        /// <param name="root">Destination configuration root.</param>
        /// <returns>The matching object, asset, or null for a slot requiring a choice.</returns>
        internal UnityEngine.Object Resolve(Transform root)
        {
            if (!Local)
                return External ? null : Asset;
            if (root == null)
                return null;
            ObjectWorkspaceTarget target = new ObjectWorkspaceTarget();
            target.Capture(root.gameObject, 0);
            // Reopening the same prefab keeps exact file identities even if siblings were reordered.
            if (FileId != 0 && target.PrefabGuid == SourceGuid)
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (ObjectWorkspaceTarget.FileId(child.gameObject) == FileId)
                        return child.gameObject;
                    foreach (Component component in child.GetComponents<Component>())
                        if (component != null && ObjectWorkspaceTarget.FileId(component) == FileId)
                            return component;
                }
            Transform selected = ObjectPresetHierarchy.Find(root, Path);
            return ResolveAt(selected);
        }

        /// <summary>Resolves the exact object or component after an import preview maps its hierarchy slot.</summary>
        /// <param name="selected">Mapped destination transform.</param>
        /// <returns>The matching reference, or null for an ambiguous component count.</returns>
        internal UnityEngine.Object ResolveAt(Transform selected)
        {
            Type type = Type.GetType(TypeName, false);
            if (selected == null || type == null)
                return null;
            if (type == typeof(GameObject))
                return selected.gameObject;
            Component[] components = selected.GetComponents(type);
            return components.Length == Count && Index >= 0 && Index < components.Length ? components[Index] : null;
        }

        #endregion

        #endregion
    }
}
