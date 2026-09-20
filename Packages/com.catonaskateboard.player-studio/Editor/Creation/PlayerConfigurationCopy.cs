using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Copies a complete configuration without sharing mutable presets or input bindings.</summary>
    internal static class PlayerConfigurationCopy
    {
        #region Methods

        #region Configuration Copies

        /// <summary>Copies every assigned preset and rewires the master before constructing the new player.</summary>
        /// <param name="source">Default master whose stored choices supply the initial values.</param>
        /// <param name="folder">New configuration folder owned by this creation attempt.</param>
        /// <param name="copyActions">False when a draft will replace action roles before they are copied.</param>
        /// <returns>The saved independent master.</returns>
        internal static PlayerMasterPreset Copy(PlayerMasterPreset source, string folder, bool copyActions = true)
        {
            // Null slots stay optional, and each copied asset retains its original values.
            PlayerMasterPreset master = Copy(source, folder, "Master");
            PlayerInputPreset input = Copy(source.InputPreset, folder, "Input");
            if (input != null && copyActions)
                CopyActions(input, folder);
            PlayerCreationUtility.SetReferences(master, ("bodyPreset", Copy(source.BodyPreset, folder, "Body")),
                ("inputPreset", input), ("locomotionPreset", Copy(source.LocomotionPreset, folder, "Locomotion")),
                ("visualPreset", Copy(source.VisualPreset, folder, "Visual")), ("cameraPreset", Copy(source.CameraPreset, folder, "Camera")));
            AssetDatabase.SaveAssetIfDirty(master);
            return master;
        }

        /// <summary>Copies one optional preset without invoking default reset logic on an existing asset.</summary>
        /// <typeparam name="T">Concrete preset type.</typeparam>
        /// <param name="source">Assigned source asset, or null for an empty slot.</param>
        /// <param name="folder">Owned destination folder.</param>
        /// <param name="name">Role-based asset filename.</param>
        /// <returns>The saved copy, or null for an unassigned module.</returns>
        private static T Copy<T>(T source, string folder, string name) where T : ScriptableObject
        {
            // The copy keeps values and external content references while gaining a new asset identity.
            if (source == null)
                return null;
            T copy = UnityEngine.Object.Instantiate(source);
            copy.name = name;
            AssetDatabase.CreateAsset(copy, folder + "/" + name + ".asset");
            return copy;
        }

        /// <summary>Copies the action asset and resolves each assigned role by ID into that copy.</summary>
        /// <param name="input">New Input preset whose roles are being redirected.</param>
        /// <param name="folder">Owned folder receiving actions and their role references.</param>
        internal static void CopyActions(PlayerInputPreset input, string folder)
        {
            // No action name or binding is assumed; customized defaults preserve their identifiers and controls.
            using SerializedObject serialized = new SerializedObject(input);
            InputActionReference movement = (InputActionReference)serialized.FindProperty("movementAction").objectReferenceValue;
            string path = folder + "/Controls.inputactions";
            File.WriteAllText(path, movement.asset.ToJson());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            foreach (string role in new[] { "movementAction", "jumpAction", "lookDeltaAction", "lookRateAction", "cursorToggleAction" })
            {
                SerializedProperty property = serialized.FindProperty(role);
                InputActionReference reference = (InputActionReference)property.objectReferenceValue;
                if (reference == null)
                    continue;
                InputActionReference copy = InputActionReference.Create(actions.FindAction(reference.action.id));
                AssetDatabase.CreateAsset(copy, folder + "/" + ObjectNames.NicifyVariableName(role) + ".asset");
                property.objectReferenceValue = copy;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(input);
        }

        #endregion

        #endregion
    }
}
