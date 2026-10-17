using CatOnASkateboard.StudioIdentity.Editor;
using CatOnASkateboard.StudioIdentity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors object-local hover configuration and one reusable scene label.</summary>
    internal static class HoverAuthoring
    {
        #region Methods

        #region Interactions

        /// <summary>Adds an independent hover interaction to the selected prefab stage and saves its asset.</summary>
        /// <param name="root">Editable object receiving the new hover component.</param>
        /// <returns>The newly configured hover interaction.</returns>
        internal static ObjectHover AddHover(GameObject root)
        {
            // Only the native prefab stage may receive structural interaction edits.
            if (!ObjectAuthoringSave.TryValidate(root, out string warning) || EditorUtility.IsPersistent(root))
                throw new System.InvalidOperationException(warning.Length > 0 ? warning : "Open the prefab workspace before adding Hover.");
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
            ObjectAuthoringSave.Save(root);
            Undo.CollapseUndoOperations(group);
            return hover;
        }

        /// <summary>Removes the selected interaction while preserving the shared scene UI.</summary>
        /// <param name="hover">Interaction selected in the workspace.</param>
        internal static void RemoveHover(ObjectHover hover)
        {
            // Never remove unrelated or shared UI after a manually edited binding.
            if (!ObjectAuthoringSave.TryValidate(hover.gameObject, out string warning) || EditorUtility.IsPersistent(hover))
                throw new System.InvalidOperationException(warning.Length > 0 ? warning : "Open the prefab workspace before removing Hover.");
            GameObject target = hover.gameObject;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.DestroyObjectImmediate(hover);
            ObjectAuthoringSave.Save(target);
            Undo.CollapseUndoOperations(group);
        }

        #endregion

        #region UI Construction

        /// <summary>Builds one scene label reused by all object-local hover configurations.</summary>
        /// <param name="parent">Shared scene UI root.</param>
        /// <param name="canvas">Existing Canvas used by every gameplay panel.</param>
        /// <returns>The fully connected shared label.</returns>
        internal static HoverLabel BuildLabel(Transform parent, Canvas canvas)
        {
            RectTransform root = ObjectUiAuthoring.CreateRect("Hover HUD", parent);
            ObjectUiAuthoring.Stretch(root);
            RectTransform panel = ObjectUiAuthoring.CreateRect("Label", root);
            panel.sizeDelta = new Vector2(280f, 64f);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.04f, 0.06f, 0.08f, 0.88f);
            background.raycastTarget = false;

            // Text and background are existing native graphics; no runtime builder is necessary.
            RectTransform textRect = ObjectUiAuthoring.CreateRect("Text", panel);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);
            Text text = textRect.gameObject.AddComponent<Text>();
            text.text = string.Empty;
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
                data.FindProperty("defaultFont").objectReferenceValue = text.font;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            panel.gameObject.SetActive(false);
            return label;
        }

        #endregion

        #region Observer

        /// <summary>Connects an existing scene camera and flags an explicitly selected player root.</summary>
        /// <param name="view">Gameplay camera to observe.</param>
        /// <param name="player">Scene player root selected by the user.</param>
        /// <param name="flag">Existing project flag used for runtime discovery.</param>
        /// <param name="existing">Optional observer to reconnect without adding a second scene observer.</param>
        /// <returns>The connected observer, ready to be saved with the scene.</returns>
        internal static HoverObserver SetupObserver(Camera view, GameObject player, ObjectFlag flag, HoverObserver existing = null)
        {
            // This changes only the explicitly chosen camera and player; Undo restores both.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set up hover observer");
            ObjectIdentityAuthoring.EnsureFlag(player, flag);
            HoverObserver observer = existing != null ? existing : view.GetComponent<HoverObserver>();
            if (observer == null)
                observer = Undo.AddComponent<HoverObserver>(view.gameObject);
            using (SerializedObject data = new SerializedObject(observer))
            {
                data.FindProperty("view").objectReferenceValue = view;
                data.FindProperty("playerFlag").objectReferenceValue = flag;
                data.ApplyModifiedProperties();
            }
            Undo.CollapseUndoOperations(group);
            return observer;
        }

        #endregion

        #endregion
    }
}
