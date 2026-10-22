using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Fits an oriented effect envelope along the incoming bullet segment without authored offsets.</summary>
    internal sealed class PlayerToolImpactPlacement
    {
        #region State

        private readonly PlayerToolPhysics physics;
        private readonly PlayerCameraRig camera;
        private readonly Bounds envelope;
        private readonly bool faceCamera;
        private readonly float penetration;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Retains the measured effect volume and the shooter's existing camera.</summary>
        /// <param name="queries">Owner-filtered physics scene.</param>
        /// <param name="view">Optional camera rig used to orient effects.</param>
        /// <param name="bounds">Measured lifetime envelope in effect coordinates.</param>
        /// <param name="orientToCamera">Whether placement replaces the authored rotation with camera-facing orientation.</param>
        /// <param name="allowedPenetration">World-space inset permitted in the measured collision envelope.</param>
        internal PlayerToolImpactPlacement(PlayerToolPhysics queries, PlayerCameraRig view, Bounds bounds, bool orientToCamera, float allowedPenetration)
        {
            physics = queries;
            camera = view;
            envelope = bounds;
            faceCamera = orientToCamera;
            penetration = allowedPenetration;
        }

        #endregion

        #region Placement

        /// <summary>Finds the nearest clear location on the shot path, reducing the effect only when full size cannot fit.</summary>
        /// <param name="origin">World muzzle point captured when this bullet fired.</param>
        /// <param name="endpoint">Bullet centre at its contact or range endpoint.</param>
        /// <param name="direction">Normalized incoming flight direction.</param>
        /// <param name="orientation">World rotation retained when the configured orientation is preserved.</param>
        /// <param name="position">Receives the fitted world origin.</param>
        /// <param name="rotation">Receives the selected effect orientation.</param>
        /// <param name="scale">Receives a uniform fallback scale.</param>
        /// <returns>True when a visible, clear envelope fits; false suppresses a fully obstructed effect.</returns>
        internal bool TryPlace(Vector3 origin, Vector3 endpoint, Vector3 direction, Quaternion orientation,
            out Vector3 position, out Quaternion rotation, out float scale)
        {
            position = endpoint;
            rotation = Quaternion.identity;
            scale = 1f;
            float distance = Mathf.Max(0f, Vector3.Dot(endpoint - origin, direction));
            // Back off geometrically rather than sweeping a long flight in fixed centimetre steps.
            for (int size = 0; size < 4; size++, scale *= 0.5f)
            {
                float previous = 0f;
                float retreat = 0f;
                float step = Mathf.Max(0.005f, envelope.extents.magnitude * scale * 0.05f);
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    position = endpoint - direction * retreat;
                    if (Fits(position, direction, orientation, scale, out rotation))
                    {
                        // Refine only a bracket already containing a verified clear endpoint.
                        for (int refine = 0; refine < 5 && retreat > previous; refine++)
                        {
                            float middle = (previous + retreat) * 0.5f;
                            if (Fits(endpoint - direction * middle, direction, orientation, scale, out _))
                                retreat = middle;
                            else
                                previous = middle;
                        }
                        position = endpoint - direction * retreat;
                        return Fits(position, direction, orientation, scale, out rotation);
                    }
                    if (retreat >= distance)
                        break;
                    previous = retreat;
                    retreat = Mathf.Min(distance, retreat + step);
                    step *= 1.7f;
                }
            }
            return false;
        }

        /// <summary>Tests the complete oriented envelope and the camera's unobstructed sight to its centre.</summary>
        /// <param name="position">Candidate world effect origin on the shot path.</param>
        /// <param name="direction">Incoming normalized shot direction.</param>
        /// <param name="orientation">Configured world orientation used by Preserve Orientation.</param>
        /// <param name="scale">Candidate uniform effect scale.</param>
        /// <param name="rotation">Receives the preserved or camera-facing rotation.</param>
        /// <returns>True when solids and the near clip plane leave enough space.</returns>
        private bool Fits(Vector3 position, Vector3 direction, Quaternion orientation, float scale, out Quaternion rotation)
        {
            Camera view = camera != null ? camera.View : null;
            rotation = orientation;
            // Translation and fallback scale never change the configured orientation in Preserve Orientation mode.
            if (faceCamera)
            {
                Vector3 facing = view != null ? view.transform.position - position : -direction;
                if (facing.sqrMagnitude < 0.000001f)
                    facing = -direction;
                rotation = Quaternion.LookRotation(facing, Mathf.Abs(Vector3.Dot(facing.normalized, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up);
            }
            Vector3 centre = position + rotation * (envelope.center * scale);
            Vector3 extents = envelope.extents * scale;
            // Inset only the solid-clearance volume; camera visibility still uses the complete effect bounds.
            if (physics.Overlaps(centre, Vector3.Max(extents - Vector3.one * penetration, Vector3.one * 0.001f), rotation))
                return false;
            if (view == null)
                return true;
            // A clear volume behind another wall or inside the camera cannot represent a visible impact.
            Vector3 sight = centre - view.transform.position;
            return view.transform.InverseTransformPoint(centre).z > view.nearClipPlane + extents.magnitude
                && (sight.sqrMagnitude < 0.000001f || !physics.Cast(view.transform.position, sight.normalized,
                    sight.magnitude, 0f, ~0, false, out _));
        }

        #endregion

        #endregion
    }
}
