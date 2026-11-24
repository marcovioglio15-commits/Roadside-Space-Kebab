using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Coordinates controller and transform roles with movement, payment and self-ejection.</summary>
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
        private readonly Dictionary<SpawnFlowAnimatorState, SpawnFlowTransformRun> transforms;
        private readonly HashSet<ObjectDialogue> speakers;
        private SpawnFlowTransformRun transformAnimation;
        private SpawnFlowAnimatorState activeRole;
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
            if (settings.UsesAnimator)
            {
                int index = Array.IndexOf(prefab.GetComponentsInChildren<Animator>(true), settings.Target);
                Animator[] generated = instance.GetComponentsInChildren<Animator>(true);
                target = index >= 0 && index < generated.Length ? generated[index] : null;
            }
            // Controller-only visitors need no transform catalogs or track buffers.
            if (settings.UsesTransforms)
            {
                transforms = new Dictionary<SpawnFlowAnimatorState, SpawnFlowTransformRun>();
                Transform[] originalTransforms = prefab.GetComponentsInChildren<Transform>(true);
                Transform[] generatedTransforms = instance.GetComponentsInChildren<Transform>(true);
                foreach (SpawnFlowAnimatorState role in new[] { settings.Walk, settings.Idle, settings.Talk, settings.Pay, settings.Death })
                    if (role is { Mode: SpawnFlowAnimationMode.TransformKeyframes })
                        transforms.Add(role, new SpawnFlowTransformRun(role.Tracks, originalTransforms, generatedTransforms));
            }
            collectors = settings.Pay is { Selected: true }
                ? instance.GetComponentsInChildren<ObjectContactModifier>(true) : Array.Empty<ObjectContactModifier>();
            if (target != null)
            {
                rootMotion = target.applyRootMotion;
                keepState = target.keepAnimatorStateOnDisable;
                culling = target.cullingMode;
                // Day Flow owns root travel; offscreen actions still need to release departure.
                target.applyRootMotion = false;
                target.keepAnimatorStateOnDisable = true;
                target.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (settings.SynchronizeWalk && settings.Walk is { ControllerSelected: true })
                    playback = new SpawnFlowWalkPlayback(target, root, prefab.transform, settings);
            }
            foreach (ObjectContactModifier collector in collectors)
                collector.PriceCollected += Pay;
            if (settings.Talk is { Selected: true })
            {
                speakers = new HashSet<ObjectDialogue>();
                DialogueHud.PresentationChanged += Talk;
            }
        }

        /// <summary>Releases collector callbacks and restores only this run's Animator overrides.</summary>
        public void Dispose()
        {
            transformAnimation?.Restore();
            DialogueHud.PresentationChanged -= Talk;
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
            if (!Busy && !Dying && !Play(speakers is { Count: > 0 } ? settings.Talk : walking ? settings.Walk : settings.Idle))
                RestoreLayer();
        }

        /// <summary>Changes the speaking role only when this visitor gains or loses a visible text box.</summary>
        /// <param name="source">Dialogue owning the HUD presentation.</param>
        /// <param name="visible">Whether its text box is rendered.</param>
        private void Talk(ObjectDialogue source, bool visible)
        {
            // Unrelated visitors and observers never change this clone's animation.
            if (source == null || !source.transform.IsChildOf(root))
                return;
            if (visible)
                speakers.Add(source);
            else
                speakers.Remove(source);
            Move(walking);
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

        /// <summary>Advances transform tracks and releases completed one-shots from either playback mode.</summary>
        /// <param name="delta">Scaled seconds available for transform playback.</param>
        internal void Tick(float delta)
        {
            // Only the active transform role advances; unused roles retain their cached bindings.
            transformAnimation?.Tick(delta);
            if (!Busy)
                return;
            if (action.Mode == SpawnFlowAnimationMode.TransformKeyframes)
            {
                if (transformAnimation is { Finished: false })
                    return;
                CompleteAction();
                return;
            }
            if (target == null || !target.isActiveAndEnabled)
            {
                CompleteAction();
                return;
            }
            AnimatorStateInfo state = target.GetCurrentAnimatorStateInfo(action.Layer);
            // A blended action may still be the destination state while the previous state fades out.
            if (state.fullPathHash != actionHash && target.IsInTransition(action.Layer))
                state = target.GetNextAnimatorStateInfo(action.Layer);
            if (state.fullPathHash == actionHash && state.normalizedTime < 1f)
                return;
            CompleteAction();
        }

        /// <summary>Removes last frame's offsets before the visitor's travel path advances.</summary>
        internal void RestoreTransforms()
        {
            // The next travel sample starts from the underlying visitor pose.
            transformAnimation?.Restore();
        }

        /// <summary>Composes the current offsets after Unity evaluates controller animation.</summary>
        internal void ApplyTransforms()
        {
            // LateUpdate keeps controller evaluation from replacing the sampled offsets.
            transformAnimation?.Apply();
        }

        /// <summary>Releases departure and resumes the current neutral role after payment.</summary>
        private void CompleteAction()
        {
            // Death keeps its final pose until the visitor is removed.
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
                || settings.Death is not { Selected: true })
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
            if (state is not { Selected: true } || !Play(state, true, immediate))
                return false;
            action = state;
            actionHash = state.Mode == SpawnFlowAnimationMode.Animator ? Animator.StringToHash(state.Path) : 0;
            return true;
        }

        /// <summary>Switches the active playback source without allocating per-frame state.</summary>
        /// <param name="state">Optional role to play from its beginning.</param>
        /// <param name="restart">Whether a repeated action should start a fresh cycle.</param>
        /// <param name="immediate">Whether blending must be bypassed.</param>
        /// <returns>True when the active clone accepted the state.</returns>
        private bool Play(SpawnFlowAnimatorState state, bool restart = false, bool immediate = false)
        {
            // Mode changes release the previous offsets without restarting a repeated neutral role.
            if (!restart && activeRole == state && state is { Selected: true })
                return true;
            transformAnimation?.Restore();
            transformAnimation = null;
            activeRole = null;
            if (state is not { Selected: true })
                return false;
            if (state.Mode == SpawnFlowAnimationMode.TransformKeyframes)
            {
                RestoreLayer();
                if (!transforms.TryGetValue(state, out transformAnimation))
                    return false;
                transformAnimation.Begin(state == settings.Walk || state == settings.Idle || state == settings.Talk);
                activeRole = state;
                return true;
            }
            if (!PlayController(state, restart, immediate))
                return false;
            activeRole = state;
            return true;
        }

        /// <summary>Requests one controller state after releasing any transform role.</summary>
        /// <param name="state">Selected Animator state.</param>
        /// <param name="restart">Whether repeated actions begin another cycle.</param>
        /// <param name="immediate">Whether death bypasses blending.</param>
        /// <returns>True when the active clone accepts the controller state.</returns>
        private bool PlayController(SpawnFlowAnimatorState state, bool restart, bool immediate)
        {
            // A removed controller state must not lock the visitor in an unfinished action.
            if (state?.Clip == null || target == null || !target.isActiveAndEnabled)
                return false;
            CaptureLayers();
            int hash = Animator.StringToHash(state.Path);
            if (!restart && activeLayer == state.Layer && activeHash == hash)
                return true;
            if (state.Layer < 0 || state.Layer >= target.layerCount || !target.HasState(state.Layer, hash))
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
