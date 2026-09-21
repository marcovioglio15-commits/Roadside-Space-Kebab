using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Creates and removes hover components and their dedicated UI only through explicit editor actions.</summary>
    internal static class HoverAuthoring
    {
        #region Methods

        #region Interactions

        /// <summary>Adds an independent hover interaction to the selected prefab stage or scene object.</summary>
        /// <param name="root">Editable object receiving the new component and UI child.</param>
        /// <returns>The newly configured hover interaction.</returns>
        internal static ObjectHover AddHover(GameObject root)
        {
            // One Undo group restores the complete hierarchy and binding.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add object hover");
            ObjectHover hover = Undo.AddComponent<ObjectHover>(root);
            using (SerializedObject data = new SerializedObject(hover))
            {
                data.FindProperty("interactionName").stringValue = "First Person Hover";
                data.ApplyModifiedProperties();
            }
            CreateLabel(hover);
            Undo.CollapseUndoOperations(group);
            return hover;
        }

        /// <summary>Removes an interaction and its unshared dedicated label as one reversible action.</summary>
        /// <param name="hover">Interaction selected in the workspace.</param>
        internal static void RemoveHover(ObjectHover hover)
        {
            // Never remove unrelated or shared UI after a manually edited binding.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            HoverLabel label = hover.Label;
            bool removeLabel = label != null && label.transform != hover.transform && label.transform.IsChildOf(hover.transform);
            foreach (ObjectHover other in hover.transform.root.GetComponentsInChildren<ObjectHover>(true))
                if (other != hover && other.Label == label)
                    removeLabel = false;
            // Capture the component while its label reference is still valid for Undo restoration.
            Undo.DestroyObjectImmediate(hover);
            if (removeLabel)
                Undo.DestroyObjectImmediate(label.gameObject);
            Undo.CollapseUndoOperations(group);
        }

        #endregion

        #region UI Construction

        /// <summary>Authors the full overlay before Play and assigns its references to an existing hover.</summary>
        /// <param name="hover">Interaction missing its dedicated label.</param>
        internal static void CreateLabel(ObjectHover hover)
        {
            // Existing authored UI is never replaced by a setup button.
            if (hover.Label != null)
                return;
            RectTransform root = CreateRect("Hover UI", hover.transform);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            canvas.enabled = false;
            RectTransform panel = CreateRect("Label", root);
            panel.sizeDelta = new Vector2(280f, 64f);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.04f, 0.06f, 0.08f, 0.88f);
            background.raycastTarget = false;

            // Text and background are existing native graphics; no runtime builder is necessary.
            RectTransform textRect = CreateRect("Text", panel);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);
            Text text = textRect.gameObject.AddComponent<Text>();
            text.text = hover.gameObject.name;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            HoverLabel label = root.gameObject.AddComponent<HoverLabel>();
            using (SerializedObject data = new SerializedObject(label))
            {
                data.FindProperty("canvas").objectReferenceValue = canvas;
                data.FindProperty("panel").objectReferenceValue = panel;
                data.FindProperty("text").objectReferenceValue = text;
                data.FindProperty("background").objectReferenceValue = background;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            // Register the completed child before recording the component's reference change.
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create hover UI");
            using (SerializedObject data = new SerializedObject(hover))
            {
                data.FindProperty("label").objectReferenceValue = label;
                data.ApplyModifiedProperties();
            }
        }

        /// <summary>Creates a centered rectangle under an existing editor-owned hierarchy.</summary>
        /// <param name="name">Hierarchy name assigned to the new object.</param>
        /// <param name="parent">Existing parent in a scene or prefab stage.</param>
        /// <returns>The new layout rectangle.</returns>
        private static RectTransform CreateRect(string name, Transform parent)
        {
            // Unity's UI layer remains local to the authored graphics.
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        #endregion

        #region Observer

        /// <summary>Connects an existing scene camera and tags an explicitly selected player root.</summary>
        /// <param name="view">Gameplay camera to observe.</param>
        /// <param name="player">Scene player root selected by the user.</param>
        /// <param name="tag">Existing project tag used for runtime discovery.</param>
        /// <param name="existing">Optional observer to reconnect without adding a second scene observer.</param>
        /// <returns>The connected observer, ready to be saved with the scene.</returns>
        internal static HoverObserver SetupObserver(Camera view, GameObject player, string tag, HoverObserver existing = null)
        {
            // This changes only the explicitly chosen camera and player; Undo restores both.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set up hover observer");
            Undo.RecordObject(player, "Assign player tag");
            player.tag = tag;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            HoverObserver observer = existing != null ? existing : view.GetComponent<HoverObserver>();
            if (observer == null)
                observer = Undo.AddComponent<HoverObserver>(view.gameObject);
            using (SerializedObject data = new SerializedObject(observer))
            {
                data.FindProperty("view").objectReferenceValue = view;
                data.FindProperty("playerTag").stringValue = tag;
                data.ApplyModifiedProperties();
            }
            Undo.CollapseUndoOperations(group);
            return observer;
        }

        #endregion

        #endregion
    }
}
