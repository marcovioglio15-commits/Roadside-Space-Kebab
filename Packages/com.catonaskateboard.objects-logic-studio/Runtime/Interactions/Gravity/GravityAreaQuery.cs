using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Reuses overlap storage when an active gravity area admits moving participants.</summary>
    internal sealed class GravityAreaQuery
    {
        #region State

        private Collider[] hits = new Collider[32];

        #endregion
        #region Methods
        #region Query

        /// <summary>Collects local colliders and grows only when an actual overlap exceeds the retained buffer.</summary>
        /// <param name="owner">Generator receiving eligible late participants.</param>
        internal void Scan(ObjectGravityGenerator owner)
        {
            GravityArea area = owner.Settings.Area;
            int count;
            // Repeat a saturated query so a dense area never silently omits compatible objects.
            while (true)
            {
                count = area.Overlap(owner.transform, hits);
                if (count < hits.Length)
                    break;
                Array.Resize(ref hits, checked(hits.Length * 2));
            }
            for (int index = 0; index < count; index++)
            {
                Collider hit = hits[index];
                if (owner.Settings.AffectObjects && hit.attachedRigidbody != null)
                    owner.Include(hit.attachedRigidbody);
                if (owner.Settings.AffectPlayer)
                    owner.IncludePlayer(hit.GetComponentInParent<PlayerCharacterControllerMotor>());
                hits[index] = null;
            }
        }

        #endregion
        #endregion
    }
}
