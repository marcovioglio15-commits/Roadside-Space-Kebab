using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Grants native cursor capture to the embedded Play viewport on supported Unity Editor versions.</summary>
    [InitializeOnLoad]
    internal static class PlayerQuickPlayCursor
    {
        #region State

        private static readonly Action<bool> setAllowLock = FindPermission();

        #endregion

        #region Methods

        #region Permission

        /// <summary>Resolves Unity's Editor cursor permission once, outside gameplay assemblies.</summary>
        /// <returns>The cached permission delegate, or null when this Editor version does not expose it.</returns>
        private static Action<bool> FindPermission()
        {
            // Unity grants locking to Game views internally; an embedded Editor viewport needs the same permission.
            Type reasons = typeof(Unsupported).GetNestedType("DisallowCursorLockReasons", BindingFlags.NonPublic);
            if (reasons == null || !Enum.IsDefined(reasons, "Other"))
                return null;
            MethodInfo method = typeof(Unsupported).GetMethod("SetAllowCursorLock", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(bool), reasons }, null);
            if (method == null)
                return null;
            ParameterExpression allowed = Expression.Parameter(typeof(bool), "allowed");
            // Preserve Unity's separate focus, pause and modal-dialog restrictions.
            return Expression.Lambda<Action<bool>>(Expression.Call(method, allowed,
                Expression.Constant(Enum.Parse(reasons, "Other"), reasons)), allowed).Compile();
        }

        /// <summary>Changes Editor permission at viewport capture boundaries without changing runtime input semantics.</summary>
        /// <param name="allowed">Whether this focused preview may lock and hide the native cursor.</param>
        /// <returns>True when the installed Editor supports granting this permission.</returns>
        internal static bool TrySetAllowed(bool allowed)
        {
            // Calls use the cached delegate; no reflection or cursor override is added to the runtime package.
            if (setAllowLock == null)
                return false;
            setAllowLock(allowed);
            Unsupported.SetAllowCursorHide(allowed);
            return true;
        }

        #endregion

        #endregion
    }
}
