using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shows unobtrusive muzzle origins and forward arrows for the selected player's configured weapons.</summary>
    internal static class PlayerToolsGizmos
    {
        #region Methods

        #region Scene Drawing

        /// <summary>Draws only valid configured muzzles without sampling animations or changing scene objects.</summary>
        /// <param name="tools">Selected player module.</param>
        /// <param name="type">Unity selection flags.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy)]
        private static void Draw(PlayerTools tools, GizmoType type)
        {
            PlayerToolsPreset settings = tools.GetComponent<PlayerHost>().MasterPreset?.ToolsPreset;
            if (settings?.Tools == null)
                return;
            Transform root = PlayerHierarchy.Resolve(tools.transform, settings.RootPath);
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.2f, 0.85f, 1f));
            // Compact arrows keep long-range weapons from filling the Scene view with debug lines.
            foreach (PlayerToolEntry entry in settings.Tools)
            {
                PlayerToolWeaponSettings weapon = entry?.Tool != null && entry.Tool.IsWeapon ? entry.Tool.Weapon : null;
                Transform muzzle = weapon != null ? PlayerHierarchy.Resolve(root, weapon.MuzzlePath) : null;
                if (muzzle == null || !PlayerToolWeaponSettings.Finite(weapon.MuzzlePosition) || !PlayerToolWeaponSettings.Finite(weapon.MuzzleRotation))
                    continue;
                Vector3 position = muzzle.TransformPoint(weapon.MuzzlePosition);
                Quaternion rotation = muzzle.rotation * Quaternion.Euler(weapon.MuzzleRotation);
                float size = HandleUtility.GetHandleSize(position) * 0.4f;
                Handles.ArrowHandleCap(0, position, rotation, size, EventType.Repaint);
                Handles.DrawWireDisc(position, rotation * Vector3.forward, size * 0.06f);
                if (weapon.Mode == PlayerToolWeaponMode.CursorInteraction)
                    Handles.Label(position, new GUIContent("Cursor interaction", "The gameplay pointer selects the target; this muzzle supplies shot effects and impact direction."));
                // The optional arm burst has its own attachment and may sit away from the firing point.
                if (weapon.MuzzleEffect?.Prefab == null)
                    continue;
                Transform attachment = PlayerHierarchy.Resolve(root, weapon.MuzzleEffect.Path);
                if (attachment == null || !PlayerToolWeaponSettings.Finite(weapon.MuzzleEffect.Position))
                    continue;
                Vector3 effect = attachment.TransformPoint(weapon.MuzzleEffect.Position);
                Handles.DrawDottedLine(position, effect, 4f);
                Handles.SphereHandleCap(0, effect, Quaternion.identity, size * 0.08f, EventType.Repaint);
            }
        }

        #endregion

        #endregion
    }
}
