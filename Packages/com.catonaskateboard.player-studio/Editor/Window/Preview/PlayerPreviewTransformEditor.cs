using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Owns the collapsible preview transform panel and routes manipulation to the chosen player part.</summary>
    [Serializable]
    internal sealed class PlayerPreviewTransformEditor
    {
        #region Serialized Fields

        [Header("Preview Editing")]
        [Tooltip("Show numeric transforms above the preview; closing the panel retains every draft.")]
        [SerializeField]
        private bool expanded;

        [Tooltip("Whether the visual model, rather than the player root, receives handle input.")]
        [SerializeField]
        private bool visualSelected;

        [Tooltip("Player transform controls and their retained native handle preferences.")]
        [SerializeField]
        private PlayerTransformDraftView playerView = new PlayerTransformDraftView();

        [Tooltip("Visual handle mode: move, rotate or uniform scale.")]
        [SerializeField]
        private int visualTool;

        #endregion

        #region Labels

        private static readonly GUIContent playerLabel = new GUIContent("Player", "Select the player root for preview manipulation.");
        private static readonly GUIContent visualLabel = new GUIContent("Visual Child", "Select the model pivot for preview manipulation.");
        private static readonly GUIContent positionLabel = new GUIContent("Position", "Visual offset relative to the player, before the authored model pose.");
        private static readonly GUIContent rotationLabel = new GUIContent("Rotation", "Visual rotation offset in degrees. The collider is unchanged.");
        private static readonly GUIContent scaleLabel = new GUIContent("Scale", "Positive uniform multiplier for the visual model.");
        private static readonly GUIContent[] toolLabels =
        {
            new GUIContent("Move", "Move the proposed model at its own pivot."),
            new GUIContent("Rotate", "Rotate around the proposed model pivot."),
            new GUIContent("Scale", "Scale the proposed model around its pivot.")
        };

        #endregion

        #region Properties

        /// <summary>Visibility affects presentation only, never whether the session has changes.</summary>
        public bool Expanded => expanded;

        #endregion

        #region Methods

        #region Panel

        /// <summary>Toggles the complete panel from the preview toolbar without discarding values.</summary>
        public void Toggle()
        {
            // Opening defaults to the last selected part.
            expanded = !expanded;
        }

        /// <summary>Opens the panel and immediately selects the picked part for manipulation.</summary>
        /// <param name="visual">True for the model; false for the player root.</param>
        public void Select(bool visual)
        {
            // Picking changes editor focus only; it never starts a scene transform operation.
            expanded = true;
            visualSelected = visual;
        }

        /// <summary>Draws player fields above optional visual fields in the preview pane.</summary>
        /// <param name="root">Player transform proposal.</param>
        /// <param name="visual">Visual preset proposal.</param>
        /// <param name="scene">Scene binding and outside-edit baseline.</param>
        /// <param name="owner">Window recorded before changing drafts.</param>
        /// <param name="warning">Receives a scene import warning without changing the draft.</param>
        /// <returns>True when numeric input or scene import changed a proposal.</returns>
        public bool Draw(PlayerTransformEditSession root, PlayerVisualEditSession visual, PlayerVisualSceneSession scene,
            UnityEngine.Object owner, out string warning)
        {
            // The panel occupies no space until explicitly opened or a player part is picked.
            warning = string.Empty;
            if (!expanded || root.Source == null)
                return false;
            bool hasVisual = scene.Managed && visual.Draft.Prefab != null;
            if (!hasVisual)
                visualSelected = false;
            if (GUILayout.Toggle(!visualSelected, playerLabel, EditorStyles.miniButton))
                visualSelected = false;
            bool changed = playerView.Draw(root, owner);
            if (!hasVisual)
                return changed;

            // Numeric fields share the asset draft; only the selected part draws handles.
            if (GUILayout.Toggle(visualSelected, visualLabel, EditorStyles.miniButton))
                visualSelected = true;
            if (visualSelected)
                visualTool = GUILayout.Toolbar(visualTool, toolLabels);
            PlayerVisualDraft draft = visual.Draft;
            float width = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 65f;
            EditorGUI.BeginChangeCheck();
            Vector3 position = EditorGUILayout.Vector3Field(positionLabel, draft.Position);
            Vector3 rotation = EditorGUILayout.Vector3Field(rotationLabel, draft.EulerAngles);
            float scale = EditorGUILayout.FloatField(scaleLabel, draft.Scale);
            EditorGUIUtility.labelWidth = width;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(owner, "Edit Visual Offset Draft");
                draft.SetOffset(position, rotation, scale);
                visual.SetDraft(draft);
                changed = true;
            }
            return changed;
        }


        #endregion

        #region Handles and Picking

        /// <summary>Routes one native handle to the active draft while preserving normal camera gestures.</summary>
        /// <param name="root">Player transform proposal.</param>
        /// <param name="visual">Visual preset proposal.</param>
        /// <param name="scene">Selected binding context.</param>
        /// <param name="owner">Window recorded for draft Undo.</param>
        /// <returns>True when the active handle changed its draft.</returns>
        public bool DrawHandles(PlayerTransformEditSession root, PlayerVisualEditSession visual, PlayerVisualSceneSession scene,
            UnityEngine.Object owner)
        {
            // Native navigation and other controls keep their own input priority.
            if (!expanded || Tools.viewToolActive || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            if (!visualSelected || !scene.Managed || visual.Draft.Prefab == null)
                return playerView.DrawHandles(root, owner);
            if (!root.TryValidateNumbers(out _))
                return false;
            PlayerVisualDraft draft = visual.Draft;
            if (!PlayerVisualPose.DrawHandles(ref draft, PlayerVisualPose.Authored(scene, draft), root.WorldMatrix, visualTool))
                return false;
            Undo.RecordObject(owner, "Edit Visual Offset Draft");
            visual.SetDraft(draft);
            return true;
        }

        /// <summary>Selects the clicked player part only when no handle or navigation gesture owns the mouse.</summary>
        /// <param name="host">Player currently opened in the tool.</param>
        /// <param name="scene">Visual hierarchy used to distinguish a model click.</param>
        /// <param name="visual">Visual proposal used for picking before Apply.</param>
        /// <param name="root">Player pose used by the preview.</param>
        /// <param name="preview">Cached proposed meshes, without scene objects.</param>
        /// <returns>True when a click opened the matching transform controls.</returns>
        public bool Pick(PlayerHost host, PlayerVisualSceneSession scene, PlayerVisualDraft visual,
            PlayerTransformEditSession root, PlayerVisualPreview preview)
        {
            // The passive default control yields to all visible native handles.
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (host == null || Tools.viewToolActive || Event.current.alt || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            if (Event.current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(control);
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0
                || GUIUtility.hotControl != 0 || HandleUtility.nearestControl != control)
                return false;
            GameObject picked = HandleUtility.PickGameObject(Event.current.mousePosition, false);
            bool pickedPlayer = picked != null && picked.GetComponentInParent<PlayerHost>() == host;
            bool pickedVisual = visual.Prefab != null && scene.Managed
                && ((pickedPlayer && scene.Binding != null && scene.Binding.VisualRoot != null
                    && picked.transform.IsChildOf(scene.Binding.VisualRoot))
                    || (root.TryValidateNumbers(out _) && preview != null
                        && preview.Pick(scene, visual, root.WorldMatrix, HandleUtility.GUIPointToWorldRay(Event.current.mousePosition))));
            if (!pickedPlayer && !pickedVisual)
            {
                // The player often has no renderer: its body outline still provides a selectable region.
                if (picked != null || !root.TryValidateNumbers(out _) || !host.TryGetBodySettings(out PlayerBodySettings body, out _))
                    return false;
                Ray worldRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                Matrix4x4 inverse = root.WorldMatrix.inverse;
                if (!new Bounds(body.Center, new Vector3(body.Radius * 2f, body.Height, body.Radius * 2f))
                    .IntersectRay(new Ray(inverse.MultiplyPoint3x4(worldRay.origin), inverse.MultiplyVector(worldRay.direction))))
                    return false;
            }
            Select(pickedVisual);
            Event.current.Use();
            return true;
        }

        #endregion

        #endregion
    }
}
