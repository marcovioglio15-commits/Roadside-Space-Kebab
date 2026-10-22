using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits conditional tool behavior and opens a muzzle layout against an existing sample hierarchy.</summary>
    [CustomEditor(typeof(PlayerTool))]
    internal sealed class PlayerToolEditor : UnityEditor.Editor
    {
        #region Fields

        [Header("Preview")]
        [Tooltip("Sample matching Tools Hierarchy Root. Used only to browse visual targets and preview the muzzle.")]
        [SerializeField]
        private GameObject hierarchySource;
        [Tooltip("Whether the player used to open this Inspector supports tool switching.")]
        [SerializeField]
        private bool switching = true;

        #endregion

        #region Methods

        #region Inspector

        /// <summary>Opens a visible tool editor even when the main Inspector is closed or locked.</summary>
        /// <param name="tool">Tool asset to edit.</param>
        /// <param name="root">Current Tools hierarchy root.</param>
        /// <param name="allowSwitching">Whether switch animation fields are relevant to this player.</param>
        internal static void Open(PlayerTool tool, Transform root, bool allowSwitching)
        {
            // Keep the workspace's selected player while editing its reusable tool asset.
            PlayerToolWindow.Open(tool, root, allowSwitching);
        }

        /// <summary>Supplies hierarchy choices to the Inspector hosted by the dedicated tool window.</summary>
        /// <param name="root">Current Tools hierarchy root.</param>
        /// <param name="allowSwitching">Whether switch animations apply to the current player.</param>
        internal void Bind(Transform root, bool allowSwitching)
        {
            hierarchySource = root != null ? root.gameObject : null;
            switching = allowSwitching;
        }

        /// <summary>Shows weapon, reload and switch settings only when their corresponding modes are enabled.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            // Project Inspectors can open the same transactional editor without changing selection.
            if (EditorUtility.IsPersistent(target)
                && StudioButton.Draw(new GUIContent("Open Tool Editor", "Edit a detached tool proposal with independent Apply and Discard.")))
                Open((PlayerTool)target, hierarchySource != null ? hierarchySource.transform : null, switching);
            hierarchySource = (GameObject)StudioFieldGUI.ObjectField(new GUIContent("Hierarchy Source",
                "Choose the same root used by Tools, so animation, muzzle and aim paths match."), hierarchySource, typeof(GameObject), true);
            StudioFields.Draw(serializedObject, "DisplayName");
            Animation("UseAnimation");
            StudioFields.Draw(serializedObject, "UseInterval", "IsWeapon");
            if (serializedObject.FindProperty("IsWeapon").boolValue)
            {
                StudioFields.Draw(serializedObject, "InteractOnBulletImpact");
                Weapon(serializedObject.FindProperty("Weapon"));
            }
            StudioFields.Draw(serializedObject, "ReloadEnabled");
            if (serializedObject.FindProperty("ReloadEnabled").boolValue)
                Reload();
            // Shared tool assets retain switch clips for players that use multiple tools.
            if (switching)
            {
                Animation("SwitchIn");
                Animation("SwitchOut");
            }
            serializedObject.ApplyModifiedProperties();
            if (!((PlayerTool)target).TryValidate(false, out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Draws the automatic reload option and ordered animation steps when reload is enabled.</summary>
        private void Reload()
        {
            // Automatic and manual reload share the same animation combo.
            StudioFields.Draw(serializedObject, "AutoReload");
            SerializedProperty steps = serializedObject.FindProperty("ReloadCombo");
            StudioArrayGUI.Add(steps, "Add Reload Step", () => (PlayerToolAnimationPreset)null);
            for (int index = 0; index < steps.arraySize; index++)
            {
                if (StudioArrayGUI.Header(steps, index, new GUIContent("Reload Step " + (index + 1), steps.tooltip)))
                    break;
                if (!steps.GetArrayElementAtIndex(index).isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                PlayerToolFields.Animation(steps.GetArrayElementAtIndex(index), hierarchySource != null ? hierarchySource.transform : null,
                    new GUIContent("Animation", steps.tooltip));
            }
        }

        /// <summary>Shares the tool's hierarchy context with each referenced animation editor.</summary>
        /// <param name="name">Serialized animation field.</param>
        private void Animation(string name)
        {
            // All action clips resolve tracks relative to the same Tools root.
            PlayerToolFields.Animation(serializedObject.FindProperty(name), hierarchySource != null ? hierarchySource.transform : null);
        }

        /// <summary>Edits muzzle bindings, projectile behavior and only the active optional impact and aim settings.</summary>
        /// <param name="weapon">Serialized weapon settings.</param>
        private void Weapon(SerializedProperty weapon)
        {
            Transform root = hierarchySource != null ? hierarchySource.transform : null;
            PlayerHierarchyMenu.DrawPath(weapon.FindPropertyRelative("MuzzlePath"), root,
                new GUIContent("Muzzle Parent", weapon.FindPropertyRelative("MuzzlePath").tooltip), true);
            PlayerToolFields.Vector(weapon.FindPropertyRelative("MuzzlePosition"), -5f, 5f);
            PlayerToolFields.Vector(weapon.FindPropertyRelative("MuzzleRotation"), -180f, 180f);
            using (new EditorGUI.DisabledScope(root == null || EditorApplication.isPlayingOrWillChangePlaymode))
                if (StudioButton.Draw(new GUIContent("Muzzle Preview", "Position and rotate the firing point against the selected hierarchy.")))
                {
                    serializedObject.ApplyModifiedProperties();
                    PlayerToolMuzzleWindow.Open((PlayerTool)target, hierarchySource);
                    GUIUtility.ExitGUI();
                }
            StudioFields.Draw(weapon, "Bullet", "Speed", "Distance", "Radius", "Layers", "HitTriggers", "Capacity", "Impact");
            if (weapon.FindPropertyRelative("Impact").objectReferenceValue != null)
            {
                StudioFields.Draw(weapon, "ImpactScale", "ImpactLifetime", "AutomaticImpactPlacement");
                bool automatic = weapon.FindPropertyRelative("AutomaticImpactPlacement").boolValue;
                if (automatic)
                    StudioFields.Draw(weapon, "ImpactPlacementMode", "ImpactPenetration");
                if (!automatic
                    || weapon.FindPropertyRelative("ImpactPlacementMode").enumValueIndex == (int)PlayerToolImpactPlacementMode.PreserveOrientation)
                {
                    StudioFields.Draw(weapon, "ImpactOrientation");
                    if (weapon.FindPropertyRelative("ImpactOrientation").enumValueIndex == (int)PlayerToolImpactOrientation.SurfaceNormal)
                        StudioFields.Draw(weapon, "ImpactSurfaceAligned");
                    PlayerToolFields.Vector(weapon.FindPropertyRelative("ImpactRotation"), -180f, 180f);
                }
                if (!automatic)
                    StudioFields.Draw(weapon, "ImpactOffset");
                StudioFields.Draw(weapon, "ImpactAtRange");
            }
            StudioFields.Draw(weapon, "AutoAim");
            if (!weapon.FindPropertyRelative("AutoAim").boolValue)
                return;
            PlayerHierarchyMenu.DrawPath(weapon.FindPropertyRelative("AimPath"), root,
                new GUIContent("Aim Pivot", weapon.FindPropertyRelative("AimPath").tooltip), root != null && root.GetComponent<PlayerHost>() == null);
            StudioFields.Draw(weapon, "AimDistance", "AimResponse");
            StudioFields.Draw(weapon, "RepositionAim");
            if (weapon.FindPropertyRelative("RepositionAim").boolValue)
            {
                StudioFields.Draw(weapon, "RepositionDistance", "RepositionRadius", "RepositionResponse");
                if (weapon.FindPropertyRelative("AutomaticImpactPlacement").boolValue
                    && weapon.FindPropertyRelative("Impact").objectReferenceValue != null)
                {
                    StudioFields.Draw(weapon, "AlignBeforeShot");
                    if (weapon.FindPropertyRelative("AlignBeforeShot").boolValue)
                        StudioFields.Draw(weapon, "ShotAlignmentDuration", "ShotAlignmentSamples");
                }
            }
        }

        #endregion

        #endregion
    }
}
