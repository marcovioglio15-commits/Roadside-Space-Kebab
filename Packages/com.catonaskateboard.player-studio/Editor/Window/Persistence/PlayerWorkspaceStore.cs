using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Stores workspace data with persistent object identities instead of session-specific instance IDs.</summary>
    internal static class PlayerWorkspaceStore
    {
        #region Storage

        internal const string FilePath = "UserSettings/PlayerStudio/workspace.json";
        private const string processKey = "PlayerStudio.Workspace.Process";
        private static readonly Regex references = new Regex(@"((?:\\*"")(?:entityId|instanceID|prefabId)(?:\\*""):[ ]*)([0-9]+)", RegexOptions.Compiled);

        #endregion

        #region Data

        /// <summary>Retains one referenced asset or scene object across Editor restarts.</summary>
        [Serializable]
        internal sealed class Reference
        {
            [Tooltip("Instance ID used inside this saved JSON only.")]
            public ulong Instance;
            [Tooltip("Stable Unity asset or saved scene identity.")]
            public string Global;
        }

        /// <summary>Stores the proposal independently of native SceneView internals and Undo history.</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            [Tooltip("Storage schema version; unsupported versions are not overwritten.")]
            public int Version = 1;
            [Tooltip("Editor process identity used only to recover unsaved objects during the same session.")]
            public string Process;
            [Tooltip("Serialized proposals including outside-edit baselines.")]
            public string State;
            [Tooltip("Persistent identity for every referenced Unity object.")]
            public List<Reference> References = new List<Reference>();
            [Tooltip("Window position used when reopened as a floating window.")]
            public Rect Position;
            [Tooltip("Native preview orbit center.")]
            public Vector3 Pivot;
            [Tooltip("Native preview orientation.")]
            public Quaternion Rotation;
            [Tooltip("Native preview zoom.")]
            public float Size;
            [Tooltip("Native orthographic preview mode.")]
            public bool Orthographic;
            [Tooltip("Width or height of the controls pane.")]
            public float Split = 320f;
            [Tooltip("Preset controls scroll offset.")]
            public Vector2 ControlsScroll;
            [Tooltip("Preview transform controls scroll offset.")]
            public Vector2 TransformScroll;
        }

        #endregion

        #region Methods

        #region Capture

        /// <summary>Captures the complete proposal and camera without saving an asset or scene.</summary>
        /// <param name="state">Retained session and layout choices.</param>
        /// <param name="window">Native preview providing its view pose.</param>
        /// <returns>A self-contained workspace snapshot.</returns>
        internal static Snapshot Capture(PlayerStudioState state, SceneView window)
        {
            // Only explicit save boundaries serialize the workspace; rendering performs no disk writes.
            Snapshot snapshot = new Snapshot
            {
                State = JsonUtility.ToJson(state),
                Process = Process(),
                Position = window.position,
                Pivot = window.pivot,
                Rotation = window.rotation,
                Size = window.size,
                Orthographic = window.orthographic
            };
            HashSet<ulong> found = new HashSet<ulong>();
            foreach (Match match in references.Matches(snapshot.State))
            {
                ulong instance = ulong.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                if (instance == 0 || !found.Add(instance))
                    continue;
                UnityEngine.Object target = EditorUtility.EntityIdToObject(EntityId.FromULong(instance));
                snapshot.References.Add(new Reference
                {
                    Instance = instance,
                    Global = target != null ? GlobalObjectId.GetGlobalObjectIdSlow(target).ToString() : string.Empty
                });
            }
            return snapshot;
        }

        /// <summary>Writes a replacement atomically while retaining the previous snapshot on failure.</summary>
        /// <param name="snapshot">Already captured proposal and layout.</param>
        internal static void Save(Snapshot snapshot)
        {
            // The project-local UserSettings folder is separate from reusable package content.
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(snapshot, true));
            if (File.Exists(FilePath))
                File.Replace(temporary, FilePath, null);
            else
                File.Move(temporary, FilePath);
        }

        #endregion

        #region Recovery

        /// <summary>Reads an existing snapshot without altering it if parsing or schema validation fails.</summary>
        /// <returns>The stored snapshot, or null before the first saved workspace.</returns>
        internal static Snapshot Load()
        {
            // Corrupt or newer files must remain available for recovery rather than being silently replaced.
            if (!File.Exists(FilePath))
                return null;
            Snapshot snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(FilePath));
            if (snapshot == null || snapshot.Version != 1 || string.IsNullOrEmpty(snapshot.State))
                throw new IOException("The saved Player Studio workspace is invalid or uses an unsupported version.");
            return snapshot;
        }

        /// <summary>Resolves stored identities, preserving drafts while reporting unavailable targets.</summary>
        /// <param name="snapshot">Disk or pre-Play snapshot to recover.</param>
        /// <param name="warning">Receives missing scene or asset identities.</param>
        /// <returns>Recovered session with unresolved references left unassigned.</returns>
        internal static PlayerStudioState Restore(Snapshot snapshot, out string warning)
        {
            // Scene loading remains explicit; a missing source never redirects a draft by object name.
            Dictionary<ulong, ulong> resolved = new Dictionary<ulong, ulong>();
            int missing = 0;
            foreach (Reference reference in snapshot.References)
            {
                UnityEngine.Object target = null;
                if (GlobalObjectId.TryParse(reference.Global, out GlobalObjectId identity))
                    target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identity);
                if (target == null && snapshot.Process == Process())
                    target = EditorUtility.EntityIdToObject(EntityId.FromULong(reference.Instance));
                if (target == null)
                    missing++;
                resolved[reference.Instance] = target != null ? EntityId.ToULong(target.GetEntityId()) : 0;
            }
            string json = references.Replace(snapshot.State, match =>
            {
                ulong previous = ulong.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                return match.Groups[1].Value + (resolved.TryGetValue(previous, out ulong current) ? current : 0)
                    .ToString(CultureInfo.InvariantCulture);
            });
            warning = missing > 0 ? "Some workspace sources are unavailable. Load their saved scenes and retry recovery, or explicitly discard the saved workspace." : string.Empty;
            return JsonUtility.FromJson<PlayerStudioState>(json);
        }

        /// <summary>Reopens saved source scenes additively without replacing or saving the current scene setup.</summary>
        /// <param name="snapshot">Snapshot whose object identities identify the required scenes.</param>
        internal static void LoadScenes(Snapshot snapshot)
        {
            // Only scene assets referenced by the retained session are eligible for recovery.
            HashSet<string> paths = new HashSet<string>();
            foreach (Reference reference in snapshot.References)
                if (GlobalObjectId.TryParse(reference.Global, out GlobalObjectId identity))
                {
                    string path = AssetDatabase.GUIDToAssetPath(identity.assetGUID.ToString());
                    if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && paths.Add(path)
                        && !SceneManager.GetSceneByPath(path).isLoaded)
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
        }

        /// <summary>Returns an Editor-session token that survives reload but changes after a process restart.</summary>
        /// <returns>Opaque token preventing reuse of another process's instance IDs.</returns>
        private static string Process()
        {
            // Unsaved scene objects can only be reconnected inside their original Editor process.
            string value = SessionState.GetString(processKey, string.Empty);
            if (value.Length > 0)
                return value;
            value = Guid.NewGuid().ToString();
            SessionState.SetString(processKey, value);
            return value;
        }

        #endregion

        #endregion
    }
}
