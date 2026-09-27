using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Chooses independent parking poses or one rotating sequence of interchangeable slots.</summary>
    public enum PlayerToolLayout { Independent, Cyclic }

    /// <summary>Chooses straight slot travel or rotation around a shared wheel pivot.</summary>
    public enum PlayerToolSlotMotion { Direct, AroundPivot }

    /// <summary>Connects a tool identity to its optional visual child and private parking pose.</summary>
    [Serializable]
    public sealed class PlayerToolEntry
    {
        #region Fields

        [Header("Tool")]
        [Tooltip("Stable tool asset used by input bindings and interaction requirements.")]
        public PlayerTool Tool;
        [Tooltip("Move a visual child between slots when switching tools. Tools without models can leave this disabled.")]
        public bool MoveVisual;
        [Tooltip("Path relative to the visual model. Empty selects the model root.")]
        public string Path = string.Empty;
        [Tooltip("Parking pose used by this tool in Independent layout.")]
        public PlayerToolPose PassivePose = PlayerToolPose.Identity;

        #endregion
    }

    /// <summary>Defines the player's ordered tools and their interchangeable transform slots.</summary>
    [CreateAssetMenu(fileName = "Player Tools", menuName = "Player Studio/Tools Preset")]
    public sealed class PlayerToolsPreset : ScriptableObject
    {
        #region Fields

        [Header("Tools")]
        [Tooltip("Tools in cycling order. Each identity may appear only once.")]
        public PlayerToolEntry[] Tools = Array.Empty<PlayerToolEntry>();
        [Tooltip("Initially active tool, or empty to start with no active tool.")]
        public PlayerTool InitialTool;
        [Tooltip("Independent returns each tool to its own pose. Cyclic rotates every tool through the shared slot sequence.")]
        public PlayerToolLayout Layout;
        [Tooltip("Active pose in Independent layout, relative to each animated child's parent.")]
        public PlayerToolPose ActivePose = PlayerToolPose.Identity;
        [Tooltip("Cyclic slots in rotation order; slot zero is active. Supply one slot per tool using a common parent.")]
        public PlayerToolPose[] Slots = Array.Empty<PlayerToolPose>();
        [Tooltip("Direct interpolates between slots. Around Pivot follows a circular path for a tool wheel.")]
        public PlayerToolSlotMotion SlotMotion;
        [Tooltip("Wheel centre relative to the common parent of all slotted children.")]
        public Vector3 Pivot;
        [Tooltip("Wheel rotation axis in the common parent's local space. Must be non-zero.")]
        public Vector3 Axis = Vector3.forward;
        [Tooltip("Follow negative angles around the axis. Disable to follow positive angles.")]
        public bool Clockwise = true;
        [Tooltip("Seconds used to interpolate slot changes. Zero applies slots immediately.")]
        public float SwitchDuration = 0.3f;

        #endregion

        #region Methods

        #region Resolution

        /// <summary>Finds a stable identity without depending on the tool's editable name.</summary>
        /// <param name="tool">Tool asset requested by input or gameplay.</param>
        /// <returns>Configured index, or minus one when absent.</returns>
        public int IndexOf(PlayerTool tool)
        {
            // Null always means no active tool.
            if (tool != null && Tools != null)
                for (int index = 0; index < Tools.Length; index++)
                    if (Tools[index]?.Tool == tool)
                        return index;
            return -1;
        }

        /// <summary>Resolves one destination for a selected configuration.</summary>
        /// <param name="index">Visual entry whose destination is needed.</param>
        /// <param name="active">Selected tool index, or minus one for all parked.</param>
        /// <returns>The local destination pose.</returns>
        public PlayerToolPose Destination(int index, int active)
        {
            // No selection parks all tools in their own authored poses.
            if (active < 0 || Layout == PlayerToolLayout.Independent)
                return index == active ? ActivePose : Tools[index].PassivePose;
            return Slots[(index - active + Tools.Length) % Tools.Length];
        }

        #endregion

        #region Validation

        /// <summary>Validates the complete module without silently adjusting any authored value.</summary>
        /// <param name="warning">Receives a missing identity, slot or invalid animation.</param>
        /// <returns>True when the tool configuration is internally consistent.</returns>
        public bool TryValidate(out string warning)
        {
            // Validate identities once before runtime binding or Editor Apply.
            warning = "Assign unique tools, valid poses and a finite non-negative switch duration.";
            if (Tools == null)
                return false;
            HashSet<PlayerTool> identities = new HashSet<PlayerTool>();
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            bool moving = false;
            foreach (PlayerToolEntry entry in Tools)
            {
                if (entry == null || entry.Tool == null || !identities.Add(entry.Tool) || string.IsNullOrWhiteSpace(entry.Tool.DisplayName)
                    || entry.MoveVisual && (entry.Path == null || !paths.Add(entry.Path) || !entry.PassivePose.IsValid()))
                    return false;
                if (entry.Tool.SwitchIn != null && !entry.Tool.SwitchIn.TryValidate(out warning)
                    || entry.Tool.SwitchOut != null && !entry.Tool.SwitchOut.TryValidate(out warning))
                    return false;
                moving |= entry.MoveVisual;
            }
            if (InitialTool != null && IndexOf(InitialTool) < 0)
            {
                warning = "The initial tool must belong to this Tools preset.";
                return false;
            }
            if (moving && (!float.IsFinite(SwitchDuration) || SwitchDuration < 0f
                || Layout is not (PlayerToolLayout.Independent or PlayerToolLayout.Cyclic)
                || Layout == PlayerToolLayout.Independent && !ActivePose.IsValid()))
                return false;
            if (moving && Layout == PlayerToolLayout.Cyclic)
            {
                warning = "Cyclic layout requires one valid slot for every configured tool.";
                if (Slots == null || Slots.Length != Tools.Length || SlotMotion is not (PlayerToolSlotMotion.Direct or PlayerToolSlotMotion.AroundPivot))
                    return false;
                foreach (PlayerToolPose slot in Slots)
                    if (!slot.IsValid())
                        return false;
                if (SlotMotion == PlayerToolSlotMotion.AroundPivot)
                {
                    warning = "A wheel needs a finite pivot, a non-zero finite axis and slots away from that axis.";
                    if (!float.IsFinite(Pivot.sqrMagnitude) || !float.IsFinite(Axis.sqrMagnitude) || Axis.sqrMagnitude < 0.000001f)
                        return false;
                    foreach (PlayerToolPose slot in Slots)
                        if (Vector3.ProjectOnPlane(slot.Position - Pivot, Axis).sqrMagnitude < 0.000001f)
                            return false;
                }
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Reports invalid Inspector configuration without normalizing stored values.</summary>
        private void OnValidate()
        {
            // Invalid values remain available for manual correction and Undo.
            if (!TryValidate(out string warning))
                Debug.LogWarning(warning, this);
        }

        #endregion

        #endregion
    }
}
