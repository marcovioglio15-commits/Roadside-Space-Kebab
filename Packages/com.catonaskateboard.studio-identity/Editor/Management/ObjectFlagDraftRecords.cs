using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Retains one isolated flag definition until the whole tool is applied.</summary>
    [Serializable]
    internal sealed class ObjectFlagDefinitionDraft
    {
        #region Fields

        [Tooltip("Existing definition. Empty for a new flag.")]
        public ObjectFlag Source;
        [Tooltip("Detached editable definition.")]
        public ObjectFlag Working;
        [Tooltip("Source metadata captured before editing.")]
        public string Baseline;
        [Tooltip("Definition is new and has no project asset yet.")]
        public bool Created;
        [Tooltip("Definition will be removed on Apply.")]
        public bool Deleted;

        #endregion
        #region Properties

        internal ObjectFlag Identity => Created ? Working : Source;
        internal bool Changed => Created ? !Deleted : Deleted || Working != null && JsonUtility.ToJson(Working) != Baseline;

        #endregion
    }

    /// <summary>Retains an exact field baseline and its proposed membership.</summary>
    [Serializable]
    internal sealed class ObjectFlagSelectionDraft
    {
        #region Fields

        [Tooltip("Stable field location and original scan values.")]
        public ObjectFlagField Field;
        [Tooltip("Serialized field hash used to reject outside edits.")]
        public string Baseline;
        [Tooltip("Proposed flag membership.")]
        public ObjectFlag[] Values = Array.Empty<ObjectFlag>();
        [Tooltip("Clear missing scalar references even when their resolved value is already null.")]
        public bool CleanMissing;

        #endregion
        #region Properties

        internal bool Changed => CleanMissing || !Field.Flags.SequenceEqual(Values);

        #endregion
    }

    /// <summary>Stages group definitions independently of navigation filters in the same preset.</summary>
    [Serializable]
    internal sealed class ObjectFlagGroupDraft
    {
        #region Fields

        [Tooltip("Preset storing named empty groups.")]
        public ObjectFlagGroupsPreset Source;
        [Tooltip("Original named groups.")]
        public List<string> Baseline = new List<string>();
        [Tooltip("Proposed named groups.")]
        public List<string> Values = new List<string>();

        #endregion
        #region Properties

        internal bool Changed => !Baseline.SequenceEqual(Values);

        #endregion
    }
}
