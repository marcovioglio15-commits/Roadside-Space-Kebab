using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Separates overlapping cut outputs once before they become one rigid carry group.</summary>
    internal static class SliceCarryLayout
    {
        #region Methods
        #region Placement

        /// <summary>Retains clear authored poses and moves only intersecting later outputs using their actual solid shapes.</summary>
        /// <param name="parts">Prepared outputs with suspended original bodies and active mirrored compound colliders.</param>
        /// <param name="count">Number of populated output entries.</param>
        internal static void Arrange(SliceCarryPart[] parts, int count)
        {
            // A bounded solve runs only at the cut boundary; carried pieces keep these poses without a per-frame solver.
            for (int index = 1; index < count; index++)
            {
                bool clear = false;
                for (int pass = 0; pass < 64; pass++)
                {
                    Vector3 correction = Vector3.zero;
                    for (int previous = 0; previous < index; previous++)
                        if (parts[index].TrySeparation(parts[previous], out Vector3 candidate) && candidate.sqrMagnitude > correction.sqrMagnitude)
                            correction = candidate;
                    if (correction.sqrMagnitude <= 0f)
                    {
                        clear = true;
                        break;
                    }
                    parts[index].Move(correction);
                }
                if (!clear)
                    Debug.LogWarning("Slice could not fully separate this held output's compound geometry. Check its output colliders and spawn poses.", parts[index].Body);
            }
        }

        #endregion
        #endregion
    }
}
