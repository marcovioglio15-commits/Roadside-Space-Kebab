using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using System.IO;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Persists actual draft data and asset references outside the lifetime of the tool window.</summary>
    internal sealed class ObjectWorkspace : ScriptableObject
    {
        #region State

        internal const HideFlags DraftFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        private static ObjectWorkspace current;

        #endregion

        #region Fields

        [Header("Storage")]
        [Tooltip("Keeps the main workspace connected after scripts reload.")]
        [SerializeField]
        [HideInInspector]
        private bool workspaceOwner;

        [Header("Selection")]
        [Tooltip("Last selected prefab, also available before it contains interactions.")]
        public GameObject Prefab;
        [Tooltip("Durable object and interaction selection.")]
        public ObjectWorkspaceTarget Target = new ObjectWorkspaceTarget();
        [Tooltip("Object shown in the interaction list. Child selections keep this list open.")]
        public ObjectWorkspaceTarget BrowseTarget = new ObjectWorkspaceTarget();
        [Tooltip("Preset whose saved values the current draft may update.")]
        public HoverPreset Source;
        [Tooltip("Preset assigned to the object when this binding session began.")]
        public HoverPreset OriginalPreset;
        [Tooltip("Selected asset state used only by the explicit Update action.")]
        public string SourceBaseline = string.Empty;

        [Header("Draft")]
        [Tooltip("Detached detection and style proposal. Apply saves the object; Update saves the preset.")]
        public HoverConfiguration Draft = new HoverConfiguration();
        [Tooltip("Saved configuration captured when the source was opened.")]
        public HoverConfiguration Baseline = new HoverConfiguration();
        [Tooltip("Pending per-object name, enabled state and anchor.")]
        public HoverBindingDraft Binding = new HoverBindingDraft();
        [Tooltip("Applied per-object values captured when the interaction was opened.")]
        public HoverBindingDraft OriginalBinding = new HoverBindingDraft();
        [Tooltip("Hierarchy structure at selection, checked before committing an anchor route.")]
        public string Hierarchy = string.Empty;
        [Tooltip("Whether a component binding belongs to this session, including temporarily missing objects.")]
        public bool HasBinding;
        [Tooltip("Retained Grab, Drop or Throw proposal sharing the common Apply and Discard transaction.")]
        public SingleInteractionSession Single = new SingleInteractionSession();
        [Tooltip("Retained contact or dialogue proposal sharing the common Apply and Discard transaction.")]
        public ExtendedInteractionSession Extended = new ExtendedInteractionSession();

        [Header("Availability Presets")]
        [Tooltip("Reusable set of availability rules selected for explicit Import, Export or Update.")]
        public UnlockRuleSetPreset RuleSet;
        [Tooltip("Selected rule-set asset state used to detect outside edits before Update.")]
        public string RuleSetBaseline = string.Empty;

        [Header("Workspace")]
        [Tooltip("Choose views, filters and quick access.")]
        public ObjectStudioNavigation Navigation = new ObjectStudioNavigation();
        [Tooltip("Other interaction drafts kept while navigating.")]
        public System.Collections.Generic.List<ObjectWorkspaceDraft> SavedDrafts = new System.Collections.Generic.List<ObjectWorkspaceDraft>();
        [Tooltip("Interaction linked to the current draft.")]
        public string DraftKey = string.Empty;
        [Tooltip("Interactions to add or remove when Apply is clicked.")]
        public System.Collections.Generic.List<ObjectStructureChange> Structure = new System.Collections.Generic.List<ObjectStructureChange>();
        [Tooltip("Presets last imported into individual settings groups.")]
        public System.Collections.Generic.List<ObjectGroupAssignment> GroupPresets = new System.Collections.Generic.List<ObjectGroupAssignment>();
        [Tooltip("Master presets selected for individual objects.")]
        public System.Collections.Generic.List<ObjectMasterAssignment> Masters = new System.Collections.Generic.List<ObjectMasterAssignment>();
        [Tooltip("Interaction category currently displayed in the workspace.")]
        public ObjectInteractionCategory Category;
        [Tooltip("Expand the selected interaction to show its preset and object settings.")]
        public bool InteractionExpanded = true;
        [Tooltip("Collapsed menus retained independently of pending data.")]
        public ObjectStudioSections Sections = new ObjectStudioSections();
        [Tooltip("Form scroll position restored on reopening.")]
        public Vector2 Scroll;
        [Tooltip("Observer camera/player setup retained and rediscovered from the actual scene component.")]
        public ObjectObserverSession Observer = new ObjectObserverSession();
        [Tooltip("Last floating window position and size.")]
        public Rect Position;

        #endregion

        #region Properties

        /// <summary>Loads the persistent workspace while detached group documents remain ordinary ScriptableObjects.</summary>
        internal static ObjectWorkspace instance
        {
            get
            {
                if (current != null)
                    return current;
                // Group documents have no ownership flag and cannot become the persistent workspace.
                foreach (ObjectWorkspace document in Resources.FindObjectsOfTypeAll<ObjectWorkspace>())
                    if (document.workspaceOwner)
                    {
                        current = document;
                        break;
                    }
                if (current == null && File.Exists(StoragePath))
                    foreach (UnityEngine.Object saved in InternalEditorUtility.LoadSerializedFileAndForget(StoragePath))
                        if (saved is ObjectWorkspace workspace)
                            current = workspace;
                if (current == null)
                    current = CreateInstance<ObjectWorkspace>();
                current.workspaceOwner = true;
                return current;
            }
        }

        /// <summary>Existing native workspace file, kept outside imported project assets.</summary>
        private static string StoragePath => Path.GetFullPath(Path.Combine(Application.dataPath, "../UserSettings/ObjectsLogicStudio.asset"));

        /// <summary>Master selected for the current browsing root rather than the last visited interaction.</summary>
        internal ObjectMasterPreset Master
        {
            get => Masters.Find(entry => entry.Key == MasterKey)?.Preset;
            set
            {
                if (Master == value)
                    return;
                Masters.RemoveAll(entry => entry.Key == MasterKey);
                if (value != null)
                    Masters.Add(new ObjectMasterAssignment { Key = MasterKey, Preset = value });
            }
        }

        /// <summary>Stable object identity for the master selection.</summary>
        private string MasterKey => BrowseTarget.PrefabGuid + ":" + BrowseTarget.ObjectFileId + ":" + BrowseTarget.ObjectPath;

        /// <summary>Changes to reusable configuration, independent of object bindings.</summary>
        internal bool PresetChanged => JsonUtility.ToJson(Draft) != JsonUtility.ToJson(Baseline);
        /// <summary>Pending data survives a missing source or temporarily closed prefab stage.</summary>
        internal bool InteractionChanged => PresetChanged || HasBinding && (Source != OriginalPreset
            || JsonUtility.ToJson(Binding) != JsonUtility.ToJson(OriginalBinding));
        /// <summary>Pending object settings requiring the retained prefab to be open before Apply.</summary>
        internal bool PrefabChanged => InteractionChanged || Single.HasChanges || Extended.HasChanges;
        /// <summary>Whether any domain requires the common Apply action.</summary>
        internal bool HasChanges => PrefabChanged || Observer.HasChanges;

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Keeps every new or restored document hidden without locking its native property controls.</summary>
        private void OnEnable()
        {
            // HideAndDontSave also includes NotEditable, which disables native flag and asset pickers.
            hideFlags = DraftFlags;
        }

        #endregion

        #region Persistence

        /// <summary>Saves the workspace data and asset GUID references without writing a prefab or scene.</summary>
        internal void Persist()
        {
            if (this != current)
                return;
            // Native editor serialization preserves asset references inside both snapshots.
            if (!EditorApplication.isPlaying)
            {
                Target.RefreshIdentity();
                Directory.CreateDirectory(Path.GetDirectoryName(StoragePath));
                InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { this }, StoragePath, true);
            }
        }

        /// <summary>Copies serialized configuration for an independent baseline or working proposal.</summary>
        /// <typeparam name="T">Serializable editor snapshot type.</typeparam>
        /// <param name="value">Data to copy without retaining nested mutable objects.</param>
        /// <returns>A detached copy retaining the same external asset references.</returns>
        internal static T Copy<T>(T value)
        {
            // JSON is used only within this editor process; durable storage uses native asset references.
            return JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        }

        #endregion

        #endregion
    }
}
