using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Refreshes runtime group data at explicit Play, build and source-group changes.</summary>
    internal sealed class InteractionGroupBuild : AssetPostprocessor, IPreprocessBuildWithReport
    {
        #region State

        private static bool queued;

        #endregion

        #region Properties

        /// <summary>Prepares contact data before scene and prefab dependencies enter a build.</summary>
        public int callbackOrder => -100;

        #endregion

        #region Methods

        #region Boundaries

        /// <summary>Prepares current group values before Play instantiates any affected items.</summary>
        /// <param name="options">Current scene and domain reload policy.</param>
        [InitializeOnEnterPlayMode]
        private static void EnterPlay(EnterPlayModeOptions options)
        {
            CompileAll();
        }

        /// <summary>Includes the latest saved groups without referencing editor-only assets in the player.</summary>
        /// <param name="report">Build receiving the prepared contact operations.</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            CompileAll();
        }

        /// <summary>Queues one refresh when a group or reusable contact rule is imported.</summary>
        /// <param name="imported">Imported asset paths.</param>
        /// <param name="deleted">Deleted asset paths.</param>
        /// <param name="moved">New asset paths.</param>
        /// <param name="previous">Previous paths of moved assets.</param>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] previous)
        {
            if (queued || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            // Runtime preparation is event-driven; no asset search runs during ordinary repaint or gameplay.
            if (!Array.Exists(imported, path => AssetDatabase.GetMainAssetTypeAtPath(path) is Type type
                && (type == typeof(ObjectGroupPreset) || type == typeof(ContactModificationDefinition))))
                return;
            queued = true;
            EditorApplication.delayCall += CompileQueued;
        }

        /// <summary>Coalesces several group imports into one project pass.</summary>
        private static void CompileQueued()
        {
            queued = false;
            CompileAll();
        }

        /// <summary>Updates only changed rule payloads and preserves unrelated rule settings.</summary>
        internal static void CompileAll()
        {
            // Contact operations belong to reusable definitions referenced by the interaction components.
            foreach (string guid in AssetDatabase.FindAssets("t:ContactModificationDefinition"))
            {
                ContactModificationDefinition definition = AssetDatabase.LoadAssetAtPath<ContactModificationDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition?.Settings == null)
                    continue;
                string before = EditorJsonUtility.ToJson(definition);
                Compile(definition.Settings.Self?.Interactions);
                Compile(definition.Settings.Other?.Interactions);
                if (before == EditorJsonUtility.ToJson(definition))
                    continue;
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }
        }

        /// <summary>Compiles each configured operation while retaining visible validation warnings.</summary>
        /// <param name="changes">Operations for one contact participant.</param>
        private static void Compile(InteractionChangeSettings[] changes)
        {
            if (changes == null)
                return;
            foreach (InteractionChangeSettings change in changes)
                if (change != null)
                    InteractionGroupCompiler.Compile(change);
        }

        #endregion

        #endregion
    }
}
