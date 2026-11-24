using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Hosts flag-only validation and scoped assignment browsing over a cached search index.</summary>
    internal sealed class ObjectFlagReferencesView
    {
        #region State

        private readonly bool validation;
        private readonly ObjectFlagTree tree = new ObjectFlagTree();
        private ObjectFlagValidationPreset validationPreset;
        private ObjectFlagCatalogWindow settingsOwner;
        private ScriptableObject previousPreset;
        private ObjectFlagDraftSession session;
        private ObjectFlagScan scan;
        private UnityEngine.Object scannedAsset;
        private bool scannedFollow;
        private bool stale;
        private bool rebuild;
        private bool filterPending;
        private bool scanPending;
        private double searchDue;
        private double scanDue;
        private string status = string.Empty;

        #endregion
        #region Properties

        private ObjectFlagAssignmentSettings assignments => settingsOwner != null ? settingsOwner.AssignmentSettings : null;
        private bool Ready => validation ? validationPreset != null : assignments != null && settingsOwner != null;
        private string Search => validation ? validationPreset.Search : assignments.Search;
        private List<string> Expanded => validation ? validationPreset.Expanded : assignments.Expanded;

        #endregion
        #region Methods
        #region State

        /// <summary>Creates one view without allocating asset previews or starting a scan.</summary>
        /// <param name="isValidation">Whether this view stages missing flag cleanup.</param>
        internal ObjectFlagReferencesView(bool isValidation)
        {
            validation = isValidation;
        }

        /// <summary>Marks source results stale without rebuilding their graph.</summary>
        internal void Invalidate()
        {
            stale = scan != null;
        }

        /// <summary>Queues a fresh scan after Apply so saved memberships replace the previous index.</summary>
        internal void Applied()
        {
            stale = scanPending = scan != null;
            scanDue = EditorApplication.timeSinceStartup + 0.12d;
        }

        /// <summary>Refreshes only proposal visibility after an Undo or staged edit.</summary>
        internal void DraftChanged()
        {
            filterPending = scan != null;
            searchDue = 0d;
        }

        /// <summary>Runs one requested scoped scan or a debounced cached search.</summary>
        /// <returns>True when visible results changed.</returns>
        internal bool UpdateSearch()
        {
            if (session == null || session.Released || !Ready)
                return false;
            if (scanPending)
            {
                if (EditorApplication.timeSinceStartup < scanDue || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    return false;
                scanPending = false;
                Scan();
                return true;
            }
            if (!filterPending || scan == null || rebuild || EditorApplication.timeSinceStartup < searchDue)
                return false;
            Filter();
            filterPending = false;
            return true;
        }

        #endregion
        #region Drawing

        /// <summary>Draws only settings relevant to the current asset scope and the cached result tree.</summary>
        /// <param name="draft">Shared management proposal.</param>
        /// <param name="owner">Window retaining assignment navigation.</param>
        internal void Draw(ObjectFlagDraftSession draft, ObjectFlagCatalogWindow owner = null)
        {
            session = draft;
            if (validation)
                ObjectFlagPresetGUI.Draw(ref validationPreset);
            else
                settingsOwner = owner;
            if (!Ready)
            {
                GUILayout.FlexibleSpace();
                return;
            }
            if (validation && previousPreset != validationPreset)
            {
                previousPreset = validationPreset;
                scan = null;
                status = string.Empty;
                scanPending = false;
            }
            bool filtersChanged = Fields("Search");
            if (!validation)
                filtersChanged |= Fields("FlagSearch", "HideUnassigned");
            if (filtersChanged)
            {
                filterPending = true;
                searchDue = EditorApplication.timeSinceStartup + 0.12d;
            }
            if (!validation && Fields("TargetAsset"))
            {
                if (scan != null && scannedAsset == null && !stale)
                    rebuild = true;
                else if (assignments.TargetAsset != null)
                    scanPending = true;
                else
                    scan = null;
            }
            if ((validation || assignments.TargetAsset == null) && Fields("IncludeScenes"))
                stale = scan != null;
            if ((validation || assignments.TargetAsset == null || assignments.FollowSharedAssets)
                && Fields("IncludePackages"))
                stale = scan != null;
            if (validation)
            {
                if (Fields("EmptyFlagSlots"))
                    stale = scan != null;
            }
            else if (Fields("FollowSharedAssets"))
            {
                if (scannedAsset != null && !scannedFollow && assignments.FollowSharedAssets)
                    scanPending = true;
                else
                    rebuild = scan != null;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(scan == null ? "Scan" : "Refresh Scan", "Inspect only flag references in the selected scope."), GUILayout.Width(130f)))
                {
                    Scan();
                    GUIUtility.ExitGUI();
                }
                if (validation && scan != null)
                    using (new EditorGUI.DisabledScope(!scan.Complete || !tree.FilteredFields.Any()))
                        if (GUILayout.Button(new GUIContent("Clean Filtered Results", "Stage cleanup of the visible missing flag references."), GUILayout.Width(170f)))
                            Clean(null);
                if (!validation && scan != null
                    && GUILayout.Button(new GUIContent("Expand All", "Expand every indexed assignment branch."), GUILayout.Width(95f)))
                {
                    tree.ExpandAll(Expanded);
                    EditorUtility.SetDirty(settingsOwner);
                }
                EditorGUILayout.LabelField(stale ? "Sources may have changed. Refresh to update results." : status, EditorStyles.miniLabel);
            }
            if (scan == null)
            {
                GUILayout.FlexibleSpace();
                return;
            }
            if (rebuild)
            {
                tree.Build(scan, validation, !validation && assignments.FollowSharedAssets, Expanded, validation ? null : assignments.TargetAsset);
                Filter();
                rebuild = false;
                filterPending = false;
            }
            if (tree.Draw(Expanded, session, DraftChanged, Clean))
                EditorUtility.SetDirty(validation ? validationPreset : settingsOwner);
        }

        /// <summary>Draws stored tooltips while keeping assignment preferences inside their window.</summary>
        /// <param name="names">Settings shown for the current scope.</param>
        /// <returns>True when a displayed setting changed.</returns>
        private bool Fields(params string[] names)
        {
            if (validation)
                return ObjectFlagPresetGUI.Fields(validationPreset, names);
            using SerializedObject data = new SerializedObject(settingsOwner);
            SerializedProperty settings = data.FindProperty("assignmentSettings");
            foreach (string name in names)
                EditorGUILayout.PropertyField(settings.FindPropertyRelative(name));
            return data.ApplyModifiedProperties();
        }

        /// <summary>Combines path and assigned-flag searches over the existing index and draft.</summary>
        private void Filter()
        {
            tree.Filter(Search, session, validation ? string.Empty : assignments.FlagSearch, !validation && assignments.HideUnassigned);
        }

        #endregion
        #region Scanning and Cleanup

        /// <summary>Builds an index once from the explicit asset scope, following only actual shared links.</summary>
        private void Scan()
        {
            scannedAsset = validation ? null : assignments.TargetAsset;
            scannedFollow = !validation && assignments.FollowSharedAssets;
            scan = ObjectFlagProject.Scan(validation ? validationPreset.IncludeScenes : assignments.IncludeScenes,
                validation ? validationPreset.IncludePackages : assignments.IncludePackages,
                !validation || validationPreset.EmptyFlagSlots, scannedAsset, scannedFollow);
            status = validation ? scan.Fields.Sum(field => session.Missing(field)) + " missing flag reference(s)."
                : scan.Fields.Count + " flag selections.";
            if (!scan.Complete)
                status += scan.Cancelled ? " Scan cancelled; partial results." : " Some assets could not be read. See Console.";
            foreach (string error in scan.Errors)
                Debug.LogWarning(error);
            stale = false;
            rebuild = true;
        }

        /// <summary>Stages cleanup against the visible indexed fields without writing or rescanning assets.</summary>
        /// <param name="field">One selected field, or null for all filtered results.</param>
        private void Clean(ObjectFlagField field)
        {
            ObjectFlagField[] targets = field != null ? new[] { field } : tree.FilteredFields.ToArray();
            try
            {
                session.Clean(targets);
                status = targets.Length + " field(s) staged for cleanup.";
                DraftChanged();
            }
            catch (InvalidOperationException exception)
            {
                ObjectFlagOperations.Report(exception.Message, Array.Empty<string>());
            }
        }

        /// <summary>Saves navigation preferences without committing the flag proposal.</summary>
        internal void SaveNavigation()
        {
            if (validationPreset != null)
                AssetDatabase.SaveAssetIfDirty(validationPreset);
        }

        #endregion
        #endregion
    }
}
