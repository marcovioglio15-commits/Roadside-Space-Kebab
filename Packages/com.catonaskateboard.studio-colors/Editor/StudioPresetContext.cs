using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Connects an external preset action to the current tool proposal.</summary>
    public sealed class StudioPresetContext
    {
        #region Properties

        public ScriptableObject Source { get; }
        public Object UndoOwner { get; }
        public string Scope { get; }
        public string[] Paths { get; }
        public Func<Object> Capture { get; }
        public Action<Object> Import { get; }
        public Func<bool> IsCurrent { get; }
        public Func<Object, string> Validate { get; }

        #endregion
        #region Methods
        #region Construction

        /// <summary>Stores callbacks invoked only when an external action reads or updates the proposal.</summary>
        /// <param name="source">Persistent preset receiving the tool's normal Apply.</param>
        /// <param name="undoOwner">Object retaining the tool's pending values.</param>
        /// <param name="capture">Creates an owned snapshot of the current values.</param>
        /// <param name="import">Transfers an approved snapshot back into the tool.</param>
        /// <param name="isCurrent">Checks that the same preset and editing scope remain open.</param>
        /// <param name="scope">Stable scope within the selected preset.</param>
        /// <param name="paths">Optional logical fields exposed by this scope.</param>
        /// <param name="validate">Optional existing preset validation.</param>
        public StudioPresetContext(ScriptableObject source, Object undoOwner, Func<Object> capture,
            Action<Object> import, Func<bool> isCurrent, string scope = "", string[] paths = null,
            Func<Object, string> validate = null)
        {
            Source = source;
            UndoOwner = undoOwner;
            Capture = capture;
            Import = import;
            IsCurrent = isCurrent;
            Scope = scope;
            Paths = paths;
            Validate = validate;
        }

        /// <summary>Connects an ordinary serialized draft without redirecting writes to its saved asset.</summary>
        /// <param name="source">Persistent source identity.</param>
        /// <param name="draft">Current detached values.</param>
        /// <param name="owner">Window owning this editing session.</param>
        /// <param name="changed">Refreshes pending state after an import.</param>
        /// <param name="paths">Optional fields belonging to one logical group.</param>
        /// <param name="scope">Stable group identity.</param>
        /// <param name="current">Additional selection check for shared documents.</param>
        /// <param name="validate">Optional validation of the proposed values.</param>
        /// <param name="undoOwner">Optional window retaining serialized session values.</param>
        /// <returns>A context whose snapshots preserve object references.</returns>
        public static StudioPresetContext ForDraft(ScriptableObject source, Object draft, EditorWindow owner,
            Action changed, string[] paths = null, string scope = "", Func<bool> current = null,
            Func<Object, string> validate = null, Object undoOwner = null)
        {
            // Capture and callbacks are allocated on a button click, never during normal repaint.
            return new StudioPresetContext(source, undoOwner != null ? undoOwner : draft,
                () => Clone(draft), proposal =>
                {
                    using SerializedObject destination = new SerializedObject(draft);
                    using SerializedObject incoming = new SerializedObject(proposal);
                    if (paths != null)
                        foreach (string path in paths)
                            destination.CopyFromSerializedProperty(incoming.FindProperty(path));
                    else
                    {
                        using SerializedProperty property = incoming.GetIterator();
                        if (property.NextVisible(true))
                            do
                                if (!property.name.StartsWith("m_", StringComparison.Ordinal))
                                    destination.CopyFromSerializedProperty(property);
                            while (property.NextVisible(false));
                    }
                    destination.ApplyModifiedPropertiesWithoutUndo();
                    changed?.Invoke();
                    owner.Repaint();
                }, () => owner != null && source != null && draft != null && (current == null || current()),
                scope, paths, validate);
        }

        /// <summary>Creates a hidden snapshot and keeps inactive component previews inactive.</summary>
        /// <param name="source">Typed document or inactive component preview.</param>
        /// <returns>An owned copy released with Release.</returns>
        public static Object Clone(Object source)
        {
            Object copy = Object.Instantiate(source);
            copy.name = source.name;
            copy.hideFlags = HideFlags.HideAndDontSave;
            if (copy is Component component)
                component.gameObject.hideFlags = HideFlags.HideAndDontSave;
            return copy;
        }

        /// <summary>Releases a snapshot including the hidden root of a component copy.</summary>
        /// <param name="snapshot">Owned snapshot; never a persistent asset.</param>
        public static void Release(Object snapshot)
        {
            if (snapshot != null && !EditorUtility.IsPersistent(snapshot))
                Object.DestroyImmediate(snapshot is Component component ? component.gameObject : snapshot);
        }

        #endregion
        #endregion
    }
}
