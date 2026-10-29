using CatOnASkateboard.StudioColors.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Caches dropdown buckets for the current object's filtered interactions.</summary>
    internal sealed class ObjectInteractionList
    {
        #region Types

        /// <summary>Groups components under a type name or name initial.</summary>
        internal sealed class Bucket
        {
            internal readonly string Name;
            internal readonly List<ObjectInteraction> Entries = new List<ObjectInteraction>();

            #region Methods

            /// <summary>Creates one list group during refresh.</summary>
            /// <param name="name">Dropdown label.</param>
            internal Bucket(string name)
            {
                Name = name;
            }

            #endregion
        }

        #endregion

        #region State

        private readonly List<Bucket> buckets = new List<Bucket>();
        private readonly List<ObjectInteraction> visible = new List<ObjectInteraction>();

        #endregion

        #region Properties

        /// <summary>Exact components used to filter the adjacent preset library.</summary>
        internal IReadOnlyList<ObjectInteraction> Visible => visible;

        #endregion

        #region Methods

        #region Catalog

        /// <summary>Groups only matching components after the search index has refreshed.</summary>
        /// <param name="state">Workspace retaining filters and grouping.</param>
        /// <param name="interactions">Existing components in the browsing scope.</param>
        internal void Rebuild(ObjectWorkspace state, ObjectInteraction[] interactions)
        {
            buckets.Clear();
            visible.Clear();
            Array.Sort(interactions, (left, right) => ObjectSettingsSearch.Compare(left, right, state.Navigation.Sort));
            foreach (ObjectInteraction feature in interactions)
            {
                if (!ObjectSettingsSearch.Matches(state, feature))
                    continue;
                visible.Add(feature);
                string name = state.Navigation.Sort == ObjectStudioNavigation.Order.Name ? Initial(feature.InteractionName) : ObjectInteractionCatalog.Name(feature);
                Bucket bucket = buckets.Find(entry => entry.Name == name);
                if (bucket == null)
                {
                    bucket = new Bucket(name);
                    buckets.Add(bucket);
                }
                bucket.Entries.Add(feature);
            }
        }

        /// <summary>Draws compact dropdown groups and reports an explicit selection.</summary>
        /// <param name="state">Workspace retaining collapsed buckets.</param>
        /// <returns>The clicked component, or null.</returns>
        internal ObjectInteraction Draw(ObjectWorkspace state)
        {
            foreach (Bucket bucket in buckets)
            {
                if (!ObjectWorkspaceGUI.Foldout(state, "List." + state.Navigation.Sort + "." + bucket.Name, bucket.Name, "Show interactions in this group."))
                    continue;
                using StudioIndentScope indent = new StudioIndentScope();
                foreach (ObjectInteraction feature in bucket.Entries)
                {
                    string label = (feature.enabled ? "● " : "○ ") + feature.InteractionName
                        + (state.Navigation.Sort == ObjectStudioNavigation.Order.Name ? " · " + ObjectInteractionCatalog.Name(feature) : string.Empty);
                    if (ObjectWorkspaceGUI.Row(label, ObjectWorkspaceTarget.FileId(feature) == state.Navigation.SelectedId))
                        return feature;
                }
            }
            return null;
        }

        /// <summary>Groups blank or non-letter names under one visible fallback heading.</summary>
        /// <param name="name">Interaction name.</param>
        /// <returns>The uppercase initial or a hash.</returns>
        private static string Initial(string name)
        {
            string trimmed = name?.TrimStart();
            return !string.IsNullOrEmpty(trimmed) && char.IsLetter(trimmed[0]) ? char.ToUpperInvariant(trimmed[0]).ToString() : "#";
        }

        #endregion

        #endregion
    }
}
