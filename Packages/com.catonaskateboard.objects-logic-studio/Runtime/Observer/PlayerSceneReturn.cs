using System.Collections.Generic;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains scene-local player starting poses for death volumes and day transitions without scene reloads.</summary>
    internal static class PlayerSceneReturn
    {
        #region State

        private static readonly Dictionary<Transform, Pose> origins = new Dictionary<Transform, Pose>();
        private static readonly List<Transform> removed = new List<Transform>();
        private static readonly HashSet<GameObject> blockers = new HashSet<GameObject>();

        #endregion
        #region Methods
        #region Registration

        /// <summary>Resets retained poses between Play sessions and listens only at scene boundaries.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            origins.Clear();
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        /// <summary>Captures scene-authored players before their first movement update.</summary>
        /// <param name="scene">Newly loaded scene.</param>
        /// <param name="mode">Unity's scene loading mode.</param>
        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            removed.Clear();
            foreach (Transform root in origins.Keys)
                if (root == null)
                    removed.Add(root);
            foreach (Transform root in removed)
                origins.Remove(root);
            foreach (PlayerHost host in Object.FindObjectsByType<PlayerHost>())
                Capture(host.transform);
        }

        /// <summary>Records the first pose only; observer refreshes never redefine the respawn point.</summary>
        /// <param name="player">Flag-resolved player root, including dynamically spawned players.</param>
        internal static void Capture(Transform player)
        {
            if (player != null && !origins.ContainsKey(player))
                origins.Add(player, new Pose(player.position, player.rotation));
        }

        #endregion
        #region Return

        /// <summary>Identifies player contact without depending on Unity tags.</summary>
        /// <param name="shape">Collider entering a death volume.</param>
        /// <param name="player">Receives the known player root.</param>
        /// <returns>True when the collider belongs to a recorded player.</returns>
        internal static bool TryPlayer(Collider shape, out Transform player)
        {
            foreach (Transform root in origins.Keys)
                if (root != null && shape.transform.IsChildOf(root))
                {
                    player = root;
                    return true;
                }
            player = null;
            return false;
        }

        /// <summary>Returns the observer's current player while leaving all unrelated scene objects intact.</summary>
        internal static void ReturnActive()
        {
            if (HoverObserver.ActivePlayer != null)
                Return(HoverObserver.ActivePlayer);
        }

        /// <summary>Releases carry ownership, clears only overlapping loose objects, and restarts motion at the initial pose.</summary>
        /// <param name="player">Recorded player to return.</param>
        internal static void Return(Transform player)
        {
            if (player == null || !origins.TryGetValue(player, out Pose origin))
                return;
            HoverObserver.ReleasePlayer(player);
            CharacterController controller = player.GetComponent<CharacterController>();
            bool enabled = controller != null && controller.enabled;
            if (enabled)
                controller.enabled = false;
            ClearSpace(player, controller, origin);
            player.SetPositionAndRotation(origin.position, origin.rotation);
            if (player.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                body.linearVelocity = body.angularVelocity = Vector3.zero;
            if (enabled)
                controller.enabled = true;
            if (player.TryGetComponent(out PlayerCharacterControllerMotor motor) && motor.isActiveAndEnabled)
                motor.TryInitialize(out _);
            if (player.TryGetComponent(out PlayerCameraRig rig) && rig.isActiveAndEnabled)
                rig.TryInitialize(out _);
            Physics.SyncTransforms();
        }

        /// <summary>Removes only independent movable items intersecting the player's destination capsule.</summary>
        /// <param name="player">Root excluded from clearing.</param>
        /// <param name="controller">Authored character dimensions, or null for a body-based player.</param>
        /// <param name="origin">Captured destination pose.</param>
        private static void ClearSpace(Transform player, CharacterController controller, Pose origin)
        {
            Vector3 scale = player.lossyScale;
            float radius = controller != null ? controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : 0.35f;
            float height = controller != null ? Mathf.Max(controller.height * Mathf.Abs(scale.y), radius * 2f) : 1.8f;
            Vector3 centre = origin.position + origin.rotation * (controller != null ? Vector3.Scale(controller.center, scale) : Vector3.up * 0.9f);
            Vector3 axis = origin.rotation * Vector3.up * (height * 0.5f - radius);
            blockers.Clear();
            // This allocation occurs only at a respawn boundary; no fixed-size buffer can silently miss a blocker.
            foreach (Collider shape in Physics.OverlapCapsule(centre - axis, centre + axis, radius * 0.98f, ~0, QueryTriggerInteraction.Ignore))
            {
                GameObject candidate = ObjectDespawn.Root(shape);
                if (shape.transform.IsChildOf(player) || player.IsChildOf(candidate.transform) || candidate.isStatic
                    || candidate.GetComponentInChildren<PlayerHost>() != null
                    || !candidate.TryGetComponent(out ObjectGrab _) && shape.attachedRigidbody == null)
                    continue;
                blockers.Add(candidate);
            }
            foreach (GameObject candidate in blockers)
                ObjectDespawn.Remove(candidate);
            blockers.Clear();
        }

        #endregion
        #endregion
    }
}
