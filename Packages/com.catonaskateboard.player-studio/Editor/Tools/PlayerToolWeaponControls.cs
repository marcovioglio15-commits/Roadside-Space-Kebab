using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Groups weapon controls by firing, attachment, impact and aiming dependencies.</summary>
    internal sealed class PlayerToolWeaponControls
    {
        #region Fields

        private bool firingOpen = true;
        private bool muzzleOpen;
        private bool effectsOpen = true;
        private bool impactOpen;
        private bool impactAreaOpen;
        private bool impactForceOpen;
        private bool aimOpen;

        #endregion

        #region Methods

        #region Weapon

        /// <summary>Shows only controls used by the selected weapon mode and enabled effects.</summary>
        /// <param name="data">Serialized tool draft.</param>
        /// <param name="tool">Tool edited by the dedicated window or Inspector.</param>
        /// <param name="hierarchy">Optional sample matching Tools Hierarchy Root.</param>
        internal void Draw(SerializedObject data, PlayerTool tool, GameObject hierarchy)
        {
            SerializedProperty weapon = data.FindProperty("Weapon");
            Transform root = hierarchy != null ? hierarchy.transform : null;
            bool projectile = weapon.FindPropertyRelative("Mode").intValue == (int)PlayerToolWeaponMode.Projectile;
            if (PlayerToolFields.Section(ref firingOpen, "Firing", "Weapon mode, shot timing, reach and collision."))
                projectile = Firing(data, weapon);
            if (PlayerToolFields.Section(ref muzzleOpen, "Firing Point", "Position and rotation used by shots and the Shot Effect."))
                Muzzle(data, weapon, tool, hierarchy, root);
            if (PlayerToolFields.Section(ref effectsOpen, "Effects", "Configure muzzle, shot and impact feedback."))
            {
                using StudioFieldLayout.SectionScope indent = new StudioFieldLayout.SectionScope(1);
                Effect(weapon.FindPropertyRelative("MuzzleEffect"), root, true);
                Effect(weapon.FindPropertyRelative("ShotEffect"), root, false);
                if (PlayerToolFields.Section(ref impactOpen, "Impact", "Surface effects, area contacts and simulated force."))
                {
                    using StudioFieldLayout.SectionScope impactIndent = new StudioFieldLayout.SectionScope(1);
                    Impact(weapon, projectile);
                    if (PlayerToolFields.Section(ref impactAreaOpen, "Impact Area", "Extend a confirmed hit to nearby exposed targets."))
                    {
                        using StudioFieldLayout.SectionScope areaIndent = new StudioFieldLayout.SectionScope(1);
                        if (PlayerToolFields.Toggle(weapon.FindPropertyRelative("ImpactAreaEnabled")))
                            StudioFields.Draw(weapon, "ImpactAreaRadius");
                    }
                    if (PlayerToolFields.Section(ref impactForceOpen, "Simulated Impact Force", "Push matching dynamic bodies at the shot contact."))
                        ImpactForce(weapon.FindPropertyRelative("SimulatedImpactForce"));
                }
            }
            if (PlayerToolFields.Section(ref aimOpen, "Aim", "Automatic aim, obstacle avoidance and optional shot alignment."))
                Aim(weapon, root, projectile);
        }

        /// <summary>Shows only the force and filter controls used by this weapon.</summary>
        /// <param name="settings">Serialized simulated impact force settings.</param>
        private static void ImpactForce(SerializedProperty settings)
        {
            using StudioFieldLayout.SectionScope indent = new StudioFieldLayout.SectionScope(1);
            if (!PlayerToolFields.Toggle(settings.FindPropertyRelative("Enabled")))
                return;
            // Layer and flag groups can be enabled independently.
            bool explosive = PlayerToolFields.Toggle(settings.FindPropertyRelative("ExplosiveResponse"));
            bool shatter = PlayerToolFields.Toggle(settings.FindPropertyRelative("SliceShattering"));
            if (shatter)
                StudioFields.Draw(settings, "SliceDistanceMultiplier");
            if (!explosive)
                StudioFields.Draw(settings, "Impulse");
            if (explosive || shatter)
                StudioFields.Draw(settings, "BurstSpeed", "BurstLift", "BurstSpin");
            StudioFields.Draw(settings, "IgnoreCarriedObjects");
            // Keep the multiplier with the effect it controls.
            if (PlayerToolFields.Toggle(settings.FindPropertyRelative("AffectImpactDegradation")))
                StudioFields.Draw(settings, "DegradationImpulseMultiplier");
            if (PlayerToolFields.Toggle(settings.FindPropertyRelative("FilterLayers")))
                StudioFields.Draw(settings, "Layers");
            StudioFields.Draw(settings, "Interactions", "FilterFlags");
            if (settings.FindPropertyRelative("FilterFlags").boolValue)
            {
                SerializedProperty flags = settings.FindPropertyRelative("Flags");
                ObjectFlagSelector.Draw(flags);
                if (flags.arraySize > 1)
                    StudioFields.Draw(settings, "FlagMatch");
            }
        }

        /// <summary>Draws the shared firing controls and the settings specific to the selected mode.</summary>
        /// <param name="data">Tool draft that owns the interaction toggle.</param>
        /// <param name="weapon">Serialized weapon settings.</param>
        /// <returns>True when the weapon launches projectiles.</returns>
        private static bool Firing(SerializedObject data, SerializedProperty weapon)
        {
            using StudioFieldLayout.SectionScope indent = new StudioFieldLayout.SectionScope(1);
            SerializedProperty mode = weapon.FindPropertyRelative("Mode");
            PlayerToolWeaponMode selected = (PlayerToolWeaponMode)StudioFieldGUI.EnumPopup(StudioFieldMenu.Value(mode,
                new GUIContent("Weapon Mode", mode.tooltip)), (PlayerToolWeaponMode)mode.intValue);
            if ((int)selected != mode.intValue)
                mode.intValue = (int)selected;
            StudioFields.Draw(weapon, "PlayShootSound");
            if (data.FindProperty("ReloadEnabled").boolValue)
                StudioFields.Draw(weapon, "PlayReloadSound");
            StudioFields.Draw(weapon, "ShotsPerUse");
            if (weapon.FindPropertyRelative("ShotsPerUse").intValue > 1)
                StudioFields.Draw(weapon, "ShotInterval");
            // Projectile and cursor shots share speed. The cursor can also use it for travel time.
            bool projectile = selected == PlayerToolWeaponMode.Projectile;
            if (projectile)
            {
                StudioFields.Draw(data, "InteractOnBulletImpact");
                PlayerToolFields.Reference(weapon.FindPropertyRelative("Bullet"), typeof(GameObject));
                StudioFields.Draw(weapon, "Speed", "Radius");
            }
            else
            {
                StudioFields.Draw(weapon, "IgnoreInteractionRange", "DelayCursorImpact");
                bool delayed = weapon.FindPropertyRelative("DelayCursorImpact").boolValue;
                if (delayed)
                    StudioFields.Draw(weapon, "CursorTravelMultiplier");
                SerializedProperty speed = weapon.FindPropertyRelative("Speed");
                StudioFieldGUI.PropertyField(speed, new GUIContent(delayed ? "Travel and Impact Speed" : "Impact Speed", speed.tooltip));
            }
            StudioFields.Draw(weapon, "Distance", "Layers", "HitTriggers", "Capacity");
            return projectile;
        }

        /// <summary>Edits the firing point against an existing hierarchy with slider-assisted offsets.</summary>
        /// <param name="data">Serialized tool draft flushed before opening the muzzle preview.</param>
        /// <param name="weapon">Weapon settings.</param>
        /// <param name="tool">Tool receiving the preview proposal.</param>
        /// <param name="hierarchy">Sample hierarchy object.</param>
        /// <param name="root">Resolved sample transform.</param>
        private static void Muzzle(SerializedObject data, SerializedProperty weapon, PlayerTool tool, GameObject hierarchy, Transform root)
        {
            using StudioFieldLayout.SectionScope indent = new StudioFieldLayout.SectionScope(1);
            PlayerHierarchyMenu.DrawPath(weapon.FindPropertyRelative("MuzzlePath"), root,
                new GUIContent("Muzzle Parent", weapon.FindPropertyRelative("MuzzlePath").tooltip), true);
            PlayerToolFields.Vector(weapon.FindPropertyRelative("MuzzlePosition"), -5f, 5f);
            PlayerToolFields.Vector(weapon.FindPropertyRelative("MuzzleRotation"), -180f, 180f);
            using (new EditorGUI.DisabledScope(root == null || EditorApplication.isPlayingOrWillChangePlaymode))
                if (StudioButton.Draw(new GUIContent("Muzzle Preview", "Position and rotate the firing point against the selected hierarchy.")))
                {
                    data.ApplyModifiedProperties();
                    PlayerToolMuzzleWindow.Open(tool, hierarchy);
                    GUIUtility.ExitGUI();
                }
        }

        #endregion

        #region Effects

        /// <summary>Reveals one burst's offsets, scale and lifetime only after its prefab is assigned.</summary>
        /// <param name="effect">Independent muzzle or shot effect settings.</param>
        /// <param name="root">Optional sample used by the arm attachment picker.</param>
        /// <param name="arm">Whether the burst has its own arm attachment instead of using the firing point.</param>
        private static void Effect(SerializedProperty effect, Transform root, bool arm)
        {
            // Each effect keeps its own foldout and source-prefab context menu.
            effect.isExpanded = StudioArrayGUI.Foldout(effect, new GUIContent(effect.displayName, effect.tooltip));
            if (!effect.isExpanded)
                return;
            using StudioFieldLayout.SectionScope indent = new StudioFieldLayout.SectionScope(1);
            SerializedProperty prefab = effect.FindPropertyRelative("Prefab");
            PlayerToolFields.Reference(prefab, typeof(GameObject));
            if (prefab.objectReferenceValue == null)
                return;
            if (arm)
                PlayerHierarchyMenu.DrawPath(effect.FindPropertyRelative("Path"), root,
                    new GUIContent("Arm Attachment", effect.FindPropertyRelative("Path").tooltip), true);
            PlayerToolFields.Vector(effect.FindPropertyRelative("Position"), -5f, 5f);
            PlayerToolFields.Vector(effect.FindPropertyRelative("Rotation"), -180f, 180f);
            StudioFields.Draw(effect, "Scale", "Lifetime");
            Playback(effect.FindPropertyRelative("Playback"));
        }

        /// <summary>Shows the placement controls and the impact trigger for the selected weapon mode.</summary>
        /// <param name="weapon">Serialized weapon settings.</param>
        /// <param name="projectile">Whether physical range-end impacts are meaningful.</param>
        private static void Impact(SerializedProperty weapon, bool projectile)
        {
            SerializedProperty prefab = weapon.FindPropertyRelative("Impact");
            PlayerToolFields.Reference(prefab, typeof(GameObject), new GUIContent("Prefab", prefab.tooltip));
            if (prefab.objectReferenceValue == null)
                return;
            // Cursor shots can show surface feedback before an interaction accepts the hit.
            if (!projectile)
                StudioFields.Draw(weapon, "ImpactOnCursorContact");
            StudioFields.Draw(weapon, "ImpactScale", "ImpactLifetime");
            Playback(weapon.FindPropertyRelative("ImpactPlayback"));
            StudioFields.Draw(weapon, "AutomaticImpactPlacement");
            bool automatic = weapon.FindPropertyRelative("AutomaticImpactPlacement").boolValue;
            if (automatic)
                StudioFields.Draw(weapon, "ImpactPlacementMode", "ImpactPenetration");
            if (!automatic || weapon.FindPropertyRelative("ImpactPlacementMode").enumValueIndex == (int)PlayerToolImpactPlacementMode.PreserveOrientation)
            {
                StudioFields.Draw(weapon, "ImpactOrientation");
                if (weapon.FindPropertyRelative("ImpactOrientation").enumValueIndex == (int)PlayerToolImpactOrientation.SurfaceNormal)
                    StudioFields.Draw(weapon, "ImpactSurfaceAligned");
                PlayerToolFields.Vector(weapon.FindPropertyRelative("ImpactRotation"), -180f, 180f);
            }
            if (!automatic)
                StudioFields.Draw(weapon, "ImpactOffset");
            if (projectile)
                StudioFields.Draw(weapon, "ImpactAtRange");
        }

        /// <summary>Shows the reference timing only when the selected prefab will be retimed.</summary>
        /// <param name="playback">Shared timing controls for one assigned effect.</param>
        private static void Playback(SerializedProperty playback)
        {
            // The default leaves particle timing with the prefab.
            if (PlayerToolFields.Toggle(playback.FindPropertyRelative("FitParticlePlayback")))
                StudioFields.Draw(playback, "ReferenceDuration");
        }

        #endregion

        #region Aiming

        /// <summary>Shows arm aiming independently of cursor interaction selection and limits shot preparation to projectiles.</summary>
        /// <param name="weapon">Serialized weapon settings.</param>
        /// <param name="root">Sample supplying valid visual pivots.</param>
        /// <param name="projectile">Whether a physical shot can require impact alignment.</param>
        private static void Aim(SerializedProperty weapon, Transform root, bool projectile)
        {
            using StudioFieldLayout.SectionScope indent = new StudioFieldLayout.SectionScope(1);
            if (!PlayerToolFields.Toggle(weapon.FindPropertyRelative("AutoAim")))
                return;
            PlayerHierarchyMenu.DrawPath(weapon.FindPropertyRelative("AimPath"), root,
                new GUIContent("Aim Pivot", weapon.FindPropertyRelative("AimPath").tooltip), root != null && root.GetComponent<PlayerHost>() == null);
            StudioFields.Draw(weapon, "AimDistance", "AimResponse", "RepositionAim");
            if (!weapon.FindPropertyRelative("RepositionAim").boolValue)
                return;
            StudioFields.Draw(weapon, "RepositionDistance", "RepositionRadius", "RepositionResponse");
            if (!projectile || !weapon.FindPropertyRelative("AutomaticImpactPlacement").boolValue
                || weapon.FindPropertyRelative("Impact").objectReferenceValue == null)
                return;
            if (PlayerToolFields.Toggle(weapon.FindPropertyRelative("AlignBeforeShot")))
                StudioFields.Draw(weapon, "ShotAlignmentDuration", "ShotAlignmentSamples");
        }

        #endregion

        #endregion
    }
}
