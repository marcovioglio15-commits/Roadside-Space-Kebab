using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Reconnects saved scene objects after Play recreates their native instances.</summary>
    internal static class PlayerStudioSceneReference
    {
        #region Methods

        #region Identity

        /// <summary>Captures an Editor identity without saving a scene or changing an object.</summary>
        /// <param name="value">Scene object or asset reference, possibly empty.</param>
        /// <returns>Global identity text, or an empty string for no object.</returns>
        public static string Capture(Object value)
        {
            // Global identities survive recreation of native objects in a saved scene.
            return value != null ? GlobalObjectId.GetGlobalObjectIdSlow(value).ToString() : string.Empty;
        }

        /// <summary>Finds the same object in an already loaded scene without guessing by name.</summary>
        /// <typeparam name="T">Expected object type.</typeparam>
        /// <param name="identity">Previously captured global identity.</param>
        /// <returns>The matching live object, or null when its scene or object is unavailable.</returns>
        public static T Resolve<T>(string identity) where T : Object
        {
            // Failed resolution retains a missing context; it never chooses a similar object.
            return GlobalObjectId.TryParse(identity, out GlobalObjectId id)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as T : null;
        }

        #endregion

        #endregion
    }
}
