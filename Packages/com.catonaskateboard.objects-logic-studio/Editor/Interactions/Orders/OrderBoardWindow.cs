using CatOnASkateboard.StudioColors.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors shared 3D text slots in a gameplay scene without runtime UI creation.</summary>
    internal sealed class OrderBoardWindow : EditorWindow
    {
        #region Fields

        [Tooltip("Scene board whose authored text slots are being edited.")]
        [SerializeField]
        private OrderBoard board;
        private UnityEditor.Editor inspector;

        #endregion
        #region Methods
        #region Window

        /// <summary>Opens board configuration independently of a locked Inspector or prefab stage.</summary>
        [MenuItem("Tools/Objects Logic Studio/Order Board")]
        internal static void Open()
        {
            // Opening an editor does not modify scene objects.
            GetWindow<OrderBoardWindow>("Order Board").Show();
        }

        /// <summary>Shows an existing scene board or offers explicit scene authoring.</summary>
        private void OnGUI()
        {
            // The prefab stage cannot own a shared scene destination.
            board = (OrderBoard)EditorGUILayout.ObjectField(new GUIContent("Board", "Shared board in the gameplay scene."), board, typeof(OrderBoard), true);
            if (board != null)
            {
                UnityEditor.Editor.CreateCachedEditor(board, null, ref inspector);
                inspector.OnInspectorGUI();
                if (!board.TryValidate(out string warning))
                    EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
                if (StudioButton.Draw(new GUIContent("Add Text Slot", "Create one editable 3D text child and append it to this board.")))
                    AddSlot(board);
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null))
                if (StudioButton.Draw(new GUIContent("Create Scene Board", "Create a world-space board with four editable text slots in the active scene.")))
                    board = Create(SceneManager.GetActiveScene());
        }

        /// <summary>Releases the cached inspector when this editor window closes.</summary>
        private void OnDisable()
        {
            // Cached inspectors are editor-only temporary objects.
            if (inspector != null)
                DestroyImmediate(inspector);
        }

        #endregion
        #region Authoring

        /// <summary>Creates a board and initial slots entirely in Edit mode.</summary>
        /// <param name="scene">Gameplay scene receiving the board root.</param>
        /// <returns>The new scene board.</returns>
        internal static OrderBoard Create(Scene scene)
        {
            // Each authored child remains directly editable after generation.
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                Debug.LogWarning("Import TMP Essential Resources from Window > TextMeshPro before creating an Order Board.");
                return null;
            }
            GameObject root = new GameObject("Order Board");
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create Order Board");
            OrderBoard created = Undo.AddComponent<OrderBoard>(root);
            for (int index = 0; index < 4; index++)
                AddSlot(created);
            if (SceneView.lastActiveSceneView != null)
            {
                Transform camera = SceneView.lastActiveSceneView.camera.transform;
                root.transform.SetPositionAndRotation(camera.position + camera.forward * 3f, camera.rotation);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            return created;
        }

        /// <summary>Appends one authored TextMeshPro object to an existing board.</summary>
        /// <param name="target">Scene board receiving a new slot.</param>
        private static void AddSlot(OrderBoard target)
        {
            // Strikethrough is rendered by TMP itself; gameplay never creates line objects.
            using SerializedObject data = new SerializedObject(target);
            SerializedProperty slots = data.FindProperty("slots");
            GameObject child = new GameObject("Order " + (slots.arraySize + 1), typeof(RectTransform), typeof(TextMeshPro));
            Undo.RegisterCreatedObjectUndo(child, "Add Order Slot");
            child.transform.SetParent(target.transform, false);
            child.transform.localPosition = Vector3.down * slots.arraySize * 0.4f;
            TextMeshPro text = child.GetComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 3f;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(3.5f, 0.35f);
            text.text = "Order slot " + (slots.arraySize + 1);
            slots.arraySize++;
            slots.GetArrayElementAtIndex(slots.arraySize - 1).objectReferenceValue = text;
            data.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        }

        #endregion
        #endregion
    }
}
