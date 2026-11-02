using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Identifies supported local component slots through compiled Unity types.</summary>
    public enum InteractionGroupObjectKind
    {
        Asset, GameObject, Transform, RectTransform, Interaction, BoxCollider, SphereCollider, CapsuleCollider, MeshCollider,
        MeshRenderer, SkinnedMeshRenderer, SpriteRenderer, Rigidbody, AudioSource, Camera, Light, Animator, ParticleSystem, Canvas, CanvasGroup
    }

    /// <summary>Retains an asset or an exact local component slot without runtime type lookup.</summary>
    [Serializable]
    public sealed class InteractionGroupReference
    {
        #region Fields

        [Header("Compiled Reference")]
        [Tooltip("Direct asset link, independent of the current Play session.")]
        [HideInInspector]
        public UnityEngine.Object Asset;
        [Tooltip("Compiled Unity object or interaction type.")]
        [HideInInspector]
        public InteractionGroupObjectKind Kind;
        [Tooltip("Interaction family when this slot targets an interaction.")]
        [HideInInspector]
        public InteractionRuntimeKind Interaction;
        [Tooltip("Path relative to the affected object's hierarchy root.")]
        [HideInInspector]
        public string Path = string.Empty;
        [Tooltip("Index among components of the selected type.")]
        [HideInInspector]
        public int Index;
        [Tooltip("Expected component count. Ambiguous hierarchies reject the import.")]
        [HideInInspector]
        public int Count;

        #endregion

        #region Methods

        #region Resolution

        /// <summary>Finds this slot on a runtime hierarchy without reading type names.</summary>
        /// <param name="root">Affected hierarchy root.</param>
        /// <returns>The exact asset or local object, or null for an incompatible hierarchy.</returns>
        internal UnityEngine.Object Resolve(Transform root)
        {
            // Assets retain their native reference; local slots are resolved for each affected instance.
            if (Kind == InteractionGroupObjectKind.Asset)
                return Asset;
            Transform branch = string.IsNullOrEmpty(Path) || Path == "." ? root : root.Find(Path);
            if (branch == null)
                return null;
            return Kind switch
            {
                InteractionGroupObjectKind.GameObject => branch.gameObject,
                InteractionGroupObjectKind.Transform => branch,
                InteractionGroupObjectKind.RectTransform => branch as RectTransform,
                InteractionGroupObjectKind.Interaction => ResolveInteraction(branch),
                InteractionGroupObjectKind.BoxCollider => Component<BoxCollider>(branch),
                InteractionGroupObjectKind.SphereCollider => Component<SphereCollider>(branch),
                InteractionGroupObjectKind.CapsuleCollider => Component<CapsuleCollider>(branch),
                InteractionGroupObjectKind.MeshCollider => Component<MeshCollider>(branch),
                InteractionGroupObjectKind.MeshRenderer => Component<MeshRenderer>(branch),
                InteractionGroupObjectKind.SkinnedMeshRenderer => Component<SkinnedMeshRenderer>(branch),
                InteractionGroupObjectKind.SpriteRenderer => Component<SpriteRenderer>(branch),
                InteractionGroupObjectKind.Rigidbody => Component<Rigidbody>(branch),
                InteractionGroupObjectKind.AudioSource => Component<AudioSource>(branch),
                InteractionGroupObjectKind.Camera => Component<Camera>(branch),
                InteractionGroupObjectKind.Light => Component<Light>(branch),
                InteractionGroupObjectKind.Animator => Component<Animator>(branch),
                InteractionGroupObjectKind.ParticleSystem => Component<ParticleSystem>(branch),
                InteractionGroupObjectKind.Canvas => Component<Canvas>(branch),
                InteractionGroupObjectKind.CanvasGroup => Component<CanvasGroup>(branch),
                _ => null
            };
        }

        /// <summary>Checks component count before selecting a typed local slot.</summary>
        /// <typeparam name="T">Compiled Unity component type.</typeparam>
        /// <param name="branch">Transform owning the selected component.</param>
        /// <returns>The unambiguous component, or null.</returns>
        private T Component<T>(Transform branch) where T : Component
        {
            // Component arrays are created only at an explicit contact-import boundary.
            T[] components = branch.GetComponents<T>();
            return components.Length == Count && Index >= 0 && Index < components.Length ? components[Index] : null;
        }

        /// <summary>Resolves one exact interaction family while preserving duplicate-card ordering.</summary>
        /// <param name="branch">Transform owning the interaction.</param>
        /// <returns>The matching card, or null when count or identity changed.</returns>
        private ObjectInteraction ResolveInteraction(Transform branch)
        {
            ObjectInteraction selected = null;
            int count = 0;
            // Family matching replaces runtime reflection over component class names.
            foreach (ObjectInteraction feature in branch.GetComponents<ObjectInteraction>())
                if (InteractionRuntimeCatalog.KindOf(feature) == Interaction)
                {
                    if (count == Index)
                        selected = feature;
                    count++;
                }
            return count == Count ? selected : null;
        }

        #endregion

        #endregion
    }
}
