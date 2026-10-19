using TMPro;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Prepares reusable text paragraphs in Edit mode while preserving existing board slots.</summary>
    internal static class OrderBoardTextAuthoring
    {
        #region Methods
        #region Authoring

        /// <summary>Adds missing paragraph children up to the board's requested capacity.</summary>
        /// <param name="board">Scene or prefab board being edited.</param>
        internal static void Prepare(OrderBoard board)
        {
            // Never extend presentation hierarchies during Play or replace manually authored paragraphs.
            if (Application.isPlaying)
                return;
            using SerializedObject data = new SerializedObject(board);
            int count = board.ParagraphCapacity;
            if (count < 1 || count > 33)
            {
                Debug.LogWarning("Choose a paragraph capacity between 1 and 33 before preparing the board.", board);
                return;
            }
            SerializedProperty slots = data.FindProperty("slots");
            for (int index = 0; index < slots.arraySize; index++)
                if (slots.GetArrayElementAtIndex(index).objectReferenceValue is TMP_Text slot)
                    Prepare(slot, count);
        }

        /// <summary>Copies the slot's TMP configuration into independent inactive children.</summary>
        /// <param name="slot">Original board text retaining its plain display path.</param>
        /// <param name="count">Required reusable paragraph capacity.</param>
        private static void Prepare(TMP_Text slot, int count)
        {
            OrderBoardText presenter = slot.GetComponent<OrderBoardText>();
            if (presenter == null)
                presenter = Undo.AddComponent<OrderBoardText>(slot.gameObject);
            using SerializedObject data = new SerializedObject(presenter);
            SerializedProperty paragraphs = data.FindProperty("paragraphs");
            // Preserve surplus authored capacity and repair empty references explicitly in the editor.
            int previousCount = paragraphs.arraySize;
            if (previousCount < count)
            {
                paragraphs.arraySize = count;
                for (int index = previousCount; index < count; index++)
                    paragraphs.GetArrayElementAtIndex(index).objectReferenceValue = null;
            }
            for (int index = 0; index < paragraphs.arraySize; index++)
            {
                if (paragraphs.GetArrayElementAtIndex(index).objectReferenceValue != null)
                    continue;
                GameObject child = new GameObject("Order Paragraph " + (index + 1), typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(child, "Prepare Order Text");
                child.transform.SetParent(slot.transform, false);
                TMP_Text text = (TMP_Text)Undo.AddComponent(child, slot is TextMeshProUGUI ? typeof(TextMeshProUGUI) : typeof(TextMeshPro));
                EditorUtility.CopySerialized(slot, text);
                text.text = string.Empty;
                text.enabled = true;
                text.raycastTarget = false;
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0f, 1f);
                text.rectTransform.pivot = new Vector2(0f, 1f);
                text.rectTransform.sizeDelta = slot.rectTransform.rect.size;
                text.rectTransform.anchoredPosition = Vector2.zero;
                child.SetActive(false);
                paragraphs.GetArrayElementAtIndex(index).objectReferenceValue = text;
            }
            data.ApplyModifiedProperties();
            EditorUtility.SetDirty(presenter);
        }

        #endregion
        #endregion
    }
}
