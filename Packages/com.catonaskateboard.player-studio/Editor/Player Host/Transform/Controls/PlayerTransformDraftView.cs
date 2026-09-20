using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shares one Transform draft between an Inspector-like panel and native scene handles.</summary>
    [Serializable]
    internal sealed class PlayerTransformDraftView
    {
        #region Serialized State

        [Header("Transform Handles")]
        [Tooltip("Selected draft handle: move, rotate or scale.")]
        [SerializeField]
        private int toolIndex;

        [Tooltip("Orient movement arrows along the player's proposed axes instead of world axes.")]
        [SerializeField]
        private bool localAxes;

        #endregion

        #region Labels

        private static readonly GUIContent positionLabel = new GUIContent("Position", "Proposed position relative to the current parent, in metres.");
        private static readonly GUIContent rotationLabel = new GUIContent("Rotation", "Proposed local Euler angles in degrees. The native body currently requires an upright root.");
        private static readonly GUIContent scaleLabel = new GUIContent("Scale", "Proposed local scale. The current body requires unit world scale; incompatible values remain in the draft.");
        private static readonly GUIContent axesLabel = new GUIContent("Local Axes", "Orient the movement arrows along the proposed player rotation. Numeric fields always remain local.");
        private static readonly GUIContent[] toolLabels =
        {
            new GUIContent("Move", "Edit the proposed position with native arrows and planes."),
            new GUIContent("Rotate", "Edit the proposed rotation with native rings."),
            new GUIContent("Scale", "Edit the proposed local scale with native scale handles.")
        };

        #endregion

        #region Methods

        #region Panel

        /// <summary>Displays local pose fields outside the scrolling preset controls.</summary>
        /// <param name="session">Pose draft shared with scene handles.</param>
        /// <param name="owner">Window whose serialized draft participates in Undo.</param>
        /// <returns>True when numeric pose values changed.</returns>
        public bool Draw(PlayerTransformEditSession session, UnityEngine.Object owner)
        {
            // No Transform controls are relevant without a scene player or a retained draft.
            if (session.Source == null && !session.HasChanges)
                return false;

            // Mode and axis preferences do not mark the scene or preset draft as changed.
            toolIndex = GUILayout.Toolbar(toolIndex, toolLabels);
            if (toolIndex == 0)
                localAxes = EditorGUILayout.Toggle(axesLabel, localAxes);

            // The compact label width leaves room for X, Y and Z in a narrow dock.
            float previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 65f;
            EditorGUI.BeginChangeCheck();
            Vector3 position = EditorGUILayout.Vector3Field(positionLabel, session.Position);
            Vector3 euler = EditorGUILayout.Vector3Field(rotationLabel, session.Euler);
            Vector3 scale = EditorGUILayout.Vector3Field(scaleLabel, session.Scale);
            EditorGUIUtility.labelWidth = previousWidth;
            if (!EditorGUI.EndChangeCheck())
                return false;

            Undo.RecordObject(owner, "Edit Player Transform Draft");
            session.SetDraft(position, euler, scale);
            return true;
        }

        #endregion

        #region Handles

        /// <summary>Manipulates values rather than a real Transform, using Unity's public handles.</summary>
        /// <param name="session">Draft whose captured parent converts between local and world coordinates.</param>
        /// <param name="owner">Window recorded by draft-only Undo.</param>
        /// <returns>True when a handle produced new draft values.</returns>
        public bool DrawHandles(PlayerTransformEditSession session, UnityEngine.Object owner)
        {
            // Let native camera navigation own its gestures, and pause authoring during Play.
            if (session.Source == null || EditorApplication.isPlayingOrWillChangePlaymode
                || Tools.viewToolActive || !session.TryValidateNumbers(out _))
                return false;

            // Keep untouched values exactly as entered; only the active handle changes its domain.
            Vector3 position = session.Position;
            Vector3 euler = session.Euler;
            Vector3 scale = session.Scale;
            EditorGUI.BeginChangeCheck();
            switch (toolIndex)
            {
                case 0:
                    position = session.ToLocalPosition(Handles.PositionHandle(session.WorldPosition,
                        localAxes ? session.WorldRotation : Quaternion.identity));
                    break;
                case 1:
                    euler = session.ToLocalEuler(Handles.RotationHandle(session.WorldRotation, session.WorldPosition));
                    break;
                case 2:
                    scale = Handles.ScaleHandle(scale, session.WorldPosition, session.WorldRotation,
                        HandleUtility.GetHandleSize(session.WorldPosition));
                    break;
            }

            if (!EditorGUI.EndChangeCheck())
                return false;

            // The actual scene root is deliberately absent from this write path.
            Undo.RecordObject(owner, "Edit Player Transform Draft");
            session.SetDraft(position, euler, scale);
            return true;
        }

        #endregion

        #endregion
    }
}
