using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    internal static class StudioCleanupObjectChecks
    {
        public static void Run()
        {
            List<string> results = new List<string>();
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Renderer renderer = root.GetComponent<Renderer>();
                ObjectOutline outline = root.AddComponent<ObjectOutline>();
                SerializedObject data = new SerializedObject(outline);
                data.FindProperty("renderers").arraySize = 1;
                data.FindProperty("renderers").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                data.ApplyModifiedPropertiesWithoutUndo();
                outline.Settings.HoverOnly = true;
                outline.Refresh();
                Tick(outline);
                Require(Visible(renderer), "Absent Hover ignores an inapplicable stored restriction");
                ObjectHover hover = root.AddComponent<ObjectHover>();
                outline.Refresh();
                Tick(outline);
                Require(!Visible(renderer), "Hover-only outline begins hidden");
                SetHovered(hover, true);
                Tick(outline);
                Require(Visible(renderer), "Hovered object lights up");
                typeof(ObjectInteraction).GetMethod("SetLocked", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(hover, new object[] { outline, true });
                Tick(outline);
                Require(!Visible(renderer), "Hover lock hides outline before the next targeting query");
                typeof(ObjectInteraction).GetMethod("SetLocked", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(hover, new object[] { outline, false });
                Tick(outline);
                Require(Visible(renderer), "Unlock restores hovered outline");
                SetHovered(hover, false);
                Tick(outline);
                Require(!Visible(renderer), "Leaving hover hides outline");
                ObjectHover second = root.AddComponent<ObjectHover>();
                outline.Refresh();
                SetHovered(second, true);
                Tick(outline);
                Require(Visible(renderer), "Any matching local Hover enables the outline");
                second.enabled = false;
                Tick(outline);
                Require(!Visible(renderer), "Disabled Hover cannot enable the outline");
                outline.Settings.HoverOnly = false;
                Tick(outline);
                Require(Visible(renderer), "Ordinary outline retains unrestricted behavior");
                results.Add("PASS outline hover state, multiple Hover components, lock/unlock and disabled Hover");
                OutlinePreset preset = ScriptableObject.CreateInstance<OutlinePreset>();
                try
                {
                    preset.Settings.HoverOnly = true;
                    OutlineSettings copy = ObjectWorkspace.Copy(preset.Settings);
                    Require(copy.HoverOnly, "Outline preset retains HoverOnly");
                    results.Add("PASS outline setting survives preset snapshot copy");
                }
                finally { Object.DestroyImmediate(preset); }
            }
            finally { Object.DestroyImmediate(root); }
            File.WriteAllLines("Library/CodexStudioCleanupCheck/object-checks.txt", results);
            Debug.Log("STUDIO_OBJECT_CHECKS_OK");
        }

        private static void SetHovered(ObjectHover hover, bool value)
        {
            typeof(ObjectHover).GetField("hovered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hover, value);
        }

        private static void Tick(ObjectOutline outline)
        {
            typeof(ObjectOutline).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(outline, null);
        }

        private static bool Visible(Renderer renderer)
        {
            return (renderer.renderingLayerMask & (1u << 31)) != 0;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
