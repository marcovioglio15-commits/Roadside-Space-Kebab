using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Provides a dedicated day-plan editor independent of Inspector selection and locks.</summary>
    public sealed class SpawnFlowPlanWindow : EditorWindow
    {
        #region Fields

        [Tooltip("Shared plan currently open in this window.")]
        [SerializeField]
        private SpawnFlowPlan plan;
        private UnityEditor.Editor inspector;
        private Vector2 scroll;

        #endregion

        #region Methods
        #region Window

        /// <summary>Opens the selected plan in a focused authoring window.</summary>
        /// <param name="plan">Plan selected in Spawn Management.</param>
        public static void Open(SpawnFlowPlan plan)
        {
            // Reuse a single window so edits always target the most recently requested asset.
            SpawnFlowPlanWindow window = GetWindow<SpawnFlowPlanWindow>("Day Flow Plan");
            window.plan = plan;
            window.minSize = new Vector2(480f, 360f);
            window.Show();
        }

        /// <summary>Shares the asset inspector with the dedicated scrollable window.</summary>
        private void OnGUI()
        {
            // Cached editors are rebuilt only when the selected asset changes.
            plan = (SpawnFlowPlan)StudioGUI.ObjectField(new GUIContent("Plan", "Shared asset edited by this window."), plan, typeof(SpawnFlowPlan), false);
            if (plan == null)
                return;
            UnityEditor.Editor.CreateCachedEditor(plan, typeof(SpawnFlowPlanEditor), ref inspector);
            using EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = view.scrollPosition;
            inspector.OnInspectorGUI();
        }

        /// <summary>Releases the cached editor when the window closes or reloads.</summary>
        private void OnDisable()
        {
            // Shared plan assets remain intact; only the transient inspector is released.
            if (inspector != null)
                DestroyImmediate(inspector);
        }

        #endregion
        #endregion
    }
}
