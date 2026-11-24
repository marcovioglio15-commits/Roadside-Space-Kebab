using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Saves project assignment filters and expanded navigation paths.</summary>
    [Serializable]
    internal sealed class ObjectFlagAssignmentSettings
    {
        #region Fields

        [Header("Scope")]
        [Tooltip("Search asset, interaction, order and field names.")]
        public string Search = string.Empty;
        [Tooltip("Filter by the display names of flags currently assigned, including pending changes.")]
        public string FlagSearch = string.Empty;
        [Tooltip("Hide selections with no assigned flags, including fields shown as None.")]
        public bool HideUnassigned;
        [Tooltip("Limit results to this asset and its referenced flag settings. Empty scans the project.")]
        public UnityEngine.Object TargetAsset;
        [Tooltip("Include flag selections stored in scene files.")]
        public bool IncludeScenes = true;
        [Tooltip("Include flag selections in package assets.")]
        public bool IncludePackages = true;
        [Tooltip("Show shared catalog selections beneath every prefab or preset that references them.")]
        public bool FollowSharedAssets = true;
        [Header("Navigation")]
        [Tooltip("Expanded assignment paths retained by this window.")]
        [HideInInspector]
        public List<string> Expanded = new List<string>();

        #endregion
    }
}
