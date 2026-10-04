using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Exposes resolved draft data to Unity serialization only while building a field command.</summary>
    internal sealed class ObjectFieldSnapshot : ScriptableObject
    {
        #region Fields

        [Tooltip("Detached Single configuration used for a typed field transfer.")]
        public SingleInteractionDraft Single;
        [Tooltip("Detached extended configuration with source IDs resolved to local components.")]
        public ExtendedInteractionDraft Extended;
        [Tooltip("Detached hover detection and style configuration.")]
        public HoverConfiguration Hover;
        [Tooltip("Detached common hover binding.")]
        public HoverBindingDraft Binding;
        [Tooltip("Resolved optional hover anchor.")]
        public Transform Anchor;

        #endregion
        #region Methods
        #region Capture

        /// <summary>Copies proposals and resolves references without applying anything to the edited object.</summary>
        /// <param name="workspace">Current tool draft.</param>
        /// <param name="source">Selected interaction.</param>
        internal void Read(ObjectWorkspace workspace, ObjectInteraction source)
        {
            // Resolve only the selected feature; unrelated unfinished drafts may contain incomplete arrays.
            Single = ObjectWorkspace.Copy(workspace.Single.Draft);
            Extended = ObjectWorkspace.Copy(workspace.Extended.Draft);
            Hover = ObjectWorkspace.Copy(workspace.Draft);
            Binding = ObjectWorkspace.Copy(workspace.Binding);
            switch (source)
            {
                case ObjectMakeOrder:
                    Extended.Orders.Settings = workspace.Extended.Draft.Orders.Resolve(source.gameObject);
                    break;
                case ObjectInteractionUnlock:
                    Extended.Unlock.Settings = workspace.Extended.Draft.Unlock.Resolve(source.transform.root);
                    break;
                case ObjectAssemblyProduct:
                    Extended.AssemblyProduct.Settings = workspace.Extended.Draft.AssemblyProduct.Resolve(source.transform);
                    break;
                case ObjectHover:
                    Anchor = HoverHierarchy.Resolve(source.transform, workspace.Binding.AnchorPath);
                    break;
            }
        }

        #endregion
        #endregion
    }
}
