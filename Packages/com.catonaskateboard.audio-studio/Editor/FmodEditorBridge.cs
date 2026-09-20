using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio.Editor
{
    /// <summary>Adapts an independently installed FMOD integration exclusively inside the editor.</summary>
    internal static class FmodEditorBridge
    {
        #region Fields
        private static readonly Type settingsType = FindType("FMODUnity.Settings");
        private static readonly Type managerType = FindType("FMODUnity.EventManager");
        private static readonly Type utilitiesType = FindType("FMODUnity.EditorUtils");
        private static object preview;
        #endregion

        #region Properties
        internal static bool Available => settingsType != null && managerType != null && utilitiesType != null;
        #endregion

        #region Methods
        #region Connection
        /// <summary>Writes a validated connection into the official integration's settings asset.</summary>
        /// <param name="connection">Project-owned source configuration.</param>
        /// <returns>An operation result suitable for the editor footer.</returns>
        internal static string Connect(AudioConnection connection)
        {
            // Reflection is editor-only; the package has no runtime backend or SDK source copy.
            if (!Available)
                return "Install the official FMOD for Unity integration to connect banks and preview audio.";
            if (connection.UseStudioProject && !File.Exists(ResolvePath(connection.ProjectPath))
                || !connection.UseStudioProject && !Directory.Exists(ResolvePath(connection.BankPath)))
                return "Select an existing Studio project or bank directory first.";
            try
            {
                UnityEngine.Object settings = Read(settingsType, null, "Instance") as UnityEngine.Object;
                if (settings == null)
                    return "FMOD settings could not be opened.";
                Undo.RecordObject(settings, "Connect Audio Studio to FMOD");
                Write(settings, "HasSourceProject", connection.UseStudioProject);
                Write(settings, "SourceProjectPath", ResolvePath(connection.ProjectPath));
                Write(settings, "SourceBankPath", ResolvePath(connection.BankPath));
                Write(settings, "HasPlatforms", connection.BanksHavePlatforms);
                Write(settings, "AutomaticEventLoading", connection.AutomaticBankLoading);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
                Invoke(managerType, "RefreshBanks");
                return "FMOD connection saved; bank refresh requested.";
            }
            catch (Exception exception)
            {
                return "FMOD connection: " + exception.GetBaseException().Message;
            }
        }

        /// <summary>Normalizes a source path relative to the current Unity project.</summary>
        /// <param name="path">Absolute or project-relative path.</param>
        /// <returns>Absolute normalized path, or an empty string.</returns>
        internal static string ResolvePath(string path)
        {
            // Empty optional fields stay empty instead of resolving to the project directory.
            return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
        }

        /// <summary>Requests a bank refresh without changing any connection settings.</summary>
        /// <returns>Refresh result or an actionable SDK warning.</returns>
        internal static string Refresh()
        {
            // FMOD owns bank importing and platform-specific native loading.
            try
            {
                if (!Available)
                    return "FMOD integration is not installed; Studio metadata can still be browsed.";
                Invoke(managerType, "RefreshBanks");
                return "Bank refresh requested.";
            }
            catch (Exception exception)
            {
                return exception.GetBaseException().Message;
            }
        }
        #endregion

        #region Catalog
        /// <summary>Reads event descriptors from the official integration's current bank cache.</summary>
        /// <param name="catalog">Destination catalog, replaced only after a successful read.</param>
        /// <returns>Whether an SDK cache was available.</returns>
        internal static bool ReadCatalog(List<FmodCatalogEntry> catalog)
        {
            // Cache reflection results into package-owned records rather than reflecting during GUI drawing.
            if (!Available || !(Read(managerType, null, "Events") is IEnumerable events))
                return false;
            List<FmodCatalogEntry> result = new List<FmodCatalogEntry>();
            foreach (object item in events)
            {
                string path = Read(item.GetType(), item, "Path") as string;
                if (string.IsNullOrEmpty(path) || !path.StartsWith("event:/", StringComparison.Ordinal))
                    continue;
                FmodCatalogEntry entry = new FmodCatalogEntry
                {
                    Path = path,
                    Guid = Read(item.GetType(), item, "Guid")?.ToString(),
                    Spatial = Read(item.GetType(), item, "Is3D") is bool spatial && spatial,
                    OneShot = Read(item.GetType(), item, "IsOneShot") is bool oneShot && oneShot,
                    Source = item
                };
                if (Read(item.GetType(), item, "Banks") is IEnumerable banks)
                    foreach (object bank in banks)
                        entry.Banks.Add(Read(bank.GetType(), bank, "Name") as string ?? string.Empty);
                if (Read(item.GetType(), item, "Parameters") is IEnumerable parameters)
                    foreach (object parameter in parameters)
                        entry.Parameters.Add(new FmodCatalogParameter
                        {
                            Name = Read(parameter.GetType(), parameter, "Name") as string,
                            Minimum = Convert.ToSingle(Read(parameter.GetType(), parameter, "Min")),
                            Maximum = Convert.ToSingle(Read(parameter.GetType(), parameter, "Max")),
                            Default = Convert.ToSingle(Read(parameter.GetType(), parameter, "Default")),
                            Global = Read(parameter.GetType(), parameter, "IsGlobal") is bool global && global
                        });
                result.Add(entry);
            }
            catalog.Clear();
            catalog.AddRange(result);
            return true;
        }
        #endregion

        #region Preview
        /// <summary>Starts a single editor audition through FMOD's own preview service.</summary>
        /// <param name="entry">Built-bank event descriptor.</param>
        /// <param name="parameters">Preview parameter overrides.</param>
        /// <param name="volume">Audition volume multiplier.</param>
        /// <returns>Preview result or native integration error.</returns>
        internal static string Play(FmodCatalogEntry entry, Dictionary<string, float> parameters, float volume)
        {
            // Only release the audition owned by this workspace, never other FMOD browser previews.
            Stop();
            if (!Available || entry.Source == null)
                return "Preview needs the official FMOD integration and built banks for this event.";
            try
            {
                Invoke(utilitiesType, "LoadPreviewBanks");
                preview = Invoke(utilitiesType, "PreviewEvent", entry.Source, parameters, volume, 0f);
                return "Previewing " + entry.Path;
            }
            catch (Exception exception)
            {
                return "Preview: " + exception.GetBaseException().Message;
            }
        }

        /// <summary>Stops and releases only the current Audio Studio audition.</summary>
        internal static void Stop()
        {
            // Cleanup also runs when closing the window or reloading assemblies.
            if (preview == null || utilitiesType == null)
                return;
            try
            {
                MethodInfo method = utilitiesType.GetMethod("PreviewStop", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                    method.Invoke(null, new[] { preview, Enum.ToObject(method.GetParameters()[1].ParameterType, 1) });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Audio Studio preview cleanup: " + exception.GetBaseException().Message);
            }
            preview = null;
        }
        #endregion

        #region Editor Reflection
        /// <summary>Locates an optional integration type once during editor domain initialization.</summary>
        /// <param name="name">Fully qualified integration type name.</param>
        /// <returns>Loaded type or null when the SDK is absent.</returns>
        private static Type FindType(string name)
        {
            // Optional editor references keep the authoring package importable before installing FMOD.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        /// <summary>Reads a public integration field or property.</summary>
        /// <param name="type">Declaring integration type.</param>
        /// <param name="target">Instance, or null for static data.</param>
        /// <param name="name">Public member name.</param>
        /// <returns>Member value or null.</returns>
        private static object Read(Type type, object target, string name)
        {
            // Do not inspect private SDK internals or persist reflected values in runtime assets.
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            return type.GetProperty(name, flags)?.GetValue(target) ?? type.GetField(name, flags)?.GetValue(target);
        }

        /// <summary>Writes one supported public integration setting.</summary>
        /// <param name="target">FMOD settings asset.</param>
        /// <param name="name">Public field or property.</param>
        /// <param name="value">Requested setting.</param>
        private static void Write(object target, string name, object value)
        {
            // Unsupported SDK versions produce a visible error instead of silent partial writes.
            PropertyInfo property = target.GetType().GetProperty(name);
            if (property != null)
                property.SetValue(target, value);
            else if (target.GetType().GetField(name) is FieldInfo field)
                field.SetValue(target, value);
            else
                throw new MissingMemberException(target.GetType().FullName, name);
        }

        /// <summary>Calls a public static editor API with a matching argument count.</summary>
        /// <param name="type">Integration type providing the operation.</param>
        /// <param name="name">Public method name.</param>
        /// <param name="arguments">Operation arguments.</param>
        /// <returns>Result returned by the integration.</returns>
        private static object Invoke(Type type, string name, params object[] arguments)
        {
            // Restrict invocation to explicit public editor operations used by this adapter.
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (method.Name == name && method.GetParameters().Length == arguments.Length)
                    return method.Invoke(null, arguments);
            throw new MissingMethodException(type.FullName, name);
        }
        #endregion
        #endregion
    }
}
