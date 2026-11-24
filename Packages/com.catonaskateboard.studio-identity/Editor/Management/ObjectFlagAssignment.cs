using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Applies popup choices to an asset field with fresh resolution and conflict checks.</summary>
    internal sealed class ObjectFlagAssignment
    {
        #region State

        private readonly ObjectFlagField field;
        private readonly Action changed;
        private ObjectFlag[] expected;
        private readonly ObjectFlagDraftSession session;
        private readonly int generation;
        private bool valid = true;
        internal bool Valid => valid && (session == null || !session.Released && session.Generation == generation);

        #endregion
        #region Methods
        #region Editing

        /// <summary>Captures membership so delayed choices cannot overwrite external changes.</summary>
        /// <param name="selection">Scanned destination selection.</param>
        /// <param name="onChanged">Refreshes the source view after a saved choice.</param>
        /// <param name="draft">Optional management proposal receiving the selection.</param>
        internal ObjectFlagAssignment(ObjectFlagField selection, Action onChanged, ObjectFlagDraftSession draft = null)
        {
            field = selection;
            session = draft;
            generation = draft != null ? draft.Generation : 0;
            expected = draft != null ? draft.Values(selection) : selection.Flags;
            changed = onChanged;
        }

        /// <summary>Applies a flag toggle or an explicit clear command.</summary>
        /// <param name="flag">Flag to include or remove. Null clears the whole selection.</param>
        /// <param name="include">Whether this flag should be selected.</param>
        internal void Assign(ObjectFlag flag, bool include)
        {
            List<ObjectFlag> next = expected.Where(value => value != null).Distinct().ToList();
            if (!field.Multiple || flag == null)
                next.Clear();
            if (flag != null)
                if ((include || !field.Multiple) && !next.Contains(flag))
                    next.Add(flag);
                else if (!include && field.Multiple)
                    next.Remove(flag);
            Apply(next);
        }

        /// <summary>Removes missing slots without changing the remaining membership.</summary>
        internal void RemoveMissing()
        {
            if (session != null && Valid)
            {
                session.SetSelection(field, expected.Where(flag => flag != null).ToArray(), true);
                expected = session.Values(field);
                changed?.Invoke();
                return;
            }
            Apply(expected.Where(flag => flag != null).ToList());
        }

        /// <summary>Writes one selection only while its named context and original membership still match.</summary>
        /// <param name="next">Explicit replacement membership.</param>
        private void Apply(List<ObjectFlag> next)
        {
            try
            {
                if (!Valid)
                    return;
                if (session != null)
                {
                    session.SetSelection(field, next.ToArray());
                    expected = session.Values(field);
                    changed?.Invoke();
                    return;
                }
                if (!Valid || !ObjectFlagAssetAccess.Writable(field.AssetPath))
                    throw new InvalidOperationException("This asset is no longer writable. Refresh the scan.");
                bool found = field.Visit((owner, property) =>
                {
                    if (!field.Matches(property, expected) || ObjectFlagLabels.Path(property, field.Portable) != field.Label)
                        throw new InvalidOperationException("This selection changed outside the tool. Refresh the scan before editing it.");
                    ObjectFlagStorage.Write(property, field.Portable, next);
                    bool applied = property.serializedObject.ApplyModifiedProperties();
                    expected = field.Multiple ? next.ToArray() : new[] { next.Count > 0 ? next[0] : null };
                    field.Flags = expected;
                    field.Missing = 0;
                    return applied;
                }, true);
                if (!found)
                    throw new InvalidOperationException("The original field no longer exists. Refresh the scan.");
                changed?.Invoke();
            }
            catch (Exception exception)
            {
                valid = false;
                ObjectFlagOperations.Report(exception.Message, new[] { field.Location });
            }
        }

        #endregion
        #region Navigation

        /// <summary>Loads a closed scene additively before handing its owner to a persistent editor window.</summary>
        /// <param name="field">Field whose exact owner should be opened.</param>
        internal static void Ping(ObjectFlagField field)
        {
            if (field.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                Scene scene = SceneManager.GetSceneByPath(field.AssetPath);
                if (!scene.IsValid() || !scene.isLoaded)
                    EditorSceneManager.OpenScene(field.AssetPath, OpenSceneMode.Additive);
            }
            if (!field.Visit((owner, property) =>
            {
                ObjectFlagProject.Open(owner, field.PropertyPath);
                return false;
            }))
                ObjectFlagOperations.Report("The original field no longer exists. Refresh the scan.", new[] { field.Location });
        }

        #endregion
        #endregion
    }
}
