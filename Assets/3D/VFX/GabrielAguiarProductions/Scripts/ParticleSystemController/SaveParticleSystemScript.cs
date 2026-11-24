using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace GAP_ParticleSystemController
{
    /// <summary>Stores the particle controller's original settings beside its prefab as JSON.</summary>
    public static class SaveParticleSystemScript
    {
        #region Data

        private const string settingsFormat = "GAP.ParticleSettings.1";

        /// <summary>Wraps the settings list for Unity's field-based JSON serializer.</summary>
        [Serializable]
        private sealed class SettingsData
        {
            [Tooltip("Settings format identifier. Checked before loading the saved values.")]
            public string Format;
            [Tooltip("Original values in the same order as the controller's particle systems.")]
            public List<ParticleSystemOriginalSettings> Settings;
        }

        #endregion
        #region Methods
        #region Persistence

        /// <summary>Saves original values before controller adjustments and updates an open prefab stage.</summary>
        /// <param name="prefabVFX">Prefab or instance whose original values are being stored.</param>
        /// <param name="originalSettings">Original values in particle-system order.</param>
        public static void SaveVFX(GameObject prefabVFX, List<ParticleSystemOriginalSettings> originalSettings)
        {
            string path = SettingsPath(prefabVFX);
            if (string.IsNullOrEmpty(path) || originalSettings == null)
            {
                Debug.LogWarning("Select a prefab and supply its original particle settings before saving.", prefabVFX);
                return;
            }

            // Keep field data separate from prefab serialization and close file handles immediately.
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new SettingsData { Format = settingsFormat, Settings = originalSettings }, true));
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.ImportAsset(path);
#endif
            SaveNestedPrefab(prefabVFX);
            Debug.Log("Original settings saved: " + path, prefabVFX);
        }

        /// <summary>Loads original values without applying them to the current particle systems.</summary>
        /// <param name="prefabVFX">Prefab or instance owning the settings file.</param>
        /// <returns>Saved values in particle-system order, or null when no file exists.</returns>
        public static List<ParticleSystemOriginalSettings> LoadVFX(GameObject prefabVFX)
        {
            string path = SettingsPath(prefabVFX);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            // A malformed file must remain intact for inspection instead of becoming new defaults.
            SettingsData data = JsonUtility.FromJson<SettingsData>(File.ReadAllText(path));
            if (data?.Format != settingsFormat || data.Settings == null)
                throw new InvalidDataException("Unsupported or incomplete particle settings: " + path);
            return data.Settings;
        }

        /// <summary>Checks whether the controller already has a saved set of original values.</summary>
        /// <param name="prefabVFX">Prefab or instance owning the settings file.</param>
        /// <returns>True when its original-settings file exists.</returns>
        public static bool CheckExistingFile(GameObject prefabVFX)
        {
            return File.Exists(SettingsPath(prefabVFX));
        }

        #endregion
        #region Prefab Access

        /// <summary>Resolves a prefab-stage object, persistent prefab or scene instance to its settings file.</summary>
        /// <param name="prefabVFX">Object managed by the particle controller.</param>
        /// <returns>Project-relative JSON path, or null without an editor prefab connection.</returns>
        private static string SettingsPath(GameObject prefabVFX)
        {
#if UNITY_EDITOR
            if (prefabVFX == null)
                return null;
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(prefabVFX);
            string path = stage != null ? stage.assetPath : AssetDatabase.GetAssetPath(prefabVFX);
            if (string.IsNullOrEmpty(path))
                path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabVFX);
            return string.IsNullOrEmpty(path) ? null
                : Path.Combine(Path.GetDirectoryName(path), "OriginalSettings", prefabVFX.name + ".json").Replace('\\', '/');
#else
            return null;
#endif
        }

        /// <summary>Saves the open prefab stage after explicit controller adjustments.</summary>
        /// <param name="prefab">Object belonging to the edited prefab stage.</param>
        public static void SaveNestedPrefab(GameObject prefab)
        {
#if UNITY_EDITOR
            // Scene instances and persistent prefab assets do not have an open stage to save.
            PrefabStage stage = prefab != null ? PrefabStageUtility.GetPrefabStage(prefab) : null;
            if (stage != null)
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
#endif
        }

        #endregion
        #endregion
    }
}
