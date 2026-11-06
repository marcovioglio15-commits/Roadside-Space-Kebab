using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Applies a short outward scatter to restored ingredient bodies.</summary>
    internal static class AssemblyDismantleMotion
    {
        #region Methods

        #region Physics

        /// <summary>Launches each detached ingredient once after its original colliders are restored.</summary>
        /// <param name="ingredients">Original Grab components released from the recipe.</param>
        /// <param name="center">World-space origin of the dismantled recipe.</param>
        /// <param name="settings">Upward and outward launch speeds.</param>
        internal static void Launch(IReadOnlyList<ObjectGrab> ingredients, Vector3 center, AssemblyDismantleSettings settings)
        {
            for (int index = 0; index < ingredients.Count; index++)
            {
                ObjectGrab ingredient = ingredients[index];
                if (ingredient == null || ingredient.Body == null)
                    continue;
                Rigidbody body = ingredient.Body;
                // Use different outward bearings even if the recipe stacks every magnet at the same point.
                Vector3 outward = Vector3.ProjectOnPlane(body.position - center, Vector3.up);
                if (outward.sqrMagnitude < 0.0001f)
                    outward = Quaternion.AngleAxis(360f * index / ingredients.Count, Vector3.up) * Vector3.forward;
                body.isKinematic = false;
                body.useGravity = true;
                body.detectCollisions = true;
                body.AddForce(Vector3.up * settings.Lift + outward.normalized * settings.Spread, ForceMode.VelocityChange);
            }
        }

        #endregion

        #endregion
    }
}
