using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Converts between preset offsets and the actual model pivot without moving scene objects.</summary>
    internal static class PlayerVisualPose
    {
        #region Methods

        #region Composition

        /// <summary>Resolves the authored model pose that follows the editable offset.</summary>
        /// <param name="scene">Current scene proposal.</param>
        /// <param name="draft">Source chosen in the preset draft.</param>
        /// <returns>The model pose relative to the offset frame.</returns>
        public static Matrix4x4 Authored(PlayerVisualSceneSession scene, PlayerVisualDraft draft)
        {
            // An existing binding retains the original pose even after scene manipulation.
            if (scene.Binding != null && scene.Binding.Model != null && scene.Binding.VisualRoot != null
                && scene.Binding.SourcePrefab == draft.Prefab)
                return scene.Binding.OwnsContainer
                    ? Local(scene.Binding.SourcePrefab.transform) : scene.Binding.BaseMatrix;
            return draft.Prefab != null ? Local(draft.Prefab.transform) : scene.AdoptedMatrix;
        }

        /// <summary>Builds the local matrix of a model or its authored prefab root.</summary>
        /// <param name="transform">Transform whose local values are read.</param>
        /// <returns>The transform relative to its parent.</returns>
        private static Matrix4x4 Local(Transform transform)
        {
            // No temporary Transform is needed for preview or conversion.
            return Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
        }

        #endregion

        #region Handles

        /// <summary>Edits around the visible model pivot, then converts the result back to an offset.</summary>
        /// <param name="draft">Values that receive the new offset after a handle changes.</param>
        /// <param name="authored">Retained model pose after the offset.</param>
        /// <param name="playerMatrix">Proposed player frame in the scene.</param>
        /// <param name="tool">Move, Rotate or uniform Scale.</param>
        /// <returns>True when a native handle changed the proposal.</returns>
        public static bool DrawHandles(ref PlayerVisualDraft draft, Matrix4x4 authored, Matrix4x4 playerMatrix, int tool)
        {
            // Invalid numbers stay in their fields and never reach a native handle.
            if (!draft.TryGetSettings(out PlayerVisualSettings settings, out _))
                return false;
            Vector3 pivot = settings.Position + settings.Rotation * ((Vector3)authored.GetColumn(3) * settings.Scale);
            Quaternion rotation = settings.Rotation;
            float scale = settings.Scale;
            using (new Handles.DrawingScope(playerMatrix))
            {
                EditorGUI.BeginChangeCheck();
                switch (tool)
                {
                    case 0:
                        pivot = Handles.PositionHandle(pivot, rotation * authored.rotation);
                        break;
                    case 1:
                        rotation = Handles.RotationHandle(rotation * authored.rotation, pivot) * Quaternion.Inverse(authored.rotation);
                        break;
                    case 2:
                        scale = Handles.ScaleValueHandle(scale, pivot, rotation * authored.rotation,
                            HandleUtility.GetHandleSize(pivot), Handles.CubeHandleCap, 0f);
                        break;
                }
                if (!EditorGUI.EndChangeCheck())
                    return false;
            }

            // Rotation and scaling keep the model pivot fixed instead of orbiting the player's origin.
            draft.SetOffset(pivot - rotation * ((Vector3)authored.GetColumn(3) * scale),
                tool == 1 ? rotation.eulerAngles : draft.EulerAngles, scale);
            return true;
        }

        #endregion

        #endregion
    }
}
