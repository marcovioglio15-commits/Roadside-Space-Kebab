using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Limits the initial visual binding to static geometry and checks destructive replacement before Apply.</summary>
    internal static class PlayerVisualModelValidation
    {
        #region Methods

        #region Geometry

        /// <summary>Checks an entire model before previewing or instantiating any part of it.</summary>
        /// <param name="model">Prefab root or existing scene child.</param>
        /// <param name="warning">Receives the first unsupported component or mesh.</param>
        /// <returns>True for a model containing only supported static rendering components.</returns>
        public static bool TryValidate(GameObject model, out string warning)
        {
            // A null model never becomes an invisible successful binding.
            warning = string.Empty;
            if (model == null)
            {
                warning = "Choose a static visual prefab or a direct child of the player.";
                return false;
            }

            bool hasRenderer = false;
            foreach (Component component in model.GetComponentsInChildren<Component>(true))
            {
                // Restrict the first contract explicitly; no scripts are executed to build a preview.
                switch (component)
                {
                    case Transform transform:
                        if (!IsFinite(transform.localPosition) || !IsFinite(transform.localEulerAngles) || !IsFinite(transform.localScale)
                            || transform.localScale.x <= 0f || transform.localScale.y <= 0f || transform.localScale.z <= 0f)
                            warning = "Visual transforms require finite positions and positive scales.";
                        else
                            TryValidateMatrix(Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale), out warning);
                        break;
                    case MeshFilter filter:
                        if (filter.sharedMesh == null)
                            warning = "A Visual MeshFilter has no mesh.";
                        break;
                    case MeshRenderer renderer:
                        hasRenderer = true;
                        if (!renderer.TryGetComponent(out MeshFilter mesh) || mesh.sharedMesh == null
                            || renderer.additionalVertexStreams != null)
                            warning = "Visual preview requires a MeshFilter and does not support additional vertex streams yet.";
                        break;
                    default:
                        warning = "Static Visual supports Transform, MeshFilter and MeshRenderer only. Unsupported component: "
                            + (component != null ? component.GetType().Name : "Missing Script") + ".";
                        break;
                }
                if (warning.Length > 0)
                    return false;
            }

            if (!hasRenderer)
                warning = "The visual needs at least one MeshRenderer.";
            return warning.Length == 0;
        }

        /// <summary>Rejects overflow and collapsed axes after composing otherwise finite offset values.</summary>
        /// <param name="matrix">Complete proposed model-to-world matrix.</param>
        /// <param name="warning">Receives a composition warning without adjusting the entered values.</param>
        /// <returns>True when the composed pose remains finite and invertible.</returns>
        public static bool TryValidateMatrix(Matrix4x4 matrix, out string warning)
        {
            // Very large or tiny finite inputs can still produce an unusable transform after multiplication.
            warning = "The composed Visual pose overflows or collapses an axis. Review the offset, model and player scales.";
            for (int index = 0; index < 16; index++)
                if (!float.IsFinite(matrix[index]))
                    return false;
            if (!float.IsFinite(matrix.determinant) || matrix.determinant == 0f)
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks vector values before their matrices reach preview or Transform setters.</summary>
        /// <param name="value">Local position or scale.</param>
        /// <returns>True when all three values are finite.</returns>
        private static bool IsFinite(Vector3 value)
        {
            // Invalid data remains untouched for correction in its original asset or object.
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        #endregion

        #region Ownership

        /// <summary>Checks explicit binding references and refuses containers holding unrelated content.</summary>
        /// <param name="binding">Existing managed visual.</param>
        /// <param name="warning">Receives a hierarchy or ownership warning.</param>
        /// <returns>True when the binding still owns exactly the recorded hierarchy.</returns>
        public static bool TryValidateBinding(PlayerVisualBinding binding, out string warning)
        {
            // Do not infer ownership from names or repair missing links silently.
            warning = string.Empty;
            if (binding.Host == null || binding.Host.gameObject != binding.gameObject || binding.VisualRoot == null
                || binding.VisualRoot.parent != binding.transform || binding.Model == null)
                warning = "The Visual binding hierarchy changed. Restore its references before applying.";
            else if (binding.OwnsContainer
                ? binding.VisualRoot.childCount != 1 || binding.Model.transform.parent != binding.VisualRoot
                    || binding.VisualRoot.GetComponents<Component>().Length != 1
                : binding.Model.transform != binding.VisualRoot)
                warning = "The Visual container contains unrelated content or its model was moved. Apply was cancelled.";

            return warning.Length == 0;
        }

        /// <summary>Rejects replacement of adopted objects or references that deletion would break.</summary>
        /// <param name="binding">Binding whose current model would be removed.</param>
        /// <param name="warning">Receives the reason replacement would lose data.</param>
        /// <returns>True when the requested prefab replacement has no unrelated references.</returns>
        public static bool TryValidateReplacement(PlayerVisualBinding binding, out string warning)
        {
            // An adopted object belongs to the project, not to the replacement operation.
            warning = string.Empty;
            if (binding.SourcePrefab == null || !PrefabUtility.IsAnyPrefabInstanceRoot(binding.Model))
            {
                warning = "The current visual is adopted. Release it before choosing a replacement prefab.";
                return false;
            }
            return TryValidateRemoval(binding, out warning);
        }

        /// <summary>Checks references before an explicitly requested model removal or replacement.</summary>
        /// <param name="binding">Binding identifying the exact visual subtree.</param>
        /// <param name="warning">Receives the first reference that would become missing.</param>
        /// <returns>True when no unrelated loaded component refers into the model.</returns>
        public static bool TryValidateRemoval(PlayerVisualBinding binding, out string warning)
        {
            // A preset source removal authorizes deleting this branch, but not breaking unrelated references.
            warning = string.Empty;
            // Scene references are inspected once per replacement, never during preview repaint.
            foreach (Component component in Object.FindObjectsByType<Component>(FindObjectsInactive.Include))
            {
                if (component == null || component is Transform || component == binding || component.transform.IsChildOf(binding.VisualRoot)
                    || EditorUtility.IsPersistent(component) || !component.gameObject.scene.IsValid())
                    continue;

                using SerializedObject serialized = new SerializedObject(component);
                using SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && ReferencesModel(property.objectReferenceValue, binding.Model.transform))
                    {
                        warning = "The current visual is referenced by " + component.name + " / " + property.propertyPath
                            + ". Release the visual to keep that object instead of replacing it.";
                        return false;
                    }
            }
            return true;
        }

        /// <summary>Identifies references into the managed model without relying on field names.</summary>
        /// <param name="value">Serialized object reference being examined.</param>
        /// <param name="root">Model proposed for deletion.</param>
        /// <returns>True when the reference targets that model or one of its components.</returns>
        private static bool ReferencesModel(Object value, Transform root)
        {
            // Both GameObject and component references can be broken by deleting a hierarchy.
            return value switch
            {
                GameObject gameObject => gameObject.transform.IsChildOf(root),
                Component component => component.transform.IsChildOf(root),
                _ => false
            };
        }

        #endregion

        #region Conflicts

        /// <summary>Captures component values and hierarchy identity for a later synchronous conflict check.</summary>
        /// <param name="root">Visual subtree to capture, or null for no subtree.</param>
        /// <returns>A comparison string used only by the Editor session.</returns>
        public static string Capture(GameObject root)
        {
            // Captures occur on selection, source changes and Apply, not on each rendering frame.
            if (root == null)
                return string.Empty;

            StringBuilder result = new StringBuilder();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component != null)
                {
                    // Compare scene identity and authored geometry, not native IDs recreated after Play.
                    result.Append(PlayerStudioSceneReference.Capture(component)).Append(component.GetType().FullName)
                        .Append(component.gameObject.activeSelf).Append(component.gameObject.layer).Append(component.gameObject.name);
                    switch (component)
                    {
                        case Transform transform:
                            result.Append(PlayerStudioSceneReference.Capture(transform.parent)).Append(transform.GetSiblingIndex())
                                .Append(transform.localPosition.ToString("R")).Append(transform.localRotation.ToString("R"))
                                .Append(transform.localScale.ToString("R"));
                            break;
                        case MeshFilter filter:
                            result.Append(PlayerStudioSceneReference.Capture(filter.sharedMesh));
                            break;
                        case MeshRenderer renderer:
                            result.Append(renderer.enabled);
                            foreach (Material material in renderer.sharedMaterials)
                                result.Append(PlayerStudioSceneReference.Capture(material));
                            break;
                    }
                }
                else
                    result.Append("Missing Script");
            return result.ToString();
        }

        /// <summary>Captures binding ownership in a form independent of native instance recreation.</summary>
        /// <param name="binding">Binding to compare after outside edits or a Play round trip.</param>
        /// <returns>Stable ownership and authored-pose signature, or empty for no binding.</returns>
        public static string CaptureBinding(PlayerVisualBinding binding)
        {
            // Only fields written by the visual operation belong to this conflict baseline.
            return binding != null ? PlayerStudioSceneReference.Capture(binding.Host)
                + PlayerStudioSceneReference.Capture(binding.VisualRoot) + PlayerStudioSceneReference.Capture(binding.Model)
                + PlayerStudioSceneReference.Capture(binding.SourcePrefab) + binding.OwnsContainer + binding.BaseMatrix.ToString("R") : string.Empty;
        }

        #endregion

        #endregion
    }
}
