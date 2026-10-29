using System;
using System.Collections.Generic;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Breaks larger control sections into small reusable groups without duplicating their field logic.</summary>
    internal static class ObjectGroupLayout
    {
        #region Types

        /// <summary>Lists the fields edited together in one subgroup.</summary>
        private readonly struct Split
        {
            internal readonly string Name;
            internal readonly string[] Fields;

            #region Methods

            /// <summary>Defines a short group label and its relative field paths.</summary>
            /// <param name="name">Group label.</param>
            /// <param name="fields">Fields included in this group.</param>
            internal Split(string name, params string[] fields)
            {
                Name = name;
                Fields = fields;
            }

            #endregion
        }

        #endregion

        #region Definitions

        private static readonly Dictionary<string, Split[]> splits = new Dictionary<string, Split[]>(StringComparer.Ordinal)
        {
            ["Carry"] = new[]
            {
                new Split("Carry pose", "Space", "Offset", "Rotation"),
                new Split("Pickup transition", "Instant", "TransitionDuration"),
                new Split("Physical pickup", "PhysicalPickup", "PickupTimeMargin", "PickupSpeed", "PickupPush"),
                new Split("Carry collisions", "WorldCollisions", "FollowSpeed", "RecoveryResponse", "RotationSpeed", "CollisionPadding"),
                new Split("Contact rotation", "ContactRotation", "ContactAngle", "ContactResponse"),
                new Split("Hover while carried", "ShowHover")
            },
            ["Item Audio"] = new[] { new Split("Pickup sound", "PickupSound"), new Split("Collision sounds", "CollisionAudio") },
            ["Grab Detection"] = new[]
            {
                new Split("Grab targeting", "Distance", "TargetMode", "SolidHitOnly", "CenterRadius", "TargetOffset"),
                new Split("Grab obstacles", "ObstacleMask")
            },
            ["Release Body"] = new[]
            {
                new Split("Release weight and drag", "OverrideBody", "Mass", "LinearDamping", "AngularDamping"),
                new Split("Release physics", "Gravity", "Constraints", "CollisionDetection", "MaxAngularSpeed")
            },
            ["Dispenser Supply"] = new[]
            {
                new Split("Supply source", "UseContainer", "Prefab"),
                new Split("Stock", "Unlimited", "Stock")
            },
            ["Dispenser Output"] = new[]
            {
                new Split("Output mode", "Mode", "Interval", "PickupDuration"),
                new Split("Outlet layout", "Outlets", "OutputRotation")
            },
            ["Container Storage"] = new[]
            {
                new Split("Accepted items", "AllowedFlags", "Match"),
                new Split("Storage capacity", "Unlimited", "Capacity", "LimitToDispenserSpace"),
                new Split("Refill dispenser", "RefillDispenser"),
                new Split("Stored item layout", "KeepVisible", "Position", "Rotation", "Spacing")
            },
            ["Transform Animation"] = new[]
            {
                new Split("Animation poses", "Path", "StateA", "StateB", "StartAtB"),
                new Split("Animation timing", "Duration", "ForwardRotation", "ReturnRotation"),
                new Split("Automatic return", "AutoReturn", "ReturnDelay", "AutoReturnDuration", "AutoReturnRotation")
            },
            ["Contact Detection"] = new[]
            {
                new Split("Contact participants", "Flags", "IncludeTriggers"),
                new Split("Contact timing", "ContactDuration", "ContactTolerance", "QueryInterval")
            },
            ["Modification"] = new[]
            {
                new Split("Effect timing", "Duration", "WhileContact", "RevertDelay"),
                new Split("Effect interruption", "CompleteAfterSeparation", "ResumeAfterInterruption", "RepeatAfterSeparation")
            },
            ["Dialogue Activation"] = new[]
            {
                new Split("Dialogue trigger", "Trigger", "Consumption"),
                new Split("Dialogue distances", "Distance", "ExitDistance"),
                new Split("Dialogue priority", "Priority", "PreferSingleActions", "PreferredActions")
            },
            ["Dialogue Visibility"] = new[]
            {
                new Split("Dialogue sight", "RequireSightToStart", "RequireSightToContinue", "ObstacleMask"),
                new Split("Lost sight", "HideWhenSightLost")
            },
            ["Dialogue Flow"] = new[]
            {
                new Split("Entry selection", "Selection", "UseSpawnPresets", "SpawnPresets"),
                new Split("Dialogue interruption", "Interruption"),
                new Split("Dialogue replay", "ReplayOnReturn", "ReplayOnInput")
            },
            ["Dialogue Audio"] = new[]
            {
                new Split("Phrase audio", "Audio.Voice", "Audio.Interval"),
                new Split("Result audio", "Audio.Completion")
            },
            ["Detection"] = new[]
            {
                new Split("Hover targeting", "targetMode", "playerDistance", "centerRadius"),
                new Split("Hover visibility", "queryInterval", "releaseDelay", "obstacleMask")
            },
            ["Placement"] = new[]
            {
                new Split("Hover anchor", "anchorOffset"),
                new Split("Label placement", "fixedHud", "worldOffset", "screenOffset", "followSmoothing")
            },
            ["Text"] = new[]
            {
                new Split("Label text", "content", "richText"),
                new Split("Label font", "font", "fontSize", "fontStyle", "textColor"),
                new Split("Text alignment", "alignment", "horizontalOverflow", "verticalOverflow")
            },
            ["Start VFX"] = new[]
            {
                new Split("Start effect", "Enabled", "Prefab"),
                new Split("Effect lifetime", "AutoTiming", "Duration"),
                new Split("Effect attachment", "OnContactObject", "FollowObject", "Position", "Rotation", "Scale")
            },
            ["Ejection"] = new[]
            {
                new Split("Eject contacts", "IncludeTriggers", "Tolerance", "ReleaseKinematic"),
                new Split("Eject impulse", "Mode", "SelfImpulse", "UseBulletImpulse", "BulletImpulseMultiplier")
            },
            ["Black Hole Attraction"] = new[]
            {
                new Split("Attraction", "Duration", "Acceleration", "MaximumSpeed"),
                new Split("Spiral", "Spiral", "SpiralAxis", "SpiralSpeed")
            },
            ["Assembly Table"] = new[]
            {
                new Split("Assembly product", "ProductPrefab"),
                new Split("Assembly activation", "Trigger", "Distance", "RequireSight", "Obstacles", "AcceptReturnedProduct"),
                new Split("Ingredient contacts", "ContactDuration", "ContactQueryInterval", "ContactTolerance", "IncludeTriggers", "IgnoreCarriedIngredients"),
                new Split("Product placement", "OutputPosition", "OutputRotation", "AnimatePlacement", "PlacementDuration")
            },
            ["Recipe"] = new[]
            {
                new Split("Required ingredients", "Ingredients"),
                new Split("Product price", "PriceEnabled", "Price")
            },
            ["Deformation response"] = new[]
            {
                new Split("Deformation contacts", "Collisions", "Layers", "MinimumImpact", "FullImpact"),
                new Split("Deformation recovery", "Compression", "Duration", "Oscillations", "Damping", "Cooldown", "StrongestWins")
            },
            ["Trail detection"] = new[]
            {
                new Split("Trail surfaces", "Layers", "FilterFlags", "Flags"),
                new Split("Trail spacing", "MinimumSpeed", "Sliding", "Spacing", "Interval")
            },
            ["Population limit"] = new[]
            {
                new Split("Population", "Maximum", "IncludeFixedObjects", "UseOwnFlags", "Flags"),
                new Split("Offscreen removal", "OffscreenDelay")
            },
            ["Deathbox contacts"] = new[]
            {
                new Split("Deathbox targets", "Layers", "AffectPlayer", "AffectStatic"),
                new Split("Deathbox timing", "QueryInterval")
            },
            ["Spawn Conditions"] = new[]
            {
                new Split("Spawn source", "SourcePrefab"),
                new Split("Completion rules", "Conditions", "RequireAll"),
                new Split("Repeat spawning", "Repeat", "CyclesPerInstance")
            },
            ["Spawn Draw"] = new[]
            {
                new Split("Spawn outputs", "Selection", "Choices"),
                new Split("Spawn selection", "Draws", "WithoutReplacement", "Chance", "FixedSeed", "Seed")
            },
            ["Spawn Placement"] = new[]
            {
                new Split("Spawn timing", "MinimumDelay", "MaximumDelay"),
                new Split("Spawn pose", "Position", "Rotation", "Radius", "RandomYaw"),
                new Split("Spawn population", "LimitAlive", "MaximumAlive")
            },
            ["Outline Appearance"] = new[]
            {
                new Split("Outline activation", "HoverOnly"),
                new Split("Outline edges", "Thickness", "Color"),
                new Split("Outline lighting", "Intensity", "EdgeAngle")
            },
            ["Order Board & Reveal"] = new[]
            {
                new Split("Order board", "Board"),
                new Split("Order reveal", "WaitForCompletion", "CompletionSource")
            }
        };

        #endregion

        #region Methods

        #region Discovery

        /// <summary>Registers the small groups used by the section, keeping unknown fields available.</summary>
        /// <param name="groups">List receiving groups in display order.</param>
        /// <param name="parent">Serialized section block.</param>
        /// <param name="section">Existing drawer section name.</param>
        /// <param name="tooltip">Brief section description.</param>
        /// <param name="fields">Fields passed by the existing controls.</param>
        internal static void Collect(List<ObjectGroupInfo> groups, SerializedProperty parent, string section, string tooltip, string[] fields)
        {
            if (!splits.TryGetValue(section, out Split[] definitions))
            {
                groups.Add(new ObjectGroupInfo(parent, section, section, tooltip, fields));
                return;
            }
            HashSet<string> covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (Split split in definitions)
            {
                List<string> selected = new List<string>();
                foreach (string field in split.Fields)
                    if (parent.FindPropertyRelative(field) != null)
                    {
                        selected.Add(field);
                        covered.Add(field.Split('.')[0]);
                    }
                if (selected.Count > 0)
                    groups.Add(new ObjectGroupInfo(parent, section, split.Name, ObjectFieldText.Group(split.Name, tooltip), selected.ToArray()));
            }
            // New fields remain accessible even before a section receives a more specific split.
            List<string> remaining = new List<string>();
            foreach (string field in fields)
                if (!covered.Contains(field) && parent.FindPropertyRelative(field) != null)
                    remaining.Add(field);
            if (remaining.Count > 0)
                groups.Add(new ObjectGroupInfo(parent, section, section, tooltip, remaining.ToArray()));
        }

        #endregion

        #endregion
    }
}
