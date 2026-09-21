using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Exposes the observer's camera and a project tag selector without unrelated input options.</summary>
    [CustomEditor(typeof(HoverObserver))]
    internal sealed class HoverObserverEditor : UnityEditor.Editor
    {
        #region Methods

        #region Inspector

        /// <summary>Draws context bindings and an explicit refresh action during Play.</summary>
        public override void OnInspectorGUI()
        {
            // TagField prevents new authoring from producing undefined tags.
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("view"));
            SerializedProperty tag = serializedObject.FindProperty("playerTag");
            tag.stringValue = EditorGUILayout.TagField(new GUIContent(tag.displayName, tag.tooltip), tag.stringValue);
            serializedObject.ApplyModifiedProperties();
            HoverObserver observer = (HoverObserver)target;
            if (Application.isPlaying && GUILayout.Button(new GUIContent("Refresh Context", "Reacquire the camera and tagged player after an explicit binding change.")))
                observer.RefreshContext();
            if (observer.Warning.Length > 0)
                EditorGUILayout.HelpBox(observer.Warning, MessageType.Warning);
        }

        #endregion

        #endregion
    }
}
