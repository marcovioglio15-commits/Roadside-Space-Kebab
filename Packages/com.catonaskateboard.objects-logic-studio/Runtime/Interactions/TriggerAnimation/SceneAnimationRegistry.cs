using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Resolves exact scene animation identities without scene searches during interaction execution.</summary>
    internal static class SceneAnimationRegistry
    {
        #region State

        private static readonly Dictionary<string, List<ObjectTriggerAnimation>> sources = new Dictionary<string, List<ObjectTriggerAnimation>>();

        #endregion
        #region Methods
        #region Membership

        /// <summary>Registers a selected animation when its scene object becomes available.</summary>
        /// <param name="animation">Enabled source with an editor-authored scene identity.</param>
        internal static void Register(ObjectTriggerAnimation animation)
        {
            if (string.IsNullOrEmpty(animation.SceneIdentity))
                return;
            if (!sources.TryGetValue(animation.SceneIdentity, out List<ObjectTriggerAnimation> matches))
            {
                matches = new List<ObjectTriggerAnimation>(1);
                sources.Add(animation.SceneIdentity, matches);
            }
            if (!matches.Contains(animation))
                matches.Add(animation);
            if (matches.Count > 1)
                Debug.LogWarning("Several Trigger Animations share a scene connection. Select the intended scene animation again in Eject and save that scene.", animation);
        }

        /// <summary>Withdraws a disabled source before a later Eject request can target it.</summary>
        /// <param name="animation">Source leaving its scene or being disabled.</param>
        internal static void Unregister(ObjectTriggerAnimation animation)
        {
            if (string.IsNullOrEmpty(animation.SceneIdentity) || !sources.TryGetValue(animation.SceneIdentity, out List<ObjectTriggerAnimation> matches))
                return;
            matches.Remove(animation);
            if (matches.Count == 0)
                sources.Remove(animation.SceneIdentity);
        }

        /// <summary>Clears retained scene references before Play, including sessions without a domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            sources.Clear();
        }

        #endregion
        #region Resolution

        /// <summary>Returns only a unique active match so duplicated scene identities cannot select an arbitrary object.</summary>
        /// <param name="identity">Scene identity serialized by the Eject prefab.</param>
        /// <returns>The exact registered animation, or null while missing or ambiguous.</returns>
        internal static ObjectTriggerAnimation Resolve(string identity)
        {
            return !string.IsNullOrEmpty(identity) && sources.TryGetValue(identity, out List<ObjectTriggerAnimation> matches)
                && matches.Count == 1 && matches[0] != null && matches[0].isActiveAndEnabled ? matches[0] : null;
        }

        #endregion
        #endregion
    }
}
