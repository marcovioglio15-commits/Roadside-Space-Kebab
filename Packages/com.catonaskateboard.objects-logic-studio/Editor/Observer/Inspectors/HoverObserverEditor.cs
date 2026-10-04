using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
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
            StudioGUI.PropertyField(serializedObject.FindProperty("view"));
            SerializedProperty flag = serializedObject.FindProperty("playerFlag");
            ObjectFlagSelector.Draw(flag);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                StudioGUI.PropertyField(serializedObject.FindProperty("dialogueHud"));
            serializedObject.ApplyModifiedProperties();
            HoverObserver observer = (HoverObserver)target;
            if (!Application.isPlaying && !EditorUtility.IsPersistent(observer) && observer.DialogueHud == null
                && StudioButton.Draw(new GUIContent("Create Shared Dialogue HUD", "Create one overlay for all dialogues before Play.")))
                DialogueAuthoring.CreateHud(observer);
            if (Application.isPlaying && StudioButton.Draw(new GUIContent("Refresh Context", "Reacquire the camera and flagged player after an explicit binding change.")))
                observer.RefreshContext();
            if (observer.Warning.Length > 0)
                EditorGUILayout.HelpBox(observer.Warning, MessageType.Warning);
        }

        #endregion

        #endregion
    }
}
