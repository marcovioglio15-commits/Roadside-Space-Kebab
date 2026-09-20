using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Stores raw visual values for both the conflict baseline and the editable proposal.</summary>
    [Serializable]
    internal struct PlayerVisualDraft
    {
        #region Serialized Fields

        [Header("Source")]
        [Tooltip("Optional prefab asset; scene references belong to the scene session.")]
        [SerializeField]
        private GameObject prefab;

        [Tooltip("Reference identity retained when an assigned prefab becomes missing during this Editor session.")]
        [SerializeField]
        private ulong prefabId;

        [Header("Offset")]
        [Tooltip("Exact proposed position, including invalid input awaiting correction.")]
        [SerializeField]
        private Vector3 position;

        [Tooltip("Exact proposed Euler angles, without wrapping them to a different representation.")]
        [SerializeField]
        private Vector3 eulerAngles;

        [Tooltip("Uniform positive scale proposed for the visual.")]
        [SerializeField]
        private float scale;

        #endregion

        #region Properties

        /// <summary>The source selected in the draft.</summary>
        public GameObject Prefab => prefab;
        /// <summary>The exact local offset entered in the draft.</summary>
        public Vector3 Position => position;
        /// <summary>The entered rotation representation, including full turns.</summary>
        public Vector3 EulerAngles => eulerAngles;
        /// <summary>The uniform scale entered in the draft.</summary>
        public float Scale => scale;
        /// <summary>Distinguishes a missing source from a deliberate empty source.</summary>
        public bool IsMissing => prefab == null && prefabId != 0;

        #endregion

        #region Methods

        #region Data

        /// <summary>Reads raw properties, including unapplied candidates, without modifying their asset.</summary>
        /// <param name="serialized">Visual preset properties to capture.</param>
        /// <returns>An independent value snapshot retaining missing reference identity.</returns>
        public static PlayerVisualDraft Read(SerializedObject serialized)
        {
            // Read identifiers separately because Unity compares missing objects equal to null.
            SerializedProperty reference = serialized.FindProperty("prefab");
            return new PlayerVisualDraft
            {
                prefab = (GameObject)reference.objectReferenceValue,
                prefabId = EntityId.ToULong(reference.objectReferenceEntityIdValue),
                position = serialized.FindProperty("position").vector3Value,
                eulerAngles = serialized.FindProperty("eulerAngles").vector3Value,
                scale = serialized.FindProperty("scale").floatValue
            };
        }

        /// <summary>Changes only the explicitly chosen source, including deliberate clearing of a missing reference.</summary>
        /// <param name="value">Requested prefab asset or null.</param>
        public void SetPrefab(GameObject value)
        {
            // Numeric edits never call this method and cannot implicitly clear a missing source.
            prefab = value;
            prefabId = value != null ? EntityId.ToULong(value.GetEntityId()) : 0;
        }

        /// <summary>Retains offset input without clamping, normalizing or changing the source.</summary>
        /// <param name="newPosition">Requested local position.</param>
        /// <param name="newEuler">Requested local Euler angles.</param>
        /// <param name="newScale">Requested uniform scale.</param>
        public void SetOffset(Vector3 newPosition, Vector3 newEuler, float newScale)
        {
            // Values remain raw until validation accepts the proposal.
            position = newPosition;
            eulerAngles = newEuler;
            scale = newScale;
        }

        /// <summary>Compares the exact authored representation for dirty tracking and external-edit conflicts.</summary>
        /// <param name="other">Baseline or current asset snapshot.</param>
        /// <returns>True when every stored property still agrees.</returns>
        public bool Matches(PlayerVisualDraft other)
        {
            // Value Equals also handles retained NaN input without a permanently changing comparison.
            return prefabId == other.prefabId && position.Equals(other.position)
                && eulerAngles.Equals(other.eulerAngles) && scale.Equals(other.scale);
        }

        /// <summary>Prepares raw properties for the common Apply path without committing them.</summary>
        /// <param name="serialized">Visual asset view owned by the confirmation operation.</param>
        public void Write(SerializedObject serialized)
        {
            // Validation has already rejected missing references before reaching this boundary.
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("position").vector3Value = position;
            serialized.FindProperty("eulerAngles").vector3Value = eulerAngles;
            serialized.FindProperty("scale").floatValue = scale;
        }

        /// <summary>Checks numeric input and source identity with no asset or scene side effects.</summary>
        /// <param name="settings">Receives usable immutable settings.</param>
        /// <param name="warning">Receives the first invalid offset or source warning.</param>
        /// <returns>True when the proposal can be confirmed.</returns>
        public bool TryGetSettings(out PlayerVisualSettings settings, out string warning)
        {
            // The runtime settings remain the single owner of numeric rules.
            if (!PlayerVisualSettings.TryCreate(prefab, position, eulerAngles, scale, out settings, out warning))
                return false;

            return PlayerVisualPresetValidation.TryValidateSource(prefab, IsMissing, out warning);
        }

        #endregion

        #endregion
    }
}
