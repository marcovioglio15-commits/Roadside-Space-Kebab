using System.Collections.Generic;

namespace CatOnASkateboard.StudioIdentity
{
    /// <summary>Chooses whether one or every selected flag must be present.</summary>
    public enum ObjectFlagMatch { Any, All }
    /// <summary>Chooses how a successful interaction updates an object's runtime flags.</summary>
    public enum ObjectFlagOperation { Replace, Add, Remove, Toggle, Clear }

    /// <summary>Shares flag-list validation between identities and interaction requirements.</summary>
    public static class ObjectFlagRules
    {
        #region Methods

        #region Validation

        /// <summary>Checks supported flag operations without runtime type inspection.</summary>
        /// <param name="operation">Serialized or requested membership operation.</param>
        /// <returns>True for a defined operation.</returns>
        public static bool IsValidOperation(ObjectFlagOperation operation)
        {
            // Unknown serialized values remain invalid until explicitly corrected.
            return operation is ObjectFlagOperation.Replace or ObjectFlagOperation.Add or ObjectFlagOperation.Remove
                or ObjectFlagOperation.Toggle or ObjectFlagOperation.Clear;
        }

        /// <summary>Rejects missing references and duplicates before runtime flag matching.</summary>
        /// <param name="flags">Proposed flag selection.</param>
        /// <param name="allowEmpty">Whether an empty identity or filter is permitted.</param>
        /// <param name="warning">Receives a missing or duplicate flag.</param>
        /// <returns>True when the complete list is usable.</returns>
        public static bool TryValidate(IReadOnlyList<ObjectFlag> flags, bool allowEmpty, out string warning)
        {
            // Validation runs at authoring and activation boundaries, never inside each match.
            warning = "Select distinct, assigned object flags.";
            if (flags == null || !allowEmpty && flags.Count == 0)
                return false;
            HashSet<ObjectFlag> unique = new HashSet<ObjectFlag>();
            foreach (ObjectFlag flag in flags)
            {
                warning = "Select distinct, assigned object flags.";
                if (flag == null || !unique.Add(flag) || !flag.TryValidate(out warning))
                    return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
