using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains per-layer ejection deadlines on the body, independently of the ejector's lifetime.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class EjectCollisionGrace : MonoBehaviour
    {
        #region State

        private const int exclusionPriority = 64;
        private readonly float[] deadlines = new float[32];
        private readonly Dictionary<Collider, int> priorities = new Dictionary<Collider, int>();
        private Rigidbody body;
        private int activeLayers;
        private int addedLayers;

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Restores body exclusions before a disabled or pooled object can be reused.</summary>
        private void OnDisable()
        {
            // Restore only bits introduced here, retaining exclusions authored before the impulse.
            if (body != null)
                body.excludeLayers = body.excludeLayers.value & ~addedLayers;
            foreach (KeyValuePair<Collider, int> entry in priorities)
                if (entry.Key != null && entry.Key.layerOverridePriority == exclusionPriority)
                    entry.Key.layerOverridePriority = entry.Value;
            priorities.Clear();
            activeLayers = addedLayers = 0;
            enabled = false;
        }

        /// <summary>Clears transient exclusions when Play retains scene instances.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetSession()
        {
            // Discovery is limited to the session boundary, including currently inactive pooled objects.
            foreach (EjectCollisionGrace grace in FindObjectsByType<EjectCollisionGrace>(FindObjectsInactive.Include))
            {
                grace.OnDisable();
                grace.enabled = false;
            }
        }

        #endregion

        #region Exclusions

        /// <summary>Adds or extends only the selected body's collision exclusions.</summary>
        /// <param name="target">Body receiving the impulse.</param>
        /// <param name="layers">Additional layers to ignore.</param>
        /// <param name="duration">Validated positive duration in scaled seconds.</param>
        internal static void Apply(Rigidbody target, LayerMask layers, float duration)
        {
            // One reusable component survives timer expiry; no object searches occur on ordinary physics frames.
            if (!target.TryGetComponent(out EjectCollisionGrace grace))
                grace = target.gameObject.AddComponent<EjectCollisionGrace>();
            grace.body = target;
            grace.enabled = true;
            grace.addedLayers |= layers.value & ~target.excludeLayers.value;
            for (int layer = 0; layer < 32; layer++)
                if ((layers.value & (1 << layer)) != 0)
                    grace.deadlines[layer] = (grace.activeLayers & (1 << layer)) != 0
                        ? Mathf.Max(grace.deadlines[layer], Time.time + duration) : Time.time + duration;
            grace.activeLayers |= layers.value;
            target.excludeLayers = target.excludeLayers.value | layers.value;
            // Exclusion wins even when the contacted collider explicitly includes this object's layer.
            foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
                if (collider.attachedRigidbody == target && !grace.priorities.ContainsKey(collider))
                {
                    grace.priorities.Add(collider, collider.layerOverridePriority);
                    collider.layerOverridePriority = exclusionPriority;
                }
        }

        /// <summary>Expires individual layer deadlines without reallocating or scanning scene colliders.</summary>
        private void FixedUpdate()
        {
            // Destroyed bodies and fully expired timers stop this component's recurring callbacks.
            if (body == null)
            {
                enabled = false;
                return;
            }
            int expired = 0;
            for (int layer = 0; layer < 32; layer++)
                if ((activeLayers & (1 << layer)) != 0 && Time.time >= deadlines[layer])
                    expired |= 1 << layer;
            if (expired == 0)
                return;
            body.excludeLayers = body.excludeLayers.value & ~(expired & addedLayers);
            activeLayers &= ~expired;
            addedLayers &= ~expired;
            if (activeLayers == 0)
                enabled = false;
        }

        #endregion

        #endregion
    }
}
