using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Saves group definitions and the group tab's current filter.</summary>
    [CreateAssetMenu(menuName = "Studio Identity/Groups Preset", fileName = "Identity Flag Groups")]
    public sealed class ObjectFlagGroupsPreset : ScriptableObject
    {
        #region Fields

        [Header("Groups")]
        [Tooltip("Named groups, including groups that do not contain flags yet.")]
        public List<string> Groups = new List<string>();
        [Tooltip("Search flag names and descriptions inside the selected group.")]
        public string Search = string.Empty;
        [Tooltip("Show one group instead of the whole catalog.")]
        public bool FilterGroup;
        [Tooltip("Group shown when group filtering is enabled. Empty selects ungrouped flags.")]
        public string SelectedGroup = string.Empty;

        #endregion
    }
}
