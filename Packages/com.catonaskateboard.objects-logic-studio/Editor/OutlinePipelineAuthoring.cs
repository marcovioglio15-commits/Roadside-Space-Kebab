using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors the shared edge-glow feature on the project's URP renderer assets.</summary>
    internal static class OutlinePipelineAuthoring
    {
        #region Methods

        #region Pipeline Setup

        /// <summary>Adds the package pass once per renderer while preserving its existing features.</summary>
        internal static void Prepare()
        {
            // Asset discovery occurs only during explicit outline authoring.
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                bool exists = false;
                foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
                    exists |= feature is OutlineRendererFeature;
                if (exists)
                    continue;
                OutlineRendererFeature created = ScriptableObject.CreateInstance<OutlineRendererFeature>();
                created.name = "Objects Logic Studio Edge Glow";
                using (SerializedObject data = new SerializedObject(created))
                {
                    data.FindProperty("material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(
                        "Packages/com.catonaskateboard.objects-logic-studio/Runtime/Shaders/Outline.mat");
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                Undo.RegisterCreatedObjectUndo(created, "Add object edge glow");
                Undo.RecordObject(renderer, "Add object edge glow");
                AssetDatabase.AddObjectToAsset(created, renderer);
                renderer.rendererFeatures.Add(created);
                // Keep Unity's subasset recovery map aligned with the feature list at the same write boundary.
                using (SerializedObject data = new SerializedObject(renderer))
                {
                    SerializedProperty map = data.FindProperty("m_RendererFeatureMap");
                    map.arraySize = renderer.rendererFeatures.Count;
                    for (int index = 0; index < map.arraySize; index++)
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[index], out string _, out long identity))
                            map.GetArrayElementAtIndex(index).longValue = identity;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                renderer.SetDirty();
                EditorUtility.SetDirty(renderer);
                AssetDatabase.SaveAssetIfDirty(renderer);
            }
        }

        #endregion

        #endregion
    }
}
