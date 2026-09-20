using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shows the received input during Play without adding any runtime UI.</summary>
    [CustomEditor(typeof(PlayerInputBridge))]
    internal sealed class PlayerInputBridgeEditor : UnityEditor.Editor
    {
        #region Labels

        private static readonly GUIContent movementLabel = new GUIContent("Received Movement", "Last value from the player's own action instance. Zero while the bridge or action is inactive.");

        #endregion

        #region Methods

        #region Inspector

        /// <summary>Uses normal assignment fields and adds a read-only runtime value for the connection tutorial.</summary>
        public override void OnInspectorGUI()
        {
            // Setup remains ordinary component authoring outside the Player Studio session.
            DrawDefaultInspector();
            if (!Application.isPlaying)
                return;

            // This view reads the cache; it never looks up actions or enables maps.
            PlayerInputBridge bridge = (PlayerInputBridge)target;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Vector2Field(movementLabel, bridge.Movement);

            if (bridge.ConnectionWarning.Length > 0)
                EditorGUILayout.HelpBox(bridge.ConnectionWarning, MessageType.Warning);
        }

        /// <summary>Refreshes the visible diagnostic only during Play.</summary>
        /// <returns>True while a runtime value can change in this Inspector.</returns>
        public override bool RequiresConstantRepaint()
        {
            // The refresh is Editor-only and creates no runtime Update method.
            return Application.isPlaying;
        }

        #endregion

        #endregion
    }
}
