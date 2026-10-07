using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Exposes complete interaction transfers directly on each interaction header.</summary>
    internal static class ObjectInteractionHeader
    {
        #region Methods
        #region Drawing

        /// <summary>Draws a colored foldout and binds its context menu to the active interaction proposal.</summary>
        /// <param name="state">Workspace retaining pending settings.</param>
        /// <param name="data">Current serialized workspace wrapper.</param>
        /// <param name="feature">Interaction represented by this header.</param>
        /// <param name="expanded">Current foldout state.</param>
        /// <param name="label">Interaction name and tooltip.</param>
        /// <returns>The requested foldout state.</returns>
        internal static bool Draw(ObjectWorkspace state, SerializedObject data, ObjectInteraction feature, bool expanded, GUIContent label)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            string key = "ObjectsLogicStudio.Interaction." + feature.GetType().Name;
            if (GUI.enabled && Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                // Selecting an inactive card is permitted only when the existing proposal is resolved.
                switch (feature)
                {
                    case ObjectHover hover when ObjectWorkspaceSession.Resolve(state) != hover:
                        ObjectWorkspaceSession.Select(state, hover.gameObject, System.Array.IndexOf(hover.GetComponents<ObjectHover>(), hover), out _);
                        data.Update();
                        break;
                    case ObjectSingleInteraction single when state.Single.Kind != single.Kind:
                        state.Single.Kind = single.Kind;
                        state.Single.Read(feature.gameObject);
                        data.Update();
                        break;
                    case ObjectExtendedInteraction extended when state.Extended.ComponentId != ObjectWorkspaceTarget.FileId(extended):
                        state.Extended.Select(extended);
                        data.Update();
                        break;
                }
                StudioFieldGroup.Context(rect, Fields(data, feature), key);
            }
            using StudioFieldColors colors = new StudioFieldColors(rect, key);
            return EditorGUI.Foldout(rect, expanded, label, true, EditorStyles.foldoutHeader);
        }

        /// <summary>Selects only fields owned by this interaction, excluding inactive draft configurations.</summary>
        /// <param name="data">Serialized workspace containing the selected interaction.</param>
        /// <param name="feature">Interaction defining the configuration and input bindings.</param>
        /// <returns>The exact field set represented by its header.</returns>
        internal static SerializedProperty[] Fields(SerializedObject data, ObjectInteraction feature)
        {
            if (feature is ObjectHover)
                return new[]
                {
                    data.FindProperty("Draft"), data.FindProperty("Binding.Name"), data.FindProperty("Binding.Enabled"),
                    data.FindProperty("Binding.AnchorPath"), data.FindProperty("Binding.ToolRequirement"),
                    data.FindProperty("Binding.FlagChange"), data.FindProperty("Binding.VisualEffect"), data.FindProperty("Binding.DrawGizmos")
                };
            string prefix = feature is ObjectSingleInteraction ? "Single.Draft." : "Extended.Draft.";
            List<string> names = new List<string> { "Name", "Enabled", "ToolRequirement", "FlagChange", "VisualEffect", "DrawGizmos" };
            switch (feature)
            {
                case ObjectSingleInteraction single:
                    names.Add("Action");
                    if (single is ObjectRelease)
                    {
                        names.Add("Release");
                        if (single is ObjectThrow)
                            names.Add("Throw");
                    }
                    else
                        names.Add(single.Kind.ToString());
                    break;
                case ObjectExtendedInteraction extended:
                    names.Add(extended switch
                    {
                        ObjectContactModifier => "Contact", ObjectAmbient => "Ambient", ObjectAvailableOrders => "Orders",
                        _ => extended.Kind.ToString()
                    });
                    if (extended is ObjectDialogue)
                    {
                        names.Add("StartAction");
                        names.Add("AdvanceAction");
                    }
                    else if (extended is ObjectAssemblyStation or ObjectSlice or ObjectRequestedInteraction)
                        names.Add("StartAction");
                    break;
            }
            return names.ConvertAll(name => data.FindProperty(prefix + name)).ToArray();
        }

        #endregion
        #endregion
    }
}
