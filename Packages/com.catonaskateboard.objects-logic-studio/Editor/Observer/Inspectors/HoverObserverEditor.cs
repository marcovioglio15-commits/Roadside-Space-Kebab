using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Exposes the observer's camera and a project flag selector without unrelated input options.</summary>
    [CustomEditor(typeof(HoverObserver))]
    internal sealed class HoverObserverEditor : UnityEditor.Editor
    {
        #region Methods

        #region Inspector

        /// <summary>Draws context bindings and an explicit refresh action during Play.</summary>
        public override void OnInspectorGUI()
        {
            using ObjectStudioFieldLayout layout = new ObjectStudioFieldLayout(205f);
            // The shared selector provides search, groups and new flag creation.
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("view"));
            SerializedProperty flag = serializedObject.FindProperty("playerFlag");
            ObjectFlagSelector.Draw(flag);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueHud"));
            serializedObject.ApplyModifiedProperties();
            HoverObserver observer = (HoverObserver)target;
            if (!Application.isPlaying && !EditorUtility.IsPersistent(observer) && observer.DialogueHud == null
                && GUILayout.Button(new GUIContent("Create Shared Dialogue HUD", "Create one overlay for all dialogues before Play.")))
                DialogueAuthoring.CreateHud(observer);
            if (Application.isPlaying && GUILayout.Button(new GUIContent("Refresh Context", "Reacquire the camera and flagged player after an explicit binding change.")))
                observer.RefreshContext();
            if (observer.Warning.Length > 0)
                EditorGUILayout.HelpBox(observer.Warning, MessageType.Warning);
        }

        #endregion

        #endregion
    }
}
