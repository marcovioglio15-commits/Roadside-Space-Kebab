using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shares the exact prefab fields owned by Player Studio between comparison and saving.</summary>
    internal static class PlayerPrefabProperties
    {
        #region State

        private static readonly string[] hostFields = { "bodyBinding", "bodyController" };
        private static readonly string[] bodyFields = { "m_Radius", "m_Height", "m_Center" };
        private static readonly string[] inputFields = { "m_Enabled", "m_Actions", "m_NotificationBehavior", "m_DefaultActionMap" };
        private static readonly string[] bridgeFields = { "m_Enabled", "host", "playerInput" };
        private static readonly string[] motorFields = { "m_Enabled", "host", "input" };
        private static readonly string[] rigFields = { "m_Enabled", "host", "view", "input", "target", "model", "motor" };
        private static readonly string[] enabledFields = { "m_Enabled" };
        private static readonly string[] poseFields = { "m_LocalPosition", "m_LocalRotation" };
        private static readonly string[] cameraFields = { "m_Enabled", "field of view", "near clip plane", "far clip plane", "orthographic" };

        #endregion

        #region Methods

        #region Ownership

        /// <summary>Resolves only fields configured by this tool; unrelated camera scripts and rendering options remain authored.</summary>
        /// <param name="component">Component being compared or saved.</param>
        /// <param name="host">Player that owns the root modules and selected camera.</param>
        /// <returns>The managed property paths, or an empty array for an unrelated component.</returns>
        private static string[] Fields(Component component, PlayerHost host)
        {
            // The root placement and third-party components are never part of module synchronization.
            if (component == null)
                return Array.Empty<string>();
            if (component.gameObject == host.gameObject)
                return component switch
                {
                    PlayerHost => hostFields,
                    CharacterController => bodyFields,
                    PlayerInput => inputFields,
                    PlayerInputBridge => bridgeFields,
                    PlayerCharacterControllerMotor => motorFields,
                    PlayerCameraRig => rigFields,
                    PlayerTools => enabledFields,
                    _ => Array.Empty<string>()
                };
            PlayerCameraRig rig = host.GetComponent<PlayerCameraRig>();
            if (rig == null || rig.View == null || component.gameObject != rig.View.gameObject)
                return Array.Empty<string>();
            return component switch
            {
                Transform => poseFields,
                Camera => cameraFields,
                AudioListener => enabledFields,
                _ => Array.Empty<string>()
            };
        }

        /// <summary>Detects unsaved managed additions or overrides without treating rendering and audio customization as preset changes.</summary>
        /// <param name="component">Scene component returned by Unity's override inspection.</param>
        /// <param name="host">Player defining field ownership.</param>
        /// <returns>True when a managed field or component needs to be saved to the player prefab.</returns>
        internal static bool HasOverrides(Component component, PlayerHost host)
        {
            // Added managed components have no source properties to compare yet.
            string[] fields = Fields(component, host);
            if (fields.Length == 0)
                return false;
            if (PrefabUtility.GetCorrespondingObjectFromSource(component) == null)
                return true;
            using SerializedObject serialized = new SerializedObject(component);
            foreach (string field in fields)
                if (IsOverride(serialized.FindProperty(field)))
                    return true;
            return false;
        }

        #endregion

        #region Application

        /// <summary>Captures configured fields before the first prefab write can propagate older instance overrides.</summary>
        /// <param name="component">Configured component on the player root or its camera.</param>
        /// <param name="host">Player defining field ownership.</param>
        internal static void Record(Component component, PlayerHost host)
        {
            // Native controller assignments need explicit recording before any camera property is applied.
            if (Fields(component, host).Length > 0)
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        /// <summary>Saves individual managed properties without submitting hierarchy data or whole camera components to native ApplyObjectOverride.</summary>
        /// <param name="component">Configured scene component, or null when its module is absent.</param>
        /// <param name="host">Player defining field ownership.</param>
        /// <param name="path">Destination player prefab.</param>
        internal static void Apply(Component component, PlayerHost host, string path)
        {
            // Apply additions before field references; never touch components owned by other tools.
            string[] fields = Fields(component, host);
            if (fields.Length == 0)
                return;
            if (PrefabUtility.GetCorrespondingObjectFromSourceAtPath(component, path) == null)
            {
                PrefabUtility.ApplyAddedComponent(component, path, InteractionMode.UserAction);
                return;
            }
            using SerializedObject serialized = new SerializedObject(component);
            foreach (string field in fields)
            {
                // Prefab propagation can change serialized state after each applied property.
                serialized.Update();
                SerializedProperty property = serialized.FindProperty(field);
                if (IsOverride(property))
                    PrefabUtility.ApplyPropertyOverride(property, path, InteractionMode.UserAction);
            }
        }

        /// <summary>Ignores unchanged values and instance-only defaults when examining a managed field.</summary>
        /// <param name="property">Property from the managed field catalog.</param>
        /// <returns>True only for a property that Unity can apply to its source prefab.</returns>
        private static bool IsOverride(SerializedProperty property)
        {
            // Fail clearly if a serialized field is renamed instead of silently losing synchronization.
            if (property == null)
                throw new InvalidOperationException("A managed Player Studio prefab field could not be resolved.");
            return property.prefabOverride && !property.isDefaultOverride;
        }

        #endregion

        #endregion
    }
}
