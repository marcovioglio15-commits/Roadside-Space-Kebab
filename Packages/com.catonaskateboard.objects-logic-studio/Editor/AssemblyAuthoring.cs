using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors the small prefab dependency used to prepare products before their first activation.</summary>
    internal static class AssemblyAuthoring
    {
        #region Methods

        #region Authoring

        /// <summary>Creates or reconnects an inactive staging child through the existing prefab Undo transaction.</summary>
        /// <param name="station">Editable table component.</param>
        internal static void Prepare(ObjectAssemblyStation station)
        {
            // No camera, scene or gameplay object is loaded when the tool opens this configuration.
            using SerializedObject data = new SerializedObject(station);
            SerializedProperty property = data.FindProperty("staging");
            Transform staging = property.objectReferenceValue as Transform;
            if (staging == null || staging == station.transform || !staging.IsChildOf(station.transform))
            {
                GameObject created = new GameObject("Assembly Staging");
                Undo.RegisterCreatedObjectUndo(created, "Create assembly staging");
                Undo.SetTransformParent(created.transform, station.transform, "Parent assembly staging");
                staging = created.transform;
                staging.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                property.objectReferenceValue = staging;
                data.ApplyModifiedProperties();
            }
            Undo.RecordObject(staging.gameObject, "Prepare assembly staging");
            staging.gameObject.SetActive(false);
        }

        #endregion

        #endregion
    }
}
