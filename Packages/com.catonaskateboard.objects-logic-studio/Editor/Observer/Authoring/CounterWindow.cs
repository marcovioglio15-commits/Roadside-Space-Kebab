using CatOnASkateboard.StudioColors.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Creates saved money and timer text before Play, or selects existing counters.</summary>
    internal sealed class CounterWindow : EditorWindow
    {
        #region Types

        private enum CounterKind { Money, OrderTimer }
        private enum CounterDisplay { Hud, World }

        #endregion

        #region Serialized Fields

        [Header("Counter")]
        [Tooltip("Value displayed by the new counter.")]
        [SerializeField]
        private CounterKind kind;
        [Tooltip("Use the shared scene HUD or a spatial TMP text.")]
        [SerializeField]
        private CounterDisplay display;
        [Tooltip("Optional existing counter to inspect instead of creating another one.")]
        [SerializeField]
        private MonoBehaviour existing;
        [Tooltip("Optional parent for a spatial counter.")]
        [SerializeField]
        private Transform parent;
        [Tooltip("Position relative to the chosen parent. World offset when no parent is selected.")]
        [SerializeField]
        private Vector3 offset;
        [Tooltip("Euler rotation relative to the chosen parent.")]
        [SerializeField]
        private Vector3 rotation;
        [Tooltip("Font used by spatial text. Empty uses the project's TMP default font.")]
        [SerializeField]
        private TMP_FontAsset font;
        [Tooltip("Spatial text size in world units.")]
        [SerializeField]
        [Range(0.05f, 5f)]
        private float fontSize = 0.5f;
        [Tooltip("Keep spatial text facing the Observer camera.")]
        [SerializeField]
        private bool faceCamera;
        [Tooltip("Optional exact customer for a spatial timer. Empty follows the latest visible customer in this scene.")]
        [SerializeField]
        private ObjectAvailableOrders customer;
        private Vector2 scroll;

        #endregion

        #region Methods

        #region Window

        /// <summary>Opens the small scene counter setup window.</summary>
        //[MenuItem("Tools/Objects Logic Studio Counters")]
        private static void Open()
        {
            // This window edits saved scene objects only.
            GetWindow<CounterWindow>("Counters").minSize = new Vector2(340f, 220f);
        }

        /// <summary>Shows only settings used by the chosen representation.</summary>
        private void OnGUI()
        {
            // Serialized controls retain native Undo and tooltips.
            using EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = view.scrollPosition;
            using SerializedObject data = new SerializedObject(this);
            foreach (string name in new[] { "kind", "display", "existing" })
                StudioFieldGUI.PropertyField(data.FindProperty(name));
            if (existing != null)
            {
                if (StudioButton.Draw(new GUIContent("Select Counter", "Select this saved component in the Inspector.")))
                    Selection.activeObject = existing;
                data.ApplyModifiedProperties();
                return;
            }
            if (display == CounterDisplay.World)
            {
                StudioFieldGUI.PropertyField(data.FindProperty("parent"));
                ObjectNumericControls.Vector(data.FindProperty("offset"));
                ObjectNumericControls.Vector(data.FindProperty("rotation"), -180f, 180f);
                foreach (string name in new[] { "font", "fontSize", "faceCamera" })
                    StudioFieldGUI.PropertyField(data.FindProperty(name));
                if (kind == CounterKind.OrderTimer)
                    StudioFieldGUI.PropertyField(data.FindProperty("customer"));
            }
            data.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (StudioButton.Draw(new GUIContent("Create Counter", "Create editable text in the active scene. Runtime never constructs this UI.")))
                    Create();
        }

        #endregion

        #region Creation

        /// <summary>Builds the selected representation and records one native Undo group.</summary>
        private void Create()
        {
            // Reject invalid input without silently changing the requested pose or size.
            if (display == CounterDisplay.World && (!InteractionValues.Finite(offset) || !InteractionValues.Finite(rotation)
                || !InteractionValues.Positive(fontSize) || parent != null && EditorUtility.IsPersistent(parent)))
            {
                Debug.LogWarning("Use a scene parent, finite offset and rotation, and a positive text size.");
                return;
            }
            int group = Undo.GetCurrentGroup();
            if (display == CounterDisplay.World)
                CreateWorld();
            else
                CreateHud();
            Undo.CollapseUndoOperations(group);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
            }
        }

        /// <summary>Creates mesh text and assigns its saved runtime binding.</summary>
        private void CreateWorld()
        {
            // All graphics and optional facing behavior exist before Play.
            GameObject root = new GameObject(kind == CounterKind.Money ? "Money Counter 3D" : "Order Timer 3D");
            Undo.RegisterCreatedObjectUndo(root, "Create spatial counter");
            if (parent != null)
                Undo.SetTransformParent(root.transform, parent, "Parent spatial counter");
            root.transform.localPosition = offset;
            root.transform.localRotation = Quaternion.Euler(rotation);
            TextMeshPro text = Undo.AddComponent<TextMeshPro>(root);
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.text = kind == CounterKind.Money ? "0$" : "Order 00:00";
            text.raycastTarget = false;
            existing = kind == CounterKind.Money ? Undo.AddComponent<CollectedPriceHud>(root) : Undo.AddComponent<OrderTimerHud>(root);
            using SerializedObject data = new SerializedObject(existing);
            data.FindProperty("spatialText").objectReferenceValue = text;
            if (kind == CounterKind.OrderTimer)
                data.FindProperty("customer").objectReferenceValue = customer;
            data.ApplyModifiedProperties();
            if (faceCamera)
                Undo.AddComponent<CounterFacing>(root);
        }

        /// <summary>Reuses the saved gameplay Canvas and its existing timer where possible.</summary>
        private void CreateHud()
        {
            // Counter setup never creates an additional scene Canvas.
            ObjectSceneHud hud = ObjectSceneHudAuthoring.Find(EditorSceneManager.GetActiveScene());
            if (hud == null)
            {
                hud = ObjectSceneHudAuthoring.Build();
                Undo.RegisterCreatedObjectUndo(hud.gameObject, "Create gameplay HUD");
            }
            if (kind == CounterKind.OrderTimer)
            {
                existing = OrderTimerHudAuthoring.Build(hud.transform, hud.GetComponent<Canvas>());
                return;
            }
            CollectedPriceHud counter = hud.GetComponentInChildren<CollectedPriceHud>(true);
            if (counter != null)
            {
                existing = counter;
                return;
            }
            RectTransform label = ObjectUiAuthoring.CreateRect("Money Counter", hud.transform);
            Undo.RegisterCreatedObjectUndo(label.gameObject, "Create money HUD");
            label.anchorMin = label.anchorMax = label.pivot = new Vector2(0f, 1f);
            label.anchoredPosition = new Vector2(40f, -40f);
            label.sizeDelta = new Vector2(250f, 48f);
            Text text = Undo.AddComponent<Text>(label.gameObject);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.raycastTarget = false;
            existing = Undo.AddComponent<CollectedPriceHud>(label.gameObject);
            using SerializedObject data = new SerializedObject(existing);
            data.FindProperty("text").objectReferenceValue = text;
            data.ApplyModifiedProperties();
        }

        #endregion

        #endregion
    }
}
