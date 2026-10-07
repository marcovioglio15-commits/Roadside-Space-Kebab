using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Provides typed field copying and lets each Studio register its own prefab update operations.</summary>
    [InitializeOnLoad]
    public static class StudioFieldMenu
    {
        #region State

        internal static StudioPropertyValue Clipboard { get; set; }
        private static SerializedProperty next;
        internal static SerializedProperty NextProperty => next;

        /// <summary>Adds operations that understand the owning tool's interaction and preset mapping.</summary>
        public static event Action<GenericMenu, SerializedProperty> Populate;
        /// <summary>Persists detached tool drafts after a delayed Paste operation.</summary>
        public static event Action<UnityEngine.Object> Changed;
        /// <summary>Records ownership before a delayed edit changes a temporary module draft.</summary>
        public static event Action<UnityEngine.Object> BeforeChange;
        /// <summary>Protects delayed commands from workspace selection changes.</summary>
        public static event Action<UnityEngine.Object, string, List<Func<bool>>> CollectGuards;
        /// <summary>Lets a tool translate local identities for complex clipboard operations.</summary>
        public static event Func<GenericMenu, SerializedProperty[], bool> ClipboardMenu;

        #endregion

        #region Methods

        #region Menus

        /// <summary>Extends native serialized fields, including fields inside nested Unity property drawers.</summary>
        static StudioFieldMenu()
        {
            // Native label menus and custom value-area menus share the same commands.
            EditorApplication.contextualPropertyMenu += AddItems;
        }

        /// <summary>Associates the next custom scalar widget with its serialized field.</summary>
        /// <param name="property">Property whose raw value will be drawn next.</param>
        public static void Next(SerializedProperty property)
        {
            // The wrapper consumes this association once so it cannot leak to a following field.
            next = property;
        }

        /// <summary>Associates a custom widget with its property while preserving its label or initial value.</summary>
        /// <typeparam name="T">Widget argument type.</typeparam>
        /// <param name="property">Serialized field represented by the widget.</param>
        /// <param name="argument">Unchanged label or value passed to the native widget.</param>
        /// <returns>The supplied widget argument.</returns>
        public static T Value<T>(SerializedProperty property, T argument)
        {
            // Argument evaluation happens before the widget consumes its one-use property association.
            Next(property);
            return argument;
        }

        /// <summary>Retrieves and clears the pending serialized association.</summary>
        /// <returns>The next custom widget's property, or null for a transient UI field.</returns>
        internal static SerializedProperty TakeNext()
        {
            // Control wrappers call this even on Layout and Repaint.
            SerializedProperty property = next;
            next = null;
            return property;
        }

        /// <summary>Opens the field menu from either the label or value area of a custom control.</summary>
        /// <param name="rect">Complete visible field rectangle.</param>
        /// <param name="property">Field represented by this control.</param>
        public static void Context(Rect rect, SerializedProperty property)
        {
            // Right-clicks are intercepted before native text and popup widgets consume them.
            if (property == null || Event.current.type != EventType.ContextClick || !rect.Contains(Event.current.mousePosition))
                return;
            GenericMenu menu = new GenericMenu();
            AddItems(menu, property);
            menu.ShowAsContext();
            Event.current.Use();
        }

        /// <summary>Creates callbacks from durable owner/path data instead of retaining a temporary property.</summary>
        /// <param name="menu">Native menu receiving the four shared operations.</param>
        /// <param name="property">Clicked serialized field.</param>
        private static void AddItems(GenericMenu menu, SerializedProperty property)
        {
            // Snapshot values before the menu closes its IMGUI event.
            if (!(property.serializedObject.targetObject.GetType().Namespace ?? string.Empty).StartsWith("CatOnASkateboard", StringComparison.Ordinal))
                return;
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            StudioPropertyValue copied = new StudioPropertyValue(property);
            Func<bool> guard = Guard(owner, path);
            bool handled = TryClipboard(menu, new[] { property });
            if (!handled)
                menu.AddItem(new GUIContent("Copy"), false, () => Clipboard = copied);
            if (!handled && GUI.enabled && !EditorApplication.isPlayingOrWillChangePlaymode && Clipboard != null && Clipboard.Accepts(property))
            {
                StudioPropertyValue pasted = Clipboard;
                menu.AddItem(new GUIContent("Paste"), false, () => Paste(owner, path, pasted, guard));
            }
            else if (!handled)
                menu.AddDisabledItem(new GUIContent("Paste"));
            int count = menu.GetItemCount();
            Populate?.Invoke(menu, property);
            if (menu.GetItemCount() == count)
                Unavailable(menu);
            StudioFieldColors.Menu(menu, StudioFieldColors.Key(property));
        }

        /// <summary>Gives a UI Toolkit field the same typed clipboard commands as IMGUI controls.</summary>
        /// <typeparam name="T">Field value type.</typeparam>
        /// <param name="field">Existing field receiving the menu.</param>
        /// <param name="accepts">Optional reference or value compatibility check.</param>
        public static void Attach<T>(BaseField<T> field, Func<T, bool> accepts = null)
        {
            // Assigning value dispatches the field's existing change event and preserves its normal workflow.
            field.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                T copied = field.value;
                evt.menu.AppendAction("Copy", action => Clipboard = new StudioPropertyValue(copied,
                    copied is Enum enumeration ? enumeration.GetType() : typeof(T)));
                if (field.enabledInHierarchy && !EditorApplication.isPlayingOrWillChangePlaymode && Clipboard != null
                    && Clipboard.TryRead(out T pasted) && (accepts == null || accepts(pasted)))
                    evt.menu.AppendAction("Paste", action => field.value = pasted);
                else
                    evt.menu.AppendAction("Paste", null, DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Update Same Preset", null, DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Update All", null, DropdownMenuAction.Status.Disabled);
                evt.StopPropagation();
            }));
        }

        /// <summary>Keeps bulk commands visible on fields that do not belong to a prefab interaction.</summary>
        /// <param name="menu">Field menu being assembled.</param>
        public static void Unavailable(GenericMenu menu)
        {
            // Search filters, scene selections and standalone UI values have no matching interaction set.
            menu.AddDisabledItem(new GUIContent("Update Same Preset", "This field has no assigned interaction preset."));
            menu.AddDisabledItem(new GUIContent("Update All", "This field does not map to a prefab interaction."));
        }

        #endregion

        #region Persistence

        /// <summary>Lets the owning tool supply mapped clipboard commands before falling back to raw serialization.</summary>
        /// <param name="menu">Context menu receiving Copy and Paste.</param>
        /// <param name="properties">Complete field selection represented by the menu.</param>
        /// <returns>True when one registered tool supplied both clipboard commands.</returns>
        public static bool TryClipboard(GenericMenu menu, SerializedProperty[] properties)
        {
            if (ClipboardMenu == null)
                return false;
            foreach (Func<GenericMenu, SerializedProperty[], bool> handler in ClipboardMenu.GetInvocationList())
                if (handler(menu, properties))
                    return true;
            return false;
        }

        /// <summary>Captures all registered workspace checks for a delayed field command.</summary>
        /// <param name="owner">Object storing the field.</param>
        /// <param name="path">Serialized field route.</param>
        /// <returns>A check that rejects destroyed owners and changed workspace selections.</returns>
        public static Func<bool> Guard(UnityEngine.Object owner, string path)
        {
            // Tools may retain one draft object while switching the prefab behind it.
            List<Func<bool>> guards = new List<Func<bool>>();
            CollectGuards?.Invoke(owner, path, guards);
            return () => owner != null && guards.TrueForAll(check => check());
        }

        /// <summary>Applies a compatible copied field through Undo and notifies its draft owner.</summary>
        /// <param name="owner">Original serialized owner.</param>
        /// <param name="path">Original field route.</param>
        /// <param name="value">Typed Clipboard snapshot.</param>
        /// <param name="guard">Selection-lifetime check captured when opening the menu.</param>
        private static void Paste(UnityEngine.Object owner, string path, StudioPropertyValue value, Func<bool> guard)
        {
            // Reopen the property after the menu callback instead of using a disposed GUI wrapper.
            if (!guard() || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            using SerializedObject data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(path);
            if (!value.Accepts(property))
                return;
            RecordChange(owner);
            value.Apply(property);
            data.ApplyModifiedProperties();
            Notify(owner);
        }

        /// <summary>Persists a changed draft and schedules native inspector/window repaints.</summary>
        /// <param name="owner">Edited draft, component or asset.</param>
        public static void Notify(UnityEngine.Object owner)
        {
            // Each tool keeps responsibility for its own Apply/Discard policy.
            Changed?.Invoke(owner);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>Records draft ownership before a delayed field or section edit.</summary>
        /// <param name="owner">Draft, component or asset receiving the change.</param>
        public static void RecordChange(UnityEngine.Object owner)
        {
            BeforeChange?.Invoke(owner);
        }

        #endregion

        #endregion
    }
}
