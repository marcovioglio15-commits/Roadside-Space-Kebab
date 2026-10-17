using CatOnASkateboard.MenuStudio;
using CatOnASkateboard.PlayerStudio;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors a single scene Canvas and reusable panels through explicit editor actions.</summary>
    internal static class ObjectSceneHudAuthoring
    {
        #region Methods

        #region Scene Tools

        /// <summary>Shows the current scene HUD or creates its complete hierarchy before Play.</summary>
        /// <param name="observer">Scene or prefab observer being configured.</param>
        internal static void Draw(HoverObserver observer)
        {
            // Prefab observers discover scene UI at runtime, so their assets contain no graphics.
            Scene scene = observer != null && !EditorUtility.IsPersistent(observer)
                ? observer.gameObject.scene : SceneManager.GetActiveScene();
            using (new EditorGUI.DisabledScope(!scene.IsValid() || !scene.isLoaded || PrefabStageUtility.GetCurrentPrefabStage() != null))
            {
                ObjectSceneHud hud = Find(scene);
                if (!StudioButton.Draw(new GUIContent(hud != null ? "Select Scene HUD" : "Create Scene HUD",
                    "Use one saved Canvas for Hover, Dialogue, Assembly and the centered cursor. Keep it in the gameplay scene.")))
                    return;
                if (hud == null)
                {
                    hud = Build();
                    SceneManager.MoveGameObjectToScene(hud.gameObject, scene);
                    Undo.RegisterCreatedObjectUndo(hud.gameObject, "Create shared scene HUD");
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                Selection.activeObject = hud.gameObject;
                EditorGUIUtility.PingObject(hud);
            }
        }

        /// <summary>Finds an already authored HUD only within the requested scene.</summary>
        /// <param name="scene">Loaded gameplay scene.</param>
        /// <returns>The existing HUD, or null when setup is still required.</returns>
        internal static ObjectSceneHud Find(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ObjectSceneHud hud = root.GetComponentInChildren<ObjectSceneHud>(true);
                if (hud != null)
                    return hud;
            }
            return null;
        }

        #endregion

        #region Construction

        /// <summary>Builds editable shared UI; callers save it in a scene or reusable scene-UI prefab.</summary>
        /// <returns>The complete scene HUD with one Canvas and all runtime references assigned.</returns>
        internal static ObjectSceneHud Build()
        {
            RectTransform root = ObjectUiAuthoring.CreateRect("Gameplay UI", null);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            HoverLabel hover = HoverAuthoring.BuildLabel(root, canvas);
            DialogueHud dialogue = DialogueAuthoring.Build(root, canvas);
            AssemblyChargeHud assembly = AssemblyHudAuthoring.Build(root, canvas);
            RectTransform pointer = ObjectUiAuthoring.CreateRect("Centered Cursor", root);
            pointer.gameObject.AddComponent<CanvasRenderer>();
            PlayerCenteredCursor cursor = pointer.gameObject.AddComponent<PlayerCenteredCursor>();
            cursor.raycastTarget = false;
            CreateTransition(root);

            // References are serialized now; runtime never repairs or constructs the UI hierarchy.
            ObjectSceneHud hud = root.gameObject.AddComponent<ObjectSceneHud>();
            using SerializedObject data = new SerializedObject(hud);
            data.FindProperty("hover").objectReferenceValue = hover;
            data.FindProperty("dialogue").objectReferenceValue = dialogue;
            data.FindProperty("assembly").objectReferenceValue = assembly;
            data.FindProperty("cursor").objectReferenceValue = cursor;
            data.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        /// <summary>Adds the shared black panel above existing content without another Canvas.</summary>
        /// <param name="parent">Existing scene Canvas or a full-screen branch beneath it.</param>
        /// <returns>The authored transition panel used by persistent day controllers.</returns>
        internal static MenuTransitionOverlay CreateTransition(Transform parent)
        {
            RectTransform black = ObjectUiAuthoring.CreateRect("Scene Fade", parent);
            ObjectUiAuthoring.Stretch(black);
            Image image = black.gameObject.AddComponent<Image>();
            image.color = Color.black;
            CanvasGroup group = black.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            return black.gameObject.AddComponent<MenuTransitionOverlay>();
        }

        #endregion

        #endregion
    }
}
