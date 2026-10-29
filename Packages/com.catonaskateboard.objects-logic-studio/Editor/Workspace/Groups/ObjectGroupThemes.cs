using System;
using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Combines related controls into useful preset units while preserving their conditional drawers.</summary>
    internal static class ObjectGroupThemes
    {
        #region Methods

        #region Composition

        /// <summary>Builds one group per theme in the same order as the interaction's controls.</summary>
        /// <param name="sections">Discovered sections with their complete field paths.</param>
        /// <returns>The thematic settings groups.</returns>
        internal static List<ObjectGroupInfo> Combine(List<ObjectGroupInfo> sections)
        {
            List<string> order = new List<string>();
            Dictionary<string, List<ObjectGroupInfo>> themes = new Dictionary<string, List<ObjectGroupInfo>>(StringComparer.Ordinal);
            foreach (ObjectGroupInfo section in sections)
            {
                string theme = Theme(section);
                if (!themes.TryGetValue(theme, out List<ObjectGroupInfo> entries))
                {
                    entries = new List<ObjectGroupInfo>();
                    themes.Add(theme, entries);
                    order.Add(theme);
                }
                entries.Add(section);
            }
            List<ObjectGroupInfo> groups = new List<ObjectGroupInfo>();
            foreach (string theme in order)
                groups.Add(new ObjectGroupInfo(theme, themes[theme].ToArray()));
            return groups;
        }

        /// <summary>Assigns familiar themes using stable section identifiers rather than translated labels.</summary>
        /// <param name="group">Existing section metadata.</param>
        /// <returns>The broader preset unit containing this section.</returns>
        internal static string Theme(ObjectGroupInfo group)
        {
            string section = group.Section.ToLowerInvariant();
            if (section == "general settings")
                return group.TypeName.Substring(group.TypeName.LastIndexOf('.') + 1) switch
                {
                    "ObjectEject" => "Ejection",
                    "ObjectGravityGenerator" => "Gravity",
                    "ObjectContactModifier" => "Contact effects",
                    "ObjectDegradation" => "Degradation",
                    "ObjectAmbient" => "Audio",
                    "ObjectSpraySauce" => "Surfaces",
                    "ObjectPresenceCap" => "Lifetime",
                    "ObjectAssemblyProduct" => "Assembly",
                    _ => "Interaction"
                };
            if (section.Contains("audio") || section.Contains("sound") || section.Contains("radio"))
                return "Audio";
            if (section.Contains("vfx") || section.Contains("flag change") || section == "identity change" || section.Contains("rumble") || section.Contains("air suction"))
                return "Feedback";
            if (section is "interaction" or "player tool" or "interaction debug" or "debug guides")
                return "Interaction";
            if (section.Contains("black hole"))
                return section.Contains("navigation") || section.Contains("collision") ? "Black Hole navigation" : "Black Hole";
            if (section.Contains("target") || section is "grab detection" or "detection" or "suspension" || section.Contains("visibility"))
                return "Targeting";
            if (section is "carry" or "item value")
                return "Carry";
            if (section.StartsWith("release", StringComparison.Ordinal))
                return "Physics";
            if (section is "trajectory")
                return "Throw";
            if (section.Contains("eject") || section is "ejection" || group.Parent.Contains(".Eject"))
                return "Ejection";
            if (section.Contains("animation"))
                return "Animation";
            if (section.Contains("dialogue"))
                return "Dialogue";
            if (section is "text" or "layout" or "background" or "appearance" or "placement")
                return "Appearance";
            if (section.Contains("dispenser") || section.Contains("dispens"))
                return "Dispenser";
            if (section.Contains("container") || section.Contains("storage"))
                return "Storage";
            if (section.Contains("spawn") || section is "day flow")
                return "Spawning";
            if (section.Contains("assembly") || section.Contains("product") || section is "recipe" or "magnets" or "completed appearance")
                return "Assembly";
            if (section.Contains("order"))
                return "Orders";
            if (section.Contains("contact") || section.Contains("modification") || section.Contains("carried") || section.Contains("consum") || section is "payment")
                return "Contact effects";
            if (section.Contains("trail") || section.Contains("deposit") || section.Contains("surface") || section.Contains("wetness") || section.Contains("spray"))
                return "Surfaces";
            if (section.Contains("deform"))
                return "Deformation";
            if (section.Contains("outline"))
                return "Outline";
            if (section.Contains("availab") || section is "conditions")
                return "Availability";
            if (section.Contains("gravity") || section.Contains("suspension") || section.Contains("affected") || section.Contains("area of effect"))
                return "Gravity";
            if (section.Contains("deathbox") || section.Contains("population"))
                return "Lifetime";
            if (section.Contains("slice"))
                return "Slice";
            if (section.Contains("degradation"))
                return "Degradation";
            return "Settings";
        }

        #endregion

        #endregion
    }
}
