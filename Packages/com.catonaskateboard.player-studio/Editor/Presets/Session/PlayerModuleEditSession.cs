using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Retains an Input or Camera proposal independently of its temporary Inspector object.</summary>
    [Serializable]
    internal sealed class PlayerModuleEditSession
    {
        #region Serialized Fields

        [Header("Source and Draft")]
        [Tooltip("Original module asset; it is never edited by the temporary Inspector object.")]
        [SerializeField]
        private ScriptableObject source;

        [Tooltip("Source values captured when this session was opened or discarded.")]
        [SerializeField]
        private string baseline = string.Empty;

        [Tooltip("Unconfirmed module values retained across reload and workspace recovery.")]
        [SerializeField]
        private string draft = string.Empty;

        #endregion

        #region Editor Cache

        [NonSerialized] private ScriptableObject working;
        [NonSerialized] private SerializedObject editor;
        [NonSerialized] private string cached;

        #endregion

        #region Properties

        /// <summary>The only persistent asset this session may update.</summary>
        public ScriptableObject Source => source;
        /// <summary>Raw differences remain pending even when a source reference disappears.</summary>
        public bool HasChanges => draft != baseline;

        #endregion

        #region Methods

        #region Draft

        /// <summary>Follows the applied slot only when no draft would be abandoned.</summary>
        /// <param name="asset">Input or Camera asset currently assigned to the master.</param>
        public void Refresh(ScriptableObject asset)
        {
            // Slot changes never redirect an unfinished proposal to a different asset.
            if (HasChanges)
                return;
            source = asset;
            Discard();
        }

        /// <summary>Reloads applied values without writing to the source.</summary>
        public void Discard()
        {
            // JsonUtility captures user fields and excludes temporary object names and hide flags.
            baseline = source != null ? JsonUtility.ToJson(source) : string.Empty;
            draft = baseline;
        }

        /// <summary>Returns a cached Editor-only copy suitable for conditional serialized fields.</summary>
        /// <returns>The temporary SerializedObject, or null without a source.</returns>
        public SerializedObject GetEditor()
        {
            // Rebuild only after a source type change; ordinary repaints reuse this object.
            if (source == null)
                return null;
            if (working == null || working.GetType() != source.GetType())
            {
                Dispose();
                working = UnityEngine.Object.Instantiate(source);
                working.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                editor = new SerializedObject(working);
            }
            if (cached != draft)
            {
                JsonUtility.FromJsonOverwrite(draft, working);
                cached = draft;
            }
            editor.Update();
            return editor;
        }

        /// <summary>Copies temporary Inspector input into the serialized draft after the window records Undo.</summary>
        public void Capture()
        {
            // Only the temporary clone changes here; the source remains untouched until Apply.
            editor.ApplyModifiedPropertiesWithoutUndo();
            draft = cached = JsonUtility.ToJson(working);
        }

        /// <summary>Releases transient Inspector resources without abandoning serialized values.</summary>
        public void Dispose()
        {
            // Window close and script reload must not leak hidden ScriptableObjects.
            editor?.Dispose();
            editor = null;
            if (working != null)
                UnityEngine.Object.DestroyImmediate(working);
            working = null;
            cached = null;
        }

        #endregion

        #region Confirmation

        /// <summary>Validates the active module fields on its temporary copy.</summary>
        /// <param name="warning">Receives unsupported roles or numeric values.</param>
        /// <returns>True when this proposal can be confirmed.</returns>
        public bool TryValidate(out string warning)
        {
            // Validation reads current raw draft values, including invalid values still being edited.
            GetEditor();
            return PlayerModuleValidation.TryValidate(working, out warning);
        }

        /// <summary>Prepares pending properties after checking source identity and outside changes.</summary>
        /// <param name="assigned">Asset still assigned to the current master slot.</param>
        /// <param name="changes">Receives properties for the shared transaction.</param>
        /// <param name="warning">Receives a conflict or validation warning.</param>
        /// <returns>True when the unchanged source can receive this proposal.</returns>
        public bool TryPrepare(ScriptableObject assigned, out SerializedObject changes, out string warning)
        {
            // Never write to a redirected slot or save over external Inspector changes.
            changes = null;
            warning = string.Empty;
            if (!HasChanges)
                return true;
            if (source == null || assigned != source || !EditorUtility.IsPersistent(source)
                || JsonUtility.ToJson(source) != baseline)
            {
                warning = "The module source changed outside this session. Discard to reload its current values.";
                return false;
            }
            if (!TryValidate(out warning))
                return false;
            changes = new SerializedObject(source);
            using SerializedProperty property = editor.GetIterator();
            if (property.NextVisible(true))
                do
                    if (property.name != "m_Script")
                        changes.CopyFromSerializedProperty(property);
                while (property.NextVisible(false));
            return true;
        }

        #endregion

        #endregion
    }
}
