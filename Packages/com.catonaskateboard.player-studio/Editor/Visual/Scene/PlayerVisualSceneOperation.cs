using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Applies one prevalidated visual change inside the session's existing Undo group.</summary>
    internal sealed class PlayerVisualSceneOperation
    {
        #region State

        private readonly PlayerHost host;
        private readonly PlayerVisualBinding binding;
        private readonly PlayerVisualSettings settings;
        private readonly GameObject existing;
        private readonly bool managed;
        private readonly bool removeModel;
        private readonly string prefabPath;

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Stores a validated scene operation for immediate execution by the save coordinator.</summary>
        /// <param name="host">Player receiving the binding.</param>
        /// <param name="binding">Current binding, or null when creating one.</param>
        /// <param name="settings">Validated prefab and offset values.</param>
        /// <param name="existing">Direct child to adopt when no binding or prefab exists.</param>
        /// <param name="managed">False releases management and preserves the model.</param>
        /// <param name="removeModel">Whether clearing the preset deletes the owned model.</param>
        /// <param name="prefabPath">Player prefab receiving only this visual change.</param>
        public PlayerVisualSceneOperation(PlayerHost host, PlayerVisualBinding binding, PlayerVisualSettings settings,
            GameObject existing, bool managed, bool removeModel, string prefabPath)
        {
            // The operation never outlives its synchronous Apply request.
            this.host = host;
            this.binding = binding;
            this.settings = settings;
            this.existing = existing;
            this.managed = managed;
            this.removeModel = removeModel;
            this.prefabPath = prefabPath;
        }

        #endregion

        #region Application

        /// <summary>Creates or releases only the explicitly managed hierarchy and records every change for Undo.</summary>
        public void Apply()
        {
            // Clearing the source removes its owned model; an explicit release keeps the existing model.
            if (!managed)
            {
                if (binding == null)
                    return;
                if (removeModel && binding.VisualRoot != null)
                    PlayerVisualPrefabUtility.RemoveChild(host, binding.VisualRoot.gameObject, prefabPath);
                PlayerVisualPrefabUtility.RemoveBinding(host, binding, prefabPath);
                return;
            }

            PlayerVisualPrefabUtility.RestoreMissingChild(binding, prefabPath);
            PlayerVisualBinding target = binding;
            if (target == null || (target.VisualRoot == null && target.Model == null))
            {
                // A new prefab is a direct player child; adoption keeps the original hierarchy.
                GameObject model = settings.Prefab != null ? CreateModel(host.transform) : existing;
                if (target == null)
                    target = Undo.AddComponent<PlayerVisualBinding>(host.gameObject);
                WriteBinding(target, model.transform, model, settings.Prefab, false);
            }
            else if (settings.Prefab != null && settings.Prefab != target.SourcePrefab)
            {
                // Replacement removes only the recorded visual branch, never other player children.
                PlayerVisualPrefabUtility.RemoveChild(host, target.VisualRoot.gameObject, prefabPath);
                GameObject model = CreateModel(host.transform);
                WriteBinding(target, model.transform, model, settings.Prefab, false);
            }

            // A retained container uses the prefab's authored child pose; the complete offset belongs on its root.
            if (target.OwnsContainer && settings.Prefab != null)
            {
                Undo.RegisterCompleteObjectUndo(target.Model.transform, "Apply Visual Model Pose");
                target.Model.transform.SetLocalPositionAndRotation(settings.Prefab.transform.localPosition, settings.Prefab.transform.localRotation);
                target.Model.transform.localScale = settings.Prefab.transform.localScale;
            }

            // The native collider and the player's root are never passed to these setters.
            Undo.RegisterCompleteObjectUndo(target.VisualRoot, "Apply Visual Offset");
            target.ApplyOffset(settings);
            PlayerVisualPrefabUtility.ApplyBinding(target, prefabPath);
        }

        /// <summary>Instantiates a validated prefab while keeping its authored root pose and prefab connection.</summary>
        /// <param name="parent">Player transform receiving the direct visual child.</param>
        /// <returns>The registered model instance.</returns>
        private GameObject CreateModel(Transform parent)
        {
            // Validation admits no scripts or physics components in this initial static model contract.
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(settings.Prefab, host.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(model, "Create Visual Model");
            Undo.SetTransformParent(model.transform, parent, "Parent Visual Model");
            Undo.RecordObject(model.transform, "Restore Authored Model Pose");
            model.transform.SetLocalPositionAndRotation(settings.Prefab.transform.localPosition, settings.Prefab.transform.localRotation);
            model.transform.localScale = settings.Prefab.transform.localScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            return model;
        }

        /// <summary>Stores explicit ownership and the authored pose without exposing runtime mutation helpers.</summary>
        /// <param name="target">Binding being created or receiving a replacement model.</param>
        /// <param name="root">Transform receiving the offset.</param>
        /// <param name="model">Managed model identity.</param>
        /// <param name="prefab">Source asset, or null for adoption.</param>
        /// <param name="ownsContainer">Whether the tool created a separate container.</param>
        private void WriteBinding(PlayerVisualBinding target, Transform root, GameObject model, GameObject prefab, bool ownsContainer)
        {
            // Existing children retain their pose as a separate baseline; offsets compose before it.
            using SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty("host").objectReferenceValue = host;
            serialized.FindProperty("visualRoot").objectReferenceValue = root;
            serialized.FindProperty("model").objectReferenceValue = model;
            serialized.FindProperty("sourcePrefab").objectReferenceValue = prefab;
            serialized.FindProperty("ownsContainer").boolValue = ownsContainer;
            serialized.FindProperty("basePosition").vector3Value = ownsContainer ? Vector3.zero : root.localPosition;
            serialized.FindProperty("baseRotation").quaternionValue = ownsContainer ? Quaternion.identity : root.localRotation;
            serialized.FindProperty("baseScale").vector3Value = ownsContainer ? Vector3.one : root.localScale;
            serialized.ApplyModifiedProperties();
        }

        #endregion

        #endregion
    }
}
