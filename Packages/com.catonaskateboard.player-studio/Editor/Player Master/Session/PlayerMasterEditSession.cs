using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Holds pending preset assignments separately from the master and from Body dimension drafts.</summary>
    [Serializable]
    internal sealed class PlayerMasterEditSession
    {
        #region Serialized State

        [Header("Source")]
        [Tooltip("Master whose Body, Input, Locomotion and Visual slots receive the confirmed assignments.")]
        [SerializeField]
        private PlayerMasterPreset source;

        [Header("Baseline")]
        [Tooltip("Body assigned when the master session last opened, applied or discarded.")]
        [SerializeField]
        private PlayerBodyPreset originalBody;

        [Tooltip("Remembers whether the baseline contained a Body, even if Unity later loses that reference.")]
        [SerializeField]
        private bool hadOriginalBody;

        [Tooltip("Input assigned when this master session last opened, applied or discarded.")]
        [SerializeField]
        private PlayerInputPreset originalInput;

        [Tooltip("Whether an Input reference existed before a possible external deletion.")]
        [SerializeField]
        private bool hadOriginalInput;

        [Tooltip("Locomotion assigned when the session last read this master.")]
        [SerializeField]
        private PlayerLocomotionPreset originalLocomotion;

        [Tooltip("Whether a Locomotion reference existed before a possible deletion.")]
        [SerializeField]
        private bool hadOriginalLocomotion;

        [Tooltip("Visual assigned when the session last read this master.")]
        [SerializeField]
        private PlayerVisualPreset originalVisual;

        [Tooltip("Whether the baseline contained a Visual reference before a possible deletion.")]
        [SerializeField]
        private bool hadOriginalVisual;

        [Tooltip("Camera assigned when this master session last opened.")]
        [SerializeField]
        private PlayerCameraPreset originalCamera;

        [Tooltip("Whether the original Camera slot existed before a possible deletion.")]
        [SerializeField]
        private bool hadOriginalCamera;

        [Header("Draft")]
        [Tooltip("Body proposed for the master's slot. Selecting it does not change either asset.")]
        [SerializeField]
        private PlayerBodyPreset bodyPreset;

        [Tooltip("Optional Input mapping proposed for this master. Null leaves local input unconfigured.")]
        [SerializeField]
        private PlayerInputPreset inputPreset;

        [Tooltip("Optional movement preset proposed for this master; its contents remain unchanged by a slot assignment.")]
        [SerializeField]
        private PlayerLocomotionPreset locomotionPreset;

        [Tooltip("Optional visual preset proposed for this master; its source and offsets remain unchanged by assignment.")]
        [SerializeField]
        private PlayerVisualPreset visualPreset;

        [Tooltip("Whether the proposed Visual existed before a possible deletion while editing.")]
        [SerializeField]
        private bool hadVisual;

        [Tooltip("Optional Camera preset proposed for this master.")]
        [SerializeField]
        private PlayerCameraPreset cameraPreset;

        [Tooltip("Whether the proposed Camera existed before a possible deletion.")]
        [SerializeField]
        private bool hadCamera;

        [Tooltip("Retains pending assignments even if a referenced preset becomes unavailable during a reload.")]
        [SerializeField]
        private bool hasChanges;

        #endregion

        #region Properties

        /// <summary>The master that can receive this assignment.</summary>
        public PlayerMasterPreset Source => source;

        /// <summary>The proposed Body, which stays independent of the master's applied slot.</summary>
        public PlayerBodyPreset BodyPreset => bodyPreset;

        /// <summary>The optional Input assignment pending in the same master draft.</summary>
        public PlayerInputPreset InputPreset => inputPreset;

        /// <summary>The movement preset proposed for the same master.</summary>
        public PlayerLocomotionPreset LocomotionPreset => locomotionPreset;

        /// <summary>The optional visual preset proposed for this master.</summary>
        public PlayerVisualPreset VisualPreset => visualPreset;

        /// <summary>The optional Camera preset proposed in this master session.</summary>
        public PlayerCameraPreset CameraPreset => cameraPreset;

        /// <summary>Pending assignment state, preserved when an asset reference is lost.</summary>
        public bool HasChanges => hasChanges;

        #endregion

        #region Methods

        #region Draft

        /// <summary>Opens a master after the selection coordinator has resolved any pending edits.</summary>
        /// <param name="master">Validated persistent master, or null when clearing the selection.</param>
        public void Open(PlayerMasterPreset master)
        {
            // Only the selection coordinator chooses a new source for this draft.
            source = master;
            Discard();
        }

        /// <summary>Stores the requested slot without editing the master or the candidate Body.</summary>
        /// <param name="body">Requested Body, including null so incomplete input can remain visible.</param>
        /// <param name="input">Optional Input mapping requested for the same master.</param>
        /// <param name="locomotion">Optional movement configuration requested for the same master.</param>
        /// <param name="visual">Optional source and offset configuration requested for the same master.</param>
        /// <param name="camera">Optional view configuration requested for this master.</param>
        public void SetDraft(PlayerBodyPreset body, PlayerInputPreset input, PlayerLocomotionPreset locomotion, PlayerVisualPreset visual, PlayerCameraPreset camera)
        {
            // Recompute only on an explicit edit; asset deletion must not silently clear pending state.
            bodyPreset = body;
            inputPreset = input;
            locomotionPreset = locomotion;
            if (!ReferenceEquals(visualPreset, visual))
                hadVisual = visual != null;
            visualPreset = visual;
            if (!ReferenceEquals(cameraPreset, camera))
                hadCamera = camera != null;
            cameraPreset = camera;
            hasChanges = bodyPreset != originalBody || inputPreset != originalInput || locomotionPreset != originalLocomotion || visualPreset != originalVisual
                || (hadOriginalBody && originalBody == null) || (hadOriginalInput && originalInput == null)
                || (hadOriginalLocomotion && originalLocomotion == null) || (hadOriginalVisual && originalVisual == null)
                || (hadVisual && visualPreset == null) || cameraPreset != originalCamera
                || (hadOriginalCamera && originalCamera == null) || (hadCamera && cameraPreset == null);
        }

        /// <summary>Reloads the currently applied slot and forgets only the pending assignment.</summary>
        public void Discard()
        {
            // Capture whether an assigned reference existed before a possible later deletion.
            originalBody = source != null ? source.BodyPreset : null;
            hadOriginalBody = originalBody != null;
            bodyPreset = originalBody;
            originalInput = source != null ? source.InputPreset : null;
            hadOriginalInput = originalInput != null;
            inputPreset = originalInput;
            originalLocomotion = source != null ? source.LocomotionPreset : null;
            hadOriginalLocomotion = originalLocomotion != null;
            locomotionPreset = originalLocomotion;
            originalVisual = source != null ? source.VisualPreset : null;
            hadOriginalVisual = originalVisual != null;
            visualPreset = originalVisual;
            hadVisual = hadOriginalVisual;
            originalCamera = source != null ? source.CameraPreset : null;
            hadOriginalCamera = originalCamera != null;
            cameraPreset = originalCamera;
            hadCamera = hadOriginalCamera;
            hasChanges = false;
        }

        /// <summary>Checks that the proposed slot identifies a saved Body with usable dimensions.</summary>
        /// <param name="settings">Receives the candidate's applied dimensions for the preview.</param>
        /// <param name="warning">Receives a missing-reference, persistence or dimension warning.</param>
        /// <returns>True when the proposed Body is suitable for assignment.</returns>
        public bool TryGetSettings(out PlayerBodySettings settings, out string warning)
        {
            // Candidate preview uses saved dimensions, not another editable Body draft.
            settings = default;
            warning = string.Empty;
            if (bodyPreset == null || !EditorUtility.IsPersistent(bodyPreset))
            {
                warning = "Choose a Body asset saved in the Project window for this slot.";
                return false;
            }

            return bodyPreset.TryGetSettings(out settings, out warning);
        }

        #endregion

        #region Apply

        /// <summary>Prepares preset references after checking all master slots still match their baseline.</summary>
        /// <param name="changes">Receives pending properties owned and disposed by the confirmation coordinator.</param>
        /// <param name="warning">Receives the reason assignment could not be applied.</param>
        /// <returns>True when the candidate is ready without writing the master.</returns>
        public bool TryPrepareApply(out SerializedObject changes, out string warning)
        {
            // Apply must remain an Edit-mode action with an existing destination.
            warning = string.Empty;
            changes = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode || source == null)
            {
                warning = "Apply Slot requires Edit mode and an existing master.";
                return false;
            }

            // Module values are validated together with their independent drafts by the batch coordinator.
            if (bodyPreset == null || hadVisual && visualPreset == null || hadCamera && cameraPreset == null)
            {
                warning = "Choose an available Body and replace or explicitly clear any deleted optional preset.";
                return false;
            }
            foreach (ScriptableObject asset in new ScriptableObject[] { bodyPreset, inputPreset, locomotionPreset, visualPreset, cameraPreset })
                if (asset != null && !EditorUtility.IsPersistent(asset))
                {
                    warning = "Choose preset assets saved in the Project window.";
                    return false;
                }
            if (!hasChanges)
                return true;

            // A deleted baseline preset is also a conflict, including after script reload.
            if (source.BodyPreset != originalBody || source.InputPreset != originalInput || source.LocomotionPreset != originalLocomotion
                || source.VisualPreset != originalVisual || source.CameraPreset != originalCamera
                || (hadOriginalCamera && originalCamera == null)
                || (hadOriginalBody && originalBody == null) || (hadOriginalInput && originalInput == null)
                || (hadOriginalLocomotion && originalLocomotion == null) || (hadOriginalVisual && originalVisual == null))
            {
                warning = "The master's preset slots changed outside this session. Discard to reload their current assignments.";
                return false;
            }

            // Prepare slot references only; no candidate preset is modified here.
            changes = new SerializedObject(source);
            changes.FindProperty("bodyPreset").objectReferenceValue = bodyPreset;
            changes.FindProperty("inputPreset").objectReferenceValue = inputPreset;
            changes.FindProperty("locomotionPreset").objectReferenceValue = locomotionPreset;
            changes.FindProperty("visualPreset").objectReferenceValue = visualPreset;
            changes.FindProperty("cameraPreset").objectReferenceValue = cameraPreset;
            return true;
        }

        #endregion

        #endregion
    }
}
