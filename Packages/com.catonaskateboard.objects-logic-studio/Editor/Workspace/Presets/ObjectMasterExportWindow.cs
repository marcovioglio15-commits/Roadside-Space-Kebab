using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Previews the exact asset list exported with a complete object configuration.</summary>
    internal sealed class ObjectMasterExportWindow : EditorWindow
    {
        #region Types

        /// <summary>Records the package requirements beside a portable configuration.</summary>
        [Serializable]
        private sealed class Manifest
        {
            [Tooltip("Exported master name.")]
            public string Name;
            [Tooltip("Unity version used for export.")]
            public string Unity;
            [Tooltip("Required installed packages.")]
            public string[] Packages;
            [Tooltip("Assets included in this package.")]
            public string[] Assets;
        }

        #endregion

        #region Fields

        [Tooltip("Master configuration to export.")]
        [SerializeField]
        private ObjectMasterPreset master;
        [Tooltip("Include the original prefab as well as its configuration.")]
        [SerializeField]
        private bool includePrefab = true;
        [Tooltip("Include referenced models, materials, textures, audio and output prefabs.")]
        [SerializeField]
        private bool includeResources = true;
        [Tooltip("Selected assets to include in this export.")]
        [SerializeField]
        private List<string> assets = new List<string>();
        [Tooltip("Scroll position in the export preview.")]
        [SerializeField]
        private Vector2 scroll;

        #endregion

        #region State

        private string warning = string.Empty;
        private string[] packages = Array.Empty<string>();

        #endregion

        #region Methods

        #region Opening

        /// <summary>Opens the reviewable dependency list before any package is written.</summary>
        /// <param name="preset">Master asset to export.</param>
        internal static void Open(ObjectMasterPreset preset)
        {
            ObjectMasterExportWindow window = CreateInstance<ObjectMasterExportWindow>();
            window.master = preset;
            window.titleContent = new GUIContent("Export object configuration");
            window.minSize = new Vector2(460f, 320f);
            window.Refresh();
            window.Show();
        }

        #endregion

        #region Dependencies

        /// <summary>Builds an explicit asset list without silently adding package code or external scripts.</summary>
        private void Refresh()
        {
            assets.Clear();
            if (master == null)
                return;
            HashSet<string> roots = new HashSet<string>(StringComparer.Ordinal);
            roots.Add(AssetDatabase.GetAssetPath(master));
            List<ObjectPresetLink> links = new List<ObjectPresetLink>();
            foreach (ObjectMasterPreset.InteractionEntry entry in master.Interactions)
            {
                if (entry.Preset == null)
                    continue;
                roots.Add(AssetDatabase.GetAssetPath(entry.Preset));
                if (entry.Preset.SourcePreset != null)
                    roots.Add(AssetDatabase.GetAssetPath(entry.Preset.SourcePreset));
                foreach (ObjectPresetValue field in entry.Preset.Configuration.Fields)
                    field.CollectLinks(links);
                foreach (ObjectGroupPreset group in entry.Preset.Groups)
                    if (group != null)
                    {
                        roots.Add(AssetDatabase.GetAssetPath(group));
                        if (group.Context != null)
                            roots.Add(AssetDatabase.GetAssetPath(group.Context));
                        foreach (ObjectPresetValue field in group.Values)
                            field.CollectLinks(links);
                    }
            }
            foreach (ObjectMasterPreset.SharedEntry entry in master.Shared)
                if (entry.Preset != null)
                {
                    roots.Add(AssetDatabase.GetAssetPath(entry.Preset));
                    foreach (ObjectComponentPreset component in entry.Preset.Components)
                        foreach (ObjectPresetValue field in component.Fields)
                            field.CollectLinks(links);
                }
            foreach (ObjectPresetLink link in links)
                if (link.Asset != null)
                {
                    string path = AssetDatabase.GetAssetPath(link.Asset);
                    if (includeResources || path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                        roots.Add(path);
                }
            if (includePrefab && master.SourcePrefab != null)
                roots.Add(AssetDatabase.GetAssetPath(master.SourcePrefab));
            HashSet<string> included = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> required = new HashSet<string>(StringComparer.Ordinal);
            foreach (string root in roots)
            {
                if (!string.IsNullOrEmpty(root))
                    included.Add(root);
                if (!includeResources || string.IsNullOrEmpty(root))
                    continue;
                foreach (string path in AssetDatabase.GetDependencies(root, true))
                {
                    if (!includePrefab && master.SourcePrefab != null && path == AssetDatabase.GetAssetPath(master.SourcePrefab))
                        continue;
                    if (path.StartsWith("Packages/", StringComparison.Ordinal))
                    {
                        string[] parts = path.Split('/');
                        if (parts.Length > 1)
                            required.Add(parts[1]);
                        continue;
                    }
                    if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                        continue;
                    included.Add(path);
                }
            }
            included.RemoveWhere(path => !path.StartsWith("Assets/", StringComparison.Ordinal));
            assets.AddRange(included);
            assets.Sort(StringComparer.Ordinal);
            required.Add("com.catonaskateboard.objects-logic-studio");
            required.Add("com.catonaskateboard.player-studio");
            packages = new string[required.Count];
            required.CopyTo(packages);
            for (int index = 0; index < packages.Length; index++)
            {
                UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + packages[index]);
                if (package != null)
                    packages[index] += "@" + package.version;
            }
            Array.Sort(packages, StringComparer.Ordinal);
        }

        #endregion

        #region Drawing

        /// <summary>Shows optional source resources and the exact export paths before saving.</summary>
        private void OnGUI()
        {
            if (master == null)
                return;
            EditorGUILayout.LabelField(master.name, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            includePrefab = EditorGUILayout.Toggle(new GUIContent("Include source prefab", "Include the original object."), includePrefab);
            includeResources = EditorGUILayout.Toggle(new GUIContent("Include resources", "Include the referenced models, effects, audio and other resources."), includeResources);
            if (EditorGUI.EndChangeCheck())
                Refresh();
            EditorGUILayout.LabelField("Install the matching Studio packages in the other project.", EditorStyles.wordWrappedMiniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string package in packages)
                EditorGUILayout.LabelField(package, EditorStyles.miniLabel);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(assets.Count + " assets", EditorStyles.boldLabel);
            foreach (string path in assets)
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(new GUIContent("Export package", "Save the listed assets as a Unity package.")))
                Export();
        }

        /// <summary>Writes only the reviewed assets and a small requirements file beside the package.</summary>
        private void Export()
        {
            string path = EditorUtility.SaveFilePanel("Export object configuration", string.Empty, master.name, "unitypackage");
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                UnityEditor.AssetPackage.Package.Export(new UnityEditor.AssetPackage.ExportPackageParameters(assets.ToArray(), path, string.Empty, ExportPackageOptions.Default));
                Manifest manifest = new Manifest { Name = master.name, Unity = Application.unityVersion, Packages = packages, Assets = assets.ToArray() };
                File.WriteAllText(Path.ChangeExtension(path, ".requirements.json"), JsonUtility.ToJson(manifest, true));
                warning = "Configuration package exported.";
            }
            catch (Exception exception)
            {
                warning = "Export could not finish: " + exception.Message;
            }
        }

        #endregion

        #endregion
    }
}
