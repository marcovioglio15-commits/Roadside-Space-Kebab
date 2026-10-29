using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Indents complete foldout contents, including layout buttons and custom rows.</summary>
    public sealed class StudioIndentScope : IDisposable
    {
        #region Types

        /// <summary>Skips layout creation while editor controls are used only to collect metadata.</summary>
        public readonly struct Suspension : IDisposable
        {
            private readonly bool active;

            #region Methods

            /// <summary>Suspends indentation for a discovery pass, including nested calls.</summary>
            /// <param name="suspend">Whether this scope collects metadata without drawing controls.</param>
            public Suspension(bool suspend)
            {
                active = suspend;
                if (active)
                    suspensions++;
            }

            /// <summary>Restores layout creation after this discovery pass.</summary>
            public void Dispose()
            {
                if (active)
                    suspensions--;
            }

            #endregion
        }

        #endregion

        #region State

        private static int suspensions;
        private readonly EditorGUILayout.HorizontalScope row;
        private readonly EditorGUILayout.VerticalScope column;
        private readonly EditorGUI.IndentLevelScope nativeIndent;

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Moves the whole child layout right while keeping native labels aligned with custom controls.</summary>
        public StudioIndentScope()
        {
            if (suspensions > 0 || Event.current == null)
                return;
            row = new EditorGUILayout.HorizontalScope();
            GUILayout.Space(24f + EditorGUI.IndentedRect(new Rect()).x);
            column = new EditorGUILayout.VerticalScope();
            nativeIndent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
        }

        /// <summary>Restores native indentation and closes both layout groups even after ExitGUI.</summary>
        public void Dispose()
        {
            nativeIndent?.Dispose();
            column?.Dispose();
            row?.Dispose();
        }

        #endregion

        #endregion
    }
}
