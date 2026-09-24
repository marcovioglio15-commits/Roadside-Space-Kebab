using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Builds prefab-local outline shells without modifying original meshes or surface materials.</summary>
    internal static class OutlineAuthoring
    {
        #region Methods

        #region Geometry

        /// <summary>Replaces this component's authored shells after renderer or mesh changes.</summary>
        /// <param name="outline">Outline inside the native prefab workspace.</param>
        internal static void Rebuild(ObjectOutline outline)
        {
            // Delete only shells explicitly owned by this outline before collecting original renderers.
            Clear(outline);
            List<OutlineBinding> bindings = new List<OutlineBinding>();
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.catonaskateboard.objects-logic-studio/Runtime/Shaders/"
                + (outline.Settings.ThroughWalls ? "OutlineThroughWalls.mat" : "Outline.mat"));
            foreach (Renderer source in outline.GetComponentsInChildren<Renderer>(true))
            {
                if (source.GetComponentInParent<ObjectItem>() != outline.GetComponent<ObjectItem>())
                    continue;
                OutlineBinding binding = OutlineGeometry.Create(source, material);
                if (binding == null)
                    continue;
                bindings.Add(binding);
                Undo.RegisterCreatedObjectUndo(binding.Shell.gameObject, "Create outline shell");
            }
            using (SerializedObject data = new SerializedObject(outline))
            {
                data.FindProperty("occludedMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(
                    "Packages/com.catonaskateboard.objects-logic-studio/Runtime/Shaders/Outline.mat");
                data.FindProperty("throughWallsMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(
                    "Packages/com.catonaskateboard.objects-logic-studio/Runtime/Shaders/OutlineThroughWalls.mat");
                SerializedProperty array = data.FindProperty("bindings");
                array.arraySize = bindings.Count;
                for (int index = 0; index < bindings.Count; index++)
                {
                    SerializedProperty element = array.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("Source").objectReferenceValue = bindings[index].Source;
                    element.FindPropertyRelative("Shell").objectReferenceValue = bindings[index].Shell;
                    element.FindPropertyRelative("SourceMesh").objectReferenceValue = bindings[index].SourceMesh;
                    element.FindPropertyRelative("ShellMesh").objectReferenceValue = bindings[index].ShellMesh;
                }
                data.ApplyModifiedProperties();
            }
            OutlineGeometry.UpdateLods(outline.transform.root.gameObject, bindings, true, RecordLodChange);
        }

        /// <summary>Records the original LOD membership before changing authored shell references.</summary>
        /// <param name="group">LOD group about to receive updated renderer lists.</param>
        private static void RecordLodChange(LODGroup group)
        {
            // Runtime composition uses the same geometry helper without editor dependencies.
            Undo.RecordObject(group, "Update outline LOD geometry");
        }

        /// <summary>Removes owned shell children while preserving all original renderers.</summary>
        /// <param name="outline">Component whose outline geometry is being rebuilt or removed.</param>
        internal static void Clear(ObjectOutline outline)
        {
            // Ownership and hierarchy checks prevent stale references from deleting another object's geometry.
            foreach (OutlineBinding binding in outline.Bindings)
                if (binding?.Shell != null && binding.Shell != binding.Source
                    && binding.Shell.transform.IsChildOf(outline.transform) && binding.Shell.gameObject != outline.gameObject)
                    Undo.DestroyObjectImmediate(binding.Shell.gameObject);
            OutlineGeometry.UpdateLods(outline.transform.root.gameObject, outline.Bindings, false, RecordLodChange);
        }

        #endregion

        #endregion
    }
}
