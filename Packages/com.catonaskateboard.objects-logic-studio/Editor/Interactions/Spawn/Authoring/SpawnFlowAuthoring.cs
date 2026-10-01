using CatOnASkateboard.MenuStudio;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Prepares the persistent manager's black overlay entirely in the editor.</summary>
    internal static class SpawnFlowAuthoring
    {
        #region Methods
        #region Authoring

        /// <summary>Creates a missing fade canvas inside the existing Apply Undo transaction.</summary>
        /// <param name="manager">Day-flow component receiving an authored overlay.</param>
        internal static void Prepare(ObjectSpawnManager manager)
        {
            // Existing valid overlays retain their scene layout and sorting configuration.
            using SerializedObject data = new SerializedObject(manager);
            SerializedProperty reference = data.FindProperty("transition");
            if (reference.objectReferenceValue is MenuSceneTransition existing && existing.Overlay != null)
                return;
            GameObject root = new GameObject("Day Transition", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster), typeof(MenuSceneTransition));
            Undo.RegisterCreatedObjectUndo(root, "Create day transition");
            Undo.SetTransformParent(root.transform, manager.transform, "Parent day transition");
            root.layer = 5;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            GameObject black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(black, "Create black overlay");
            Undo.SetTransformParent(black.transform, root.transform, "Parent black overlay");
            black.layer = 5;
            RectTransform rectangle = black.GetComponent<RectTransform>();
            rectangle.anchorMin = Vector2.zero;
            rectangle.anchorMax = Vector2.one;
            rectangle.offsetMin = rectangle.offsetMax = Vector2.zero;
            black.GetComponent<Image>().color = Color.black;
            MenuSceneTransition transition = root.GetComponent<MenuSceneTransition>();
            transition.Overlay = group;
            reference.objectReferenceValue = transition;
            data.ApplyModifiedProperties();
        }

        #endregion
        #endregion
    }
}
