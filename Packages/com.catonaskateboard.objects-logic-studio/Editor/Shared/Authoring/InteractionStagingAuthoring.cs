using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors reusable inactive staging roots for controlled prefab activation.</summary>
    internal static class InteractionStagingAuthoring
    {
        #region Methods

        #region Authoring

        /// <summary>Creates or reconnects a staging child inside the caller's existing prefab Undo transaction.</summary>
        /// <param name="feature">Assembly station or spawn rule exposing a serialized staging reference.</param>
        /// <param name="name">Purpose-specific name for a newly required child.</param>
        internal static void Prepare(ObjectInteraction feature, string name)
        {
            // Staging is authored once; no empty helper GameObject is created during gameplay.
            using SerializedObject data = new SerializedObject(feature);
            SerializedProperty property = data.FindProperty("staging");
            Transform staging = property.objectReferenceValue as Transform;
            if (staging == null || staging == feature.transform || !staging.IsChildOf(feature.transform))
            {
                GameObject created = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(created, "Create interaction staging");
                Undo.SetTransformParent(created.transform, feature.transform, "Parent interaction staging");
                staging = created.transform;
                staging.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                property.objectReferenceValue = staging;
                data.ApplyModifiedProperties();
            }
            Undo.RecordObject(staging.gameObject, "Prepare interaction staging");
            staging.gameObject.SetActive(false);
        }

        #endregion

        #endregion
    }
}
