using System;
using System.Collections.Generic;
using System.IO;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio.Editor
{
    /// <summary>Authors FMOD connections, event maps and music through a compact preset workspace.</summary>
    public sealed class AudioStudioWindow : StudioWindow
    {
        #region Fields
        private static readonly string[] sections = { "Setup", "Catalog", "Events", "Playback", "Routing", "Music", "Limits", "Validation" };
        private readonly List<FmodCatalogEntry> catalog = new List<FmodCatalogEntry>();
        private readonly List<FmodCatalogEntry> filtered = new List<FmodCatalogEntry>();
        private readonly Dictionary<string, float> previewParameters = new Dictionary<string, float>();
        private readonly List<string> diagnostics = new List<string>();
        private FmodCatalogEntry selected;
        private string search = string.Empty;
        private int eventIndex;
        private int musicIndex;
        private float previewVolume = 0.7f;
        private Vector2 catalogScroll;
        #endregion

        #region Properties
        protected override Type PresetType => typeof(AudioPreset);
        protected override string[] Tabs => sections;
        private AudioPreset Preset => (AudioPreset)Draft;
        #endregion

        #region Methods
        #region Lifecycle
        /// <summary>Opens the standalone Audio Studio workspace.</summary>
        //[MenuItem("Tools/Audio Studio")]
        public static void Open()
        {
            // The source preset is selected explicitly; no old gameplay events are imported.
            GetWindow<AudioStudioWindow>("Audio Studio");
        }

        /// <summary>Releases editor preview audio before script reload or closure.</summary>
        protected override void OnDisable()
        {
            // Native FMOD instances must not survive this workspace's audition session.
            FmodEditorBridge.Stop();
            base.OnDisable();
        }
        #endregion

        #region Tabs
        /// <summary>Draws only the selected audio domain.</summary>
        /// <param name="data">Serialized audio preset proposal.</param>
        protected override void DrawTab(SerializedObject data)
        {
            // The shared workspace owns Apply, Discard and source conflict detection.
            switch (Tab)
            {
                case 0: DrawSetup(data); break;
                case 1: DrawCatalog(data); break;
                case 2: DrawEvents(data, false); break;
                case 3: DrawPlayback(data); break;
                case 4: StudioFields.Draw(data, "Routing"); break;
                case 5: DrawMusic(data); break;
                case 6: DrawEvents(data, true); break;
                case 7:
                    diagnostics.Clear();
                    Validate(diagnostics);
                    EditorGUILayout.LabelField(diagnostics.Count == 0 ? "No configuration warnings." : string.Join("\n", diagnostics), EditorStyles.wordWrappedLabel);
                    break;
            }
        }

        /// <summary>Draws source selection with project and bank controls shown only when relevant.</summary>
        /// <param name="data">Serialized audio proposal.</param>
        private void DrawSetup(SerializedObject data)
        {
            // Connection paths remain project-owned rather than being written into the package.
            StudioFields.Draw(data, "Description", "Version");
            SerializedProperty connection = data.FindProperty("Connection");
            StudioFields.Draw(connection, "UseStudioProject");
            bool project = connection.FindPropertyRelative("UseStudioProject").boolValue;
            StudioFields.Draw(connection, project ? "ProjectPath" : "BankPath");
            if (StudioFields.Button(project ? "Browse Studio project" : "Browse banks", "Choose the FMOD source on disk."))
            {
                string path = project ? EditorUtility.OpenFilePanel("FMOD Studio project", string.Empty, "fspro")
                    : EditorUtility.OpenFolderPanel("FMOD banks", string.Empty, string.Empty);
                if (!string.IsNullOrEmpty(path))
                    connection.FindPropertyRelative(project ? "ProjectPath" : "BankPath").stringValue = path.Replace('\\', '/');
            }
            if (!project)
                StudioFields.Draw(connection, "BanksHavePlatforms");
            StudioFields.Draw(connection, "AutomaticBankLoading");
            EditorGUILayout.LabelField(FmodEditorBridge.Available ? "FMOD integration detected" : "Metadata browsing available; FMOD integration not installed", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (StudioFields.Button("Connect FMOD", "Save the applied preset connection to the official FMOD integration."))
                {
                    data.ApplyModifiedProperties();
                    SetStatus(Pending ? "Apply the preset before connecting FMOD." : FmodEditorBridge.Connect(Preset.Connection));
                }
                if (StudioFields.Button("Refresh banks", "Ask the official integration to refresh its event cache."))
                    SetStatus(FmodEditorBridge.Refresh());
            }
            if (!FmodEditorBridge.Available && StudioFields.Button("Official FMOD integration", "Open FMOD's official Unity integration guide."))
                Application.OpenURL("https://www.fmod.com/docs/2.03/unity/user-guide.html");
        }

        /// <summary>Shows global playback controls without irrelevant disabled details.</summary>
        /// <param name="data">Serialized audio proposal.</param>
        private void DrawPlayback(SerializedObject data)
        {
            // Disabled playback keeps stored values but hides tuning that currently has no effect.
            SerializedProperty playback = data.FindProperty("Playback");
            StudioFields.Draw(playback, "Enabled");
            if (playback.FindPropertyRelative("Enabled").boolValue)
                StudioFields.Draw(playback, "MasterVolume", "MinimumDistance", "MaximumDistance", "LogMissingEvents");
        }
        #endregion

        #region Catalog
        /// <summary>Browses cached event paths, banks, GUIDs and preview parameters.</summary>
        /// <param name="data">Audio proposal receiving explicit assignments.</param>
        private void DrawCatalog(SerializedObject data)
        {
            // Search filters cached records only; metadata IO is an explicit refresh action.
            if (StudioFields.Button("Load catalog", "Read the selected Studio project or current built-bank cache."))
            {
                data.ApplyModifiedProperties();
                selected = null;
                previewParameters.Clear();
                FmodEditorBridge.Stop();
                SetStatus(FmodCatalog.Load(Preset.Connection, catalog));
                FilterCatalog();
            }
            string requested = EditorGUILayout.TextField(new GUIContent("Search", "Filter event paths and bank names."), search);
            if (requested != search)
            {
                search = requested;
                FilterCatalog();
            }
            catalogScroll = EditorGUILayout.BeginScrollView(catalogScroll, GUILayout.Height(160f));
            foreach (FmodCatalogEntry entry in filtered)
                if (GUILayout.Toggle(selected == entry, new GUIContent(entry.Path, string.Join(", ", entry.Banks)), EditorStyles.miniButton))
                    if (selected != entry)
                    {
                        selected = entry;
                        previewParameters.Clear();
                        foreach (FmodCatalogParameter parameter in entry.Parameters)
                            previewParameters[parameter.Name] = parameter.Default;
                    }
            EditorGUILayout.EndScrollView();
            if (selected == null)
                return;
            EditorGUILayout.SelectableLabel(selected.Path, GUILayout.Height(18f));
            EditorGUILayout.LabelField("Banks", string.Join(", ", selected.Banks));
            EditorGUILayout.SelectableLabel(selected.Guid, GUILayout.Height(18f));
            if (selected.Source != null)
                EditorGUILayout.LabelField(selected.Spatial ? "3D" : "2D", selected.OneShot ? "One-shot" : "Sustained");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (StudioFields.Button("Copy path", "Copy the selected FMOD event path."))
                    EditorGUIUtility.systemCopyBuffer = selected.Path;
                if (StudioFields.Button("New event", "Add a project-owned binding; no gameplay hook is created."))
                {
                    data.ApplyModifiedProperties();
                    Undo.RecordObject(Draft, "Add audio binding");
                    Preset.Events.Add(new AudioBinding { Key = UniqueKey(selected.Path), DisplayName = Path.GetFileName(selected.Path), EventPath = selected.Path, EventGuid = selected.Guid, Spatialize = selected.Spatial });
                    data.Update();
                    RefreshActions();
                }
            }
            DrawAssignments(data);
            foreach (FmodCatalogParameter parameter in selected.Parameters)
                previewParameters[parameter.Name] = EditorGUILayout.Slider(new GUIContent(parameter.Name, parameter.Global ? "Global preview parameter." : "Event preview parameter."), previewParameters[parameter.Name], parameter.Minimum, parameter.Maximum);
            previewVolume = EditorGUILayout.Slider(new GUIContent("Preview volume", "Volume of editor auditions only."), previewVolume, 0f, 1f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (StudioFields.Button("Play preview", "Audition the built event with these parameter values."))
                    SetStatus(FmodEditorBridge.Play(selected, previewParameters, previewVolume));
                if (StudioFields.Button("Stop", "Stop only Audio Studio's current audition."))
                    FmodEditorBridge.Stop();
            }
        }

        /// <summary>Assigns the selected catalog event to existing bindings or music contexts.</summary>
        /// <param name="data">Audio proposal receiving paths and identities.</param>
        private void DrawAssignments(SerializedObject data)
        {
            // Assignments preserve volume, rate limits and other authored tuning.
            if (Preset.Events.Count > 0)
            {
                eventIndex = EditorGUILayout.Popup(new GUIContent("Event target", "Existing event binding to update."), Mathf.Min(eventIndex, Preset.Events.Count - 1), Preset.Events.ConvertAll(binding => binding.Key).ToArray());
                if (StudioFields.Button("Assign event", "Replace only this binding's FMOD path and GUID."))
                {
                    SerializedProperty binding = data.FindProperty("Events").GetArrayElementAtIndex(eventIndex);
                    binding.FindPropertyRelative("EventPath").stringValue = selected.Path;
                    binding.FindPropertyRelative("EventGuid").stringValue = selected.Guid;
                }
            }
            if (Preset.Music.Count > 0)
            {
                musicIndex = EditorGUILayout.Popup(new GUIContent("Music target", "Existing music context to update."), Mathf.Min(musicIndex, Preset.Music.Count - 1), Preset.Music.ConvertAll(music => music.Context).ToArray());
                if (StudioFields.Button("Assign music", "Set this music context's event and first assigned bank."))
                {
                    SerializedProperty music = data.FindProperty("Music").GetArrayElementAtIndex(musicIndex);
                    music.FindPropertyRelative("EventPath").stringValue = selected.Path;
                    music.FindPropertyRelative("EventGuid").stringValue = selected.Guid;
                    music.FindPropertyRelative("Bank").stringValue = selected.Banks.Count > 0 ? selected.Banks[0] : string.Empty;
                }
            }
        }

        /// <summary>Filters the cached catalog after a search or reload.</summary>
        private void FilterCatalog()
        {
            // Reuse list storage instead of parsing metadata for every GUI event.
            filtered.Clear();
            foreach (FmodCatalogEntry entry in catalog)
                if (entry.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || entry.Banks.Exists(bank => bank.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                    filtered.Add(entry);
        }

        /// <summary>Produces an unused initial binding key from the catalog leaf name.</summary>
        /// <param name="path">Selected Studio event path.</param>
        /// <returns>A unique key editable by the project.</returns>
        private string UniqueKey(string path)
        {
            // Repeated imports must not create invalid duplicate identities.
            string basis = Path.GetFileName(path);
            string key = basis;
            int suffix = 2;
            while (Preset.Events.Exists(binding => binding.Key == key))
                key = basis + suffix++;
            return key;
        }
        #endregion

        #region Event Editing
        /// <summary>Draws focused event records or just their rate caps.</summary>
        /// <param name="data">Audio proposal.</param>
        /// <param name="limitsOnly">Show only rate limits.</param>
        private void DrawEvents(SerializedObject data, bool limitsOnly)
        {
            // Each record folds independently so large maps stay navigable in narrow docks.
            SerializedProperty events = data.FindProperty("Events");
            if (!limitsOnly && StudioFields.Button("Add event", "Add an empty project-specific event binding."))
            {
                data.ApplyModifiedProperties();
                Undo.RecordObject(Draft, "Add audio event");
                Preset.Events.Add(new AudioBinding { Key = UniqueKey("event:/NewEvent") });
                data.Update();
                RefreshActions();
            }
            for (int index = 0; index < events.arraySize; index++)
            {
                SerializedProperty binding = events.GetArrayElementAtIndex(index);
                binding.isExpanded = EditorGUILayout.Foldout(binding.isExpanded, binding.FindPropertyRelative("Key").stringValue, true);
                if (!binding.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope entryIndent = new EditorGUI.IndentLevelScope();
                if (!limitsOnly)
                {
                    StudioFields.Draw(binding, "Key", "DisplayName", "Description", "EventPath", "EventGuid", "Volume", "Pitch", "Spatialize");
                    if (binding.FindPropertyRelative("Spatialize").boolValue)
                    {
                        StudioFields.Draw(binding, "OverrideDistances");
                        if (binding.FindPropertyRelative("OverrideDistances").boolValue)
                            StudioFields.Draw(binding, "MinimumDistance", "MaximumDistance");
                    }
                    StudioFields.Draw(binding, "SingleInstance", "Parameters");
                }
                StudioFields.Draw(binding, "LimitRate");
                if (binding.FindPropertyRelative("LimitRate").boolValue)
                    StudioFields.Draw(binding, "MaxPlays", "WindowSeconds");
                if (!limitsOnly && StudioFields.Button("Remove event", "Remove this binding from the pending preset proposal."))
                {
                    events.DeleteArrayElementAtIndex(index);
                    break;
                }
            }
        }

        /// <summary>Edits any number of named music contexts and their transition controls.</summary>
        /// <param name="data">Audio proposal.</param>
        private void DrawMusic(SerializedObject data)
        {
            // Menu, gameplay and boss are optional project contexts rather than baked-in states.
            StudioFields.Draw(data, "MusicCrossfadeSeconds");
            if (StudioFields.Button("Add music context", "Create a new music context without an automatic gameplay trigger."))
            {
                data.ApplyModifiedProperties();
                Undo.RecordObject(Draft, "Add music context");
                Preset.Music.Add(new AudioMusic());
                data.Update();
                RefreshActions();
            }
            SerializedProperty music = data.FindProperty("Music");
            for (int index = 0; index < music.arraySize; index++)
            {
                SerializedProperty item = music.GetArrayElementAtIndex(index);
                item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, item.FindPropertyRelative("Context").stringValue, true);
                if (!item.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope entryIndent = new EditorGUI.IndentLevelScope();
                StudioFields.Draw(item, "Context", "Enabled");
                if (item.FindPropertyRelative("Enabled").boolValue)
                    StudioFields.Draw(item, "EventPath", "EventGuid", "Bank", "Volume", "AutoStart", "RestartWhenPathChanges", "StopWhenDisabled");
                if (StudioFields.Button("Remove music", "Remove this context from the pending proposal."))
                {
                    music.DeleteArrayElementAtIndex(index);
                    break;
                }
            }
        }

        /// <summary>Collects invalid audio settings without replacing authored values.</summary>
        /// <param name="messages">Output diagnostics.</param>
        protected override void Validate(List<string> messages)
        {
            // Validation belongs to the data domain and can also be reused by project adapters.
            AudioValidation.Collect(Preset, messages);
        }
        #endregion
        #endregion
    }
}
