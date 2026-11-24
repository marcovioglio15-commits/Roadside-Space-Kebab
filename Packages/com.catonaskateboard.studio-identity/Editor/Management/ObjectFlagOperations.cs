using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Reports explicit operation failures without exposing immediate asset mutation shortcuts.</summary>
    internal static class ObjectFlagOperations
    {
        #region Methods
        #region Diagnostics

        /// <summary>Shows one concise failure notice and logs the full diagnostic list.</summary>
        /// <param name="message">Operation failure.</param>
        /// <param name="details">Affected paths or scan diagnostics.</param>
        internal static void Report(string message, IEnumerable<string> details)
        {
            string complete = message + "\n" + string.Join("\n", details);
            Debug.LogWarning(complete);
            EditorUtility.DisplayDialog("Identity Flags", complete, "OK");
        }

        #endregion
        #endregion
    }
}
