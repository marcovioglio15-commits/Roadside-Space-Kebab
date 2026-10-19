using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatOnASkateboard.MenuStudio.Editor
{
    /// <summary>Authors Play's standalone controller and black panel on the menu's existing Canvas.</summary>
    public static class MenuTransitionAuthoring
    {
        #region Methods

        #region Authoring

        /// <summary>Connects a saved main-menu host without creating another Canvas or changing its pages.</summary>
        /// <param name="host">Scene menu using a saved fade profile.</param>
        public static void Connect(MenuHost host)
        {
            // Pause scenes retain their existing independent loading and UI setup.
            if (host == null || host.Kind != MenuKind.Main || host.Preset == null || host.Preset.PlayFade == null)
                return;
            if (host.PlayTransition == null)
            {
                GameObject controller = new GameObject("Menu Play Transition");
                SceneManager.MoveGameObjectToScene(controller, host.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(controller, "Connect Play Fade");
                Undo.RecordObject(host, "Connect Play Fade");
                host.PlayTransition = Undo.AddComponent<MenuSceneTransition>(controller);
                PrefabUtility.RecordPrefabInstancePropertyModifications(host);
                EditorUtility.SetDirty(host);
            }
            // Prefer the existing authored panel; only a missing panel needs editor-time creation.
            MenuTransitionOverlay overlay = host.GetComponentInChildren<MenuTransitionOverlay>(true);
            if (overlay == null)
            {
                GameObject panel = new GameObject("Transition Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                Undo.RegisterCreatedObjectUndo(panel, "Create Play Fade Panel");
                panel.transform.SetParent(host.transform, false);
                RectTransform rect = (RectTransform)panel.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                panel.GetComponent<Image>().color = Color.black;
                panel.GetComponent<CanvasGroup>().alpha = 0f;
                panel.GetComponent<CanvasGroup>().blocksRaycasts = false;
                panel.AddComponent<MenuTransitionOverlay>();
            }
            EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
        }

        #endregion

        #endregion
    }
}
