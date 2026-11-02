using System;
using System.Text;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores a group-only settings patch prepared in editor for reflection-free runtime imports.</summary>
    [Serializable]
    public sealed class InteractionGroupPatch
    {
        #region Fields

        [Header("Compiled Group")]
        [Tooltip("Interaction family accepted by the source group.")]
        [HideInInspector]
        public InteractionRuntimeKind Kind;
        [Tooltip("Source group name shown in runtime warnings.")]
        [HideInInspector]
        public string Name = string.Empty;
        [Tooltip("Native JSON fragments separated by portable object references.")]
        [HideInInspector]
        public string[] Fragments = Array.Empty<string>();
        [Tooltip("Asset or local references inserted between the saved fragments.")]
        [HideInInspector]
        public InteractionGroupReference[] References = Array.Empty<InteractionGroupReference>();

        #endregion

        #region Methods

        #region Import

        /// <summary>Checks a compiled group before an operation enters its preparation phase.</summary>
        /// <param name="kind">Destination interaction family.</param>
        /// <returns>True when fragments and references form a complete patch.</returns>
        internal bool Accepts(InteractionRuntimeKind kind)
        {
            // Empty or stale editor data must never become a successful runtime import.
            return kind == Kind && Fragments != null && References != null && Fragments.Length == References.Length + 1
                && !string.IsNullOrEmpty(Fragments[0]);
        }

        /// <summary>Resolves every reference before writing the group's selected fields.</summary>
        /// <param name="feature">Instance receiving this group.</param>
        /// <param name="warning">Receives an incompatible group or missing local slot.</param>
        /// <returns>True when only this group's settings were copied.</returns>
        internal bool Apply(ObjectInteraction feature, out string warning)
        {
            warning = "Choose a matching group preset: " + Name;
            if (InteractionRuntimeCatalog.KindOf(feature) is not InteractionRuntimeKind kind || !Accepts(kind))
                return false;
            StringBuilder json = new StringBuilder(Fragments[0]);
            // Native serialization supplies current object identities, including after prefab instantiation.
            for (int index = 0; index < References.Length; index++)
            {
                UnityEngine.Object value = References[index].Resolve(feature.transform);
                if (value == null)
                {
                    warning = Name + ": a local object reference is missing on the affected item.";
                    return false;
                }
                string reference = JsonUtility.ToJson(new InteractionGroupObject { Value = value });
                json.Append(reference, 9, reference.Length - 10);
                json.Append(Fragments[index + 1]);
            }
            JsonUtility.FromJsonOverwrite(json.ToString(), feature);
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }

    /// <summary>Uses Unity's native object-reference JSON without storing session-specific IDs in assets.</summary>
    [Serializable]
    internal sealed class InteractionGroupObject
    {
        #region Fields

        [Tooltip("Object inserted into one compiled group reference slot.")]
        public UnityEngine.Object Value;

        #endregion
    }
}
