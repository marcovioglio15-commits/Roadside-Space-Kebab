using System;

namespace CatOnASkateboard.StudioIdentity
{
    /// <summary>Retains one owner's reversible flag state independently of contact operations.</summary>
    internal readonly struct ObjectFlagState
    {
        #region State

        internal readonly UnityEngine.Object Owner;
        internal readonly bool Replace;
        internal readonly ObjectFlag[] Add;
        internal readonly ObjectFlag[] Remove;
        internal readonly int Priority;

        #endregion

        #region Methods

        #region Construction

        /// <summary>Captures validated flag arrays that remain read-only during this state.</summary>
        /// <param name="owner">Component owning the state.</param>
        /// <param name="replace">Whether to replace underlying membership.</param>
        /// <param name="add">Flags present in this state.</param>
        /// <param name="remove">Flags excluded from this state.</param>
        /// <param name="priority">Stable order below temporary contact operations.</param>
        internal ObjectFlagState(UnityEngine.Object owner, bool replace, ObjectFlag[] add, ObjectFlag[] remove, int priority)
        {
            // State arrays belong to validated settings, never to mutable identity membership.
            Owner = owner;
            Replace = replace;
            Add = add ?? Array.Empty<ObjectFlag>();
            Remove = remove ?? Array.Empty<ObjectFlag>();
            Priority = priority;
        }

        #endregion

        #endregion
    }

    /// <summary>Defers identity notifications until a synchronous group of changes is complete.</summary>
    public readonly struct ObjectIdentityChangeScope : IDisposable
    {
        #region State

        private readonly ObjectIdentity identity;

        #endregion

        #region Methods

        #region Ownership

        /// <summary>Starts one nested notification scope.</summary>
        /// <param name="identity">Identity receiving the grouped changes.</param>
        internal ObjectIdentityChangeScope(ObjectIdentity identity)
        {
            this.identity = identity;
            identity.BeginChangeScope();
        }

        /// <summary>Publishes the final membership after the last scope closes.</summary>
        public void Dispose()
        {
            identity.EndChangeScope();
        }

        #endregion

        #endregion
    }
}
