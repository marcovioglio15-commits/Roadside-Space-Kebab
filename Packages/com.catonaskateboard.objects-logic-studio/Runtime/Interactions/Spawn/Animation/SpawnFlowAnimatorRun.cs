using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Coordinates one visitor's controller with movement, payment and self-ejection.</summary>
    internal sealed class SpawnFlowAnimatorRun : IDisposable
    {
        #region State

        private readonly SpawnFlowAnimatorSettings settings;
        private readonly Animator target;
        private readonly Transform root;
        private readonly ObjectContactModifier[] collectors;
        private readonly bool rootMotion;
        private readonly bool keepState;
        private readonly AnimatorCullingMode culling;
        private readonly SpawnFlowWalkPlayback playback;
        private SpawnFlowAnimatorState action;
        private int actionHash;
        private bool walking;
        private int[] initialStates;
        private int activeLayer = -1;
        private int activeHash;

        #endregion

        #region Properties

        /// <summary>Whether a payment or death state still needs to finish.</summary>
        internal bool Busy => action != null;
        /// <summary>Whether death has taken priority for the remainder of this visit.</summary>
        internal bool Dying { get; private set; }

        #endregion

        #region Methods
        #region Lifecycle

        /// <summary>Maps the prefab Animator once and subscribes only to this clone's collectors.</summary>
        /// <param name="settings">Validated optional role selections.</param>
        /// <param name="prefab">Original visitor prefab.</param>
        /// <param name="instance">Inactive visitor clone before interaction activation.</param>
        internal SpawnFlowAnimatorRun(SpawnFlowAnimatorSettings settings, GameObject prefab, GameObject instance)
        {
            // Matching component order also supports nested models and duplicate transform names.
            this.settings = settings;
            root = instance.transform;
            int index = Array.IndexOf(prefab.GetComponentsInChildren<Animator>(true), settings.Target);
            Animator[] generated = instance.GetComponentsInChildren<Animator>(true);
            target = index >= 0 && index < generated.Length ? generated[index] : null;
            collectors = instance.GetComponentsInChildren<ObjectContactModifier>(true);
            if (target == null)
                return;
            rootMotion = target.applyRootMotion;
            keepState = target.keepAnimatorStateOnDisable;
            culling = target.cullingMode;
            // Day Flow owns root travel; offscreen actions must still complete and release departure.
            target.applyRootMotion = false;
            target.keepAnimatorStateOnDisable = true;
            target.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (settings.SynchronizeWalk)
                playback = new SpawnFlowWalkPlayback(target, root, prefab.transform, settings);
            foreach (ObjectContactModifier collector in collectors)
                collector.PriceCollected += Pay;
        }

        /// <summary>Releases collector callbacks and restores only this run's Animator overrides.</summary>
        public void Dispose()
        {
            // Unsubscribe before lifetime settlement can publish a final pending balance.
            foreach (ObjectContactModifier collector in collectors)
                if (collector != null)
                    collector.PriceCollected -= Pay;
            if (target == null)
                return;
            playback?.Restore();
            target.applyRootMotion = rootMotion;
            target.keepAnimatorStateOnDisable = keepState;
            target.cullingMode = culling;
        }

        #endregion
        #region Playback

        /// <summary>Changes the neutral role once at each path boundary.</summary>
        /// <param name="moving">Whether a walk path is currently active.</param>
        internal void Move(bool moving)
        {
            // A missing role leaves the controller's current behavior intact.
            walking = moving;
            if (!Busy && !Dying && !Play(walking ? settings.Walk : settings.Idle))
                RestoreLayer();
        }

        /// <summary>Updates the gait from actual travel and selects Idle during path delays or stationary turns.</summary>
        /// <param name="speed">World travel per scaled second measured by the path sampler.</param>
        internal void Travel(float speed)
        {
            // Only real movement changes the locomotion role; repeated frames do not restart a state.
            playback?.Apply(speed);
            bool moving = speed > 0.001f;
            if (walking != moving)
                Move(moving);
        }

        /// <summary>Releases a finished one-shot using actual controller progress, including speed changes.</summary>
        internal void Tick()
        {
            // Looping roles require no polling or per-frame state requests.
            if (!Busy)
                return;
            if (target == null || !target.isActiveAndEnabled)
            {
                action = null;
                return;
            }
            AnimatorStateInfo state = target.GetCurrentAnimatorStateInfo(action.Layer);
            // A blended action may still be the destination state while the previous state fades out.
            if (state.fullPathHash != actionHash && target.IsInTransition(action.Layer))
                state = target.GetNextAnimatorStateInfo(action.Layer);
            if (state.fullPathHash == actionHash && state.normalizedTime < 1f)
                return;
            action = null;
            if (!Dying)
                Move(walking);
        }

        /// <summary>Starts death only after a successful self-impulse on this visitor.</summary>
        /// <param name="source">Interaction publishing its successful start.</param>
        /// <param name="moment">Boundary emitted after applying the effect.</param>
        internal void Observe(ObjectInteraction source, InteractionMoment moment)
        {
            // Death interrupts payment and never resumes a neutral controller role.
            if (Dying || moment != InteractionMoment.Started || source is not ObjectEject eject
                || !source.transform.IsChildOf(root) || eject.Settings is not { SelfEject: true, Behavior: EjectBehavior.Impulse }
                || settings.Death?.Clip == null)
                return;
            Dying = StartAction(settings.Death, true);
        }

        /// <summary>Plays one payment only when this collector publishes real earnings.</summary>
        private void Pay()
        {
            // Pending, discarded and zero-price receipts never reach this callback.
            if (!Dying)
                StartAction(settings.Pay, false);
        }

        /// <summary>Tracks a one-shot only after its controller state accepts playback.</summary>
        /// <param name="state">Optional Pay or Death binding.</param>
        /// <param name="immediate">Whether an impulse requires an immediate death pose.</param>
        /// <returns>True when this action owns the departure hold.</returns>
        private bool StartAction(SpawnFlowAnimatorState state, bool immediate)
        {
            // Shared tracking keeps death interruption and repeated payments consistent.
            if (!Play(state, true, immediate))
                return false;
            action = state;
            actionHash = Animator.StringToHash(state.Path);
            return true;
        }

        /// <summary>Requests one validated controller state without allocating transition objects.</summary>
        /// <param name="state">Optional role to play from its beginning.</param>
        /// <param name="restart">Whether a repeated action should start a fresh cycle.</param>
        /// <param name="immediate">Whether blending must be bypassed.</param>
        /// <returns>True when the active clone accepted the state.</returns>
        private bool Play(SpawnFlowAnimatorState state, bool restart = false, bool immediate = false)
        {
            // A removed controller state must not lock the visitor in an unfinished action.
            if (state?.Clip == null || target == null || !target.isActiveAndEnabled)
                return false;
            CaptureLayers();
            int hash = Animator.StringToHash(state.Path);
            if (!restart && activeLayer == state.Layer && activeHash == hash)
                return true;
            if (state.Layer >= target.layerCount || !target.HasState(state.Layer, hash))
            {
                Debug.LogWarning("Day Flow animation state is missing: " + state.Path, target);
                return false;
            }
            if (activeLayer != state.Layer)
                RestoreLayer();
            activeLayer = state.Layer;
            activeHash = hash;
            if (immediate || settings.BlendDuration <= 0f)
                target.Play(hash, state.Layer, 0f);
            else
                target.CrossFadeInFixedTime(hash, settings.BlendDuration, state.Layer, 0f);
            target.Update(0f);
            return true;
        }

        /// <summary>Captures controller defaults once after the inactive clone becomes visible.</summary>
        private void CaptureLayers()
        {
            // Unselected roles can return to prefab behavior without requiring an Idle assignment.
            if (initialStates != null)
                return;
            target.Update(0f);
            initialStates = new int[target.layerCount];
            for (int layer = 0; layer < initialStates.Length; layer++)
                initialStates[layer] = target.GetCurrentAnimatorStateInfo(layer).fullPathHash;
        }

        /// <summary>Releases the previous layer when a role is empty or uses another layer.</summary>
        private void RestoreLayer()
        {
            // Optional roles must not leave walking or payment stuck on a previous layer.
            if (activeLayer < 0 || target == null || !target.isActiveAndEnabled)
                return;
            target.Play(initialStates[activeLayer], activeLayer, 0f);
            target.Update(0f);
            activeLayer = -1;
            activeHash = 0;
        }

        #endregion
        #endregion
    }
}
