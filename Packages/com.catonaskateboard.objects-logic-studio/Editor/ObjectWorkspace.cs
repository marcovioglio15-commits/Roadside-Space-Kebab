using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Persists actual draft data and asset references outside the lifetime of the tool window.</summary>
    [FilePath("UserSettings/ObjectsLogicStudio.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ObjectWorkspace : ScriptableSingleton<ObjectWorkspace>
    {
        #region Fields

        [Header("Selection")]
        [Tooltip("Last selected prefab, also available before it contains interactions.")]
        public GameObject Prefab;
        [Tooltip("Durable object and interaction selection.")]
        public ObjectWorkspaceTarget Target = new ObjectWorkspaceTarget();
        [Tooltip("Preset whose saved values the current draft may update.")]
        public HoverPreset Source;
        [Tooltip("Preset assigned to the object when this binding session began.")]
        public HoverPreset OriginalPreset;

        [Header("Draft")]
        [Tooltip("Detached detection and style proposal; this never edits the source until Apply.")]
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

        [Header("Workspace")]
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

        /// <summary>Changes to reusable configuration, independent of object bindings.</summary>
        internal bool PresetChanged => JsonUtility.ToJson(Draft) != JsonUtility.ToJson(Baseline);
        /// <summary>Pending data survives a missing source or temporarily closed prefab stage.</summary>
        internal bool InteractionChanged => PresetChanged || HasBinding && (Source != OriginalPreset
            || JsonUtility.ToJson(Binding) != JsonUtility.ToJson(OriginalBinding));
        /// <summary>Whether any domain requires the common Apply action.</summary>
        internal bool HasChanges => InteractionChanged || Single.HasChanges || Observer.HasChanges;

        #endregion

        #region Methods

        #region Persistence

        /// <summary>Saves the workspace data and asset GUID references without writing a prefab or scene.</summary>
        internal void Persist()
        {
            // ScriptableSingleton's native serialization preserves asset references inside both snapshots.
            if (!EditorApplication.isPlaying)
            {
                Target.RefreshIdentity();
                Save(true);
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
