using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits the selected object's own identity without depending on an open or unlocked Inspector.</summary>
    internal sealed class ObjectIdentityWindow : EditorWindow
    {
        #region Serialized Fields

        [Header("Object")]
        [Tooltip("Object identity opened from Object Logic Studio. Edits affect this object only.")]
        [SerializeField]
        private ObjectIdentity identity;

        #endregion

        #region Methods

        #region Window

        /// <summary>Observes selections committed by the searchable popup's separate serialized wrapper.</summary>
        private void OnEnable()
        {
            // The popup can remain open while several flags are toggled.
            ObjectFlagSelector.SelectionChanged += SaveSelection;
        }

        /// <summary>Releases the popup subscription when this editor closes.</summary>
        private void OnDisable()
        {
            // Closed windows never retain selected prefab objects through static events.
            ObjectFlagSelector.SelectionChanged -= SaveSelection;
        }

        /// <summary>Persists only membership changes belonging to this window's object.</summary>
        /// <param name="owner">Serialized target changed by a flag popup.</param>
        private void SaveSelection(Object owner)
        {
            // A different interaction's flag filter must not trigger an unrelated prefab save.
            if (identity == null || owner != identity)
                return;
            ObjectAuthoringSave.Save(identity.gameObject);
            Repaint();
        }

        /// <summary>Shows the searchable flag selector for an existing object identity.</summary>
        /// <param name="target">Identity on the selected prefab branch or scene object.</param>
        internal static void Open(ObjectIdentity target)
        {
            // A utility window stays visible even when the Inspector is hidden or locked.
            ObjectIdentityWindow window = GetWindow<ObjectIdentityWindow>(true, "Object Flags", true);
            window.identity = target;
            window.minSize = new Vector2(390f, 125f);
            window.Show();
            window.Focus();
        }

        /// <summary>Applies flag membership with Undo and persists the owning prefab or scene.</summary>
        private void OnGUI()
        {
            // Deleted objects leave a harmless empty editor instead of a stale serialized target.
            if (identity == null)
            {
                EditorGUILayout.LabelField("Select an object in Object Logic Studio.");
                return;
            }
            using (new EditorGUI.DisabledScope(true))
                StudioGUI.ObjectField(new GUIContent("Object", "Object receiving these identity flags."), identity.gameObject, typeof(GameObject), true);
            using SerializedObject data = new SerializedObject(identity);
            data.Update();
            ObjectFlagSelector.Draw(data.FindProperty("flags"));
            if (data.ApplyModifiedProperties())
                ObjectAuthoringSave.Save(identity.gameObject);
            if (!ObjectFlagRules.TryValidate(identity.AuthoredFlags, true, out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
        }

        #endregion

        #endregion
    }
}
