using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>
    /// Shows the body dimensions used by the selected host and a warning when they are unavailable.
    /// Transform checks run here on the editor thread instead of inside OnValidate.
    /// </summary>
    [CustomEditor(typeof(PlayerHost))]
    internal sealed class PlayerHostEditor : UnityEditor.Editor
    {
        #region Labels

        private static readonly GUIContent radiusLabel = new GUIContent("Radius (m)", "Radius read through the master; read-only in this component.");
        private static readonly GUIContent heightLabel = new GUIContent("Height (m)", "Total capsule height read through the master; read-only in this component.");

        #endregion

        #region Serialized Properties

        private SerializedProperty masterPreset;
        private SerializedProperty bodyBinding;
        private SerializedProperty bodyController;
        private SerializedProperty drawBodyGizmo;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Caches the host's serialized fields once per Inspector lifetime.</summary>
        private void OnEnable()
        {
            // Property names match the private serialized fields owned by PlayerHost.
            masterPreset = serializedObject.FindProperty("masterPreset");
            bodyBinding = serializedObject.FindProperty("bodyBinding");
            bodyController = serializedObject.FindProperty("bodyController");
            drawBodyGizmo = serializedObject.FindProperty("drawBodyGizmo");
        }

        #endregion

        #region Inspector

        /// <summary>
        /// Keeps normal field editing and adds a read-only view of the resolved body.
        /// During Play the displayed dimensions come from the captured runtime copy.
        /// </summary>
        public override void OnInspectorGUI()
        {
            // Keep unused binding fields hidden without clearing their saved references.
            serializedObject.Update();
            EditorGUILayout.PropertyField(masterPreset);
            EditorGUILayout.PropertyField(bodyBinding);
            if (bodyBinding.intValue == (int)PlayerBodyBinding.CharacterController)
                EditorGUILayout.PropertyField(bodyController);

            EditorGUILayout.PropertyField(drawBodyGizmo);
            serializedObject.ApplyModifiedProperties();

            // Resolve the body once for this Inspector draw and show only a necessary warning.
            PlayerHost host = (PlayerHost)target;

            if (!host.TryGetBodySettings(out PlayerBodySettings settings, out string warning))
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
                return;
            }

            // Make the difference between preset preview and runtime values visible.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(Application.isPlaying ? "Captured Body" : "Configured Body", EditorStyles.boldLabel);

            // These values belong to the preset, so the host cannot edit a second copy.
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(radiusLabel, settings.Radius);
                EditorGUILayout.FloatField(heightLabel, settings.Height);
            }

        }

        #endregion

        #endregion
    }
}
