using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Matches the Walk state's multiplier to real travel without changing other states or layers.</summary>
    internal sealed class SpawnFlowWalkPlayback
    {
        #region State

        private readonly Animator target;
        private readonly Transform root;
        private readonly float referenceScale;
        private readonly float referenceSpeed;
        private readonly int parameter;
        private readonly string parameterName;
        private bool initialized;
        private bool available;
        private float original;
        private float applied;

        #endregion

        #region Methods
        #region Lifecycle

        /// <summary>Captures the selected state's speed control before clone activation.</summary>
        /// <param name="target">Generated Animator controlled by this visit.</param>
        /// <param name="root">Generated visitor root used to account for scale changes.</param>
        /// <param name="prefab">Original visitor root defining the reference scale.</param>
        /// <param name="settings">Walk clip, controller multiplier and reference travel speed.</param>
        internal SpawnFlowWalkPlayback(Animator target, Transform root, Transform prefab, SpawnFlowAnimatorSettings settings)
        {
            // The parameter hash and prefab scale are stable for this visit.
            this.target = target;
            this.root = root;
            referenceScale = prefab.lossyScale.y;
            referenceSpeed = settings.WalkReferenceSpeed;
            parameterName = settings.Walk.SpeedParameter;
            parameter = Animator.StringToHash(parameterName);
        }

        /// <summary>Restores the multiplier when Day Flow releases this Animator.</summary>
        internal void Restore()
        {
            // No shared controller asset is modified during playback.
            if (available && target != null)
                target.SetFloat(parameter, original);
        }

        #endregion
        #region Synchronization

        /// <summary>Sets the gait rate from actual movement, preserving the controller's original multiplier.</summary>
        /// <param name="speed">World metres travelled per scaled second, excluding teleports.</param>
        internal void Apply(float speed)
        {
            // Resolve parameters only after Unity has initialized the visible clone.
            if (target == null || !target.isActiveAndEnabled)
                return;
            Initialize();
            if (!available)
                return;
            float rate = original * speed * referenceScale / (referenceSpeed * root.lossyScale.y);
            if (Mathf.Approximately(rate, applied))
                return;
            target.SetFloat(parameter, rate);
            applied = rate;
        }

        /// <summary>Checks the configured float once and retains its initial value for cleanup.</summary>
        private void Initialize()
        {
            // A removed parameter reports one warning instead of logging during every travel frame.
            if (initialized)
                return;
            initialized = true;
            foreach (AnimatorControllerParameter candidate in target.parameters)
                if (candidate.nameHash == parameter && candidate.type == AnimatorControllerParameterType.Float)
                {
                    available = true;
                    original = applied = target.GetFloat(parameter);
                    return;
                }
            Debug.LogWarning("Day Flow Walk needs its configured float Speed Multiplier: " + parameterName, target);
        }

        #endregion
        #endregion
    }
}
