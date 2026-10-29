using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Organizes black hole authoring by destination, selection, motion and optional feedback.</summary>
    internal static class BlackHoleControls
    {
        #region Methods
        #region Sections

        /// <summary>Draws independent foldouts while retaining prefab-local references for reusable presets.</summary>
        /// <param name="eject">Ejection settings containing the final impulse mode.</param>
        /// <param name="sections">Retained workspace foldout state.</param>
        internal static void Draw(SerializedProperty eject, ObjectStudioSections sections)
        {
            SerializedProperty settings = eject.FindPropertyRelative("BlackHole");
            GameObject source = HierarchyPathMenu.Source(settings);
            if (sections.Draw("Black Hole Destination", "Collector and optional approach point.", settings,
                "ColliderPath", "ColliderIndex", "Offset", "ArrivalRadius", "GuideEntrance", "EntranceOffset"))
                using (new StudioIndentScope())
                    Destination(settings, source);
            if (sections.Draw("Black Hole Effect Area", "Choose the population's admission region independently of its destination.", settings, "Area", "Radius"))
                using (new StudioIndentScope())
                    Area(settings);
            if (sections.Draw("Black Hole Selection & Capacity", "Eligibility and bounded acquisition work.", settings, "Filter", "ReleaseKinematic", "Performance"))
                using (new StudioIndentScope())
                    Selection(settings);
            if (sections.Draw("Black Hole Attraction", "Duration, inward motion and optional spiral.", settings, "Duration", "Acceleration", "MaximumSpeed", "Spiral", "SpiralAxis", "SpiralSpeed"))
                using (new StudioIndentScope())
                    Attraction(settings);
            if (sections.Draw("Black Hole Collision & Navigation", "Stream traffic, solid obstacles and path planning.", settings,
                "IgnoreParticipantCollisions", "PassThrough", "PassageLayers", "Obstacles"))
                using (new StudioIndentScope())
                    Collisions(settings);
            if (sections.Draw("Black Hole Arrival Impulse", "Applied once at the collector, where the optional lifetime begins.", eject,
                "Mode", "BlackHole.Impulse", "BlackHole.ImpulseSpace", "BlackHole.ReplaceVelocity"))
                using (new StudioIndentScope())
                {
                    HoverControls.Field(eject, "Mode", "Force Mode");
                    Vector(settings, "Impulse", -100f, 100f);
                    Fields(settings, "ImpulseSpace", "ReplaceVelocity");
                }
            BlackHoleFeedbackControls.Draw(settings, source, sections);
        }

        #endregion
        #region Destination & Selection

        /// <summary>Separates collector selection from optional duct entrance guidance.</summary>
        /// <param name="settings">Black hole proposal.</param>
        /// <param name="source">Current authoring object.</param>
        private static void Destination(SerializedProperty settings, GameObject source)
        {
            BlackHoleColliderMenu.Draw(settings, source);
            Vector(settings, "Offset", -10f, 10f);
            HoverControls.Field(settings, "ArrivalRadius");
            HoverControls.Field(settings, "GuideEntrance");
            if (settings.FindPropertyRelative("GuideEntrance").boolValue)
                Vector(settings, "EntranceOffset", -50f, 50f);
        }

        /// <summary>Uses the gravity controller's sphere or oriented box, with independent entry and exit policy.</summary>
        /// <param name="settings">Black hole proposal.</param>
        private static void Area(SerializedProperty settings)
        {
            SerializedProperty area = settings.FindPropertyRelative("Area");
            HoverControls.Field(area, "Enabled", "Custom Area");
            if (!area.FindPropertyRelative("Enabled").boolValue)
            {
                HoverControls.Field(settings, "Radius");
                return;
            }
            HoverControls.Field(area, "Shape");
            Vector(area, "Offset", -50f, 50f);
            switch ((GravityAreaShape)area.FindPropertyRelative("Shape").enumValueIndex)
            {
                case GravityAreaShape.Sphere:
                    HoverControls.Field(area, "Radius");
                    break;
                case GravityAreaShape.Box:
                    Vector(area, "Size", 0.1f, 100f);
                    Vector(area, "Rotation", -180f, 180f);
                    break;
            }
            Fields(area, "TrackEntries", "RestoreOutside");
        }

        /// <summary>Exposes the safety cap beside optional eligibility and scheduling details.</summary>
        /// <param name="settings">Black hole proposal.</param>
        private static void Selection(SerializedProperty settings)
        {
            HoverControls.Field(settings.FindPropertyRelative("Performance"), "MaximumParticipants", "Object Cap");
            HoverControls.Field(settings, "ReleaseKinematic");
            GravityControls.Filter(settings.FindPropertyRelative("Filter"), false);
        }

        #endregion
        #region Motion

        /// <summary>Keeps tangential motion controls inside their own optional group.</summary>
        /// <param name="settings">Black hole proposal.</param>
        private static void Attraction(SerializedProperty settings)
        {
            Fields(settings, "Duration", "Acceleration", "MaximumSpeed");
            HoverControls.Field(settings, "Spiral");
            if (!settings.FindPropertyRelative("Spiral").boolValue)
                return;
            Vector(settings, "SpiralAxis", -1f, 1f);
            HoverControls.Field(settings, "SpiralSpeed");
        }

        /// <summary>Shows pathfinding and contact recovery only when solid obstacles can participate.</summary>
        /// <param name="settings">Black hole proposal.</param>
        private static void Collisions(SerializedProperty settings)
        {
            HoverControls.Field(settings, "IgnoreParticipantCollisions");
            HoverControls.Field(settings, "PassThrough");
            if (settings.FindPropertyRelative("PassThrough").boolValue)
            {
                HoverControls.Field(settings, "PassageLayers");
                if (settings.FindPropertyRelative("PassageLayers").intValue == -1)
                    return;
            }
            SerializedProperty obstacles = settings.FindPropertyRelative("Obstacles");
            HoverControls.Field(obstacles, "Enabled", "Navigate Obstacles");
            if (obstacles.FindPropertyRelative("Enabled").boolValue)
                HoverControls.Field(obstacles, "PathSpacing", "Navigation Precision");
        }

        #endregion
        #region Shared Fields

        /// <summary>Offers a slider and numeric entry for each vector axis.</summary>
        /// <param name="parent">Parent serialized settings.</param>
        /// <param name="name">Vector field to display.</param>
        /// <param name="minimum">Suggested slider lower endpoint.</param>
        /// <param name="maximum">Suggested slider upper endpoint.</param>
        internal static void Vector(SerializedProperty parent, string name, float minimum, float maximum)
        {
            SerializedProperty field = parent.FindPropertyRelative(name);
            if (!ObjectGroupScope.Allows(field))
                return;
            Vector3 value = field.vector3Value;
            EditorGUI.BeginChangeCheck();
            value.x = StudioFieldGUI.Slider(new GUIContent(field.displayName + " X", field.tooltip), value.x, minimum, maximum);
            value.y = StudioFieldGUI.Slider(new GUIContent(field.displayName + " Y", field.tooltip), value.y, minimum, maximum);
            value.z = StudioFieldGUI.Slider(new GUIContent(field.displayName + " Z", field.tooltip), value.z, minimum, maximum);
            if (EditorGUI.EndChangeCheck())
                field.vector3Value = value;
        }

        /// <summary>Draws related scalar controls through the shared tooltip and slider renderer.</summary>
        /// <param name="parent">Settings being edited.</param>
        /// <param name="names">Fields meaningful in the current mode.</param>
        internal static void Fields(SerializedProperty parent, params string[] names)
        {
            foreach (string name in names)
                HoverControls.Field(parent, name);
        }

        #endregion
        #endregion
    }
}
