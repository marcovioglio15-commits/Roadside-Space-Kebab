using System.Collections.Generic;
using CatOnASkateboard.PlayerStudio;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Offers this independent object for offscreen cleanup when its identity population exceeds its cap.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Presence Cap")]
    public sealed class ObjectPresenceCap : ObjectExtendedInteraction
    {
        #region Fields

        [Header("Presence Cap")]
        [Tooltip("Population limit and offscreen removal policy.")]
        [SerializeField]
        private PresenceCapSettings settings = new PresenceCapSettings();
        private Renderer[] renderers;
        private Collider[] colliders;
        private ObjectGrab grab;
        private bool player;
        private bool fixedObject;
        private float outsideSince = -1f;

        #endregion
        #region Properties

        /// <summary>Applied per-object population policy.</summary>
        public PresenceCapSettings Settings => settings;
        /// <summary>Passive preset identifier.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.PresenceCap;
        /// <summary>Flags defining independent population groups for this object.</summary>
        internal IEnumerable<ObjectFlag> Flags => settings.UseOwnFlags ? Identity.ActiveFlags : settings.Flags;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Caches presentation bounds and joins the shared cleanup schedule.</summary>
        private void OnEnable()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            grab = GetComponent<ObjectGrab>();
            player = GetComponent<PlayerHost>() != null;
            fixedObject = gameObject.isStatic || GetComponent<Rigidbody>() == null && GetComponents<ObjectInteraction>().Length == 1;
            outsideSince = -1f;
            if (TryValidate(out string warning))
                PresenceCapRegistry.Register(this);
            else
                Debug.LogWarning(warning, this);
        }

        /// <summary>Withdraws pooled, stored and assembled ingredients from cleanup candidates.</summary>
        private void OnDisable() => PresenceCapRegistry.Unregister(this);

        /// <summary>Lets one shared interval service every registered cap without per-object scene scans.</summary>
        private void Update() => PresenceCapRegistry.Tick();

        #endregion
        #region Selection

        /// <summary>Protects gameplay ownership and any geometry still inside the actual gameplay camera frame.</summary>
        /// <param name="view">Active observer camera.</param>
        /// <param name="planes">Reused camera frustum planes.</param>
        /// <returns>True after this removable object's offscreen delay elapses.</returns>
        internal bool Removable(Camera view, Plane[] planes)
        {
            if (!Available(InteractionChannels.Passive) || player || transform == HoverObserver.ActivePlayer
                || !settings.IncludeFixedObjects && fixedObject
                || Item.IsReserved || grab != null && grab.IsHeld || !PresenceCapRegistry.Independent(Identity) || Visible(view, planes))
            {
                outsideSince = -1f;
                return false;
            }
            if (outsideSince < 0f)
                outsideSince = Time.time;
            return Time.time - outsideSince >= settings.OffscreenDelay;
        }

        /// <summary>Tests authored visual bounds, falling back to collision geometry or the root point.</summary>
        /// <param name="view">Gameplay camera, excluding unrelated editor and reflection cameras.</param>
        /// <param name="planes">Current camera frustum.</param>
        /// <returns>True when any visible branch can appear in the viewport.</returns>
        private bool Visible(Camera view, Plane[] planes)
        {
            bool geometry = false;
            foreach (Renderer renderer in renderers)
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && (view.cullingMask & (1 << renderer.gameObject.layer)) != 0)
                {
                    geometry = true;
                    if (GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                        return true;
                }
            if (geometry)
                return false;
            foreach (Collider shape in colliders)
                if (shape != null && shape.enabled && shape.gameObject.activeInHierarchy && GeometryUtility.TestPlanesAABB(planes, shape.bounds))
                    return true;
            Vector3 point = view.WorldToViewportPoint(transform.position);
            return point.z >= view.nearClipPlane && point.z <= view.farClipPlane && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
        }

        /// <summary>Shares configuration validation with presets and detached tool proposals.</summary>
        /// <param name="warning">Receives invalid limits.</param>
        /// <returns>True when the configured cap is usable.</returns>
        public override bool TryValidate(out string warning) => settings.TryValidate(out warning);

        #endregion
        #endregion
    }
}
