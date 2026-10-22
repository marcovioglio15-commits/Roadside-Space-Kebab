using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Layers cursor aiming over authored transforms and resolves the final muzzle ray after camera following.</summary>
    internal sealed class PlayerToolAim
    {
        #region State

        private readonly PlayerToolWeaponSettings settings;
        private readonly Transform muzzle;
        private readonly Transform pivot;
        private readonly PlayerCameraRig rig;
        private readonly PlayerToolPhysics physics;
        private readonly Quaternion muzzleRotation;
        private readonly PlayerToolShotAlignment alignment;
        private Quaternion original;
        private Vector3 originalPosition;
        private Vector2 translation;
        private Vector2 translationVelocity;
        private Vector2 destination;
        private Quaternion offset = Quaternion.identity;
        private Vector3 point;
        private bool applied;
        private bool hasPoint;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Resolves visual bindings once without assuming a main camera or creating a cursor.</summary>
        /// <param name="weapon">Validated weapon settings.</param>
        /// <param name="root">Tools hierarchy root.</param>
        /// <param name="camera">Optional camera rig owned by this player.</param>
        /// <param name="queries">Non-allocating physics scene queries.</param>
        internal PlayerToolAim(PlayerToolWeaponSettings weapon, Transform root, PlayerCameraRig camera, PlayerToolPhysics queries)
        {
            settings = weapon;
            muzzle = PlayerHierarchy.Resolve(root, weapon.MuzzlePath);
            pivot = weapon.AutoAim ? PlayerHierarchy.Resolve(root, weapon.AimPath) : null;
            rig = camera;
            physics = queries;
            muzzleRotation = Quaternion.Euler(weapon.MuzzleRotation);
            alignment = weapon.UsesShotAlignment ? new PlayerToolShotAlignment(weapon) : null;
        }

        /// <summary>Validates muzzle ownership and optional visual aiming before any pose changes.</summary>
        /// <param name="moved">Original-pose collection for module cleanup.</param>
        /// <param name="player">Protected player root.</param>
        /// <returns>True when the muzzle and enabled aiming bindings are usable.</returns>
        internal bool CollectTargets(HashSet<Transform> moved, Transform player)
        {
            if (muzzle == null)
                return false;
            if (!settings.AutoAim)
                return true;
            // The rotated visual must contain the muzzle, and an existing player camera supplies targeting.
            if (pivot == null || pivot == player || !muzzle.IsChildOf(pivot) || rig == null || rig.View == null
                || rig.View.transform.IsChildOf(pivot))
                return false;
            moved.Add(pivot);
            return true;
        }

        #endregion

        #region Presentation

        /// <summary>Removes only this frame's additive aiming before keyframes or switch poses are sampled.</summary>
        internal void Restore()
        {
            if (applied && pivot != null)
            {
                pivot.localRotation = original;
                pivot.localPosition = originalPosition;
            }
            applied = false;
        }

        /// <summary>Clears smoothing when a tool is parked, so its next activation starts from its authored pose.</summary>
        internal void Reset()
        {
            Restore();
            offset = Quaternion.identity;
            translation = translationVelocity = destination = Vector2.zero;
            hasPoint = false;
            alignment?.Reset();
        }

        /// <summary>Returns from the prepared shooting pose through ordinary damped aiming after emission.</summary>
        internal void FinishShotAlignment()
        {
            alignment?.Reset();
        }

        /// <summary>Samples visible repositioning and cursor aim after the camera and carried bodies finish following.</summary>
        /// <param name="delta">Scaled frame duration used by optional aim smoothing.</param>
        /// <param name="shot">Optional shared impact forecaster while a shot waits for preparation.</param>
        /// <returns>True when preparation has completed or no preparation was requested.</returns>
        internal bool Sample(float delta, PlayerToolProjectiles shot)
        {
            hasPoint = false;
            if (!settings.AutoAim || pivot == null || muzzle == null || rig == null || !rig.isActiveAndEnabled
                || rig.View == null || !rig.View.isActiveAndEnabled)
                return true;
            // Locked and texture-rendered views use the same centre as their preauthored fake cursor.
            Camera view = rig.View;
            // Carried bodies finish following before Tools; their current render-frame colliders must be queryable.
            if (settings.RepositionAim)
                Physics.SyncTransforms();
            Ray ray = Cursor.lockState == CursorLockMode.Locked || view.targetTexture != null || Mouse.current == null
                ? view.ViewportPointToRay(new Vector3(0.5f, 0.5f)) : view.ScreenPointToRay(Mouse.current.position.ReadValue());
            point = physics.Cast(ray.origin, ray.direction, settings.AimDistance, 0f, settings.Layers,
                settings.HitTriggers, out RaycastHit contact) ? contact.point : ray.GetPoint(settings.AimDistance);
            Vector3 muzzleOffset = muzzle.TransformPoint(settings.MuzzlePosition) - pivot.position;
            Vector3 forward = muzzle.rotation * muzzleRotation * Vector3.forward;
            originalPosition = pivot.localPosition;
            if (settings.RepositionAim)
                Reposition(view, muzzleOffset, forward, contact.collider, shot, delta);
            // The analytic solution rejects targets behind the muzzle instead of flipping the arm vertically.
            if (!PlayerToolAimPose.Solve(pivot.position, muzzleOffset, forward, point, 0f, out Quaternion correction))
                correction = Quaternion.identity;
            original = pivot.localRotation;
            Quaternion desired = correction * pivot.rotation;
            Quaternion local = pivot.parent != null ? Quaternion.Inverse(pivot.parent.rotation) * desired : desired;
            offset = shot != null && alignment is { Active: true } ? alignment.Orient(Quaternion.Inverse(original) * local)
                : Quaternion.Slerp(offset, Quaternion.Inverse(original) * local,
                    settings.AimResponse > 0f ? 1f - Mathf.Exp(-settings.AimResponse * delta) : 1f);
            pivot.localRotation = original * offset;
            applied = hasPoint = true;
            return shot == null || alignment == null || alignment.Ready;
        }

        /// <summary>Smoothly shifts the clean arm through visible horizontal space before solving its aiming rotation.</summary>
        /// <param name="view">Current camera supplying horizontal heading.</param>
        /// <param name="muzzleOffset">Clean world displacement to the muzzle.</param>
        /// <param name="forward">Clean world muzzle direction.</param>
        /// <param name="collider">Current physical cursor target.</param>
        /// <param name="shot">Optional forecaster for the pending shot.</param>
        /// <param name="delta">Scaled presentation interval.</param>
        private void Reposition(Camera view, Vector3 muzzleOffset, Vector3 forward, Collider collider, PlayerToolProjectiles shot, float delta)
        {
            // Camera pitch does not tilt the player's horizontal repositioning plane.
            Vector3 heading = Vector3.ProjectOnPlane(view.transform.forward, physics.Owner.up).normalized;
            if (heading.sqrMagnitude < 0.000001f)
                heading = Vector3.ProjectOnPlane(physics.Owner.forward, physics.Owner.up).normalized;
            Vector3 right = Vector3.Cross(physics.Owner.up, heading).normalized;
            PlayerToolAimQuery query = new PlayerToolAimQuery(physics, view, pivot.position, muzzleOffset, forward, point,
                heading, right, destination, settings);
            if (shot != null && alignment != null)
                translation = alignment.Sample(query, shot, translation, translationVelocity, offset, point,
                    view.transform.InverseTransformPoint(point), collider, delta);
            if (shot != null && alignment is { Active: true })
            {
                destination = translation;
                translationVelocity = alignment.Velocity;
            }
            else
            {
                destination = PlayerToolAimPose.Find(query, destination, settings.RepositionDistance);
                // Ordinary avoidance keeps its critically damped recovery outside the short preparation phase.
                translation = Vector2.SmoothDamp(translation, destination, ref translationVelocity, 2f / settings.RepositionResponse,
                    settings.RepositionDistance * settings.RepositionResponse, delta);
            }
            pivot.position += right * translation.x + heading * translation.y;
        }

        /// <summary>Reads the animated muzzle and optionally corrects its shot direction to the resolved cursor point.</summary>
        /// <param name="position">Receives the world muzzle position.</param>
        /// <param name="rotation">Receives the positive-Z bullet orientation.</param>
        /// <returns>True when the muzzle still exists.</returns>
        internal bool TryGetShot(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (muzzle == null)
                return false;
            position = muzzle.TransformPoint(settings.MuzzlePosition);
            rotation = muzzle.rotation * muzzleRotation;
            // Cursor correction is computed once per shot; smoothing affects only presentation.
            if (hasPoint && (point - position).sqrMagnitude > 0.000001f)
                rotation = Quaternion.FromToRotation(rotation * Vector3.forward, point - position) * rotation;
            return true;
        }

        #endregion

        #endregion
    }
}
