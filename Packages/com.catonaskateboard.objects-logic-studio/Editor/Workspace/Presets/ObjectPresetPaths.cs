using System;
using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Maps editor group paths to component fields so reusable overrides replace the captured baseline cleanly.</summary>
    internal static class ObjectPresetPaths
    {
        #region Methods

        #region Mapping

        /// <summary>Collects runtime fields supplied by group presets rather than the complete snapshot.</summary>
        /// <param name="preset">Main interaction preset containing the overrides.</param>
        /// <param name="feature">Matching destination interaction.</param>
        /// <returns>Component field paths replaced by granular presets.</returns>
        internal static HashSet<string> Overrides(ObjectInteractionPreset preset, ObjectInteraction feature)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            if (preset == null)
                return paths;
            foreach (ObjectGroupPreset group in preset.Groups)
                if (group?.Group != null)
                    foreach (string path in group.Group.Paths)
                    {
                        string runtime = Component(path, feature);
                        if (!string.IsNullOrEmpty(runtime))
                            paths.Add(runtime);
                    }
            return paths;
        }

        /// <summary>Translates the existing typed draft adapters without runtime reflection.</summary>
        /// <param name="path">Absolute serialized draft field.</param>
        /// <param name="feature">Component family supplying its settings layout.</param>
        /// <returns>The equivalent component path, or empty for editor-only identities.</returns>
        private static string Component(string path, ObjectInteraction feature)
        {
            if (path.StartsWith("Draft.", StringComparison.Ordinal))
                return "configuration." + path.Substring(6);
            string prefix = feature is ObjectHover ? "Binding." : feature is ObjectSingleInteraction ? "Single.Draft." : "Extended.Draft.";
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                return string.Empty;
            string field = path.Substring(prefix.Length);
            int separator = field.IndexOf('.');
            string name = separator < 0 ? field : field.Substring(0, separator);
            string suffix = separator < 0 ? string.Empty : field.Substring(separator);
            string root = name switch
            {
                "Name" => "interactionName",
                "Enabled" => "m_Enabled",
                "DrawGizmos" => "drawGizmos",
                "Action" => "action",
                "StartAction" => feature is ObjectDialogue ? "startAction" : "action",
                "AdvanceAction" => "advanceAction",
                "ToolRequirement" => "toolRequirement",
                "FlagChange" => "flagChange",
                "VisualEffect" => "visualEffect",
                "AnchorPath" => "anchor",
                "OrdersCompletionId" => "settings.CompletionSource",
                "Release" => "physics",
                "Throw" => "trajectory",
                "Unlock" or "AssemblyProduct" => string.Empty,
                _ => "settings"
            };
            if (name is "Unlock" or "AssemblyProduct")
            {
                if (suffix.StartsWith(".Settings", StringComparison.Ordinal))
                    return "settings" + suffix.Substring(9);
                return suffix switch { ".TargetId" => "settings.Target", ".ReplacementId" => "settings.Replacement", _ => string.Empty };
            }
            return root + suffix;
        }

        #endregion

        #endregion
    }
}
