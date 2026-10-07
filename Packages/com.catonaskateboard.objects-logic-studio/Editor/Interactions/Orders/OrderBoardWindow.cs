using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
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
        /// <param name="selected">Optional gameplay board selected from the prefab draft.</param>
        internal static void Open(OrderBoard selected = null)
        {
            // Opening an editor does not modify scene objects.
            OrderBoardWindow window = GetWindow<OrderBoardWindow>("Order Board");
            if (selected != null)
                window.board = selected;
            window.Show();
        }

        /// <summary>Shows an existing scene board or offers explicit scene authoring.</summary>
        private void OnGUI()
        {
            // The prefab stage cannot own a shared scene destination.
            board = (OrderBoard)StudioGUI.ObjectField(new GUIContent("Board", "Shared board in the gameplay scene."), board, typeof(OrderBoard), true);
            using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode);
            if (OrderBoardBinding.IsSceneBoard(board))
            {
                UnityEditor.Editor.CreateCachedEditor(board, null, ref inspector);
                inspector.OnInspectorGUI();
                if (!board.TryValidate(out string warning))
                    EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
                if (StudioButton.Draw(new GUIContent("Add Text Slot", "Create one editable 3D text child and append it to this board.")))
                    AddSlot(board);
            }
            if (StudioButton.Draw(new GUIContent("Create Scene Board", "Choose a loaded gameplay scene for a board with four authored text slots.")))
            {
                GenericMenu menu = new GenericMenu();
                for (int index = 0; index < SceneManager.sceneCount; index++)
                {
                    Scene scene = SceneManager.GetSceneAt(index);
                    if (scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene))
                        menu.AddItem(new GUIContent(scene.name), false, () => board = Create(scene));
                }
                menu.ShowAsContext();
            }
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
            if (PrefabStageUtility.GetCurrentPrefabStage() == null)
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
            SceneManager.MoveGameObjectToScene(child, target.gameObject.scene);
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
