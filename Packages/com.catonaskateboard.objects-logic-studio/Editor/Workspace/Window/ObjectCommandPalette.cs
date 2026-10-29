using CatOnASkateboard.StudioColors.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Finds settings only on the current object's existing interactions, grouped by category.</summary>
    internal sealed class ObjectCommandPalette : EditorWindow
    {
        #region Types

        /// <summary>Stores one existing component and its searchable thematic groups.</summary>
        private sealed class Entry
        {
            internal ObjectInteraction Feature;
            internal ObjectStudioNavigation.Categories Category;
            internal readonly List<ObjectGroupInfo> Groups = new List<ObjectGroupInfo>();
            internal readonly List<string> Text = new List<string>();
        }

        #endregion

        #region Fields

        [Header("Search")]
        [Tooltip("Workspace whose current object is being searched.")]
        [SerializeField]
        private ObjectWorkspace workspace;
        [Tooltip("Setting or interaction to find.")]
        [SerializeField]
        private string search = string.Empty;
        [Tooltip("Scroll position in the results.")]
        [SerializeField]
        private Vector2 scroll;

        #endregion

        #region State

        private readonly List<Entry> entries = new List<Entry>();
        private GameObject target;
        private bool dirty = true;
        private bool focusSearch = true;

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Opens a settings-only finder when a current object exists.</summary>
        /// <param name="state">Workspace defining the object scope.</param>
        internal static void Open(ObjectWorkspace state)
        {
            if (ObjectWorkspaceContext.Target(state) == null)
                return;
            ObjectCommandPalette window = CreateInstance<ObjectCommandPalette>();
            window.workspace = state;
            window.titleContent = new GUIContent("Find settings");
            window.minSize = new Vector2(440f, 320f);
            window.ShowUtility();
        }

        /// <summary>Refreshes the index after structural changes rather than on every repaint.</summary>
        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
        }

        /// <summary>Releases editor event listeners when the finder closes.</summary>
        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Invalidate;
            EditorApplication.projectChanged -= Invalidate;
        }

        /// <summary>Requests one index update after an editor change.</summary>
        private void Invalidate()
        {
            dirty = true;
            Repaint();
        }

        /// <summary>Indexes existing scoped interactions and their settings without adding commands.</summary>
        private void Build()
        {
            target = ObjectWorkspaceContext.Target(workspace);
            entries.Clear();
            foreach (ObjectInteraction feature in ObjectWorkspaceContext.Interactions(workspace))
            {
                if ((workspace.Navigation.Filters & ObjectInteractionCatalog.Find(feature.GetType()).Category) == 0)
                    continue;
                Entry entry = new Entry { Feature = feature, Category = ObjectInteractionCatalog.Find(feature.GetType()).Category };
                ObjectWorkspace document = ObjectPresetCapture.Document(feature);
                try
                {
                    using SerializedObject data = new SerializedObject(document);
                    foreach (ObjectGroupInfo group in ObjectInteractionPanel.Groups(document, data))
                    {
                        entry.Groups.Add(group);
                        entry.Text.Add((feature.InteractionName + " " + ObjectInteractionCatalog.Name(feature) + " " + ObjectSettingsSearch.Describe(group, data)).ToLowerInvariant());
                    }
                }
                finally
                {
                    DestroyImmediate(document);
                }
                entries.Add(entry);
            }
            entries.Sort((left, right) => ObjectSettingsSearch.Compare(left.Feature, right.Feature, ObjectStudioNavigation.Order.Type));
            dirty = false;
        }

        #endregion

        #region Drawing

        /// <summary>Shows category dropdowns containing existing interactions and settings matches.</summary>
        private void OnGUI()
        {
            if (workspace == null || ObjectWorkspaceContext.Target(workspace) == null)
            {
                Close();
                return;
            }
            if (dirty || target != ObjectWorkspaceContext.Target(workspace))
                Build();
            GUI.SetNextControlName("Object command search");
            search = EditorGUILayout.TextField(new GUIContent(string.Empty, "Find an interaction or setting on this object."), search, EditorStyles.toolbarSearchField);
            if (focusSearch && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("Object command search");
                focusSearch = false;
            }
            if (Event.current.type == EventType.KeyDown)
                switch (Event.current.keyCode)
                {
                    case KeyCode.Escape:
                        Close();
                        Event.current.Use();
                        return;
                    case KeyCode.Return:
                        foreach (Entry entry in entries)
                            for (int index = 0; index < entry.Groups.Count; index++)
                                if (Matches(entry.Text[index]))
                                {
                                    Navigate(entry.Feature, entry.Groups[index]);
                                    return;
                                }
                        break;
                }
            using EditorGUILayout.ScrollViewScope content = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = content.scrollPosition;
            bool any = false;
            foreach (ObjectStudioNavigation.Categories category in Enum.GetValues(typeof(ObjectStudioNavigation.Categories)))
            {
                if (category is ObjectStudioNavigation.Categories.Nothing or ObjectStudioNavigation.Categories.Everything
                    || !entries.Exists(entry => entry.Category == category && entry.Text.Exists(Matches)))
                    continue;
                any = true;
                if (!ObjectWorkspaceGUI.Foldout(workspace, "Finder.Category." + category, ObjectStudioNavigation.CategoryName(category), "Show settings in this category."))
                    continue;
                using StudioIndentScope categoryContent = new StudioIndentScope();
                foreach (Entry entry in entries)
                {
                    if (entry.Category != category || !entry.Text.Exists(Matches))
                        continue;
                    bool expanded;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        expanded = ObjectWorkspaceGUI.Foldout(workspace, "Finder.Feature." + ObjectWorkspaceTarget.FileId(entry.Feature), entry.Feature.InteractionName + " · " + ObjectInteractionCatalog.Name(entry.Feature), "Show this interaction's settings.");
                        if (GUILayout.Button(new GUIContent("Select", "Select this interaction."), EditorStyles.miniButton, GUILayout.Width(48f)))
                        {
                            Navigate(entry.Feature, null);
                            return;
                        }
                    }
                    if (!expanded)
                        continue;
                    using StudioIndentScope interactionContent = new StudioIndentScope();
                    for (int index = 0; index < entry.Groups.Count; index++)
                        if (Matches(entry.Text[index]) && ObjectWorkspaceGUI.Row(entry.Groups[index].Title, false, "Open these settings."))
                        {
                            Navigate(entry.Feature, entry.Groups[index]);
                            return;
                        }
                }
            }
            if (!any)
                EditorGUILayout.LabelField("No settings match on this object.", EditorStyles.miniLabel);
        }

        /// <summary>Matches a short search against cached names and descriptions.</summary>
        /// <param name="text">Indexed settings text.</param>
        /// <returns>True when every typed word is present.</returns>
        private bool Matches(string text)
        {
            foreach (string word in search.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (!text.Contains(word))
                    return false;
            return true;
        }

        /// <summary>Opens the exact existing component and optionally its thematic settings window.</summary>
        /// <param name="feature">Existing scoped interaction.</param>
        /// <param name="group">Optional group to open.</param>
        private void Navigate(ObjectInteraction feature, ObjectGroupInfo group)
        {
            switch (feature)
            {
                case ObjectHover hover:
                    ObjectsLogicStudioWindow.Open(hover);
                    break;
                case ObjectSingleInteraction single:
                    ObjectsLogicStudioWindow.Open(single);
                    break;
                case ObjectExtendedInteraction extended:
                    ObjectsLogicStudioWindow.Open(extended);
                    break;
            }
            workspace.Navigation.Collapsed.Remove("View.Interaction");
            if (group != null)
                ObjectGroupWindow.Open(workspace, group);
            ObjectsLogicStudioWindow.RefreshOpen();
            Close();
        }

        #endregion

        #endregion
    }
}
