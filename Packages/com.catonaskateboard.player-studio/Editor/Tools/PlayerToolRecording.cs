using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Records local transform keys through the existing Player Studio viewport without moving scene objects.</summary>
    [Serializable]
    internal sealed class PlayerToolRecording
    {
        #region Fields

        [Header("Animation Recording")]
        [Tooltip("Show animation pose handles and the sampled model in the existing scene preview.")]
        [SerializeField]
        private bool enabled;
        [Tooltip("Animation receiving explicitly recorded keys. Record Key writes this asset with Undo.")]
        [SerializeField]
        private PlayerToolAnimationPreset animation;
        [Tooltip("Selected child path relative to the visual model.")]
        [SerializeField]
        private string path = string.Empty;
        [Tooltip("Time in seconds at which the next key is recorded.")]
        [SerializeField]
        private float time;
        [Tooltip("Local transform proposed through numeric fields or scene handles.")]
        [SerializeField]
        private PlayerToolPose pose = PlayerToolPose.Identity;
        [Tooltip("Active preview handle: move, rotate or scale.")]
        [SerializeField]
        private int handle;

        #endregion

        #region Cache

        [NonSerialized] private GameObject model;
        [NonSerialized] private Transform[] children;
        [NonSerialized] private GUIContent[] labels;
        [NonSerialized] private string[] paths;
        [NonSerialized] private readonly Dictionary<Transform, PlayerToolPose> sampled = new Dictionary<Transform, PlayerToolPose>();
        private static readonly GUIContent[] handles =
        {
            new GUIContent("Move", "Record a local position."),
            new GUIContent("Rotate", "Record local rotation; numeric angles also allow full turns."),
            new GUIContent("Scale", "Record local scale.")
        };

        #endregion

        #region Properties

        /// <summary>Whether this recorder owns the embedded viewport handles.</summary>
        internal bool Enabled => enabled;

        #endregion

        #region Methods

        #region Controls

        /// <summary>Shows child selection, time, pose and explicit key recording controls.</summary>
        /// <param name="state">Current visual and player proposals.</param>
        /// <param name="owner">Window used for pose Undo.</param>
        internal void Draw(PlayerStudioState state, UnityEngine.Object owner)
        {
            // Preview changes are temporary; only Record Key writes the animation asset.
            enabled = EditorGUILayout.Foldout(enabled, new GUIContent("Record Switch Animation", "Pose visual children in the scene preview and record reusable transform keys."), true);
            if (!enabled)
                return;
            Refresh(PlayerVisualPreview.ResolveModel(state.VisualScene, state.Visual.Draft));
            if (model == null)
            {
                EditorGUILayout.LabelField("Assign a Visual model to record its children.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            PlayerToolAnimationPreset selected = (PlayerToolAnimationPreset)EditorGUILayout.ObjectField(
                new GUIContent("Animation", "Switch-in or switch-out preset receiving recorded keys."), animation, typeof(PlayerToolAnimationPreset), false);
            if (selected != animation)
            {
                animation = selected;
                ReadPose();
            }
            if (animation == null && GUILayout.Button(new GUIContent("Create Animation Preset", "Save an empty preset for recording switch animation keys.")))
                animation = PlayerToolsAuthoring.Create<PlayerToolAnimationPreset>("Tool Animation");
            int index = Array.IndexOf(paths, path);
            int next = EditorGUILayout.Popup(new GUIContent("Visual Child", "Existing child whose local transform is edited in the preview."), index, labels);
            if (next >= 0 && next != index)
            {
                Undo.RecordObject(owner, "Select Tool Animation Child");
                path = paths[next];
                ReadPose();
            }
            float nextTime = EditorGUILayout.FloatField(new GUIContent("Time", "Seconds from animation start. Scrubbing samples every recorded track."), time);
            if (nextTime != time)
            {
                Undo.RecordObject(owner, "Scrub Tool Animation");
                time = nextTime;
                ReadPose();
            }
            handle = GUILayout.Toolbar(handle, handles);
            EditorGUI.BeginChangeCheck();
            PlayerToolPose edited = pose;
            edited.Position = EditorGUILayout.Vector3Field(new GUIContent("Position", "Local position relative to this child's parent."), pose.Position);
            edited.Rotation = EditorGUILayout.Vector3Field(new GUIContent("Rotation", "Local degrees; values such as 360 or 720 preserve full rotations."), pose.Rotation);
            edited.Scale = EditorGUILayout.Vector3Field(new GUIContent("Scale", "Positive scale along each local axis."), pose.Scale);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(owner, "Pose Tool Animation Child");
                pose = edited;
            }
            using (new EditorGUI.DisabledScope(animation == null || Array.IndexOf(paths, path) < 0 || !pose.IsValid() || !float.IsFinite(time) || time < 0f))
                if (GUILayout.Button(new GUIContent("Record Key", "Save or replace this child's key at the selected time, with Undo. Animation duration grows to include the key.")))
                    Record();
            if (GUILayout.Button(new GUIContent("Reload Pose", "Discard the preview pose and sample the recorded animation at this time.")))
                ReadPose();
        }

        #endregion

        #region Model Cache

        /// <summary>Requests a new child catalog after an editor hierarchy or project change.</summary>
        internal void Invalidate()
        {
            // Rebuild on the next preview event rather than in the hierarchy callback.
            children = null;
        }

        /// <summary>Rebuilds child labels only when the visual source changes.</summary>
        /// <param name="source">Visual model currently shown by the workspace.</param>
        private void Refresh(GameObject source)
        {
            // No preview GameObjects or user scripts are instantiated.
            if (source == model && children != null)
                return;
            model = source;
            sampled.Clear();
            children = model != null ? model.GetComponentsInChildren<Transform>(true) : Array.Empty<Transform>();
            labels = new GUIContent[children.Length];
            paths = new string[children.Length];
            for (int index = 0; index < children.Length; index++)
            {
                paths[index] = AnimationUtility.CalculateTransformPath(children[index], model.transform);
                labels[index] = new GUIContent(paths[index].Length > 0 ? paths[index] : "Model Root", "Record this existing transform.");
            }
            ReadPose();
        }

        /// <summary>Samples the selected child without overwriting its live local values.</summary>
        private void ReadPose()
        {
            // Unknown paths remain visible and never resolve to another child implicitly.
            int index = paths != null ? Array.IndexOf(paths, path) : -1;
            if (index < 0)
                return;
            pose = PlayerToolPose.Read(children[index]);
            if (animation != null && animation.TryValidate(out _))
                foreach (PlayerToolTrack track in animation.Tracks)
                    if (track.Path == path)
                    {
                        pose = track.Sample(time, pose);
                        break;
                    }
        }

        /// <summary>Builds pose overrides for a viewport draw or handle event.</summary>
        /// <param name="source">Visual model shared by rendering and handle placement.</param>
        internal void Prepare(GameObject source)
        {
            // Reuse one dictionary; no hierarchy or component query occurs on an unchanged source.
            Refresh(source);
            sampled.Clear();
            if (!enabled || model == null)
                return;
            if (animation != null && animation.Tracks != null && float.IsFinite(time) && time >= 0f)
                foreach (PlayerToolTrack track in animation.Tracks)
                {
                    int index = track != null ? Array.IndexOf(paths, track.Path) : -1;
                    if (index >= 0 && track.Keys != null)
                        sampled[children[index]] = track.Sample(time, PlayerToolPose.Read(children[index]));
                }
            int selected = Array.IndexOf(paths, path);
            if (selected >= 0 && pose.IsValid())
                sampled[children[selected]] = pose;
        }

        /// <summary>Composes preview poses along the hierarchy while retaining the model's authored root transform.</summary>
        /// <param name="target">Renderer transform or handle parent.</param>
        /// <returns>Transform relative to the unchanged model root used by the visual preview.</returns>
        internal Matrix4x4 RelativeMatrix(Transform target)
        {
            // The existing preview already includes the model root's original pose.
            if (target == model.transform.parent)
                return Matrix4x4.TRS(model.transform.localPosition, model.transform.localRotation, model.transform.localScale).inverse;
            PlayerToolPose current = sampled.TryGetValue(target, out PlayerToolPose value) ? value : PlayerToolPose.Read(target);
            return RelativeMatrix(target.parent) * Matrix4x4.TRS(current.Position, Quaternion.Euler(current.Rotation), current.Scale);
        }

        #endregion

        #region Viewport

        /// <summary>Draws native handles at the sampled child pose in the existing preview.</summary>
        /// <param name="state">Player and visual offset drafts supplying the preview matrix.</param>
        /// <param name="owner">Window recorded before a handle edit.</param>
        /// <returns>True when a handle changed the local preview pose.</returns>
        internal bool DrawHandles(PlayerStudioState state, UnityEngine.Object owner)
        {
            // Navigation retains native priority; runtime views never edit preset data.
            if (!enabled || UnityEditor.Tools.viewToolActive || EditorApplication.isPlayingOrWillChangePlaymode
                || !state.Visual.Draft.TryGetSettings(out PlayerVisualSettings visual, out _) || !state.Transform.TryValidateNumbers(out _))
                return false;
            Prepare(PlayerVisualPreview.ResolveModel(state.VisualScene, state.Visual.Draft));
            int index = Array.IndexOf(paths, path);
            if (model == null || index < 0)
                return false;
            Matrix4x4 root = state.Transform.WorldMatrix * Matrix4x4.TRS(visual.Position, visual.Rotation, Vector3.one * visual.Scale)
                * PlayerVisualPose.Authored(state.VisualScene, state.Visual.Draft);
            Matrix4x4 parent = root * RelativeMatrix(children[index].parent);
            PlayerToolPose edited = pose;
            EditorGUI.BeginChangeCheck();
            using (new Handles.DrawingScope(new Color(0.2f, 0.85f, 1f), parent))
            {
                switch (handle)
                {
                    case 0:
                        edited.Position = Handles.PositionHandle(pose.Position, Quaternion.Euler(pose.Rotation));
                        break;
                    case 1:
                        edited.Rotation = Handles.RotationHandle(Quaternion.Euler(pose.Rotation), pose.Position).eulerAngles;
                        break;
                    case 2:
                        edited.Scale = Handles.ScaleHandle(pose.Scale, pose.Position, Quaternion.Euler(pose.Rotation), HandleUtility.GetHandleSize(pose.Position));
                        break;
                }
                Handles.Label(pose.Position, new GUIContent(path.Length > 0 ? path : "Model Root", "Animation preview target."));
            }
            if (!EditorGUI.EndChangeCheck())
                return false;
            Undo.RecordObject(owner, "Pose Tool Animation Child");
            pose = edited;
            return true;
        }

        #endregion

        #region Recording

        /// <summary>Saves an ordered key to the selected asset while preserving every other track.</summary>
        private void Record()
        {
            // Record is an explicit asset write independent of the Tools module's Apply button.
            if (!EditorUtility.IsPersistent(animation) || !AssetDatabase.IsOpenForEdit(animation))
            {
                Debug.LogWarning("Choose a writable Tool Animation asset before recording.", animation);
                return;
            }
            Undo.RecordObject(animation, "Record Tool Animation Key");
            List<PlayerToolTrack> tracks = new List<PlayerToolTrack>(animation.Tracks ?? Array.Empty<PlayerToolTrack>());
            PlayerToolTrack track = tracks.Find(candidate => candidate != null && candidate.Path == path);
            if (track == null)
            {
                track = new PlayerToolTrack { Path = path };
                tracks.Add(track);
            }
            List<PlayerToolKey> keys = new List<PlayerToolKey>(track.Keys ?? Array.Empty<PlayerToolKey>());
            keys.RemoveAll(key => key == null || Mathf.Abs(key.Time - time) < 0.0001f);
            keys.Add(new PlayerToolKey { Time = time, Pose = pose });
            keys.Sort((left, right) => left.Time.CompareTo(right.Time));
            track.Keys = keys.ToArray();
            animation.Tracks = tracks.ToArray();
            animation.Duration = Mathf.Max(animation.Duration, time > 0f ? time : 0.01f);
            EditorUtility.SetDirty(animation);
            AssetDatabase.SaveAssetIfDirty(animation);
        }

        #endregion

        #endregion
    }
}
