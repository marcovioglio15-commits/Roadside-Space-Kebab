using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Groups weapon controls by firing, attachment, impact and aiming dependencies.</summary>
    internal static class PlayerToolWeaponControls
    {
        #region Methods

        #region Weapon

        /// <summary>Shows only controls used by the selected weapon mode and enabled effects.</summary>
        /// <param name="data">Serialized tool draft.</param>
        /// <param name="tool">Tool edited by the dedicated window or Inspector.</param>
        /// <param name="hierarchy">Optional sample matching Tools Hierarchy Root.</param>
        internal static void Draw(SerializedObject data, PlayerTool tool, GameObject hierarchy)
        {
            SerializedProperty weapon = data.FindProperty("Weapon");
            Transform root = hierarchy != null ? hierarchy.transform : null;
            StudioFields.Draw(weapon, "Mode", "ShotsPerUse");
            if (weapon.FindPropertyRelative("ShotsPerUse").intValue > 1)
                StudioFields.Draw(weapon, "ShotInterval");
            bool projectile = weapon.FindPropertyRelative("Mode").enumValueIndex == (int)PlayerToolWeaponMode.Projectile;
            if (projectile)
                StudioFields.Draw(data, "InteractOnBulletImpact");
            Muzzle(data, weapon, tool, hierarchy, root);
            if (projectile)
                StudioFields.Draw(weapon, "Bullet", "Speed", "Radius");
            StudioFields.Draw(weapon, "Distance", "Layers", "HitTriggers", "Capacity");
            Effect(weapon.FindPropertyRelative("MuzzleEffect"), root, true);
            Effect(weapon.FindPropertyRelative("ShotEffect"), root, false);
            Impact(weapon, projectile);
            Aim(weapon, root, projectile);
        }

        /// <summary>Edits the firing point against an existing hierarchy with slider-assisted offsets.</summary>
        /// <param name="data">Serialized tool draft flushed before opening the muzzle preview.</param>
        /// <param name="weapon">Weapon settings.</param>
        /// <param name="tool">Tool receiving the preview proposal.</param>
        /// <param name="hierarchy">Sample hierarchy object.</param>
        /// <param name="root">Resolved sample transform.</param>
        private static void Muzzle(SerializedObject data, SerializedProperty weapon, PlayerTool tool, GameObject hierarchy, Transform root)
        {
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
            effect.isExpanded = EditorGUILayout.Foldout(effect.isExpanded, new GUIContent(effect.displayName, effect.tooltip), true);
            if (!effect.isExpanded)
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            StudioFields.Draw(effect, "Prefab");
            if (effect.FindPropertyRelative("Prefab").objectReferenceValue == null)
                return;
            if (arm)
                PlayerHierarchyMenu.DrawPath(effect.FindPropertyRelative("Path"), root,
                    new GUIContent("Arm Attachment", effect.FindPropertyRelative("Path").tooltip), true);
            PlayerToolFields.Vector(effect.FindPropertyRelative("Position"), -5f, 5f);
            PlayerToolFields.Vector(effect.FindPropertyRelative("Rotation"), -180f, 180f);
            StudioFields.Draw(effect, "Scale", "Lifetime");
        }

        /// <summary>Shares placement controls while hiding range-end feedback from instant cursor mode.</summary>
        /// <param name="weapon">Serialized weapon settings.</param>
        /// <param name="projectile">Whether physical range-end impacts are meaningful.</param>
        private static void Impact(SerializedProperty weapon, bool projectile)
        {
            StudioFields.Draw(weapon, "Impact");
            if (weapon.FindPropertyRelative("Impact").objectReferenceValue == null)
                return;
            StudioFields.Draw(weapon, "ImpactScale", "ImpactLifetime", "AutomaticImpactPlacement");
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

        #endregion

        #region Aiming

        /// <summary>Shows arm aiming independently of cursor interaction selection and limits shot preparation to projectiles.</summary>
        /// <param name="weapon">Serialized weapon settings.</param>
        /// <param name="root">Sample supplying valid visual pivots.</param>
        /// <param name="projectile">Whether a physical shot can require impact alignment.</param>
        private static void Aim(SerializedProperty weapon, Transform root, bool projectile)
        {
            StudioFields.Draw(weapon, "AutoAim");
            if (!weapon.FindPropertyRelative("AutoAim").boolValue)
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
            StudioFields.Draw(weapon, "AlignBeforeShot");
            if (weapon.FindPropertyRelative("AlignBeforeShot").boolValue)
                StudioFields.Draw(weapon, "ShotAlignmentDuration", "ShotAlignmentSamples");
        }

        #endregion

        #endregion
    }
}
