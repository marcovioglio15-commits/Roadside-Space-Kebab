using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits the Visual proposal and scene binding without touching the source asset or real transforms.</summary>
    [Serializable]
    internal sealed class PlayerVisualDraftView
    {
        #region Serialized State

        [Header("Sections")]
        [Tooltip("Whether the selected player's visual binding choices are visible.")]
        [SerializeField]
        private bool bindingOpen = true;

        [Tooltip("Whether the visual model source is visible.")]
        [SerializeField]
        private bool sourceOpen = true;

        #endregion

        #region Labels

        private static readonly GUIContent prefabLabel = new GUIContent("Prefab", "Static model asset. Clearing an applied source removes its managed model on Apply.");
        private static readonly GUIContent clearLabel = new GUIContent("Clear Missing Reference", "Explicitly replace the lost prefab reference with an empty source in this draft.");
        private static readonly GUIContent bindingLabel = new GUIContent("Scene Binding", "Manage the chosen scene player's visual without affecting unrelated children.");
        private static readonly GUIContent managedLabel = new GUIContent("Manage Visual", "Apply creates or adopts the model. Turning this off releases management and keeps the current model in place.");
        private static readonly GUIContent existingLabel = new GUIContent("Existing Child", "Direct child of the chosen player to adopt when no prefab is specified. Its original local pose is preserved as the offset baseline.");
        #endregion

        #region Methods

        #region Controls

        /// <summary>Draws source and binding choices; offset fields belong to the preview transform panel.</summary>
        /// <param name="session">Asset proposal retained by the window.</param>
        /// <param name="scene">Selected instance proposal, possibly without a scene context.</param>
        /// <param name="owner">Window to record before changing a draft.</param>
        /// <returns>True when either proposal changed.</returns>
        public bool Draw(PlayerVisualEditSession session, PlayerVisualSceneSession scene, UnityEngine.Object owner)
        {
            // A slot must be confirmed before its separate asset can be edited.
            if (session.Source == null && !session.HasChanges)
            {
                EditorGUILayout.LabelField("Assign and apply a Visual Slot to edit its source and offsets.", EditorStyles.wordWrappedLabel);
                return false;
            }

            PlayerVisualDraft draft = session.Draft;
            EditorGUI.BeginChangeCheck();
            bool wasChanged = GUI.changed;
            sourceOpen = EditorGUILayout.Foldout(sourceOpen, "Model", true, EditorStyles.foldoutHeader);
            GUI.changed = wasChanged;
            GameObject prefab = sourceOpen
                ? (GameObject)EditorGUILayout.ObjectField(prefabLabel, draft.Prefab, typeof(GameObject), false) : draft.Prefab;
            bool sourceChanged = EditorGUI.EndChangeCheck();
            if (draft.IsMissing && GUILayout.Button(clearLabel))
            {
                prefab = null;
                sourceChanged = true;
            }
            if (sourceChanged)
                draft.SetPrefab(prefab);

            bool sceneChanged = false;
            if (scene.Host != null || scene.HasChanges)
            {
                bindingOpen = EditorGUILayout.Foldout(bindingOpen, bindingLabel, true);
                if (bindingOpen)
                {
                    EditorGUI.BeginChangeCheck();
                    bool managed = EditorGUILayout.Toggle(managedLabel, scene.Managed);
                    GameObject existing = scene.Existing;
                    if (managed && prefab == null)
                        using (new EditorGUI.DisabledScope(scene.Binding != null))
                            existing = (GameObject)EditorGUILayout.ObjectField(existingLabel, existing, typeof(GameObject), true);
                    sceneChanged = EditorGUI.EndChangeCheck();
                    if (sceneChanged)
                    {
                        Undo.RecordObject(owner, "Edit Visual Binding Draft");
                        scene.SetDraft(managed, existing);
                    }
                }
            }

            if (sourceChanged)
            {
                Undo.RecordObject(owner, "Edit Visual Preset Draft");
                session.SetDraft(draft);
                if (prefab != null && scene.Host != null && !scene.Managed)
                    scene.SetDraft(true, scene.Existing);
            }
            return sourceChanged || sceneChanged;
        }

        #endregion

        #endregion
    }
}
