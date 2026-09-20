using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Retains a local Transform draft and detects external changes before touching the scene.</summary>
    [Serializable]
    internal sealed class PlayerTransformEditSession
    {
        #region Serialized State

        [Header("Source")]
        [Tooltip("Scene player that can receive the confirmed Transform draft.")]
        [SerializeField]
        private PlayerHost source;

        [Header("Baseline")]
        [Tooltip("Parent captured when the draft was opened; reparenting requires reloading the draft.")]
        [SerializeField]
        private Transform originalParent;

        [Tooltip("World matrix of the captured parent, including its ancestors.")]
        [SerializeField]
        private Matrix4x4 originalParentMatrix;

        [Tooltip("Parent rotation captured independently of scale, including mirrored parent transforms.")]
        [SerializeField]
        private Quaternion originalParentRotation;

        [Tooltip("Whether a parent existed before a possible external deletion.")]
        [SerializeField]
        private bool hadParent;

        [Tooltip("Loaded scene identity captured before editing.")]
        [SerializeField]
        private ulong originalScene;

        [Tooltip("Local position captured before editing.")]
        [SerializeField]
        private Vector3 originalPosition;

        [Tooltip("Local rotation captured before editing, without Euler conversion.")]
        [SerializeField]
        private Quaternion originalRotation;

        [Tooltip("Initial Euler representation used by the numeric draft fields.")]
        [SerializeField]
        private Vector3 originalEuler;

        [Tooltip("Local scale captured before editing.")]
        [SerializeField]
        private Vector3 originalScale;

        [Header("Draft")]
        [Tooltip("Proposed local position; the scene object stays unchanged until Apply.")]
        [SerializeField]
        private Vector3 position;

        [Tooltip("Proposed local Euler angles in degrees, retained exactly as entered.")]
        [SerializeField]
        private Vector3 euler;

        [Tooltip("Proposed local scale; incompatible values are reported rather than corrected.")]
        [SerializeField]
        private Vector3 scale;

        [Header("Play Recovery")]
        [Tooltip("Saved player identity used to reconnect its Transform proposal after Play.")]
        [SerializeField]
        private string sourceIdentity;

        [Tooltip("Captured parent's saved identity, retained without changing the original pose baseline.")]
        [SerializeField]
        private string parentIdentity;

        #endregion

        #region Properties

        /// <summary>The scene instance that owns this draft.</summary>
        public PlayerHost Source => source;

        /// <summary>Local position shown beside the viewport.</summary>
        public Vector3 Position => position;

        /// <summary>Local Euler angles shown beside the viewport.</summary>
        public Vector3 Euler => euler;

        /// <summary>Local scale shown beside the viewport.</summary>
        public Vector3 Scale => scale;

        /// <summary>Pending values remain detectable even when the source is deleted.</summary>
        public bool HasChanges => !position.Equals(originalPosition) || !euler.Equals(originalEuler) || !scale.Equals(originalScale);

        /// <summary>The captured parent frame keeps a dirty draft stable across external edits.</summary>
        public Matrix4x4 WorldMatrix => originalParentMatrix * Matrix4x4.TRS(position, Quaternion.Euler(euler), scale);

        /// <summary>Rotation frame used by the native handles without including scale.</summary>
        public Quaternion WorldRotation => originalParentRotation * Quaternion.Euler(euler);

        /// <summary>World position of the proposed feet.</summary>
        public Vector3 WorldPosition => originalParentMatrix.MultiplyPoint3x4(position);

        #endregion

        #region Methods

        #region Draft

        /// <summary>Opens a player only after the window has resolved all pending session edits.</summary>
        /// <param name="host">Loaded scene player, or null when clearing the context.</param>
        public void Open(PlayerHost host)
        {
            // Source changes are coordinated by the owning window, never by the handles.
            source = host;
            Discard();
        }

        /// <summary>Stores numeric or handle edits without writing any Transform or scene flag.</summary>
        /// <param name="newPosition">Proposed local position.</param>
        /// <param name="newEuler">Proposed local Euler angles.</param>
        /// <param name="newScale">Proposed local scale.</param>
        public void SetDraft(Vector3 newPosition, Vector3 newEuler, Vector3 newScale)
        {
            // Invalid input stays available for a deliberate correction.
            position = newPosition;
            euler = newEuler;
            scale = newScale;
        }

        /// <summary>Converts a world-space handle position into the captured local frame.</summary>
        /// <param name="worldPosition">Position returned by Unity's movement handle.</param>
        /// <returns>The matching local position for the numeric fields.</returns>
        public Vector3 ToLocalPosition(Vector3 worldPosition)
        {
            // The baseline parent is used until Apply or Discard resolves the session.
            return originalParentMatrix.inverse.MultiplyPoint3x4(worldPosition);
        }

        /// <summary>Converts a world-space handle rotation into local Euler angles.</summary>
        /// <param name="worldRotation">Rotation returned by Unity's rotation handle.</param>
        /// <returns>Local Euler angles suitable for the draft fields.</returns>
        public Vector3 ToLocalEuler(Quaternion worldRotation)
        {
            // Remove the parent's rotation without reading an externally moved parent.
            return (Quaternion.Inverse(originalParentRotation) * worldRotation).eulerAngles;
        }

        /// <summary>Reloads the live pose without restoring over external scene edits.</summary>
        public void Discard()
        {
            // A missing source clears only our values, not another object or its Undo history.
            originalParent = source != null ? source.transform.parent : null;
            hadParent = originalParent != null;
            originalParentMatrix = hadParent ? originalParent.localToWorldMatrix : Matrix4x4.identity;
            originalParentRotation = hadParent ? originalParent.rotation : Quaternion.identity;
            originalScene = source != null ? source.gameObject.scene.handle.GetRawData() : 0;
            originalPosition = source != null ? source.transform.localPosition : Vector3.zero;
            originalRotation = source != null ? source.transform.localRotation : Quaternion.identity;
            originalEuler = source != null ? source.transform.localEulerAngles : Vector3.zero;
            originalScale = source != null ? source.transform.localScale : Vector3.one;
            SetDraft(originalPosition, originalEuler, originalScale);
        }

        #endregion

        #region Validation

        /// <summary>Checks numeric input before constructing preview matrices or accepting a handle edit.</summary>
        /// <param name="warning">Receives an invalid-number or parent-frame warning.</param>
        /// <returns>True when the draft can be represented without changing any value.</returns>
        public bool TryValidateNumbers(out string warning)
        {
            // A singular parent cannot provide a reversible coordinate conversion.
            warning = string.Empty;
            if (!IsFinite(position) || !IsFinite(euler) || !IsFinite(scale)
                || !float.IsFinite(originalParentMatrix.determinant) || Mathf.Approximately(originalParentMatrix.determinant, 0f)
                || !IsFinite(WorldPosition) || !IsFinite(WorldMatrix.lossyScale))
                warning = "Enter finite Transform values and use a parent with a non-zero scale.";

            return warning.Length == 0;
        }

        /// <summary>Checks conflicts and the proposed body pose before any preset or scene write.</summary>
        /// <param name="preset">Pending preset properties, or null for a Transform-only confirmation.</param>
        /// <param name="warning">Receives the first conflict or binding incompatibility.</param>
        /// <param name="batch">Optional combined module proposals used by workspace Apply.</param>
        /// <returns>True when the complete Transform draft can be confirmed.</returns>
        public bool TryValidate(SerializedObject preset, out string warning, PlayerPresetBatch batch = null)
        {
            // A clean Transform adds no new requirements to a preset-only operation.
            warning = string.Empty;
            if (!HasChanges)
                return true;

            if (EditorApplication.isPlayingOrWillChangePlaymode || source == null || EditorUtility.IsPersistent(source)
                || !source.gameObject.scene.IsValid() || !source.gameObject.scene.isLoaded)
            {
                warning = "The Transform draft requires its loaded player in Edit mode. Discard before choosing another player.";
                return false;
            }

            // Compare parent, ancestors, scene and local pose; never overwrite an outside edit.
            if (source.transform.parent != originalParent || (hadParent && originalParent == null)
                || !(originalParent != null ? originalParent.localToWorldMatrix : Matrix4x4.identity).Equals(originalParentMatrix)
                || source.gameObject.scene.handle.GetRawData() != originalScene || !source.transform.localPosition.Equals(originalPosition)
                || !source.transform.localRotation.Equals(originalRotation) || !source.transform.localScale.Equals(originalScale))
            {
                warning = "The player Transform, parent or scene changed outside this session. Discard to reload the current pose.";
                return false;
            }

            if (!TryValidateNumbers(out warning) || source.MasterPreset == null)
            {
                if (warning.Length == 0)
                    warning = "The scene player needs an applied master before confirming its Transform.";
                return false;
            }

            // A simultaneous Body edit must validate against its candidate dimensions.
            PlayerBodySettings settings;
            bool valid = batch != null ? PlayerBodySceneChange.TryReadCandidate(batch, source.MasterPreset, out settings, out warning)
                : preset != null && (preset.targetObject == source.MasterPreset || preset.targetObject == source.MasterPreset.BodyPreset)
                ? PlayerBodySceneChange.TryReadCandidate(preset, out settings, out warning)
                : source.MasterPreset.TryGetBodySettings(out settings, out warning);
            return valid && TryValidateBody(source, settings, out warning);
        }

        /// <summary>Uses the proposed pose only for its owner while preserving ordinary validation for other instances.</summary>
        /// <param name="host">Instance affected by the preset confirmation.</param>
        /// <param name="settings">Candidate Body dimensions.</param>
        /// <param name="warning">Receives a binding incompatibility.</param>
        /// <returns>True when the current or proposed pose accepts the Body.</returns>
        public bool TryValidateBody(PlayerHost host, PlayerBodySettings settings, out string warning)
        {
            // No temporary assignment to the real Transform is needed for this preflight.
            Vector3 worldScale = host == source && HasChanges ? WorldMatrix.lossyScale : host.transform.lossyScale;
            Vector3 worldUp = host == source && HasChanges ? WorldRotation * Vector3.up : host.transform.up;
            switch (host.BodyBinding)
            {
                case PlayerBodyBinding.CharacterController:
                    return PlayerCharacterControllerBody.TryValidate(host.BodyController, host.transform, settings, worldScale, worldUp, out warning);
                case PlayerBodyBinding.ConfigurationOnly:
                    warning = worldScale == Vector3.one ? string.Empty : "Player Host requires unit world scale. Scale a visual child instead.";
                    break;
                default:
                    warning = "The player uses an unsupported Body binding.";
                    break;
            }

            return warning.Length == 0;
        }

        /// <summary>Checks each vector component without allocating a temporary collection.</summary>
        /// <param name="value">Position, Euler angles or scale to inspect.</param>
        /// <returns>True when all components are finite.</returns>
        private static bool IsFinite(Vector3 value)
        {
            // NaN and infinity remain visible in the field but cannot reach a Transform setter.
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        #endregion

        #region Application

        /// <summary>Writes a prevalidated pose inside the caller's shared Undo group.</summary>
        public void ApplyValidated()
        {
            // Only the confirmation path may write the real scene object.
            if (!HasChanges)
                return;

            Undo.RegisterCompleteObjectUndo(source.transform, "Apply Player Transform");
            source.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(euler));
            source.transform.localScale = scale;
            if (PrefabUtility.IsPartOfPrefabInstance(source.transform))
                PrefabUtility.RecordPrefabInstancePropertyModifications(source.transform);
        }

        #endregion

        #region Play Recovery

        /// <summary>Captures stable references before Unity recreates native scene objects during Play.</summary>
        public void CaptureSceneReferences()
        {
            // Numbers and parent matrices remain untouched for the later conflict comparison.
            sourceIdentity = PlayerStudioSceneReference.Capture(source);
            parentIdentity = PlayerStudioSceneReference.Capture(originalParent);
        }

        /// <summary>Reconnects the same saved objects after Play without accepting a new pose baseline.</summary>
        public void RestoreSceneReferences()
        {
            // A missing object remains unavailable; names never substitute for identity.
            if (source == null)
                source = PlayerStudioSceneReference.Resolve<PlayerHost>(sourceIdentity);
            if (originalParent == null)
                originalParent = PlayerStudioSceneReference.Resolve<Transform>(parentIdentity);
            if (source != null)
                originalScene = source.gameObject.scene.handle.GetRawData();
        }

        #endregion

        #endregion
    }
}
