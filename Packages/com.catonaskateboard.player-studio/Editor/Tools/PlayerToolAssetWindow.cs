using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Hosts existing tool Inspectors on independent, recoverable asset drafts.</summary>
    internal abstract class PlayerToolAssetWindow : StudioWindow
    {
        #region Fields

        [Header("Hierarchy Context")]
        [Tooltip("Sample hierarchy supplying reusable animation paths and muzzle preview geometry.")]
        [SerializeField]
        private Transform hierarchy;
        [Tooltip("Whether switch animation settings apply to this player.")]
        [SerializeField]
        private bool switching = true;

        private UnityEditor.Editor inspector;

        #endregion

        #region Properties

        /// <summary>Whether the current player uses tool selection and switch clips.</summary>
        protected bool Switching => switching;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Reuses a window for the same asset and leaves other pending proposals intact.</summary>
        /// <typeparam name="T">Tool or animation window type.</typeparam>
        /// <param name="source">Persistent source of this independent transaction.</param>
        /// <param name="root">Sample hierarchy selected in the calling tool.</param>
        /// <param name="allowSwitching">Whether switch settings apply.</param>
        /// <param name="title">Window caption.</param>
        protected static void ShowAsset<T>(ScriptableObject source, Transform root, bool allowSwitching, string title)
            where T : PlayerToolAssetWindow
        {
            if (source == null || !EditorUtility.IsPersistent(source))
                return;
            // The same preset has one proposal; separate presets can be edited side by side.
            T window = null;
            foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
                if (candidate.Source == source)
                {
                    window = candidate;
                    break;
                }
            if (window == null)
            {
                window = CreateInstance<T>();
                window.SelectPreset(source);
            }
            if (root != null)
                window.hierarchy = root;
            window.switching = allowSwitching;
            window.ReleaseInspector();
            window.titleContent = new GUIContent(title, source.name);
            window.Show();
            window.Focus();
            window.Repaint();
        }

        /// <summary>Opens tool and animation assets in their dedicated editors on a Project double-click.</summary>
        /// <param name="entityId">Asset selected by Unity.</param>
        /// <returns>True when a supported asset opened in its independent window.</returns>
        [OnOpenAsset]
        private static bool OpenAsset(EntityId entityId)
        {
            // Project navigation uses the same per-asset transactions as the tool buttons.
            switch (EditorUtility.EntityIdToObject(entityId))
            {
                case PlayerTool tool:
                    PlayerToolWindow.Open(tool, null, true);
                    return true;
                case PlayerToolAnimationPreset animation:
                    PlayerToolAnimationWindow.Open(animation, null);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Releases the Inspector while the shared window retains unfinished draft values.</summary>
        protected override void OnDisable()
        {
            // Release the Inspector before the shared window retains its draft for recovery.
            ReleaseInspector();
            base.OnDisable();
        }

        /// <summary>Destroys only the cached Editor, leaving source and draft ownership to the session.</summary>
        private void ReleaseInspector()
        {
            // The session keeps ownership of the detached target after its Editor is released.
            if (inspector != null)
                DestroyImmediate(inspector);
            inspector = null;
        }

        #endregion

        #region Drawing

        /// <summary>Rebuilds the Inspector after Apply, Discard or source refresh replaces the draft.</summary>
        /// <param name="data">Detached serialized preset owned by the shared window.</param>
        protected override void DrawTab(SerializedObject data)
        {
            // Conditional controls always edit the draft, including delayed hierarchy menu selections.
            if (inspector == null || inspector.target != data.targetObject)
            {
                ReleaseInspector();
                inspector = UnityEditor.Editor.CreateEditor(data.targetObject);
                switch (inspector)
                {
                    case PlayerToolEditor tool:
                        tool.Bind(hierarchy, switching);
                        break;
                    case PlayerToolAnimationPresetEditor animation:
                        animation.Bind(hierarchy);
                        break;
                }
            }
            inspector.OnInspectorGUI();
        }

        #endregion

        #endregion
    }
}
