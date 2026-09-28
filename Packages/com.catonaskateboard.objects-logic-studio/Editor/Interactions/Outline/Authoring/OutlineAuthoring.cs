using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Collects original renderers and prepares the shared pipeline effect before Play.</summary>
    internal static class OutlineAuthoring
    {
        #region Methods

        #region Setup

        /// <summary>Refreshes original source references without creating shell geometry.</summary>
        /// <param name="outline">Outline in a prefab workspace.</param>
        internal static void Rebuild(ObjectOutline outline)
        {
            // Nested items keep their independent interactions until explicitly joined by assembly.
            List<Renderer> sources = new List<Renderer>();
            ObjectItem owner = outline.GetComponent<ObjectItem>();
            foreach (Renderer source in outline.GetComponentsInChildren<Renderer>(true))
                if (source is MeshRenderer or SkinnedMeshRenderer && source.GetComponentInParent<ObjectItem>(true) == owner)
                    sources.Add(source);
            using (SerializedObject data = new SerializedObject(outline))
            {
                SerializedProperty renderers = data.FindProperty("renderers");
                renderers.arraySize = sources.Count;
                for (int index = 0; index < sources.Count; index++)
                    renderers.GetArrayElementAtIndex(index).objectReferenceValue = sources[index];
                data.ApplyModifiedProperties();
            }
            OutlinePipelineAuthoring.Prepare();
        }

        #endregion

        #endregion
    }
}
